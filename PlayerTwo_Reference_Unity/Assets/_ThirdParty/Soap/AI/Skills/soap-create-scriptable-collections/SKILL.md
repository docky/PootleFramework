---
name: soap-create-scriptable-collections
description: "Use this skill whenever the user wants a shared, observable collection as a ScriptableObject with Soap — e.g. 'keep a list of all enemies alive', 'track spawned players in a scriptable list', 'a runtime registry every script can read', 'react when something is added or removed from the list', 'count how many items are in the list', 'clear the list when the scene changes', 'a scriptable dictionary mapping element to damage', 'my list still has stuff from the last scene'. Covers creating ScriptableListGameObject / ScriptableListComponent / ScriptableListVector3 assets, Add/TryAdd/Remove/ForEach/SetItems, OnItemAdded/OnItemRemoved/Modified events, ResetTiming, ScriptableDictionary<K,V> usage and read-only wrappers. Do NOT use for single values (see soap-create-scriptable-variables), one-shot notifications (soap-create-scriptable-events) or for generating a list/dictionary class of your own type (soap-generate-custom-types). When in doubt whether a 'list', 'registry', 'dictionary' or 'collection of objects' request could involve Soap, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Soap - ScriptableObject Architecture"
  publisher: "Obvious Game"
  asset-version: "3.8.2"
  skill-version: "1.0.0"
  unity: "2019.4+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/utilities"
  asset-store-url: "https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107"
  documentation-url: "https://obvious-game.gitbook.io/soap/soap-core-assets/scriptable-list"
  support-url: "https://discord.gg/CVhCNDbxF5"
  last-verified: "2026-09-12"
---

# Manage Scriptable Lists and Dictionaries with Soap

Soap collections are ScriptableObject assets that hold many items and notify subscribers when the contents change: `Obvious.Soap.ScriptableList<T>` (implements `IList<T>`) and `Obvious.Soap.ScriptableDictionary<K,V>` (implements `IDictionary<K,V>`). Both derive from `Obvious.Soap.ScriptableCollection`, which clears them on Play start/end and optionally on scene unload. This skill lets an agent create list assets, register/unregister objects at runtime, iterate safely, react to changes, and control reset behaviour.

## When to use this skill

- "make a scriptable list that every spawned enemy adds itself to"
- "the UI should show how many players are in the game — use a list"
- "do something whenever an item is added to the list"
- "loop over all objects in the list and damage them"
- "the list keeps items from the previous scene, clear it on scene load"
- "map an element enum asset to an int with a scriptable dictionary"
- "expose the list read-only"

Not for:
- One value → `soap-create-scriptable-variables`
- Fire-and-forget messages → `soap-create-scriptable-events`
- A `ScriptableList<Enemy>` / dictionary class for your own type → `soap-generate-custom-types` (then come back here for usage)

## Prerequisites

- Soap 3.8.2, Unity 2019.4+, any render pipeline.
- Install check: `System.Type.GetType("Obvious.Soap.ScriptableListGameObject, Obvious.Soap") != null` and the menu `Assets > Create > Soap > ScriptableLists > GameObject` exists. If not, tell the user Soap is not installed (https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107) — see `soap-setup-and-overview`.
- Built-in list assets (namespace `Obvious.Soap`, menu `Assets > Create > Soap > ScriptableLists > …`): `ScriptableListGameObject` (`GameObject`), `ScriptableListComponent` (`Component`), `ScriptableListVector3` (`Vector3`). No built-in dictionary assets ship — generate one (see Workflow: Dictionary).

## Quick start

Goal: an `ActiveEnemies` list that enemies join on enable and leave on disable, with a counter that logs the count.

1. Select a folder, `Assets > Create > Soap > ScriptableLists > GameObject`, name it `ActiveEnemies`. Or:

```csharp
Obvious.Soap.Editor.SoapEditorUtils.CreateScriptableObject(
    typeof(Obvious.Soap.ScriptableListGameObject), "ActiveEnemies", "Assets/Data/Lists");
```

