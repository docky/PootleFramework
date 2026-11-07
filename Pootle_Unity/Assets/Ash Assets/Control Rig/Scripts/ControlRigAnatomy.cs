using UnityEngine;
namespace ControlRig
{
    [DisallowMultipleComponent]
    public sealed class ControlRigAnatomy : MonoBehaviour
    {
        public enum Facing { AutoFromFeet, PositiveZ, NegativeZ }
        [Tooltip("Auto estimates forward from toe reach beyond the ankles. Override for symmetric or unusual feet, then refit the control rig.")]
        public Facing facing;
        [HideInInspector] public float detectedForward = 1;
        public float ForwardSign => facing == Facing.PositiveZ ? 1 : facing == Facing.NegativeZ ? -1 : detectedForward;
        public Vector3 Forward => transform.forward * ForwardSign;
    }
}
