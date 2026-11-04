---
name: soap-create-scriptable-events
description: "Use this skill whenever the user wants decoupled messaging with Soap scriptable events — e.g. 'fire an event when the player dies', 'create an OnEnemyKilled event', 'make a scriptableobject event with an int payload', 'listen to the event from the inspector without code', 'call this method when the event is raised', 'add a delay before the response', 'raise the event from a button', 'which event calls this method', 'my event listener isn't firing'. Covers creating ScriptableEventNoParam / ScriptableEventInt / Float / Bool / String / Vector / Color / GameObject / Component assets, adding EventListener* components with UnityEvent responses and delays, raising and subscribing from code, enabling Raise in the Editor, debug logging and the Events Debug Window. Do NOT use for shared values that change over time (see soap-create-scriptable-variables), events carrying your own type (soap-generate-custom-types) or collections (soap-create-scriptable-collections). When in doubt whether an 'event', 'message', 'broadcast' or 'trigger' request could involve Soap, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Soap - ScriptableObject Architecture"
  publisher: "Obvious Game"
  asset-version: "3.8.2"
  skill-version: "1.0.0"
  unity: "2019.4+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/utilities"
  asset-store-url: "https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107"
  documentation-url: "https://obvious-game.gitbook.io/soap/soap-core-assets/scriptable-event"
  support-url: "https://discord.gg/CVhCNDbxF5"
  last-verified: "2026-09-12"
---

# Raise and Listen to Scriptable Events with Soap

A Soap scriptable event is a ScriptableObject asset (`Obvious.Soap.ScriptableEvent<T>` or `Obvious.Soap.ScriptableEventNoParam`) that any script can `Raise()` and any script or `EventListener*` component can respond to, without the two knowing each other. This skill lets an agent create event assets, wire Inspector-only listeners with `UnityEvent` responses and optional delays, subscribe from code, and debug which listeners react.

## When to use this skill

- "create a PlayerDied event and play a sound when it fires"
- "i need an event that passes the damage amount (int)"
- "hook this UI panel to open when OnLevelComplete is raised — no code"
- "wait 2 seconds after the event before running the response"
- "raise the event when the button is clicked"
- "why does my event not fire in edit mode / why is the Raise button disabled"
- "find which event calls `OpenShop()`"

Not for:
- Values that persist and change (`Health`, `Score`) → `soap-create-scriptable-variables`
- Events with a custom payload type (`ScriptableEventEnemy`) → `soap-generate-custom-types`
- Lists/dictionaries → `soap-create-scriptable-collections`

## Prerequisites

- Soap 3.8.2, Unity 2019.4+, any render pipeline.
- Install check: `System.Type.GetType("Obvious.Soap.ScriptableEventNoParam, Obvious.Soap") != null` and the menu `Assets > Create > Soap > ScriptableEvents > No Parameters` exists. If not, tell the user Soap is not installed (https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107) — see `soap-setup-and-overview`.

Built-in event and listener types (namespace `Obvious.Soap`):

| Event asset class | `Assets > Create > Soap > ScriptableEvents > …` | Listener component (`Add Component > Soap > EventListeners > …`) |
|---|---|---|
| `ScriptableEventNoParam` | `No Parameters` | `EventListenerNoParam` |
| `ScriptableEventBool` / `Int` / `Float` / `String` | `bool` / `int` / `float` / `string` | `EventListenerBool` / `Int` / `Float` / `String` |
| `ScriptableEventColor` / `Vector2` / `Vector2Int` / `Vector3` / `Quaternion` | `color` / `Vector2` / `Vector2Int` / `Vector3` / `Quaternion` | matching `EventListener*` |
| `ScriptableEventGameObject` / `ScriptableEventComponent` | `GameObject` / `Component` | `EventListenerGameObject` / `EventListenerComponent` |

## Quick start

Goal: an `OnPlayerDied` event, raised from code, that disables a GameObject through an Inspector-wired listener.

1. Select a Project folder, then `Assets > Create > Soap > ScriptableEvents > No Parameters`; name it `OnPlayerDied`. Or from an Editor script:

```csharp
var evt = Obvious.Soap.Editor.SoapEditorUtils.CreateScriptableObject(
    typeof(Obvious.Soap.ScriptableEventNoParam), "OnPlayerDied", "Assets/Data/Events");
```

2. Raise it from a MonoBehaviour:

