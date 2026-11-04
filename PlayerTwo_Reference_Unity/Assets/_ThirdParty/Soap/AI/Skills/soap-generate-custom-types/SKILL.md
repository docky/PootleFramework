---
name: soap-generate-custom-types
description: "Use this skill whenever the user needs Soap classes for a type that Soap does not ship — e.g. 'make a scriptable variable for my Player class', 'I need a ScriptableEvent that passes an Enemy', 'create a list of WeaponData', 'generate a ScriptableEnum for damage types', 'make a ScriptableSave for my SaveData class', 'create a scriptable singleton for game settings', 'a dictionary from ItemId to int', 'the create menu doesn't have a variable for my type', 'the type creator window'. Covers the Soap Type Creator window (Ctrl+Shift+T): generating {Type}Variable, ScriptableEvent{Type}, EventListener{Type}, ScriptableList{Type}, ScriptableEnum{Type}, ScriptableSave{Type}, ScriptableDictionary and ScriptableSingleton classes from templates, choosing namespace and destination, and what the generated code and menus look like. Do NOT use for creating or using assets of the built-in types (see soap-create-scriptable-variables, soap-create-scriptable-events, soap-create-scriptable-collections) or for save-file behaviour after generation (soap-save-game-data). When in doubt whether a request for 'a variable/event/list of MyType' could involve Soap, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Soap - ScriptableObject Architecture"
  publisher: "Obvious Game"
  asset-version: "3.8.2"
  skill-version: "1.0.0"
  unity: "2019.4+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/utilities"
  asset-store-url: "https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107"
  documentation-url: "https://obvious-game.gitbook.io/soap"
  support-url: "https://discord.gg/CVhCNDbxF5"
  last-verified: "2026-09-12"
---

# Generate Custom Soap Types with the Type Creator

Soap's generic bases (`ScriptableVariable<T>`, `ScriptableEvent<T>`, `EventListenerGeneric<T>`, `ScriptableList<T>`, `ScriptableDictionary<K,V>`, `ScriptableSave<T>`, `ScriptableSingleton<T>`, `ScriptableEnumBase`) must be closed by a concrete, non-generic class before Unity can serialize an asset of them. The **Soap Type Creator** window generates those classes from templates in `Assets/Obvious/Soap/Core/Editor/Resources/Templates/`. This skill lets an agent generate the right classes for any type — by driving the window or by writing the equivalent code directly — and know exactly which files, class names and `Create` menus result.

## When to use this skill

- "make a scriptable variable that holds my `Player` MonoBehaviour"
- "i want an event with an `Enemy` parameter and a listener component for it"
- "create a `ScriptableList<Item>`"
- "create a ScriptableEnum type for `Element` with icon and colour"
- "generate the save class for `SaveData`" (then see `soap-save-game-data`)
- "i need a singleton scriptable object for game parameters"
- "a dictionary keyed by `ScriptableEnumElement` with int values"
- "the Create > Soap menu doesn't have my type"

Not for:
- Using the built-in `FloatVariable`, `ScriptableEventInt`, `ScriptableListGameObject` → sibling skills
- Save/load behaviour, save modes, upgrades → `soap-save-game-data`

## Prerequisites

- Soap 3.8.2, Unity 2019.4+, any render pipeline.
- Install check: the menu `Window > Obvious > 🧼 Soap > Soap Type Creator` exists, or `System.Type.GetType("Obvious.Soap.Editor.SoapTypeCreatorWindow, Obvious.Soap.Editor") != null`, and the folder `Assets/Obvious/Soap/Core/Editor/Resources/Templates` exists. If not, tell the user Soap is not installed (https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107) — see `soap-setup-and-overview`.
- The payload type must be serializable by Unity for its value to show/persist: a `UnityEngine.Object` subclass, or a class/struct marked `[System.Serializable]` (`Obvious.Soap.SoapUtils.IsSerializable(Type)`). The type must be resolvable from the generated file's namespace — the templates add only `using UnityEngine; using Obvious.Soap;` (plus `UnityEngine.Events` for listeners and `System` for save/dictionary), so put your type in the same or global namespace, or edit the generated file to add a `using`.

## Quick start

Goal: `EnemyVariable`, `ScriptableEventEnemy` and `EventListenerEnemy` for an existing `Enemy : MonoBehaviour`.

