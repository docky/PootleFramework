using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using TMPro;

namespace Pootle.FairyDemo.Editor
{
    public static partial class FairyGroveBuilder
    {
        public const string Root="Assets/Pootle/Samples/FairyOneButton";
        public const string ScenePath=Root+"/Pootle_FairyOneButton.unity";
        static readonly Color Green=Hex("8DDC62"), Dark=Hex("286957"), Cream=Hex("FFF3CD"), Pink=Hex("FF8EBD"), Gold=Hex("FFD45F"), Mint=Hex("BAF6E7"), Purple=Hex("B5A1F0"), Soil=Hex("716394");
        static readonly Color[] Rainbow={Hex("FF7598"),Hex("FFAE65"),Gold,Green,Hex("75DCD5"),Purple};
        static Material material;
        static readonly Dictionary<string,Mesh> meshes=new();
        static readonly List<GameObject> hiddenRoots=new();
        static Scene previousScene;
        static Transform scenery;
        static int meshIndex;
        static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var color);return color;}
        [MenuItem("Pootle/Samples/Build Rainbow Fairy Grove")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Exit Play Mode before building.");
            if(SceneManager.GetSceneByPath(ScenePath).isLoaded)throw new System.InvalidOperationException("Close the existing fairy scene before rebuilding it.");
            Directory.CreateDirectory(Root+"/Generated");
            previousScene=SceneManager.GetActiveScene();
            // Keep the user's unsaved scenes loaded and preserve their active roots during our preview.
            hiddenRoots.Clear();
            for(int i=0;i<SceneManager.sceneCount;i++)foreach(var root in SceneManager.GetSceneAt(i).GetRootGameObjects())if(root.activeSelf){hiddenRoots.Add(root);root.SetActive(false);}
            SessionState.SetString("FairyPreviewRoots",string.Join("|",hiddenRoots.ConvertAll(x=>GlobalObjectId.GetGlobalObjectIdSlow(x).ToString())));
            SessionState.SetString("FairyPreviousScene",previousScene.path);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/Grove Vertex Colours.mat");
            if(!material){material=new Material(Shader.Find("Pootle/Vertex Color Lit"));AssetDatabase.CreateAsset(material,Root+"/Generated/Grove Vertex Colours.mat");}
            material.SetColor("_BaseColor",Color.white);meshes.Clear();meshIndex=0;
            scenery=new GameObject("Rainbow Fairy Grove · procedural garden").transform;
            RenderSettings.skybox=null;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=Hex("D3DAF1")*.8f;RenderSettings.ambientEquatorColor=Hex("A5C7C3")*.65f;RenderSettings.ambientGroundColor=Hex("665378")*.5f;
            RenderSettings.fog=false;
            var sun=new GameObject("Warm afternoon sun").AddComponent<Light>();sun.type=LightType.Directional;sun.color=Hex("FFF0CE");sun.intensity=1.1f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-35,0);
            var camera=new GameObject("Grove · fixed three-quarter camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=8.2f;camera.nearClipPlane=.1f;camera.farClipPlane=120;camera.backgroundColor=Hex("A5D2DE");camera.clearFlags=CameraClearFlags.SolidColor;camera.transform.position=new Vector3(11,17,-20);camera.transform.LookAt(new Vector3(0,.4f,0));camera.gameObject.AddComponent<AudioListener>();
            Shape("Floating lavender earth",PrimitiveType.Sphere,new Vector3(0,-1.55f,0),new Vector3(16,2.5f,14),Soil);
            var floor=Shape("Meadow · flat walkable top",PrimitiveType.Cylinder,new Vector3(0,-.18f,0),new Vector3(15.7f,.18f,13.7f),Green,true);
            Shape("Moss rim",PrimitiveType.Cylinder,new Vector3(0,-.32f,0),new Vector3(16,.10f,14),Dark);
            // Clear cream path through each decision point.
            for(int i=0;i<20;i++)
            {
                float a=i/20f*Mathf.PI*2;var p=new Vector3(Mathf.Cos(a)*4.7f,.016f,Mathf.Sin(a)*3.8f);
                Shape("Petal path",PrimitiveType.Cylinder,p,new Vector3(.7f,.012f,.5f),Cream);
            }
            Ring("Launch landing ring",new Vector3(0,.025f,-3.5f),1.25f,.065f,Cream,scenery);
            Ring("Launch ring accent",new Vector3(0,.028f,-3.5f),1.36f,.025f,Gold,scenery);
            Platform("First flower · 0.8m",new Vector3(-.4f,0,.0f),.8f,1.10f,Pink);
            Platform("Sideways flower · 1.7m",new Vector3(2.5f,0,1.2f),1.7f,1.05f,Purple);
            Platform("Walk-off leaf",new Vector3(4.7f,0,4.8f),1.1f,1.25f,Mint);
            RainbowArch(new Vector3(.1f,0,4.7f),3.25f);
            // The smaller arch is deliberately low enough to catch a launch.
            var arch=new GameObject("Low mint arch · ceiling and wall test").transform;arch.SetParent(scenery);
            Shape("Left arch wall",PrimitiveType.Cube,new Vector3(3.5f,.7f,3.1f),new Vector3(.35f,1.4f,.65f),Mint,true,arch);
            Shape("Right arch wall",PrimitiveType.Cube,new Vector3(5.7f,.7f,3.1f),new Vector3(.35f,1.4f,.65f),Mint,true,arch);
            Shape("Low ceiling",PrimitiveType.Cube,new Vector3(4.6f,1.52f,3.1f),new Vector3(2.55f,.24f,.65f),Mint,true,arch);
            for(int i=0;i<5;i++)Shape("Arch blossom",PrimitiveType.Sphere,new Vector3(3.6f+i*.5f,1.73f,3.1f),Vector3.one*.3f,i%2==0?Pink:Gold,false,arch);
            var chat=Npc("Pip · chatty garden keeper",new Vector3(-4.65f,0,1.35f),FairyTargetKind.Chatter,Pink,0);
            var manual=Npc("Moss · two-page storyteller",new Vector3(4.7f,0,-2.6f),FairyTargetKind.Dialogue,Purple,1);
            var button=new GameObject("Sunflower mushroom plunger").AddComponent<FairyTarget>();button.kind=FairyTargetKind.Plunger;button.stableOrder=2;button.transform.position=new Vector3(-3.2f,0,1.35f);button.transform.SetParent(scenery);
            Shape("Ivory pedestal",PrimitiveType.Cylinder,new Vector3(0,.22f,0),new Vector3(.75f,.22f,.75f),Cream,true,button.transform);
            button.plunger=Shape("Coral plunger",PrimitiveType.Sphere,new Vector3(0,.54f,0),new Vector3(.94f,.3f,.94f),Pink,false,button.transform).transform;
            for(int i=0;i<5;i++){float a=i*1.2566f;Shape("Plunger spots",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.26f,.673f,Mathf.Sin(a)*.26f),new Vector3(.12f,.025f,.12f),Cream,false,button.transform);}
            button.focusPoint=Empty("Authored base focus",button.transform,new Vector3(0,0,-.46f));button.highlight=Ring("Press focus",new Vector3(0,.03f,-.15f),.65f,.045f,Gold,button.transform);button.highlight.gameObject.SetActive(false);
            for(int i=0;i<2;i++)Shape("Gate guide post",PrimitiveType.Cylinder,new Vector3(-5.55f+i*2.3f,1.4f,3.6f),new Vector3(.16f,1.4f,.16f),Cream,true);
            var gate=new GameObject("Rainbow gate").transform;gate.SetParent(scenery);gate.position=new Vector3(-4.4f,0,3.6f);
            for(int i=0;i<5;i++)Shape("Gate petal",PrimitiveType.Capsule,new Vector3(-.8f+i*.4f,.65f,0),new Vector3(.2f,.65f,.22f),Rainbow[i],true,gate);
            Shape("Gate crossbar",PrimitiveType.Cube,new Vector3(0,.75f,0),new Vector3(1.95f,.14f,.18f),Cream,true,gate);button.gate=gate;
            var lamp=Empty("Indicator stem",scenery,new Vector3(-2.45f,.9f,1.5f));Shape("Stem",PrimitiveType.Capsule,new Vector3(0,-.45f,0),new Vector3(.12f,.45f,.12f),Dark,false,lamp);button.indicator=Empty("Golden on indicator",lamp,Vector3.zero);Flower(button.indicator,Vector3.zero,.65f,Gold);
            // Perimeter planting leaves all central routes unobstructed.
            Random.InitState(811);
            for(int i=0;i<18;i++)
            {
                float a=i*Mathf.PI*2/18;var p=new Vector3(Mathf.Cos(a)*7,0,Mathf.Sin(a)*6);
                if(p.z< -4.5f&&Mathf.Abs(p.x)<4)continue;
                if(i%3==0)Tree(p,1.5f+Random.value*.8f);
                else Bush(p,.8f+Random.value*.4f);
            }
            for(int i=0;i<58;i++)
            {
                float a=Random.value*Mathf.PI*2;float r=Random.Range(.83f,.96f);var p=new Vector3(Mathf.Cos(a)*7.6f*r,0,Mathf.Sin(a)*6.6f*r);
                var flower=Empty("Wildflower",scenery,p);Flower(flower,Vector3.up*Random.Range(.14f,.4f),Random.Range(.18f,.35f),Rainbow[i%6]);
            }
            for(int i=0;i<8;i++){float a=i*.7854f;Shape("Floating root stone",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*6.8f,-1.6f,Mathf.Sin(a)*5.8f),new Vector3(.65f,1.1f,.65f),Soil);}
            var profile=LoadOrCreate<PootleProfile>("Fairy Movement.asset");profile.walkSpeed=3.2f;profile.allowRun=false;profile.minimumAnalogueSpeed=0;profile.airAcceleration=16;profile.airTurningDrag=16;profile.risingGravity=8;profile.fallingGravity=6.5f;profile.maximumFallSpeed=4.5f;profile.jumpLaunchSpeed=4.8f;profile.jumpReleaseSpeed=4.8f;profile.coyoteTime=0;profile.jumpBufferTime=0;profile.additionalAirJumps=0;profile.automaticGrip=false;profile.smallObstacleHop=false;profile.allowGliding=false;profile.enableSteepSliding=false;profile.applySlopeSpeed=false;profile.landingMomentumConversion=0;profile.groundSnapSpeed=8;EditorUtility.SetDirty(profile);
            var tuning=LoadOrCreate<FairyTuning>("Fairy Flight.asset");
            var input=CreateInput();
            var fairy=CreateFairy(profile,tuning,input);
            var prefabPath=Root+"/Rainbow Fairy.prefab";
            var prefab=PrefabUtility.SaveAsPrefabAsset(fairy.gameObject,prefabPath);Object.DestroyImmediate(fairy.gameObject);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);instance.transform.position=new Vector3(0,.02f,-3.5f);
            fairy=instance.GetComponent<FairyController>();var character=instance.GetComponent<PootleCharacter>();character.outputCamera=camera.transform;
            CreateHud(fairy,camera);
            AddCourse();
            PrefabUtility.RecordPrefabInstancePropertyModifications(character);PrefabUtility.RecordPrefabInstancePropertyModifications(fairy.presentation);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
            Selection.activeGameObject=instance;
            Debug.Log("Rainbow Fairy Grove saved: "+ScenePath);
        }
        [MenuItem("Pootle/Samples/Restore scene before Fairy preview")]
        public static void RestoreWorkspace()
        {
            foreach(var id in SessionState.GetString("FairyPreviewRoots", "").Split('|'))
                if(GlobalObjectId.TryParse(id,out var parsed)&&GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) is GameObject root)root.SetActive(true);
            hiddenRoots.Clear();SessionState.EraseString("FairyPreviewRoots");
            previousScene=SceneManager.GetSceneByPath(SessionState.GetString("FairyPreviousScene", ""));
            var demo=SceneManager.GetSceneByPath(ScenePath);if(demo.isLoaded)EditorSceneManager.CloseScene(demo,true);
            if(previousScene.IsValid()&&previousScene.isLoaded)SceneManager.SetActiveScene(previousScene);
        }
        static T LoadOrCreate<T>(string file) where T:ScriptableObject
        {var asset=AssetDatabase.LoadAssetAtPath<T>(Root+"/"+file);if(!asset){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,Root+"/"+file);}return asset;}
        static InputActionAsset CreateInput()
        {
            var asset=ScriptableObject.CreateInstance<InputActionAsset>();var map=asset.AddActionMap("Fairy");var move=map.AddAction("Move",InputActionType.Value,expectedControlLayout:"Vector2");
            move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Down","<Keyboard>/s").With("Left","<Keyboard>/a").With("Right","<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up","<Keyboard>/upArrow").With("Down","<Keyboard>/downArrow").With("Left","<Keyboard>/leftArrow").With("Right","<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/dpad");move.AddBinding("<Joystick>/stick");
            var action=map.AddAction("Action",InputActionType.Button);action.AddBinding("<Keyboard>/space");action.AddBinding("<Gamepad>/buttonSouth");action.AddBinding("<Joystick>/trigger");
            var back=map.AddAction("Back",InputActionType.Button);back.AddBinding("<Keyboard>/escape");back.AddBinding("<Gamepad>/buttonEast");back.AddBinding("<Keyboard>/b");
            var path=Root+"/FairyControls.inputactions";File.WriteAllText(path,asset.ToJson());Object.DestroyImmediate(asset);AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        }
        static FairyController CreateFairy(PootleProfile profile,FairyTuning tuning,InputActionAsset input)
        {
            var root=new GameObject("Rainbow Fairy");var motor=root.AddComponent<PootleMotor>();motor.height=.82f;motor.radius=.22f;motor.center=Vector3.up*.41f;motor.stepHeight=.18f;motor.Configure();
            var c=root.AddComponent<PootleCharacter>();c.profile=profile;c.simulateAutomatically=false;
            var controller=root.AddComponent<FairyController>();controller.tuning=tuning;controller.controls=input;c.movementPolicy=controller;
            var visual=Empty("Visual facing",root.transform,Vector3.zero);c.visualRoot=visual;
            var pose=Empty("Squash and bank",visual,Vector3.zero);
            var skin=Hex("FFD0A1");var hair=Hex("FFCC49");
            Shape("Petal tunic",PrimitiveType.Capsule,new Vector3(0,.31f,0),new Vector3(.23f,.18f,.19f),Green,false,pose);
            for(int i=0;i<5;i++){float a=i*1.2566f;var petal=Shape("Outfit petal",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.1f,.23f,Mathf.Sin(a)*.09f),new Vector3(.13f,.25f,.09f),i%2==0?Green:Dark,false,pose);petal.transform.localRotation=Quaternion.Euler(Mathf.Sin(a)*25,0,-Mathf.Cos(a)*25);}
            Shape("Apricot face",PrimitiveType.Sphere,new Vector3(0,.61f,.015f),new Vector3(.40f,.40f,.37f),skin,false,pose);
            Shape("Golden hair cap",PrimitiveType.Sphere,new Vector3(0,.71f,-.03f),new Vector3(.42f,.24f,.36f),hair,false,pose);
            var swoop=Shape("Side swept fringe",PrimitiveType.Sphere,new Vector3(-.065f,.735f,.13f),new Vector3(.30f,.10f,.12f),Gold,false,pose);swoop.transform.localRotation=Quaternion.Euler(0,0,-20);
            Shape("Topknot",PrimitiveType.Sphere,new Vector3(.025f,.835f,-.06f),new Vector3(.19f,.17f,.18f),hair,false,pose);
            Shape("Leaf hair ribbon",PrimitiveType.Sphere,new Vector3(.09f,.80f,-.01f),new Vector3(.18f,.045f,.07f),Mint,false,pose);
            for(int side=-1;side<=1;side+=2)
            {
                Shape("Dot eye",PrimitiveType.Sphere,new Vector3(side*.07f,.61f,.184f),new Vector3(.035f,.049f,.018f),Hex("393253"),false,pose);
                Shape("Eye glint",PrimitiveType.Sphere,new Vector3(side*.07f-.007f,.622f,.194f),Vector3.one*.012f,Cream,false,pose);
                Shape("Blush",PrimitiveType.Sphere,new Vector3(side*.115f,.56f,.165f),new Vector3(.062f,.025f,.015f),Pink,false,pose);
                var arm=Shape("Little arm",PrimitiveType.Capsule,new Vector3(side*.17f,.34f,0),new Vector3(.063f,.10f,.063f),skin,false,pose);arm.transform.localRotation=Quaternion.Euler(0,0,side*32);
                Shape("Mitten",PrimitiveType.Sphere,new Vector3(side*.215f,.27f,.015f),Vector3.one*.086f,skin,false,pose);
                Shape("Leg",PrimitiveType.Capsule,new Vector3(side*.065f,.13f,0),new Vector3(.07f,.08f,.07f),skin,false,pose);
                Shape("Rounded slipper",PrimitiveType.Sphere,new Vector3(side*.07f,.055f,.045f),new Vector3(.115f,.10f,.18f),Dark,false,pose);
                Shape("Slipper pompom",PrimitiveType.Sphere,new Vector3(side*.07f,.082f,.115f),Vector3.one*.064f,Cream,false,pose);
            }
            Shape("Button nose",PrimitiveType.Sphere,new Vector3(0,.568f,.2f),Vector3.one*.045f,skin,false,pose);
            var presentation=root.AddComponent<FairyPresentation>();presentation.fairy=controller;presentation.pose=pose;controller.presentation=presentation;
            for(int side=-1;side<=1;side+=2)
            {
                var pivot=Empty(side<0?"Left wing pivot":"Right wing pivot",pose,new Vector3(side*.06f,.39f,-.09f));
                var wing=Shape("Upper mint wing",PrimitiveType.Sphere,new Vector3(side*.20f,.16f,-.035f),new Vector3(.32f,.46f,.045f),Mint,false,pivot);wing.transform.localRotation=Quaternion.Euler(0,0,side*-32);
                var lower=Shape("Lower lilac wing",PrimitiveType.Sphere,new Vector3(side*.17f,-.025f,-.04f),new Vector3(.28f,.25f,.035f),Purple,false,pivot);lower.transform.localRotation=Quaternion.Euler(0,0,side*30);
                Shape("Wing gleam",PrimitiveType.Sphere,new Vector3(side*.23f,.21f,-.065f),new Vector3(.10f,.25f,.015f),Cream,false,pivot);
                if(side<0)presentation.leftWing=pivot;else presentation.rightWing=pivot;
            }
            presentation.shadow=Shape("Ground shadow",PrimitiveType.Cylinder,new Vector3(0,.014f,0),new Vector3(.48f,.003f,.48f),Dark,false,root.transform).transform;
            presentation.sparkles=new Transform[8];for(int i=0;i<8;i++){presentation.sparkles[i]=Shape("Lift sparkle",PrimitiveType.Sphere,Vector3.zero,Vector3.one*.05f,Gold,false,root.transform).transform;presentation.sparkles[i].gameObject.SetActive(false);}
            presentation.sound=root.AddComponent<AudioSource>();presentation.sound.playOnAwake=false;presentation.sound.spatialBlend=0;
            return controller;
        }
        static FairyTarget Npc(string name,Vector3 position,FairyTargetKind kind,Color cap,int order)
        {
            var root=new GameObject(name);root.transform.SetParent(scenery);root.transform.position=position;
            var target=root.AddComponent<FairyTarget>();target.kind=kind;target.stableOrder=order;
            Shape("Little stem body",PrimitiveType.Capsule,new Vector3(0,.34f,0),new Vector3(.50f,.34f,.45f),Cream,true,root.transform);
            Shape("Mushroom beret",PrimitiveType.Sphere,new Vector3(0,.80f,0),new Vector3(1.15f,.55f,.95f),cap,false,root.transform);
            for(int i=-1;i<=1;i+=2){Shape("Boot",PrimitiveType.Sphere,new Vector3(i*.19f,.10f,-.10f),new Vector3(.25f,.18f,.36f),Dark,false,root.transform);Shape("Eye",PrimitiveType.Sphere,new Vector3(i*.11f,.43f,-.226f),new Vector3(.047f,.07f,.025f),Dark,false,root.transform);}
            for(int i=0;i<5;i++){float a=i*1.256f;Shape("Cap freckles",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.3f,1.025f,Mathf.Sin(a)*.23f),new Vector3(.17f,.025f,.14f),Cream,false,root.transform);}
            target.focusPoint=Empty("Authored approach point",root.transform,new Vector3(0,0,-.4f));target.highlight=Ring("Talk focus",new Vector3(0,.027f,-.1f),.7f,.045f,Gold,root.transform);target.highlight.gameObject.SetActive(false);return target;
        }
        static void Platform(string name,Vector3 p,float height,float radius,Color colour)
        {
            var parent=Empty(name,scenery,p);Shape("Stem",PrimitiveType.Cylinder,new Vector3(0,height*.45f,0),new Vector3(.40f,height*.45f,.40f),Dark,false,parent);
            Shape("Flat flower landing",PrimitiveType.Cylinder,new Vector3(0,height-.12f,0),new Vector3(radius*1.65f,.12f,radius*1.65f),colour,true,parent);
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Shape("Round petal edge",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*radius*.68f,height-.1f,Mathf.Sin(a)*radius*.68f),new Vector3(radius*.75f,.20f,radius*.75f),colour,false,parent);}
            Shape("Pollen landing centre",PrimitiveType.Cylinder,new Vector3(0,height+.005f,0),new Vector3(radius,.005f,radius),Gold,false,parent);
            Ring("Landing inlay",new Vector3(0,height+.018f,0),radius*.37f,.025f,Cream,parent);
        }
        static void RainbowArch(Vector3 p,float radius)
        {
            var root=Empty("Six-colour rainbow landmark",scenery,p);
            for(int band=0;band<6;band++)for(int i=0;i<36;i++)
            {
                float a=(i+.5f)/36*Mathf.PI;float r=radius-band*.18f;
                var piece=Shape("Rainbow band",PrimitiveType.Cube,new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,0),new Vector3(.19f,r*Mathf.PI/36+.02f,.26f),Rainbow[band],false,root);piece.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg);
            }
            for(int side=-1;side<=1;side+=2)for(int i=0;i<5;i++)Shape("Cloud puff",PrimitiveType.Sphere,new Vector3(side*2.8f+(i-2)*.23f,.15f+Mathf.Sin(i)*.08f,-.05f),new Vector3(.7f,.5f,.6f),Cream,false,root);
        }
        static void Tree(Vector3 p,float height)
        {
            var root=Empty("Lollipop canopy",scenery,p);Shape("Trunk",PrimitiveType.Capsule,Vector3.up*height*.4f,new Vector3(.28f,height*.4f,.28f),Soil,false,root);
            for(int i=0;i<5;i++){float a=i*1.2566f;Shape("Canopy leaf",PrimitiveType.Sphere,new Vector3(Mathf.Cos(a)*.5f,height+Mathf.Sin(a)*.17f,Mathf.Sin(a)*.35f),new Vector3(1.15f,1.35f,1.05f),i%2==0?Dark:Green,false,root);}
            for(int i=0;i<3;i++)Shape("Golden garden fruit",PrimitiveType.Sphere,new Vector3((i-1)*.45f,height-.3f,-.5f),Vector3.one*.25f,Gold,false,root);
        }
        static void Bush(Vector3 p,float size){var root=Empty("Mint fern",scenery,p);for(int i=0;i<5;i++){var leaf=Shape("Fern leaf",PrimitiveType.Sphere,new Vector3((i-2)*.16f,.3f,0),new Vector3(.25f,size,.38f),i%2==0?Mint:Dark,false,root);leaf.transform.localRotation=Quaternion.Euler(15,(i-2)*25,(i-2)*-22);}}
        static void Flower(Transform parent,Vector3 p,float size,Color colour)
        {
            Shape("Flower heart",PrimitiveType.Sphere,p,new Vector3(size*.45f,.1f,size*.45f),Gold,false,parent);
            for(int i=0;i<5;i++){float a=i*1.2566f;Shape("Petal",PrimitiveType.Sphere,p+new Vector3(Mathf.Cos(a)*size*.35f,0,Mathf.Sin(a)*size*.35f),new Vector3(size*.48f,.08f,size*.48f),colour,false,parent);}
        }
        static Transform Empty(string name,Transform parent,Vector3 pos){var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}
        static GameObject Shape(string name,PrimitiveType type,Vector3 p,Vector3 scale,Color color,bool solid=false,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent?parent:scenery,false);go.transform.localPosition=p;go.transform.localScale=scale;
            var collider=go.GetComponent<Collider>();if(!solid)Object.DestroyImmediate(collider);
            var filter=go.GetComponent<MeshFilter>();string key=type+ColorUtility.ToHtmlStringRGB(color);
            if(!meshes.TryGetValue(key,out var mesh))
            {
                mesh=Object.Instantiate(filter.sharedMesh);mesh.name=key;var vertices=mesh.vertices;var colors=new Color[vertices.Length];
                for(int i=0;i<colors.Length;i++)colors[i]=Color.Lerp(color*.78f,color,Mathf.Clamp01(vertices[i].y+.6f));mesh.colors=colors;
                string path=Root+"/Generated/"+key+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
                meshes[key]=mesh;meshIndex++;
            }
            if(solid&&type==PrimitiveType.Cylinder){Object.DestroyImmediate(collider);go.AddComponent<MeshCollider>().sharedMesh=mesh;}
            filter.sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go;
        }
        static Transform Ring(string name,Vector3 p,float radius,float width,Color color,Transform parent)
        {
            var root=Empty(name,parent,p);
            for(int i=0;i<48;i++){float a=i*Mathf.PI*2/48;var piece=Shape("Ring inlay",PrimitiveType.Cube,new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius),new Vector3(width,.012f,radius*Mathf.PI*2/48+.01f),color,false,root);piece.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg+90,0);}return root;
        }
        static void CreateHud(FairyController fairy,Camera camera)
        {
            var go=new GameObject("Grove HUD",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>();scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,1000);scaler.matchWidthOrHeight=.5f;
            var title=Text("Grove title",go.transform,"RAINBOW FAIRY GROVE",26,TextAlignmentOptions.Left);Anchor(title.rectTransform,new Vector2(0,1),new Vector2(38,-38),new Vector2(600,45),new Vector2(0,1));title.fontStyle=FontStyles.Bold;title.characterSpacing=3;
            var controls=Text("Small controls card",go.transform,"WASD / arrows   |   Stop + tap twice to fly   |   Tap in air to flap   |   Tap a prompt to use",19,TextAlignmentOptions.Center);Anchor(controls.rectTransform,new Vector2(.5f,0),new Vector2(0,25),new Vector2(1250,44),new Vector2(.5f,0));
            var p=fairy.presentation;p.canvas=canvas;p.view=camera;
            p.promptPanel=Panel("Action pill",go.transform,new Vector2(185,36));p.prompt=Text("Action",p.promptPanel,"Tap twice to fly",18,TextAlignmentOptions.Center);Stretch(p.prompt.rectTransform);
            p.speechPanel=Panel("Speech bubble",go.transform,new Vector2(360,70));p.speech=Text("Speech",p.speechPanel,"",21,TextAlignmentOptions.Center);Stretch(p.speech.rectTransform);p.speech.margin=new Vector4(14,5,14,5);p.speechPanel.gameObject.SetActive(false);
            p.pipPanel=new GameObject("Two flap charges",typeof(RectTransform)).GetComponent<RectTransform>();p.pipPanel.SetParent(go.transform,false);p.pipPanel.sizeDelta=new Vector2(60,22);
            p.pips=new UnityEngine.UI.Image[2];for(int i=0;i<2;i++){var pip=new GameObject("Flap pip "+(i+1),typeof(RectTransform),typeof(UnityEngine.UI.Image));pip.transform.SetParent(p.pipPanel,false);p.pips[i]=pip.GetComponent<UnityEngine.UI.Image>();p.pips[i].raycastTarget=false;Anchor(p.pips[i].rectTransform,new Vector2(.5f,.5f),new Vector2((i-.5f)*22,0),new Vector2(11,11),new Vector2(.5f,.5f));pip.transform.localRotation=Quaternion.Euler(0,0,45);}
        }
        static TMP_Text Text(string name,Transform parent,string text,int size,TextAlignmentOptions alignment)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var t=go.AddComponent<TextMeshProUGUI>();t.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");t.text=text;t.fontSize=size;t.color=Cream;t.alignment=alignment;t.raycastTarget=false;return t;
        }
        static RectTransform Panel(string name,Transform parent,Vector2 size){var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image));go.transform.SetParent(parent,false);go.GetComponent<UnityEngine.UI.Image>().color=new Color(.13f,.24f,.25f,.92f);go.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;var r=go.GetComponent<RectTransform>();r.sizeDelta=size;return r;}
        static void Anchor(RectTransform r,Vector2 anchor,Vector2 pos,Vector2 size,Vector2 pivot){r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=pos;r.sizeDelta=size;}
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    }
}
