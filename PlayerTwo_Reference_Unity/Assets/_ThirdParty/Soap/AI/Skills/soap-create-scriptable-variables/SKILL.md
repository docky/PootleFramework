---
name: soap-create-scriptable-variables
description: "Use this skill whenever the user wants shared state as a ScriptableObject variable with Soap — e.g. 'make a health variable', 'create a float variable for player speed', 'store the score in a scriptable object', 'let two scripts share a value without references', 'react when the value changes', 'clamp health between 0 and 100', 'keep this value between scenes', 'save this variable to playerprefs', 'why does my variable reset when I stop playing', 'use a FloatReference so I can override locally'. Covers creating FloatVariable/IntVariable/BoolVariable/StringVariable/Vector/Color/GameObject variables from menus or code, reading and writing Value, subscribing to OnValueChanged, clamping, ResetTiming, PlayerPrefs saving, *Reference and *ReadOnly wrappers and sub-assets. Do NOT use for event assets (see soap-create-scriptable-events), lists/dictionaries (soap-create-scriptable-collections), variables of custom types (soap-generate-custom-types) or UI display (soap-bind-ui). When in doubt whether a shared-value request could involve Soap, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Soap - ScriptableObject Architecture"
  publisher: "Obvious Game"
  asset-version: "3.8.2"
  skill-version: "1.0.0"
  unity: "2019.4+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/utilities"
  asset-store-url: "https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107"
  documentation-url: "https://obvious-game.gitbook.io/soap/soap-core-assets/scriptable-variable"
  support-url: "https://discord.gg/CVhCNDbxF5"
  last-verified: "2026-09-12"
---

# Create and Use Scriptable Variables with Soap

A Soap scriptable variable is a ScriptableObject asset (`Obvious.Soap.ScriptableVariable<T>` subclass) that holds one value, exposes it through `Value`, raises `OnValueChanged` when it changes, optionally clamps it, optionally persists it to PlayerPrefs, and resets to its pre-play value when Play mode ends. This skill lets an agent create variable assets, wire them into MonoBehaviours, subscribe to changes, and configure clamping, reset timing and saving.

## When to use this skill

- "create a float variable called PlayerHealth starting at 100"
- "i want the score to live in a scriptableobject so the UI and the game manager both see it"
- "make the health clamp between 0 and max health"
- "run this method when the coin count changes"
- "the variable goes back to its old value when I stop the game — how do I keep it?"
- "save the high score between sessions" (PlayerPrefs, single value)
- "let me override the speed per prefab but default to the shared variable" (`FloatReference`)
- "expose the health read-only to other scripts" (`FloatVariableReadOnly`)

Not for:
- Firing one-shot notifications → `soap-create-scriptable-events`
- Groups of items → `soap-create-scriptable-collections`
- A variable of your own type (`PlayerVariable`, `EnemyStatsVariable`) → `soap-generate-custom-types`
- Displaying the value in Text/Slider/Image → `soap-bind-ui`
- Structured multi-field save files → `soap-save-game-data`

## Prerequisites

- Soap 3.8.2 (any 3.x with `Obvious.Soap.ScriptableVariable<T>` works for the core API), Unity 2019.4+, any render pipeline.
- Install check: `System.Type.GetType("Obvious.Soap.FloatVariable, Obvious.Soap") != null` and the menu `Assets > Create > Soap > ScriptableVariables > float` exists. If either fails, tell the user Soap is not installed and point to https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107 (see `soap-setup-and-overview`).

Built-in variable classes (all in namespace `Obvious.Soap`, all `[CreateAssetMenu]` under `Soap/ScriptableVariables/`):

| Class | Menu item | Extra API |
|---|---|---|
| `BoolVariable` | `bool` | `Toggle()` |
| `IntVariable` | `int` | `IsClamped`, `MinReference`, `MaxReference`, `Min`, `Max`, `Ratio`, `Add(int)` |
| `FloatVariable` | `float` | `IsClamped`, `MinReference`, `MaxReference`, `Min`, `Max`, `Ratio`, `Add(float)` |
| `StringVariable` | `string` | — |
| `ColorVariable` | `color` | `SetRandom()` |
| `Vector2Variable` / `Vector2IntVariable` / `Vector3Variable` / `QuaternionVariable` | `Vector2` / `Vector2Int` / `Vector3` / `Quaternion` | — |
| `GameObjectVariable` / `ComponentVariable` | `GameObject` / `Component` | not saveable |

## Quick start

Goal: a `FloatVariable` named `PlayerHealth`, default 100, clamped 0–100, read and written from a script.

