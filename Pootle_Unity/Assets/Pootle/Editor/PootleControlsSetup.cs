using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle.Editor
{
    public static class PootleControlsSetup
    {
        public const string MainPrompt = "LEFT STICK  Move    RIGHT STICK  Orbit\nA / CROSS  Jump    HOLD B / CIRCLE or LB / L1  Run\nLT / L2  Toggle ball    X / SQUARE  Interact\nY / TRIANGLE  Carry / drop    RB / R1  Glide\nRT / R2  Detach / ground pound    AUTO GRAB\nWASD  Move    SPACE  Jump    SHIFT  Run\nX  Ball    E  Interact    V  Carry    F  Glide\nQ  Detach / pound    C  Recenter    R  Reset";
        public const string RollingPrompt = "LT / L2  Toggle ball    Y / TRIANGLE  Carry / drop\nKeyboard X  Ball    V  Carry / drop\nJump twice for double jump    Ball fits under tunnel\n1 Orbit   2 Close   3 Chase   4 Angled   5 Overhead   6 Side";
        public const string TraversalPrompt = "BLUE  Approach to climb automatically\nMove up/down and around cylinders\nYELLOW  Auto grab poles / ropes; turn with left stick\nJump follows facing; RT / R2 or Q to detach";
        public const string FrobjectPrompt = "FROBJECTS\nDot: nearby   Circle: ready\nFace + stop, then X / SQUARE or E";

        // Shared by upgrade tools, so rerunning an older course setup cannot restore old bindings.
        public static void ConfigureBindings(InputActionAsset controls)
        {
            var map = controls.FindActionMap("Character", true);
            map.FindAction("Grip")?.RemoveAction();
            void Button(string name, params string[] paths)
            {
                var action = map.FindAction(name) ?? map.AddAction(name, InputActionType.Button);
                for (int i = action.bindings.Count - 1; i >= 0; i--) action.ChangeBinding(i).Erase();
                foreach (string path in paths) action.AddBinding(path, groups: path.StartsWith("<Gamepad>") ? "Gamepad" : "KeyboardMouse");
            }
            Button("Run", "<Gamepad>/buttonEast", "<Gamepad>/leftShoulder", "<Keyboard>/leftShift");
            Button("Roll", "<Gamepad>/leftTrigger", "<Keyboard>/x");
            Button("Interact", "<Gamepad>/buttonWest", "<Keyboard>/e");
            Button("Carry", "<Gamepad>/buttonNorth", "<Keyboard>/v");
            Button("Detach", "<Gamepad>/rightTrigger", "<Keyboard>/q");
            Button("Glide", "<Gamepad>/rightShoulder", "<Keyboard>/f");
        }

        public static void ConfigureInput()
        {
            string path = PootleSceneSetup.Root + "/Data/PootleControls.inputactions";
            var controls = InputActionAsset.FromJson(File.ReadAllText(path));
            try { ConfigureBindings(controls); File.WriteAllText(path, controls.ToJson()); AssetDatabase.ImportAsset(path); }
            finally { Object.DestroyImmediate(controls); }
        }

        [MenuItem("Pootle/Update Controls and Prompts")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first");
            ConfigureInput();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var label in root.GetComponentsInChildren<TextMesh>(true))
                {
                    if (label.text.StartsWith("LEFT STICK")) label.text = MainPrompt;
                    else if (label.name == "New controls") label.text = RollingPrompt;
                    else if (label.name == "Frobject instructions") label.text = FrobjectPrompt;
                    else if (label.text.StartsWith("BLUE  ")) label.text = TraversalPrompt;
                    else continue;
                    EditorUtility.SetDirty(label);
                }
                foreach (var speech in root.GetComponentsInChildren<PootleFrobjectSpeech>(true))
                {
                    for (int i = 0; i < speech.lines.Length; i++) speech.lines[i] = speech.lines[i].Replace("Press A again", "Press X / Square again");
                    EditorUtility.SetDirty(speech);
                }
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                {
                    string updated = item.name.Replace("A or F", "X / Square or E")
                        .Replace("hold LT or E; jump or release to drop", "auto grab; RT / R2 or Q to drop")
                        .Replace("hold LT or E", "auto grab");
                    if (updated == item.name) continue;
                    item.name = updated; EditorUtility.SetDirty(item.gameObject);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
    }
}