2. Enemy script:

```csharp
using UnityEngine;
using Obvious.Soap;

public class EnemyRegistrar : MonoBehaviour
{
    [SerializeField] private ScriptableListGameObject _activeEnemies;

    private void OnEnable()  => _activeEnemies.TryAdd(gameObject);   // no duplicates
    private void OnDisable() => _activeEnemies.Remove(gameObject);
}
```

3. Counter script:

```csharp
using UnityEngine;
using Obvious.Soap;

public class EnemyCounter : MonoBehaviour
{
    [SerializeField] private ScriptableListGameObject _activeEnemies;

    private void OnEnable()
    {
        _activeEnemies.OnItemAdded   += OnChanged;
        _activeEnemies.OnItemRemoved += OnChanged;
    }
    private void OnDisable()
    {
        _activeEnemies.OnItemAdded   -= OnChanged;
        _activeEnemies.OnItemRemoved -= OnChanged;
    }
    private void OnChanged(GameObject _) => Debug.Log($"Enemies: {_activeEnemies.Count}");
}
```

4. Assign `ActiveEnemies` to both fields; place several enemies in the scene; press Play.

Expected result: the list asset's Inspector shows the live items; the Console logs `Enemies: N` on each add/remove; on exiting Play mode the list is empty (collections `Clear()` on `EnteredEditMode`).

## Workflows

### Workflow: Create a list asset

**Goal:** Create a list of a built-in type.

**Steps:**
1. Menu `Assets > Create > Soap > ScriptableLists > {GameObject|Component|Vector3}` (default file names `scriptable_list_gameObject`, etc.), or `SoapEditorUtils.CreateScriptableObject(typeof(ScriptableListComponent), name, path)`.
2. Optional Inspector field **Reset On** (`_resetOn`, default `OnSceneUnload`).

**Expected result:** Asset exists; `AssetDatabase.LoadAssetAtPath<ScriptableListGameObject>(path).Count == 0`.

### Workflow: Add, remove and query

**Goal:** Mutate the list correctly and cheaply.

**Steps (API of `ScriptableList<T>`):**
- `Add(T)` — allows duplicates. `TryAdd(T)` — adds only if absent, returns `bool`. `Insert(int, T)`, `AddRange(IEnumerable<T>)`, `TryAddRange(IEnumerable<T>)`.
- `Remove(T)` (returns `bool`, only if present), `RemoveAt(int)`, `RemoveRange(int index, int count)`, `Clear()`.
- `SetItems(IEnumerable<T>)` — replace all items, raising `OnItemsRemoved` then `OnItemsAdded` once.
- `Contains(T)` is O(1) (internal count dictionary), `IndexOf(T)`, indexer `list[i]` (setter raises `OnItemRemoved` for the old item and `OnItemAdded` for the new), `Count`, `IsEmpty`.
- `ForEach(Action<T>)` iterates **backwards**, so removing the current item inside the callback is safe. Standard `foreach` is also available (`IEnumerable<T>`) but must not mutate.

**Expected result:** `Count` and Inspector reflect every change immediately.

### Workflow: React to changes

**Goal:** Update UI or logic when the list changes.

**Steps:** Subscribe to the events that fit:
- `OnItemAdded` / `OnItemRemoved` (`Action<T>`) — single-item operations.
- `OnItemsAdded` / `OnItemsRemoved` (`Action<IEnumerable<T>>`) — `AddRange`/`TryAddRange`/`SetItems`/`RemoveRange`.
- `Modified` (`Action` field on `ScriptableCollection`) — any change including `Clear`. `OnCleared` (`event Action`) — on `Clear()`.
- `OnItemCountChanged` is `[Obsolete("Use Modified instead")]`.

Unsubscribe in `OnDisable`/`OnDestroy`.

