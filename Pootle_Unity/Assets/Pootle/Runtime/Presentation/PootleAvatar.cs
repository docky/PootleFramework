using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Optional Avatar Animator")]
    public sealed class PootleAvatar : MonoBehaviour
    {
        public PootleCharacter character;
        public Animator animator;
        [Min(.1f)] public float joggingAnimationRate = 2;
        [Min(.1f)] public float runningAnimationRate = 3;
        private int speed, vertical, grounded, mode;
        private bool hasSpeed, hasVertical, hasGrounded, hasMode, hasLocomotionRate;
        private static readonly int LocomotionRate = Animator.StringToHash("LocomotionRate");
        private string state;
        private void OnEnable() => state = null;
        private void Awake()
        {
            if (!character) character = GetComponentInParent<PootleCharacter>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (!animator) return;
            animator.applyRootMotion = false;
            speed = Animator.StringToHash("Speed"); vertical = Animator.StringToHash("VerticalSpeed");
            grounded = Animator.StringToHash("Grounded"); mode = Animator.StringToHash("Mode");
            foreach (var p in animator.parameters)
            {
                hasSpeed |= p.nameHash == speed && p.type == AnimatorControllerParameterType.Float;
                hasVertical |= p.nameHash == vertical && p.type == AnimatorControllerParameterType.Float;
                hasGrounded |= p.nameHash == grounded && p.type == AnimatorControllerParameterType.Bool;
                hasMode |= p.nameHash == mode && p.type == AnimatorControllerParameterType.Int;
                hasLocomotionRate |= p.nameHash == LocomotionRate && p.type == AnimatorControllerParameterType.Float;
            }
        }
        private void LateUpdate()
        {
            if (!animator || !animator.isActiveAndEnabled || !character || !animator.runtimeAnimatorController) return;
            if (hasSpeed) animator.SetFloat(speed, Vector3.ProjectOnPlane(character.Velocity, Vector3.up).magnitude);
            if (hasVertical) animator.SetFloat(vertical, character.Velocity.y);
            if (hasGrounded) animator.SetBool(grounded, character.IsGrounded);
            if (hasMode) animator.SetInteger(mode, (int)character.Mode);
            string next = character.Mode switch
            {
                PootleMode.Grounded => "Locomotion",
                PootleMode.PoleStanding => "Locomotion",
                PootleMode.GroundPounding => "SeatedSlide",
                PootleMode.RailWalking => "Locomotion",
                PootleMode.RailSliding => "SeatedSlide",
                PootleMode.Sliding => "SeatedSlide",
                PootleMode.Climbing => "Climb",
                PootleMode.PoleClimbing => "Climb",
                PootleMode.LedgeHanging => "LedgeHang",
                PootleMode.Dangling => "LedgeHang",
                PootleMode.Gliding => "Glide",
                PootleMode.LedgeHop => character.Velocity.y >= 0 ? "Jump" : "Fall",
                _ => character.Velocity.y > 0 ? "Jump" : "Fall"
            };
            if(character.SlideRideAirborne)next="SeatedSlide";
            if(hasLocomotionRate)animator.SetFloat(LocomotionRate,next=="Locomotion" && Vector3.ProjectOnPlane(character.Velocity,Vector3.up).sqrMagnitude>.01f
                ? character.IsRunning ? runningAnimationRate : joggingAnimationRate : 1);
            if (next != state && animator.HasState(0, Animator.StringToHash(next)))
            {
                animator.CrossFadeInFixedTime(next, 0.12f, 0);
                state = next;
            }
        }
    }
}
