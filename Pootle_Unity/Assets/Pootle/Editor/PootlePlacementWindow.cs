using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pootle.Editor
{
    public sealed class PootlePlacementWindow : EditorWindow
    {
        PootlePlacementPreview preview;
        PootleZoneCameraDirector director;
        Label status;
        Toggle enabledToggle, blendToggle;
        Vector3 a, b;
        bool hasA, hasB;
        int surfaceMask = ~0;
        double lastTime;
        [MenuItem("Pootle/Exploration/Surface Placement Preview")]
        public static void Open() => GetWindow<PootlePlacementWindow>("Exploration Preview").Show();
        void OnEnable()
        {
            SceneView.duringSceneGui += SceneGUI; EditorApplication.update += EditorTick;
            EditorApplication.playModeStateChanged += PlayChanged; AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorSceneManager.sceneSaving += BeforeSave; EditorSceneManager.sceneClosing += BeforeClose;
        }
        void OnDisable()
        {
            Stop(); SceneView.duringSceneGui -= SceneGUI; EditorApplication.update -= EditorTick;
            EditorApplication.playModeStateChanged -= PlayChanged; AssemblyReloadEvents.beforeAssemblyReload -= Stop;
            EditorSceneManager.sceneSaving -= BeforeSave; EditorSceneManager.sceneClosing -= BeforeClose;
        }
        public void CreateGUI()
        {
            director = Object.FindAnyObjectByType<PootleZoneCameraDirector>();
            rootVisualElement.Add(new HelpBox("Edit Mode only. Enable preview, then Shift + left-click/drag on a surface in Scene view. Or drag the player's placement handle. Game view shows the selected camera. Preview bypasses dwell/hysteresis to expose exact zone boundaries.", HelpBoxMessageType.Info));
            var field = new ObjectField("Camera director") { objectType = typeof(PootleZoneCameraDirector), allowSceneObjects = true, value = director };
            field.RegisterValueChangedCallback(e => { Stop(); director = e.newValue as PootleZoneCameraDirector; }); rootVisualElement.Add(field);
            enabledToggle = new Toggle("Enable surface placement preview");
            enabledToggle.RegisterValueChangedCallback(e => { if (e.newValue) StartPreview(); else Stop(); }); rootVisualElement.Add(enabledToggle);
            blendToggle = new Toggle("Blend zone changes (off = hard cuts)") { value = true };
            blendToggle.RegisterValueChangedCallback(e => { if (preview != null) preview.BlendChanges = e.newValue; }); rootVisualElement.Add(blendToggle);
            var mask = new LayerMaskField("Surface layers", ~0);
            mask.RegisterValueChangedCallback(e => { surfaceMask = e.newValue; if (preview != null) preview.SurfaceMask = surfaceMask; }); rootVisualElement.Add(mask);
            rootVisualElement.Add(new Button(() => { if (preview != null) preview.SnapNear(director.target.position); }) { text = "Snap current position to nearby surface" });
            rootVisualElement.Add(new Button(() => { if (preview != null) { a = director.target.position; hasA = true; } }) { text = "Store position A" });
            rootVisualElement.Add(new Button(() => { if (preview != null && hasA) { director.target.position = a; preview.Tick(0); } }) { text = "Go to A" });
            rootVisualElement.Add(new Button(() => { if (preview != null) { b = director.target.position; hasB = true; } }) { text = "Store position B" });
            rootVisualElement.Add(new Button(() => { if (preview != null && hasB) { director.target.position = b; preview.Tick(0); } }) { text = "Go to B" });
            rootVisualElement.Add(new Button(() => { preview?.Finish(true); Stop(); }) { text = "Keep player here and stop preview (Undo supported)" });
            rootVisualElement.Add(new Button(Stop) { text = "Stop and restore original placement" });
            status = new Label("Preview off"); rootVisualElement.Add(status);
            rootVisualElement.Add(new HelpBox("Closing the tool, saving the scene, reloading scripts or entering Play Mode ends preview and restores the original placement. Use Keep player here to author a new spawn. A/B checks use the Brain's Ease In Out blend; runtime input and airborne deferral are not simulated.", HelpBoxMessageType.None));
        }
        void StartPreview()
        {
            try
            {
                if (!director) director = Object.FindAnyObjectByType<PootleZoneCameraDirector>();
                preview?.Dispose(); preview = new PootlePlacementPreview(director) { BlendChanges = blendToggle.value, SurfaceMask = surfaceMask };
                lastTime = EditorApplication.timeSinceStartup;
            }
            catch (System.Exception e) { Stop(); status.text = e.Message; }
        }
        void Stop() { preview?.Dispose(); preview = null; enabledToggle?.SetValueWithoutNotify(false); if (status != null) status.text = "Preview off · original placement restored"; SceneView.RepaintAll(); }
        void PlayChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) Stop(); }
        void BeforeSave(Scene scene, string path) => Stop();
        void BeforeClose(Scene scene, bool removingScene) => Stop();
        void EditorTick()
        {
            if (preview == null) return;
            double now = EditorApplication.timeSinceStartup; preview.Tick(Mathf.Min(.1f, (float)(now - lastTime))); lastTime = now;
            status.text = $"{(preview.Region ? preview.Region.name : "Fallback")}\n{(preview.Shot ? preview.Shot.name : "No shot")}\nBlend {preview.BlendProgress:P0} · yaw change {preview.YawChange:F0}° · player {director.target.position:F2}";
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
        }
        void SceneGUI(SceneView view)
        {
            if (preview == null || Application.isPlaying || !director || !director.target) return;
            var e = Event.current;
            if (e.shift && !e.alt)
            {
                if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(GUIUtility.GetControlID(FocusType.Passive));
                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
                { preview.Place(HandleUtility.GUIPointToWorldRay(e.mousePosition)); e.Use(); }
            }
            EditorGUI.BeginChangeCheck(); var point = Handles.PositionHandle(director.target.position, Quaternion.identity);
            if (EditorGUI.EndChangeCheck()) preview.SnapNear(point);
            Handles.Label(director.target.position + Vector3.up * 2.5f, "Surface placement · Shift + click");
        }
    }
}
