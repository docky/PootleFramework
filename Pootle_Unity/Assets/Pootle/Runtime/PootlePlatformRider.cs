using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent]
    public sealed class PootlePlatformRider : MonoBehaviour
    {
        public PootleMovingSurface Surface { get; private set; }
        public Vector3 CarryVelocity { get; private set; }
        private Vector3 anchor;
        private Quaternion rotation;
        public void ClearRiding() { Surface = null; CarryVelocity = Vector3.zero; }
        public void Apply(PootleCharacter c, float dt)
        {
            CarryVelocity = Vector3.zero;
            if (!Surface || !Surface.isActiveAndEnabled || c.Mode != PootleMode.Grounded) { Surface = null; return; }
            Vector3 target = Surface.transform.TransformPoint(anchor);
            Vector3 delta = target - transform.position;
            CarryVelocity = Vector3.ClampMagnitude(delta / dt, 30);
            var change = Surface.transform.rotation * Quaternion.Inverse(rotation);
            Vector3 facing = Vector3.ProjectOnPlane(change * c.Facing, Vector3.up);
            if (facing.sqrMagnitude > 0.001f) c.Facing = facing.normalized;
            c.Motor.MoveTo(target);
            if (Vector3.Distance(transform.position, target) > 0.3f) Surface = null;
        }
        public void Remember(PootleCharacter c)
        {
            Surface = c.Mode == PootleMode.Grounded && c.Support.walkable && c.Support.collider
                ? c.Support.collider.GetComponentInParent<PootleMovingSurface>() : null;
            if (!Surface) return;
            anchor = Surface.transform.InverseTransformPoint(transform.position);
            rotation = Surface.transform.rotation;
        }
    }
}
