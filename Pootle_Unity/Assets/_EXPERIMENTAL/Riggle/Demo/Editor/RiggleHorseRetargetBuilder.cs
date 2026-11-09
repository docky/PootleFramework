using System;
using System.IO;
using System.Collections;
using System.Linq;
using Docky.RiggleTools.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Docky.RiggleTools.Demo
{
    public static class RiggleHorseRetargetBuilder
    {
        public const string Folder="Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Horse Retarget";
        const string Horse="Assets/LPHorse_Version_2_9/Version_2_9/";
        public const string ScenePath="Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Pink Pony Horse Animations.unity";
        public const string TerrainScenePath="Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Pink Pony Retarget Uneven Walk.unity";
        [MenuItem("Tools/Riggle/Build Pink Pony Retarget Terrain Trial")]
        public static void BuildTerrainTrial()
        {
            if(EditorApplication.isPlaying||Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Use Edit Mode with saved scenes.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Pink Pony Animated.prefab"));root.name="Pink Pony Retarget";
                var rig=root.GetComponent<RiggleGeneric>();var animator=root.GetComponent<Animator>();
                var clip=animator.runtimeAnimatorController.animationClips.Single(c=>c.name.Contains("Rig_Collected_Walk_RM"));
                var controller=UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(AssetDatabase.GenerateUniqueAssetPath(Folder+"/Pink Pony Terrain Walk.controller"));controller.AddMotion(clip);animator.runtimeAnimatorController=controller;
                var la=rig.legsAnimator;((Behaviour)la).enabled=true;var so=new SerializedObject(la);
                so.FindProperty("Mecanim").objectReferenceValue=animator;so.FindProperty("GlueMode").enumValueIndex=1;
                so.FindProperty("AllowGlueDrag").floatValue=0;so.FindProperty("GlueRangeThreshold").floatValue=.22f;
                so.FindProperty("GluingFloorLevel").floatValue=.05f;
                so.FindProperty("AllowGlueBelowFoot").floatValue=.6f;
                so.FindProperty("UnglueOn").floatValue=55;
                so.FindProperty("HipsHeightStepBlend").floatValue=.25f;so.FindProperty("StabilizeCenterOfMass").floatValue=.15f;
                so.FindProperty("HipsStretchPreventer").floatValue=0;so.FindProperty("PushHipsOnLegMove").floatValue=.015f;
                so.ApplyModifiedPropertiesWithoutUndo();
                la.GetType().GetMethod("User_RefreshHelperVariablesOnParametersChange").Invoke(la,null);
                la.GetType().GetMethod("Finders_RefreshAllLegsAnkleAxes").Invoke(la,null);
                Type Integration(string n)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Docky.RiggleTools.Demo."+n)).First(t=>t!=null);
                string modulePath=Folder+"/Animated Hoof Grounding.asset";var module=AssetDatabase.LoadAssetAtPath<ScriptableObject>(modulePath);
                if(!module){module=ScriptableObject.CreateInstance(Integration("RiggleAnimatedHoofGrounding"));AssetDatabase.CreateAsset(module,modulePath);}
                var list=(IList)la.GetType().GetField("CustomModules").GetValue(la);list.Clear();
                var helper=Activator.CreateInstance(list.GetType().GetGenericArguments()[0],new object[]{la});helper.GetType().GetField("ModuleReference").SetValue(helper,module);list.Add(helper);
                var walk=root.AddComponent(Integration("RiggleQuadrupedWalk"));var ws=new SerializedObject(walk);
                ws.FindProperty("animal").stringValue="Pink Pony Retarget";
                ws.FindProperty("recordingSubtitle").stringValue="Retargeted LP Horse walk | Four-segment legs + Legs Animator";
                ws.FindProperty("clipAnimator").objectReferenceValue=animator;
                var profile=Object.Instantiate(AssetDatabase.LoadAssetAtPath<RiggleQuadrupedRetargetProfile>(Folder+"/LP Horse to Pink Pony.asset"));
                try
                {
                    profile.includeRootMotion=true;
                    using(var session=new RiggleQuadrupedAnimationConverter.Session(profile))
                    {
                        var original=profile.clips[1];session.Sample(original,0);var start=session.TargetBones[0].position;
                        session.Sample(original,original.length);float speed=(session.TargetBones[0].position-start).magnitude/original.length;
                        ws.FindProperty("clipPlaybackSpeed").floatValue=.3f/Mathf.Max(.01f,speed);
                    }
                }
                finally{Object.DestroyImmediate(profile);}
                ws.ApplyModifiedPropertiesWithoutUndo();root.AddComponent(Integration("RiggleQuadrupedObserver"));
                RiggleQuadrupedTrialBuilder.Stage("Pink Pony Retarget",rig);
                EditorUtility.SetDirty(la);PrefabUtility.SaveAsPrefabAsset(root,Folder+"/Pink Pony Retarget Terrain.prefab");
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,TerrainScenePath);
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }
        [MenuItem("Tools/Riggle/Build Pink Pony Horse Animation Showcase")]
        public static void Build()=>BuildShowcase("Pink Pony","Pink Horse",Folder,ScenePath);

        public const string BlueScenePath="Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Blue Pony Horse Animations.unity";
        [MenuItem("Tools/Riggle/Build Blue Pony Horse Animation Showcase")]
        public static void BuildBlue()=>BuildShowcase("Blue Pony","Blue Pony",Folder+"/Blue Pony",BlueScenePath);

        static void BuildShowcase(string pony,string originalName,string folder,string scenePath)
        {
            if(EditorApplication.isPlaying || Enumerable.Range(0,UnityEngine.SceneManagement.SceneManager.sceneCount).Any(i=>UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Use Edit Mode with saved scenes.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var profile=AssetDatabase.LoadAssetAtPath<RiggleQuadrupedRetargetProfile>(folder+"/LP Horse to "+pony+".asset");
            if(!profile){profile=ScriptableObject.CreateInstance<RiggleQuadrupedRetargetProfile>();AssetDatabase.CreateAsset(profile,folder+"/LP Horse to "+pony+".asset");}
            profile.sourceModel=AssetDatabase.LoadAssetAtPath<GameObject>(Horse+"Mesh/LP_Horse_Mesh_2_9.fbx");
            profile.sourceReferencePose=AssetDatabase.LoadAssetAtPath<AnimationClip>(Horse+"Animations/Primary_Cycles/Rig_Idle_Center.anim");
            profile.clampFeetToReferenceFloor=true;profile.maxBodyLowering=.12f;
            profile.targetPrefab=BuildFourSegmentPony(pony,originalName,folder);
            profile.outputFolder=folder+"/Clips";profile.sourceMotionRoot="Rig/Root_Motion";profile.targetHips="Hips";
            string PathOf(string name)=>AnimationUtility.CalculateTransformPath(profile.sourceModel.GetComponentsInChildren<Transform>().Single(t=>t.name==name),profile.sourceModel.transform);
            profile.sourceHips=PathOf("Back_Back");profile.body.Clear();profile.legs.Clear();
            foreach(var pair in new[]{("Back_Back","Hips"),("BackMiddle","Spine"),("Back_Forward","Chest"),("Neck_Middle","Neck"),("Head","Head")})
                profile.body.Add(new RiggleQuadrupedRetargetProfile.BodyBone{sourcePath=PathOf(pair.Item1),targetJoint=pair.Item2});
            for(int pair=0;pair<2;pair++)for(int side=0;side<2;side++)
            {
                bool front=pair==0;string suffix=side==0?".L":".R";string target=(front?"Front":"Hind")+(side==0?"Left":"Right");
                var foot=profile.sourceModel.transform.Find(PathOf((front?"Front_Hoof":"Rear_Hoof")+suffix));
                profile.legs.Add(new RiggleQuadrupedRetargetProfile.Leg{name=target,sourceUpper=PathOf((front?"Shoulder":"Rear_UpperThigh")+suffix),sourceFoot=PathOf(foot.name),
                    targetUpper=target+"Upper",targetMiddle=target+"Lower",targetFoot=target+"Foot",bendDirection=Vector3.back,
                    additionalTargetJoints=new[]{target+"Joint3",target+"Joint4"},
                    sourceShapeChain=(front?new[]{"Shoulder","Front_UpperLeg","Front_Shin","Front_Calf","Front_Hoof"}:new[]{"Rear_UpperThigh","Rear_LowerThigh","Rear_Shin","Rear_Calf","Rear_Hoof"}).Select(n=>PathOf(n+suffix)).ToArray(),
                    sourceSoleOffset=foot.InverseTransformPoint(new Vector3(foot.position.x,0,foot.position.z)),targetSoleOffset=new Vector3(0,-.085f,0)});
            }
            string[] names={"Rig_Idle_Center","Rig_Collected_Walk_RM","Rig_Extended_Walk_RM","Rig_Collected_Trot_RM","Rig_Left_Lead_Canter_RootMotion","Rig_Gallop_Steady_RootMotion"};
            profile.clips=names.Select(n=>AssetDatabase.LoadAssetAtPath<AnimationClip>(Horse+"Animations/Primary_Cycles/"+n+".anim")).ToArray();
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            var converted=RiggleQuadrupedAnimationConverter.Bake(profile);
            try
            {
                var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var source=Object.Instantiate(profile.sourceModel);source.name="Source · LP Horse";
                foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>())Object.DestroyImmediate(renderer.gameObject);
                foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mats=skin.sharedMaterials;
                    for(int i=0;i<mats.Length;i++)
                    {
                        var original=mats[i];string path=folder+"/Source Material "+i+".mat";
                        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
                        if(original){mat.SetTexture("_BaseMap",original.mainTexture);mat.SetColor("_BaseColor",original.HasProperty("_Color")?original.color:Color.white);}
                        mat.SetFloat("_Smoothness",.2f);EditorUtility.SetDirty(mat);mats[i]=mat;
                    }
                    skin.sharedMaterials=mats;skin.updateWhenOffscreen=true;
                }
                var target=Object.Instantiate(profile.targetPrefab);target.name=pony+" · converted animation";
                var targetRig=target.GetComponent<RiggleGeneric>();
                foreach(var b in target.GetComponentsInChildren<MonoBehaviour>())if(!(b is RiggleGeneric)&&b!=targetRig.legsAnimator)Object.DestroyImmediate(b);
                foreach(var skin in target.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
                var controllerPath=folder+"/"+pony+" Converted.controller";
                var controller=UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(AssetDatabase.GenerateUniqueAssetPath(controllerPath));
                foreach(var clip in converted)controller.AddMotion(clip);
                var animator=target.GetComponent<Animator>();if(!animator)animator=target.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
                PrefabUtility.SaveAsPrefabAsset(target,folder+"/"+pony+" Animated.prefab");
                source.transform.position=new Vector3(1.65f,0,0);source.transform.localScale=Vector3.one*.57f;
                target.transform.position=new Vector3(-1.45f,0,0);
                var demo=new GameObject("Animation comparison").AddComponent<RiggleQuadrupedRetargetShowcase>();
                demo.source=source;demo.target=target;demo.sourceMotionRoot=source.transform.Find(profile.sourceMotionRoot);
                demo.originalClips=profile.clips;demo.convertedClips=converted;
                demo.targetLabel=pony.ToUpperInvariant();
                demo.recordingSlug="riggle-"+pony.ToLowerInvariant().Replace(' ','-')+"-horse-retarget-hoof-fix-2026-11-08";
                demo.recordingSubfolder=pony=="Blue Pony"?"Blue Pony":"";
                demo.labels=new[]{"IDLE","COLLECTED WALK","EXTENDED WALK","TROT","CANTER","GALLOP"};
                var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";
                camera.transform.position=new Vector3(4.3f,2.8f,7.2f);camera.transform.LookAt(new Vector3(0,.95f,0));camera.fieldOfView=36;
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.045f,.065f,.09f);demo.camera=camera;
                var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Comparison stage";floor.transform.localScale=Vector3.one*3;
                var ground=new Material(Shader.Find("Universal Render Pipeline/Lit"));ground.color=new Color(.18f,.24f,.28f);AssetDatabase.CreateAsset(ground,AssetDatabase.GenerateUniqueAssetPath(folder+"/Stage.mat"));floor.GetComponent<Renderer>().sharedMaterial=ground;
                var key=new GameObject("Key").AddComponent<Light>();key.type=LightType.Directional;key.intensity=2;key.shadows=LightShadows.Soft;key.transform.rotation=Quaternion.Euler(50,-30,0);
                var fill=new GameObject("Fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.55f;fill.transform.rotation=Quaternion.Euler(20,150,0);
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.55f,.6f);
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,scenePath);
            }
            finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        }

        static GameObject BuildFourSegmentPony(string pony,string originalName,string folder)
        {
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Quadruped Trials/"+originalName+" Walking.prefab");
            var root=Object.Instantiate(original);root.name=pony+" Four Segment";
            try
            {
                var rig=root.GetComponent<RiggleGeneric>();
                RiggleGenericBuilder.Clear(rig);
                foreach(var b in root.GetComponents<MonoBehaviour>())if(b!=rig)Object.DestroyImmediate(b);
                rig.legSegments=4;rig.RebuildLegSegments();rig.assetFolder=folder+"/Four Segment Skin";
                foreach(var leg in rig.legs)
                {
                    bool front=leg.name.StartsWith("Front");var foot=rig.joints.Single(j=>j.name==leg.foot).position;
                    var middle=rig.joints.Single(j=>j.name==leg.middle);
                    middle.position=new Vector3(foot.x,front?.60f:.67f,foot.z+(front?.025f:.13f));
                    rig.joints.Single(j=>j.name==leg.additionalJoints[0]).position=new Vector3(foot.x,front?.33f:.34f,foot.z-(front?.035f:.10f));
                    rig.joints.Single(j=>j.name==leg.additionalJoints[1]).position=new Vector3(foot.x,.17f,foot.z-.025f);
                    if(pony=="Blue Pony")
                    {
                        float span=rig.joints.Single(j=>j.name==leg.upper).position.y-foot.y;
                        middle.position=new Vector3(foot.x,foot.y+span*.67f,foot.z+(front?.014f:.07f));
                        rig.joints.Single(j=>j.name==leg.additionalJoints[0]).position=new Vector3(foot.x,foot.y+span*.35f,foot.z-(front?.02f:.055f));
                        rig.joints.Single(j=>j.name==leg.additionalJoints[1]).position=new Vector3(foot.x,foot.y+span*.12f,foot.z-.014f);
                    }
                    foreach(string n in leg.additionalJoints)rig.joints.Single(j=>j.name==n).radius=.055f;
                }
                RiggleGenericBuilder.Bake(rig);
                RiggleGenericLegsAnimator.Configure(rig);((Behaviour)rig.legsAnimator).enabled=false;
                var result=PrefabUtility.SaveAsPrefabAsset(root,folder+"/"+pony+" Four Segment.prefab");
                return result;
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
