using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Rail Rider")]
    public sealed class PootleRailRider : MonoBehaviour
    {
        public PootleRail Rail { get; private set; }
        public float Speed { get; private set; }
        private int segment;
        private float along, cooldown, facingAngle;
        private bool sliding;
        public void ResetRide() { Rail = null; Speed = cooldown = 0; sliding = false; }
        private static Vector3 Flat(Vector3 v) => Vector3.ProjectOnPlane(v, Vector3.up).normalized;
        private void SnapFacing(PootleCharacter c, Vector3 tangent)
        {
            facingAngle = Mathf.Round(Vector3.SignedAngle(Flat(tangent), c.Facing, Vector3.up) / 90) * 90;
            c.Facing = Quaternion.AngleAxis(facingAngle, Vector3.up) * Flat(tangent);
        }
        public bool Catch(PootleCharacter c, Vector3 from, Vector3 to)
        {
            if (Rail || cooldown > 0 || c.Velocity.y > 0 || c.Mode != PootleMode.Airborne && c.Mode != PootleMode.Sliding) return false;
            float best = float.PositiveInfinity, bestAlong = 0; int bestSegment = 0; PootleRail bestRail = null;
            foreach (var rail in PootleRail.Active)
                if (rail && rail.Landing(from, to, out int s, out float d, out float t, c.Motor.Radius * c.profile.railLandingRadiusFraction) && t < best)
                { best = t; bestRail = rail; bestSegment = s; bestAlong = d; }
            if (!bestRail) return false;
            return Attach(c, bestRail, bestSegment, bestAlong);
        }
        private bool Attach(PootleCharacter c, PootleRail bestRail, int bestSegment, float bestAlong)
        {
            Vector3 tangent = bestRail.Tangent(bestSegment);
            Vector3 target = bestRail.Point(bestSegment) + tangent * bestAlong;
            if (!c.Motor.CanOccupy(target)) return false;
            var correction = c.Motor.Move(target - transform.position, false);
            if (Vector3.Distance(transform.position, target) > 0.04f) return false;
            Rail = bestRail; segment = bestSegment; along = bestAlong;
            Speed = Vector3.Dot(c.Velocity, tangent);
            sliding = c.Mode == PootleMode.Sliding || Mathf.Abs(tangent.y) > Mathf.Sin(c.Motor.slopeLimit * Mathf.Deg2Rad);
            SnapFacing(c, tangent);
            c.RailSupported();
            c.Velocity = tangent * Speed;
            c.SetMode(sliding ? PootleMode.RailSliding : PootleMode.RailWalking, PootleTransition.Grabbed);
            return true;
        }
        public bool Tick(PootleCharacter c, float dt, PootleInputFrame frame, out PootleMotionResult result)
        {
            result = default; cooldown = Mathf.Max(0, cooldown - dt);
            bool wasRiding = c.Mode == PootleMode.RailWalking || c.Mode == PootleMode.RailSliding;
            if (!Rail || !Rail.isActiveAndEnabled)
            {
                if (wasRiding) Detach(c);
                if (!wasRiding && cooldown <= 0 && c.IsGrounded && !frame.jumpPressed && TryWalkOn(c, frame))
                    return true;
                return false;
            }
            if (segment >= Rail.SegmentCount || Rail.Length(segment) < 0.001f) { Detach(c); return false; }
            c.RailSupported();
            var p = c.profile;
            Vector3 tangent = Rail.Tangent(segment);
            Vector3 input = PootleMovement.CameraDirection(frame.move, c.outputCamera, out float magnitude);
            if (c.ConsumeJump())
            {
                Vector3 direction = magnitude > 0.1f ? input : c.Facing;
                Vector3 launch = direction * p.walkSpeed + Vector3.ProjectOnPlane(c.Velocity, Vector3.up) * 0.35f;
                Detach(c); c.LaunchTraversal(launch, frame.jumpHeld); return false;
            }
            bool steep = Mathf.Abs(tangent.y) > Mathf.Sin(c.Motor.slopeLimit * Mathf.Deg2Rad);
            if (steep && !sliding) { sliding = true; SnapFacing(c, tangent); }
            float oldSpeed = Speed;
            if (sliding)
            {
                Speed += -tangent.y * p.slideGravity * dt;
                Speed = Mathf.MoveTowards(Speed, 0, (steep ? p.slideSlopeFriction : p.slideFlatDeceleration) * dt);
                Speed = Mathf.Clamp(Speed, -p.slideMaximumSpeed, p.slideMaximumSpeed);
                if (!steep && Mathf.Abs(Speed) < p.slideStopSpeed) { Speed = 0; sliding = false; }
            }
            else
            {
                float intent = Vector3.Dot(input, Flat(tangent)) * magnitude;
                float target = Mathf.Abs(intent) > 0.15f ? intent * (frame.run && p.allowRun ? p.runSpeed : p.walkSpeed) : 0;
                Speed = Mathf.MoveTowards(Speed, target, (target == 0 ? p.releaseDeceleration : p.groundAcceleration) * dt);
                if (Mathf.Abs(Speed) > 0.1f) facingAngle = Speed > 0 ? 0 : 180;
            }
            float next = along + (oldSpeed + Speed) * 0.5f * dt;
            int nextSegment = segment;
            while (next > Rail.Length(nextSegment) && nextSegment < Rail.SegmentCount - 1) { next -= Rail.Length(nextSegment); nextSegment++; }
            while (next < 0 && nextSegment > 0) { nextSegment--; next += Rail.Length(nextSegment); }
            bool offEnd = next < 0 || next > Rail.Length(nextSegment);
            Vector3 newTangent = Rail.Tangent(nextSegment);
            Vector3 destination = Rail.Point(nextSegment) + newTangent * next;
            result = c.Motor.Move(destination - transform.position, false);
            if (Vector3.Distance(transform.position, destination) > 0.05f) { Speed = 0; Detach(c); return true; }
            segment = nextSegment; along = next;
            c.Facing = Quaternion.AngleAxis(facingAngle, Vector3.up) * Flat(newTangent);
            c.SeatedNormal = Vector3.ProjectOnPlane(Vector3.up, newTangent).normalized;
            c.Velocity = newTangent * Speed;
            c.SetMode(sliding ? PootleMode.RailSliding : PootleMode.RailWalking, PootleTransition.Supported);
            if (offEnd) Detach(c);
            return true;
        }
        private bool TryWalkOn(PootleCharacter c, PootleInputFrame frame)
        {
            Vector3 input = PootleMovement.CameraDirection(frame.move, c.outputCamera, out float magnitude);
            if (magnitude < 0.1f) return false;
            PootleRail nearest = null; int chosen = 0; float distance = float.PositiveInfinity, chosenAlong = 0;
            foreach (var rail in PootleRail.Active)
            {
                if (!rail || rail.SegmentCount == 0) continue;
                for (int end = 0; end < 2; end++)
                {
                    int s = end == 0 ? 0 : rail.SegmentCount - 1;
                    Vector3 point = rail.Point(end == 0 ? 0 : rail.SegmentCount);
                    Vector3 inward = rail.Tangent(s) * (end == 0 ? 1 : -1);
                    Vector3 delta = point - transform.position;
                    float planar = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
                    if (planar > c.Motor.Radius + 0.15f || planar >= distance || Mathf.Abs(delta.y) > c.Motor.stepHeight + 0.06f
                        || Vector3.Dot(input, Flat(inward)) < 0.35f || Vector3.Dot(delta, input) < -0.15f) continue;
                    nearest = rail; chosen = s; chosenAlong = end == 0 ? 0 : rail.Length(s); distance = planar;
                }
            }
            return nearest && Attach(c, nearest, chosen, chosenAlong);
        }

        private void Detach(PootleCharacter c)
        {
            Rail = null; cooldown = 0.15f;
            c.SetMode(sliding ? PootleMode.Sliding : PootleMode.Airborne, PootleTransition.Released);
            sliding = false;
        }
    }
}