1. In the Project window select the folder where the scripts should go (e.g. `Assets/Scripts/Soap`).
2. Open `Window > Obvious > 🧼 Soap > Soap Type Creator` (Ctrl+Shift+T; also `Tools > Obvious > 🧼 Soap > Soap Type Creator`).
3. **Namespace**: leave empty or type your namespace (remembered per project). **Type Name**: `Enemy`. The name turns red if invalid (`SoapEditorUtils.IsTypeNameValid`).
4. Under **Soap Classes** tick **Variable** (`EnemyVariable`), **ScriptableEvent** (`ScriptableEventEnemy`) and **EventListener** (`EventListenerEnemy`; only enabled when ScriptableEvent is ticked). Leave the top toggle (base class) unticked because `Enemy` already exists — ticking it would generate `Enemy.cs`.
5. **Destination Folder**: `Selected in Project` (default) or `Custom` with a path.
6. Click **Create**. A progress bar runs, a "Success" dialog says "`Enemy was created!`", scripts compile, and Soap assigns its icons to the new scripts after the assembly reload.

Expected result: files `Assets/Scripts/Soap/EnemyVariable.cs`, `ScriptableEventEnemy.cs`, `EventListenerEnemy.cs` exist; the menus `Assets > Create > Soap > ScriptableVariables > Enemy` and `Soap > ScriptableEvents > Enemy` appear; `Add Component > Soap > EventListeners > EventListenerEnemy` is available; the Console has no compile errors.

## Workflows

### Workflow: Generate via the window (any combination)

**Goal:** Produce one or more Soap classes for `{Type}`.

**Steps:**
1. Open the window (see Quick start) and set Namespace / Type Name.
2. Toggles and what each produces (file written to the destination; `{T}` = type name as typed, `{TN}` = capitalized):

| Toggle | Template | Generated class | Menu it adds |
|---|---|---|---|
| top toggle `{TN}` | `NewTypeTemplate.cs.txt` | `[System.Serializable] public class {T}` | — |
| + **MonoBehaviour** | `NewTypeMonoTemplate.cs.txt` | `public class {T} : MonoBehaviour` | — |
| **Variable** | `ScriptableVariableTemplate.cs.txt` | `{TN}Variable : ScriptableVariable<{T}>` + `{TN}VariableReadOnly` | `Soap/ScriptableVariables/{T}` |
| **ScriptableEvent** | `ScriptableEventTemplate.cs.txt` | `ScriptableEvent{TN} : ScriptableEvent<{T}>` | `Soap/ScriptableEvents/{T}` |
| **EventListener** | `EventListenerTemplate.cs.txt` | `EventListener{TN} : EventListenerGeneric<{T}>` with nested `EventResponse` and `{TN}UnityEvent` | `Add Component > Soap/EventListeners/EventListener{T}` |
| **ScriptableList** | `ScriptableListTemplate.cs.txt` | `ScriptableList{TN} : ScriptableList<{T}>` + `ScriptableList{TN}ReadOnly` | `Soap/ScriptableLists/{T}` |
| **ScriptableSave** | `ScriptableSaveTemplate.cs.txt` | `ScriptableSave{TN} : ScriptableSave<{T}>` | `Soap/ScriptableSaves/ScriptableSave{T}` |
| **ScriptableEnum** | `ScriptableEnumTemplate.cs.txt` | `ScriptableEnum{TN} : ScriptableEnumBase` (disables all other toggles) | `Soap/ScriptableEnums/{T}` |

   For intrinsic names (`int`, `float`, `bool`, `string`, …) Soap uses the `1_*.cs.txt` variants (string literals instead of `nameof`), and base class/enum/save are skipped.
3. Click **Create**; wait for compilation.
4. File naming (from `SoapEditorUtils.CreateClassFromTemplate`): variables → `{TN}Variable.cs`; other Soap classes → template name with `Template` replaced by `{TN}` (e.g. `ScriptableEvent{TN}.cs`); base class → `{TN}.cs`. Creation fails with an "Error — Failed to create {T}" dialog if a file with that name already exists in the destination.

**Expected result:** Listed files exist; `Assets > Create > Soap > …` shows the new entries after compile.

### Workflow: Write the class by hand (headless / no window)

**Goal:** Same result without UI, e.g. when an agent generates code directly.

**Steps:** Create files mirroring the templates exactly:

```csharp
// EnemyVariable.cs
using UnityEngine;
using Obvious.Soap;

[CreateAssetMenu(fileName = "ScriptableVariable" + nameof(Enemy), menuName = "Soap/ScriptableVariables/" + nameof(Enemy))]
public class EnemyVariable : ScriptableVariable<Enemy> { }

[System.Serializable]
public class EnemyVariableReadOnly : ScriptableVariableReadOnly<EnemyVariable, Enemy> { }
```

```csharp
// ScriptableEventEnemy.cs
using System;
using UnityEngine;
using Obvious.Soap;

[CreateAssetMenu(fileName = "ScriptableEvent" + nameof(Enemy), menuName = "Soap/ScriptableEvents/" + nameof(Enemy))]
public class ScriptableEventEnemy : ScriptableEvent<Enemy> { }
```

