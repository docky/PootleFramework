using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    public static class PootleTraversalCourseSetup
    {
        [MenuItem("Pootle/Add Junctions Poles and Helix")]
        public static void Upgrade()
        {
            var prefab=PrefabUtility.LoadPrefabContents(PootleSceneSetup.SamplePrefab);
            try
            {
                if(!prefab.GetComponent<PootleRecovery>())prefab.AddComponent<PootleRecovery>();
                prefab.GetComponent<PootleCharacter>().profile.automaticGrip=true;
                EditorUtility.SetDirty(prefab.GetComponent<PootleCharacter>().profile);
                PrefabUtility.SaveAsPrefabAsset(prefab,PootleSceneSetup.SamplePrefab);
            }
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            if(!GameObject.Find("Junctions poles and helix"))
            {
                var root=new GameObject("Junctions poles and helix").transform;
                var yellow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Accent.mat");
                var blue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Climbable.mat");
                Rail("T junction · steer to select",new[]{new Vector3(0,1.4f,3),new Vector3(0,1.4f,9)},root,yellow);
                var wall=Primitive("Narrow climb wall · rail top",PrimitiveType.Cube,new Vector3(-18,1.5f,-8),new Vector3(.3f,3,6),root,blue,true);
                wall.AddComponent<PootleClimbSurface>();
                Rail("Wall top rail · automatic top out mount",new[]{new Vector3(-18,3.03f,-11),new Vector3(-18,3.03f,-5)},root,yellow);
                for(int i=0;i<5;i++)
                {var p=Pole("Short standing post "+(i+1),new Vector3(-12+i*2, .55f+i*.08f,-15),1.1f+i*.16f,root,yellow);p.climbable=false;}
                Pole("Elevated pole · open bottom",new Vector3(7,4,-14),4,root,yellow);
                var locked=Pole("Capped pole · locked ends",new Vector3(10,3,-14),6,root,yellow);locked.allowExitTop=locked.allowExitBottom=false;Markers(locked,yellow);
                var limited=Pole("Pole · 90 degree facing range",new Vector3(13,3,-14),6,root,yellow);limited.limitFacing=true;limited.minimumYaw=-45;limited.maximumYaw=45;
                var ladder=Pole("Ladder pole · face wall only",new Vector3(16,3,-14),6,root,yellow);ladder.limitFacing=true;ladder.minimumYaw=ladder.maximumYaw=0;
                Primitive("Ladder backing wall",PrimitiveType.Cube,new Vector3(16,3,-13),new Vector3(3,6,.5f),root,blue,true);
                var helix=new GameObject("Helix spline rail · ground pound to slide");helix.transform.SetParent(root);helix.transform.position=new Vector3(-28,0,18);
                var path=helix.AddComponent<SplineContainer>();var spline=new Spline();
                for(int i=0;i<=32;i++){float t=i/32f,a=t*Mathf.PI*4;spline.Add(new BezierKnot(new float3(Mathf.Cos(a)*4,.05f+t*7,Mathf.Sin(a)*4)),TangentMode.AutoSmooth);}
                path.Spline=spline;
                var extrusion=helix.AddComponent<SplineExtrude>();extrusion.Container=path;extrusion.Radius=.12f;extrusion.SegmentsPerUnit=8;extrusion.RebuildOnSplineChange=true;extrusion.Rebuild();
                helix.GetComponent<MeshRenderer>().sharedMaterial=yellow;
                var adapter=helix.AddComponent<PootleSplineRail>();adapter.path=path;adapter.Rebuild();
            }
            var course=GameObject.Find("Junctions poles and helix").transform;
            var existingHelix=Object.FindAnyObjectByType<PootleSplineRail>();
            existingHelix.transform.position=new Vector3(-28,0,18);existingHelix.Rebuild();EditorUtility.SetDirty(existingHelix.GetComponent<PootleRail>());
            var floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Floor.mat");
            if(!GameObject.Find("Helix floor extension"))Primitive("Helix floor extension",PrimitiveType.Cube,new Vector3(-27,-.5f,14),new Vector3(14,1,32),course,floor,true);
            if(!GameObject.Find("Pole testing apron"))Primitive("Pole testing apron",PrimitiveType.Cube,new Vector3(0,-.5f,-15.5f),new Vector3(44,1,5),course,floor,true);
            var apron=GameObject.Find("Pole testing apron");apron.transform.position=new Vector3(0,-.5f,-15.5f);apron.transform.localScale=new Vector3(44,1,5);
            var railMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Accent.mat");
            foreach(string wallName in new[]{"Wall glide","Corner A","Corner B"})
            {
                var wall=GameObject.Find(wallName);if(!wall)continue;
                if(!wall.GetComponent<PootleClimbSurface>())wall.AddComponent<PootleClimbSurface>();
                if(GameObject.Find(wallName+" · top rail"))continue;
                var bounds=wall.GetComponent<Collider>().bounds;Vector3 a=bounds.center,b=bounds.center;a.y=b.y=bounds.max.y+.03f;
                if(bounds.size.x>bounds.size.z){a.x=bounds.min.x;b.x=bounds.max.x;}else{a.z=bounds.min.z;b.z=bounds.max.z;}
                Rail(wallName+" · top rail",new[]{a,b},course,railMaterial);
            }
            foreach(var label in Object.FindObjectsByType<TextMesh>())
                if(label.text.StartsWith("LEFT STICK"))
                {label.text="LEFT STICK  Move    RIGHT STICK  Orbit\nA / CROSS  Jump    LB / L1  Run    RB / R1  Glide\nAUTO GRAB    B / CIRCLE  Detach or ground pound\nWASD  Move    RMB + MOUSE  Orbit    SPACE  Jump\nSHIFT  Run    F  Glide    Q  Detach / pound    R  Reset";EditorUtility.SetDirty(label);}
            foreach(var pole in Object.FindObjectsByType<PootlePole>())
            {Markers(pole,AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Accent.mat"));EditorUtility.SetDirty(pole);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        private static GameObject Primitive(string name,PrimitiveType type,Vector3 point,Vector3 scale,Transform parent,Material mat,bool solid)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent);go.transform.position=point;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        private static PootlePole Pole(string name,Vector3 point,float length,Transform parent,Material mat)
        {
            var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=point;
            var p=go.AddComponent<PootlePole>();p.length=length;p.radius=.12f;
            var capsule=go.AddComponent<CapsuleCollider>();capsule.height=length;capsule.radius=p.radius;capsule.isTrigger=true;
            Primitive("Pole visual",PrimitiveType.Cylinder,point,new Vector3(.24f,length*.5f,.24f),go.transform,mat,false);return p;
        }
        private static void Markers(PootlePole pole,Material mat)
        {
            if(!pole.topBlockMarker)pole.topBlockMarker=Primitive("Top stop sphere",PrimitiveType.Sphere,pole.Top,Vector3.one*pole.radius*3.6f,pole.transform,mat,false).transform;
            if(!pole.bottomBlockMarker)pole.bottomBlockMarker=Primitive("Bottom stop sphere",PrimitiveType.Sphere,pole.Bottom,Vector3.one*pole.radius*3.6f,pole.transform,mat,false).transform;
            pole.topBlockMarker.gameObject.SetActive(!pole.allowExitTop);pole.bottomBlockMarker.gameObject.SetActive(!pole.allowExitBottom);
        }
        private static void Rail(string name,Vector3[] points,Transform parent,Material mat)
        {
            var go=new GameObject(name);go.transform.SetParent(parent);go.AddComponent<PootleRail>().points=points;
            for(int i=0;i<points.Length-1;i++)
            {var v=Primitive("Rail visual",PrimitiveType.Cube,(points[i]+points[i+1])*.5f-Vector3.up*.06f,new Vector3(.22f,.12f,Vector3.Distance(points[i],points[i+1])),go.transform,mat,false);v.transform.rotation=Quaternion.LookRotation(points[i+1]-points[i]);}
        }
    }
}