1. In the Project window select the target folder, then `Assets > Create > Soap > ScriptableVariables > float`. Name the asset `PlayerHealth`. (Default file name is `scriptable_variable_float`; with `SoapSettings.NamingOnCreationMode = Manual` a naming popup appears instead.)
2. In the Inspector set **Value** = `100`, tick **Is Clamped**, set **Min** = `0`, **Max** = `100`. Leave **Saved** off and **Reset On** = `OnPlayStart`.
3. Or do the same from an Editor script:

```csharp
using UnityEditor;
using UnityEngine;
using Obvious.Soap;
using Obvious.Soap.Editor;

public static class CreatePlayerHealth
{
    [MenuItem("Tools/Examples/Create PlayerHealth Variable")]
    public static void Create()
    {
        var so = SoapEditorUtils.CreateScriptableObject(typeof(FloatVariable), "PlayerHealth", "Assets/Data/Variables");
        var health = (FloatVariable)so;
        health.IsClamped = true;
        health.MinReference = new FloatReference(0f, true);
        health.MaxReference = new FloatReference(100f, true);
        health.Value = 100f;          // in edit mode this writes the serialized _value
        EditorUtility.SetDirty(health);
        AssetDatabase.SaveAssets();
    }
}
```

4. Use it from a MonoBehaviour:

```csharp
using UnityEngine;
using Obvious.Soap;

public class DamageOnKey : MonoBehaviour
{
    [SerializeField] private FloatVariable _health;

    private void OnEnable()  => _health.OnValueChanged += OnHealthChanged;
    private void OnDisable() => _health.OnValueChanged -= OnHealthChanged;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            _health.Add(-10f);            // same as _health.Value -= 10f, clamped
    }

    private void OnHealthChanged(float newValue)
    {
        Debug.Log($"Health {_health.PreviousValue} -> {newValue}");
        if (_health <= 0f)                 // implicit conversion to float
            Debug.Log("Dead");
    }
}
```

5. Assign `PlayerHealth` to the `_health` field (or click the **Create** button Soap draws next to an empty Soap field — it creates the asset in the selected/custom folder).

Expected result: `Assets/Data/Variables/PlayerHealth.asset` exists (type `FloatVariable`); in Play mode pressing Space lowers the Inspector value by 10, never below 0; on exiting Play mode the value returns to 100.

## Workflows

### Workflow: Create a variable asset from code

**Goal:** Create any built-in variable at a known path without the menu.

**Steps:**
1. Call `Obvious.Soap.Editor.SoapEditorUtils.CreateScriptableObject(Type type, string name, string path, bool ping = true)`. `path` may be `Assets/Sub/Dir` or `Sub/Dir`; missing folders are created. A unique path is generated if the name is taken.
2. Cast to the concrete type and set public properties (`Value`, `IsClamped`, `MinReference`, `MaxReference`).
3. For serialized-only fields, use `SerializedObject` with these property names: `_value`, `_debugLogEnabled`, `_saved`, `_defaultValue`, `_resetOn`, `_saveGuid` (`Auto`/`Manual`), `_guid`. Call `ApplyModifiedProperties()`.
4. `EditorUtility.SetDirty(asset); AssetDatabase.SaveAssets();`

**Expected result:** The `.asset` exists at the path and `AssetDatabase.LoadAssetAtPath<FloatVariable>(path)` returns it with the configured values.

### Workflow: Read, write and subscribe from a script

**Goal:** Use the variable as shared state between unrelated scripts.

**Steps:**
1. Declare `[SerializeField] private IntVariable _score;` and assign the asset.
2. Read `_score.Value` or rely on the implicit conversion (`int s = _score;`).
3. Write `_score.Value = 10;` — the setter is a no-op when the value is equal (`EqualityComparer<T>.Default`) and otherwise calls `ValueChanged(true)` which saves (if `_saved`), logs (if debug), invokes `OnValueChanged`, then updates `PreviousValue`.
4. Use `_score.SetValueWithoutNotify(v)` to change the value without raising `OnValueChanged`.
5. Subscribe/unsubscribe in `OnEnable`/`OnDisable` (or `Awake`/`OnDestroy`): `_score.OnValueChanged += Handler;` where `Handler(int newValue)`.
6. Tick **Debug Log Enabled** on the asset to log every change as `[Variable] Name : Value`.

**Expected result:** Every distinct write triggers exactly one `OnValueChanged` callback; equal writes trigger none.

### Workflow: Clamp a numeric variable

**Goal:** Keep `IntVariable`/`FloatVariable` within bounds.

