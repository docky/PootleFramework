using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Pootle.Editor
{
    public static class PootleNextFeaturesSetup
    {
        [MenuItem("Pootle/Add Rolling Carrying and Camera Styles")]
        public static void Apply()
        {
            var profile=PootleSceneSetup.DefaultProfile();
            profile.additionalAirJumps=1;profile.applySlopeSpeed=true;profile.uphillForce=10;profile.downhillForce=16;
            profile.slopeMaximumSpeed=12;profile.landingMomentumConversion=.2f;EditorUtility.SetDirty(profile);
            string inputPath=PootleSceneSetup.Root+"/Data/PootleControls.inputactions";
            var input=Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(inputPath));
            void Button(string map,string name,params string[] bindings)
            {
                var m=input.FindActionMap(map,true);var action=m.FindAction(name)??m.AddAction(name,InputActionType.Button);
                foreach(string binding in bindings)if(!action.bindings.Any(b=>b.path==binding))action.AddBinding(binding);
            }
            Button("Character","Roll","<Keyboard>/x","<Gamepad>/buttonWest");
            Button("Character","Carry","<Keyboard>/v","<Gamepad>/buttonNorth");
            for(int i=1;i<=6;i++)
            {
                var action=input.FindAction("Camera/Style"+i);
                if(action!=null)for(int b=action.bindings.Count-1;b>=0;b--)if(action.bindings[b].path.StartsWith("<Keyboard>/digit"))action.ChangeBinding(b).Erase();
                Button("Camera","Style"+i,"<Keyboard>/"+i);
            }
            input.name="PootleControls";
            File.WriteAllText(inputPath,input.ToJson());Object.DestroyImmediate(input);AssetDatabase.ImportAsset(inputPath,ImportAssetOptions.ForceSynchronousImport);
            var prefab=PrefabUtility.LoadPrefabContents(PootleSceneSetup.SamplePrefab);
            try{ConfigureCharacter(prefab.GetComponent<PootleCharacter>());PrefabUtility.SaveAsPrefabAsset(prefab,PootleSceneSetup.SamplePrefab);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var c=Object.FindAnyObjectByType<PootleCharacter>();ConfigureCharacter(c);
            foreach(var orbit in Object.FindObjectsByType<PootleOrbitInput>())ConfigureCamera(orbit);
            if(!GameObject.Find("Rolling and carrying course"))
            {
                var root=new GameObject("Rolling and carrying course").transform;
                var crate=Material("Carry props",new Color(.93f,.63f,.3f));
                for(int i=0;i<3;i++)
                {
                    Vector3 position=i==0?new Vector3(0,.45f,-2.5f):new Vector3(2+i*1.2f,.45f,-1);
                    var go=Box("Carry cube "+(i+1),position,Vector3.one*.8f,root,crate);
                    var body=go.AddComponent<Rigidbody>();body.mass=2;body.interpolation=RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                    go.AddComponent<PootleCarryable>();
                    var stripe=Box("Top marker",position+Vector3.up*.405f,new Vector3(.65f,.015f,.12f),go.transform,Material("Ball marking",Color.cyan));
                    Object.DestroyImmediate(stripe.GetComponent<Collider>());
                }
                var tunnel=Material("Rolling tunnel",new Color(.7f,.78f,.85f));
                Box("Ball tunnel · 1.10 m clearance",new Vector3(14,1.25f,-2),new Vector3(4,.3f,4),root,tunnel);
                Box("Tunnel left",new Vector3(11.8f,.65f,-2),new Vector3(.4f,1.3f,4),root,tunnel);
                Box("Tunnel right",new Vector3(16.2f,.65f,-2),new Vector3(.4f,1.3f,4),root,tunnel);
                var label=new GameObject("New controls");label.transform.SetParent(root);label.transform.SetPositionAndRotation(new Vector3(10,.04f,-4.7f),Quaternion.Euler(90,0,0));
                var text=label.AddComponent<TextMesh>();text.fontSize=64;text.characterSize=.022f;text.color=Color.white;
                text.text="X / WEST  Toggle ball    V / NORTH  Pick up or drop\nJump twice for double jump    Ball fits under tunnel\n1 Orbit   2 Close   3 Chase   4 Angled   5 Overhead   6 Side";
            }
            // Keep the rolling course clear of the moving hook's hanging-body sweep.
            GameObject.Find("Ball tunnel · 1.10 m clearance").transform.position=new Vector3(14,1.25f,-2);
            GameObject.Find("Tunnel left").transform.position=new Vector3(11.8f,.65f,-2);
            GameObject.Find("Tunnel right").transform.position=new Vector3(16.2f,.65f,-2);
            GameObject.Find("New controls").transform.position=new Vector3(10,.04f,-4.7f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(c);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        public static void ConfigureCharacter(PootleCharacter c)
        {
            if(!c.GetComponent<PootleCarrying>())c.gameObject.AddComponent<PootleCarrying>();
            var visual=c.GetComponent<PootleBallVisual>()??c.gameObject.AddComponent<PootleBallVisual>();visual.character=c;
            if(!visual.ball)
            {
                var ball=GameObject.CreatePrimitive(PrimitiveType.Sphere);ball.name="Rolling ball";ball.transform.SetParent(c.transform,false);
                ball.transform.localScale=Vector3.one*c.GetComponent<PootleMotor>().Radius*2;
                Object.DestroyImmediate(ball.GetComponent<Collider>());
                ball.GetComponent<Renderer>().sharedMaterial=Material("Rolling pink",new Color(.9f,.15f,.74f));
                for(int i=0;i<3;i++)
                {
                    var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);marker.name="Rotation marker";marker.transform.SetParent(ball.transform,false);
                    marker.transform.localPosition=Quaternion.Euler(0,0,i*120)*Vector3.up*.47f;marker.transform.localScale=new Vector3(.22f,.09f,.22f);
                    marker.transform.localRotation=Quaternion.Euler(0,0,i*120);Object.DestroyImmediate(marker.GetComponent<Collider>());
                    marker.GetComponent<Renderer>().sharedMaterial=Material("Ball marking",Color.cyan);
                }
                visual.ball=ball.transform;ball.SetActive(false);
            }
            EditorUtility.SetDirty(visual);EditorUtility.SetDirty(c);
        }
        public static void ConfigureCamera(PootleOrbitInput orbit)
        {
            var styles=orbit.GetComponent<PootleCameraStyles>()??orbit.gameObject.AddComponent<PootleCameraStyles>();
            styles.character=orbit.character;styles.actions=orbit.actions;EditorUtility.SetDirty(styles);
        }
        private static Material Material(string name,Color color)
        {
            string path=PootleSceneSetup.Root+"/Samples/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};
            AssetDatabase.CreateAsset(material,path);return material;
        }
        private static GameObject Box(string name,Vector3 position,Vector3 scale,Transform parent,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
    }
}
