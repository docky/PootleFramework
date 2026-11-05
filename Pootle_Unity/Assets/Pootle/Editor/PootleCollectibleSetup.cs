using System.Linq;
using Obvious.Soap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pootle.Editor
{
    public static class PootleCollectibleSetup
    {
        const string Art = "Assets/Pootle/Samples/Art/Collectibles/";
        const string Prefabs = "Assets/Pootle/Samples/Collectibles";
        const string Counters = "Assets/Pootle/Data/Collectibles";
        [MenuItem("Pootle/Add Collectible Trail")]
        public static void ApplyMenu() => Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode first");
            EnsureFolder(Prefabs); EnsureFolder(Counters);
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var character = Object.FindAnyObjectByType<PootleCharacter>();
            PootleAnimationSetup.Configure(character);
            var avatar = character.GetComponent<PootleAvatar>(); avatar.joggingAnimationRate=2;avatar.runningAnimationRate=3;EditorUtility.SetDirty(avatar);
            var player = PrefabUtility.LoadPrefabContents(PootleSceneSetup.SamplePrefab);
            try
            {
                PootleAnimationSetup.Configure(player.GetComponent<PootleCharacter>());
                player.GetComponent<PootleAvatar>().joggingAnimationRate=2;player.GetComponent<PootleAvatar>().runningAnimationRate=3;
                PrefabUtility.SaveAsPrefabAsset(player,PootleSceneSetup.SamplePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            string[] names={"Coin","Star","Heart"}; string[] models={"coin","Star","heart"};
            Color[] colors={new Color(1,.75f,.12f),new Color(1,.65f,.08f),new Color(1,.12f,.22f)};
            var prefabs=new GameObject[3];
            for(int k=0;k<3;k++)
            {
                string counterPath=Counters+"/"+names[k]+"s.asset";
                var counter=AssetDatabase.LoadAssetAtPath<IntVariable>(counterPath);
                if(!counter){counter=ScriptableObject.CreateInstance<IntVariable>();counter.Value=0;counter.ResetTiming=ResetTiming.OnSceneUnload;AssetDatabase.CreateAsset(counter,counterPath);}
                string materialPath=Art+names[k]+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!material)
                {
                    material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",colors[k]);
                    material.SetFloat("_Smoothness",.45f);material.SetFloat("_Metallic",k==0?.35f:0);
                    if(k==0)material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"Coin.png"));
                    AssetDatabase.CreateAsset(material,materialPath);
                }
                var root=new GameObject(names[k]+" · Soap pickup");
                try
                {
                    var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+models[k]+".fbx");
                    if(!model)throw new System.InvalidOperationException("Missing collectible model "+models[k]);
                    var visual=(GameObject)PrefabUtility.InstantiatePrefab(model);visual.name="Reference visual";visual.transform.SetParent(root.transform,false);
                    var renderers=visual.GetComponentsInChildren<Renderer>();
                    var bounds=renderers[0].bounds;foreach(var r in renderers){bounds.Encapsulate(r.bounds);r.sharedMaterials=Enumerable.Repeat(material,r.sharedMaterials.Length).ToArray();}
                    float scale=(k==0?.8f:.9f)/Mathf.Max(.01f,bounds.size.y);
                    visual.transform.localScale=Vector3.one*scale;visual.transform.localPosition=-bounds.center*scale;
                    var pickup=root.AddComponent<PootleCollectible>();pickup.counter=counter;pickup.spinSpeed=k==0?150:75;
                    var trigger=root.AddComponent<SphereCollider>();trigger.radius=.55f;trigger.isTrigger=true;
                    var shadow=root.AddComponent<PootlePropBlobShadow>();
                    shadow.depthMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Body blob shadow.mat");
                    shadow.transparentMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Simple prop blob shadow.mat");
                    shadow.localOrigin=Vector3.down*.5f;shadow.opacity=.4f;
                    shadow.shape=k==0?PootlePropBlobShadow.ShadowShape.RoundedRectangle:PootlePropBlobShadow.ShadowShape.Circle;
                    shadow.size=k==0?new Vector2(.8f,.18f):Vector2.one*.7f;
                    prefabs[k]=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"/"+names[k]+".prefab");
                }
                finally{Object.DestroyImmediate(root);}
            }
            var old=GameObject.Find("Collectible trail · Soap counters");if(old)Undo.DestroyObjectImmediate(old);
            var trail=new GameObject("Collectible trail · Soap counters");Undo.RegisterCreatedObjectUndo(trail,"Create collectible trail");
            var points=new[]{new Vector2(-3,-2),new Vector2(-1,0),new Vector2(1,2),new Vector2(3,4),new Vector2(5,6),new Vector2(8,9),
                new Vector2(-7,5),new Vector2(-9,7),new Vector2(-10,10),new Vector2(-12,13),new Vector2(0,10),new Vector2(3,12),
                new Vector2(6,14),new Vector2(9,17),new Vector2(-15,-7),new Vector2(-11,-10),new Vector2(-7,-12),new Vector2(5,-11),new Vector2(11,-11),new Vector2(15,-6)};
            Physics.SyncTransforms();
            for(int i=0;i<points.Length;i++)
            {
                int kind=i%5==0?2:i%5==1?1:0;
                var item=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[kind],trail.transform);item.name=$"{i+1:00} · {names[kind]}";
                var pos=new Vector3(points[i].x,0,points[i].y);
                bool hit=Physics.Raycast(pos+Vector3.up*60,Vector3.down,out var ground,80,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                pos.y=(hit?ground.point.y:0)+1.05f;item.transform.position=pos;item.transform.rotation=Quaternion.Euler(0,i*37,0);
                var pickup=item.GetComponent<PootleCollectible>();pickup.phase=i*.8f;
                var shadow=item.GetComponent<PootlePropBlobShadow>();
                shadow.technique=hit && ground.normal.y>.999f && ground.point.y<.1f ? PootlePropBlobShadow.ShadowTechnique.TransparentGroundQuad : PootlePropBlobShadow.ShadowTechnique.DepthProjection;
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(pickup);PrefabUtility.RecordPrefabInstancePropertyModifications(shadow);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "20 collectibles: 12 coins, 4 stars, 4 hearts; three Soap counters; jogging 2x and running 3x";
        }
        static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            int slash=path.LastIndexOf('/');AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1));
        }
    }
}
