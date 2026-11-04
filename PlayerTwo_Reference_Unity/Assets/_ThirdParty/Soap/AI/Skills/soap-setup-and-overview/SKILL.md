---
name: soap-setup-and-overview
description: "Use this skill whenever the user mentions Soap, ScriptableObject architecture, 'scriptable variables', 'scriptable events', or asks things like 'is soap installed', 'set up soap in my project', 'how do I use this ScriptableObject architecture asset', 'where is the soap window', 'open the soap examples', 'what does soap do', 'configure soap settings / tags / prefixes'. Covers confirming Soap - ScriptableObject Architecture is installed, first-time setup (Soap Window, SoapSettings, example scenes, Odin define), where the files live, and an index of the other soap-* skills. Do NOT use for actually creating variables (see soap-create-scriptable-variables), events (soap-create-scriptable-events), lists/dictionaries (soap-create-scriptable-collections), generating custom types (soap-generate-custom-types), UI bindings (soap-bind-ui) or JSON save data (soap-save-game-data). When in doubt whether a ScriptableObject-based data, event or decoupling request could involve Soap, use this skill first — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Soap - ScriptableObject Architecture"
  publisher: "Obvious Game"
  asset-version: "3.8.3"
  skill-version: "1.0.0"
  unity: "2019.4+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/utilities"
  asset-store-url: "https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107"
  documentation-url: "https://obvious-game.gitbook.io/soap"
  support-url: "https://discord.gg/CVhCNDbxF5"
  last-verified: "2026-09-12"
---

# Soap Setup and Overview

Soap (ScriptableObject Architecture Pattern) is a Unity Editor and runtime toolkit that replaces hard-wired MonoBehaviour references with ScriptableObject assets: **variables** (`FloatVariable`, `IntVariable`, …) that hold shared state and raise `OnValueChanged`, **events** (`ScriptableEventInt`, `ScriptableEventNoParam`, …) that decouple senders from listeners, **collections** (`ScriptableList<T>`, `ScriptableDictionary<K,V>`), **enums**, **singletons**, and **saves** (`ScriptableSave<T>` JSON persistence), plus UI **bindings** and a code generator for custom types. This skill lets an agent confirm Soap is present, perform first-time project setup, locate the key files and windows, and choose the right sibling skill for the user's actual task.

## When to use this skill

- "is soap installed in this project?" / "which version of soap do I have?"
- "set up soap" / "get me started with the scriptableobject architecture asset"
- "open the soap window" / "show me the soap example scenes"
- "change soap settings so it asks me for a name when creating assets"
- "add a tag / prefix convention for my soap assets"
- "why do I see a Soap window every time I open the project"
- Any request that names a Soap type (`FloatVariable`, `ScriptableEventNoParam`, `ScriptableList…`, `BindTextMeshPro`) when you are not yet sure which capability is needed.

Not for:
- Creating or wiring a variable → `soap-create-scriptable-variables`
- Raising/listening to events → `soap-create-scriptable-events`
- Lists and dictionaries → `soap-create-scriptable-collections`
- Generating classes for a custom type (`PlayerVariable`, `ScriptableEventEnemy`, dictionary, singleton) → `soap-generate-custom-types`
- Showing a variable in UI (text, slider, health bar) → `soap-bind-ui`
- Saving/loading a JSON save file → `soap-save-game-data`

## Prerequisites

- Unity 2019.4 or newer (the package declares `"unity": "2019.4"` in `Assets/Obvious/Soap/package.json`). Unity 6000.5+ is supported through a `UNITY_6000_5_OR_NEWER` code path.
- Render pipeline: any. Soap contains no rendering code; it depends only on **uGUI** (`UnityEngine.UI`) and **TextMeshPro** (`TMPro`), both referenced by `Assets/Obvious/Soap/Core/Runtime/Obvious.Soap.asmdef`. If TextMeshPro is missing the runtime assembly will not compile.
- Optional: Odin Inspector. When the `ODIN_INSPECTOR` scripting define is present, `ScriptableBase` derives from `Sirenix.OdinInspector.SerializedScriptableObject` and Soap's own drawers switch to Odin attributes. Nothing is required from the agent.

**Programmatic install check** — run this in an Editor script (or verify the equivalent by hand) before doing anything else:

