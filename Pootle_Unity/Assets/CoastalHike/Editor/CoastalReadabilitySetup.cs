using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;
using Pootle;
using Object = UnityEngine.Object;

public static class CoastalReadabilitySetup
{
    const string Folder = "Assets/CoastalHike";
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
    [MenuItem("Pootle/Coastal Hike/Apply Readability and Camera Policy")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before authoring.");
        var player = Object.FindAnyObjectByType<PootleCharacter>();
        var policy = AssetDatabase.LoadAssetAtPath<PootleCameraPolicy>(Folder + "/Exploration Camera Policy.asset");
        if (!policy)
        {
            policy = ScriptableObject.CreateInstance<PootleCameraPolicy>();
            AssetDatabase.CreateAsset(policy, Folder + "/Exploration Camera Policy.asset");
        }
        foreach (var shot in Object.FindObjectsByType<PootleExplorationShot>())
        {
            Undo.RecordObject(shot, "Assign shared camera policy"); shot.policy = policy; shot.ResetTracking(); shot.ApplySettings();
        }
        var tilt = player.GetComponentInChildren<PootleViewTilt>(true);
        Undo.RecordObject(tilt, "Assign View Tilt policy"); tilt.policy = policy;
        Undo.RecordObject(tilt.gameObject, "Name View Tilt"); tilt.gameObject.name = "View Tilt";
        foreach (var t in Object.FindObjectsByType<Transform>())
        {
            if (t.name.EndsWith("· exterior landing")) SetHeight(t, 2.95f);
            if (t.name == "Hidden entrance landing") SetHeight(t, 2.92f);
            if (t.name.EndsWith("tunnel · floor")) SetHeight(t, 2.865f);
            if (t.name.EndsWith("· exterior arrival") || t.name.EndsWith("· exterior buffer end") || t.name == "Fern exterior arrival" || t.name == "Fern exterior buffer end") SetHeight(t, 3.2f);
        }
        var signs = Object.FindObjectsByType<Transform>().Where(t => t.name == "Coral trail waymarker" || t.name.StartsWith("Signpost · ")).OrderBy(t => t.position.y).ToArray();
        string[] messages = { "PINE MEADOW\nFollow the uphill trail", "SUN TERRACE\nContinue along the ridge", "HIGH MEADOW\nClimb toward the lookout", "SUMMIT LOOKOUT\nThe last ascent" };
        for (int i = 0; i < signs.Length; i++)
        {
            var sign = signs[i]; Undo.RecordObject(sign.gameObject, "Name signpost"); sign.name = "Signpost · " + messages[Mathf.Min(i, 3)].Split('\n')[0];
            var proximity = sign.GetComponent<PootleSignLabel>();
            if (!proximity) proximity = Undo.AddComponent<PootleSignLabel>(sign.gameObject);
            Undo.RecordObject(proximity, "Configure sign label"); proximity.player = player.transform; proximity.viewCamera = Camera.main;
            proximity.message = messages[Mathf.Min(i, 3)]; proximity.showDistance = 4; proximity.exitDistance = 6; proximity.worldOffset = Vector3.up * 3.2f;
            if (!proximity.labelRoot)
            {
                var canvasGo = new GameObject("Nearby sign label", typeof(RectTransform), typeof(Canvas)); Undo.RegisterCreatedObjectUndo(canvasGo, "Create sign label");
                canvasGo.transform.SetParent(sign, false); var rect = (RectTransform)canvasGo.transform;
                rect.sizeDelta = new Vector2(330, 70); rect.localScale = Vector3.one * .015f;
                var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main; canvas.sortingOrder = 150;
                var panel = new GameObject("Dark backing", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(rect, false);
                var panelRect = (RectTransform)panel.transform; panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one; panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
                var image = panel.GetComponent<Image>(); image.color = new Color(.035f, .07f, .075f, .94f); image.raycastTarget = false;
                var label = new GameObject("Sign text", typeof(RectTransform), typeof(TextMeshProUGUI)); label.transform.SetParent(rect, false);
                var labelRect = (RectTransform)label.transform; labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one; labelRect.offsetMin = new Vector2(10, 5); labelRect.offsetMax = new Vector2(-10, -5);
                var text = label.GetComponent<TextMeshProUGUI>(); text.font = Font; text.fontSize = 25; text.alignment = TextAlignmentOptions.Center; text.color = new Color(1, .96f, .8f); text.raycastTarget = false;
                text.text = proximity.message; proximity.labelRoot = rect; proximity.label = text; canvasGo.SetActive(false);
            }
        }
        Physics.SyncTransforms(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene); EditorSceneManager.SaveScene(player.gameObject.scene);
        CreateTiltComparison(player, policy);
        Selection.activeGameObject = tilt.gameObject;
    }
    static void SetHeight(Transform t, float y)
    {
        Undo.RecordObject(t, "Separate overlapping walking surfaces"); var p = t.localPosition; p.y = y; t.localPosition = p;
    }
    static void CreateTiltComparison(PootleCharacter player, PootleCameraPolicy policy)
    {
        string demoPath = Folder + "/View Tilt Comparison.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(demoPath)) return;
        var source = player.visualRoot.GetComponentsInChildren<Animator>(true).First().gameObject;
        var root = new GameObject("View Tilt"); var component = root.AddComponent<PootleViewTilt>(); component.policy = policy;
        var model = Object.Instantiate(source, root.transform); model.name = "Avatar model";
        PrefabUtility.SaveAsPrefabAsset(root, Folder + "/View Tilt.prefab"); Object.DestroyImmediate(root);
        var current = SceneManager.GetActiveScene();
        var demo = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(demo);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/View Tilt.prefab");
        foreach (bool tilted in new[] { false, true })
        {
            var facing = new GameObject(tilted ? "View Tilt · policy comparison" : "View Tilt · upright reference"); facing.transform.position = new Vector3(tilted ? 1.8f : -1.8f, 0, 0); facing.transform.rotation = Quaternion.Euler(0, 180, 0);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, demo); instance.transform.SetParent(facing.transform, false);
            var tilt = instance.GetComponent<PootleViewTilt>(); tilt.overridePolicy = !tilted; tilt.tiltBackDegrees = 0;
            Label(tilted ? "VIEW TILT\n25 DEGREES" : "UPRIGHT\n0 DEGREES", facing.transform.position + Vector3.back * 2, 3.4f);
        }
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "View Tilt · comparison stage"; floor.transform.position = new Vector3(0, -.15f, 0); floor.transform.localScale = new Vector3(12, .3f, 9);
        floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Cave Assets/Cave cool stone.mat");
        var camera = new GameObject("View Tilt · comparison camera", typeof(Camera)).GetComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.rotation = Quaternion.Euler(60, 0, 0); camera.transform.position = new Vector3(0, 1.2f, 0) - camera.transform.forward * 17; camera.fieldOfView = 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .1f, .12f);
        var light = new GameObject("View Tilt · key light", typeof(Light)).GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(45, -30, 0);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.45f, .48f, .5f);
        foreach (var text in Object.FindObjectsByType<TextMeshPro>().Where(t => t.gameObject.scene == demo)) text.transform.rotation = camera.transform.rotation;
        EditorSceneManager.SaveScene(demo, demoPath); EditorSceneManager.CloseScene(demo, true); SceneManager.SetActiveScene(current);
    }
    static void Label(string value, Vector3 position, float width)
    {
        var text = new GameObject("View Tilt · " + value, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        text.text = value; text.font = Font; text.fontSize = 3.5f; text.alignment = TextAlignmentOptions.Center; text.rectTransform.sizeDelta = new Vector2(width, 1.4f);
        text.rectTransform.anchoredPosition3D = position + Vector3.up * .8f; text.color = Color.white;
    }
}
