using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using ProceduralPrimitivesUtil;
using Pootle;

public static class CoastalHikeBuilder
{
    public const string Root = "Assets/CoastalHike";
    public const string ScenePath = Root + "/CoastalHike.unity";
    static Material sand, cliff, grass, meadow, rock, bark, leaves, water, foam, trail, red, cream;
    static Transform land, detail;
    static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
    static Material Mat(string name, string hex)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", Hex(hex)); m.SetFloat("_Smoothness", .05f); return m;
    }
    static Transform Group(string name) => new GameObject(name).transform;
    static GameObject Box(string name, Vector3 p, Vector3 scale, Material mat, Transform parent, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent);
        go.transform.position = p; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static Cone Cone(string name, Vector3 center, float height, float bottom, float top, Material mat, Transform parent, int sides = 14, bool collision = true)
    {
        var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = center;
        var cone = go.AddComponent<Cone>(); cone.radius1 = bottom; cone.radius2 = top;
        cone.height = height; cone.sides = sides; cone.heightSegs = 1; cone.capSegs = 1; cone.smooth = false; cone.Apply();
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collision) cone.AddMeshCollider(); return cone;
    }
    static void Plateau(string name, float x, float z, float top, float bottom, float radius, Material cap, float depth = 1)
    {
        var body = Cone(name + " · stone", new Vector3(x, (top - .25f + bottom) * .5f, z), top - .25f - bottom, radius + 2.5f, radius, cliff, land);
        body.transform.localScale = new Vector3(1, 1, depth);
        var lip = Cone(name + " · flat crown", new Vector3(x, top - .13f, z), .26f, radius + .15f, radius, cap, land);
        lip.transform.localScale = new Vector3(1, 1, depth);
    }
    static void Ramp(Vector3 a, Vector3 b, string name)
    {
        var delta = b - a;
        var go = Box(name, (a+b)*.5f - Vector3.up*.14f, new Vector3(3.8f,.3f,delta.magnitude+.7f),trail,land);
        go.transform.rotation = Quaternion.LookRotation(delta, Vector3.up);
        for (int i=0;i<=5;i++)
        {
            var p = Vector3.Lerp(a,b,i/5f);
            var right = Vector3.Cross(Vector3.up,delta).normalized;
            foreach (var side in new[]{-1,1})
                Cone("Trail edge stone",p+right*(side*2.05f)+Vector3.up*.12f,.24f,.32f,.23f,rock,detail,6,false);
        }
    }
    static void Tree(float x,float y,float z,float size=1)
    {
        var root=Group("Wind pine");root.SetParent(detail);root.position=new Vector3(x,y,z);
        Cone("Trunk",root.position+Vector3.up*1.1f*size,2.2f*size,.23f*size,.15f*size,bark,root,7);
        Cone("Lower canopy",root.position+Vector3.up*2.2f*size,2.3f*size,1.45f*size,.15f*size,leaves,root,7,false);
        Cone("Upper canopy",root.position+Vector3.up*3.3f*size,1.9f*size,1.05f*size,.06f*size,grass,root,7,false);
    }
    static void Sign(Vector3 p, float yaw)
    {
        var root=Group("Coral trail waymarker");root.SetParent(detail);root.position=p;
        Box("Post",p+Vector3.up*.65f,new Vector3(.13f,1.3f,.13f),bark,root);
        var board=Box("Summit arrow",p+Vector3.up*1.1f,new Vector3(.85f,.26f,.12f),red,root,false);
        board.transform.rotation=Quaternion.Euler(0,yaw,-8);
    }
    static CinemachineCamera CameraRig(string name, Transform target, Vector3 offset, float fov, Transform parent)
    {
        var go=new GameObject(name);go.transform.SetParent(parent);
        go.transform.rotation=Quaternion.LookRotation(-offset+Vector3.forward*3,Vector3.up);
        var cam=go.AddComponent<CinemachineCamera>();cam.Follow=target;cam.Lens.FieldOfView=fov;cam.Priority=0;
        var follow=go.AddComponent<CinemachineFollow>();follow.FollowOffset=offset;
        follow.TrackerSettings.BindingMode=BindingMode.WorldSpace;follow.TrackerSettings.PositionDamping=new Vector3(.65f,.8f,.65f);
        return cam;
    }
    static PootleCameraZone Zone(string name, Vector3 center,Vector3 size,int priority,CinemachineCamera cam,Transform parent)
    {
        var go=new GameObject(name);go.transform.SetParent(parent);go.transform.position=center;
        var box=go.AddComponent<BoxCollider>();box.size=size;box.isTrigger=true;
        var zone=go.AddComponent<PootleCameraZone>();zone.cameraRig=cam;zone.precedence=priority;return zone;
    }
    [MenuItem("Pootle/Coastal Hike/Open New Scene")]
    public static void Open()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(ScenePath)) EditorSceneManager.OpenScene(ScenePath); else Build();
    }
    public static string Build(bool replaceGeneratedScene = false)
    {
        if (File.Exists(ScenePath) && !replaceGeneratedScene) throw new System.InvalidOperationException("Coastal Hike already exists; open it instead of overwriting authored changes.");
        Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        sand=Mat("Warm sand","E6CA91");cliff=Mat("Ochre strata","A68C6A");grass=Mat("Moss crown","88A65A");
        meadow=Mat("Meadow crown","A7B968");rock=Mat("Pale stone","CEC5A8");bark=Mat("Driftwood","685747");
        leaves=Mat("Pine shade","436F5D");water=Mat("Lagoon teal","4A9D9F");foam=Mat("Sea glass foam","ADD4C1");
        trail=Mat("Trail sand","D4B782");red=Mat("Trail coral","D57751");cream=Mat("Warm ivory","F0DEC1");
        land=Group("01 · Layered cone landscape");detail=Group("02 · Trail and landmarks");
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=Hex("BCD6D8");RenderSettings.ambientEquatorColor=Hex("A5B6AA");RenderSettings.ambientGroundColor=Hex("726651");
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogStartDistance=70;RenderSettings.fogEndDistance=190;RenderSettings.fogColor=Hex("BCD6D8");
        var light=new GameObject("Warm coastal afternoon").AddComponent<Light>();light.type=LightType.Directional;light.color=Hex("FFF0D5");light.intensity=1.1f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(43,-32,0);RenderSettings.sun=light;
        Box("Open sea",new Vector3(0,-1.3f,30),new Vector3(600,.2f,600),water,land,false);
        Plateau("01 · Crescent beach",0,0,0,-4,30,sand,.72f);
        Plateau("02 · Pine meadow",-9,17,3,-3,18,meadow,.85f);
        Plateau("03 · Sun terrace",9,34,7,-2,16,grass,.87f);
        Plateau("04 · High meadow",-4,49,11,-1,13,meadow,.9f);
        Plateau("05 · Lookout",2,62,15,1,9,grass,.9f);
        // The route bends across the terraces; every climb is a broad walkable ramp.
        Ramp(new Vector3(-20,.08f,-7),new Vector3(-20,3.08f,4.5f),"01 · Beach ascent");
        Ramp(new Vector3(-18,3.08f,20),new Vector3(-7.5f,7.08f,30),"02 · Meadow traverse");
        Ramp(new Vector3(13,7.08f,32),new Vector3(2,11.08f,38),"03 · Ridge ascent");
        Ramp(new Vector3(-9,11.08f,47),new Vector3(0,15.08f,53.5f),"04 · Lookout ascent");
        Ramp(new Vector3(-20,3.08f,4.5f),new Vector3(-18,3.08f,20),"Path · Pine meadow");
        Ramp(new Vector3(-7.5f,7.08f,30),new Vector3(13,7.08f,32),"Path · Sun terrace");
        Ramp(new Vector3(2,11.08f,38),new Vector3(-9,11.08f,47),"Path · High meadow");
        Ramp(new Vector3(0,15.08f,53.5f),new Vector3(2,15.08f,63),"Path · Summit");
        var mountains=Group("03 · Mountains · positive Z");
        var mountainA=Mat("Mountain sage","758D88");var mountainB=Mat("Mountain mist","8B9B95");
        float[] mx={-33,-19,-3,16,32};float[] mz={80,87,89,86,81};float[] mh={24,33,40,32,23};
        for(int i=0;i<mx.Length;i++)
        {
            var m=Cone("Distant mountain "+(i+1),new Vector3(mx[i],mh[i]*.5f-3,mz[i]),mh[i],16,2.3f,i%2==0?mountainA:mountainB,mountains,7);
            m.transform.rotation=Quaternion.Euler(0,i*23,0);
            var snow=Cone("Pale summit",new Vector3(mx[i],mh[i]-4.47f,mz[i]),3,2.36f+41.1f/mh[i],2.36f,cream,mountains,7,false);
            snow.transform.rotation=m.transform.rotation;
        }
        Tree(-22,3,16,1.3f);Tree(-18,3,23,.9f);Tree(-5,3,15,1.1f);Tree(1,3,18,.85f);
        Tree(19,7,31,1.2f);Tree(17,7,41,.9f);Tree(4,7,37,.8f);Tree(-12,11,45,.9f);Tree(-9,11,55,.75f);Tree(5,11,50,.8f);
        Sign(new Vector3(-17,0,-7),0);Sign(new Vector3(-19,3,19),-50);Sign(new Vector3(14,7,31),30);Sign(new Vector3(-10,11,46),0);
        // Flat rocks, shore foam and a modest landing jetty give the starting cove a clear identity.
        for(int i=0;i<13;i++)
        {
            float angle=(195+i*12)*Mathf.Deg2Rad;
            var p=new Vector3(Mathf.Sin(angle)*30.7f,-1.12f,Mathf.Cos(angle)*22.3f);
            var wave=Cone("Shore foam",p,.045f,2.2f,2.2f,foam,detail,10,false);wave.transform.localScale=new Vector3(1,1,.22f);wave.transform.rotation=Quaternion.Euler(0,-(195+i*12),0);
        }
        for(int i=0;i<11;i++) Box("Jetty plank",new Vector3(8,.12f,-10-i*.62f),new Vector3(3,.18f,.55f),bark,detail);
        foreach(float x in new[]{6.6f,9.4f})foreach(float z in new[]{-10f,-15.8f})Box("Jetty post",new Vector3(x,-.1f,z),new Vector3(.22f,1.5f,.22f),bark,detail);
        var drift=Box("Sun bleached driftwood",new Vector3(-5,.3f,-10),new Vector3(3.5f,.45f,.5f),rock,detail);drift.transform.rotation=Quaternion.Euler(0,-20,8);
        foreach(var p in new[]{new Vector3(21,0,1),new Vector3(24,0,5),new Vector3(-24,0,-6),new Vector3(15,7,43),new Vector3(-11,11,51)})
            Cone("Weathered trail boulder",p+Vector3.up*.6f,1.2f,1.5f,.8f,rock,detail,7);
        Box("Lookout bench seat",new Vector3(2,15.65f,64),new Vector3(2.6f,.18f,.65f),bark,detail);
        foreach(float x in new[]{1f,3f})Box("Bench foot",new Vector3(x,15.3f,64),new Vector3(.18f,.6f,.5f),bark,detail);
        Box("Summit flagpole",new Vector3(5,17.2f,64),new Vector3(.13f,4.4f,.13f),bark,detail);
        Box("Coral summit pennant",new Vector3(5.8f,18.6f,64),new Vector3(1.5f,.7f,.07f),red,detail,false);
        for(int i=0;i<3;i++)Cone("Summit cairn",new Vector3(-1,15.2f+i*.32f,64),.36f,.65f-i*.16f,.5f-i*.14f,rock,detail,7);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pootle/Samples/PootleCharacter.prefab");
        var player=(GameObject)PrefabUtility.InstantiatePrefab(prefab);player.name="Explorer · Lily";player.transform.position=new Vector3(-16,.15f,-5);
        var character=player.GetComponent<PootleCharacter>();player.AddComponent<PootleCoastalRecovery>();
        var camRoot=Group("04 · Authored cameras and zones");
        var output=new GameObject("Main Camera").AddComponent<Camera>();output.tag="MainCamera";output.clearFlags=CameraClearFlags.SolidColor;output.backgroundColor=Hex("BCD6D8");output.farClipPlane=500;output.nearClipPlane=.1f;output.gameObject.AddComponent<AudioListener>();
        var brain=output.gameObject.AddComponent<CinemachineBrain>();brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,1.2f);
        character.outputCamera=output.transform;PrefabUtility.RecordPrefabInstancePropertyModifications(character);
        var c0=CameraRig("CAM 01 · Cove",character.cameraTarget,new Vector3(0,18,-27),50,camRoot);
        var c1=CameraRig("CAM 02 · Pine meadow",character.cameraTarget,new Vector3(-5,19,-27),48,camRoot);
        var c2=CameraRig("CAM 03 · Sun terrace",character.cameraTarget,new Vector3(6,21,-29),48,camRoot);
        var c3=CameraRig("CAM 04 · High meadow",character.cameraTarget,new Vector3(-4,22,-29),48,camRoot);
        var c4=CameraRig("CAM 05 · Summit vista",character.cameraTarget,new Vector3(0,24,-34),56,camRoot);
        c4.transform.rotation=Quaternion.LookRotation(new Vector3(0,-24,46),Vector3.up);
        var director=camRoot.gameObject.AddComponent<PootleZoneCameraDirector>();director.target=player.transform;director.fallbackCamera=c0;
        director.zones=new[]{
            Zone("ZONE 02 · Pine meadow",new Vector3(-9,5,18),new Vector3(43,12,22),10,c1,camRoot),
            Zone("ZONE 03 · Sun terrace",new Vector3(9,10,35),new Vector3(39,12,20),20,c2,camRoot),
            Zone("ZONE 04 · High meadow",new Vector3(-4,14,49),new Vector3(32,12,15),30,c3,camRoot),
            Zone("ZONE 05 · Summit",new Vector3(2,19,63),new Vector3(25,16,16),40,c4,camRoot)};
        director.Evaluate();
        output.transform.SetPositionAndRotation(character.cameraTarget.position+new Vector3(0,18,-27),c0.transform.rotation);
        Physics.SyncTransforms();
        SceneView.lastActiveSceneView?.LookAt(new Vector3(0,8,31),Quaternion.Euler(42,0,0),80);
        AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
        Selection.activeGameObject=camRoot.gameObject;
        return ScenePath;
    }
}
