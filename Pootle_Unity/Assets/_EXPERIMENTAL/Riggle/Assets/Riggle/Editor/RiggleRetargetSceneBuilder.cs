using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ControlRig;
namespace Docky.RiggleTools.Editor
{
    public static class RiggleRetargetSceneBuilder
    {
        public const string ScenePath="Assets/_EXPERIMENTAL/Riggle/Riggle Kawaii Retargeting.unity";
        public static readonly string[] Clips={"KA_Idle18_Shy","KA_Idle20_TriplePose","KA_Idle23_Dance03","KA_Idle25_Cheers","KA_Idle30_Stretch2","KA_Walk01","KA_Run01","KA_Run01_TurnL"};
        [MenuItem("Tools/Riggle/Create Kawaii Retarget Test Scene")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var paths=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/RiggleGenerated"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.EndsWith("_Skinned.prefab")).OrderBy(p=>p).ToArray();
            var all=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/KAWAII_ANIMATIOMS_100"}).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
            var clips=Clips.Select(n=>AssetDatabase.LoadAllAssetsAtPath(all.Single(p=>p.EndsWith("@"+n+".FBX"))).OfType<AnimationClip>().Single(c=>c.name==n)).ToArray();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Retarget comparison floor";floor.transform.position=new Vector3(9,-.075f,14);floor.transform.localScale=new Vector3(32,.1f,40);
            var material=new Material(shader);material.SetColor("_BaseColor",new Color(.12f,.18f,.23f));
            RiggleModelPreparation.Folder("Assets/RiggleGenerated/Retarget Lab");
            string controllerPath="Assets/RiggleGenerated/Retarget Lab/Kawaii Walk.controller";
            if(!AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath))
            {
                var controller=UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
                controller.AddMotion(clips[5]);
            }
            string matPath="Assets/RiggleGenerated/Retarget Lab/Floor.mat";var saved=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(saved){Object.DestroyImmediate(material);material=saved;}else AssetDatabase.CreateAsset(material,matPath);
            floor.GetComponent<Renderer>().sharedMaterial=material;
            for(int row=0;row<paths.Length;row++)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(paths[row]);
                var group=new GameObject(prefab.name.Replace("_Skinned","")+" · Kawaii row");
                Label(group.transform,new Vector3(-3.5f,.02f,row*5),prefab.name.Replace("_Skinned","").Replace("model_doggy_avatar_sketch","Dog").Replace("Riggle Demo - Bean Biped","Bean biped"),.1f);
                for(int column=0;column<clips.Length;column++)
                {
                    var anchor=new GameObject(Clips[column]+" · start marker");anchor.transform.SetParent(group.transform,false);anchor.transform.position=new Vector3(column*3.4f,0,row*5);
                    var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,anchor.transform);model.name=Clips[column];model.transform.localRotation=Quaternion.Euler(0,180,0);
                    var control=model.GetComponent<ControlRigAnimation>()??model.AddComponent<ControlRigAnimation>();control.poseSource=ControlRigAnimation.PoseSource.Animation;control.Refresh();
                    var sample=model.AddComponent<RiggleRetargetSample>();sample.clip=clips[column];sample.moveRoot=true;sample.previewTime=column==4?.45f:.35f;sample.resetAfterSeconds=clips[column].length;
                    sample.Preview();
                    // Keep the anchor at the original root, so displacement remains visible.
                    Label(anchor.transform,anchor.transform.position+new Vector3(0,.02f,-2.5f),Clips[column].Replace("KA_Idle","").Replace("KA_","").Replace('_',' '),.045f);
                    var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.name="Root start";marker.transform.SetParent(anchor.transform,false);marker.transform.localPosition=new Vector3(0,-.01f,0);marker.transform.localScale=new Vector3(.5f,.015f,.05f);
                }
            }
            var camera=Camera.main;camera.orthographic=true;camera.orthographicSize=16;camera.transform.position=new Vector3(10,34,-24);camera.transform.LookAt(new Vector3(10,0,14));camera.backgroundColor=new Color(.06f,.09f,.13f);camera.clearFlags=CameraClearFlags.SolidColor;
            var light=Object.FindAnyObjectByType<Light>();light.transform.rotation=Quaternion.Euler(45,-35,0);light.intensity=1.4f;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        }
        static void Label(Transform parent,Vector3 position,string text,float size)
        {
            var go=new GameObject(text+" · label");go.transform.SetParent(parent,false);go.transform.position=position;go.transform.rotation=Quaternion.Euler(90,0,0);
            var label=go.AddComponent<TextMesh>();label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=48;label.characterSize=size;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;go.GetComponent<Renderer>().sharedMaterial=label.font.material;
        }
    }
}
