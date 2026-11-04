using System;
using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(PootleMotor), typeof(PootleTraversal))]
    [RequireComponent(typeof(PootleRailRider)), AddComponentMenu("Pootle/Character")]
    public sealed class PootleCharacter : MonoBehaviour
    {
        public PootleProfile profile;
        public PootleInput input;
        [Tooltip("The rendered camera, not the Cinemachine rig. Assign explicitly.")]
        public Transform outputCamera;
        public Transform visualRoot;
        public Transform cameraTarget;
        public bool simulateAutomatically = true;
        public bool captureDiagnostics = true;
        public Vector3 Velocity { get; internal set; }
        public Vector3 Facing { get; internal set; } = Vector3.forward;
        public PootleMode Mode { get; private set; } = PootleMode.Airborne;
        public bool IsGrounded => Mode == PootleMode.Grounded || Mode == PootleMode.RailWalking;
        public bool IsBraking { get; private set; }
        public bool IsRunning { get; private set; }
        public int AirJumpsUsed { get; private set; }
        public PootleSupport Support { get; private set; }
        public PootleMotor Motor { get; private set; }
        public PootleDebugSnapshot DebugSnapshot { get; } = new PootleDebugSnapshot();
        public event Action<bool> GroundedChanged;
        public event Action Jumped;
        public event Action<float> Landed;
        public event Action<PootleMode> ModeChanged;
        public event Action<PootleContact, float> Impact;
        public event Action<Vector3> Teleported;
        public Vector3 SeatedNormal { get; internal set; } = Vector3.up;
        private PootleTraversal traversal;
        private PootleRailRider railRider;
        private float slideTime, slideFacingAngle;
        private PootleJumpBuffer jumpBuffer;
        private float clock, lastGrounded = float.NegativeInfinity;
        private bool groundJumpUsed;
        private int tick;
        private Vector3 spawn;
        private Quaternion spawnRotation;

        private void Reset() { input = GetComponent<PootleInput>(); }
        private void Awake()
        {
            Motor = GetComponent<PootleMotor>();
            traversal = GetComponent<PootleTraversal>();
            railRider = GetComponent<PootleRailRider>();
            if (!input) input = GetComponent<PootleInput>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
            Facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (Facing.sqrMagnitude < 0.01f) Facing = Vector3.forward;
        }
        private void OnEnable()
        {
            Velocity = Vector3.zero;
            Mode = PootleMode.Airborne;
            traversal?.ResetTraversal(); railRider?.ResetRide(); SeatedNormal = Vector3.up;
            lastGrounded = float.NegativeInfinity;
            jumpBuffer.Clear(); groundJumpUsed = false; AirJumpsUsed = 0;
        }
        private void OnDisable()
        {
            Velocity = Vector3.zero; Support = default; jumpBuffer.Clear();
            traversal?.ResetTraversal(); railRider?.ResetRide();
            SetMode(PootleMode.Airborne, PootleTransition.Disabled);
        }
        private void Update()
        {
            if (!simulateAutomatically || !profile) return;
            var frame = input ? input.Sample() : default;
            if (frame.resetPressed || transform.position.y < -25) { Teleport(spawn, spawnRotation); return; }
            Tick(Time.deltaTime, frame);
        }

        // One authoritative Update tick; explicit input also supports deterministic physics tests.
        public void Tick(float dt, PootleInputFrame frame)
        {
            if (!profile || dt <= 0 || !float.IsFinite(dt)) return;
            if (!Motor) Motor = GetComponent<PootleMotor>();
            dt = Mathf.Min(dt, 0.1f);
            clock += dt; tick++;
#if UNITY_EDITOR
            Motor.Capture = captureDiagnostics ? DebugSnapshot : null;
#else
            Motor.Capture = Debug.isDebugBuild && captureDiagnostics ? DebugSnapshot : null;
#endif
            Motor.Capture?.Begin();
            Motor.BeginTick();
            if (frame.jumpPressed) jumpBuffer.Press(clock, profile.jumpBufferTime);
            Vector3 tickStart = transform.position;
            Vector3 before = Velocity;
            var previousMode = Mode;
            if (!railRider) railRider = GetComponent<PootleRailRider>();
            if (railRider && railRider.Tick(this, dt, frame, out var railMotion))
            {
                IsBraking = false; IsRunning = frame.run && profile.allowRun;
                UpdateVisual(); Capture(previousMode, Vector3.zero, railMotion, false); return;
            }
            float reach = IsGrounded ? Motor.stepHeight + 0.06f : 0.07f;
            Support = Motor.ProbeGround(transform.position, reach, profile.requireCentralSupport);
            if (!traversal) traversal = GetComponent<PootleTraversal>();
            if (traversal && traversal.Tick(this, dt, frame, out var traversalMotion))
            {
                IsBraking = IsRunning = false;
                UpdateVisual(); Capture(previousMode, Vector3.zero, traversalMotion, false); return;
            }
            bool steep = profile.enableSteepSliding && Support.found && Support.central &&
                Support.angle > Motor.slopeLimit + 0.05f && Support.angle < 85 && Velocity.y <= 0.1f;
            if (Mode == PootleMode.Sliding || steep)
            {
                if (TickSlide(dt, frame, steep, out var slideMotion))
                {
                    railRider?.Catch(this, tickStart, transform.position);
                    UpdateVisual(); Capture(previousMode, Vector3.zero, slideMotion, false); return;
                }
            }
            bool supported = Support.walkable && Velocity.y <= 0.01f;
            if (supported)
            {
                if (!IsGrounded) Land(-Velocity.y);
                SetMode(PootleMode.Grounded, PootleTransition.Supported);
                lastGrounded = clock; groundJumpUsed = false; AirJumpsUsed = 0;
            }
            else if (IsGrounded) SetMode(PootleMode.Airborne, PootleTransition.WalkedOffEdge);

            Vector3 direction = PootleMovement.CameraDirection(frame.move, outputCamera, out float magnitude);
            Vector3 planar = Vector3.ProjectOnPlane(Velocity, Vector3.up);
            IsRunning = profile.allowRun && frame.run;
            float topSpeed = IsRunning ? profile.runSpeed : profile.walkSpeed;
            IsBraking = IsGrounded && magnitude > 0 &&
                (IsBraking || planar.magnitude > profile.minimumBrakeSpeed && Vector3.Dot(direction, planar) < profile.brakeThreshold);
            if (IsBraking)
            {
                planar = Vector3.MoveTowards(planar, Vector3.zero, profile.brakeDeceleration * dt);
                if (planar.sqrMagnitude < 0.0001f) IsBraking = false;
            }
            else if (magnitude > 0)
            {
                planar = PootleMovement.Accelerate(planar, direction, IsRunning ? 1 : magnitude,
                    IsGrounded ? IsRunning ? profile.runAcceleration : profile.groundAcceleration : profile.airAcceleration,
                    IsGrounded ? IsRunning ? profile.runTurningDrag : profile.turningDrag : profile.airTurningDrag,
                    topSpeed, profile.minimumAnalogueSpeed, dt);
                if (IsGrounded && profile.decelerateExcessSpeed && planar.magnitude > topSpeed)
                    planar = Vector3.MoveTowards(planar, planar.normalized * topSpeed, profile.excessSpeedDeceleration * dt);
            }
            else if (IsGrounded) planar = Vector3.MoveTowards(planar, Vector3.zero, profile.releaseDeceleration * dt);
            if (IsGrounded && profile.applySlopeSpeed && Support.angle > 20)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Support.normal, Vector3.up);
                float force = Vector3.Dot(downhill, planar) > 0 ? profile.downhillForce : profile.uphillForce;
                planar += downhill * force * dt;
            }
            Vector3 edgeSlip = !IsGrounded && Support.found && !Support.central && Velocity.y <= 0
                ? Vector3.ProjectOnPlane(Support.normal, Vector3.up).normalized * profile.edgeSlipSpeed : Vector3.zero;

            float vertical = IsGrounded ? 0 : Velocity.y;
            bool jumped = TryJump(ref vertical, frame.jumpHeld);
            if (!frame.jumpHeld && vertical > profile.jumpReleaseSpeed) vertical = profile.jumpReleaseSpeed;
            float initialVertical = vertical;
            if (!IsGrounded)
            {
                // Integrate gravity over this tick using its average velocity for displacement.
                float gravity = vertical > 0 ? profile.risingGravity : profile.fallingGravity;
                vertical = Mathf.Max(-profile.maximumFallSpeed, vertical - gravity * dt);
            }
            Velocity = planar + Vector3.up * vertical;
            float moveY = vertical;
            if (!IsGrounded) moveY = (initialVertical + vertical) * 0.5f;
            Vector3 planarMotion = (Vector3.ProjectOnPlane(before, Vector3.up) + planar) * 0.5f;
            Vector3 displacement = planarMotion * dt + Vector3.up * (moveY * dt);
            if (IsGrounded && Support.walkable && planar.sqrMagnitude > 0)
            {
                Vector3 slopeMotion = Vector3.ProjectOnPlane(planarMotion, Support.normal);
                if (slopeMotion.sqrMagnitude > 0) displacement = slopeMotion.normalized * planarMotion.magnitude * dt;
            }
            displacement += edgeSlip * dt;
            var result = Motor.Move(displacement, IsGrounded && !jumped, IsGrounded && !jumped ? Motor.stepHeight + 0.06f : 0, false, profile.groundSnapSpeed * dt);
            if (railRider && railRider.Catch(this, tickStart, transform.position))
            { UpdateVisual(); Capture(previousMode, direction, result, jumped); return; }
            for (int i = 0; i < Motor.ContactCount; i++)
            {
                var contact = Motor.Contacts[i];
                float speed = -Vector3.Dot(Velocity, contact.normal);
                if (contact.ceiling && Velocity.y > 0) Velocity = new Vector3(Velocity.x, 0, Velocity.z);
                if (contact.wall && speed > 0)
                {
                    Vector3 normal = Vector3.ProjectOnPlane(contact.normal, Vector3.up).normalized;
                    Velocity -= normal * Mathf.Min(0, Vector3.Dot(Velocity, normal));
                }
                if (speed > 5 && Vector3.Dot(before, contact.normal) < -5) Impact?.Invoke(contact, speed);
            }
            if (!jumped && Velocity.y <= 0)
            {
                Support = Motor.ProbeGround(transform.position, IsGrounded ? Motor.stepHeight + 0.06f : 0.07f, profile.requireCentralSupport);
                if (Support.walkable)
                {
                    if (!IsGrounded) Land(-Velocity.y);
                    lastGrounded = clock; groundJumpUsed = false; AirJumpsUsed = 0;
                    SetMode(PootleMode.Grounded, PootleTransition.Landed);
                    Velocity = new Vector3(Velocity.x, 0, Velocity.z);
                    float launch = 0;
                    if (TryJump(ref launch, frame.jumpHeld)) { Velocity += Vector3.up * launch; jumped = true; }
                }
                else if (IsGrounded) SetMode(PootleMode.Airborne, PootleTransition.WalkedOffEdge);
            }
            if (planar.sqrMagnitude > 0.002f && !IsBraking)
                Facing = Vector3.RotateTowards(Facing, planar.normalized, profile.facingSpeed * Mathf.Deg2Rad * dt, 0).normalized;
            UpdateVisual();
            Capture(previousMode, direction, result, jumped);
        }

        private void UpdateVisual()
        {
            if (!visualRoot) return;
            Vector3 up = Mode == PootleMode.Sliding || Mode == PootleMode.RailSliding
                ? Vector3.Slerp(Vector3.up, SeatedNormal, profile.seatedSlopeNormalBlend)
                : profile.tiltAvatarToGround && IsGrounded ? Support.normal : Vector3.up;
            Vector3 look = Vector3.ProjectOnPlane(Facing, up).normalized;
            if (look.sqrMagnitude > 0.001f) visualRoot.rotation = Quaternion.LookRotation(look, up);
        }

        private bool TickSlide(float dt, PootleInputFrame frame, bool steep, out PootleMotionResult result)
        {
            result = default;
            Vector3 normal = Support.found ? Support.normal : Vector3.up;
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
            Vector3 flatDownhill = Vector3.ProjectOnPlane(downhill, Vector3.up).normalized;
            if (Mode != PootleMode.Sliding)
            {
                slideTime = 0;
                float downhillYaw = Mathf.Atan2(flatDownhill.x, flatDownhill.z) * Mathf.Rad2Deg;
                float facingYaw = Mathf.Atan2(Facing.x, Facing.z) * Mathf.Rad2Deg;
                slideFacingAngle = Mathf.Round(Mathf.DeltaAngle(downhillYaw, facingYaw) / 90) * 90;
                Vector3 tangent = Vector3.ProjectOnPlane(Velocity, normal);
                // A falling impact cannot launch the seated character uphill.
                if (Vector3.Dot(tangent, downhill) < 0) tangent -= downhill * Vector3.Dot(tangent, downhill);
                Vector3 downVelocity = downhill * Mathf.Max(0, Vector3.Dot(tangent, downhill));
                Velocity = downVelocity + (tangent - downVelocity) * profile.slideLateralRetention;
                groundJumpUsed = false; AirJumpsUsed = 0;
                SetMode(PootleMode.Sliding, PootleTransition.SlideStarted);
            }
            if (jumpBuffer.Consume(clock))
            {
                Vector3 direction = PootleMovement.CameraDirection(frame.move, outputCamera, out float magnitude);
                Vector3 planar = magnitude > 0 ? direction * Mathf.Max(profile.walkSpeed, Vector3.ProjectOnPlane(Velocity, Vector3.up).magnitude)
                    : Vector3.ProjectOnPlane(Velocity, Vector3.up);
                LaunchTraversal(planar, frame.jumpHeld);
                return false;
            }
            slideTime += dt;
            IsRunning = IsBraking = false;
            if (steep)
            {
                SeatedNormal = normal;
                Facing = Quaternion.AngleAxis(slideFacingAngle, Vector3.up) * flatDownhill;
                Vector3 tangent = Vector3.ProjectOnPlane(Velocity, normal);
                float downSpeed = Mathf.Max(0, Vector3.Dot(tangent, downhill));
                Vector3 lateral = tangent - downhill * Vector3.Dot(tangent, downhill);
                tangent = downhill * downSpeed + lateral * Mathf.Exp(-profile.slideLateralDamping * dt);
                tangent += Vector3.ProjectOnPlane(Vector3.down * profile.slideGravity, normal) * dt;
                Velocity = Vector3.ClampMagnitude(Vector3.MoveTowards(tangent, Vector3.zero, profile.slideSlopeFriction * dt), profile.slideMaximumSpeed);
            }
            else if (Support.walkable)
            {
                SeatedNormal = normal;
                Vector3 tangent = Vector3.ProjectOnPlane(Velocity, normal);
                Velocity = Vector3.MoveTowards(tangent, Vector3.zero, profile.slideFlatDeceleration * dt);
                if (slideTime >= profile.slideMinimumTime && Velocity.magnitude <= profile.slideStopSpeed)
                {
                    Velocity = Vector3.zero; SetMode(PootleMode.Grounded, PootleTransition.SlideStopped);
                    return false;
                }
            }
            else Velocity += Vector3.down * profile.fallingGravity * dt;
            result = Motor.Move(Velocity * dt, false, Support.walkable ? 0.1f : 0, true);
            for (int i = 0; i < Motor.ContactCount; i++)
            {
                var hit = Motor.Contacts[i];
                if (hit.ceiling || hit.normal.y < 0.1f)
                    Velocity = Vector3.ProjectOnPlane(Velocity, hit.normal);
            }
            return true;
        }

        internal void RailSupported() { lastGrounded = clock; groundJumpUsed = false; AirJumpsUsed = 0; }

        internal bool ConsumeJump() => jumpBuffer.Consume(clock);
        internal void LaunchTraversal(Vector3 planar, bool held)
        {
            Velocity = planar + Vector3.up * (held ? profile.jumpLaunchSpeed : profile.jumpReleaseSpeed);
            groundJumpUsed = true; lastGrounded = float.NegativeInfinity;
            SetMode(PootleMode.Airborne, PootleTransition.Jumped); Jumped?.Invoke();
        }

        private bool TryJump(ref float vertical, bool held)
        {
            bool coyote = !groundJumpUsed && clock - lastGrounded <= profile.coyoteTime;
            bool air = !IsGrounded && !coyote && AirJumpsUsed < profile.additionalAirJumps;
            if ((!IsGrounded && !coyote && !air) || !jumpBuffer.Consume(clock)) return false;
            if (air) AirJumpsUsed++;
            groundJumpUsed = true; lastGrounded = float.NegativeInfinity;
            vertical = held ? profile.jumpLaunchSpeed : Mathf.Min(profile.jumpLaunchSpeed, profile.jumpReleaseSpeed);
            SetMode(PootleMode.Airborne, air ? PootleTransition.AirJumped : PootleTransition.Jumped);
            Jumped?.Invoke();
            return true;
        }

        private void Land(float speed)
        {
            if (profile.landingMomentumConversion > 0 && Support.angle > 20)
                Velocity += Vector3.ProjectOnPlane(Support.normal, Vector3.up).normalized * speed * profile.landingMomentumConversion;
            Landed?.Invoke(speed);
        }
        internal void SetMode(PootleMode mode, PootleTransition reason)
        {
            if (Mode == mode) return;
            Mode = mode;
            Motor?.Capture?.Transition(reason);
            GroundedChanged?.Invoke(IsGrounded); ModeChanged?.Invoke(mode);
        }
        private void Capture(PootleMode previous, Vector3 direction, PootleMotionResult result, bool jumped)
        {
            var d = Motor.Capture;
            if (d == null || d.frozen) return;
            d.tick = tick; d.time = Time.realtimeSinceStartup; d.position = transform.position;
            d.center = Motor.center; d.radius = Motor.Radius; d.height = Motor.Height; d.skin = Motor.Skin; d.step = Motor.stepHeight;
            d.slopeLimit = Motor.slopeLimit;
            d.previousMode = previous; d.mode = Mode; d.velocity = Velocity; d.inputDirection = direction;
            d.requested = result.requested; d.resolved = result.displacement; d.support = Support; d.facing = Facing;
            d.bufferRemaining = jumpBuffer.Remaining(clock); d.coyoteRemaining = Mathf.Max(0, profile.coyoteTime - (clock - lastGrounded));
            d.jumpAccepted = jumped;
        }
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            Vector3 delta = position - transform.position;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            Facing = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
            if (Facing.sqrMagnitude < 0.01f) Facing = Vector3.forward;
            Velocity = Vector3.zero; Support = default; IsBraking = IsRunning = false;
            lastGrounded = float.NegativeInfinity; groundJumpUsed = false; AirJumpsUsed = 0;
            jumpBuffer.Clear(); Mode = PootleMode.Airborne;
            traversal?.ResetTraversal(); railRider?.ResetRide(); SeatedNormal = Vector3.up;
            DebugSnapshot.frozen = false; DebugSnapshot.Clear(); DebugSnapshot.Transition(PootleTransition.Teleported);
            if (visualRoot) visualRoot.rotation = Quaternion.LookRotation(Facing);
            Physics.SyncTransforms();
            GroundedChanged?.Invoke(false); ModeChanged?.Invoke(Mode);
            Teleported?.Invoke(delta);
        }
    }
}
