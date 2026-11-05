using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, DefaultExecutionOrder(140), AddComponentMenu("Pootle/Optional Avatar Animator")]
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
        private Vector3 displayedRoot,displayVelocity,displayOffset;
        private float displayUntil;
        public Vector3 RenderedRootPosition=>displayedRoot;
        private void OnEnable()
        {
            state=null;
            if(!character)character=GetComponentInParent<PootleCharacter>();
            if(character)character.Teleported+=ResetPhysicsPresentation;
            ResetPhysicsPresentation(Vector3.zero);
        }
        private void OnDisable()
        {
            if(character)character.Teleported-=ResetPhysicsPresentation;
            RemoveDisplayOffset();
        }
        private void ResetPhysicsPresentation(Vector3 unused)
        {
            RemoveDisplayOffset();
            displayedRoot=character?character.transform.position:transform.position;
            displayVelocity=Vector3.zero;displayUntil=float.NegativeInfinity;
        }
        private void RemoveDisplayOffset()
        {
            if(character && character.visualRoot)character.visualRoot.position-=displayOffset;
            displayOffset=Vector3.zero;
        }
        private void Update()=>RemoveDisplayOffset();
        private void PresentPhysicsMotion()
        {
            if(!character || !character.visualRoot)return;
            Vector3 root=character.transform.position;
            if(character.HasPhysicsMomentum)displayUntil=Time.time+.08f;
            bool free=character.Mode==PootleMode.Grounded || character.Mode==PootleMode.Airborne || character.Mode==PootleMode.Gliding;
            if(!free || Time.time>=displayUntil || (root-displayedRoot).sqrMagnitude>1)
            {displayedRoot=root;displayVelocity=Vector3.zero;return;}
            // Interpolate the visible avatar after an actual shove, rather than
            // pushing its collider early to hide fixed-step contact corrections.
            float smoothing=.025f*Mathf.Clamp01((displayUntil-Time.time)/.08f);
            displayedRoot=Vector3.SmoothDamp(displayedRoot,root,ref displayVelocity,smoothing,float.PositiveInfinity,Time.deltaTime);
            displayOffset=Vector3.ClampMagnitude(displayedRoot-root,.12f);
            displayedRoot=root+displayOffset;
            character.visualRoot.position+=displayOffset;
        }
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
            PresentPhysicsMotion();
            if (!animator || !animator.isActiveAndEnabled || !character || !animator.runtimeAnimatorController) return;
            float locomotionSpeed=character.LocomotionSpeed;
            if (hasSpeed) animator.SetFloat(speed,locomotionSpeed,.12f,Time.deltaTime);
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
            if(hasLocomotionRate)animator.SetFloat(LocomotionRate,next=="Locomotion" && locomotionSpeed>.1f
                ? character.IsRunning ? runningAnimationRate : joggingAnimationRate : 1);
            if (next != state && animator.HasState(0, Animator.StringToHash(next)))
            {
                animator.CrossFadeInFixedTime(next, 0.12f, 0);
                state = next;
            }
        }
    }
}
