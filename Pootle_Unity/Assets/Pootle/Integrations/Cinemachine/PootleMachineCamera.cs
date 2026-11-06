using UnityEngine;
using Unity.Cinemachine;
namespace Pootle
{
    [DefaultExecutionOrder(10200), RequireComponent(typeof(CinemachineCamera))]
    public sealed class PootleMachineCamera : MonoBehaviour
    {
        public PootleMachinePassenger passenger;
        public Transform vehicle;
        [Tooltip("Follow the cart from a fixed world-space angle, like the exploration camera. Disable for the original cart-relative chase view.")]
        public bool fixedThirdPerson = true;
        [Min(.01f)] public float followSharpness = 5;

        [Header("Fixed Third Person")]
        public PootleCameraPolicy explorationPolicy;
        [Tooltip("World-space pitch and yaw; unaffected by track turns, climbs or descents.")]
        public Vector2 fixedPitchYaw = new Vector2(55, 0);
        [Min(1)] public float fixedDistance = 28;
        public float focusHeight = 1.2f;
        [Range(15, 90), Tooltip("Used when no exploration policy is assigned.")]
        public float fixedFieldOfView = 30;

        [Header("Chase Camera")]
        public Vector3 offset = new Vector3(5, 4.5f, -8);
        [Range(15, 90)] public float chaseFieldOfView = 48;

        public float EffectiveFixedDistance => Mathf.Max(1, fixedDistance) * (explorationPolicy ? explorationPolicy.CameraDistanceMultiplier : 1);
        public float EffectiveFixedFieldOfView => explorationPolicy ? explorationPolicy.fieldOfView : fixedFieldOfView;
        CinemachineCamera cameraRig;
        bool wasOccupied;
        void Awake() { cameraRig = GetComponent<CinemachineCamera>(); }
        void LateUpdate() => TickCamera(Time.deltaTime);
        public void TickCamera(float dt)
        {
            if (!float.IsFinite(dt) || dt < 0) return;
            if (!cameraRig) cameraRig = GetComponent<CinemachineCamera>();
            bool occupied = passenger && passenger.Occupied && vehicle;
            cameraRig.Priority = occupied ? 500 : -100;
            if (occupied)
            {
                float blend = wasOccupied ? 1 - Mathf.Exp(-Mathf.Max(.01f, followSharpness) * dt) : 1;
                Quaternion rotation = Quaternion.Euler(fixedPitchYaw.x, fixedPitchYaw.y, 0);
                Vector3 focus = vehicle.position + Vector3.up * focusHeight;
                Vector3 target = fixedThirdPerson
                    ? focus - rotation * Vector3.forward * EffectiveFixedDistance
                    : vehicle.position + vehicle.rotation * offset;
                transform.position = Vector3.Lerp(transform.position, target, blend);
                if (!fixedThirdPerson)
                    rotation = Quaternion.LookRotation(vehicle.position + vehicle.forward * 2 + Vector3.up - transform.position, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, blend);
                cameraRig.Lens.FieldOfView = Mathf.Lerp(cameraRig.Lens.FieldOfView,
                    fixedThirdPerson ? EffectiveFixedFieldOfView : chaseFieldOfView, blend);
                if (!wasOccupied) cameraRig.PreviousStateIsValid = false;
            }
            wasOccupied = occupied;
        }
        void OnDisable() { if (cameraRig) cameraRig.Priority = -100; wasOccupied = false; }
    }
}
