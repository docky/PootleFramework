using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Pootle.Editor
{
    public static class PootlePropShadowSetup
    {
        const string DepthPath = "Assets/Pootle/Samples/Materials/Body blob shadow.mat";
        const string SimplePath = "Assets/Pootle/Samples/Materials/Simple prop blob shadow.mat";
        static Material SimpleMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SimplePath);
            if (!material)
            { material = new Material(Shader.Find("Pootle/Transparent Blob Shadow")); AssetDatabase.CreateAsset(material, SimplePath); }
            return material;
        }
        static PootlePropBlobShadow Configure(GameObject target)
        {
            var blob = target.GetComponent<PootlePropBlobShadow>();
            if (!blob) blob = Undo.AddComponent<PootlePropBlobShadow>(target);
            Undo.RecordObject(blob, "Configure prop shadow");
            blob.depthMaterial = AssetDatabase.LoadAssetAtPath<Material>(DepthPath);
            blob.transparentMaterial = SimpleMaterial();
            EditorUtility.SetDirty(blob);
            return blob;
        }
        [MenuItem("Pootle/Add Prop Blob Shadow to Selection")]
        public static void AddToSelection()
        {
            foreach (var target in Selection.gameObjects) Configure(target);
            AssetDatabase.SaveAssets();
        }
        [MenuItem("Pootle/Add Playground Prop Shadows")]
        public static void ApplyMenu() => Apply();
        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first");
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            int poles = 0, hooks = 0;
            foreach (var pole in Object.FindObjectsByType<PootlePole>())
            {
                if (pole.gameObject.scene != scene) continue;
                var blob = Configure(pole.gameObject);
                blob.technique = PootlePropBlobShadow.ShadowTechnique.DepthProjection;
                blob.shape = PootlePropBlobShadow.ShadowShape.Circle;
                blob.localOrigin = pole.transform.InverseTransformPoint(pole.Bottom);
                blob.size = Vector2.one * Mathf.Max(.45f, pole.radius * 3);
                blob.opacity = .4f; poles++;
            }
            foreach (var hook in Object.FindObjectsByType<PootleDangleHook>())
            {
                if (hook.gameObject.scene != scene) continue;
                var blob = Configure(hook.gameObject);
                blob.technique = hook.name.StartsWith("Dangle shuttle") ? PootlePropBlobShadow.ShadowTechnique.TransparentGroundQuad : PootlePropBlobShadow.ShadowTechnique.DepthProjection;
                blob.shape = PootlePropBlobShadow.ShadowShape.Circle;
                blob.localOrigin = hook.localGripPoint - Vector3.up * .25f;
                blob.size = Vector2.one * .7f; blob.opacity = .45f; hooks++;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return $"Configured {poles} poles and {hooks} hooks";
        }
    }
}
