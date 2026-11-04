using UnityEngine;

namespace Pootle
{
    public enum PootleMode { Grounded, Airborne, Sliding, Climbing, LedgeHanging, LedgeHop, PoleClimbing, RailWalking, RailSliding, Dangling, Gliding }
    public enum PootleTransition { None, Supported, WalkedOffEdge, Jumped, AirJumped, Landed, Teleported, Disabled, SlideStarted, SlideStopped, Grabbed, Released, TopOut }
    public enum PootleProbeKind { GroundSphere, CentralSupport, Movement, StepUp, StepForward, StepDown, Overlap, Recovery, Wall, Ledge, Clearance, Pole }
    public enum PootleProbeStatus { NotEvaluated, Miss, Hit, Accepted, Rejected }
    public enum PootleProbeRejection { None, SlopeLimit, NoCentralSupport, OutsideSupportReach }

    public struct PootleContact
    {
        public Collider collider;
        public Vector3 point, normal;
        public bool ceiling, wall;
    }

    public struct PootleSupport
    {
        public bool found, walkable, central;
        public Collider collider;
        public Vector3 point, normal;
        public float distance, angle;
    }

    public struct PootleMotionResult
    {
        public Vector3 requested, displacement;
        public bool hitCeiling, stepped;
        public int contactCount;
    }

    public struct PootleInputFrame
    {
        public Vector2 move;
        public bool run, grip, glide, jumpPressed, jumpHeld, resetPressed;
    }

    public static class PootleMovement
    {
        // Extracted from Entity.Accelerate: forward gain and lateral drag remain separate.
        public static Vector3 Accelerate(Vector3 velocity, Vector3 direction, float magnitude,
            float acceleration, float drag, float topSpeed, float minimumSpeed, float dt)
        {
            if (direction.sqrMagnitude < 0.000001f) return velocity;
            float speed = Vector3.Dot(direction, velocity);
            Vector3 lateral = velocity - direction * speed;
            float target = Mathf.Max(minimumSpeed, topSpeed * Mathf.Clamp01(magnitude));
            if (speed < target) speed = Mathf.Clamp(speed + acceleration * dt, -target, target);
            return direction * speed + Vector3.MoveTowards(lateral, Vector3.zero, drag * dt);
        }

        public static Vector3 CameraDirection(Vector2 input, Transform camera, out float magnitude)
        {
            input = Vector2.ClampMagnitude(input, 1);
            magnitude = input.magnitude;
            if (magnitude < 0.00001f) return Vector3.zero;
            Vector3 forward = camera ? Vector3.ProjectOnPlane(camera.forward, Vector3.up) : Vector3.forward;
            if (forward.sqrMagnitude < 0.0001f)
                forward = camera ? Vector3.ProjectOnPlane(camera.up, Vector3.up) : Vector3.forward;
            forward.Normalize();
            return (forward * input.y + Vector3.Cross(Vector3.up, forward) * input.x).normalized;
        }
    }

    public struct PootleJumpBuffer
    {
        private float expires;
        private bool pending;
        public void Press(float now, float duration) { pending = true; expires = now + duration; }
        public bool Available(float now) => pending && now <= expires;
        public float Remaining(float now) => pending ? Mathf.Max(0, expires - now) : 0;
        public bool Consume(float now)
        {
            if (!Available(now)) return false;
            pending = false;
            return true;
        }
        public void Clear() { pending = false; expires = 0; }
    }
}
