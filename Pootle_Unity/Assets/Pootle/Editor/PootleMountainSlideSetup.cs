using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    /// <summary>Continuous swept trough; gaps are actual holes, never scripted launches.</summary>
    public static class PootleMountainSlideSetup
    {
        const string MeshPath="Assets/Pootle/Samples/Mountain slide.asset";
        static readonly float[] lengths={35,22*Mathf.PI*2/3,30,26*Mathf.PI,35,24*Mathf.PI*7/9,45,24*Mathf.PI*5/9,40};
        static readonly float[] turns={0,120,0,-180,0,140,0,-100,0};
        public static float Length {get{float n=0;foreach(float l in lengths)n+=l;return n;}}
        public static bool Gap(float s)=>(s>94 && s<100)||(s>212 && s<219);
        public static Vector3 Point(float distance)
        {
            float remaining=distance,heading=0;Vector3 p=new Vector3(116,0,-25);
            for(int i=0;i<lengths.Length;i++)
            {
                float take=Mathf.Clamp(remaining,0,lengths[i]);float k=turns[i]*Mathf.Deg2Rad/lengths[i];
                if(Mathf.Abs(k)<.0001f)p+=new Vector3(Mathf.Sin(heading),0,Mathf.Cos(heading))*take;
                else
                {
                    float end=heading+k*take;
                    p+=new Vector3((Mathf.Cos(heading)-Mathf.Cos(end))/k,0,(Mathf.Sin(end)-Mathf.Sin(heading))/k);
                    heading=end;
                }
                remaining-=take;if(remaining<=0)break;
            }
            // A gentle final transition reduces the descent gradient smoothly to zero.
            float runoutStart=Length-40,descent=Mathf.Min(distance,runoutStart);
            float r=Mathf.Max(0,distance-runoutStart);
            p.y=.15f+.15f*(runoutStart-descent)+.15f*(20-r+r*r/80);
            if(distance<runoutStart)p.y=.15f+.15f*(runoutStart-distance)+3;
            foreach(float lip in new[]{94f,212f})
                if(distance>lip-5 && distance<=lip){float t=(distance-lip+5)/5;p.y+=.9f*t*t;}
            return p;
        }
        static Vector3 Side(float s)
        {Vector3 tangent=Point(Mathf.Min(Length,s+.04f))-Point(Mathf.Max(0,s-.04f));return Vector3.Cross(Vector3.up,tangent).normalized;}
        static Vector3 Vertex(float s,float x)
        {
            float width=5.3f;
            foreach(float landing in new[]{100f,219f})if(s>=landing && s<landing+16)width+=1.3f*(1-(s-landing)/16);
            float bank=s<Length-30?6:6*Mathf.Pow(Mathf.Clamp01((Length-s)/30),2);
            return Point(s)+Side(s)*x*width+Vector3.up*(bank*x*x);
        }
        [MenuItem("Pootle/Add Mountain Slide Course")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode before authoring.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            var old=GameObject.Find("Mountain slide course");if(old)Object.DestroyImmediate(old);
            var root=new GameObject("Mountain slide course").transform;
            var snow=Material("Mountain ice",new Color(.58f,.88f,.95f));
            var rim=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Slide rim.mat");
            var pink=Material("Mountain access",new Color(.83f,.35f,.75f));
            const int across=32;int count=Mathf.CeilToInt(Length*4),width=across+1;
            var vertices=new Vector3[(count+1)*width*2];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
            var indices=new List<int>();
            for(int i=0;i<=count;i++)
            {
                float s=Length*i/count;
                for(int j=0;j<=across;j++)
                {
                    float x=j/(float)across*2-1;int a=(i*width+j)*2;
                    vertices[a]=Vertex(s,x);vertices[a+1]=vertices[a]-Vector3.up*.25f;
                    Vector3 along=Vertex(Mathf.Min(Length,s+.04f),x)-Vertex(Mathf.Max(0,s-.04f),x);
                    Vector3 cross=Vertex(s,Mathf.Min(1,x+.01f))-Vertex(s,Mathf.Max(-1,x-.01f));
                    normals[a]=Vector3.Cross(along,cross).normalized;normals[a+1]=-normals[a];
                    uv[a]=uv[a+1]=new Vector2(j/(float)across,s*.2f);
                    if(i==count || j==across || Gap(s) || Gap(Length*(i+1)/count))continue;
                    int b=a+width*2;
                    indices.AddRange(new[]{a,b,a+2,a+2,b,b+2,a+1,a+3,b+1,a+3,b+3,b+1});
                }
            }
            var mesh=new Mesh{name="Continuous mountain slide · two open jumps",indexFormat=IndexFormat.UInt32};
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.SetTriangles(indices,0);mesh.RecalculateBounds();
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if(saved){EditorUtility.CopySerialized(mesh,saved);Object.DestroyImmediate(mesh);mesh=saved;}else AssetDatabase.CreateAsset(mesh,MeshPath);
            var serialized=new SerializedObject(mesh);var bake=serialized.FindProperty("m_PreBakeTriangleCollisionMesh");
            if(bake!=null){bake.boolValue=true;serialized.ApplyModifiedPropertiesWithoutUndo();}
            var go=new GameObject("Mountain slide · banked downhill");go.transform.SetParent(root);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=snow;
            go.AddComponent<MeshCollider>().sharedMesh=mesh;var surface=go.AddComponent<PootleSlideSurface>();
            surface.maximumSpeed=28;surface.friction=.35f;surface.steeringAcceleration=14;surface.runoutDeceleration=4;
            // Separate rim strips stop at each gap so the jumps read clearly.
            float[] cuts={0,93.8f,100.2f,211.8f,219.2f,Length};
            for(int strip=0;strip<3;strip++)foreach(float x in new[]{-1f,1f})
            {
                float from=cuts[strip*2],to=cuts[strip*2+1];int n=Mathf.CeilToInt((to-from)*3);
                var line=new GameObject("Bank rim").AddComponent<LineRenderer>();line.transform.SetParent(root);
                line.sharedMaterial=rim;line.widthMultiplier=.12f;line.positionCount=n+1;
                for(int i=0;i<=n;i++)line.SetPosition(i,Vertex(Mathf.Lerp(from,to,i/(float)n),x)+Vector3.up*.06f);
            }
            Box("Mountain landing floor",new Vector3(200,-.5f,150),new Vector3(200,1,430),root,snow);
            Box("Mountain entry deck",Point(0)+new Vector3(0,-.2f,-4),new Vector3(10,.4f,8),root,snow);
            var character=Object.FindAnyObjectByType<PootleCharacter>();
            Pad("Mountain quick access pad",new Vector3(-3,.02f,-8),Point(0)+new Vector3(0,.05f,-5),0,character,root,pink);
            Label("MOUNTAIN SLIDE\nStep on the pink pad",new Vector3(-5,.05f,-10.5f),root);
            Pad("Mountain slide access pad",new Vector3(92,.02f,3),Point(0)+new Vector3(0,.05f,-5),0,character,root,pink);
            Pad("Mountain slide return pad",Point(Length)+new Vector3(-1.4f,0,4),new Vector3(88,.03f,3),-90,character,root,pink);
            Label("MOUNTAIN SLIDE\nStep on the pink pad to ride",new Vector3(87,.05f,6),root);
            Label("MOUNTAIN RUN · 413 m\n7 Follow cam · lean to steer · pull back to brake\nTwo open jumps · speed carries you across",Point(0)+new Vector3(-4,.05f,-7),root);
            foreach(float s in new[]{80f,198f})Label("JUMP AHEAD",Point(s)+Vector3.up*.06f,root);
            Label("FINISH · RETURN PAD",Point(Length)+new Vector3(-4,.05f,5),root);
            for(float s=15;s<Length-40;s+=35)
            {
                Vector3 p=Point(s)+Side(s)*6;
                Box("Mountain support",new Vector3(p.x,p.y*.5f,p.z),new Vector3(.6f,p.y,.6f),root,rim);
            }
            EditorUtility.SetDirty(mesh);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static void Pad(string name,Vector3 position,Vector3 target,float yaw,PootleCharacter character,Transform parent,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent);go.transform.position=position-Vector3.up*.05f;go.transform.localScale=new Vector3(3,.05f,3);
            Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
            var pad=go.AddComponent<PootleTravelPad>();pad.character=character;pad.destination=target;pad.facingYaw=yaw;
        }
        static Material Material(string name,Color color)
        {string path="Assets/Pootle/Samples/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(mat,path);}return mat;}
        static void Box(string name,Vector3 position,Vector3 scale,Transform root,Material mat)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;}
        static void Label(string text,Vector3 position,Transform root)
        {var go=new GameObject(text.Split('\n')[0]);go.transform.SetParent(root);go.transform.SetPositionAndRotation(position,Quaternion.Euler(90,0,0));var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=64;label.characterSize=.045f;label.color=Color.white;}
    }
}
