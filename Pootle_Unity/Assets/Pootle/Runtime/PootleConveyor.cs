using UnityEngine;
namespace Pootle
{
    public interface IPootleConveyorPath { Vector3 DirectionAt(Vector3 worldPoint); }
    [AddComponentMenu("Pootle/Conveyor Surface")]
    public sealed class PootleConveyor : MonoBehaviour
    {
        public float speed=3;
        public Vector3 localDirection=Vector3.forward;
        [Tooltip("Turn riders and props by the bend in the belt, preserving their relative heading.")]
        public bool rotateWithCurve=true;
        private IPootleConveyorPath path;
        private void Awake(){foreach(var component in GetComponents<MonoBehaviour>())if(component is IPootleConveyorPath curve)path=curve;}
        private Vector3 DirectionAt(Vector3 point)
        {
            if(path==null)Awake();
            return (path!=null?path.DirectionAt(point):transform.TransformDirection(localDirection)).normalized;
        }
        public Vector3 VelocityAt(Vector3 point)=>DirectionAt(point)*speed;
        public Quaternion RotationBetween(Vector3 from,Vector3 to)
        {
            if(!rotateWithCurve || (to-from).sqrMagnitude<.00000001f)return Quaternion.identity;
            var a=Vector3.ProjectOnPlane(DirectionAt(from),Vector3.up);
            var b=Vector3.ProjectOnPlane(DirectionAt(to),Vector3.up);
            if(a.sqrMagnitude<.001f || b.sqrMagnitude<.001f)return Quaternion.identity;
            return Quaternion.AngleAxis(Vector3.SignedAngle(a,b,Vector3.up),Vector3.up);
        }
    }
}
