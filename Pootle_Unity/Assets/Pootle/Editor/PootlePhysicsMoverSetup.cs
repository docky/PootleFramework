using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    public static class PootlePhysicsMoverSetup
    {
        [MenuItem("Pootle/Add Physics Movers")]
        public static void ApplyMenu()=>Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode first");
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var old=GameObject.Find("Physics mover examples");if(old)Object.DestroyImmediate(old);
            var root=new GameObject("Physics mover examples").transform;Undo.RegisterCreatedObjectUndo(root.gameObject,"Add physics movers");
            var floor=GameObject.Find("Helix floor extension").GetComponent<Renderer>().sharedMaterial;
            var coral=Material("Physics rotor coral",new Color(1,.35f,.25f));
            var lime=Material("Physics shuttle lime",new Color(.6f,.9f,.15f));
            var gold=Material("Physics pendulum gold",new Color(1,.66f,.14f));
            var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Turnstile hub.mat");
            Shape("Physics movers floor",PrimitiveType.Cube,root,new Vector3(-52,-.5f,8),new Vector3(20,1,44),floor);
            var rotor=Hinge("Continuous spring rotor",root,new Vector3(-51,0,-4),Vector3.up,40,metal);
            for(int i=0;i<4;i++)
            {
                var arm=new GameObject("Paddle "+(i+1)).transform;arm.SetParent(rotor.transform,false);arm.localRotation=Quaternion.Euler(0,i*90,0);
                Shape("Paddle",PrimitiveType.Cube,arm,new Vector3(1.5f,.75f+i*.008f,0),new Vector3(3,.85f,.25f),coral);
                Shape("Soft end",PrimitiveType.Capsule,arm,new Vector3(3,.75f+i*.008f,0),new Vector3(.36f,.55f,.36f),metal);
            }
            var drive=rotor.AddComponent<PootleDrivenHinge>();drive.degreesPerSecond=40;drive.damper=450;
            Shape("Rotor hub",PrimitiveType.Cylinder,rotor.transform,new Vector3(0,.6f,0),new Vector3(.65f,.6f,.65f),metal);
            var shuttle=Shape("Reciprocating physics shuttle",PrimitiveType.Cube,root,new Vector3(-52,.8f,8),new Vector3(1,1.6f,3),lime);
            var rb=Body(shuttle,30);rb.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezePositionZ;
            shuttle.AddComponent<PootlePhysicsShuttle>();
            Shape("Shuttle travel strip",PrimitiveType.Cube,root,new Vector3(-52,.015f,8),new Vector3(8,.02f,.15f),metal,false);
            var pendulum=Hinge("Swinging physics pendulum",root,new Vector3(-52,3.5f,22),Vector3.forward,22,metal);
            Shape("Pendulum rod",PrimitiveType.Cylinder,pendulum.transform,new Vector3(0,-1.2f,0),new Vector3(.18f,1.2f,.18f),metal);
            Shape("Pendulum ball",PrimitiveType.Sphere,pendulum.transform,new Vector3(0,-2.4f,0),Vector3.one*1.3f,gold);
            var swing=pendulum.AddComponent<PootleDrivenHinge>();swing.oscillate=true;swing.amplitude=45;swing.period=4.5f;swing.spring=850;swing.damper=220;swing.maximumTorque=1400;
            var joint=pendulum.GetComponent<HingeJoint>();joint.useLimits=true;joint.limits=new JointLimits{min=-55,max=55};
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "Added continuously driven rotor, physics shuttle and swinging pendulum on a western extension";
        }
        static GameObject Hinge(string name,Transform parent,Vector3 at,Vector3 axis,float mass,Material material)
        {
            var anchor=new GameObject(name+" anchor");anchor.transform.SetParent(parent);anchor.transform.position=at;
            var baseBody=anchor.AddComponent<Rigidbody>();baseBody.isKinematic=true;
            Shape("Hinge cap",PrimitiveType.Sphere,anchor.transform,Vector3.zero,Vector3.one*.45f,material);
            var go=new GameObject(name);go.transform.SetParent(anchor.transform,false);Body(go,mass);
            var joint=go.AddComponent<HingeJoint>();joint.connectedBody=baseBody;joint.axis=axis;joint.anchor=Vector3.zero;joint.autoConfigureConnectedAnchor=false;joint.connectedAnchor=Vector3.zero;joint.enableCollision=false;
            return go;
        }
        static Rigidbody Body(GameObject go,float mass)
        {
            var rb=go.AddComponent<Rigidbody>();rb.mass=mass;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;rb.solverIterations=12;rb.maxAngularVelocity=4;return rb;
        }
        static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 at,Vector3 size,Material material,bool collision=true)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=at;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static Material Material(string name,Color color)
        {
            string path=PootleSceneSetup.Root+"/Samples/Materials/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material)return material;
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(material,path);return material;
        }
    }
}
