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
            float angle = limitToCameraElevation ? Mathf.Min(tiltBackDegrees, Mathf.Max(0, elevation)) : tiltBackDegrees;
            Quaternion restWorld = (transform.parent ? transform.parent.rotation : Quaternion.identity) * restLocalRotation;
            transform.rotation = Quaternion.AngleAxis(angle, right) * restWorld;
        }
        [Button("Restore rest pose")]
        public void RestorePose() => transform.localRotation = restLocalRotation;
    }
}
