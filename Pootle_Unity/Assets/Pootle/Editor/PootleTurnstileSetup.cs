using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    public static class PootleTurnstileSetup
    {
        [MenuItem("Pootle/Add Turnstiles And One Way Floors")]
        public static void ApplyMenu()=>Apply();
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode first");
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var old=GameObject.Find("Turnstiles and one way floors");if(old)Undo.DestroyObjectImmediate(old);
            var root=new GameObject("Turnstiles and one way floors");Undo.RegisterCreatedObjectUndo(root,"Add traversal props");
            var pink=Material("Turnstile pink",new Color(.9f,.18f,.58f));
            var blue=Material("Turnstile blue",new Color(.1f,.65f,.9f));
            var hub=Material("Turnstile hub",new Color(.25f,.3f,.36f));
            var floor=Material("One way mint",new Color(.35f,.95f,.7f));
            var groundMaterial=GameObject.Find("Helix floor extension").GetComponent<Renderer>().sharedMaterial;
            // Extend around the existing helix floor without overlapping coplanar faces.
            Ground(root.transform,"Turnstile south floor",new Vector3(-27,-.5f,-8),new Vector3(14,1,12),groundMaterial);
            Ground(root.transform,"Turnstile west floor",new Vector3(-38,-.5f,8),new Vector3(8,1,44),groundMaterial);
            for(int i=0;i<5;i++)
            {
                string[] names={"Turnstile · four arms","Turnstile · soft four arms","Turnstile · missing arm at 270 degrees","Turnstile · compact light spring","Turnstile · long firm spring"};
                Vector3[] centers={new Vector3(-24,0,2),new Vector3(-24,0,-8),new Vector3(-34,0,-3),new Vector3(-38,0,9),new Vector3(-38,0,20)};
                var gate=new GameObject(names[i]);gate.transform.SetParent(root.transform);gate.transform.position=centers[i];
                var builder=gate.AddComponent<TurnstileGateBuilder>();builder.armMeshMaterial=i==2?blue:pink;builder.hubMaterial=hub;
                builder.missingArm=i==2?3:-1;builder.defaultSpring=i==1?65:100;builder.defaultDamper=i==1?25:15;
                if(i==1)builder.armLength=2.2f;
                if(i==3){builder.armLength=1.15f;builder.armHeight=.8f;builder.defaultSpring=35;builder.defaultDamper=9;}
                if(i==4){builder.armLength=2.5f;builder.armHeight=1.3f;builder.defaultSpring=180;builder.defaultDamper=30;}
                builder.SetupOrRebuild();
            }
            AddPhysicsProps(root.transform,blue,hub);
            Vector3[] positions={new Vector3(20,1.4f,-2),new Vector3(23,2.8f,0),new Vector3(20,4.2f,3),new Vector3(25,2,7),new Vector3(-24,1.4f,9),new Vector3(-27,2.8f,11)};
            for(int i=0;i<positions.Length;i++)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=$"One way floor {i+1} · jump through from below";
                go.transform.SetParent(root.transform);go.transform.position=positions[i];go.transform.localScale=new Vector3(i==3?5:3,.2f,3);
                go.GetComponent<Renderer>().sharedMaterial=floor;go.AddComponent<PootleOneWayPlatform>();
                // Thin bright edging makes the landing surface legible from underneath.
                var stripe=GameObject.CreatePrimitive(PrimitiveType.Cube);stripe.name="Top edge";stripe.transform.SetParent(go.transform,false);
                stripe.transform.localPosition=new Vector3(0,.51f,-.46f);stripe.transform.localScale=new Vector3(1,.05f,.07f);
                stripe.GetComponent<Renderer>().sharedMaterial=blue;Object.DestroyImmediate(stripe.GetComponent<Collider>());
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            return "Added five varied turnstiles, physics boxes and seesaw, and six one-way floors";
        }
        static void AddPhysicsProps(Transform parent,Material boardMaterial,Material baseMaterial)
        {
            var baseGo=GameObject.CreatePrimitive(PrimitiveType.Cube);baseGo.name="Physics seesaw base";baseGo.transform.SetParent(parent);
            baseGo.transform.position=new Vector3(-29,.5f,-11);baseGo.transform.localScale=new Vector3(.5f,1,1.6f);
            baseGo.GetComponent<Renderer>().sharedMaterial=baseMaterial;
            var anchor=baseGo.AddComponent<Rigidbody>();anchor.isKinematic=true;
            var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.name="Physics seesaw · stand on either end";board.transform.SetParent(parent);
            board.transform.position=new Vector3(-29,1.1f,-11);board.transform.localScale=new Vector3(5,.25f,1.7f);
            board.GetComponent<Renderer>().sharedMaterial=boardMaterial;
            var body=board.AddComponent<Rigidbody>();body.mass=100;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;body.solverIterations=12;
            var hinge=board.AddComponent<HingeJoint>();hinge.connectedBody=anchor;hinge.axis=Vector3.forward;hinge.anchor=Vector3.zero;
            hinge.useLimits=true;hinge.limits=new JointLimits{min=-18,max=18};hinge.enableCollision=false;
            hinge.useSpring=true;hinge.spring=new JointSpring{spring=2500,damper=400,targetPosition=0};
            for(int i=0;i<2;i++)
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=i==0?"Physics push box · 5 kg":"Physics push box · 30 kg";
                box.transform.SetParent(parent);box.transform.position=new Vector3(-30-i*2.5f,.6f,-7-i*2);
                box.transform.localScale=Vector3.one*1.2f;box.GetComponent<Renderer>().sharedMaterial=boardMaterial;
                var rb=box.AddComponent<Rigidbody>();rb.mass=i==0?5:30;rb.interpolation=RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
            }
        }
        static void Ground(Transform parent,string name,Vector3 position,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);
            go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        }
        static Material Material(string name,Color color)
        {
            string path="Assets/Pootle/Samples/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
            m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(m,path);return m;
        }
    }
}