**Expected result:** Exactly one callback per operation (range operations raise the plural events once).

### Workflow: Control when the collection clears (ResetTiming)

**Goal:** Prevent stale items.

**Steps:** Set **Reset On** (`Obvious.Soap.ResetTiming`):
- `OnSceneUnload` (default) — cleared in `OnEnable`, on Play start/end, and when the active scene is unloaded (additive scenes do not trigger it).
- `OnPlayStart` — cleared on Play start/end only; contents persist across scene loads during Play.
- `Never` — never cleared automatically (also skips the `OnEnable` clear).

Manual: `list.Clear()` or Inspector header `⋮ > Clear` (`CONTEXT/ScriptableCollection/Clear`).

**Expected result:** Items survive or vanish exactly at the configured moment.

### Workflow: Use a ScriptableDictionary

**Goal:** Key/value lookups shared across scripts.

**Steps:**
1. No built-in concrete dictionary exists. Generate one: `Window > Obvious > 🧼 Soap > Soap Type Creator` → **ScriptableDictionary** button → set Namespace, Name (default `ScriptableDictionaryKeyValue`), Key type, Value type (tick the class toggles to also generate empty key/value classes) → **Create**. The template produces `public class {Name} : ScriptableDictionary<{Key},{Value}>` with `[CreateAssetMenu(menuName = "Soap/ScriptableDictionary/{Name}")]` plus `{Name}ReadOnly`. Details in `soap-generate-custom-types`.
2. Create the asset from `Assets > Create > Soap > ScriptableDictionary > {Name}`.
3. API (`ScriptableDictionary<T,V>`): `Add(key, value)`, `TryAdd(key, value)`, `Remove(key)`, `TryGetValue(key, out value)`, `ContainsKey`, `ContainsValue`, indexer (setter raises `Modified` only), `Keys`, `Values`, `Count`, `IsEmpty`, `Clear()`; events `OnItemAdded(T,V)`, `OnItemRemoved(T,V)`, `Modified`, `OnCleared`.
4. Serialization: the backing field is a plain `Dictionary<T,V>`, which Unity does not serialize by default. Edit-time contents are shown/kept only with Odin Inspector (`ODIN_INSPECTOR` define) or a Unity version that serializes dictionaries; otherwise fill it at runtime. The example `ScriptableDictionaryElementInt : ScriptableDictionary<ScriptableEnumElement,int>` in `Assets/Obvious/Soap/Examples/Content/Scripts/` shows the pattern (scene 9).

**Expected result:** Lookups work at runtime; `Count` matches adds minus removes.

### Workflow: Expose a collection read-only

**Goal:** Consumers can read and subscribe but not mutate.

**Steps:** Generated list classes include `{Name}ReadOnly : ScriptableListReadOnly<{ListType},{T}>`; dictionaries include `{Name}ReadOnly : ScriptableDictionaryReadOnly<K,V,{DictType}>`. Declare a field of the read-only type and drag the asset in. Available: `Count`, `IsEmpty`, indexer get, `Contains`, `IndexOf`, `ForEach`, `Modified`, `OnItemAdded/Removed`, `OnItemsAdded/Removed` (dictionary: `TryGetValue`, `ContainsKey`, `ContainsValue`, `Keys`, `Values`). Built-in lists have no shipped read-only wrapper; create one with `public class GameObjectListReadOnly : Obvious.Soap.ScriptableListReadOnly<Obvious.Soap.ScriptableListGameObject, UnityEngine.GameObject> {}` marked `[System.Serializable]`.

**Expected result:** Compile-time protection against writes.

## Verification

