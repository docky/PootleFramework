using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using Pootle;
using OinkRomp;
using Object=UnityEngine.Object;
public static class BuildOinkRomp
{
    const string Root="Assets/OinkRomp";
    const string Farm="Assets/Quirky Series/Ultimate Pack Vol.1/Mega Pack Vol.1/Farm Vol.1/";
    static Transform content;
    static Material grass,earth,wood,cream,coral,mud,gold,teal,rope,leaf,apple;
    static List<OinkRompPickup> pickups=new List<OinkRompPickup>();
    static List<OinkRompBird> birds=new List<OinkRompBird>();
    static List<OinkRompGate> gates=new List<OinkRompGate>();
    static List<Transform> springs=new List<Transform>();
    static Color Hex(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}
    static Material Mat(string name,string color)
    {
        string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",Hex(color));m.SetFloat("_Smoothness",.12f);return m;
    }
    static Transform Group(string name,Transform parent=null){var t=new GameObject(name).transform;t.SetParent(parent?parent:content);return t;}
    static GameObject Shape(string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat,Transform parent=null,bool collide=true)
    {var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(parent?parent:content);g.transform.position=pos;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=mat;if(!collide)Object.DestroyImmediate(g.GetComponent<Collider>());else if(type==PrimitiveType.Cylinder){Object.DestroyImmediate(g.GetComponent<Collider>());g.AddComponent<MeshCollider>().sharedMesh=g.GetComponent<MeshFilter>().sharedMesh;}return g;}
    static GameObject Box(string name,Vector3 p,Vector3 s,Material m,Transform parent=null,bool collision=true)=>Shape(name,PrimitiveType.Cube,p,s,m,parent,collision);
    static LineRenderer Line(string name,Vector3[] points,float width,Material material,Transform parent,bool loop=false,bool world=true)
    {var l=new GameObject(name).AddComponent<LineRenderer>();l.transform.SetParent(parent,false);l.useWorldSpace=world;l.loop=loop;l.positionCount=points.Length;l.SetPositions(points);l.startWidth=l.endWidth=width;l.numCornerVertices=3;l.numCapVertices=3;l.sharedMaterial=material;return l;}
    static void Fence(Vector3 a,Vector3 b)
    {
        float length=Vector3.Distance(a,b);Vector3 direction=(b-a).normalized;
        var root=Group("Timber fence");
        for(int i=0;i<=Mathf.CeilToInt(length/1.4f);i++)
        {Vector3 p=Vector3.Lerp(a,b,i/(float)Mathf.CeilToInt(length/1.4f));Box("Picket",p+Vector3.up*1.2f,new Vector3(.18f,2.4f,.18f),wood,root);}
        foreach(float height in new[]{.6f,1.7f})
        {var rail=Box("Rail",(a+b)*.5f+Vector3.up*height,new Vector3(.16f,.18f,length),cream,root);rail.transform.rotation=Quaternion.LookRotation(direction);}
    }
    static void Ramp(string name,Vector3 a,Vector3 b,float width,Material mat)
    {var d=b-a;var go=Box(name,(a+b)*.5f-Vector3.up*.12f,new Vector3(width,.24f,d.magnitude+.3f),mat);go.transform.rotation=Quaternion.LookRotation(d);}
    static void Bridge(Vector3 a,Vector3 b,float width=3.4f)
    {
        var d=b-a;var right=Vector3.Cross(Vector3.up,d).normalized;
        var root=Group("Rope bridge");int n=Mathf.CeilToInt(d.magnitude/.45f);
        for(int i=0;i<=n;i++){var p=Vector3.Lerp(a,b,i/(float)n);var board=Box("Plank",p-Vector3.up*.1f,new Vector3(width,.2f,.42f),wood,root);board.transform.rotation=Quaternion.LookRotation(d);}
        foreach(int side in new[]{-1,1})
        {var points=new List<Vector3>();for(int i=0;i<=n;i++){var p=Vector3.Lerp(a,b,i/(float)n)+right*(side*width*.46f);points.Add(p+Vector3.up*(1-.18f*Mathf.Sin(i/(float)n*Mathf.PI)));if(i%5==0)Box("Bridge post",p+Vector3.up*.5f,new Vector3(.13f,1.1f,.13f),wood,root);}Line("Hand rope",points.ToArray(),.055f,rope,root);}
    }
    static void Tunnel(Vector3 p,float length=6)
    {
        var root=Group("Mossy crawl-through tunnel");
        for(int z=0;z<=Mathf.CeilToInt(length/1.2f);z++)
        for(int i=0;i<9;i++)
        {
            float angle=i/8f*Mathf.PI;Vector3 q=p+new Vector3(Mathf.Cos(angle)*2.25f,.65f+Mathf.Sin(angle)*2.25f,z*1.2f-length*.5f);
            var stone=Box("Arch stone",q,new Vector3(.9f,.65f,1.3f),i%3==0?grass:earth,root);
            stone.transform.rotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg+90);
        }
    }
    static void Tree(Vector3 p,float scale=1)
    {
        var t=Group("Apple tree");Shape("Trunk",PrimitiveType.Cylinder,p+Vector3.up*1.2f*scale,new Vector3(.32f,1.2f,.32f)*scale,wood,t);
        Shape("Crown",PrimitiveType.Sphere,p+Vector3.up*3*scale,new Vector3(3.4f,2.8f,3.1f)*scale,leaf,t,false);
        foreach(var d in new[]{new Vector3(-.9f,2.6f,-.5f),new Vector3(.7f,2.4f,-.8f),new Vector3(.4f,3.2f,.9f)})Shape("Tree apple",PrimitiveType.Sphere,p+d*scale,Vector3.one*.32f,apple,t,false);
    }
    static OinkRompPickup Pickup(Vector3 p,bool balloon=false,bool refill=false)
    {
        var root=Group(balloon?"Permanent balloon upgrade":"Crunchy apple");root.position=p;
        var visual=Group("Collectible",root);visual.localPosition=Vector3.zero;
        if(balloon)
        {
            for(int i=0;i<1;i++)
            {var q=p+new Vector3((i-1)*.3f,.35f+(i%2)*.23f,0);Shape("Balloon",PrimitiveType.Sphere,q,new Vector3(.75f,.95f,.75f),BalloonMat(0),visual,false);Line("String",new[]{p-Vector3.up*.35f,q-Vector3.up*.25f},.018f,rope,visual);}
        }
        else
        {Shape("Apple",PrimitiveType.Sphere,p,new Vector3(.48f,.44f,.45f),apple,visual,false);var stem=Box("Stem",p+Vector3.up*.26f,new Vector3(.055f,.18f,.055f),wood,visual,false);stem.transform.rotation=Quaternion.Euler(0,0,-18);Shape("Leaf",PrimitiveType.Sphere,p+new Vector3(.11f,.3f,0),new Vector3(.22f,.07f,.1f),leaf,visual,false);}
        var pick=root.gameObject.AddComponent<OinkRompPickup>();pick.kind=balloon?OinkRompPickup.Kind.Balloons:OinkRompPickup.Kind.Apple;pick.visual=visual;pick.amount=1;pick.refillSeconds=0;pickups.Add(pick);return pick;
    }
    static void Bird(string species,string name,Vector3 p,string line)
    {
        var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Farm+"Prefabs/"+species+".prefab"));go.name=name;go.transform.SetParent(content);go.transform.position=p;go.transform.localScale=Vector3.one*.9f;
        var b=go.AddComponent<OinkRompBird>();b.speaker=name;b.dialogue=line;b.animator=go.GetComponentInChildren<Animator>();if(b.animator)b.animator.applyRootMotion=false;ApplyAnimalMaterials(go);birds.Add(b);
    }
    static BoxCollider Volume(string name,Vector3 p,Vector3 size)
    {var t=Group(name);t.position=p;var box=t.gameObject.AddComponent<BoxCollider>();box.isTrigger=true;box.size=size;return box;}
    static OinkRompGate Gate(float z,OinkRompGate.Requirement requirement)
    {
        var t=Group("Gate · "+requirement);t.position=new Vector3(0,.5f,z);
        Fence(new Vector3(-7,.5f,z),new Vector3(-2,.5f,z));Fence(new Vector3(2,.5f,z),new Vector3(7,.5f,z));
        var door=Box("Retracting gate",new Vector3(0,1.8f,z),new Vector3(4,2.6f,.25f),coral,t);
        for(int i=-1;i<=1;i++)Box("Ivory gate stripe",new Vector3(i,1.8f,z-.14f),new Vector3(.11f,2.3f,.05f),cream,t,false);
        var button=Shape("Push with your snout",PrimitiveType.Cylinder,new Vector3(-2.7f,1.25f,z-.5f),new Vector3(.65f,.18f,.65f),gold,t);button.transform.rotation=Quaternion.Euler(90,0,0);
        Box("Button post",new Vector3(-2.7f,.98f,z),new Vector3(.7f,1,.6f),wood,t);
        var checkpoint=Group("Checkpoint",t);checkpoint.position=new Vector3(0,.6f,z+2);
        var g=t.gameObject.AddComponent<OinkRompGate>();g.requirement=requirement;g.door=door.transform;g.blocker=door.GetComponent<Collider>();g.button=button.transform;g.lamp=button.GetComponent<Renderer>();g.checkpoint=checkpoint;gates.Add(g);return g;
    }
    static void Spring(Vector3 p)
    {
        var t=Group("Spring · first hop");t.position=p;
        Shape("Spring base",PrimitiveType.Cylinder,p+Vector3.up*.09f,new Vector3(1.65f,.09f,1.65f),teal,t);
        var coil=new Vector3[100];for(int i=0;i<coil.Length;i++){float v=i/99f;float a=v*Mathf.PI*10;coil[i]=p+new Vector3(Mathf.Cos(a)*.5f,.17f+v*.45f,Mathf.Sin(a)*.5f);}Line("Coiled spring",coil,.07f,cream,t);
        Shape("Bouncy cap",PrimitiveType.Cylinder,p+Vector3.up*.66f,new Vector3(1.7f,.1f,1.7f),coral,t);
        var top=Group("Launch surface",t);top.position=p+Vector3.up*.77f;springs.Add(top);
    }
    static TMP_Text Text(Transform parent,string name,string text,int size,Vector2 min,Vector2 max,Vector2 offMin,Vector2 offMax)
    {
        var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var rect=(RectTransform)g.transform;rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offMin;rect.offsetMax=offMax;
        var t=g.AddComponent<TextMeshProUGUI>();t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");t.text=text;t.fontSize=size;t.color=Hex("FFF0D5");t.raycastTarget=false;return t;
    }
    static RectTransform Panel(Transform parent,string name,Vector2 min,Vector2 max,Vector2 lo,Vector2 hi,string color)
    {
        var g=new GameObject(name,typeof(RectTransform));g.transform.SetParent(parent,false);var r=(RectTransform)g.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=lo;r.offsetMax=hi;var im=g.AddComponent<UnityEngine.UI.Image>();im.color=Hex(color);im.raycastTarget=false;return r;
    }
    static Material BalloonMat(int i)
    {
        var m=Mat("Glass balloon "+i,new[]{"F8A2AD","F4D67A","8DDBDD","C2ACE8","B4DF98","FFBE85","95C6ED"}[i%7]);
        var c=m.GetColor("_BaseColor");c.a=.4f;m.SetColor("_BaseColor",c);
        m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);
        m.SetFloat("_Smoothness",.75f);m.SetFloat("_Cull",2);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;m.SetShaderPassEnabled("ShadowCaster",false);
        EditorUtility.SetDirty(m);return m;
    }
    static void ApplyAnimalMaterials(GameObject g)
    {
        foreach(var animator in g.GetComponentsInChildren<Animator>(true))OinkRompAnimalSetup.UseIdleDefault(animator);
        foreach(var r in g.GetComponentsInChildren<Renderer>(true))
        {var mats=r.sharedMaterials;for(int i=0;i<mats.Length;i++){var m=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+mats[i].name+" Oink.mat");if(m)mats[i]=m;}r.sharedMaterials=mats;}
    }
    static Vector3 Ground(float x,float z)
    {
        var hits=Physics.RaycastAll(new Vector3(x,90,z),Vector3.down,100,~0,QueryTriggerInteraction.Ignore);
        var hit=hits.Where(h=>h.collider.transform.root.name.StartsWith("01")||h.collider.transform.root.name.StartsWith("02")||h.collider.transform.root.name.StartsWith("03")||h.collider.transform.root.name.StartsWith("07")).OrderByDescending(h=>h.point.y).FirstOrDefault();
        return new Vector3(x,hit.collider?hit.point.y:.03f,z);
    }
    static void PushGate(Vector3 p,string name,int apples=0)
    {
        var root=Group(name);root.position=p;
        var door=Box("Timber gate",p+new Vector3(0,1.1f,0),new Vector3(3,2.2f,.2f),wood,root);
        for(int i=-1;i<=1;i++)Box("Ivory bar",p+new Vector3(i*.8f,1.1f,-.12f),new Vector3(.12f,1.9f,.06f),cream,root,false);
        foreach(int i in new[]{-1,1})Box("Stone gatepost",p+new Vector3(i*1.7f,1.25f,0),new Vector3(.55f,2.5f,.55f),earth,root);
        var button=Shape("Shove button",PrimitiveType.Cylinder,p+new Vector3(-1.7f,.75f,-.48f),new Vector3(.8f,.15f,.8f),gold,root);button.transform.rotation=Quaternion.Euler(90,0,0);
        var gate=root.gameObject.AddComponent<OinkRompGate>();gate.door=door.transform;gate.blocker=door.GetComponent<Collider>();gate.button=button.transform;gate.lamp=button.GetComponent<Renderer>();gate.requirement=apples>0?OinkRompGate.Requirement.Apples:OinkRompGate.Requirement.Bump;gate.applesRequired=apples;gates.Add(gate);
    }
    static void SmashDeck(Vector3 p)
    {
        var root=Group("Cracked lookout floor · height slam");root.position=p;
        var f=root.gameObject.AddComponent<OinkRompSmashFloor>();f.requiredHeight=2;
        var collision=root.gameObject.AddComponent<BoxCollider>();collision.center=new Vector3(0,3,0);collision.size=new Vector3(3.2f,.2f,3.2f);f.surface=collision;
        f.planks=new Transform[7];for(int i=0;i<7;i++)f.planks[i]=Box("Cracked plank",p+new Vector3((i-3)*.46f,3,0),new Vector3(.42f,.2f,3.2f),wood,root,false).transform;
        foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})Box("Deck post",p+new Vector3(x*1.8f,1.5f,z*1.8f),new Vector3(.2f,3,.2f),earth,root);
        // Pale zigzag makes the breakable floor readable from overhead.
        Line("Crack",new[]{p+new Vector3(-.8f,3.12f,-1.4f),p+new Vector3(.1f,3.12f,-.5f),p+new Vector3(-.3f,3.12f,.2f),p+new Vector3(.9f,3.12f,1.4f)},.045f,cream,root);
        Ramp("Lookout access",p+new Vector3(0,0,-7),p+new Vector3(0,3,-1.8f),2.8f,wood);
        Pickup(p+Vector3.up*.6f);Pickup(p+new Vector3(.8f,.6f,.6f));
    }
    static void Slide(Vector3 top,Vector3 bottom,string name)
    {
        var d=bottom-top;var g=Box(name,(top+bottom)*.5f-Vector3.up*.15f,new Vector3(3.8f,.3f,d.magnitude+.3f),teal);
        g.transform.rotation=Quaternion.LookRotation(d);var surface=g.AddComponent<PootleSlideSurface>();surface.friction=.08f;surface.gravity=42;surface.maximumSpeed=23;
        foreach(float side in new[]{-2.05f,2.05f})
        {var rail=Box("Cream slide rim",g.transform.TransformPoint(new Vector3(side/3.8f,.8f,0)),new Vector3(.2f,.5f,d.magnitude),cream);rail.transform.rotation=g.transform.rotation;}
        var flat=Box("Slide runout",bottom+Vector3.ProjectOnPlane(d,Vector3.up).normalized*2-Vector3.up*.15f,new Vector3(4,.3f,4.5f),teal);flat.AddComponent<PootleSlideSurface>().runoutDeceleration=7;
        foreach(float t in new[]{.05f,.5f,.95f})foreach(float x in new[]{-2.1f,2.1f})
        {var p=Vector3.Lerp(top,bottom,t)+Vector3.right*x;float ground=Ground(p.x,p.z).y;float h=p.y-ground;if(h>.2f)Box("Slide timber support",new Vector3(p.x,ground+h*.5f,p.z),new Vector3(.18f,h,.18f),wood);}
        Box("Slide launch deck",top-Vector3.ProjectOnPlane(d,Vector3.up).normalized*1.2f-Vector3.up*.15f,new Vector3(3.8f,.3f,2.5f),wood);
    }
    static void CopyPlayground()
    {
        var target=EditorSceneManager.GetActiveScene();
        var source=EditorSceneManager.OpenScene("Assets/Pootle/Samples/PootlePlayground.unity",OpenSceneMode.Additive);
        try
        {
            var ts=source.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var original=ts.First(t=>t.name=="Helix spline rail · ground pound to slide");
            var rail=Object.Instantiate(original.gameObject);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(rail,target);rail.transform.SetParent(content,true);rail.name="Pootle spiral rail · coastal meadow";rail.transform.position=new Vector3(-20,3.05f,19);
            var mesh=rail.GetComponent<MeshFilter>();if(mesh && mesh.sharedMesh && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mesh.sharedMesh)))
            {var copy=Object.Instantiate(mesh.sharedMesh);var old=AssetDatabase.LoadAssetAtPath<Mesh>(Root+"/Helix mesh.asset");if(old){EditorUtility.CopySerialized(copy,old);Object.DestroyImmediate(copy);copy=old;}else AssetDatabase.CreateAsset(copy,Root+"/Helix mesh.asset");mesh.sharedMesh=copy;}
            var root=Object.Instantiate(source.GetRootGameObjects().First(r=>r.name=="Moving platforms and dangle hooks"));UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,target);root.transform.SetParent(content,true);root.name="Pootle hanging handles · coastal crossing";
            foreach(Transform child in root.transform.Cast<Transform>().ToArray())if(!child.name.StartsWith("Dangle shuttle")&&!child.name.StartsWith("Obstruction hook"))Object.DestroyImmediate(child.gameObject);
            foreach(var hook in root.GetComponentsInChildren<PootleDangleHook>())
            {
                var mover=hook.GetComponent<PootleSplineMover>();mover.path=root.GetComponentsInChildren<UnityEngine.Splines.SplineContainer>().First(t=>t.name==hook.name+" · spline");
                hook.hangDistance=1.4f;hook.bodyDistance=.25f;hook.grabRadius=.8f;
            }
            root.transform.position=new Vector3(-13,3,28);
            // World-space line renderers need the same relocation as the copied objects.
            foreach(var line in root.GetComponentsInChildren<LineRenderer>())if(line.useWorldSpace){var ps=new Vector3[line.positionCount];line.GetPositions(ps);for(int i=0;i<ps.Length;i++)ps[i]+=root.transform.position;line.SetPositions(ps);}
        }
        finally{EditorSceneManager.CloseScene(source,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(target);}
    }
    public static object Build()
    {
        if(Application.isPlaying)throw new Exception("Stop Play mode before rebuilding.");
        if(EditorSceneManager.GetActiveScene().isDirty)throw new Exception("Save current scene first.");
        Directory.CreateDirectory("Builds/oinkromp/archive");
        if(File.Exists(Root+"/OinkRomp.unity"))if(!File.Exists("Builds/oinkromp/archive/OinkRomp-before-coastal-rebuild.unity"))File.Copy(Root+"/OinkRomp.unity","Builds/oinkromp/archive/OinkRomp-before-coastal-rebuild.unity",false);
        var scene=EditorSceneManager.OpenScene("Assets/CoastalHike/CoastalHike.unity");
        EditorSceneManager.SaveScene(scene,Root+"/OinkRomp.unity");
        content=new GameObject("10 · OinkRomp coastal discoveries").transform;
        pickups.Clear();birds.Clear();gates.Clear();springs.Clear();
        grass=Mat("Moss","88A65A");earth=Mat("Warm stone","A68C6A");wood=Mat("Driftwood","685747");cream=Mat("Warm ivory","F0DEC1");coral=Mat("Coral","E58373");mud=Mat("Chocolate mud","604437");gold=Mat("Butter yellow","F5CD63");teal=Mat("Lagoon teal","4A9D9F");rope=Mat("Hemp rope","C5A775");leaf=Mat("Apple leaf","436F5D");apple=Mat("Apple red","D65345");
        var c=Object.FindAnyObjectByType<PootleCharacter>();var player=c.gameObject;
        if(PrefabUtility.IsPartOfPrefabInstance(player))PrefabUtility.UnpackPrefabInstance(player,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        player.name="Oink · Quirky Pig";
        foreach(var mb in player.GetComponents<MonoBehaviour>())if(mb && new[]{"PootleAvatar","PootleSeatedPose","PootleGripPose","PootleGliderVisual","PootleBallVisual","PootleCoastalRecovery"}.Contains(mb.GetType().Name))Object.DestroyImmediate(mb);
        var old=c.visualRoot;if(c.cameraTarget && c.cameraTarget.IsChildOf(old))c.cameraTarget.SetParent(player.transform,true);Object.DestroyImmediate(old.gameObject);
        var visual=Group("Pig presentation",player.transform);visual.localPosition=Vector3.zero;visual.localRotation=Quaternion.identity;c.visualRoot=visual;
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Farm+"Prefabs/Pig.prefab"));model.transform.SetParent(visual,false);model.transform.localScale=Vector3.one*1.5f;ApplyAnimalMaterials(model);
        var animator=model.GetComponentInChildren<Animator>();animator.applyRootMotion=false;
        var motor=c.GetComponent<PootleMotor>();motor.radius=.48f;motor.height=1.25f;motor.center=Vector3.up*.625f;motor.Configure();
        var profile=AssetDatabase.LoadAssetAtPath<PootleProfile>(Root+"/OinkMovement.asset");if(!profile){profile=Object.Instantiate(c.profile);AssetDatabase.CreateAsset(profile,Root+"/OinkMovement.asset");}else EditorUtility.CopySerialized(c.profile,profile);
        profile.name="OinkMovement";profile.facingSpeed=360;profile.minimumAnalogueSpeed=.6f;profile.allowGliding=false;profile.additionalAirJumps=0;c.profile=profile;EditorUtility.SetDirty(profile);
        c.input.digitalMovement.enabled=true;c.input.digitalMovement.accelerationTime=.26f;c.input.digitalMovement.releaseTime=.14f;c.input.digitalMovement.turnTime=.48f;c.input.digitalMovement.analogDirectionSteps=0;
        c.simulateAutomatically=false;
        var pig=player.AddComponent<OinkRompController>();pig.animator=animator;pig.body=model.transform;pig.startingBalloons=0;pig.maximumSupply=7;c.movementPolicy=pig;
        var shadow=player.GetComponent<PootleBlobShadow>();shadow.customBones=new[]{"body","head","leg.B.L","leg.B.R","leg.F.L","leg.F.R","tail.01"}.Select(n=>model.GetComponentsInChildren<Transform>().First(t=>t.name==n)).ToArray();shadow.customRadii=new[]{.5f,.4f,.19f,.19f,.19f,.19f,.14f};shadow.maximumDrop=60;shadow.projectionWidth=3.5f;
        var belt=new Vector3[48];for(int i=0;i<48;i++){float a=i/48f*Mathf.PI*2;belt[i]=new Vector3(Mathf.Cos(a)*.475f,.67f+Mathf.Sin(a)*.425f,-.03f);}Line("Rope around the belly",belt,.035f,rope,visual,true,false);
        var tie=Group("Balloon knot",visual);tie.localPosition=new Vector3(0,1.1f,0);
        var bv=player.AddComponent<OinkRompBalloons>();pig.balloons=bv;bv.pig=pig;bv.anchor=tie;bv.envelopes=new Transform[7];bv.strings=new LineRenderer[7];
        for(int i=0;i<7;i++){bv.envelopes[i]=Shape("Reusable balloon "+(i+1),PrimitiveType.Sphere,player.transform.position+Vector3.up*3,Vector3.one,BalloonMat(i),player.transform,false).transform;bv.envelopes[i].gameObject.SetActive(false);bv.strings[i]=Line("Balloon tether",new[]{Vector3.zero,Vector3.up},.018f,rope,player.transform);bv.strings[i].enabled=false;}
        pig.hopSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Pootle/Samples/Audio/jump_0.wav");pig.inflateSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Pootle/Samples/Audio/open_glider.wav");pig.popSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Pootle/Samples/Audio/close_glider.wav");pig.bumpSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Pootle/Samples/Audio/stomp.wav");pig.eatSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Pootle/Samples/Audio/footstep_regular_0.wav");
        var course=content.gameObject.AddComponent<OinkRompCourse>();pig.course=course;
        Physics.SyncTransforms();
        Pickup(Ground(-6.6f,13)+Vector3.up*.75f,true);
        Pickup(Ground(-17,-3)+Vector3.up*.75f,true);
        Pickup(Ground(5,30)+Vector3.up*.75f,true);
        Pickup(Ground(-4,47)+Vector3.up*.75f,true);
        Pickup(new Vector3(22,103.8f,38),true);
        Pickup(new Vector3(-16,10.95f,19),true);
        Pickup(Ground(2,62)+Vector3.up*.75f,true);
        foreach(var p in new[]{new Vector2(-5,11),new Vector2(-8,12),new Vector2(-10,14),new Vector2(-15,1),new Vector2(-18,14),new Vector2(-7,21),new Vector2(3,27),new Vector2(7,35),new Vector2(-2,45),new Vector2(3,59)})Pickup(Ground(p.x,p.y)+Vector3.up*.65f);
        Pickup(new Vector3(9,103.7f,25));Pickup(new Vector3(12,103.7f,34));
        Bird("Chick","Pip",Ground(-4.2f,11),"Tap to hop. Find balloons for extra jumps — they recharge when you land. Hold the button to pop and slam!");birds.Last().transform.localScale=Vector3.one*2.2f;
        Bird("Duck","Dottie",Ground(-17,16),"Seven balloons are hidden along the coast. Try the spiral rail and catch a hanging handle. Tap to jump off!");birds.Last().transform.localScale=Vector3.one*1.4f;
        Bird("Hen","Hazel",Ground(4,27),"Crunch the apples! Shove the golden buttons with your snout. A high slam smashes cracked wooden floors.");birds.Last().transform.localScale=Vector3.one*1.4f;
        PushGate(Ground(-9.3f,16),"Trailhead shove gate");PushGate(Ground(3,29),"Apple grove shove gate",3);
        SmashDeck(Ground(-12,2));
        CopyPlayground();
        Slide(new Vector3(9,12.5f,32),new Vector3(9,7.2f,22),"Slippery lagoon ramp");
        Slide(new Vector3(-8,15.7f,47),new Vector3(-8,11.2f,37),"Slippery high meadow ramp");
        Spring(Ground(-14,14));Spring(Ground(9,34));
        // Native spring contact uses the same first-hop launch and leaves every balloon available.
        foreach(var spring in springs){var cap=spring.parent.Find("Bouncy cap");var native=cap.gameObject.AddComponent<PootleSpring>();native.launchSpeed=25;Ramp("Spring walk-on apron",spring.position+new Vector3(0,-.77f,-2.2f),spring.position+new Vector3(0,0,-.55f),1.5f,teal);}
        var mp=Ground(13,14);Box("Mud pool",mp+Vector3.up*.7f,new Vector3(6,.05f,5),mud,null,false);
        Ramp("Muddy bank",mp+new Vector3(0,0,2),mp+new Vector3(0,.9f,4),6,mud);
        Ramp("Muddy bank outer slope",mp+new Vector3(0,.9f,4),mp+new Vector3(0,0,6),6,mud);
        course.mudVolume=Volume("Submerging mud",mp+Vector3.up*.4f,new Vector3(6,1.1f,5));course.bankVolume=Volume("Scramble muddy bank",mp+new Vector3(0,.5f,3.5f),new Vector3(6,2,3));
        course.pickups=pickups.ToArray();course.birds=birds.ToArray();course.gates=gates.ToArray();course.springs=new Transform[0];course.smashFloors=content.GetComponentsInChildren<OinkRompSmashFloor>();
        var impact=new GameObject("Slam ripple").AddComponent<OinkRompImpact>();impact.transform.SetParent(content);impact.ring=Line("Impact ring",new Vector3[48],.1f,cream,impact.transform,true);impact.ring.enabled=false;course.impact=impact;
        // Preserve every Coastal zone and its yaw handover, with a steeper view of the ground.
        var policies=new Dictionary<PootleCameraPolicy,PootleCameraPolicy>();
        foreach(var shot in Object.FindObjectsByType<PootleExplorationShot>())
        {
            shot.pitchYaw.x=Mathf.Max(shot.name.Contains("Cave")?68:65,shot.pitchYaw.x);
            if(shot.mode==ExplorationCameraMode.StaticOrBounded){var delta=shot.anchor-shot.focusOrigin;shot.anchor=shot.focusOrigin+Quaternion.Euler(shot.pitchYaw.x,shot.pitchYaw.y,0)*Vector3.back*delta.magnitude;}
            if(shot.mode==ExplorationCameraMode.RailFollow)shot.railOffset=new Vector3(shot.railOffset.x,Mathf.Abs(shot.railOffset.z)*2.15f,shot.railOffset.z);
            if(shot.policy && !policies.ContainsKey(shot.policy)){var copy=Object.Instantiate(shot.policy);var path=Root+"/CoastalCameraPolicy.asset";var existing=AssetDatabase.LoadAssetAtPath<PootleCameraPolicy>(path);if(existing){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);copy=existing;}else AssetDatabase.CreateAsset(copy,path);policies.Add(shot.policy,copy);}
            if(shot.policy)shot.policy=policies[shot.policy];shot.ApplySettings();
        }
        foreach(var overlay in Object.FindObjectsByType<PootleCameraDebugOverlay>())overlay.enabled=false;
        var main=Camera.main;
        var canvasGo=new GameObject("OinkRomp HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));var canvas=canvasGo.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=main;canvas.planeDistance=1;
        var scaler=canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
        var hud=canvasGo.AddComponent<OinkRompHud>();hud.pig=pig;hud.status=Text(canvasGo.transform,"Quiet counters","",21,new Vector2(1,1),Vector2.one,new Vector2(-340,-52),new Vector2(-24,-16));hud.status.alignment=TextAlignmentOptions.Right;
        var dialog=Panel(canvasGo.transform,"Nearby bird speech",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(-400,30),new Vector2(400,130),"304E49");hud.dialoguePanel=dialog.gameObject;hud.dialogue=Text(dialog,"Dialogue","",22,Vector2.zero,Vector2.one,new Vector2(20,10),new Vector2(-20,-10));dialog.gameObject.SetActive(false);
        // Convert pickup strings to local coordinates so their bob and turn carry the entire collectible.
        foreach(var pick in pickups)foreach(var line in pick.GetComponentsInChildren<LineRenderer>()){var ps=new Vector3[line.positionCount];line.GetPositions(ps);for(int i=0;i<ps.Length;i++)ps[i]=line.transform.InverseTransformPoint(ps[i]);line.useWorldSpace=false;line.SetPositions(ps);}
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        return new{scene=scene.path,balloonUpgrades=pickups.Count(x=>x.kind==OinkRompPickup.Kind.Balloons),apples=pickups.Count(x=>x.kind==OinkRompPickup.Kind.Apple),cameraZones=Object.FindObjectsByType<PootleCameraZone>().Length,spiralRails=content.GetComponentsInChildren<PootleRail>().Length,handles=content.GetComponentsInChildren<PootleDangleHook>().Length};
    }
}
