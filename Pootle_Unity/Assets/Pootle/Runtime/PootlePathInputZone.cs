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
        [Min(0), Tooltip("Extra degrees allowed before an already aligned input releases the path.")]
        public float angleHysteresis = 3;
        [Min(0), Tooltip("Extra world-space metres outside the box before an active alignment releases.")]
        public float boundaryHysteresis = .3f;
        [Min(0), Tooltip("Smoothing time for steering into and out of alignment. Zero responds immediately.")]
        public float blendTime = .18f;
        [Tooltip("Higher priority wins overlaps. Equal priorities choose the closest path on entry, then keep the current latch.")]
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
            return TryAlign(feet, direction, digital, grounded, false, out aligned);
        }

        internal bool TryAlign(Vector3 feet, Vector3 direction, bool digital, bool grounded, bool latched, out Vector3 aligned)
        {
            aligned = direction;
            if (!isActiveAndEnabled || !Volume.enabled || !Volume.isTrigger || digitalOnly && !digital || groundedOnly && !grounded) return false;
            Vector3 local = transform.InverseTransformPoint(feet) - Volume.center, half = Volume.size * .5f;
            if (latched)
            {
                var scale = transform.lossyScale;
                float margin = Mathf.Max(0, boundaryHysteresis);
                half += new Vector3(margin / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                    margin / Mathf.Max(.0001f, Mathf.Abs(scale.y)), margin / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            }
            if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.y) > half.y || Mathf.Abs(local.z) > half.z) return false;
            Vector3 axis = PathDirection;
            if (axis.sqrMagnitude < .001f || direction.sqrMagnitude < .00001f) return false;
            if (Vector3.Dot(axis, direction) < 0) axis = -axis;
            if (Vector3.Angle(direction, axis) > Mathf.Min(89, Mathf.Clamp(angleTolerance, 0, 45) + (latched ? Mathf.Max(0, angleHysteresis) : 0)) + .0001f) return false;
            aligned = axis * direction.magnitude;
            return true;
        }

        public static Vector3 Align(Vector3 feet, Vector3 direction, bool digital, bool grounded)
        {
            FindBest(feet, direction, digital, grounded, null, out var result);
            return result;
        }

        internal static PootlePathInputZone FindBest(Vector3 feet, Vector3 direction, bool digital, bool grounded,
            PootlePathInputZone current, out Vector3 result)
        {
            result = direction;
            PootlePathInputZone best = null;
            float bestAngle = float.PositiveInfinity;
            if (current && current.TryAlign(feet, direction, digital, grounded, true, out var retained))
            {
                best = current; result = retained; bestAngle = Vector3.Angle(direction, retained);
            }
            foreach (var zone in active)
            {
                if (!zone || zone == current && best == current ||
                    !zone.TryAlign(feet, direction, digital, grounded, out var candidate)) continue;
                float angle = Vector3.Angle(direction, candidate);
                // Keep a valid latch across equal-priority overlaps instead of oscillating between paths.
                if (best && (zone.priority < best.priority || zone.priority == best.priority &&
                    (best == current || angle >= bestAngle))) continue;
                best = zone; bestAngle = angle; result = candidate;
            }
            return best;
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
