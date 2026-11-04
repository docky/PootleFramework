using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    public static class PootleEnvironmentSetup
    {
        [MenuItem("Pootle/Add Wind Bumpers Conveyors and Falling Crates")]
        public static void Apply()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            foreach(var item in Object.FindObjectsByType<PootleCarryable>())
            {
                if(!item.GetComponent<PootleFallingCrate>())item.gameObject.AddComponent<PootleFallingCrate>();
                item.Body.collisionDetectionMode=CollisionDetectionMode.Discrete;item.Body.isKinematic=true;item.Body.useGravity=false;item.Body.interpolation=RigidbodyInterpolation.None;
                EditorUtility.SetDirty(item.Body);
            }
            foreach(var surface in Object.FindObjectsByType<PootleClimbSurface>())
            {
                surface.cylindrical=surface.name.ToLowerInvariant().Contains("cylinder");
                surface.allowCornerWrapping=true;surface.maximumCornerAngle=95;surface.rotateCamera=true;EditorUtility.SetDirty(surface);
            }
            var profile=PootleSceneSetup.DefaultProfile();profile.smallObstacleHop=true;profile.smallHopHeight=1.05f;profile.smallHopDuration=.3f;profile.externalFacingHold=.9f;EditorUtility.SetDirty(profile);
            if(!GameObject.Find("Environment momentum course"))
            {
                var root=new GameObject("Environment momentum course").transform;
                var deck=Material("Conveyor teal",new Color(.12f,.52f,.55f));
                var yellow=Material("Momentum yellow",new Color(1,.76f,.2f));
                var blue=Material("Wind blue",new Color(.3f,.8f,1));
                var pink=Material("Bumper pink",new Color(.95f,.25f,.5f));
                var ground=AssetDatabase.LoadAssetAtPath<Material>(PootleSceneSetup.Root+"/Samples/Materials/Floor.mat");
                if(!ground)ground=deck;
                Box("Environment course floor",new Vector3(35,-.5f,7),new Vector3(30,1,40),root,ground);
                Belt("Straight uphill conveyor",new[]{new Vector3(25,.12f,-8),new Vector3(25,1.5f,2)},false,root,deck,yellow);
                Belt("Curving spline conveyor",new[]{new Vector3(31,.12f,4),new Vector3(37,.9f,7),new Vector3(39,1.7f,12),new Vector3(35,.12f,18)},true,root,deck,yellow);
                for(int i=0;i<3;i++)
                {
                    var bumper=GameObject.CreatePrimitive(PrimitiveType.Sphere);bumper.name="Pinball bumper "+(i+1);bumper.transform.SetParent(root);bumper.transform.position=new Vector3(24+i*4,.9f,20);bumper.transform.localScale=Vector3.one*1.8f;bumper.GetComponent<Renderer>().sharedMaterial=pink;bumper.AddComponent<PootleBumper>();
                    var band=GameObject.CreatePrimitive(PrimitiveType.Cylinder);band.name="Bumper band";band.transform.SetParent(bumper.transform,false);band.transform.localScale=new Vector3(1.05f,.06f,1.05f);band.GetComponent<Renderer>().sharedMaterial=yellow;Object.DestroyImmediate(band.GetComponent<Collider>());
                }
                Wind("Updraft",new Vector3(22,5,8),new Vector3(4,10,4),new Vector3(0,14,0),root,blue);
                Wind("Side wind",new Vector3(30,2,-12),new Vector3(12,4,4),new Vector3(10,0,0),root,blue);
                Box("Small auto-hop · 0.8 m",new Vector3(19,.4f,-13),new Vector3(3,.8f,3),root,deck);
                Box("Small auto-hop · 1.0 m",new Vector3(23,.5f,-13),new Vector3(3,1,3),root,deck);
                var corner=Box("Corner wrapping · 95 degree limit",new Vector3(43,2.5f,-2),new Vector3(4,5,4),root,blue);corner.AddComponent<PootleClimbSurface>();
                Label("CONVEYORS · walk against the moving arrows\nStraight incline + smooth spline",new Vector3(28,.04f,0),root);
                Label("BUMPER MOMENTUM · facing stays put",new Vector3(22,.04f,23),root);
                Label("UPDRAFT · jump or glide into wind",new Vector3(19,.04f,11),root);
                Label("SIDE WIND",new Vector3(27,.04f,-15),root);
                Label("CRATES · jump, drop beneath, land, jump again\nSlow start, fast fall · player rides the descent",new Vector3(3,.04f,-5),root);
            }
            foreach(var belt in Object.FindObjectsByType<PootleSplineConveyor>())
            {
                var mesh=belt.GetComponent<MeshFilter>().sharedMesh;
                var settings=new SerializedObject(mesh);
                var prebake=settings.FindProperty("m_PreBakeTriangleCollisionMesh");
                if(prebake!=null){prebake.boolValue=true;settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(mesh);}
            }
            var courseFloor=GameObject.Find("Environment course floor");
            if(courseFloor)courseFloor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(PootleSceneSetup.Root+"/Samples/Materials/Floor.mat");
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        private static void Wind(string name,Vector3 center,Vector3 size,Vector3 velocity,Transform parent,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=center;
            go.AddComponent<BoxCollider>().size=size;var wind=go.AddComponent<PootleWind>();wind.localVelocity=velocity;
            // Sparse arrows show the volume without obscuring the character.
            for(int i=0;i<5;i++)
            {
                var arrow=new GameObject("Wind arrow");arrow.transform.SetParent(go.transform);arrow.transform.position=center+new Vector3((i%3-1)*size.x*.27f,(i/3-.5f)*size.y*.4f,0);
                arrow.transform.rotation=Quaternion.LookRotation(velocity.normalized);
                var line=arrow.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=material;line.widthMultiplier=.06f;line.positionCount=5;
                line.SetPositions(new[]{Vector3.zero,Vector3.forward*.8f,new Vector3(-.2f,0,.55f),Vector3.forward*.8f,new Vector3(.2f,0,.55f)});
            }
        }
        private static void Belt(string name,Vector3[] points,bool curved,Transform parent,Material material,Material arrowMat)
        {
            var go=new GameObject(name);go.transform.SetParent(parent);var path=go.AddComponent<SplineContainer>();var spline=new Spline();
            foreach(var p in points)spline.Add(new BezierKnot((float3)p),curved?TangentMode.AutoSmooth:TangentMode.Linear);path.Spline=spline;
            var belt=go.AddComponent<PootleConveyor>();belt.speed=3;
            var adapter=go.AddComponent<PootleSplineConveyor>();adapter.path=path;
            const int sections=160;var vertices=new Vector3[(sections+1)*4];var indices=new int[sections*24];int index=0;
            for(int i=0;i<=sections;i++)
            {
                float t=i/(float)sections;Vector3 p=path.EvaluatePosition(t),tangent=path.EvaluateTangent(t),right=Vector3.Cross(Vector3.up,tangent).normalized*1.4f;
                vertices[i*4]=p-right;vertices[i*4+1]=p+right;vertices[i*4+2]=p-right-Vector3.up*.22f;vertices[i*4+3]=p+right-Vector3.up*.22f;
                if(i==sections)continue;int a=i*4,b=a+4;
                void Tri(int x,int y,int z){indices[index++]=x;indices[index++]=y;indices[index++]=z;}
                Tri(a,b,a+1);Tri(a+1,b,b+1);Tri(a+2,a+3,b+2);Tri(a+3,b+3,b+2);
                Tri(a,a+2,b);Tri(a+2,b+2,b);Tri(a+1,b+1,a+3);Tri(a+3,b+1,b+3);
            }
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh,PootleSceneSetup.Root+"/Samples/"+name+".asset");go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;
            var markers=go.AddComponent<PootleConveyorMarkers>();markers.path=path;markers.belt=belt;markers.markers=new Transform[12];
            for(int i=0;i<12;i++)
            {
                var arrow=new GameObject("Flow arrow");arrow.transform.SetParent(go.transform);float t=i/12f;arrow.transform.SetPositionAndRotation((Vector3)path.EvaluatePosition(t)+Vector3.up*.025f,Quaternion.LookRotation(path.EvaluateTangent(t)));
                var line=arrow.AddComponent<LineRenderer>();line.useWorldSpace=false;line.sharedMaterial=arrowMat;line.widthMultiplier=.09f;line.positionCount=3;line.SetPositions(new[]{new Vector3(-.4f,0,-.2f),new Vector3(0,0,.2f),new Vector3(.4f,0,-.2f)});markers.markers[i]=arrow.transform;
            }
        }
        private static GameObject Box(string name,Vector3 position,Vector3 size,Transform parent,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
        }
        private static Material Material(string name,Color color)
        {
            string path=PootleSceneSetup.Root+"/Samples/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat)return mat;
            mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};AssetDatabase.CreateAsset(mat,path);return mat;
        }
        private static void Label(string text,Vector3 position,Transform parent)
        {
            var go=new GameObject(text.Split('\n')[0]);go.transform.SetParent(parent);go.transform.SetPositionAndRotation(position,Quaternion.Euler(90,0,0));var label=go.AddComponent<TextMesh>();label.text=text;label.fontSize=64;label.characterSize=.035f;label.color=Color.white;
        }
    }
}