```csharp
using UnityEditor;
using UnityEngine;

public static class SoapInstallCheck
{
    [MenuItem("Tools/Check Soap Installed")]
    public static void Check()
    {
        bool folder = AssetDatabase.IsValidFolder("Assets/Obvious/Soap");
        bool type = System.Type.GetType("Obvious.Soap.ScriptableBase, Obvious.Soap") != null;
        bool editorType = System.Type.GetType("Obvious.Soap.Editor.SoapWindow, Obvious.Soap.Editor") != null;
        Debug.Log($"Soap folder: {folder} | runtime type: {type} | editor type: {editorType}");
    }
}
```

All three must be `True`. Equivalent manual checks: the folder `Assets/Obvious/Soap` exists, the menu `Window > Obvious > 🧼 Soap > Soap Window` is present, and `Assets/Obvious/Soap/package.json` contains `"name": "com.obvious.soap"`.

If the check fails, tell the user: *"Soap - ScriptableObject Architecture does not appear to be installed. Import it from the Asset Store (https://assetstore.unity.com/packages/tools/utilities/soap-scriptableobject-architecture-pattern-232107) via Window > Package Manager > My Assets, then re-run this check."* Do not attempt to recreate Soap types yourself.

To read the installed version programmatically, parse `Assets/Obvious/Soap/package.json` (`"version"`), which is exactly what `Obvious.Soap.Editor.SoapPackageInfo.TryGetVersion(out string version)` does.

## Quick start

Goal: confirm Soap works end to end by running the first example scene.

1. Run the install check above; all three flags must be `True`.
2. Open `Window > Obvious > 🧼 Soap > Soap Window`. The window is titled "SOAP" and shows the version, links (Documentation, Discord, Asset Store, YouTube) and a left-hand menu with **Getting Started**, **Settings**, **Examples Scenes**, **Tools**.
3. Click **Examples Scenes**, select **1_ScriptableVariables**, click **Open Example Scene**. Alternatively open `Assets/Obvious/Soap/Examples/Scenes/1_ScriptableVariables_Example_Scene.unity` directly.
4. Press Play, click the on-screen buttons.
5. Expected result: the `FloatVariable` assets under `Assets/Obvious/Soap/Examples/Content/ScriptableVariables/` change value in the Inspector in real time, and the Console shows no errors. Exiting Play mode restores the pre-play values (Soap resets non-saved variables on `EnteredEditMode`).

## Workflows

### Workflow: Verify installation and version

**Goal:** Prove Soap is present and report its version.

**Steps:**
1. Run the `SoapInstallCheck` snippet from Prerequisites.
2. Read `Assets/Obvious/Soap/package.json`; report `displayName`, `version`, `unity`.
3. Optionally read the top entry of `Assets/Obvious/Soap/CHANGELOG.md` for the release date.

**Expected result:** A log line with three `True` values and a version string such as `3.8.2`.

### Workflow: First-time project setup

**Goal:** Ensure the settings asset exists and is configured for the user's conventions.

**Steps:**
1. Open `Window > Obvious > 🧼 Soap > Soap Window` and select **Settings**. Soap loads (or creates) a single `Obvious.Soap.Editor.SoapSettings` asset via `Resources.Load<SoapSettings>("SoapSettings")`; the shipped asset is `Assets/Obvious/Soap/Core/Editor/Resources/SoapSettings.asset`. Never create a second `SoapSettings` asset — only one can be loaded.
2. Review the fields (defaults in parentheses) and change only what the user asked for:
   - `VariableDisplayMode` (`Default`) — `Minimal` hides advanced variable fields. Not changeable with Odin.
   - `NamingOnCreationMode` (`Auto`) — `Manual` opens the Soap Asset Creator popup so the user names each asset on creation.
   - `CreatePathMode` (`Auto`) — `Auto` creates assets in the folder currently selected in the Project window; `Manual` uses the custom path stored in EditorPrefs (`SoapEditorUtils.CustomCreationPath`, default `Assets`).
   - `RaiseEventsInEditor` (`Disabled`) — set `Enabled` to allow the Inspector "Raise" button outside Play mode.
   - `Tags` (`["None"]`) — labels shown in the Soap Wizard and assignable per asset (`ScriptableBase.TagIndex`).
   - `Prefixes` (six empty strings) — optional file-name prefixes for Variable, Event, List, Dictionary, Enum, Save (indices 0–5), used when Soap auto-creates assets from the Inspector "Create" button.
3. If editing via code, load and modify the asset with `UnityEditor.SerializedObject` and call `AssetDatabase.SaveAssets()`:

