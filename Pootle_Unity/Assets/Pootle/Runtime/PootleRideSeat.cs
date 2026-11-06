using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle
{
    // Shared ownership and controls for touch-to-seat rides. Motion is supplied by the ride.
    [DefaultExecutionOrder(-50), RequireComponent(typeof(BoxCollider), typeof(PootleMachinePassenger))]
    [AddComponentMenu("Pootle/Automatic Ride Seat")]
    public sealed class PootleRideSeat : MonoBehaviour
    {
        public PootleMachinePassenger passenger;
        [Tooltip("Swings require walking into the seat; lift seats catch on contact, including in the air.")]
        public bool requirePush = true;
        public bool groundedOnly = true;
        [Min(0)] public float pushSeconds = .12f;
        [Min(.05f)] public float boardingSeconds = .4f;
        [Min(0)] public float reboardDelay = .9f;
        [Min(0)] public float jumpSpeed = 7;
        [Min(0)] public float releaseForwardSpeed = 1.5f;
        public bool simulateAutomatically = true;
        public bool Occupied => passenger && passenger.Occupied;
        public float PumpInput { get; private set; }
        public Vector3 RideVelocity { get; set; }
        public Vector3 BoardingVelocity { get; private set; }
        public bool ReleaseBlocked { get; private set; }
        public event Action<PootleCharacter> Boarded;
        public event Action<PootleCharacter> Released;
        static readonly List<PootleRideSeat> active = new List<PootleRideSeat>();
        static readonly Dictionary<PootleCharacter, float> reboardAfter = new Dictionary<PootleCharacter, float>();
        readonly Dictionary<PootleCharacter, float> pressure = new Dictionary<PootleCharacter, float>();
        BoxCollider volume;
        InputActionAsset controls;
        InputAction move, jump, reset;
        PootleCharacter lastReleased;
        bool mustLeave;
        float boardTime;
        Vector3 entry;
        Quaternion entryRotation;

        void Awake() { Resolve(); volume.isTrigger = true; }
        void Resolve() { if (!passenger) passenger = GetComponent<PootleMachinePassenger>(); if (!volume) volume = GetComponent<BoxCollider>(); }
        void OnEnable() { Resolve(); if (!active.Contains(this)) active.Add(this); }
        void OnDisable()
        {
            active.Remove(this); Abort(); pressure.Clear();
            if (active.Count == 0) reboardAfter.Clear();
        }
        void Reset() { GetComponent<BoxCollider>().isTrigger = true; passenger = GetComponent<PootleMachinePassenger>(); }
        bool Contains(PootleCharacter c, float extra = 0)
        {
            Vector3 point = transform.InverseTransformPoint(c.transform.position + c.Motor.center) - volume.center;
            Vector3 half = volume.size * .5f + Vector3.one * extra;
            return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y && Mathf.Abs(point.z) <= half.z;
        }
        public static bool Process(PootleCharacter c, PootleInputFrame frame, float dt)
        {
            for (int i = active.Count - 1; i >= 0; i--) if (active[i].TryBoard(c, frame, dt)) return true;
            return false;
        }
        public bool TryBoard(PootleCharacter c, PootleInputFrame frame, float dt)
        {
            Resolve();
            if (!isActiveAndEnabled || !c || !c.isActiveAndEnabled || !c.Motor || !passenger || !passenger.seat || !volume.enabled) return false;
            if (lastReleased == c && mustLeave)
            {
                if (!Contains(c, .25f)) mustLeave = false;
                else return false;
            }
            if (Occupied || frame.jumpPressed || frame.resetPressed || c.IsRolling || !c.CanGrab || groundedOnly && !c.IsGrounded ||
                reboardAfter.TryGetValue(c, out float until) && Time.time < until || !Contains(c))
            { pressure.Remove(c); return false; }
            if (requirePush)
            {
                Vector3 direction = PootleMovement.CameraDirection(frame.move, c.outputCamera, out float magnitude);
                Vector3 toward = Vector3.ProjectOnPlane(passenger.seat.position - c.transform.position, Vector3.up).normalized;
                if (magnitude < .3f || Vector3.Dot(direction, toward) < .35f) { pressure.Remove(c); return false; }
                pressure.TryGetValue(c, out float held); held += Mathf.Max(0, dt); pressure[c] = held;
                if (held < pushSeconds) return false;
            }
            entry = c.transform.position; entryRotation = c.transform.rotation; BoardingVelocity = c.Velocity;
            if (c.input && c.input.actions)
            {
                controls = Instantiate(c.input.actions); move = controls.FindAction("Character/Move", false);
                jump = controls.FindAction("Character/Jump", false); reset = controls.FindAction("Character/Reset", false);
                move?.Enable(); jump?.Enable(); reset?.Enable();
            }
            if (!passenger.Begin(c)) { DisposeControls(); return false; }
            boardTime = 0; PumpInput = 0; ReleaseBlocked = false; pressure.Clear();
            Boarded?.Invoke(c); return true;
        }
        void Update()
        {
            if (!simulateAutomatically) return;
            TickControls(Time.deltaTime, move != null ? move.ReadValue<Vector2>().y : 0,
                jump != null && jump.WasPressedThisFrame(), reset != null && reset.WasPressedThisFrame());
        }
        public void TickControls(float dt, float pump, bool jumpPressed = false, bool resetPressed = false)
        {
            if (!float.IsFinite(dt) || dt <= 0) return;
            if (!Occupied) { PumpInput = 0; DisposeControls(); return; }
            boardTime += dt; PumpInput = Mathf.Clamp(pump, -1, 1);
            if (resetPressed) { Abort(); return; }
            if (jumpPressed) TryRelease();
        }
        void LateUpdate() => Present();
        public void Present()
        {
            if (!Occupied) return;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(boardTime / Mathf.Max(.05f, boardingSeconds)));
            var anchor = passenger.seat;
            passenger.Place(Vector3.Lerp(entry, anchor.position - anchor.up * .65f, t), Quaternion.Slerp(entryRotation, anchor.rotation, t), t);
        }
        public bool TryRelease()
        {
            if (!Occupied) return false;
            var c = passenger.Rider; var anchor = passenger.seat;
            Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 origin = anchor.position + Vector3.up * .2f;
            Vector3 destination = default; bool found = false;
            // Clearance and a swept capsule prevent releasing through a nearby wall.
            foreach (var offset in new[] { Vector3.zero, forward * .8f, -forward * .8f, right * .9f, -right * .9f, Vector3.up * .5f })
            {
                Vector3 p = origin + offset;
                if (!c.Motor.CanOccupy(p)) continue;
                c.Motor.Endpoints(c.transform.position, out var a, out var b);
                Vector3 delta = p - c.transform.position;
                bool blocked = false;
                if (delta.sqrMagnitude > .001f)
                    foreach (var hit in Physics.CapsuleCastAll(a, b, c.Motor.Radius - c.Motor.Skin * 2,
                        delta.normalized, delta.magnitude, c.Motor.collisionMask, QueryTriggerInteraction.Ignore))
                        if (!hit.collider.transform.IsChildOf(transform)) { blocked = true; break; }
                if (blocked) continue;
                destination = p; found = true; break;
            }
            ReleaseBlocked = !found; if (!found) return false;
            Vector3 launch = Vector3.ClampMagnitude(RideVelocity, 18) + forward * releaseForwardSpeed + Vector3.up * jumpSpeed;
            BlockReboarding(c); passenger.Release(destination, Quaternion.LookRotation(forward));
            c.ApplyImpulse(launch); DisposeControls(); PumpInput = 0; Released?.Invoke(c); return true;
        }
        void BlockReboarding(PootleCharacter c) { lastReleased = c; mustLeave = true; reboardAfter[c] = Time.time + reboardDelay; }
        public void Abort()
        {
            if (Occupied)
            {
                var c = passenger.Rider; BlockReboarding(c); passenger.Abort(); Released?.Invoke(c);
            }
            DisposeControls(); PumpInput = 0; ReleaseBlocked = false;
        }
        void DisposeControls()
        {
            move = jump = reset = null;
            if (controls) { controls.Disable(); Destroy(controls); controls = null; }
        }
        void OnDrawGizmosSelected()
        {
            if (!passenger || !passenger.seat) return;
            Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(passenger.seat.position, .25f);
        }
    }
}