- Asset exists and is the expected type (`t:ScriptableListGameObject` search or `LoadAssetAtPath`).
- Registrar/consumer components reference the asset (`SerializedObject.FindProperty("_activeEnemies").objectReferenceValue`).
- Play mode: `list.Count` equals the number of active registered objects; disabling one decrements; `Contains(go)` true while registered.
- Events: one `OnItemAdded` per add, one `OnItemRemoved` per remove.
- After exiting Play mode: `Count == 0` unless `ResetTiming.Never`.
- Console clean (no `ArgumentException` from `Dictionary.Add` duplicate keys — use `TryAdd`).

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Obvious.Soap.ScriptableList<T>` | abstract `ScriptableListBase`, `IList<T>` | `Add`, `TryAdd`, `Insert`, `AddRange`, `TryAddRange`, `SetItems`, `Remove`, `RemoveAt`, `RemoveRange`, `Clear`, `Contains`, `IndexOf`, `ForEach`, `Count`, `IsEmpty`, indexer. |
| `.OnItemAdded` / `.OnItemRemoved` | `event Action<T>` | Single-item change. |
| `.OnItemsAdded` / `.OnItemsRemoved` | `event Action<IEnumerable<T>>` | Range change. |
| `Obvious.Soap.ScriptableCollection.Modified` / `.OnCleared` | `Action` field / `event Action` | Any change / clear. |
| `ScriptableCollection.ResetValue()` | method | Same as `Clear()`. |
| `Obvious.Soap.ScriptableListGameObject` / `ScriptableListComponent` / `ScriptableListVector3` | concrete assets | Built-in lists. |
| `Obvious.Soap.ScriptableDictionary<T,V>` | abstract, `IDictionary<T,V>` | `Add`, `TryAdd`, `Remove`, `TryGetValue`, `ContainsKey`, `ContainsValue`, `Keys`, `Values`, `Clear`; `OnItemAdded(T,V)`, `OnItemRemoved(T,V)`. |
| `Obvious.Soap.ScriptableListReadOnly<V,T>` / `ScriptableDictionaryReadOnly<K,V,D>` | serializable wrappers | Read-only access. |
| `Obvious.Soap.Editor.SoapEditorUtils.CreateScriptableObject(Type, string, string, bool)` | static | Create the asset from Editor code. |

## Common issues

- **Symptom:** List contains destroyed (`null`) GameObjects. **Cause:** Objects destroyed without `Remove`. **Fix:** Remove in `OnDisable`/`OnDestroy`; use `ForEach` to prune safely.
- **Symptom:** Items from the previous scene persist. **Cause:** `ResetTiming.OnPlayStart` or `Never`, or the scene was loaded additively (active-scene handle unchanged). **Fix:** Use `OnSceneUnload` with a non-additive load, or call `Clear()` on scene transition.
- **Symptom:** Duplicate entries. **Cause:** `Add` permits duplicates. **Fix:** Use `TryAdd`/`TryAddRange`.
- **Symptom:** `InvalidOperationException: Collection was modified` during iteration. **Cause:** Mutating inside `foreach`. **Fix:** Use `ForEach` (reverse iteration) or iterate a copy.
- **Symptom:** Dictionary is empty in Play mode although filled in the Inspector. **Cause:** `Dictionary<K,V>` not serialized without Odin/newer Unity. **Fix:** Populate at runtime or install Odin Inspector.
- **Symptom:** Inspector warning that the item type is not serializable / value cannot be shown. **Cause:** `CanBeSerialized()` false — custom `T` lacks `[System.Serializable]`. **Fix:** Add the attribute or use a `UnityEngine.Object`-derived type.
- **Symptom:** Menu `Soap > ScriptableLists` lacks the type I need. **Cause:** Only GameObject/Component/Vector3 ship. **Fix:** Generate a class with `soap-generate-custom-types`.

## Boundaries

- Collections are runtime registries; they are cleared on Play start/end and are not a persistence mechanism (use `soap-save-game-data`).
- Edit-mode list contents are serialized (`List<T>`) but dictionaries are not without Odin/newer Unity.
- No sorting, filtering or LINQ helpers beyond `IList<T>`/`IDictionary<K,V>`; use System.Linq on the collection.
- No thread safety and no network replication.