**Steps:**
1. Tick **Is Clamped**; set **Min**/**Max**. Both are `FloatReference`/`IntReference` (defaults `0` and `float.MaxValue`/`int.MaxValue`) so they can either be local constants or point to another variable (e.g. `MaxHealth`) — untick **Use Local** on the reference and drag the variable.
2. `Value` setter clamps with `Mathf.Clamp(value, Min, Max)`. `Ratio` returns `Mathf.InverseLerp(Min, Max, Value)` — use it for fill bars.

**Expected result:** Writes outside the range are clamped; `Ratio` is in `[0,1]`.

### Workflow: Control reset behaviour (ResetTiming)

**Goal:** Decide when the runtime value returns to its initial value.

**Steps:** Set **Reset On** (`_resetOn`, enum `Obvious.Soap.ResetTiming`):
- `OnPlayStart` (default for variables) — value captured on Play start, restored when Play ends. Persists across scene loads during play.
- `OnSceneUnload` — additionally resets (or reloads if saved) when the active scene is unloaded. Additive scene loads do not trigger it.
- `Never` — no automatic reset. Note: in the Editor, non-saved variables still revert to the pre-play `_value` on `EnteredEditMode` because Play mode writes go to `_runtimeValue`, not the serialized `_value`.

Also available: right-click the asset Inspector header → `Reset Value` (`CONTEXT/ScriptableVariableBase/Reset Value`) or call `variable.ResetValue()`.

**Expected result:** Value persists or resets exactly at the chosen moment.

### Workflow: Persist a variable with PlayerPrefs

**Goal:** Keep a single value between sessions (high score, settings toggle).

**Steps:**
1. Tick **Saved** (`_saved`). The Inspector reveals **Default Value** (used the first time `Load()` finds no key), **Save Guid** (`Auto`/`Manual`) and **Guid**.
2. Leave **Save Guid** = `Auto`: `ScriptableVariableGuidGenerator` (an `AssetPostprocessor`) sets `Guid` from the asset's GUID on import/create/move. Choose `Manual` only if you need a stable custom key.
3. Behaviour: on Play start `Init()` calls `Load()`; each write calls `Save()` (`PlayerPrefs.SetFloat/SetInt/SetString(Guid, Value)`; bool stored as 0/1; Color as `_r/_g/_b/_a`; Vector3 as `_x/_y/_z`). On exiting Play the last runtime value is written into the serialized `_value`.
4. To wipe saved values: `Tools > Obvious > 🧼 Soap > Delete Player Pref` (Ctrl+Shift+D) → `PlayerPrefs.DeleteAll()`.

**Expected result:** After stopping and restarting Play, the value equals the last value written, not the serialized default. `GameObjectVariable`, `ComponentVariable` and `QuaternionVariable` cannot be saved (no `Save/Load` override).

### Workflow: Use a *Reference for optional local override

**Goal:** A field that can be a constant or a shared variable, decided in the Inspector.

**Steps:**
1. Declare `[SerializeField] private FloatReference _maxSpeed = new FloatReference(5f, true);` (`(T initialValue, bool useLocal = false)`). Available: `BoolReference`, `IntReference`, `FloatReference`, `StringReference`, `ColorReference`, `Vector2Reference`, `Vector2IntReference`, `Vector3Reference` (all `Obvious.Soap`).
2. In the Inspector toggle **Use Local** to type a value, or untick it and drag a variable into **Variable**.
3. Read `_maxSpeed.Value` (or implicit conversion); `OnValueChanged` fires for both modes.

**Expected result:** Same script works with per-instance constants and shared variables.

### Workflow: Expose a variable read-only

**Goal:** Let a script read and subscribe without being able to write.

**Steps:** Declare `[SerializeField] private FloatVariableReadOnly _health;` (also `BoolVariableReadOnly`, `IntVariableReadOnly`, `StringVariableReadOnly`, `ColorVariableReadOnly`, `Vector3VariableReadOnly`, …). Drag the `FloatVariable` asset into it. Use `_health.Value`, `_health.OnValueChanged`, and for numeric types `Min`, `Max`, `Ratio`.

**Expected result:** The consuming script compiles with no setter available.

### Workflow: Group variables as sub-assets of a container

**Goal:** Keep related variables (e.g. `PlayerStats`) inside one ScriptableObject asset.

**Steps:**
1. Create a plain `ScriptableObject` class with fields such as `[SubAsset] public FloatVariable Health;` (`Obvious.Soap.Attributes.SubAssetAttribute`).
2. Create the container asset, then right-click its Inspector header → `🧼 Create Soap ScriptableObjects` (`CONTEXT/Object/🧼 Create Soap ScriptableObjects`). Every null Soap field is created; `[SubAsset]` fields become sub-assets named `{Prefix}{fieldName}`, others become standalone assets.
3. Manage with `Assets > Soap > Delete SubAsset` / `Delete All SubAssets` and the Inspector `Select Parent` context item.

**Expected result:** Expanding the container asset in the Project window shows the child variables; sub-asset GUIDs derive from the parent GUID, so saving still works.

## Verification

- Asset exists: `AssetDatabase.LoadAssetAtPath<Obvious.Soap.FloatVariable>("Assets/…/Name.asset") != null`.
- Values: `variable.Value`, `variable.IsClamped`, `variable.Min`, `variable.Max` match the request (edit mode reads serialized `_value`).
- Saved flag: `new SerializedObject(variable).FindProperty("_saved").boolValue`; `variable.Guid` is non-empty when saved with `Auto`.
- Scene wiring: the consuming component's serialized field references the asset (`SerializedObject.FindProperty("_health").objectReferenceValue == variable`).
- Behaviour: in Play mode a write changes `Value` and the Inspector repaints; `OnValueChanged` fired once per distinct write; Console clean.
- After exiting Play mode, non-saved variables show their pre-play value.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Obvious.Soap.ScriptableVariable<T>.Value` | property `T` | Get/set; raises `OnValueChanged` on change. |
| `.OnValueChanged` | `event Action<T>` | Subscribe for change callbacks. |
| `.PreviousValue` / `.DefaultValue` | `T` | Value before the last change / saved-mode default. |
| `.SetValueWithoutNotify(T)` | method | Write silently. |
| `.ResetValue()` | method | Return to the value captured at Play start. |
| `.Save()` / `.Load()` | virtual methods | PlayerPrefs I/O (overridden per concrete type). |
| `.ResetTiming` | `Obvious.Soap.ResetTiming` | `OnSceneUnload`, `OnPlayStart`, `Never`. |
| `ScriptableVariableBase.Guid` / `.SaveGuid` | `string` / `SaveGuidType` | PlayerPrefs key and how it is generated. |
| `FloatVariable`/`IntVariable`: `IsClamped`, `MinReference`, `MaxReference`, `Min`, `Max`, `Ratio`, `Add(x)` | members | Clamping helpers. |
| `Obvious.Soap.FloatReference(float, bool useLocal = false)` etc. | serializable class | Local-or-variable value; `UseLocal`, `LocalValue`, `Variable`, `Value`, `OnValueChanged`. |
| `Obvious.Soap.FloatVariableReadOnly` etc. | serializable class | Read-only wrapper around a variable asset. |
| `Obvious.Soap.Editor.SoapEditorUtils.CreateScriptableObject(Type, string, string, bool)` | static | Editor-side asset creation. |
| `Obvious.Soap.SoapRuntimeUtils.CreateRuntimeInstance<T>(string name)` | static | Runtime-only variable instance (sets `ResetTiming.Never`), for per-instance data; see `RuntimeVariableInjector<T>` in `Core/Runtime/RuntimeInjectors` and example scene 8. |

## Common issues

- **Symptom:** Value changes made during Play are lost when Play stops. **Cause:** By design — Play writes go to `_runtimeValue`; `EnteredEditMode` restores `_value` for non-saved variables. **Fix:** Tick **Saved** (value is then copied back and persisted), or set the desired default in edit mode.
- **Symptom:** `OnValueChanged` never fires. **Cause:** Writing the same value (setter early-outs), or using `SetValueWithoutNotify`, or subscribing after the write. **Fix:** Subscribe in `OnEnable`/`Awake`; ensure the value actually differs.
- **Symptom:** Saved value does not persist. **Cause:** `Guid` empty (asset created outside the AssetDatabase pipeline) or type without `Save/Load` (`GameObjectVariable`, `ComponentVariable`, `QuaternionVariable`). **Fix:** Reimport the asset so `ScriptableVariableGuidGenerator` assigns the GUID, or set **Save Guid** = `Manual` and type a key.
- **Symptom:** Inspector error "Do not use ScriptableVariable<float> directly…". **Cause:** Field typed as the open generic. **Fix:** Use `FloatVariable`.
- **Symptom:** Field shows "…Value field cannot be shown as it is not marked as serializable". **Cause:** Custom `T` lacks `[System.Serializable]`. **Fix:** Add the attribute (see `soap-generate-custom-types`).
- **Symptom:** Variable resets on every additive scene load / does not reset on scene change. **Cause:** `ResetTiming.OnSceneUnload` only reacts to the active scene handle. **Fix:** Choose `OnPlayStart` or handle reset manually with `ResetValue()`.
- **Symptom:** Asset created with the Inspector **Create** button landed in the wrong folder. **Cause:** `SoapSettings.CreatePathMode` = `Auto` uses the folder selected in the Project window. **Fix:** Select the folder first or switch to `Manual` with a custom path.

## Boundaries

- Variables hold exactly one value; use `soap-create-scriptable-collections` for many, `soap-save-game-data` for structured save files.
- PlayerPrefs saving is per-variable and unencrypted; not suitable for large or sensitive data.
- Custom-typed variables require a generated concrete subclass (`soap-generate-custom-types`); the open generic cannot be serialized by Unity.
- Runtime instances created with `SoapRuntimeUtils.CreateRuntimeInstance<T>` are not assets and are destroyed with the scene; they exist for per-object data only.
- Soap does not synchronise variables over the network or between processes.

