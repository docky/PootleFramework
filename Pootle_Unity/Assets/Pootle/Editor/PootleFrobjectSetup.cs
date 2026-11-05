using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle.Editor
{
    public static class PootleFrobjectSetup
    {
        [MenuItem("Pootle/Add Frobject Examples")]
        public static void ApplyMenu() => Apply();
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first");
            ConfigureInput();
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            foreach (var character in Object.FindObjectsByType<PootleCharacter>(FindObjectsSortMode.None)) Configure(character);
            var player = PrefabUtility.LoadPrefabContents(PootleSceneSetup.SamplePrefab);
            try { Configure(player.GetComponent<PootleCharacter>()); PrefabUtility.SaveAsPrefabAsset(player, PootleSceneSetup.SamplePrefab); }
            finally { PrefabUtility.UnloadPrefabContents(player); }

            if (!GameObject.Find("Frobject examples"))
            {
                var root = new GameObject("Frobject examples"); Undo.RegisterCreatedObjectUndo(root, "Add Frobject examples");
                var marker = Material("Frobject marker", Color.white, "Pootle/Frobject Marker");
                var bubble = Material("Frobject speech", new Color(.04f, .065f, .09f, .95f), "Pootle/Frobject Marker");
                var teal = Material("Frobject teal", new Color(.1f, .65f, .64f));
                var coral = Material("Frobject coral", new Color(1, .34f, .22f));
                var dark = Material("Frobject base", new Color(.13f, .19f, .23f));
                var button = new GameObject("Push button · A or F"); button.transform.SetParent(root.transform); button.transform.position = new Vector3(2, 0, -10);
                var endpoint = button.AddComponent<PootleFrobject>(); endpoint.localPoint = new Vector3(0, 1.25f, 0);
                button.AddComponent<PootleFrobjectIndicator>().markerMaterial = marker;
                Shape("Pedestal", PrimitiveType.Cylinder, button.transform, new Vector3(0, .5f, 0), new Vector3(.85f, .5f, .85f), dark);
                var cap = Shape("Button cap", PrimitiveType.Cylinder, button.transform, new Vector3(0, 1.08f, 0), new Vector3(.7f, .08f, .7f), coral);
                var lamp = Shape("Status lamp", PrimitiveType.Sphere, button.transform, new Vector3(0, .65f, .43f), Vector3.one * .2f, teal);
                var push = button.AddComponent<PootleFrobjectButton>(); push.cap = cap.transform; push.lamp = lamp.GetComponent<Renderer>();

                var npc = new GameObject("Neighbour · A or F to talk"); npc.transform.SetParent(root.transform); npc.transform.position = new Vector3(6, 0, -10);
                endpoint = npc.AddComponent<PootleFrobject>(); endpoint.localPoint = Vector3.up * 1.9f;
                npc.AddComponent<PootleFrobjectIndicator>().markerMaterial = marker;
                Shape("Body", PrimitiveType.Capsule, npc.transform, new Vector3(0, .9f, 0), new Vector3(.75f, .9f, .75f), teal);
                Shape("Face", PrimitiveType.Sphere, npc.transform, new Vector3(0, 1.48f, .25f), new Vector3(.55f, .5f, .4f), coral);
                var speech = npc.AddComponent<PootleFrobjectSpeech>(); speech.bubbleMaterial = bubble;
                speech.lines = new[] { "Hello, Pootle!\nA little circle means\nI'm ready to chat.", "Try the button beside me.\nPress A again for more!", "See you around!" };
                var label = new GameObject("Frobject instructions"); label.transform.SetParent(root.transform); label.transform.position = new Vector3(4, .025f, -12.2f); label.transform.rotation = Quaternion.Euler(90, 0, 0);
                var text = label.AddComponent<TextMesh>(); text.text = "FROBJECTS\nDot: nearby   Circle: ready\nFace + stop, then A / F"; text.fontSize = 64; text.characterSize = .025f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "Frobject sensor on scene player and prefab; A/F interaction; button and speaking NPC at z=-10.";
        }
        static void Configure(PootleCharacter character)
        {
            if (!character.GetComponent<PootleFrobjectSensor>()) character.gameObject.AddComponent<PootleFrobjectSensor>();
            EditorUtility.SetDirty(character.gameObject);
        }
        static void ConfigureInput()
        {
            string path = PootleSceneSetup.Root + "/Data/PootleControls.inputactions";
            var controls = InputActionAsset.FromJson(File.ReadAllText(path));
            try
            {
                var map = controls.FindActionMap("Character", true);
                var action = map.FindAction("Interact") ?? map.AddAction("Interact", InputActionType.Button);
                if (!action.bindings.Any(x => x.path == "<Gamepad>/buttonSouth")) action.AddBinding("<Gamepad>/buttonSouth", groups: "Gamepad");
                if (!action.bindings.Any(x => x.path == "<Keyboard>/f")) action.AddBinding("<Keyboard>/f", groups: "KeyboardMouse");
                File.WriteAllText(path, controls.ToJson()); AssetDatabase.ImportAsset(path);
            }
            finally { Object.DestroyImmediate(controls); }
        }
        static Material Material(string name, Color color, string shader = "Universal Render Pipeline/Lit")
        {
            string path = PootleSceneSetup.Root + "/Samples/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find(shader)); material.color = color; AssetDatabase.CreateAsset(material, path); }
            return material;
        }
        static GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = at; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
    }
}
