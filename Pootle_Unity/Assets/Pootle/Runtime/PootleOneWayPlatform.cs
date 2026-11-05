using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider)), AddComponentMenu("Pootle/One Way Floor")]
    public sealed class PootleOneWayPlatform : MonoBehaviour
    {
        [Min(0)] public float surfaceTolerance = .06f;
        BoxCollider box;
        public Vector3 Normal => transform.up;
        public Vector3 Top
        {
            get { if(!box)box=GetComponent<BoxCollider>();return transform.TransformPoint(box.center+Vector3.up*box.size.y*.5f); }
        }
        public bool IsAbove(PootleMotor motor, Vector3 position)
        {
            motor.Endpoints(position,out var a,out var b);
            float separation=Mathf.Min(Vector3.Dot(a-Top,Normal),Vector3.Dot(b-Top,Normal))-motor.Radius;
            return separation>=-surfaceTolerance;
        }
        public bool Accepts(PootleMotor motor,Vector3 position,Vector3 direction,Vector3 hitNormal)
            => Vector3.Dot(direction,Normal)<=.0001f && Vector3.Dot(hitNormal,Normal)>.5f && IsAbove(motor,position);
    }
}