```csharp
using UnityEngine;
using Obvious.Soap;

public class PlayerDeath : MonoBehaviour
{
    [SerializeField] private FloatVariable _health;
    [SerializeField] private ScriptableEventNoParam _onPlayerDied;

    private void OnEnable()  => _health.OnValueChanged += Check;
    private void OnDisable() => _health.OnValueChanged -= Check;

    private void Check(float hp)
    {
        if (hp <= 0f) _onPlayerDied.Raise();
    }
}
```

3. On the object that should react: `Add Component > Soap > EventListeners > EventListenerNoParam`. In **Event Responses** add one element, drag `OnPlayerDied` into **Scriptable Event**, and in **Response** add a UnityEvent entry (e.g. the player's `GameObject.SetActive(false)`). Leave **Delay** = 0, **Binding** = `UNTIL_DESTROY`.
4. Enter Play mode and drive health to 0.

Expected result: the `Response` UnityEvent executes once; with **Debug Log Enabled** on the event asset the Console shows `[Event] OnPlayerDied => Target.Method()`. The event's Inspector lists the registered listener objects while playing.

## Workflows

### Workflow: Create an event asset

**Goal:** Create a typed or parameterless event.

**Steps:**
1. Menu: `Assets > Create > Soap > ScriptableEvents > {type}`; default file names are `scriptable_event_<type>`.
2. Code: `SoapEditorUtils.CreateScriptableObject(typeof(ScriptableEventInt), "OnDamaged", "Assets/Data/Events")`.
3. Optional Inspector fields: **Debug Log Enabled** (`_debugLogEnabled`) logs every listener invoked; **Debug Value** (`_debugValue`, typed events only) is the argument used by the Inspector **Raise** button.

**Expected result:** The `.asset` exists; `AssetDatabase.LoadAssetAtPath<ScriptableEventInt>(path)` is non-null.

### Workflow: Listen without code (EventListener component)

**Goal:** React to an event purely from the Inspector.

**Steps:**
1. Add `Soap > EventListeners > EventListener{Type}` matching the event's payload type. A `ScriptableEventInt` requires `EventListenerInt`; the Inspector will not accept a mismatched asset.
2. Each element in `_eventResponses` has: **Scriptable Event** (asset), **Delay** (`FloatReference`, seconds, default 0 local), **Response** (`UnityEvent<T>`; for NoParam a `UnityEvent`). Typed responses can forward the payload dynamically (choose a method under "Dynamic {type}") or use a static argument.
3. **Binding** (`_binding`): `UNTIL_DESTROY` (default) registers in `Awake`, unregisters in `OnDestroy` — the listener reacts even while its GameObject is inactive. `UNTIL_DISABLE` registers in `OnEnable`, unregisters in `OnDisable`.
4. **Disable After Subscribing** (`_disableAfterSubscribing`): with `UNTIL_DESTROY`, deactivates the GameObject after registering — handy for hidden UI that appears on an event.
5. Delay: if > 0 the response runs after `WaitForSeconds` (coroutine, active object) or `Task.Delay` (inactive object; cancelled on destroy/disable).

**Expected result:** Raising the event invokes each response after its delay. Debug logging on the event prints `[Event] Name => Target.Method()` per persistent listener.

### Workflow: Subscribe from code

**Goal:** React in C# without a component.

**Steps:**
1. `[SerializeField] private ScriptableEventInt _onDamaged;`
2. `_onDamaged.OnRaised += OnDamaged;` in `OnEnable`, `-=` in `OnDisable`, with `void OnDamaged(int amount)`. For `ScriptableEventNoParam` the handler is `Action` (no parameter).
3. Raise anywhere with `_onDamaged.Raise(10);` / `_onPlayerDied.Raise();`.
4. Order: `Raise` first calls registered `EventListener*` components in reverse registration order, then `OnRaised` subscribers.

**Expected result:** The method runs once per `Raise`. In the Editor the event's Inspector lists the subscriber object under listeners (Soap records `value.Target` when you subscribe).

### Workflow: Raise from UI or the Inspector

**Goal:** Trigger an event from a Button or for testing.

**Steps:**
- Button: in `Button.onClick` drag the event asset and choose `Raise()` (NoParam) or `Raise(T)` with a static argument.
- Inspector: the event asset shows a **Raise** button, but `Raise` returns immediately when `!Application.isPlaying`. Outside Play mode the button only works when `SoapSettings.RaiseEventsInEditor` = `Enabled` (`Window > Obvious > 🧼 Soap > Soap Window > Settings`).

**Expected result:** Clicking invokes listeners in Play mode; in edit mode only with the setting enabled.

### Workflow: Debug which events call a method

**Goal:** Trace Inspector-wired responses.

**Steps:**
1. Open `Window > Obvious > 🧼 Soap > Events Debug Window` (also `Tools > Obvious > 🧼 Soap > Event Debug Window`).
2. Type the method name (e.g. `SetActive`) and click **Find**. Soap scans every `EventListenerBase` in open scenes with `ContainsCallToMethod(name)`.
3. For a given asset, `Assets > Soap > 🔍 Find References > In Scene and Project` lists referencing objects.

**Expected result:** Label "`{method} was found!`" plus Console lines `{method}() is called by: [Event] {EventName}`, or "was not found!".

## Verification

- Event asset exists at the expected path and is of the requested concrete type.
- Listener component present: `gameObject.GetComponent<Obvious.Soap.EventListenerNoParam>() != null`; `new SerializedObject(listener).FindProperty("_eventResponses").arraySize >= 1` and element `_scriptableEvent` references the asset.
- Runtime: in Play mode, `Raise()` produces the expected side effect once; with debug enabled, one `[Event]` log per response.
- Code subscribers appear in the event Inspector's listener list during Play.
- Console shows no `NullReferenceException` (unassigned event field) or `KeyNotFoundException`.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Obvious.Soap.ScriptableEventNoParam.Raise()` / `.OnRaised` | method / `event Action` | Parameterless event. |
| `Obvious.Soap.ScriptableEvent<T>.Raise(T)` / `.OnRaised` | virtual method / `event Action<T>` | Typed event; no-op outside Play mode. |
| `ScriptableEventBase.DebugLogEnabled` | `bool` | Toggle listener logging. |
| `Obvious.Soap.EventListenerBase` | abstract `MonoBehaviour` | `_binding` (`UNTIL_DESTROY`/`UNTIL_DISABLE`), `_disableAfterSubscribing`, `ContainsCallToMethod(string)`. |
| `Obvious.Soap.EventListenerGeneric<T>` | abstract | Base of typed listeners; `EventResponse<T>` = `{ ScriptableEvent, FloatReference Delay, UnityEvent<T> Response }`. |
| `Obvious.Soap.EventListenerNoParam` | component | `EventResponse` = `{ ScriptableEventNoParam ScriptableEvent, FloatReference Delay, UnityEvent Response }`. |
| `Obvious.Soap.Editor.SoapSettings.RaiseEventsInEditor` | enum | Allow Inspector Raise in edit mode. |
| `Obvious.Soap.SoapRuntimeUtils.CreateRuntimeEventInstance<T>(string name)` | static | Runtime-only event instance. |
| `Window > Obvious > 🧼 Soap > Events Debug Window` | menu | Find listeners calling a method. |

## Common issues

- **Symptom:** `Raise()` does nothing. **Cause:** Called in edit mode (`!Application.isPlaying` early return), or no listener registered yet (listener `Awake` not run, wrong `Binding`). **Fix:** Test in Play mode; ensure the listener object exists at raise time or uses `UNTIL_DESTROY`.
- **Symptom:** Inspector Raise button greyed/no effect. **Cause:** `RaiseEventsInEditor` disabled. **Fix:** Enable it in Soap Window > Settings, or enter Play mode.
- **Symptom:** Listener component's event field refuses the asset. **Cause:** Payload type mismatch (`EventListenerInt` needs `ScriptableEventInt`). **Fix:** Use the matching listener, or generate one via `soap-generate-custom-types`.
- **Symptom:** Response fires late or not at all on an inactive object. **Cause:** Delay uses `Task.Delay` for inactive objects and is cancelled on destroy/disable (`UNTIL_DISABLE`). **Fix:** Keep the listener active, or set delay 0.
- **Symptom:** No `[Event]` logs in a build. **Cause:** Event-side debug uses reflection and is compiled only under `UNITY_EDITOR`. **Fix:** Editor-only by design.
- **Symptom:** Handler runs twice. **Cause:** Subscribed in both a component and code, or subscribed in `OnEnable` without unsubscribing in `OnDisable`. **Fix:** Pair subscriptions.

## Boundaries

- Events are synchronous, in-process broadcasts; there is no queue, history, or network transport.
- Payload is a single value; for several fields pack them in a `[Serializable]` class and generate `ScriptableEvent{Type}` (`soap-generate-custom-types`).
- Event-side debug logging is Editor-only; listener-side logs (`EventListenerGeneric.Debug`) work in builds when `DebugLogEnabled` is on.
- Runtime-created event instances (`CreateRuntimeEventInstance`) are not assets and cannot be referenced in the Inspector.

