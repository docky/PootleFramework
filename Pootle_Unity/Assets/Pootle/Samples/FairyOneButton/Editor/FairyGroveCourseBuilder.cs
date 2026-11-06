using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;
using TMPro;
namespace Pootle.FairyDemo.Editor
{
    public static partial class FairyGroveBuilder
    {
        [MenuItem("Pootle/Samples/Add Fairy Branching Course")]
        public static void ExtendSavedScene()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play Mode first.");
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(!scene.isLoaded)
            {
                previousScene=SceneManager.GetActiveScene();hiddenRoots.Clear();
                for(int i=0;i<SceneManager.sceneCount;i++)foreach(var root in SceneManager.GetSceneAt(i).GetRootGameObjects())if(root.activeSelf){hiddenRoots.Add(root);root.SetActive(false);}
                SessionState.SetString("FairyPreviewRoots",string.Join("|",hiddenRoots.ConvertAll(x=>GlobalObjectId.GetGlobalObjectIdSlow(x).ToString())));
                SessionState.SetString("FairyPreviousScene",previousScene.path);
                scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
            }
            SceneManager.SetActiveScene(scene);AddCourse();AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
        }
        static void AddCourse()
        {
            if(Object.FindAnyObjectByType<FairyCourse>())return;
            var fairy=Object.FindAnyObjectByType<FairyController>();
            var landscape=GameObject.Find("Rainbow Fairy Grove · procedural garden").transform;
            material=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Generated/Grove Vertex Colours.mat");meshes.Clear();
            scenery=Empty("Branching seed trail",landscape,Vector3.zero);
            var course=scenery.gameObject.AddComponent<FairyCourse>();course.fairy=fairy;
            // Broad mint steps and a taller pink branch share the golden destination.
            Platform("Mint fork · 0.8m",new Vector3(-1.8f,0,.45f),.8f,1.0f,Mint);
            Platform("Mint middle · 1.6m",new Vector3(-2.3f,0,2.4f),1.6f,.95f,Mint);
            Platform("Mint upper · 2.35m",new Vector3(-1.9f,0,3.9f),2.35f,.85f,Mint);
            Platform("Pink upper · 2.35m",new Vector3(2.0f,0,3.4f),2.35f,.8f,Pink);
            Platform("Golden finish · 2.9m",new Vector3(0,0,5.1f),2.9f,1.15f,Gold);
            var a=new Vector3(-.4f,.3f,0);
            Vine(a,new Vector3(-1.8f,.5f,.45f));Vine(new Vector3(-1.8f,.5f,.45f),new Vector3(-2.3f,1.2f,2.4f));Vine(new Vector3(-2.3f,1.2f,2.4f),new Vector3(-1.9f,1.95f,3.9f));Vine(new Vector3(-1.9f,1.95f,3.9f),new Vector3(0,2.5f,5.1f));
            Vine(a,new Vector3(2.5f,1.3f,1.2f));Vine(new Vector3(2.5f,1.3f,1.2f),new Vector3(2,1.95f,3.4f));Vine(new Vector3(2,1.95f,3.4f),new Vector3(0,2.5f,5.1f));
            Platform("Recovery mint · 0.3m",new Vector3(-1.5f,0,1.3f),.3f,.65f,Mint);
            Platform("Recovery mint · 0.55m",new Vector3(-3.5f,0,2.5f),.55f,.8f,Mint);
            Platform("Recovery mint · 0.65m",new Vector3(.1f,0,2.6f),.65f,.8f,Mint);
            Platform("Recovery mint · 0.6m",new Vector3(3.4f,0,1.65f),.6f,.65f,Mint);
            Platform("Recovery mint · 1.0m",new Vector3(1.0f,0,3.6f),1.0f,.65f,Mint);
            // Side shelves sit below the island edge so an overshoot can be flown back from.
            Platform("West catch shelf",new Vector3(-8.0f,-1.1f,-.5f),.4f,1.25f,Mint);
            Platform("East catch shelf",new Vector3(8.0f,-1.1f,-.5f),.4f,1.25f,Mint);
            var rainbow=landscape.Find("Six-colour rainbow landmark");if(rainbow)rainbow.localPosition=new Vector3(.1f,1.15f,5.6f);
            var pickups=new List<FairyCollectible>();
            Vector3[] seedPositions={
                new Vector3(-.4f,.55f,-1.55f),new Vector3(-.4f,1.25f,0),
                new Vector3(-1.35f,1.65f,.05f),new Vector3(-1.8f,1.25f,.45f),new Vector3(-2.3f,2.1f,1.35f),new Vector3(-2.3f,2.05f,2.4f),new Vector3(-2.1f,3.1f,3.15f),new Vector3(-1.9f,2.8f,3.9f),new Vector3(-.9f,3.6f,4.5f),
                new Vector3(1.1f,2.05f,.5f),new Vector3(2.5f,2.15f,1.2f),new Vector3(2.2f,3.25f,2.3f),new Vector3(2,2.8f,3.4f),new Vector3(1,3.7f,4.15f),
                new Vector3(0,4,4.35f),new Vector3(0,3.35f,5.1f),
                new Vector3(-8,.0f,-.5f),new Vector3(8,.0f,-.5f)};
            for(int i=0;i<seedPositions.Length;i++)
            {
                var root=Empty("Dew seed "+(i+1).ToString("00"),scenery,seedPositions[i]);var seed=root.gameObject.AddComponent<FairyCollectible>();
                seed.visual=Empty("Floating seed",root,Vector3.zero);
                var drop=Shape("Golden dewdrop",PrimitiveType.Sphere,Vector3.zero,new Vector3(.21f,.29f,.21f),Gold,false,seed.visual);drop.transform.localRotation=Quaternion.Euler(0,0,20);
                Shape("Seed glint",PrimitiveType.Sphere,new Vector3(-.055f,.065f,-.07f),Vector3.one*.065f,Cream,false,seed.visual);
                var leaf=Shape("Seed leaf",PrimitiveType.Sphere,new Vector3(.09f,.10f,0),new Vector3(.16f,.05f,.08f),Mint,false,seed.visual);leaf.transform.localRotation=Quaternion.Euler(0,0,35);
                pickups.Add(seed);
            }
            course.collectibles=pickups.ToArray();
            course.hoops=new[]{
                Hoop("Mint hoop I",new Vector3(-2.3f,2.1f,1.35f),Vector3.forward,Mint),
                Hoop("Mint hoop II",new Vector3(-2.1f,3.1f,3.15f),new Vector3(.25f,0,1),Mint),
                Hoop("Pink hoop I",new Vector3(1.1f,2.05f,.5f),new Vector3(1,0,-.35f),Pink),
                Hoop("Pink hoop II",new Vector3(2.2f,3.25f,2.3f),Vector3.forward,Pink),
                Hoop("Golden finish hoop",new Vector3(0,4,4.35f),Vector3.forward,Gold)};
            course.signs=new[]{
                Sign("Flight sign",new Vector3(1.6f,0,-4.6f),"Stop. Tap twice to fly. Tap in air to flap."),
                Sign("Fork sign",new Vector3(-.9f,0,-1.5f),"Mint: gentle steps. Pink: higher hops."),
                Sign("Hoop sign",new Vector3(-3.7f,0,2.0f),"Fly through hoops. Gather golden seeds."),
                Sign("Recovery sign",new Vector3(6.8f,0,-.5f),"Missed? Rest on a lower leaf and try again."),
                Sign("Reset sign",new Vector3(-2.65f,0,-4.9f),"Reset returns home and restores the trail.")};
            var finish=Empty("Completed grove blossom",scenery,new Vector3(0,3.05f,5.1f));Flower(finish,Vector3.zero,1.05f,Pink);course.finishBlossom=finish.gameObject;finish.gameObject.SetActive(false);
            var reset=Empty("Reset acorn",scenery,new Vector3(-1.9f,0,-4.7f));var target=reset.gameObject.AddComponent<FairyTarget>();target.kind=FairyTargetKind.Reset;target.course=course;target.stableOrder=10;
            Shape("Reset pedestal",PrimitiveType.Cylinder,new Vector3(0,.18f,0),new Vector3(.65f,.18f,.65f),Cream,true,reset);
            target.plunger=Shape("Reset mint cap",PrimitiveType.Sphere,new Vector3(0,.45f,0),new Vector3(.8f,.25f,.8f),Mint,false,reset).transform;
            target.focusPoint=Empty("Reset focus",reset,new Vector3(0,0,-.35f));target.highlight=Ring("Reset focus ring",new Vector3(0,.025f,0),.55f,.035f,Gold,reset);target.highlight.gameObject.SetActive(false);
            // A simple geometric circular arrow on the cap avoids extra sign text.
            var arrow=Ring("Reset arrow",new Vector3(0,.58f,0),.21f,.035f,Dark,reset);for(int i=0;i<10;i++)arrow.GetChild(i).gameObject.SetActive(false);
            Shape("Arrow tip",PrimitiveType.Cube,new Vector3(.2f,.59f,.07f),new Vector3(.095f,.02f,.095f),Dark,false,reset).transform.localRotation=Quaternion.Euler(0,45,0);
            CourseHud(course,fairy.presentation.canvas);
            fairy.presentation.view.orthographicSize=9.0f;
            scenery=landscape;
        }
        static void Vine(Vector3 a,Vector3 b)
        {
            Vector3 direction=b-a;var stem=Shape("Forking vine",PrimitiveType.Capsule,(a+b)*.5f,new Vector3(.18f,direction.magnitude*.5f,.18f),Dark);stem.transform.rotation=Quaternion.FromToRotation(Vector3.up,direction.normalized);
        }
        static FairyHoop Hoop(string name,Vector3 p,Vector3 direction,Color color)
        {
            var root=Empty(name,scenery,p);root.rotation=Quaternion.LookRotation(direction.normalized);var hoop=root.gameObject.AddComponent<FairyHoop>();
            // Mesh ring uses solid segments with a genuinely open centre.
            for(int i=0;i<40;i++)
            {
                float a=i*Mathf.PI*2/40;
                var segment=Shape("Hoop rim",PrimitiveType.Cube,new Vector3(Mathf.Sin(a)*.9f,Mathf.Cos(a)*.9f,0),new Vector3(.15f,.10f,.10f),color,true,root);
                segment.transform.localRotation=Quaternion.Euler(0,0,-a*Mathf.Rad2Deg);
            }
            var cleared=Empty("Cleared petals",root,Vector3.zero);hoop.clearedPetals=cleared.gameObject;
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Shape("Success petal",PrimitiveType.Sphere,new Vector3(Mathf.Sin(a)*1.02f,Mathf.Cos(a)*1.02f,0),new Vector3(.18f,.18f,.08f),Gold,false,cleared);}
            cleared.gameObject.SetActive(false);return hoop;
        }
        static FairySign Sign(string name,Vector3 p,string message)
        {
            var root=Empty(name,scenery,p);var sign=root.gameObject.AddComponent<FairySign>();sign.message=message;
            Shape("Sign post",PrimitiveType.Capsule,new Vector3(0,.35f,0),new Vector3(.12f,.35f,.12f),Soil,false,root);
            var board=Shape("Leaf signboard",PrimitiveType.Cube,new Vector3(0,.69f,0),new Vector3(.65f,.34f,.10f),Cream,false,root);board.transform.localRotation=Quaternion.Euler(0,-20,-6);
            // A little trail arrow, with words revealed only in proximity.
            Shape("Sign arrow shaft",PrimitiveType.Cube,new Vector3(-.03f,.70f,-.065f),new Vector3(.30f,.045f,.025f),Dark,false,root);
            var tip=Shape("Sign arrowhead",PrimitiveType.Cube,new Vector3(.12f,.7f,-.065f),new Vector3(.11f,.11f,.025f),Dark,false,root);tip.transform.localRotation=Quaternion.Euler(0,0,45);
            sign.glow=Ring("Nearby sign glow",new Vector3(0,.02f,0),.33f,.025f,Gold,root);sign.glow.gameObject.SetActive(false);return sign;
        }
        static void CourseHud(FairyCourse course,Canvas canvas)
        {
            var count=Text("Seed and hoop counts",canvas.transform,"Seeds 0/18   Hoops 0/5",20,TextAlignmentOptions.Left);Anchor(count.rectTransform,new Vector2(0,1),new Vector2(38,-84),new Vector2(400,35),new Vector2(0,1));course.counter=count;
            var reset=Panel("Reset course button",canvas.transform,new Vector2(110,38));Anchor(reset,new Vector2(1,1),new Vector2(-38,-38),new Vector2(110,38),new Vector2(1,1));
            reset.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;var button=reset.gameObject.AddComponent<UnityEngine.UI.Button>();button.targetGraphic=reset.GetComponent<UnityEngine.UI.Image>();button.navigation=new UnityEngine.UI.Navigation{mode=UnityEngine.UI.Navigation.Mode.None};
            var colors=button.colors;colors.highlightedColor=new Color(.75f,1,.85f);colors.pressedColor=new Color(.5f,.85f,.65f);button.colors=colors;
            course.resetButton=button;var label=Text("Reset label",reset,"Reset [R]",18,TextAlignmentOptions.Center);Stretch(label.rectTransform);
            if(!canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>())canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            if(!Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>())
            {
                var events=new GameObject("Grove UI input",typeof(UnityEngine.EventSystems.EventSystem),typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            course.signPanel=Panel("Nearby sign hint",canvas.transform,new Vector2(490,46));Anchor(course.signPanel,new Vector2(.5f,0),new Vector2(0,85),new Vector2(490,46),new Vector2(.5f,0));
            course.signText=Text("Nearby sign words",course.signPanel,"",20,TextAlignmentOptions.Center);Stretch(course.signText.rectTransform);course.signPanel.gameObject.SetActive(false);
        }
    }
}
