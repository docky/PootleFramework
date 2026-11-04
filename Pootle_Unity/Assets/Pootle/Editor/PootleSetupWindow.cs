using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pootle.Editor
{
    public sealed class PootleSetupWindow : OdinEditorWindow
    {
        [MenuItem("Pootle/Setup")]
        private static void Open() => GetWindow<PootleSetupWindow>("Pootle Setup").Show();

        [Title("Pootle v0.1", "Character setup · twin-stick movement and camera")]
        [BoxGroup("Character"), SceneObjectsOnly, OnValueChanged(nameof(Refresh))]
        public PootleCharacter character;
        [BoxGroup("Character"), AssetsOnly, Tooltip("Optional mesh or model prefab. Lily is used by default.")]
        public GameObject modelAsset;
        [BoxGroup("Character"), ShowInInspector, ReadOnly]
        private string Project => "Unity " + Application.unityVersion + " · Input System · Cinemachine 6.6 · Odin Inspector";

        [BoxGroup("Character"), Button(ButtonSizes.Large), GUIColor(0.6f, 0.9f, 0.76f), DisableInPlayMode]
        private void CreateOrConnectCharacter()
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up Pootle");
            if (!character) character = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<PootleCharacter>() : null;
            if (!character) character = FindObjectsByType<PootleCharacter>().FirstOrDefault();
            if (!character) character = PootleSceneSetup.CreateCharacter(modelAsset);
            Undo.RecordObject(character, "Assign Pootle references");
            if (!character.profile) character.profile = PootleSceneSetup.DefaultProfile();
            if (!character.input) character.input = character.GetComponent<PootleInput>() ?? Undo.AddComponent<PootleInput>(character.gameObject);
            if (!character.input.actions)
            {
                Undo.RecordObject(character.input, "Assign Pootle controls");
                character.input.actions = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(PootleSceneSetup.Root + "/Data/PootleControls.inputactions");
            }
            PootleSceneSetup.ConnectCamera(character);
            Undo.CollapseUndoOperations(group);
            Selection.activeGameObject = character.gameObject;
            ValidateSetup();
        }

        [BoxGroup("Character"), Button, DisableInPlayMode]
        private void ConnectCinemachineCamera() { if (character) PootleSceneSetup.ConnectCamera(character); ValidateSetup(); }
        [BoxGroup("Character"), Button, DisableInPlayMode]
        private void FitCapsuleToVisualBounds() { PootleSceneSetup.FitCapsule(character); ValidateSetup(); }
        [BoxGroup("Character"), Button, DisableInPlayMode]
        private void ReplaceVisualModel()
        {
            if (!character || !character.visualRoot || !modelAsset) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            foreach (Transform child in character.visualRoot.Cast<Transform>().ToArray()) Undo.DestroyObjectImmediate(child.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            Undo.RegisterCreatedObjectUndo(model, "Replace Pootle visual");
            model.transform.SetParent(character.visualRoot, false);
            foreach (var a in model.GetComponentsInChildren<Animator>()) { a.applyRootMotion = false; a.enabled = false; }
            PootleSceneSetup.AlignModel(model.transform, character.GetComponent<PootleMotor>().Height);
            EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
            Undo.CollapseUndoOperations(group);
            ValidateSetup();
        }

        [BoxGroup("Validation"), ShowInInspector, ReadOnly, MultiLineProperty(8)]
        private string diagnostics = "Select a character, or create one. Open the playground for the ready-to-play sample.";
        [BoxGroup("Validation"), Button]
        public void ValidateSetup()
        {
            var messages = new List<string>();
            var settings = new SerializedObject(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
            var backend = settings.FindProperty("activeInputHandler");
            messages.Add("Input backend: " + (backend == null ? "see Player Settings" : backend.intValue == 0 ? "ERROR: legacy only; enable Input System" : "Input System available"));
            if (!character) { messages.Add("Assign a PootleCharacter or use Create Or Connect Character."); diagnostics = string.Join("\n", messages); return; }
            if (!character.profile) messages.Add("ERROR: assign a movement profile.");
            if (!character.input || !character.input.actions) messages.Add("ERROR: assign PootleControls to PootleInput.");
            else foreach (string action in new[] { "Character/Move", "Character/Run", "Character/Jump", "Character/Reset", "Camera/Look", "Camera/Recenter" })
                if (character.input.actions.FindAction(action) == null) messages.Add("ERROR: missing action " + action);
            if (!character.outputCamera) messages.Add("ERROR: connect the rendered Camera transform.");
            else if (!character.outputCamera.GetComponent<CinemachineBrain>()) messages.Add("ERROR: output camera needs CinemachineBrain.");
            if (!character.cameraTarget) messages.Add("ERROR: assign a camera target.");
            if (!character.visualRoot) messages.Add("Static avatar: no visual root assigned.");
            if (character.transform.lossyScale != Vector3.one) messages.Add("ERROR: character root needs unit world scale. Scale the visual child instead.");
            if (Quaternion.Angle(character.transform.rotation, Quaternion.identity) > 0.1f) messages.Add("ERROR: keep the world-up motor root unrotated; rotate the visual child instead.");
            var motor = character.GetComponent<PootleMotor>();
            if (motor)
            {
                messages.Add($"Capsule: {motor.Radius:F2} m radius × {motor.Height:F2} m height · centre {motor.center}");
                messages.Add($"Steps ≤ {motor.stepHeight:F2} m · slopes ≤ {motor.slopeLimit:F0}° · collision mask {motor.collisionMask.value} · triggers ignored");
                if (motor.collisionMask.value == 0) messages.Add("ERROR: collision mask is empty.");
                if (!FindObjectsByType<Collider>().Any(c => !c.isTrigger && (motor.collisionMask.value & (1 << c.gameObject.layer)) != 0))
                    messages.Add("ERROR: no solid level colliders found in the included layers.");
            }
            if (!messages.Any(m => m.StartsWith("ERROR"))) messages.Add("Ready: movement works with a static mesh; animation is optional.");
            diagnostics = string.Join("\n", messages);
        }
        private void Refresh() { ValidateSetup(); SceneView.RepaintAll(); }

        [FoldoutGroup("Movement tuning"), ShowInInspector, InlineEditor, HideLabel]
        private PootleProfile Tuning => character ? character.profile : null;
        [FoldoutGroup("Live state"), ShowInInspector, ReadOnly] private PootleMode Mode => character ? character.Mode : PootleMode.Airborne;
        [FoldoutGroup("Live state"), ShowInInspector, ReadOnly] private Vector3 Velocity => character ? character.Velocity : Vector3.zero;
        [FoldoutGroup("Live state"), ShowInInspector, ReadOnly] private Vector3 SupportNormal => character ? character.Support.normal : Vector3.up;
        [FoldoutGroup("Live state"), ShowInInspector, ReadOnly] private string JumpTiming => character ? $"Buffer {character.DebugSnapshot.bufferRemaining:F3}s · coyote {character.DebugSnapshot.coyoteRemaining:F3}s" : "";

        [BoxGroup("Scene diagnostics"), ShowInInspector, OnValueChanged(nameof(RepaintScene))]
        private bool DrawDiagnostics { get => PootleDebugView.enabled; set => PootleDebugView.enabled = value; }
        [BoxGroup("Scene diagnostics"), ShowInInspector, OnValueChanged(nameof(RepaintScene))]
        private PootleDebugView.Preset View { get => PootleDebugView.preset; set => PootleDebugView.preset = value; }
        [BoxGroup("Scene diagnostics"), ShowInInspector, Range(0.01f, 0.2f), OnValueChanged(nameof(RepaintScene))]
        private float MarkerSize { get => PootleDebugView.markerSize; set => PootleDebugView.markerSize = value; }
        [BoxGroup("Scene diagnostics"), ShowInInspector, OnValueChanged(nameof(RepaintScene))]
        private bool Labels { get => PootleDebugView.labels; set => PootleDebugView.labels = value; }
        [BoxGroup("Scene diagnostics"), ButtonGroup("Scene diagnostics/Freeze"), Button]
        private void FreezeSnapshot() { if (character) character.DebugSnapshot.frozen = true; RepaintScene(); }
        [ButtonGroup("Scene diagnostics/Freeze"), Button]
        private void ResumeLive() { if (character) character.DebugSnapshot.frozen = false; RepaintScene(); }
        [BoxGroup("Scene diagnostics"), Button]
        private void ClearHistory() { if (character) character.DebugSnapshot.Clear(); RepaintScene(); }
        [BoxGroup("Scene diagnostics"), Button]
        private void FocusLastRejection()
        {
            if (!character || !SceneView.lastActiveSceneView) return;
            var d = character.DebugSnapshot;
            for (int i = d.count - 1; i >= 0; i--)
                if (d.probes[i].status == PootleProbeStatus.Rejected)
                { SceneView.lastActiveSceneView.LookAt(d.probes[i].hitPoint); return; }
        }
        [BoxGroup("Scene diagnostics"), Button]
        private void FrameCharacter() { if (character) { Selection.activeGameObject = character.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); } }
        private void RepaintScene() => SceneView.RepaintAll();

        [Button(ButtonSizes.Large), GUIColor(0.9f, 0.8f, 0.5f), DisableInPlayMode]
        private void OpenCharacterPlayground() => PootleSceneSetup.OpenSample();
    }
}