```csharp
var settings = Resources.Load<Obvious.Soap.Editor.SoapSettings>("SoapSettings");
settings.NamingOnCreationMode = Obvious.Soap.Editor.ENamingCreationMode.Manual;
settings.Tags.Add("Player");
UnityEditor.EditorUtility.SetDirty(settings);
UnityEditor.AssetDatabase.SaveAssets();
```

**Expected result:** `Assets/Obvious/Soap/Core/Editor/Resources/SoapSettings.asset` reflects the new values; the Settings panel of the Soap Window shows them.

### Workflow: Stop the Soap Window opening automatically

**Goal:** The Soap Window opens once per project on first load (`SoapWindowInitializer`, keyed on EditorPrefs `Soap_Window_HasShownWindow_<hash>`). It does not reopen afterwards.

**Steps:** Close the window. If it keeps appearing, the EditorPrefs key was cleared; simply closing it again records the flag.

**Expected result:** No Soap Window on subsequent Editor launches.

### Workflow: Browse existing Soap assets with the Soap Wizard

**Goal:** Find every Soap asset in the project.

**Steps:**
1. Open `Window > Obvious > 🧼 Soap > Soap Wizard` (shortcut Ctrl+Shift+W, also under `Tools > Obvious > 🧼 Soap > Soap Wizard`).
2. Set the root folder (stored per project), then use the tabs **All, Variables, Events, Collections, Enums, Favorites**, the tag filter and the search field.
3. Select an asset to see its embedded inspector; right-click for rename/duplicate/delete; use the **+** button to create a new asset via the Soap Asset Creator.

**Expected result:** A filtered list of all `ScriptableBase`-derived assets beneath the chosen root.

### Workflow: Find where an asset is used

**Goal:** Locate scene/prefab references to a Soap asset.

**Steps:** Right-click the asset in the Project window → `Soap > 🔍 Find References > In Scene and Project` (or `In Scene` / `In Project`). The same commands exist in the asset's Inspector context menu (`⋮ > 🔍Find References > …`).

**Expected result:** A `ReferencesPopupWindow` listing GameObjects/components (scene) and asset paths with counts (project).

## Verification

