using UnityEditor;
using UnityEngine;

namespace Pootle.Editor
{
    public static class PootleDebugView
    {
        public enum Preset { Essentials, Grounding, Everything }
        public static bool enabled = true, labels = true;
        public static Preset preset;
        public static float markerSize = 0.045f;

        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        private static void Draw(PootleCharacter character, GizmoType type)
        {
            if (!enabled || !Selection.Contains(character.gameObject)) return;
            var motor = character.GetComponent<PootleMotor>();
            if (!motor) return;
            var d = character.DebugSnapshot;
            bool live = Application.isPlaying && d.tick > 0;
            bool stale = live && (!character.captureDiagnostics || !character.isActiveAndEnabled);
            Vector3 position = live ? d.position : character.transform.position;
            Vector3 center = position + (live ? d.center : motor.center);
            float radius = live ? d.radius : motor.Radius;
            float height = live ? d.height : motor.Height;
            Handles.color = (d.frozen || stale) && live ? new Color(0.65f, 0.65f, 0.7f) : Color.cyan;
            Capsule(center - Vector3.up * (height * 0.5f - radius), center + Vector3.up * (height * 0.5f - radius), radius);
            Marker(center, "CapsuleCentre");
            Marker(center - Vector3.up * height * 0.5f, "Feet");
            Marker(center + Vector3.up * height * 0.5f, "Head");
            Handles.DrawWireDisc(center - Vector3.up * (height * 0.5f - (live ? d.step : motor.stepHeight)), Vector3.up, radius);
            if (!live) { Label(center + Vector3.up * 1.5f, "Preview · configuration only (no physics queries)"); return; }
            string frozen = d.frozen || stale ? $"{(d.frozen ? "FROZEN" : "STALE")} · age {Time.realtimeSinceStartup - d.time:F1}s" : "Live";
            Label(center + Vector3.up * 1.5f,
                $"{character.name} · {frozen} · tick {d.tick}\n{d.previousMode} → {d.mode} · last transition: {d.reason}\n" +
                $"Velocity {d.velocity:F2} m/s\nSupport {(d.support.collider ? d.support.collider.name : "none")} · {d.support.angle:F1}° / {d.slopeLimit:F0}° · central {d.support.central}\n" +
                $"Jump buffer {d.bufferRemaining:F3}s · coyote {d.coyoteRemaining:F3}s · accepted {d.jumpAccepted}" +
                (d.overflow ? "\nCAPTURE OVERFLOW" : ""));
            Arrow(position + Vector3.up * 0.15f, d.inputDirection, Color.cyan, "Input direction");
            Arrow(center, d.facing, Color.white, "Facing");
            if (preset == Preset.Everything)
            {
                Arrow(center, d.velocity * 0.15f, Color.yellow, "Velocity · 0.15m per m/s");
                Arrow(position, d.requested, Color.red, "Requested displacement · metres");
                Arrow(position, d.resolved, Color.green, "Resolved displacement · metres");
            }
            for (int i = 0; i < d.count; i++)
            {
                var p = d.probes[i];
                bool ground = p.kind == PootleProbeKind.GroundSphere || p.kind == PootleProbeKind.CentralSupport;
                if (!ground && preset != Preset.Everything) continue;
                Handles.color = p.status == PootleProbeStatus.Accepted ? Color.green :
                    p.status == PootleProbeStatus.Rejected ? Color.red : p.status == PootleProbeStatus.Hit ? new Color(1, 0.7f, 0.2f) : Color.gray;
                if (p.status == PootleProbeStatus.NotEvaluated)
                { Label(center + Vector3.right * 0.7f, p.kind + ": Not evaluated"); continue; }
                if (p.kind == PootleProbeKind.Recovery)
                { Arrow(p.a, p.b - p.a, Color.magenta, "Overlap correction"); continue; }
                Vector3 offset = p.direction * p.distance;
                if (p.radius > 0)
                {
                    Capsule(p.a, p.b, p.radius);
                    Capsule(p.a + offset, p.b + offset, p.radius);
                    Vector3 right = Vector3.Cross(p.direction.sqrMagnitude > 0 ? p.direction : Vector3.up, Vector3.forward).normalized;
                    if (right.sqrMagnitude < 0.1f) right = Vector3.right;
                    Vector3 second = Vector3.Cross(p.direction, right).normalized;
                    foreach (var tangent in new[] { right, -right, second, -second })
                    {
                        Handles.DrawLine(p.a + tangent * p.radius, p.a + offset + tangent * p.radius);
                        Handles.DrawLine(p.b + tangent * p.radius, p.b + offset + tangent * p.radius);
                    }
                }
                else if (p.status == PootleProbeStatus.Miss) Handles.DrawDottedLine(p.a, p.a + offset, 4);
                else Handles.DrawLine(p.a, p.a + offset);
                if (p.collider)
                {
                    Marker(p.hitPoint, p.kind + " · " + p.status);
                    Handles.DrawLine(p.hitPoint, p.hitPoint + p.hitNormal * 0.5f);
                    if (p.status == PootleProbeStatus.Rejected)
                    {
                        Handles.DrawLine(p.hitPoint - Vector3.right * 0.12f, p.hitPoint + Vector3.right * 0.12f);
                        Handles.DrawLine(p.hitPoint - Vector3.forward * 0.12f, p.hitPoint + Vector3.forward * 0.12f);
                        Label(p.hitPoint + Vector3.up * 0.2f, "Rejected: " + p.rejection);
                    }
                }
                if (preset != Preset.Essentials) Label(p.a + offset, $"{p.kind}: {p.status} · {p.distance:F3}m · r={p.radius:F3}m · mask {p.mask} · {(p.kind == PootleProbeKind.Pole ? "Include triggers" : "Ignore triggers")}");
            }
        }
        private static void Marker(Vector3 point, string name)
        { Handles.SphereHandleCap(0, point, Quaternion.identity, markerSize * 2, EventType.Repaint); Label(point + Vector3.right * markerSize, name); }
        private static void Label(Vector3 point, string text) { if (labels) Handles.Label(point, text); }
        private static void Arrow(Vector3 origin, Vector3 delta, Color color, string text)
        {
            if (delta.sqrMagnitude < 0.000001f) return;
            Handles.color = color; Handles.DrawLine(origin, origin + delta);
            Handles.ConeHandleCap(0, origin + delta, Quaternion.LookRotation(delta), markerSize * 2, EventType.Repaint);
            Label(origin + delta, text);
        }
        private static void Capsule(Vector3 a, Vector3 b, float radius)
        {
            Handles.DrawWireDisc(a, Vector3.up, radius); Handles.DrawWireDisc(b, Vector3.up, radius);
            Handles.DrawWireArc(a, Vector3.right, Vector3.forward, 180, radius);
            Handles.DrawWireArc(a, Vector3.forward, Vector3.left, 180, radius);
            Handles.DrawWireArc(b, Vector3.right, Vector3.back, 180, radius);
            Handles.DrawWireArc(b, Vector3.forward, Vector3.right, 180, radius);
            Handles.DrawLine(a + Vector3.right * radius, b + Vector3.right * radius);
            Handles.DrawLine(a - Vector3.right * radius, b - Vector3.right * radius);
            Handles.DrawLine(a + Vector3.forward * radius, b + Vector3.forward * radius);
            Handles.DrawLine(a - Vector3.forward * radius, b - Vector3.forward * radius);
        }
    }
}
