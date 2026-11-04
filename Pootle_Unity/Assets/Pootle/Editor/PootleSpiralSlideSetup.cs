using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    public static class PootleSpiralSlideSetup
    {
        private const string MeshPath="Assets/Pootle/Samples/Spiral slide.asset";
        private static readonly Vector3 Center=new Vector3(66,0,12);
        public static Vector3 Point(float t)
        {
            float angle=t*Mathf.PI*2.5f;
            return Center+new Vector3(Mathf.Cos(angle)*8,.15f+18*Mathf.Pow(1-t,1.15f),Mathf.Sin(angle)*8);
        }
        [MenuItem("Pootle/Add Spiral Slide and Expand Playground")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode before authoring the playground.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var floor=GameObject.Find("Environment course floor");
            if(floor){floor.transform.position=new Vector3(53,-.5f,10);floor.transform.localScale=new Vector3(66,1,64);}
            foreach(var belt in Object.FindObjectsByType<PootleConveyor>()){belt.rotateWithCurve=true;EditorUtility.SetDirty(belt);}
            if(!GameObject.Find("Spiral slide course"))Build();
            var lift=GameObject.Find("Slide access lift").GetComponent<PootleSplineMover>();
            lift.endpointPause=2;EditorUtility.SetDirty(lift);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            PootleSlideRideSetup.Apply();
        }
        private static void Build()
        {
            var root=new GameObject("Spiral slide course").transform;
            var orange=Material("Slide coral",new Color(.95f,.38f,.22f));
            var cream=Material("Slide rim",new Color(1,.85f,.5f));
            var teal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Conveyor teal.mat");
            var go=new GameObject("Spiral slide · slippery trough");go.transform.SetParent(root);
            const int sections=640,across=32,runoutSections=80,total=sections+runoutSections;
            int width=across+1;var vertices=new Vector3[(total+1)*width*2];var uv=new Vector2[vertices.Length];
            var indices=new System.Collections.Generic.List<int>();
            var left=new Vector3[total+1];var right=new Vector3[total+1];
            for(int i=0;i<=total;i++)
            {
                bool runout=i>sections;float t=Mathf.Min(i/(float)sections,1);
                Vector3 p=runout?Point(1)+Vector3.left*((i-sections)/(float)runoutSections*12):Point(t);
                Vector3 side=runout?Vector3.forward:new Vector3(Mathf.Cos(t*Mathf.PI*2.5f),0,Mathf.Sin(t*Mathf.PI*2.5f));
                float rim=runout?Mathf.Pow(1-(i-sections)/(float)runoutSections,2):1;
                for(int j=0;j<=across;j++)
                {
                    float x=(j/(float)across*2-1);int k=(i*width+j)*2;
                    vertices[k]=p+side*(x*2.4f)+Vector3.up*(x*x*1.8f*rim);
                    vertices[k+1]=vertices[k]-Vector3.up*.2f;
                    uv[k]=uv[k+1]=new Vector2(j/(float)across,i*.15f);
                    if(j==0)left[i]=vertices[k]+Vector3.up*.03f;
                    if(j==across)right[i]=vertices[k]+Vector3.up*.03f;
                    if(i==total || j==across)continue;
                    int a=k,b=k+width*2;
                    void Tri(int q,int r,int s){indices.Add(q);indices.Add(r);indices.Add(s);}
                    Tri(a,b,a+2);Tri(a+2,b,b+2);
                    Tri(a+1,a+3,b+1);Tri(a+3,b+3,b+1);
                }
            }
            // Close both outer sides, so the mesh remains a solid trough from any approach.
            for(int i=0;i<total;i++)foreach(int j in new[]{0,across})
            {
                int a=(i*width+j)*2,b=a+width*2;
                if(j==0)indices.AddRange(new[]{a,a+1,b,a+1,b+1,b});
                else indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});
            }
            var mesh=new Mesh{name="Continuous spiral slide",indexFormat=IndexFormat.UInt32};mesh.vertices=vertices;mesh.uv=uv;mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,MeshPath);
            var serialized=new SerializedObject(mesh);var bake=serialized.FindProperty("m_PreBakeTriangleCollisionMesh");
            if(bake!=null){bake.boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();}
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=orange;go.AddComponent<MeshCollider>().sharedMesh=mesh;go.AddComponent<PootleSlideSurface>();
            Rim("Inner rim",left,go.transform,cream);Rim("Outer rim",right,go.transform,cream);
            Box("Slide entry deck",new Vector3(74,17.95f,10),new Vector3(4.8f,.4f,4),root,teal);
            var pathGo=new GameObject("Slide lift path");pathGo.transform.SetParent(root);
            var path=pathGo.AddComponent<SplineContainer>();var spline=new Spline();
            spline.Add(new BezierKnot(new float3(78.4f,.2f,9)),TangentMode.Linear);
            spline.Add(new BezierKnot(new float3(78.4f,17.95f,9)),TangentMode.Linear);path.Spline=spline;
            var lift=Box("Slide access lift",new Vector3(78.4f,.2f,9),new Vector3(4,.4f,4),root,teal);
            lift.AddComponent<PootleMovingSurface>();var mover=lift.AddComponent<PootleSplineMover>();mover.path=path;mover.speed=3;mover.pingPong=true;
            for(int i=0;i<3;i++)
            {
                float t=(i+.25f)/3;Vector3 p=Point(t);
                p+=Vector3.ProjectOnPlane(p-Center,Vector3.up).normalized*3.2f;
                Box("Slide support "+(i+1),new Vector3(p.x,(p.y-.35f)*.5f,p.z),new Vector3(.45f,p.y-.35f,.45f),root,cream);
            }
            Label("SPIRAL SLIDE\nRide the lift · walk into the trough\nJump to leave the slide",new Vector3(69,.04f,3),root);
            Label("SLIDE RUNOUT",new Vector3(52,.04f,23),root);
            // Give the curved conveyor loose props with visible headings, ready for a rotation test.
            var belt=GameObject.Find("Curving spline conveyor").GetComponent<PootleSplineConveyor>();
            for(int i=0;i<2;i++)
            {
                float t=i==0?.05f:.85f;Vector3 point=belt.path.EvaluatePosition(t);
                var crate=Box("Conveyor turning crate "+(i+1),point+Vector3.up*.65f,Vector3.one*.8f,root,cream);
                crate.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(belt.path.EvaluateTangent(t),Vector3.up));
                crate.AddComponent<PootleCarryable>();crate.AddComponent<PootleFallingCrate>();
                var arrow=new GameObject("Heading arrow");arrow.transform.SetParent(crate.transform,false);arrow.transform.localPosition=Vector3.up*.51f;
                var line=arrow.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=teal;line.widthMultiplier=.08f;line.positionCount=5;
                line.SetPositions(new[]{new Vector3(0,0,-.3f),new Vector3(0,0,.3f),new Vector3(-.16f,0,.1f),new Vector3(0,0,.3f),new Vector3(.16f,0,.1f)});
            }
        }
        private static void Rim(string name,Vector3[] points,Transform parent,Material material)
        {var go=new GameObject(name);go.transform.SetParent(parent);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.widthMultiplier=.09f;line.positionCount=points.Length;line.SetPositions(points);}
        private static GameObject Box(string name,Vector3 position,Vector3 scale,Transform parent,Material material)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        private static Material Material(string name,Color color)
        {var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(mat,"Assets/Pootle/Samples/Materials/"+name+".mat");return mat;}
        private static void Label(string text,Vector3 position,Transform parent)
        {var go=new GameObject(text.Split('\n')[0]);go.transform.SetParent(parent);go.transform.SetPositionAndRotation(position,Quaternion.Euler(90,0,0));var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=64;label.characterSize=.045f;label.color=Color.white;}
    }
}
