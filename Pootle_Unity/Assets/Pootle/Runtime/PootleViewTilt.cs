using UnityEngine;
using Sirenix.OdinInspector;

namespace Pootle
{
    // Put on a dedicated visual-only pivot at the feet, below the facing/animation root.
    [DefaultExecutionOrder(10000), DisallowMultipleComponent, AddComponentMenu("Pootle/View Tilt")]
    public sealed class PootleViewTilt : MonoBehaviour
    {
        [Tooltip("Rendered camera. If empty, uses Camera.main. Works with NPCs and props too.")]
        public Camera viewCamera;
        public PootleCameraPolicy policy;
        [Tooltip("Use this object's local angle instead of the shared camera policy.")]
        public bool overridePolicy;
        [ShowInInspector, ReadOnly] public float AppliedTiltDegrees { get; private set; }
        public float EffectiveTiltDegrees => policy && !overridePolicy ? (policy.enableViewTilt ? policy.viewTiltDegrees : 0) : tiltBackDegrees;
        [Range(0, 60), Tooltip("Lean away from the view to reveal more of the upright silhouette.")]
        public float tiltBackDegrees = 25;
        public bool limitToCameraElevation = true;
        [Min(0), Tooltip("Seconds to return from the climbing pose to camera-relative tilt after detaching.")]
        public float detachBlendTime = .25f;
        [ShowInInspector, ReadOnly, Tooltip("The visible model must be below this pivot in the hierarchy.")]
        public bool HasVisualChildren => GetComponentInChildren<Renderer>(true) != null;
        [SerializeField, HideInInspector] Quaternion restLocalRotation = Quaternion.identity;
        public Quaternion RestLocalRotation => restLocalRotation;
        PootleCharacter character;
        PootleTraversal traversal;
        bool wasAttached;
        float returnElapsed;
        Quaternion attachedLocalRotation;
        void Reset() => restLocalRotation = transform.localRotation;
        void OnEnable()
        {
            character = GetComponentInParent<PootleCharacter>();
            traversal = character ? character.GetComponent<PootleTraversal>() : null;
            if (character) character.Teleported += OnTeleported;
            wasAttached = false; returnElapsed = detachBlendTime;
        }
        void LateUpdate() => TickTilt(Time.deltaTime);
        void OnDisable()
        {
            if (character) character.Teleported -= OnTeleported;
            RestorePose();
        }
        void OnTeleported(Vector3 delta) => RestorePose();
        [Button("Capture current pose as rest")]
        public void CaptureRestPose() => restLocalRotation = transform.localRotation;
        [Button("Preview view tilt")]
        public void ApplyTilt() => UpdateTilt(0, true);
        public void TickTilt(float deltaTime) => UpdateTilt(deltaTime, false);
        void UpdateTilt(float dt, bool preview)
        {
            bool attached = character && (character.Mode == PootleMode.Climbing ||
                character.Mode == PootleMode.PoleClimbing || character.Mode == PootleMode.LedgeHanging);
            if (attached)
            {
                // Attachment owns the visual orientation immediately, including sloping rock faces.
                if (character.Mode != PootleMode.PoleClimbing && traversal && traversal.AttachedWall)
                {
                    Vector3 normal = traversal.VisualSurfaceNormal;
                    Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
                    if (normal.sqrMagnitude > .001f && up.sqrMagnitude > .001f)
                        transform.rotation = Quaternion.LookRotation(-normal, up) * restLocalRotation;
                    else transform.localRotation = restLocalRotation;
                }
                else transform.localRotation = restLocalRotation;
                attachedLocalRotation = transform.localRotation;
                wasAttached = true; returnElapsed = 0; AppliedTiltDegrees = 0;
                return;
            }
            var camera = viewCamera ? viewCamera : Camera.main;
            if (!camera) { RestorePose(); return; }
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            float elevation = Mathf.Asin(Mathf.Clamp(-camera.transform.forward.y, -1, 1)) * Mathf.Rad2Deg;
            bool clamp = policy && !overridePolicy ? policy.limitTiltToCameraElevation : limitToCameraElevation;
            float angle = clamp ? Mathf.Min(EffectiveTiltDegrees, Mathf.Max(0, elevation)) : EffectiveTiltDegrees;
            Quaternion restWorld = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restLocalRotation;
            Quaternion targetWorld = Quaternion.AngleAxis(angle, right) * restWorld;
            if (preview || detachBlendTime <= 0)
            {
                wasAttached = false; returnElapsed = detachBlendTime;
            }
            float blend = 1;
            if (wasAttached)
            {
                if (float.IsFinite(dt) && dt > 0) returnElapsed += dt;
                blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(returnElapsed / detachBlendTime));
                if (returnElapsed >= detachBlendTime) wasAttached = false;
                Quaternion targetLocal = transform.parent ? Quaternion.Inverse(transform.parent.rotation) * targetWorld : targetWorld;
                transform.localRotation = Quaternion.Slerp(attachedLocalRotation, targetLocal, blend);
            }
            else transform.rotation = targetWorld;
            AppliedTiltDegrees = angle * blend;
        }
        [Button("Restore rest pose")]
        public void RestorePose()
        {
            transform.localRotation = restLocalRotation; AppliedTiltDegrees = 0;
            wasAttached = false; returnElapsed = detachBlendTime;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.gray; Gizmos.DrawRay(transform.position, Vector3.up * 2);
            Gizmos.color = Color.cyan; Gizmos.DrawRay(transform.position, transform.up * 2);
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + transform.up * 2.2f, $"View Tilt · {AppliedTiltDegrees:F0}° applied / {EffectiveTiltDegrees:F0}° requested");
#endif
        }
    }
}