- `AssetDatabase.IsValidFolder("Assets/Obvious/Soap")` is `true`.
- `System.Type.GetType("Obvious.Soap.ScriptableBase, Obvious.Soap") != null`.
- `Resources.Load<Obvious.Soap.Editor.SoapSettings>("SoapSettings") != null` and exactly one `t:SoapSettings` asset exists (`AssetDatabase.FindAssets("t:SoapSettings").Length == 1`).
- Menus present: `Window > Obvious > 🧼 Soap > {Soap Window, Soap Wizard, Soap Type Creator, Events Debug Window}`; `Assets > Create > Soap > …`; `Assets > Create > 🧼 Soap Asset Creator`.
- Console clean after opening the Soap Window and an example scene.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Obvious.Soap.ScriptableBase` | abstract `ScriptableObject` | Root of every Soap asset; has `TagIndex`, `Description`, `GetGenericType`. Presence proves install. |
| `Obvious.Soap.Editor.SoapSettings` | `ScriptableObject` (Editor asm) | Project-wide settings: display mode, naming, create path, raise-in-editor, `Tags`, `Prefixes`. Load with `Resources.Load<SoapSettings>("SoapSettings")`. |
| `Obvious.Soap.Editor.SoapPackageInfo.TryGetVersion(out string)` | static method | Reads the version from `package.json`. |
| `Obvious.Soap.Editor.SoapEditorUtils.CreateScriptableObject(Type, string name, string path, bool ping = true)` | static method | Creates any ScriptableObject asset at `Assets/...` path (creates folders). Used by the other skills. |
| `Obvious.Soap.Editor.SoapEditorUtils.FindAll<T>(string path = "")` | static method | Returns all assets of type `T` under a path. |
| `Window > Obvious > 🧼 Soap > Soap Window` | menu | Hub window: settings, example scenes, tools. |
| `Tools > Obvious > 🧼 Soap > Delete Player Pref` (Ctrl+Shift+D) | menu | `PlayerPrefs.DeleteAll()` — clears saved variable values. |
| `Tools > Obvious > 🧼 Soap > ToggleFastPlayMode` (Ctrl+L) | menu | Toggles `EditorSettings.enterPlayModeOptionsEnabled` (domain reload off). |

Key locations:
- Runtime code: `Assets/Obvious/Soap/Core/Runtime/` (namespace `Obvious.Soap`, attributes in `Obvious.Soap.Attributes`).
- Editor code: `Assets/Obvious/Soap/Core/Editor/` (namespace `Obvious.Soap.Editor`).
- Code templates: `Assets/Obvious/Soap/Core/Editor/Resources/Templates/*.cs.txt`.
- Examples: `Assets/Obvious/Soap/Examples/Scenes/` (10 scenes, `1_ScriptableVariables_Example_Scene` … `10_ScriptableSingletons_Example_Scene`) and `Assets/Obvious/Soap/Examples/Content/`.
- Docs: https://obvious-game.gitbook.io/soap ; per-class `[HelpURL]` attributes link directly to the relevant page.

Skill index:
- `soap-create-scriptable-variables` — create/read/write/subscribe to variables, clamping, reset timing, PlayerPrefs saving, references, sub-assets.
- `soap-create-scriptable-events` — event assets, `EventListener*` components, `Raise`/`OnRaised`, debugging.
- `soap-create-scriptable-collections` — `ScriptableList<T>` and `ScriptableDictionary<K,V>`.
- `soap-generate-custom-types` — Soap Type Creator: variable/event/listener/list/enum/save/dictionary/singleton classes for your own types.
- `soap-bind-ui` — bind variables to Text/TMP/Slider/Toggle/InputField/Image/colors, comparison → UnityEvent.
- `soap-save-game-data` — `ScriptableSave<T>` JSON save files and PlayerPrefs-saved variables.

## Common issues

- **Symptom:** Compile errors mentioning `TMPro` or `TMP_Text`. **Cause:** TextMeshPro package missing. **Fix:** Install TextMeshPro via `Window > Package Manager` (Unity Registry) — the Soap runtime assembly references it.
- **Symptom:** Menus appear under `Tools > Obvious Game > Soap` in old tutorials but not in the Editor. **Cause:** Version 3.7.1 renamed the menu root to `Obvious`. **Fix:** Use `Window > Obvious > 🧼 Soap > …` / `Tools > Obvious > 🧼 Soap > …`.
- **Symptom:** Inspector shows "Do not use ScriptableVariable<int> directly. Please use IntVariable that inherit from it instead." **Cause:** A field was declared with the open generic type. **Fix:** Use the concrete class (`IntVariable`, `ScriptableEventInt`, …) or generate one with `soap-generate-custom-types`.
- **Symptom:** "Variable Display Mode cannot be changed when using Odin Inspector." **Cause:** `ODIN_INSPECTOR` define present. **Fix:** Expected; Odin controls drawing.
- **Symptom:** Inspectors of unrelated MonoBehaviours look different after installing Soap. **Cause:** `Obvious.Soap.Editor.ObjectEditor` is a fallback `[CustomEditor(typeof(Object), true, isFallback = true)]` used to draw Soap fields inline (compiled out under Odin). **Fix:** Its source comment states it may be deleted when using NaughtyAttributes; otherwise leave it.
- **Symptom:** Two `SoapSettings` assets, settings appear to "not stick". **Cause:** Duplicate settings asset in a Resources folder. **Fix:** Keep only `Assets/Obvious/Soap/Core/Editor/Resources/SoapSettings.asset`.

## Boundaries

- Soap is an architecture/data layer. It does not provide gameplay systems, UI toolkits, networking, or rendering features.
- Persistence is limited to PlayerPrefs (per variable) and JSON files via `JsonUtility` (`ScriptableSave<T>`); there is no encryption, cloud sync, or binary serializer — fall back to your own serializer if you need those.
- `ScriptableDictionary<K,V>` uses a plain `Dictionary<K,V>` field; its Inspector serialization depends on Odin Inspector or a Unity version that serializes dictionaries. Without them, populate dictionaries at runtime.
- Integrations with Playmaker, Game Creator 2 and other third-party assets are distributed separately (see `Assets/Obvious/Soap/Integrations/Soap Integration Links.url`) and are not covered by these skills.
- Menu strings contain emoji (`🧼`, `🔍`, `🔖`, `🖼`). When invoking menus via `UnityEditor.EditorApplication.ExecuteMenuItem`, pass the exact string including the emoji, e.g. `"Window/Obvious/🧼 Soap/Soap Window"`.

