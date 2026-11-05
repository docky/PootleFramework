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
            for(int i=0;i<3;i++)
            {
                var gate=new GameObject(i==2?"Turnstile · missing arm at 270 degrees":i==1?"Turnstile · soft four arms":"Turnstile · four arms");
                gate.transform.SetParent(root.transform);gate.transform.position=i==0?new Vector3(-24,0,2):i==1?new Vector3(-24,0,-8):new Vector3(-34,0,-3);
                var builder=gate.AddComponent<TurnstileGateBuilder>();builder.armMeshMaterial=i==2?blue:pink;builder.hubMaterial=hub;
                builder.missingArm=i==2?3:-1;builder.defaultSpring=i==1?65:100;builder.defaultDamper=i==1?12:15;builder.SetupOrRebuild();
            }
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
            return "Added two four-arm turnstiles, one missing-arm turnstile, and six one-way floors";
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
