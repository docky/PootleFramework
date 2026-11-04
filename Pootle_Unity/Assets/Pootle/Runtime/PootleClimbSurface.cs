using UnityEngine;
namespace Pootle
{
    [AddComponentMenu("Pootle/Climbable Wall")]
    public sealed class PootleClimbSurface : MonoBehaviour
    {
        public bool allowCornerWrapping=true;
        [Range(0,95)] public float maximumCornerAngle=95;
        public bool rotateCamera=true;
        [Tooltip("Use a continuous circular side instead of the cylinder render mesh facets.")]
        public bool cylindrical;
        [Min(.01f)] public float localCylinderRadius=.5f;

        public bool TryFollow(Collider shape,Vector3 center,Vector3 previousNormal,float clearance,out Vector3 target,out Vector3 normal)
        {
            target=center;normal=previousNormal;
            if(cylindrical)
            {
                Vector3 axis=shape.transform.up;
                Vector3 radial=Vector3.ProjectOnPlane(center-shape.bounds.center,axis);
                if(radial.sqrMagnitude<.001f)return false;
                normal=Vector3.ProjectOnPlane(radial,Vector3.up).normalized;
                float radius=localCylinderRadius*Mathf.Max(Mathf.Abs(shape.transform.lossyScale.x),Mathf.Abs(shape.transform.lossyScale.z));
                target=center-radial+normal*(radius+clearance);return true;
            }
            Vector3 point=shape.ClosestPoint(center);
            normal=Vector3.ProjectOnPlane(center-point,Vector3.up).normalized;
            if(normal.sqrMagnitude<.01f)return false;
            float turn=Vector3.Angle(previousNormal,normal);
            if(!allowCornerWrapping && turn>1)return false;
            if(turn>Mathf.Min(95,maximumCornerAngle))return false;
            // A box corner is a 90-degree change even though its capsule-offset arc is smooth.
            if(shape is BoxCollider box)
            {
                Vector3 local=box.transform.InverseTransformPoint(point)-box.center;
                bool corner=Mathf.Abs(Mathf.Abs(local.x)-box.size.x*.5f)<.005f && Mathf.Abs(Mathf.Abs(local.z)-box.size.z*.5f)<.005f;
                if(corner && (!allowCornerWrapping || maximumCornerAngle<90))return false;
            }
            target=point+normal*clearance;target.y=center.y;
            return true;
        }
    }
}
