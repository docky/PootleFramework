using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent]
    public sealed class PootlePlatformRider : MonoBehaviour
    {
        public PootleMovingSurface Surface { get; private set; }
        public Vector3 CarryVelocity { get; private set; }
        private Transform supportTransform;
        private Vector3 anchor;
        private Quaternion rotation;
        public void ClearRiding() { Surface = null; supportTransform=null; CarryVelocity = Vector3.zero; }
        public void Apply(PootleCharacter c, float dt)
        {
            CarryVelocity = Vector3.zero;
            if(c.IsGrounded && c.Support.collider)
            {
                var belt=c.Support.collider.GetComponentInParent<PootleConveyor>();
                if(belt && belt.isActiveAndEnabled)
                {
                    CarryVelocity=belt.VelocityAt(transform.position);
                    Vector3 before=transform.position;
                    c.Motor.Move(CarryVelocity*dt,false,.1f);
                    c.Facing=belt.RotationBetween(before,transform.position)*c.Facing;
                }
            }
            if (!supportTransform || !supportTransform.gameObject.activeInHierarchy || Surface && !Surface.isActiveAndEnabled || !c.IsGrounded) { Surface = null; supportTransform=null; return; }
            Vector3 target = supportTransform.TransformPoint(anchor);
            Vector3 delta = target - transform.position;
            CarryVelocity += Vector3.ClampMagnitude(delta / dt, 60);
            var change = supportTransform.rotation * Quaternion.Inverse(rotation);
            Vector3 facing = Vector3.ProjectOnPlane(change * c.Facing, Vector3.up);
            if (facing.sqrMagnitude > 0.001f) c.Facing = facing.normalized;
            c.Motor.MoveTo(target);
            if (Vector3.Distance(transform.position, target) > 0.3f) {Surface = null;supportTransform=null;}
        }
        public void Remember(PootleCharacter c)
        {
            Surface = (c.Mode==PootleMode.Grounded || c.IsRolling && c.IsGrounded) && c.Support.walkable && c.Support.collider
                ? c.Support.collider.GetComponentInParent<PootleMovingSurface>() : null;
            var body=c.IsGrounded && c.Support.walkable && c.Support.collider ? c.Support.collider.attachedRigidbody : null;
            supportTransform=Surface ? Surface.transform : body && !body.isKinematic ? body.transform : null;
            if (!supportTransform) return;
            anchor = supportTransform.InverseTransformPoint(transform.position);
            rotation = supportTransform.rotation;
        }
    }
}
