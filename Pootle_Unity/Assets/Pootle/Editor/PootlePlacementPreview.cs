using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace Pootle.Editor
{
    // Explicit editor session. No ExecuteAlways, OnValidate, runtime simulation or persistent priorities.
    public sealed class PootlePlacementPreview : IDisposable
    {
        public readonly PootleZoneCameraDirector Director;
        public PootleCameraZone Region { get; private set; }
        public CinemachineCamera Shot { get; private set; }
        public float BlendProgress { get; private set; } = 1;
        public float YawChange { get; private set; }
        public bool BlendChanges = true;
        public int SurfaceMask = ~0;
        public float FootOffset = .03f;
        readonly Dictionary<Transform, (Vector3 position, Quaternion rotation, Vector3 scale)> poses = new();
        readonly CinemachineBrain brain;
        readonly CinemachineBrain.UpdateMethods updateMethod;
        readonly Camera camera;
        readonly float fov, orthoSize;
        readonly bool orthographic;
        readonly PootleViewTilt[] tilts;
        readonly PootleCaveLighting lighting;
        CinemachineCamera from;
        int overrideId = -1, frame;
        float elapsed, duration;
        bool disposed;
        public PootlePlacementPreview(PootleZoneCameraDirector director)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Placement preview is Edit Mode only.");
            if (!director || !director.target || !director.focus || !director.brain) throw new InvalidOperationException("Assign a director with player, focus and Brain.");
            if (CinemachineCore.SoloCamera != null) throw new InvalidOperationException("Turn off Cinemachine Solo before starting placement preview.");
            Director = director; brain = director.brain; updateMethod = brain.UpdateMethod;
            camera = brain.OutputCamera; fov = camera.fieldOfView; orthoSize = camera.orthographicSize; orthographic = camera.orthographic;
            foreach (var t in new[] { director.target, director.focus.transform, camera.transform }) Remember(t);
            foreach (var shot in UnityEngine.Object.FindObjectsByType<PootleExplorationShot>()) Remember(shot.transform);
            tilts = UnityEngine.Object.FindObjectsByType<PootleViewTilt>(); foreach (var tilt in tilts) Remember(tilt.transform);
            var glider = director.character ? director.character.GetComponent<PootleGliderVisual>() : null;
            if (glider && glider.gliderRoot) { Remember(glider.gliderRoot); glider.gliderRoot.localScale = Vector3.zero; }
            lighting = UnityEngine.Object.FindAnyObjectByType<PootleCaveLighting>(); if (lighting) lighting.BeginSession();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
            Tick(0);
        }
        void Remember(Transform t) { if (t && !poses.ContainsKey(t)) poses.Add(t, (t.localPosition, t.localRotation, t.localScale)); }
        public bool FindSurface(Ray ray, float distance, out RaycastHit hit)
        {
            Physics.SyncTransforms();
            foreach (var candidate in Physics.RaycastAll(ray, distance, SurfaceMask, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (candidate.transform.IsChildOf(Director.target) || SceneVisibilityManager.instance.IsHidden(candidate.collider.gameObject) || candidate.normal.y < .45f) continue;
                // Cutaway cave wall collision extends above the visible wall; never place on its invisible cap.
                var renderer = candidate.collider.GetComponent<Renderer>();
                if (renderer && candidate.point.y > renderer.bounds.max.y + .1f) continue;
                hit = candidate; return true;
            }
            hit = default; return false;
        }
        public bool Place(Ray ray, float distance = 1000)
        {
            if (!FindSurface(ray, distance, out var hit)) return false;
            Director.target.position = hit.point + Vector3.up * FootOffset;
            Tick(0); return true;
        }
        public bool SnapNear(Vector3 point, float above = 2, float below = 12)
            => Place(new Ray(point + Vector3.up * above, Vector3.down), above + below);
        public void Tick(float dt)
        {
            if (disposed || Application.isPlaying || !Director || !brain) return;
            var nextRegion = Director.SelectRegion(Director.target.position, false);
            var next = nextRegion ? nextRegion.cameraRig : Director.fallbackCamera;
            if (next != Shot)
            {
                from = Shot; Shot = next; Region = nextRegion; elapsed = 0;
                duration = BlendChanges && from ? (Region && Region.overrideBlend ? Region.blendSeconds : Director.blendSeconds) : 0;
                YawChange = from && Shot ? Mathf.Abs(Mathf.DeltaAngle(from.transform.eulerAngles.y, Shot.transform.eulerAngles.y)) : 0;
            }
            Director.focus.Snap();
            foreach (var rig in new[] { from, Shot }.Where(r => r).Distinct())
            {
                if (rig.TryGetComponent<PootleExplorationShot>(out var shot)) { shot.ResetTracking(); shot.TickShot(0); }
                rig.PreviousStateIsValid = false; rig.InternalUpdateCameraState(Vector3.up, -1);
            }
            elapsed += Mathf.Max(0, dt); BlendProgress = duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration);
            float weight = Mathf.SmoothStep(0, 1, BlendProgress);
            overrideId = brain.SetCameraOverride(overrideId, 10000, from, Shot, weight, Mathf.Max(.0001f, dt));
            brain.ManualUpdate(++frame, Mathf.Max(.0001f, dt));
            foreach (var tilt in tilts) if (tilt && tilt.isActiveAndEnabled) tilt.ApplyTilt();
            if (lighting) lighting.Refresh();
        }
        public void Finish(bool keepPlayer)
        {
            if (disposed) return;
            var target = Director ? Director.target : null;
            Vector3 finalPosition = target ? target.position : Vector3.zero;
            Dispose();
            if (keepPlayer && target)
            {
                Undo.RecordObject(target, "Keep surface-snapped player placement"); target.position = finalPosition;
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (brain) { if (overrideId >= 0) brain.ReleaseCameraOverride(overrideId); brain.UpdateMethod = updateMethod; brain.ResetState(); }
            if (lighting) lighting.EndSession();
            foreach (var pair in poses) if (pair.Key) { pair.Key.localPosition = pair.Value.position; pair.Key.localRotation = pair.Value.rotation; pair.Key.localScale = pair.Value.scale; }
            if (camera) { camera.fieldOfView = fov; camera.orthographic = orthographic; camera.orthographicSize = orthoSize; }
        }
    }
}
