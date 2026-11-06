using UnityEngine;
namespace Pootle
{
    [DefaultExecutionOrder(-40), AddComponentMenu("Pootle/Playground Swing")]
    public sealed class PootleSwing : MonoBehaviour
    {
        public Transform pivot;
        public PootleRideSeat ride;
        [Min(.5f)] public float ropeLength = 3.6f;
        [Min(0)] public float gravity = 9.81f;
        [Min(0)] public float damping = .16f;
        [Min(0)] public float pumpAcceleration = 1.8f;
        [Range(10, 85)] public float maximumAngle = 65;
        [Min(0)] public float boardingKick = 24;
        public bool simulateAutomatically = true;
        public float AngleDegrees => angle * Mathf.Rad2Deg;
        public float AngularSpeed => velocity * Mathf.Rad2Deg;
        float angle, velocity;
        Quaternion rest;
        bool initialized;
        void Awake() { if (pivot) { rest = pivot.localRotation; initialized = true; } }
        void OnEnable() { if (ride) ride.Boarded += OnBoarded; }
        void OnDisable() { if (ride) { ride.Boarded -= OnBoarded; ride.Abort(); } }
        void OnBoarded(PootleCharacter c)
        {
            float approach = Vector3.Dot(ride.BoardingVelocity, transform.forward);
            velocity += Mathf.Sign(Mathf.Abs(approach) > .1f ? approach : 1) * boardingKick * Mathf.Deg2Rad;
        }
        void Update() { if (simulateAutomatically) Step(Time.deltaTime); }
        public void Step(float dt)
        {
            if (!pivot || !ride || dt <= 0 || !float.IsFinite(dt)) return;
            if (!initialized) { rest = pivot.localRotation; initialized = true; }
            dt = Mathf.Min(dt, .1f); Vector3 before = ride.passenger.seat.position;
            int count = Mathf.Max(1, Mathf.CeilToInt(dt * 120)); float step = dt / count;
            for (int i = 0; i < count; i++)
            {
                float input = ride.Occupied ? ride.PumpInput : 0;
                velocity += (-gravity / Mathf.Max(.5f, ropeLength) * Mathf.Sin(angle) - damping * velocity + input * pumpAcceleration) * step;
                angle += velocity * step;
                float limit = maximumAngle * Mathf.Deg2Rad;
                if (Mathf.Abs(angle) > limit) { angle = Mathf.Clamp(angle, -limit, limit); velocity *= -.2f; }
            }
            pivot.localRotation = rest * Quaternion.AngleAxis(-AngleDegrees, Vector3.right);
            ride.RideVelocity = (ride.passenger.seat.position - before) / dt;
        }
    }
}
