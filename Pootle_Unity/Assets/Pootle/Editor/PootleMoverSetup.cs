using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    public static class PootleMoverSetup
    {
        [MenuItem("Pootle/Add Moving Platforms and Hooks")]
        public static void Upgrade()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PootleSceneSetup.SamplePrefab);
            if (!prefab.GetComponent<PootlePlatformRider>()) prefab.AddComponent<PootlePlatformRider>();
            if (!prefab.GetComponent<PootleHookRider>()) prefab.AddComponent<PootleHookRider>();
            PootleAnimationSetup.Configure(prefab.GetComponent<PootleCharacter>());
            PrefabUtility.SaveAsPrefabAsset(prefab, PootleSceneSetup.SamplePrefab); PrefabUtility.UnloadPrefabContents(prefab);
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var c = Object.FindAnyObjectByType<PootleCharacter>();
            if (!c.GetComponent<PootlePlatformRider>()) c.gameObject.AddComponent<PootlePlatformRider>();
            if (!c.GetComponent<PootleHookRider>()) c.gameObject.AddComponent<PootleHookRider>();
            PootleAnimationSetup.Configure(c);
            c.profile.poleBodyDistance = 0.45f; EditorUtility.SetDirty(c.profile);
            foreach (var existingHook in Object.FindObjectsByType<PootleDangleHook>())
            { existingHook.hangDistance = 1.7f; existingHook.bodyDistance = .7f; EditorUtility.SetDirty(existingHook); }
            if (!GameObject.Find("Moving platforms and dangle hooks"))
            {
                var root = new GameObject("Moving platforms and dangle hooks");
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Climbable.mat");
                var accent = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Accent.mat");
                var platform = Box("Moving platform · step aboard", new Vector3(-17,0.3f,8), new Vector3(3,0.6f,3), material, root.transform);
                platform.AddComponent<PootleMovingSurface>(); var body = platform.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                Move(platform, root.transform, new[] {new Vector3(-17,.3f,8),new Vector3(-17,1.8f,12),new Vector3(-13,1.8f,13)}, 1.4f, true);
                var lift = Box("Spline lift · step aboard", new Vector3(8,.3f,25), new Vector3(3,.6f,3), material, root.transform);
                lift.AddComponent<PootleMovingSurface>(); var liftBody = lift.AddComponent<Rigidbody>(); liftBody.isKinematic = true; liftBody.useGravity = false;
                Move(lift, root.transform, new[] {new Vector3(8,.3f,25),new Vector3(8,4,25)}, 1.3f, false);
                var hook = Hook("Dangle shuttle · hold LT or E; jump or release to drop", new Vector3(0,2.8f,-8), root.transform, accent);
                Move(hook, root.transform, new[] {new Vector3(0,2.8f,-8),new Vector3(-3,4,-9),new Vector3(-6,3.4f,-8)}, 1.5f, false);
                var blocked = Hook("Obstruction hook · auto release", new Vector3(12,2.8f,-9), root.transform, accent);
                Move(blocked, root.transform, new[] {new Vector3(12,2.8f,-9),new Vector3(16,2.8f,-9)}, 1.2f, false);
                Box("Hook obstruction test", new Vector3(15,1.5f,-9), new Vector3(.6f,3,2), material, root.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            // Keep the moving root at unit scale; scale the geometry child only.
            var root = new GameObject(name); root.transform.SetParent(parent); root.transform.position = position;
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube); child.name = "Deck"; child.transform.SetParent(root.transform,false); child.transform.localScale = size; child.GetComponent<Renderer>().sharedMaterial = material;
            return root;
        }
        private static GameObject Hook(string name, Vector3 position, Transform parent, Material material)
        {
            var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position; go.AddComponent<PootleDangleHook>();
            var ring = new GameObject("Grip ring"); ring.transform.SetParent(go.transform,false);
            var line = ring.AddComponent<LineRenderer>(); line.useWorldSpace = false; line.loop = true; line.positionCount = 24; line.widthMultiplier = .065f; line.sharedMaterial = material;
            for (int i = 0; i < 24; i++) {float a=i*Mathf.PI*2/24;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.22f,Mathf.Sin(a)*.22f,0));}
            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder); stem.name="Suspension";stem.transform.SetParent(go.transform,false);stem.transform.localPosition=Vector3.up*.65f;stem.transform.localScale=new Vector3(.06f,.45f,.06f);stem.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(stem.GetComponent<Collider>());
            return go;
        }
        private static void Move(GameObject mover, Transform parent, Vector3[] points, float speed, bool rotate)
        {
            var go = new GameObject(mover.name + " · spline"); go.transform.SetParent(parent);
            var container = go.AddComponent<SplineContainer>(); var spline = new Spline();
            foreach (var p in points) spline.Add(new BezierKnot((float3)p), TangentMode.AutoSmooth);
            container.Spline = spline;
            var motion = mover.AddComponent<PootleSplineMover>(); motion.path = container; motion.speed = speed; motion.rotateToPath = rotate; motion.Rebuild();
        }
    }
}
