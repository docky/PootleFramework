using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Unity.Mathematics;
using Unity.Cinemachine;
using ProceduralPrimitivesUtil;
using Pootle;

// One explicit, scene-local authoring pass. Never runs in edit mode automatically.
public static class CoastalExplorationSetup
{
    const string ExtensionName = "05 · Exploration side routes";
    static Transform root, cameras;
    static Material stone, cap, trail, hedge, darkHedge, timber, coral;
    static PootleCameraFocus focus;
    static PootleZoneCameraDirector director;
    static List<PootleCameraZone> regions;
    static Material Material(string name, string hex)
    {
        string path="Assets/CoastalHike/Materials/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        ColorUtility.TryParseHtmlString("#"+hex,out var color);m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.05f);return m;
    }
    static GameObject Box(string name, Vector3 p, Vector3 size, Material material, bool climb=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root);
        go.transform.position=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;
        if(climb){var surface=go.AddComponent<PootleClimbSurface>();surface.rotateCamera=false;}
        return go;
    }
    static void Plateau(string name,Vector3 center,float radius,float top)
    {
        var go=new GameObject(name);go.transform.SetParent(root);go.transform.position=new Vector3(center.x,(top-3)*.5f-.13f,center.z);
        var c=go.AddComponent<Cone>();c.radius1=radius+2;c.radius2=radius;c.height=top+3-.26f;c.sides=14;c.heightSegs=1;c.capSegs=1;c.smooth=false;c.Apply();c.AddMeshCollider();go.AddComponent<MeshRenderer>().sharedMaterial=stone;
        var crown=new GameObject(name+" · crown");crown.transform.SetParent(root);crown.transform.position=new Vector3(center.x,top-.13f,center.z);
        var t=crown.AddComponent<Cone>();t.radius1=radius+.1f;t.radius2=radius;t.height=.26f;t.sides=14;t.heightSegs=1;t.capSegs=1;t.smooth=false;t.Apply();t.AddMeshCollider();crown.AddComponent<MeshRenderer>().sharedMaterial=cap;
    }
    static void Path(string name,Vector3 a,Vector3 b,float width=3.5f)
    {
        var delta=b-a;
        var go=Box(name,(a+b)*.5f-Vector3.up*.15f,new Vector3(width,.3f,delta.magnitude+.5f),trail);
        go.transform.rotation=Quaternion.LookRotation(delta,Vector3.up);
    }
    static void Mark(Vector3 p,string name)
    {
        Box(name+" · post",p+Vector3.up*.7f,new Vector3(.15f,1.4f,.15f),timber);
        var board=Box(name+" · coral arrow",p+Vector3.up*1.2f,new Vector3(.9f,.3f,.15f),coral);
        Object.DestroyImmediate(board.GetComponent<Collider>());
    }
    static PootleExplorationShot Shot(string name,Vector2 angles,float distance=28,ExplorationCameraMode mode=ExplorationCameraMode.FixedAngleFollow)
    {
        var go=new GameObject(name);go.transform.SetParent(cameras);
        var shot=go.AddComponent<PootleExplorationShot>();shot.focus=focus;shot.mode=mode;shot.pitchYaw=angles;shot.distance=distance;
        shot.Rig.Priority=0;shot.ApplySettings();return shot;
    }
    static PootleCameraZone Region(string name,PootleExplorationShot shot,Vector3 center,Vector3 size,int priority,Vector3 arrival)
    {
        var go=new GameObject(name);go.transform.SetParent(cameras);go.transform.position=center;
        var box=go.AddComponent<BoxCollider>();box.isTrigger=true;box.size=size;
        var region=go.AddComponent<PootleCameraZone>();region.cameraRig=shot.Rig;region.precedence=priority;
        var spawn=new GameObject("Test arrival · feet position");spawn.transform.SetParent(go.transform);spawn.transform.position=arrival;
        region.playerArrival=spawn.transform;regions.Add(region);return region;
    }
    static void ConfigureOldShot(CinemachineCamera rig,int index)
    {
        var old=rig.GetComponent<CinemachineFollow>();if(old)Object.DestroyImmediate(old);
        var shot=rig.GetComponent<PootleExplorationShot>();if(!shot)shot=rig.gameObject.AddComponent<PootleExplorationShot>();
        shot.focus=focus;shot.pitchYaw=new Vector2(55,index==2?-12:index==1?10:0);shot.distance=index==4?34:28;shot.fieldOfView = 30;
        shot.ApplySettings();
    }
    [MenuItem("Pootle/Coastal Hike/Add Exploration Camera Prototype")]
    public static void Upgrade()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode before authoring the scene.");
        if(SceneManager.GetActiveScene().path!=CoastalHikeBuilder.ScenePath)throw new System.InvalidOperationException("Open CoastalHike first.");
        if(GameObject.Find(ExtensionName))throw new System.InvalidOperationException("Prototype already installed. Edit its ordinary scene objects and inspectors.");
        root=new GameObject(ExtensionName).transform;
        stone=Material("Side route sandstone","9A947A");cap=Material("Side route sage","9AAD77");trail=Material("Trail sand","D4B782");
        hedge=Material("Tall hedge","376452");darkHedge=Material("Hedge trim","4B7C5C");timber=Material("Driftwood","685747");coral=Material("Trail coral","D57751");
        var player=Object.FindAnyObjectByType<PootleCharacter>();
        director=Object.FindAnyObjectByType<PootleZoneCameraDirector>();cameras=director.transform;
        regions=new List<PootleCameraZone>(director.zones);
        focus=new GameObject("Shared smoothed focus · independent of player yaw").AddComponent<PootleCameraFocus>();
        focus.transform.SetParent(cameras);focus.player=player.transform;focus.character=player;focus.Snap();
        director.character=player;director.focus=focus;director.brain=Camera.main.GetComponent<CinemachineBrain>();
        director.blendSeconds=1.2f;director.minimumDwell=.65f;
        director.brain.LensModeOverride=new CinemachineBrain.LensModeOverrideSettings{Enabled=true,DefaultMode=LensSettings.OverrideModes.Perspective};
        var basis=Camera.main.gameObject.AddComponent<PootleMovementBasis>();basis.playerInput=player.input;director.movementBasis=basis;
        var existing=new List<CinemachineCamera>{director.fallbackCamera};
        foreach(var region in regions)if(region && !existing.Contains(region.cameraRig))existing.Add(region.cameraRig);
        for(int i=0;i<existing.Count;i++)ConfigureOldShot(existing[i],i);
        // Existing broad vertical volumes are narrowed so the cliff's stacked routes can own height.
        foreach(var region in regions)
        {
            if(!region.playerArrival)
            {
                var spawn=new GameObject("Test arrival · feet position");spawn.transform.SetParent(region.transform);
                float y=region.precedence==10?3:region.precedence==20?7:region.precedence==30?11:15;
                spawn.transform.position=new Vector3(region.transform.position.x,y+.15f,region.transform.position.z);region.playerArrival=spawn.transform;
            }
        }
        // Curved shore: fixed and spline cameras share the same box for an explicit A/B comparison.
        var rail=Shot("CAM · Curved beach rail",new Vector2(55,0),28,ExplorationCameraMode.RailFollow);
        var path=new GameObject("RAIL · Shore guide · edit spline knots").AddComponent<SplineContainer>();path.transform.SetParent(cameras);
        path.Spline=new Spline(new[]{new BezierKnot(new float3(-23,1.5f,-5)),new BezierKnot(new float3(-13,1.5f,-13)),new BezierKnot(new float3(0,1.5f,-16)),new BezierKnot(new float3(14,1.5f,-12)),new BezierKnot(new float3(23,1.5f,-4))});
        path.Spline.SetTangentMode(TangentMode.AutoSmooth);rail.spline=path;rail.railOffset=new Vector3(0,24,-17);rail.railDamping=.4f;
        Region("ZONE · Beach rail · default comparison",rail,new Vector3(0,1,-5),new Vector3(62,7,28),2,new Vector3(-13,.15f,-13));
        var beachFixed=director.fallbackCamera.GetComponent<PootleExplorationShot>();
        Region("ZONE · Beach fixed · preview for A-B",beachFixed,new Vector3(0,1,-5),new Vector3(62,7,28),1,new Vector3(-13,.15f,-13));
        // West side room: three climbable walls, open toward the approaching camera.
        Plateau("Hidden courtyard shelf",new Vector3(-32,0,13),9.5f,3);
        Path("Courtyard link",new Vector3(-23,3.08f,11),new Vector3(-32,3.08f,11));
        Box("Hideaway · west climbing wall",new Vector3(-38,5.75f,14),new Vector3(.8f,5.5f,12),stone,true);
        Box("Hideaway · back climbing wall",new Vector3(-32,5.75f,20),new Vector3(12.8f,5.5f,.8f),stone,true);
        Box("Hideaway · east climbing wall",new Vector3(-26,5.75f,16),new Vector3(.8f,5.5f,8),stone,true);
        // Climb marks communicate the alternate route up onto the walls.
        for(int i=0;i<5;i++)Object.DestroyImmediate(Box("Climbing seam",new Vector3(-32+(i%2)*.5f,4+i*.8f,19.56f),new Vector3(.7f,.16f,.13f),coral).GetComponent<Collider>());
        Box("Courtyard climbing reward ledge",new Vector3(-32,8.35f,21.3f),new Vector3(8,.3f,3),trail);
        Box("Hideaway bench",new Vector3(-34,3.65f,17),new Vector3(2.3f,.25f,.7f),timber);
        foreach(float x in new[]{-34.8f,-33.2f})Box("Bench leg",new Vector3(x,3.3f,17),new Vector3(.2f,.6f,.5f),timber);
        Mark(new Vector3(-25,3,10),"Hidden courtyard");
        var room=Shot("CAM · Three-wall hideaway",new Vector2(57,0),25,ExplorationCameraMode.StaticOrBounded);
        room.focusOrigin=new Vector3(-32,4.5f,13);room.anchor=room.focusOrigin-room.AuthoredRotation*Vector3.forward*25;room.bounded=true;room.positionBounds=new Vector3(4,10,8);room.airborneWidening=2;
        Region("ZONE · Hidden three-wall courtyard",room,new Vector3(-32,6,13),new Vector3(18,15,20),55,new Vector3(-32,3.2f,10));
        foreach(var boulder in Object.FindObjectsByType<Transform>())
            if(boulder.name=="Weathered trail boulder" && boulder.position.y<2 && boulder.position.x>20)
                boulder.position=new Vector3(boulder.position.x>23?25:26,boulder.position.y,boulder.position.x>23?-9:-5);
        // Tall, deliberately simple hedge switchbacks. High pitch reveals passages over the walls.
        Box("Hedge garden floor",new Vector3(17,.025f,1.5f),new Vector3(15,.05f,16),cap);
        Box("Hedge · west",new Vector3(10,3,1.5f),new Vector3(1.1f,6,15),hedge);
        Box("Hedge · north",new Vector3(17,3,9),new Vector3(15,6,1.1f),hedge);
        Box("Hedge · east lower",new Vector3(24,3,-.5f),new Vector3(1.1f,6,11),hedge);
        Box("Hedge · entrance left",new Vector3(12.5f,3,-6),new Vector3(6,6,1.1f),hedge);
        Box("Hedge · entrance right",new Vector3(22,3,-6),new Vector3(5,6,1.1f),hedge);
        Box("Hedge · inner spine",new Vector3(17,3,3),new Vector3(1.1f,6,8),darkHedge);
        Box("Hedge · short return",new Vector3(13,3,3),new Vector3(5,6,1.1f),darkHedge);
        Mark(new Vector3(17,0,-8),"Hedge garden entrance");Mark(new Vector3(25,0,7),"Hedge garden east exit");
        var overhead=Shot("CAM · Tall hedges · high view",new Vector2(72,0),32);overhead.fieldOfView = 30;overhead.deadZone=new Vector2(.15f,.14f);overhead.airborneWidening=0;
        Region("ZONE · Tall hedges · reveal passages",overhead,new Vector3(17,3,1),new Vector3(20,14,23),60,new Vector3(19,.2f,-4));
        // East loop: walk around the lower cliff, climb its face, or use the upper return ramp.
        Plateau("Cliff corner lower shelf",new Vector3(29,0,27),11,2);
        Path("Coastal return · beach to cliff",new Vector3(26,.08f,7),new Vector3(29,2.08f,15.5f),4);
        Path("Coastal return · shelf landing",new Vector3(29,2.08f,15.5f),new Vector3(29,2.08f,20),4);
        Box("Cliff corner · climbable monolith",new Vector3(29,6,30),new Vector3(8,8,9),stone,true);
        for(int i=0;i<7;i++)Object.DestroyImmediate(Box("Cliff climbing seam",new Vector3(28+(i%2)*.6f,2.8f+i,25.43f),new Vector3(.7f,.15f,.12f),coral).GetComponent<Collider>());
        Box("Lower route under upper bridge",new Vector3(35,1.88f,36),new Vector3(5,.4f,13),trail);
        Box("Upper bridge over lower route",new Vector3(30,9.88f,36),new Vector3(15,.4f,4),trail);
        Path("Upper route return to sun terrace",new Vector3(23,10.08f,36),new Vector3(18,7.08f,39),4);
        Path("Lower loop climb to sun terrace",new Vector3(35,2.08f,41),new Vector3(22,7.08f,43),4);
        Path("Lower loop landing link",new Vector3(22,7.08f,43),new Vector3(18,7.08f,40),4);
        Mark(new Vector3(34,2,24),"Cliff corner · walk or climb");Mark(new Vector3(34,10,36),"Glide toward sun terrace");
        var cornerA=Shot("CAM · Cliff approach",new Vector2(55,-35),27);
        Region("ZONE · Cliff corner south",cornerA,new Vector3(29,5,25),new Vector3(22,13,14),65,new Vector3(29,2.2f,24));
        var cornerB=Shot("CAM · Cliff corner north",new Vector2(58,-90),27);
        Region("ZONE · Lower stacked route",cornerB,new Vector3(34,4,36.5f),new Vector3(15,7.5f,13),70,new Vector3(35,2.2f,36));
        var upper=Shot("CAM · Upper bridge and glide",new Vector2(55,25),30);upper.airborneWidening=6;
        Region("ZONE · Upper stacked route",upper,new Vector3(29,12,34),new Vector3(19,7,12),80,new Vector3(35,10.2f,36));
        var overlook=existing[4].GetComponent<PootleExplorationShot>();overlook.mode=ExplorationCameraMode.StaticOrBounded;
        overlook.pitchYaw=new Vector2(50,0);overlook.fieldOfView = 30;overlook.focusOrigin=new Vector3(2,16.5f,62);overlook.anchor=overlook.focusOrigin-overlook.AuthoredRotation*Vector3.forward*34;overlook.bounded=false;
        // A second static shot on the broad beach landing makes screen-space movement easy to compare.
        var jetty=Shot("CAM · Jetty static viewpoint",new Vector2(55,0),27,ExplorationCameraMode.StaticOrBounded);
        jetty.focusOrigin=new Vector3(7,1.5f,-12);jetty.anchor=jetty.focusOrigin-jetty.AuthoredRotation*Vector3.forward*27;jetty.airborneWidening=0;
        Region("ZONE · Jetty static composition",jetty,new Vector3(7,1,-12),new Vector3(13,7,13),25,new Vector3(7,.3f,-10));
        director.zones=regions.ToArray();
        foreach(var shot in Object.FindObjectsByType<PootleExplorationShot>()){shot.ApplySettings();shot.TickShot(.02f);}
        var overlay=cameras.gameObject.AddComponent<PootleCameraDebugOverlay>();overlay.director=director;
        director.EvaluateAt(0,false,true);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Selection.activeGameObject=cameras.gameObject;
        Debug.Log("Coastal prototype installed: fixed / spline rail / static-bounded shots, box regions, wall routes and hedge garden.");
    }
}
