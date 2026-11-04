using UnityEngine;

namespace Pootle
{
    // Optional traversal authoring is carried by surface markers. All motion still goes through the capsule motor.
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Traversal")]
    public sealed class PootleTraversal : MonoBehaviour
    {
        private readonly Collider[] nearby = new Collider[24];
        private Collider wall;
        private Vector3 normal, ledgeDestination, hopStart, poleOffset;
        private PootlePole pole;
        private float cooldown, stateTime;
        public Collider AttachedWall => wall;
        public PootlePole AttachedPole => pole;
        public Vector3 SurfaceNormal => normal;
        public void ResetTraversal() { wall = null; pole = null; cooldown = stateTime = 0; }

        public bool Tick(PootleCharacter c, float dt, PootleInputFrame frame, out PootleMotionResult result)
        {
            result = default;
            cooldown = Mathf.Max(0, cooldown - dt);
            var p = c.profile;
            if (c.Mode == PootleMode.LedgeHop)
            {
                stateTime += dt;
                float t = Mathf.Clamp01(stateTime / Mathf.Max(0.15f, p.topOutDuration));
                // Smooth lift and transfer phases meet with zero velocity at the lip.
                float liftPhase = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.45f));
                float across = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.45f, 1, t));
                Vector3 target = Vector3.Lerp(hopStart, ledgeDestination, across);
                target.y = t < 0.45f ? Mathf.Lerp(hopStart.y, ledgeDestination.y + p.topOutHopHeight, liftPhase)
                    : Mathf.Lerp(ledgeDestination.y + p.topOutHopHeight, ledgeDestination.y, across);
                result = c.Motor.Move(target - transform.position, false);
                c.Velocity = result.displacement / dt;
                if (t >= 1) { c.Velocity = Vector3.zero; Release(c); }
                return true;
            }
            if (c.Mode == PootleMode.PoleClimbing)
            {
                if (!pole || !frame.grip) { Release(c); return false; }
                if (c.ConsumeJump()) { Jump(c, frame.jumpHeld); return false; }
                // Turning changes facing only. The attachment offset never forces facing away from the pole.
                c.Facing = Quaternion.AngleAxis(frame.move.x * p.poleTurnSpeed * dt, Vector3.up) * c.Facing;
                float h = Mathf.Clamp(Vector3.Dot(transform.position + c.Motor.center - pole.Bottom, pole.transform.up)
                    + frame.move.y * p.climbSpeed * dt, c.Motor.Height * 0.5f, pole.length);
                Vector3 desiredOffset = -Vector3.ProjectOnPlane(c.Facing, pole.transform.up).normalized * (pole.radius + p.poleBodyDistance);
                poleOffset = Vector3.MoveTowards(poleOffset, desiredOffset, p.poleAttachmentSpeed * dt);
                Vector3 target = pole.Bottom + pole.transform.up * h + poleOffset - c.Motor.center;
                result = c.Motor.Move(target - transform.position, false);
                c.Velocity = result.displacement / dt;
                return true;
            }
            if (c.Mode == PootleMode.LedgeHanging)
            {
                if (!wall || frame.move.y < -0.5f) { Release(c); return false; }
                if (c.ConsumeJump()) { Jump(c, frame.jumpHeld); return false; }
                c.Velocity = Vector3.zero;
                stateTime += dt;
                if (stateTime > 0.12f && c.Motor.CanOccupy(ledgeDestination)) StartHop(c);
                return true;
            }
            if (c.Mode == PootleMode.Climbing)
            {
                if (!wall || !frame.grip) { Release(c); return false; }
                if (c.ConsumeJump()) { Jump(c, frame.jumpHeld); return false; }
                if (FindTop(c, normal, 0.65f, out var destination) && frame.move.y > 0.1f)
                { ledgeDestination = destination; StartHop(c); return true; }
                Vector3 inward = -normal;
                if (!WallRay(c, inward, out var hit) || !hit.collider.GetComponentInParent<PootleClimbSurface>())
                { Release(c); return false; }
                wall = hit.collider; normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                Vector3 side = Vector3.Cross(normal, Vector3.up);
                Vector3 delta = Vector3.up * frame.move.y * p.climbSpeed * dt + side * frame.move.x * p.climbSidewaysSpeed * dt;
                result = c.Motor.Move(delta, false);
                // Follow curved walls with a second radial observation after the tangential step.
                if (WallRay(c, -normal, out var curved) && curved.collider == wall)
                {
                    normal = Vector3.ProjectOnPlane(curved.normal, Vector3.up).normalized;
                    float correction = curved.distance - (c.Motor.Radius + c.Motor.Skin * 2);
                    var adjust = c.Motor.Move(-normal * Mathf.Clamp(correction, -0.15f, 0.15f), false);
                    result.displacement += adjust.displacement;
                }
                c.Facing = -normal; c.Velocity = result.displacement / dt;
                if (frame.move.y < 0 && c.Support.walkable) Release(c);
                return true;
            }
            if (cooldown > 0 || c.Mode == PootleMode.Sliding) return false;
            if (frame.grip)
            {
                int count = c.Motor.FindGripPoles(nearby, p.climbReach);
                PootlePole closest = null; float best = float.PositiveInfinity;
                Vector3 center = transform.position + c.Motor.center;
                for (int i = 0; i < count; i++)
                {
                    var candidate = nearby[i].GetComponentInParent<PootlePole>();
                    if (!candidate) continue;
                    float h = Mathf.Clamp(Vector3.Dot(center - candidate.Bottom, candidate.transform.up), 0, candidate.length);
                    float distance = (center - (candidate.Bottom + candidate.transform.up * h)).sqrMagnitude;
                    if (distance < best) { closest = candidate; best = distance; }
                }
                if (closest)
                {
                    pole = closest;
                    float h = Mathf.Clamp(Vector3.Dot(center - pole.Bottom, pole.transform.up), 0, pole.length);
                    poleOffset = Vector3.ProjectOnPlane(center - (pole.Bottom + pole.transform.up * h), pole.transform.up);
                    if (poleOffset.sqrMagnitude < 0.001f) poleOffset = -c.Facing * (pole.radius + c.Motor.Radius);
                    c.Velocity = Vector3.zero; c.SetMode(PootleMode.PoleClimbing, PootleTransition.Grabbed);
                    return true;
                }
            }
            Vector3 approach = PootleMovement.CameraDirection(frame.move, c.outputCamera, out float magnitude);
            if (magnitude < 0.1f) approach = c.Facing;
            if (WallRay(c, approach, out var front))
            {
                normal = Vector3.ProjectOnPlane(front.normal, Vector3.up).normalized;
                if (frame.grip && front.collider.GetComponentInParent<PootleClimbSurface>())
                {
                    wall = front.collider; c.Velocity = Vector3.zero; c.Facing = -normal;
                    c.SetMode(PootleMode.Climbing, PootleTransition.Grabbed); return true;
                }
                if (!c.IsGrounded && c.Velocity.y <= 0 && FindTop(c, normal, p.ledgeHandHeight + 0.25f, out var top)
                    && top.y - transform.position.y >= p.ledgeHandHeight - p.ledgeCatchReach)
                {
                    Vector3 hang = transform.position; hang.y = top.y - p.ledgeHandHeight;
                    if (c.Motor.CanOccupy(hang))
                    {
                        wall = front.collider; ledgeDestination = top; stateTime = 0;
                        result = c.Motor.Move(hang - transform.position, false);
                        c.Velocity = Vector3.zero; c.Facing = -normal;
                        c.SetMode(PootleMode.LedgeHanging, PootleTransition.Grabbed); return true;
                    }
                }
            }
            return false;
        }

        private bool WallRay(PootleCharacter c, Vector3 direction, out RaycastHit hit)
        {
            float reach = c.Motor.Radius + c.profile.climbReach;
            if (c.Motor.Ray(transform.position + Vector3.up * 1.1f, direction, reach, PootleProbeKind.Wall, out hit)
                && Mathf.Abs(hit.normal.y) < 0.35f) return true;
            return c.Motor.Ray(transform.position + Vector3.up * 0.3f, direction, reach, PootleProbeKind.Wall, out hit)
                && Mathf.Abs(hit.normal.y) < 0.35f;
        }

        private bool FindTop(PootleCharacter c, Vector3 outward, float maxRise, out Vector3 destination)
        {
            destination = default;
            float inset = c.Motor.Radius * 2 + c.Motor.Skin * 6;
            Vector3 above = transform.position - outward * inset + Vector3.up * (maxRise + 0.15f);
            if (!c.Motor.Ray(above, Vector3.down, maxRise + 0.3f, PootleProbeKind.Ledge, out var top)
                || Vector3.Angle(top.normal, Vector3.up) > c.Motor.slopeLimit) return false;
            float rise = top.point.y - transform.position.y;
            if (rise < 0.05f || rise > maxRise) return false;
            destination = top.point + Vector3.up * c.Motor.Skin * 3;
            return true;
        }
        private void StartHop(PootleCharacter c)
        {
            if (!c.Motor.CanOccupy(ledgeDestination))
            {
                stateTime = 0; c.SetMode(PootleMode.LedgeHanging, PootleTransition.Grabbed); return;
            }
            hopStart = transform.position; stateTime = 0; c.Velocity = Vector3.zero;
            c.SetMode(PootleMode.LedgeHop, PootleTransition.TopOut);
        }
        private void Jump(PootleCharacter c, bool held)
        {
            Vector3 direction = c.Facing;
            Release(c);
            c.LaunchTraversal(direction * c.profile.climbJumpSpeed, held);
        }
        private void Release(PootleCharacter c)
        {
            wall = null; pole = null; stateTime = 0; cooldown = c.profile.regrabDelay;
            c.SetMode(PootleMode.Airborne, PootleTransition.Released);
        }
    }
}
