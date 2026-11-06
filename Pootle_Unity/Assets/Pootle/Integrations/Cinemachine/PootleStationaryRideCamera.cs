using UnityEngine;
using Unity.Cinemachine;
namespace Pootle
{
    // An authored wide shot: the swinging avatar never becomes a follow/look-at target.
    [DefaultExecutionOrder(-60), RequireComponent(typeof(CinemachineCamera))]
    public sealed class PootleStationaryRideCamera : MonoBehaviour
    {
        public PootleMachinePassenger passenger;
        CinemachineCamera rig;
        void Awake() => rig = GetComponent<CinemachineCamera>();
        void LateUpdate() => TickCamera();
        public void TickCamera()
        {
            if (!rig) rig = GetComponent<CinemachineCamera>();
            rig.Priority = passenger && passenger.Occupied ? 600 : -100;
        }
        void OnDisable() { if (rig) rig.Priority = -100; }
    }
}
