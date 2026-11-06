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
        [SerializeField, HideInInspector] Quaternion restLocalRotation = Quaternion.identity;
        public Quaternion RestLocalRotation => restLocalRotation;
        void Reset() => restLocalRotation = transform.localRotation;
        void LateUpdate() => ApplyTilt();
        void OnDisable() => RestorePose();
        [Button("Capture current pose as rest")]
        public void CaptureRestPose() => restLocalRotation = transform.localRotation;
        [Button("Preview view tilt")]
        public void ApplyTilt()
        {
            var camera = viewCamera ? viewCamera : Camera.main;
            if (!camera) { RestorePose(); return; }
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            float elevation = Mathf.Asin(Mathf.Clamp(-camera.transform.forward.y, -1, 1)) * Mathf.Rad2Deg;
            bool clamp = policy && !overridePolicy ? policy.limitTiltToCameraElevation : limitToCameraElevation;
            float angle = clamp ? Mathf.Min(EffectiveTiltDegrees, Mathf.Max(0, elevation)) : EffectiveTiltDegrees;
            AppliedTiltDegrees = angle;
            Quaternion restWorld = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restLocalRotation;
            transform.rotation = Quaternion.AngleAxis(angle, right) * restWorld;
        }
        [Button("Restore rest pose")]
        public void RestorePose() { transform.localRotation = restLocalRotation; AppliedTiltDegrees = 0; }
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
