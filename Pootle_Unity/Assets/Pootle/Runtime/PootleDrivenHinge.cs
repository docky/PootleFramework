using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent,RequireComponent(typeof(HingeJoint),typeof(Rigidbody))]
    public sealed class PootleDrivenHinge : MonoBehaviour
    {
        public bool oscillate;
        public float degreesPerSecond=40, amplitude=45, period=4.5f;
        [Min(0)] public float spring=500,damper=450,maximumTorque=800;
        public float TargetAngle {get;private set;}
        public float AngularSpeed {get;private set;}
        Rigidbody body;HingeJoint hinge;float time;Quaternion restRotation;
        void Awake()
        {
            body=GetComponent<Rigidbody>();hinge=GetComponent<HingeJoint>();restRotation=transform.localRotation;
            hinge.useSpring=false;hinge.useMotor=false;
        }
        void FixedUpdate()
        {
            // Measure the twist from the authored pose. HingeJoint.angle can remain
            // NaN for a freshly loaded, sleeping free hinge until a drive wakes it.
            Quaternion twist=Quaternion.Inverse(restRotation)*transform.localRotation;
            float angle=2*Mathf.Atan2(Vector3.Dot(new Vector3(twist.x,twist.y,twist.z),hinge.axis.normalized),twist.w)*Mathf.Rad2Deg;
            time+=Time.fixedDeltaTime;
            float desiredSpeed=degreesPerSecond;
            if(oscillate)
            {
                float rate=2*Mathf.PI/Mathf.Max(.5f,period);
                TargetAngle=amplitude*Mathf.Sin(time*rate);desiredSpeed=amplitude*rate*Mathf.Cos(time*rate);
            }
            else TargetAngle=Mathf.Repeat(TargetAngle+degreesPerSecond*Time.fixedDeltaTime+180,360)-180;
            Vector3 axis=transform.TransformDirection(hinge.axis).normalized;
            AngularSpeed=Vector3.Dot(body.angularVelocity,axis)*Mathf.Rad2Deg;
            float error=Mathf.Clamp(Mathf.DeltaAngle(angle,TargetAngle),-45,45);
            float torque=(error*spring+(desiredSpeed-AngularSpeed)*damper)*Mathf.Deg2Rad;
            body.AddTorque(axis*Mathf.Clamp(torque,-maximumTorque,maximumTorque),ForceMode.Force);
        }
    }
}
