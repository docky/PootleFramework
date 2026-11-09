using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ControlRig;
using Docky.RiggleTools;
using UnityEngine.Animations.Rigging;
using Object=UnityEngine.Object;
namespace Docky.BodyShapeMotion.Editor
{
    public static class DoggyBodyShapeLabBuilder
    {
        public const string Folder="Assets/_EXPERIMENTAL/Humanoid Body Shape Modifier/Demo";
        public const string ScenePath=Folder+"/Scenes/Doggy Body Shape Lab.unity";
        public const string ProfilePath=Folder+"/Generated/Doggy Body Shape.asset";
        public static readonly string[] Clips={"KA_Idle18_Shy","KA_Idle19_ShyRefusal","KA_Idle20_TriplePose","KA_Idle23_Dance03","KA_Idle24_Dance04","KA_Idle25_Cheers","KA_Idle28_Laugh","KA_Idle29_Surprised","KA_Idle30_Stretch2","KA_Idle31_PickUp","KA_Idle40_CrossLegs","KA_Idle43_HandOnHip","KA_Idle52_Curtsy","KA_Walk01","KA_Run01","KA_Run01_TurnL"};
        public static readonly string[] Levels={"Original · 0%","Light · 33%","Medium · 66%","Strong · 100%"};
        public static readonly float[] Strengths={0,.33f,.66f,1};
        public static AnimationClip[] LoadClips()
        {
            var paths=AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/KAWAII_ANIMATIOMS_100"}).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToArray();
            return Clips.Select(n=>AssetDatabase.LoadAllAssetsAtPath(paths.Single(p=>p.EndsWith("@"+n+".FBX"))).OfType<AnimationClip>().Single(c=>c.name==n)).ToArray();
        }
        [MenuItem("Tools/Humanoid Body Shape Modifier/Demo/Create Doggy Comparison Scene")]
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            var clips=LoadClips();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Dog/model_doggy_avatar_sketch_Skinned.prefab");
            if(!prefab)throw new InvalidOperationException("Doggy prefab is missing.");
            if(!AssetDatabase.IsValidFolder(Folder+"/Generated"))AssetDatabase.CreateFolder(Folder,"Generated");
            var profile=AssetDatabase.LoadAssetAtPath<BodyShapeProfile>(ProfilePath);
            if(!profile)
            {
                profile=ScriptableObject.CreateInstance<BodyShapeProfile>();var source=Object.Instantiate(prefab);
                try{BodyShapeProfileFitter.Fit(source,profile);}finally{Object.DestroyImmediate(source);}
                AssetDatabase.CreateAsset(profile,ProfilePath);
            }
            AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            profile=AssetDatabase.LoadAssetAtPath<BodyShapeProfile>(ProfilePath);
            var lab=new GameObject("Body Shape Lab · synchronized comparisons").AddComponent<BodyShapeLab>();
            var samples=new System.Collections.Generic.List<BodyShapeSample>();
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Comparison floor";floor.transform.position=new Vector3(4.8f,-.05f,25.5f);floor.transform.localScale=new Vector3(18,.1f,58);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Generated/Floor.mat");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.12f,.18f,.22f));AssetDatabase.CreateAsset(mat,Folder+"/Generated/Floor.mat");}floor.GetComponent<Renderer>().sharedMaterial=mat;
            for(int row=0;row<16;row++)
            {
                var group=new GameObject((row+1).ToString("00")+" · "+Clips[row]);group.transform.SetParent(lab.transform,false);
                Label(group.transform,new Vector3(4.8f,.015f,row*3.4f-1.35f),Clips[row].Replace("KA_Idle","").Replace("KA_","").Replace('_',' '),.07f);
                for(int col=0;col<4;col++)
                {
                    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group.transform);go.name=Clips[row]+" · "+Levels[col];go.transform.position=new Vector3(col*3.2f,0,row*3.4f);go.transform.rotation=Quaternion.Euler(0,180,0);
                    var control=go.GetComponent<ControlRigAnimation>();if(control){control.poseSource=ControlRigAnimation.PoseSource.Animation;control.rootMotion=false;control.Refresh();}
                    var builder=go.GetComponent<RigBuilder>();if(builder)builder.enabled=false;
                    var adjustment=go.AddComponent<BodyShapeAdjustment>();adjustment.profile=profile;adjustment.strength=Strengths[col];adjustment.manualEvaluation=true;
                    // Deliberate contact/crossed-foot gestures need gentler corrections; shared across all four strengths.
                    if(row==10)adjustment.footWeight=.45f;
                    if(row==11)adjustment.armWeight=.65f;
                    var sample=go.AddComponent<BodyShapeSample>();sample.clip=clips[row];sample.row=row;sample.column=col;
                    sample.Sample(sample.previewPhase*clips[row].length,0,true);FinishPose(sample);sample.Stop();samples.Add(sample);
                    Label(group.transform,new Vector3(col*3.2f,.02f,row*3.4f-.75f),Levels[col],.045f);
                }
            }
            lab.samples=samples.ToArray();var camera=Camera.main;camera.backgroundColor=new Color(.055f,.08f,.11f);camera.clearFlags=CameraClearFlags.SolidColor;lab.Focus(0,true);
            var light=Object.FindAnyObjectByType<Light>();light.transform.rotation=Quaternion.Euler(45,-35,0);light.intensity=1.4f;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.7f);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Selection.activeGameObject=lab.gameObject;
        }
        public static void FinishPose(BodyShapeSample sample)
        {sample.GetComponent<RiggleTwistDistribution>()?.Evaluate();sample.GetComponent<RiggleCorrectives>()?.Evaluate();}
        public static void Preview(BodyShapeLab lab,float phase)
        {lab.Scrub(phase);foreach(var s in lab.samples)if(s && s.isActiveAndEnabled){FinishPose(s);s.Stop();}SceneView.RepaintAll();}
        static void Label(Transform parent,Vector3 p,string text,float size)
        {
            var go=new GameObject(text+" · label");go.transform.SetParent(parent,false);go.transform.position=p;go.transform.rotation=Quaternion.Euler(90,0,0);
            var t=go.AddComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=48;t.characterSize=size;t.anchor=TextAnchor.MiddleCenter;t.color=Color.white;go.GetComponent<Renderer>().sharedMaterial=t.font.material;
        }
    }
}
