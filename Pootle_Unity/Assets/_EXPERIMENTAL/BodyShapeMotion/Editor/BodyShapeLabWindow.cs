using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
namespace Docky.BodyShapeMotion.Editor
{
    public sealed class BodyShapeLabWindow : EditorWindow
    {
        DropdownField row;Toggle focus;Slider phase;Label metrics;
        BodyShapeLab Lab=>Object.FindAnyObjectByType<BodyShapeLab>();
        [MenuItem("Tools/Body Shape Motion/Open Doggy Lab")]
        public static void Open(){var w=GetWindow<BodyShapeLabWindow>();w.titleContent=new GUIContent("Body Shape Lab");w.minSize=new Vector2(420,520);}
        public void CreateGUI()
        {
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(DoggyBodyShapeLabBuilder.Folder+"/Editor/BodyShapeLabWindow.uxml").CloneTree(rootVisualElement);
            row=rootVisualElement.Q<DropdownField>("animation");row.choices=DoggyBodyShapeLabBuilder.Clips.Select((n,i)=>(i+1).ToString("00")+" · "+n.Replace("KA_","")).ToList();row.index=0;
            focus=rootVisualElement.Q<Toggle>("focus");phase=rootVisualElement.Q<Slider>("phase");metrics=rootVisualElement.Q<Label>("metrics");
            row.RegisterValueChangedCallback(_=>Focus());focus.RegisterValueChangedCallback(_=>Focus());
            Button("open",()=>{if(EditorApplication.isPlaying)return;if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(DoggyBodyShapeLabBuilder.ScenePath);Focus();});
            Button("build",()=>{DoggyBodyShapeLabBuilder.Create();Focus();});
            Button("previous",()=>row.index=(row.index+15)%16);Button("next",()=>row.index=(row.index+1)%16);
            Button("play",()=>{if(!Lab)return;if(!EditorApplication.isPlaying){EditorApplication.isPlaying=true;return;}Lab.playing=!Lab.playing;});
            Button("restart",()=>{if(!Lab)return;Lab.Restart();if(!EditorApplication.isPlaying)DoggyBodyShapeLabBuilder.Preview(Lab,0);});
            Button("frame",()=>{if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAtDirect(new Vector3(4.8f,.7f,row.index*3.4f),Quaternion.Euler(10,0,0),5.5f);});
            Button("profile",()=>{Selection.activeObject=AssetDatabase.LoadAssetAtPath<BodyShapeProfile>(DoggyBodyShapeLabBuilder.ProfilePath);EditorGUIUtility.PingObject(Selection.activeObject);});
            Button("audit",()=>{if(Lab && !EditorApplication.isPlaying)metrics.text=BodyShapeAudit.Run(Lab);});
            var bakeLevel=rootVisualElement.Q<DropdownField>("bakeLevel");bakeLevel.choices=DoggyBodyShapeLabBuilder.Levels.ToList();bakeLevel.index=3;
            Button("bakeCurrent",()=>Bake(false));Button("bakeAll",()=>Bake(true));
            phase.RegisterValueChangedCallback(e=>{if(!Lab)return;if(EditorApplication.isPlaying)Lab.Scrub(e.newValue);else DoggyBodyShapeLabBuilder.Preview(Lab,e.newValue);});
            rootVisualElement.Q<Slider>("speed").RegisterValueChangedCallback(e=>{if(Lab)Lab.speed=e.newValue;});
            rootVisualElement.schedule.Execute(UpdateMetrics).Every(350);
        }
        void Button(string name,System.Action action)=>rootVisualElement.Q<Button>(name).clicked+=action;
        void Bake(bool all)
        {
            var result=rootVisualElement.Q<Label>("bakeResult");
            if(!Lab || EditorApplication.isPlaying){result.text="Open the comparison scene in Edit Mode first.";return;}
            try
            {
                int level=rootVisualElement.Q<DropdownField>("bakeLevel").index;
                var samples=Lab.samples.Where(s=>s.column==level && (all || s.row==row.index)).OrderBy(s=>s.row).ToArray();
                string folder=BodyShapeClipBaker.BakeSelection(samples,rootVisualElement.Q<IntegerField>("bakeFps").value,rootVisualElement.Q<Toggle>("bakeSmooth").value);
                result.text="Saved "+samples.Length+" clips and playback prefab to "+folder;
                Selection.activeObject=AssetDatabase.LoadAssetAtPath<Object>(folder);EditorGUIUtility.PingObject(Selection.activeObject);
            }
            catch(System.Exception e){result.text="Bake failed: "+e.Message;Debug.LogException(e);}
        }
        void Focus(){if(!Lab)return;Lab.Focus(row.index,focus.value);if(!EditorApplication.isPlaying)DoggyBodyShapeLabBuilder.Preview(Lab,phase.value);SceneView.RepaintAll();}
        void UpdateMetrics()
        {
            bool canBake=Lab && !EditorApplication.isPlaying;
            rootVisualElement.Q<Button>("bakeCurrent").SetEnabled(canBake);rootVisualElement.Q<Button>("bakeAll").SetEnabled(canBake);
            if(!Lab){metrics.text="Open or build the comparison scene to begin.";return;}
            if(EditorApplication.isPlaying && Lab.playing)phase.SetValueWithoutNotify(Lab.phase);
            var lines=Lab.samples.Where(s=>s.row==row.index).OrderBy(s=>s.column).Select(s=>{var a=s.GetComponent<BodyShapeAdjustment>();return DoggyBodyShapeLabBuilder.Levels[s.column]+$"  ·  hands (sum) {a.HandPenetration*1000:F0} mm  ·  shoe depth {a.FootPenetration*1000:F0} mm";});
            metrics.text=string.Join("\n",lines)+"\nAudit CSV: Builds/BodyShapeMotion/doggy-overlap-audit.csv";
        }
    }
}
