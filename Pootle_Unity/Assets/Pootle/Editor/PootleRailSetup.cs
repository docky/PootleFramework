using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pootle.Editor
{
    public static class PootleRailSetup
    {
        [MenuItem("Pootle/Add Rail Playground")]
        public static void Upgrade()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PootleSceneSetup.SamplePrefab);
            if (!prefab.GetComponent<PootleRailRider>()) prefab.AddComponent<PootleRailRider>();
            PrefabUtility.SaveAsPrefabAsset(prefab, PootleSceneSetup.SamplePrefab);
            PrefabUtility.UnloadPrefabContents(prefab);
            var scene = EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var c = Object.FindAnyObjectByType<PootleCharacter>();
            if (!c.GetComponent<PootleRailRider>()) c.gameObject.AddComponent<PootleRailRider>();
            if (!GameObject.Find("One way rail course"))
            {
                var group = new GameObject("One way rail course");
                var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Accent.mat");
                Rail("Walk + turn · jump through from below", new[] { new Vector3(-4,1.4f,3),new Vector3(4,1.4f,3),new Vector3(4,1.4f,8) }, group.transform, mat);
                Rail("Steep slide + flat coast", new[] { new Vector3(18,7,18),new Vector3(18,1,22),new Vector3(18,1,29) }, group.transform, mat);
                Rail("Gentle inclined rail", new[] { new Vector3(-5,0.7f,22),new Vector3(-5,3,29) }, group.transform, mat);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static void Rail(string name, Vector3[] points, Transform parent, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent);
            var rail = go.AddComponent<PootleRail>(); rail.points = points;
            for (int i = 0; i < points.Length - 1; i++)
            {
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name = "Rail visual · one way";
                visual.transform.SetParent(go.transform); visual.transform.position = (points[i] + points[i + 1]) * 0.5f - Vector3.up * 0.06f;
                visual.transform.rotation = Quaternion.LookRotation(points[i + 1] - points[i]);
                visual.transform.localScale = new Vector3(0.22f,0.12f,Vector3.Distance(points[i],points[i + 1]));
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                visual.GetComponent<Renderer>().sharedMaterial = material;
            }
        }
    }
}
