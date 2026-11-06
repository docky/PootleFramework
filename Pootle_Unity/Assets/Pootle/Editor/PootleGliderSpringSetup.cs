using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    public static class PootleGliderSpringSetup
    {
        [MenuItem("Pootle/Add Glider and Springs")]
        public static void Upgrade()
        {
            var prefab = PrefabUtility.LoadPrefabContents(PootleSceneSetup.SamplePrefab);
            try { Configure(prefab.GetComponent<PootleCharacter>()); PrefabUtility.SaveAsPrefabAsset(prefab, PootleSceneSetup.SamplePrefab); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var character = Object.FindAnyObjectByType<PootleCharacter>(); Configure(character);
            if (!GameObject.Find("Launch springs"))
            {
                var root = new GameObject("Launch springs");
                Spring("Vertical spring · hold RB or F at apex to glide", new Vector3(-5,0,-3), Vector3.up, root.transform);
                Spring("Directional spring", new Vector3(10,0,0), new Vector3(0,1,0.55f).normalized, root.transform);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static void Configure(PootleCharacter character)
        {
            PootleAnimationSetup.Configure(character);
            var visual = character.GetComponent<PootleGliderVisual>() ?? character.gameObject.AddComponent<PootleGliderVisual>();
            visual.character = character;
            var tilt = character.visualRoot.GetComponentInChildren<PootleViewTilt>(true);
            var root = tilt ? tilt.transform.Find("Glider") : character.visualRoot.Find("Glider");
            if (!root)
            {
                root = new GameObject("Glider").transform; root.SetParent(tilt ? tilt.transform : character.visualRoot,false);
                PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pootle/Samples/Art/Glider/glider.fbx"), root);
            }
            root.localScale=Vector3.one; root.localPosition=Vector3.up*2.05f;
            var model=root.GetChild(0);
            model.localPosition=Vector3.zero; model.localRotation=Quaternion.identity;model.localScale=Vector3.one;
            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            float factor=3.1f/Mathf.Max(bounds.size.x,bounds.size.z);
            model.localScale=new Vector3(factor,factor*.25f,factor);
            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            model.position+=root.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            visual.animator=character.GetComponentInChildren<Animator>();
            visual.suspensionLines=new LineRenderer[2];
            for(int i=0;i<2;i++)
            {
                string name=i==0?"Left glider cord":"Right glider cord";
                var cord=root.Find(name);if(!cord){cord=new GameObject(name).transform;cord.SetParent(root,false);}
                var line=cord.GetComponent<LineRenderer>();if(!line)line=cord.gameObject.AddComponent<LineRenderer>();
                line.positionCount=2;line.useWorldSpace=true;line.widthMultiplier=.018f;
                line.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Accent.mat");line.enabled=false;
                visual.suspensionLines[i]=line;
            }
            visual.gliderRoot = root;
            visual.audioSource = character.GetComponent<AudioSource>();
            if (!visual.audioSource) visual.audioSource = character.gameObject.AddComponent<AudioSource>();
            visual.audioSource.playOnAwake = false; visual.audioSource.spatialBlend = 1;
            visual.openSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Pootle/Samples/Audio/open_glider.wav");
            visual.closeSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Pootle/Samples/Audio/close_glider.wav");
            EditorUtility.SetDirty(visual); EditorUtility.SetDirty(visual.audioSource);
        }
        private static void Spring(string name, Vector3 feet, Vector3 direction, Transform parent)
        {
            var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = feet;
            var spring = go.AddComponent<PootleSpring>(); spring.localLaunchDirection = direction;
            spring.sound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Pootle/Samples/Audio/spring.wav");
            var collider = go.AddComponent<BoxCollider>(); collider.center = Vector3.up * .2f; collider.size = new Vector3(1.6f,.4f,1.6f);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Accent.mat");
            var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder); cap.name="Launch pad";cap.transform.SetParent(go.transform,false);
            cap.transform.localPosition=Vector3.up*.35f;cap.transform.localScale=new Vector3(1.6f,.05f,1.6f);
            cap.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(cap.GetComponent<Collider>());
            var coil=new GameObject("Spring coil");coil.transform.SetParent(go.transform,false);
            var line=coil.AddComponent<LineRenderer>();line.useWorldSpace=false;line.positionCount=81;line.widthMultiplier=.05f;line.sharedMaterial=material;
            for(int i=0;i<81;i++){float t=i/80f;float a=t*Mathf.PI*8;line.SetPosition(i,new Vector3(Mathf.Cos(a)*.5f,.03f+t*.27f,Mathf.Sin(a)*.5f));}
        }
    }
}
