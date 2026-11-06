using System.Collections.Generic;
using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider)), AddComponentMenu("Pootle/Experimental/Path Input Zone")]
    public sealed class PootlePathInputZone : MonoBehaviour
    {
        [Tooltip("Assist keyboard, D-pad and digital sticks only. Analog sticks remain unassisted unless this is disabled.")]
        public bool digitalOnly = true;
        public bool groundedOnly = true;
        [Range(0, 45), Tooltip("Maximum angle from either direction along the blue local Z axis that is aligned to the path.")]
        public float angleTolerance = 12;
        [Tooltip("Higher priority wins when volumes overlap; equal priorities use the closest direction.")]
        public int priority;
        static readonly List<PootlePathInputZone> active = new List<PootlePathInputZone>();
        BoxCollider volume;
        public BoxCollider Volume => volume ? volume : (volume = GetComponent<BoxCollider>());
        public Vector3 PathDirection => Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() => active.Remove(this);
        void Reset() { Volume.isTrigger = true; Volume.size = new Vector3(4, 3, 12); Volume.center = Vector3.up; }

        public bool TryAlign(Vector3 feet, Vector3 direction, bool digital, bool grounded, out Vector3 aligned)
        {
            aligned = direction;
            if (!isActiveAndEnabled || !Volume.enabled || !Volume.isTrigger || digitalOnly && !digital || groundedOnly && !grounded) return false;
            Vector3 local = transform.InverseTransformPoint(feet) - Volume.center, half = Volume.size * .5f;
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) return false;
            Vector3 axis = PathDirection;
            if (axis.sqrMagnitude < .001f || direction.sqrMagnitude < .00001f) return false;
            if (Vector3.Dot(axis, direction) < 0) axis = -axis;
            if (Vector3.Angle(direction, axis) > Mathf.Clamp(angleTolerance, 0, 45) + .0001f) return false;
            aligned = axis * direction.magnitude;
            return true;
        }

        public static Vector3 Align(Vector3 feet, Vector3 direction, bool digital, bool grounded)
        {
            Vector3 result = direction;
            int bestPriority = int.MinValue;
            float bestAngle = float.PositiveInfinity;
            foreach (var zone in active)
            {
                if (!zone || !zone.TryAlign(feet, direction, digital, grounded, out var candidate)) continue;
                float angle = Vector3.Angle(direction, candidate);
                if (zone.priority < bestPriority || zone.priority == bestPriority &&
                    angle >= bestAngle) continue;
                bestPriority = zone.priority; bestAngle = angle; result = candidate;
            }
            return result;
        }

        void OnDrawGizmosSelected()
        {
            if (!Volume) return;
            Gizmos.color = new Color(.2f, .9f, .8f, .6f);
            Gizmos.matrix = transform.localToWorldMatrix; Gizmos.DrawWireCube(Volume.center, Volume.size);
            Gizmos.matrix = Matrix4x4.identity;
            Vector3 centre = transform.TransformPoint(Volume.center);
            foreach (int sign in new[] { -1, 1 })
            {
                var axis = PathDirection * sign * 3;
                Gizmos.DrawRay(centre, axis);
                Gizmos.DrawRay(centre, Quaternion.AngleAxis(angleTolerance, Vector3.up) * axis);
                Gizmos.DrawRay(centre, Quaternion.AngleAxis(-angleTolerance, Vector3.up) * axis);
            }
        }
    }
}
