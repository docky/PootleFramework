using System.Collections.Generic;
using UnityEngine;

namespace Pootle
{
    // Optional continuous geometry supplied by the Splines integration. Runtime has no package dependency.
    public interface IPootleRailCurve
    {
        float Length(int segment);
        Vector3 Position(int segment, float along);
        Vector3 Tangent(int segment, float along);
        Vector3 Closest(int segment, Vector3 point, out float along);
    }

    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Pootle/One Way Rail")]
    public sealed class PootleRail : MonoBehaviour
    {
        public static readonly HashSet<PootleRail> Active = new HashSet<PootleRail>();
        [Tooltip("Local-space walking surface points, in order. Use trigger colliders or no colliders for the visual.")]
        public Vector3[] points = { Vector3.zero, Vector3.forward * 8 };
        [Min(0.02f)] public float landingHalfWidth = 0.22f;
        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);
        public IPootleRailCurve Curve { get; set; }
        public bool IsContinuous => Curve != null;
        public int SegmentCount => points == null ? 0 : Mathf.Max(0, points.Length - 1);
        public Vector3 Point(int index) => transform.TransformPoint(points[index]);
        public float Length(int segment) => Curve != null ? Curve.Length(segment) : Vector3.Distance(Point(segment), Point(segment + 1));
        public Vector3 Tangent(int segment) => Tangent(segment, 0);
        public Vector3 Tangent(int segment, float along) => Curve != null ? Curve.Tangent(segment, along) : (Point(segment + 1) - Point(segment)).normalized;
        public Vector3 Position(int segment, float along) => Curve != null ? Curve.Position(segment, along) : Point(segment) + Tangent(segment) * along;

        public Vector3 Closest(int segment, Vector3 point, out float along)
        {
            if (Curve != null) return Curve.Closest(segment, point, out along);
            Vector3 a=Point(segment), tangent=Tangent(segment);
            along=Mathf.Clamp(Vector3.Dot(point-a,tangent),0,Length(segment));
            return a+tangent*along;
        }
        // Cross the top from above during this displacement. Upward travel cannot attach.
        public bool Landing(Vector3 from, Vector3 to, out int segment, out float along, out float time, float extraWidth = 0)
        {
            segment = -1; along = 0; time = float.PositiveInfinity;
            if (to.y > from.y + 0.0001f) return false;
            float width = landingHalfWidth + Mathf.Max(0, extraWidth);
            for (int i = 0; i < SegmentCount; i++)
            {
                Vector3 a = Point(i), delta = Point(i + 1) - a;
                Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (flat.sqrMagnitude < 0.001f) continue;
                float u0 = Vector3.Dot(from - a, flat) / flat.sqrMagnitude;
                float u1 = Vector3.Dot(to - a, flat) / flat.sqrMagnitude;
                float g0 = from.y - (a.y + delta.y * u0);
                float g1 = to.y - (a.y + delta.y * u1);
                if (g0 < -0.015f || g1 > 0.015f || g1 >= g0) continue;
                float t = Mathf.Clamp01(g0 / (g0 - g1));
                float u = Mathf.Lerp(u0, u1, t);
                if (t >= time) continue;
                u = Mathf.Clamp01(u);
                Vector3 crossing = Vector3.Lerp(from, to, t), surface = a + delta * u;
                if (Vector3.ProjectOnPlane(crossing - surface, Vector3.up).magnitude > width || Mathf.Abs(crossing.y - surface.y) > 0.08f) continue;
                segment = i; Closest(i, surface, out along); time = t;
            }
            return segment >= 0;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < SegmentCount; i++) { Gizmos.DrawLine(Point(i), Point(i + 1)); Gizmos.DrawWireSphere(Point(i), landingHalfWidth); }
        }
    }
}
