using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Unity.Cinemachine;
using TMPro;
using Pootle;
using Object = UnityEngine.Object;

public static class CoastalSeatRidesSetup
{
    public const string RootName = "09 · Beach swings and mountain chair lift";
    static Transform root;
    static Material cream, coral, teal, wood, dark;
    static Transform Group(string name, Transform parent, Vector3 p)
    { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = p; return t; }
    static GameObject Box(string name, Transform parent, Vector3 p, Vector3 size, Material mat, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat; if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static void Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material mat, bool collision = false)
    { var go = Box(name, parent, (a+b)*.5f, new Vector3(width,width,Vector3.Distance(a,b)),mat,collision); go.transform.localRotation=Quaternion.LookRotation(b-a); }
    static void Sign(string text, Transform parent, Vector3 p, float fontSize = 2.4f)
    {
        var go = Group(text, parent, p);var label=go.gameObject.AddComponent<TextMeshPro>();
        label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        label.fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Coaster Assets/Station lettering.mat");
        label.text=text;label.fontSize=fontSize;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(8,1.4f);label.color=new Color(1,.95f,.78f);
    }
    static PootleRideSeat Seat(Transform holder, bool push)
    {
        var trigger=holder.gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,.5f,0);trigger.size=new Vector3(1.7f,2,2.1f);
        var passenger=holder.gameObject.AddComponent<PootleMachinePassenger>();passenger.seat=Group("Hip anchor",holder,new Vector3(0,.19f,0));
        var seat=holder.gameObject.AddComponent<PootleRideSeat>();seat.passenger=passenger;seat.requirePush=push;seat.groundedOnly=push;
        Box("Seat cushion",holder,Vector3.zero,new Vector3(push?1.1f:1.65f,.18f,push?.6f:1.15f),coral);
        if(!push)
        {
            Box("Lift backrest",holder,new Vector3(0,.5f,-.53f),new Vector3(1.75f,.9f,.13f),teal);
            Box("Lift foot platform",holder,new Vector3(0,-.5f,.32f),new Vector3(1.8f,.12f,1.6f),wood);
            foreach(int side in new[]{-1,1})
            {
                Box("Chair armrest",holder,new Vector3(side*.86f,.4f,0),new Vector3(.1f,.1f,1.1f),cream,false);
                Beam("Suspension arm",holder,new Vector3(side*.85f,0,-.45f),new Vector3(side*.85f,2.1f,-.45f),.1f,teal);
                Beam("Suspension shoulder",holder,new Vector3(side*.85f,2.1f,-.45f),new Vector3(0,2.5f,-.45f),.1f,teal);
            }
            Beam("Cable hanger",holder,new Vector3(0,2.5f,-.45f),new Vector3(0,3.5f,0),.13f,teal);
        }
        return seat;
    }
    static void Swing(string name, Vector3 position)
    {
        var frame=Group(name,root,position);
        foreach(int side in new[]{-1,1})
        {
            Beam("A-frame front leg",frame,new Vector3(side*1.65f,0,-1.65f),new Vector3(side*1.65f,4.55f,0),.2f,teal,true);
            Beam("A-frame rear leg",frame,new Vector3(side*1.65f,0,1.65f),new Vector3(side*1.65f,4.55f,0),.2f,teal,true);
            Beam("A-frame brace",frame,new Vector3(side*1.65f,1,-1.3f),new Vector3(side*1.65f,1,1.3f),.12f,cream,true);
        }
        Beam("Swing crossbar",frame,new Vector3(-1.9f,4.55f,0),new Vector3(1.9f,4.55f,0),.25f,coral);
        var pivot=Group("Pendulum pivot",frame,new Vector3(0,4.5f,0));
        foreach(int side in new[]{-1,1})Beam("Swing rope",pivot,new Vector3(side*.48f,0,0),new Vector3(side*.48f,-3.65f,0),.055f,cream);
        var chair=Group("Push to sit",pivot,new Vector3(0,-3.65f,0));var ride=Seat(chair,true);
        var swing=frame.gameObject.AddComponent<PootleSwing>();swing.pivot=pivot;swing.ride=ride;swing.ropeLength=3.65f;
        var cam=Group("CAM · stationary "+name,root,new Vector3(-1,8,-28));
        cam.LookAt(new Vector3(-10.5f,2,-23));var rig=cam.gameObject.AddComponent<CinemachineCamera>();rig.Priority=-100;rig.Lens.FieldOfView=44;
        cam.gameObject.AddComponent<PootleStationaryRideCamera>().passenger=ride.passenger;
    }
    static void Zone(PootleZoneCameraDirector director,List<PootleCameraZone> zones,string name,Vector3 center,Vector3 size,Vector3 arrival)
    {
        var cam=Group("CAM · "+name,root,Vector3.zero);var shot=cam.gameObject.AddComponent<PootleExplorationShot>();shot.focus=director.focus;
        shot.policy=AssetDatabase.LoadAssetAtPath<PootleCameraPolicy>("Assets/CoastalHike/Exploration Camera Policy.asset");shot.pitchYaw=new Vector2(48,0);shot.distance=36;shot.airborneWidening=0;shot.ApplySettings();shot.Rig.Priority=0;
        cam.position=arrival+Vector3.up*1.4f-shot.AuthoredRotation*Vector3.forward*shot.EffectiveDistance;
        var trigger=Group("ZONE · "+name,root,center);var box=trigger.gameObject.AddComponent<BoxCollider>();box.isTrigger=true;box.size=size;
        var zone=trigger.gameObject.AddComponent<PootleCameraZone>();zone.cameraRig=shot.Rig;zone.precedence=85;zone.playerArrival=Group("Arrival · "+name,root,arrival);zones.Add(zone);
    }
    static void Pulley(Vector3 p, string name)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(root,false);go.transform.position=p;go.transform.localScale=new Vector3(4.2f,.14f,4.2f);go.GetComponent<Renderer>().sharedMaterial=teal;Object.DestroyImmediate(go.GetComponent<Collider>());
        Box("Pulley centre cap",root,p+Vector3.up*.2f,new Vector3(.5f,.2f,.5f),coral,false);
    }
    [MenuItem("Pootle/Coastal Hike/Add Swings and Mountain Chair Lift")]
    public static void Build()
    {
        if(EditorApplication.isPlaying || SceneManager.GetActiveScene().path!=CoastalHikeBuilder.ScenePath)throw new InvalidOperationException("Open CoastalHike in Edit Mode first.");
        if(GameObject.Find(RootName))throw new InvalidOperationException("Seat rides already exist; edit the saved objects.");
        Material Load(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Coaster Assets/"+name+".mat");
        cream=Load("Ivory rails");coral=Load("Coral cart and ties");teal=Load("Deep teal steel");wood=Load("Cedar platform");dark=Load("Dark wheels");
        if(!wood)throw new InvalidOperationException("Coastal coaster materials are required.");
        root=Group(RootName,null,Vector3.zero);Undo.RegisterCreatedObjectUndo(root.gameObject,"Add beach swings and chair lift");
        Box("Beach play pier",root,new Vector3(-14,-.16f,-23),new Vector3(20,.4f,14),wood);
        for(int i=0;i<33;i++)Box("Play pier board seam",root,new Vector3(-23.7f+i*.6f,.046f,-23),new Vector3(.024f,.012f,13.9f),dark,false);
        Box("Beach approach boardwalk",root,new Vector3(-9,-.08f,-13.5f),new Vector3(3,.24f,5.2f),wood);
        foreach(int side in new[]{-1,1})Beam("Approach handrail",root,new Vector3(-9+side*1.5f,1,-16),new Vector3(-9+side*1.5f,1,-12),.1f,cream,true);
        foreach(float x in new[]{-23f,-5f})foreach(float z in new[]{-29f,-17f})
        { Box("Pier footing",root,new Vector3(x,-1.1f,z),new Vector3(.3f,2.2f,.3f),teal); }
        // Open edges around the swings allow jump dismounts; low edge markings leave the view clear.
        Box("Pier outer trim",root,new Vector3(-14,.05f,-29.9f),new Vector3(20,.1f,.15f),cream,false);
        Swing("Playground swing 01",new Vector3(-13,0,-23));Swing("Playground swing 02",new Vector3(-8,0,-23));
        Box("Swing instruction board",root,new Vector3(-10.5f,1.4f,-28.5f),new Vector3(6.5f,1.1f,.12f),teal,false);
        Sign("WALK INTO A SWING\nFORWARD / BACK TO PUMP · JUMP TO LEAVE",root,new Vector3(-10.5f,1.4f,-28.58f),2.2f);
        var path=Group("Chair lift · editable return loop",root,Vector3.zero).gameObject.AddComponent<SplineContainer>();
        Vector3[] points={new Vector3(-19,.75f,-23),new Vector3(-19,.75f,-19),new Vector3(-27,5,-7),new Vector3(-38,13,18),new Vector3(-39,18,45),new Vector3(-37,21.91f,74),new Vector3(-37,21.91f,80),new Vector3(-39,21.91f,82),new Vector3(-41,21.91f,80),new Vector3(-41,21.91f,74),new Vector3(-43,18,45),new Vector3(-42,13,18),new Vector3(-31,5,-7),new Vector3(-23,.75f,-19),new Vector3(-23,.75f,-23),new Vector3(-21,.75f,-25)};
        var spline=new Spline();foreach(var point in points)spline.Add(new BezierKnot(point),TangentMode.AutoSmooth);spline.Closed=true;path.Spline=spline;
        var cable=Group("Continuous lift cable",root,Vector3.zero).gameObject.AddComponent<LineRenderer>();cable.useWorldSpace=true;cable.sharedMaterial=teal;cable.widthMultiplier=.085f;cable.positionCount=601;
        for(int i=0;i<=600;i++)cable.SetPosition(i,(Vector3)path.EvaluatePosition(i/600f)+Vector3.up*3.5f);
        Pulley(new Vector3(-21,4.25f,-23),"Beach return pulley");Pulley(new Vector3(-39,25.41f,80),"Mountain return pulley");
        foreach(var tower in new[]{new Vector3(-21,0,-23),new Vector3(-40,10,18),new Vector3(-41,10,45),new Vector3(-39,21.16f,80)})
        {
            float height=tower.z==80?4.25f:tower.z==18?6.5f:tower.z==45?11.5f:4.25f;
            foreach(int side in new[]{-1,1})Box("Lift support column",root,tower+new Vector3(side*3.4f,height*.5f,0),new Vector3(.24f,height,.24f),teal);
            Beam("Lift support crosshead",root,tower+new Vector3(-3.6f,height,0),tower+new Vector3(3.6f,height,0),.2f,teal);
        }
        Box("Mountain lift landing",root,new Vector3(-39,20.96f,80),new Vector3(9,.4f,7),wood);
        Box("Landing link to skywalk",root,new Vector3(-34,21.04f,79.5f),new Vector3(4,.24f,2.8f),wood);
        foreach(int side in new[]{-1,1})Beam("Landing edge rail",root,new Vector3(-43.4f,22.16f,80+side*3.4f),new Vector3(-35,22.16f,80+side*3.4f),.12f,cream,true);
        Sign("MOUNTAIN CHAIR LIFT\nTOUCH TO SIT · JUMP TO RELEASE",root,new Vector3(-39,23.5f,76.45f),2.4f);
        Sign("CHAIR LIFT\nTOUCH TO SIT · JUMP TO RELEASE",root,new Vector3(-21,2.1f,-26.2f),2.4f);
        for(int i=0;i<2;i++)
        {
            var rig=Group("Chair lift carrier "+(i+1),root,Vector3.zero);var platform=Group("Automatic chair platform",rig,points[i==0?0:8]);
            var ride=Seat(platform,false);ride.releaseForwardSpeed=1;ride.boardingSeconds=.35f;
            var lift=rig.gameObject.AddComponent<PootleChairLift>();lift.path=path;lift.platform=platform;lift.ride=ride;lift.startFraction=i*.5f;lift.Initialize();
            var cam=Group("CAM · chair lift "+(i+1),root,Vector3.zero);cam.gameObject.AddComponent<CinemachineCamera>().Priority=-100;
            var follow=cam.gameObject.AddComponent<PootleMachineCamera>();follow.passenger=ride.passenger;follow.vehicle=platform;follow.explorationPolicy=AssetDatabase.LoadAssetAtPath<PootleCameraPolicy>("Assets/CoastalHike/Exploration Camera Policy.asset");
        }
        var director=Object.FindAnyObjectByType<PootleZoneCameraDirector>();var zones=new List<PootleCameraZone>(director.zones);
        Zone(director,zones,"Beach play pier",new Vector3(-14,3,-23),new Vector3(24,12,18),new Vector3(-10,.08f,-17));
        Zone(director,zones,"Chair lift mountain landing",new Vector3(-39,24,80),new Vector3(12,12,11),new Vector3(-36,21.24f,79));
        Undo.RecordObject(director,"Add seat ride camera regions");director.zones=zones.ToArray();
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveOpenScenes();Selection.activeGameObject=root.gameObject;
        Debug.Log("Added two push-to-seat swings with stationary cameras and two looping chair-lift platforms.");
    }
}
