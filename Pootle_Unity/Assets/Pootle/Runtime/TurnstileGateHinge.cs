using UnityEngine;
using UnityEngine.Events;
namespace Pootle
{
    /// <summary>Adapted from the supplied spring-detent hinge. Positive local rotation is CW by convention.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Turnstile Gate Hinge")]
    public sealed class TurnstileGateHinge : MonoBehaviour
    {
        public enum LocalAxis { X,Y,Z }
        public HingeJoint hinge;
        public Transform pivot;
        public LocalAxis axis=LocalAxis.Y;
        [Range(1,180)] public float stepDegrees=90;
        [Min(0)] public float settleEpsilon=.5f;
        public bool useHalfStepClickLogic=true;
        [Min(0)] public float spring=100,damper=15;
        [Min(.1f)] public float maximumAngularSpeed=3;
        public int rotationClicks;
        [SerializeField] float currentAngle,targetAngle;
        public float CurrentAngle=>currentAngle;
        public float TargetAngle=>targetAngle;
        public UnityEvent rotationClickCw=new UnityEvent(),rotationClickCcw=new UnityEvent();
        float previousWrapped;
        bool initialized;
        bool forcedTarget;
        Rigidbody body;
        public static Vector3 AxisVector(LocalAxis a)=>a==LocalAxis.X?Vector3.right:a==LocalAxis.Z?Vector3.forward:Vector3.up;
        void Start()=>Initialize();
        void Initialize()
        {
            if(!hinge || !pivot)return;
            body=hinge.GetComponent<Rigidbody>();
            // A newly loaded PhysX joint may not have a valid angle until its first
            // simulation step. Never feed that transient NaN into the spring target.
            float angle=hinge.angle;
            if(float.IsNaN(angle) || float.IsInfinity(angle))return;
            previousWrapped=angle;currentAngle=angle;
            if(!forcedTarget)targetAngle=Quantize(currentAngle);initialized=true;SetupHingeFromSettings();
        }
        [ContextMenu("Apply Hinge Settings")]
        public void SetupHingeFromSettings()
        {
            if(!hinge)return;
            var desiredAxis=AxisVector(axis);
            if((hinge.axis-desiredAxis).sqrMagnitude>.0001f)hinge.axis=desiredAxis;
            if(!hinge.useSpring)hinge.useSpring=true;
            var settings=hinge.spring;settings.spring=spring;settings.damper=damper;
            settings.targetPosition=Mathf.DeltaAngle(0,targetAngle);hinge.spring=settings;
            if(!body)body=hinge.GetComponent<Rigidbody>();
            if(body)body.maxAngularVelocity=maximumAngularSpeed;
        }
        [ContextMenu("Zero Rotation Clicks")]
        public void ZeroRotationClicks()=>rotationClicks=0;
        [ContextMenu("Snap Target To Nearest Step")]
        public void SnapTargetToNearestStep(){targetAngle=Quantize(currentAngle);SetupHingeFromSettings();}
        [ContextMenu("Advance CW")]
        public void ForceTargetCwOneStep(){forcedTarget=true;Advance(1);}
        [ContextMenu("Advance CCW")]
        public void ForceTargetCcwOneStep(){forcedTarget=true;Advance(-1);}
        float Quantize(float angle)=>Mathf.Round(angle/Mathf.Max(1,stepDegrees))*Mathf.Max(1,stepDegrees);
        void Advance(int sign)
        {
            targetAngle+=sign*Mathf.Max(1,stepDegrees);rotationClicks+=sign;
            if(sign>0)rotationClickCw?.Invoke();else rotationClickCcw?.Invoke();
            SetupHingeFromSettings();if(body)body.WakeUp();
        }
        void FixedUpdate()
        {
            if(!initialized)Initialize();if(!initialized)return;
            float wrapped=hinge.angle;
            if(float.IsNaN(wrapped) || float.IsInfinity(wrapped))return;
            currentAngle+=Mathf.DeltaAngle(previousWrapped,wrapped);previousWrapped=wrapped;
            float step=Mathf.Max(1,stepDegrees);
            if(forcedTarget && Mathf.Abs(Mathf.DeltaAngle(currentAngle,targetAngle))<=settleEpsilon)
            {currentAngle=targetAngle+Mathf.DeltaAngle(targetAngle,currentAngle);forcedTarget=false;}
            if(useHalfStepClickLogic && !forcedTarget)
            {
                // Unwrapped joint-relative angle avoids Euler discontinuities at ±180°,
                // including gates whose parent is rotated or whose hinge uses X/Z.
                int guard=0;
                while(Mathf.Abs(currentAngle-targetAngle)>step*.5f && guard++<360)
                    Advance(currentAngle>targetAngle?1:-1);
            }
            else if(!useHalfStepClickLogic && !forcedTarget)targetAngle=Quantize(currentAngle);
            if(Mathf.Abs(currentAngle-targetAngle)<=settleEpsilon)targetAngle=Quantize(targetAngle);
            SetupHingeFromSettings();
        }
    }
}