```csharp
// EventListenerEnemy.cs
using UnityEngine;
using UnityEngine.Events;
using Obvious.Soap;

[AddComponentMenu("Soap/EventListeners/EventListener" + nameof(Enemy))]
public class EventListenerEnemy : EventListenerGeneric<Enemy>
{
    [SerializeField] private EventResponse[] _eventResponses = null;
    protected override EventResponse<Enemy>[] EventResponses => _eventResponses;

    [System.Serializable]
    public class EventResponse : EventResponse<Enemy>
    {
        [SerializeField] private ScriptableEventEnemy _scriptableEvent = null;
        public override ScriptableEvent<Enemy> ScriptableEvent => _scriptableEvent;

        [SerializeField] private EnemyUnityEvent _response = null;
        public override UnityEvent<Enemy> Response => _response;
    }

    [System.Serializable]
    public class EnemyUnityEvent : UnityEvent<Enemy> { }
}
```

```csharp
// ScriptableListEnemy.cs
using UnityEngine;
using Obvious.Soap;

[CreateAssetMenu(fileName = "ScriptableList" + nameof(Enemy), menuName = "Soap/ScriptableLists/" + nameof(Enemy))]
public class ScriptableListEnemy : ScriptableList<Enemy> { }

[System.Serializable]
public class ScriptableListEnemyReadOnly : ScriptableListReadOnly<ScriptableListEnemy, Enemy> { }
```

Optionally wrap in the user's namespace. The shipped concrete types (e.g. `Assets/Obvious/Soap/Core/Runtime/ScriptableEvents/EventListener/EventListenerInt.cs`) are the reference implementation.

**Expected result:** Identical to window output; Soap icons are only auto-assigned when created via the window (`Assets > Soap > 🖼 Set Icon` on the scripts applies them manually).

### Workflow: Generate a ScriptableDictionary type

**Goal:** `ScriptableDictionary<K,V>` subclass.

**Steps:**
1. In the Type Creator click the **ScriptableDictionary** button → popup "Create Scriptable Dictionary Type".
2. Fill **Namespace**, **Name** (default `ScriptableDictionaryKeyValue`), **Key** (default `Key`), **Value** (default `Value`). Tick the class toggle beside Key/Value to also generate `[System.Serializable]` classes for them if they do not exist.
3. Choose destination, click **Create**.
4. Output (`ScriptableDictionaryTemplate.cs.txt`): `{Name} : ScriptableDictionary<{Key},{Value}>` with `[CreateAssetMenu(fileName = nameof({Name}), menuName = "Soap/ScriptableDictionary/" + nameof({Name}))]` and `{Name}ReadOnly : ScriptableDictionaryReadOnly<{Key},{Value},{Name}>`.

**Expected result:** `Assets > Create > Soap > ScriptableDictionary > {Name}` appears. Runtime use: `soap-create-scriptable-collections`.

### Workflow: Generate a ScriptableSingleton and its instance

**Goal:** Global settings object accessible via `MyParams.Instance`.

**Steps:**
1. Type Creator → **ScriptableSingleton** button → popup "Create Scriptable Singleton Type".
2. Set Namespace and class name. Keep **Create Instance at Resource Path** ticked (default path `Assets/Resources`; must be inside a `Resources` folder — `SoapEditorUtils.IsResourcePathValid`).
3. Click **Create**. Output (`ScriptableSingletonTemplate.cs.txt`): `{Name} : ScriptableSingleton<{Name}>` with menu `Soap/ScriptableSingletons/{Name}`. After compilation Soap creates the asset `{ResourcePath}/{Name}.asset` automatically (stored in SessionState until the reload completes).
4. Add fields to the class; access anywhere with `{Name}.Instance`. `Instance` does `Resources.Load<T>(typeof(T).Name)`, then falls back to `Resources.LoadAll<T>("")[0]`, else logs `[Soap] No instance of Singleton:{Name} found in Resources folder.`

**Expected result:** Script + asset exist; `{Name}.Instance` is non-null in Play mode. Example: `Assets/Obvious/Soap/Examples/Content/Scripts/SoapGameParams.cs` (scene 10).

### Workflow: Create a data-carrying ScriptableEnum

**Goal:** Enum-like assets with data (icon, colour, relations).

**Steps:**
1. Generate `ScriptableEnum{TN} : ScriptableEnumBase` (Enum toggle), or write it by hand — `ScriptableEnumBase` is an empty `ScriptableBase` subclass.
2. Add public fields (`Sprite Icon; Color Color; List<ScriptableEnumElement> Defeats;` as in `Assets/Obvious/Soap/Examples/Content/Scripts/ScriptableEnumElement.cs`).
3. Create one asset per enum value via `Assets > Create > Soap > ScriptableEnums > {T}`.

