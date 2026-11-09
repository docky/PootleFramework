using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Docky.RiggleTools.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace Docky.RiggleTools.Demo
{
    public static class RiggleQuadrupedTrialBuilder
    {
        const string Base = "Assets/_EXPERIMENTAL/Riggle/Demo/";
        public const string Output = Base + "Generated/Quadruped Trials";
        static Type Integration(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Docky.RiggleTools.Demo."+name)).First(t=>t!=null);
        [MenuItem("Tools/Riggle/Build Horse and Fawn Walking Trials")]
        public static void Build() => BuildAnimals(new[]{"Pink Horse","Toy Fawn"});
        [MenuItem("Tools/Riggle/Build Blue Pony Walking Trial")]
        public static void BuildBluePony() => BuildAnimals(new[]{"Blue Pony"});
        static void BuildAnimals(string[] animals)
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Build trials in Edit Mode.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save current scenes before rebuilding the walking trials.");
            Directory.CreateDirectory(Output); AssetDatabase.Refresh();
            try
            {
                foreach(string animal in animals)
                {
                    var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                    var rig=Character(animal);
                    Stage(animal,rig);
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.SaveScene(scene,Base+"Scenes/Riggle "+animal+" Uneven Walk.unity");
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        static RiggleGeneric Character(string animal)
        {
            string model=Base+"Characters/Quadruped Trials/"+animal+"/"+animal+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.isReadable=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
            var root=new GameObject(animal);
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(model),root.transform);
            source.name=animal+" Source";
            bool horse=animal=="Pink Horse";
            bool pony=animal=="Blue Pony";
            string texture=pony?"tripo_base_color_efed3ddb-aea7-4ccf-af98-5422dead9157.png":horse?"tripo_base_color_fbf07c56-2dd1-4cba-a760-89cce103f73a.png":"Image_0.png";
            // glTF Image_0 is verified from the imported material's base-color connection during conversion.
            var material=AssetDatabase.LoadAssetAtPath<Material>(Output+"/"+animal+".mat");
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Output+"/"+animal+".mat");}
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Base+"Characters/Quadruped Trials/"+animal+"/"+texture));
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",horse?.28f:.48f);material.SetFloat("_Metallic",0);
            foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=material;
            var rig=root.AddComponent<RiggleGeneric>();rig.assetFolder=Output+"/"+animal;rig.joints.Clear();rig.legs.Clear();rig.hips="Hips";
            rig.falloff=5.5f;rig.jointSoftness=.27f;rig.smoothSteps=2;rig.smoothAmount=.25f;
            void Joint(string name,string parent,Vector3 point,string end,Vector3 tip,float radius)
                =>rig.joints.Add(new RiggleGeneric.Joint{name=name,parent=parent,position=point,endJoint=end,tip=tip,radius=radius});
            if(pony)
            {
                Joint("Hips","",new Vector3(0,.77f,-.20f),"Spine",Vector3.zero,.18f);
                Joint("Spine","Hips",new Vector3(0,.80f,.07f),"Chest",Vector3.zero,.20f);
                Joint("Chest","Spine",new Vector3(0,.84f,.43f),"Neck",Vector3.zero,.18f);
                Joint("Neck","Chest",new Vector3(0,1.13f,.54f),"Head",Vector3.zero,.16f);
                Joint("Head","Neck",new Vector3(0,1.40f,.61f),"",new Vector3(0,1.32f,.90f),.29f);
                Joint("Tail","Hips",new Vector3(0,.81f,-.35f),"TailMiddle",Vector3.zero,.12f);
                Joint("TailMiddle","Tail",new Vector3(0,.62f,-.53f),"TailEnd",Vector3.zero,.12f);
                Joint("TailEnd","TailMiddle",new Vector3(0,.38f,-.73f),"",new Vector3(0,.35f,-.94f),.10f);
            }
            else
            {
            Joint("Hips","",new Vector3(0,horse?.98f:.87f,horse?-.48f:-.40f),"Spine",Vector3.zero,horse?.19f:.12f);
            Joint("Spine","Hips",new Vector3(0,horse?1.0f:.88f,horse?-.07f:-.19f),"Chest",Vector3.zero,horse?.20f:.14f);
            Joint("Chest","Spine",new Vector3(0,horse?1.02f:.88f,horse?.30f:.03f),"Neck",Vector3.zero,horse?.20f:.13f);
            Joint("Neck","Chest",new Vector3(0,horse?1.25f:1.13f,horse?.42f:.17f),"Head",Vector3.zero,horse?.14f:.10f);
            Joint("Head","Neck",new Vector3(0,horse?1.49f:1.44f,horse?.51f:.26f),"",new Vector3(0,1.45f,horse?.84f:.57f),horse?.29f:.25f);
            if(!horse)Joint("Tail","Hips",new Vector3(0,.92f,-.51f),"",new Vector3(0,.67f,-.61f),.065f);
            }
            var vertices=source.GetComponentInChildren<MeshFilter>().sharedMesh.vertices;
            for(int pair=0;pair<2;pair++)for(int side=0;side<2;side++)
            {
                bool front=pair==0;float sign=side==0?-1:1;
                string prefix=(front?"Front":"Hind")+(side==0?"Left":"Right");
                var points=vertices.Where(p=>p.y<.1f && (p.x<0)==(side==0) && (p.z>0)==front).ToArray();
                if(points.Length==0)throw new InvalidOperationException(animal+": no sole vertices found for "+prefix);
                var foot=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;foot.y=.085f;
                var upper=new Vector3(foot.x*(horse?.82f:1),pony?.56f:horse?(front?.91f:.96f):.72f,foot.z+(front?0:pony?.025f:horse?.19f:.035f));
                var middle=new Vector3(foot.x,pony?.30f:horse?.39f:.36f,foot.z+(front?-.045f:horse?-.07f:-.035f));
                Joint(prefix+"Upper",front?"Chest":"Hips",upper,prefix+"Lower",Vector3.zero,pony?.10f:horse?.095f:.065f);
                Joint(prefix+"Lower",prefix+"Upper",middle,prefix+"Foot",Vector3.zero,pony?.08f:horse?.065f:.048f);
                Joint(prefix+"Foot",prefix+"Lower",foot,"",foot+new Vector3(0,-.025f,.085f),pony?.085f:horse?.065f:.045f);
                rig.legs.Add(new RiggleGeneric.Leg{name=prefix,upper=prefix+"Upper",middle=prefix+"Lower",foot=prefix+"Foot",side=side==0?RiggleGeneric.Side.Left:RiggleGeneric.Side.Right,opposite=(front?"Front":"Hind")+(side==0?"Right":"Left")});
            }
            RiggleGenericBuilder.Bake(rig);RiggleGenericLegsAnimator.Configure(rig);
            var la=rig.legsAnimator;var so=new SerializedObject(la);
            so.FindProperty("GlueMode").enumValueIndex=0;
            so.FindProperty("GlueRangeThreshold").floatValue=.22f;
            so.FindProperty("AllowGlueDrag").floatValue=0;
            so.FindProperty("LegElevateBlend").floatValue=1;
            so.FindProperty("FootAlignRapidity").floatValue=1;
            so.FindProperty("HipsHeightStepBlend").floatValue=pony?.15f:.7f;
            so.FindProperty("StabilizeCenterOfMass").floatValue=pony?.1f:.25f;
            so.FindProperty("StabilizeOnIsMoving").floatValue=1;
            so.FindProperty("HipsStretchPreventer").floatValue=pony?0:.5f;
            so.FindProperty("PushHipsOnLegMove").floatValue=.035f;
            so.FindProperty("LimitLegStretch").floatValue=.99f;
            var step=so.FindProperty("BaseLegAnimating");
            step.FindPropertyRelative("StepMoveDuration").floatValue=.25f;
            step.FindPropertyRelative("MinFootRaise").floatValue=.14f;
            step.FindPropertyRelative("MaxFootRaise").floatValue=.32f;
            step.FindPropertyRelative("AllowSpeedups").floatValue=0;
            step.FindPropertyRelative("AllowDetachBefore").floatValue=1;
            so.ApplyModifiedPropertiesWithoutUndo();
            la.GetType().GetMethod("User_RefreshHelperVariablesOnParametersChange").Invoke(la,null);
            la.GetType().GetMethod("Finders_RefreshAllLegsAnkleAxes").Invoke(la,null);
            var moduleType=Integration("RiggleQuadrupedCadence");
            string modulePath=Output+"/Quadruped Walking Cadence.asset";
            var module=AssetDatabase.LoadAssetAtPath<ScriptableObject>(modulePath);
            if(!module){module=ScriptableObject.CreateInstance(moduleType);AssetDatabase.CreateAsset(module,modulePath);}
            var list=(IList)la.GetType().GetField("CustomModules").GetValue(la);
            var helper=Activator.CreateInstance(list.GetType().GetGenericArguments()[0],new object[]{la});
            helper.GetType().GetField("ModuleReference").SetValue(helper,module);list.Add(helper);
            var walk=root.AddComponent(Integration("RiggleQuadrupedWalk"));
            var walkSO=new SerializedObject(walk);walkSO.FindProperty("animal").stringValue=animal;
            walkSO.FindProperty("bodyHeightOffset").floatValue=pony?.05f:-.025f;
            walkSO.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent(Integration("RiggleQuadrupedObserver"));
            EditorUtility.SetDirty(la);EditorUtility.SetDirty(rig);EditorUtility.SetDirty(material);
            PrefabUtility.SaveAsPrefabAsset(root,Output+"/"+animal+" Walking.prefab");
            return rig;
        }
        static Material Material(string name,Color color)
        {
            string path=Output+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Smoothness",.18f);EditorUtility.SetDirty(m);return m;
        }
        internal static void Stage(string animal,RiggleGeneric rig)
        {
            var verts=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            const int nx=48,nz=340;const float width=4.8f,zmin=-3,zmax=14;
            for(int j=0;j<=nz;j++)for(int i=0;i<=nx;i++)
            {float x=-width*.5f+width*i/nx,z=Mathf.Lerp(zmin,zmax,j/(float)nz);verts.Add(new Vector3(x,RiggleUnevenCourse.Height(x,z),z));uv.Add(new Vector2(x,z));}
            for(int j=0;j<nz;j++)for(int i=0;i<nx;i++)
            {int a=j*(nx+1)+i;triangles.AddRange(new[]{a,a+nx+1,a+1,a+1,a+nx+1,a+nx+2});}
            var mesh=new Mesh{name="Uneven test floor"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            string path=Output+"/Uneven test floor.asset";var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,path);
            var ground=new GameObject("Uneven floor · collider matches visible surface");ground.AddComponent<MeshFilter>().sharedMesh=mesh;ground.AddComponent<MeshRenderer>().sharedMaterial=Material("Course Slate",new Color(.22f,.29f,.34f));ground.AddComponent<MeshCollider>().sharedMesh=mesh;
            var stripe=Material("Distance Markers",new Color(.54f,.66f,.65f));
            for(int z=0;z<=12;z++)for(int side=-1;side<=1;side+=2)
            {
                var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.name="Distance "+z;Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.position=new Vector3(side*1.85f,RiggleUnevenCourse.Height(side*1.85f,z)+.006f,z);marker.transform.localScale=new Vector3(.3f,.008f,.035f);marker.GetComponent<Renderer>().sharedMaterial=stripe;
            }
            var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(3.5f,2.1f,3.7f);camera.transform.LookAt(Vector3.up*.95f);camera.fieldOfView=34;camera.nearClipPlane=.05f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.085f,.115f);
            var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2.2f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(50,-35,0);
            var fill=new GameObject("Fill Light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.color=new Color(.73f,.83f,1);fill.transform.rotation=Quaternion.Euler(25,140,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.54f,.6f);
            var walk=rig.GetComponent(Integration("RiggleQuadrupedWalk"));var so=new SerializedObject(walk);so.FindProperty("followCamera").objectReferenceValue=camera;so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