**Expected result:** Assets act as enum members that can be compared by reference and carry data.

## Verification

- Files exist at the destination with the expected names; `AssetDatabase.LoadAssetAtPath<MonoScript>(path).GetClass()` returns the class and `typeof(Obvious.Soap.ScriptableVariableBase).IsAssignableFrom(type)` (or the relevant base) is `true`.
- No compile errors after the assembly reload (`EditorUtility.scriptCompilationFailed == false`).
- The corresponding `Assets > Create > Soap > …` menu entry exists; creating an asset from it yields an object whose `GetGenericType` equals the payload type.
- For singletons: `Resources.Load<{Name}>("{Name}") != null`.
- For listeners: `Add Component > Soap > EventListeners > EventListener{T}` present.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Window > Obvious > 🧼 Soap > Soap Type Creator` (Ctrl+Shift+T) | menu → `Obvious.Soap.Editor.SoapTypeCreatorWindow` | Generates classes from templates. |
| `Obvious.Soap.Editor.SoapDictionaryCreatorPopup` / `SoapSingletonCreatorPopup` | `PopupWindowContent` | Opened from the Type Creator buttons. |
| `Obvious.Soap.ScriptableVariable<T>` / `ScriptableVariableReadOnly<V,T>` | generic bases | Variable + read-only wrapper. |
| `Obvious.Soap.ScriptableEvent<T>` / `EventListenerGeneric<T>` | generic bases | Event + listener. |
| `Obvious.Soap.ScriptableList<T>` / `ScriptableListReadOnly<V,T>` | generic bases | List + wrapper. |
| `Obvious.Soap.ScriptableDictionary<K,V>` / `ScriptableDictionaryReadOnly<K,V,D>` | generic bases | Dictionary + wrapper. |
| `Obvious.Soap.ScriptableEnumBase` | abstract | Base for enum assets. |
| `Obvious.Soap.ScriptableSave<T> where T : class, new()` | generic base | JSON save container (see `soap-save-game-data`). |
| `Obvious.Soap.ScriptableSingleton<T>.Instance` | static property | Resources-based singleton. |
| `Obvious.Soap.SoapUtils.IsSerializable(Type)` / `IsUnityType(Type)` | static | Check payload serializability. |
| `Assets/Obvious/Soap/Core/Editor/Resources/Templates/*.cs.txt` | files | Source of truth for generated code (`$TYPE$`, `$TYPENAME$`, `$KEY$`, `$VALUE$` placeholders). |

## Common issues

- **Symptom:** Type Name shows red / Create disabled. **Cause:** Invalid C# identifier or reserved word, or invalid namespace. **Fix:** Use letters/digits/underscore, not starting with a digit.
- **Symptom:** "Failed to create {T}" dialog. **Cause:** A file with the target name already exists at the destination. **Fix:** Delete/rename the existing file or choose another folder.
- **Symptom:** Generated file does not compile: "The type or namespace name 'Enemy' could not be found". **Cause:** Payload type lives in a namespace not imported by the template. **Fix:** Add `using YourNamespace;` to the generated file or generate inside the same namespace.
- **Symptom:** Variable asset shows "Value field cannot be shown as it is not marked as serializable". **Cause:** Payload class lacks `[System.Serializable]`. **Fix:** Add the attribute.
- **Symptom:** Generated classes have no Soap icon. **Cause:** Written by hand or compile error interrupted the post-reload step. **Fix:** Select the scripts → `Assets > Soap > 🖼 Set Icon`.
- **Symptom:** Singleton asset not created. **Cause:** Compile error after generation or resource path outside a `Resources` folder. **Fix:** Fix errors, then create the asset via `Assets > Create > Soap > ScriptableSingletons > {Name}` inside `Resources`.
- **Symptom:** MonoBehaviour toggle disabled. **Cause:** ScriptableSave is ticked (`AllowMonoBehaviour => !_save`) — save data must be a plain class. **Fix:** Untick Save or Mono.

## Boundaries

- The generator writes only the template shapes above; it does not add fields, validation or custom drawers — edit the generated file afterwards.
- Generic or nested types cannot be entered as Type Name; declare a concrete `[Serializable]` wrapper class first.
- `ScriptableDictionary` edit-time serialization depends on Odin Inspector or a Unity version that serializes `Dictionary<K,V>`.
- Generated code is standard C#; it can be committed and edited freely, but keep the class names if you rely on the shipped `Create` menu paths.


