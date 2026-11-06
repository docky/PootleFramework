using System;
using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(PootleMotor), typeof(PootleTraversal))]
    [RequireComponent(typeof(PootlePlatformRider), typeof(PootleHookRider))]
    [RequireComponent(typeof(PootleRailRider), typeof(PootleRecovery), typeof(PootleCarrying)), AddComponentMenu("Pootle/Character")]
    public sealed partial class PootleCharacter : MonoBehaviour
    {
        public PootleProfile profile;
        public PootleInput input;
        [Tooltip("The rendered camera, not the Cinemachine rig. Assign explicitly.")]
        public Transform outputCamera;
        public Transform visualRoot;
        public Transform cameraTarget;
        public bool simulateAutomatically = true;
        public bool captureDiagnostics = true;
        [Tooltip("Optional IPootleMovementPolicy. Leave empty for standard Pootle controls.")]
        public MonoBehaviour movementPolicy;
        public Vector3 Velocity { get; internal set; }
        public Vector3 Facing { get; internal set; } = Vector3.forward;
        public PootleMode Mode { get; private set; } = PootleMode.Airborne;
        public bool IsGrounded => IsRolling ? rollingGrounded : Mode == PootleMode.Grounded || Mode == PootleMode.RailWalking || Mode == PootleMode.PoleStanding;
        public bool IsBraking { get; private set; }
        public bool IsRunning { get; private set; }
        public int AirJumpsUsed { get; private set; }
        public PootleSupport Support { get; private set; }
        public PootleMotor Motor { get; private set; }
        public PootleDebugSnapshot DebugSnapshot { get; } = new PootleDebugSnapshot();
        public event Action<bool> GroundedChanged;
        public event Action Jumped;
        public event Action<float> Landed;
        internal void ReportLanding(float speed) => Landed?.Invoke(Mathf.Max(0, speed));
        public event Action<PootleMode> ModeChanged;
        public event Action<PootleContact, float> Impact;
        public event Action<Vector3> Teleported;
        public event Action<float> ClimbingViewRotated;
        internal void RotateClimbingView(float degrees)=>ClimbingViewRotated?.Invoke(degrees);
        public Vector3 SeatedNormal { get; internal set; } = Vector3.up;
        private readonly PootlePathInputAlignment pathAlignment = new PootlePathInputAlignment();
        private PootleTraversal traversal;
        private PootleRailRider railRider;
        private PootlePlatformRider platformRider;
        private PootleHookRider hookRider;
        private float slideTime, slideFacingAngle;
        private PootleJumpBuffer jumpBuffer;
        private float clock, lastGrounded = float.NegativeInfinity;
        private bool groundJumpUsed, variableJump;
        private float nextSpringTime, grabLockUntil;
        private bool forcedSlopeSlide;
        public bool CanGrab => !IsRolling && !SlideRideActive && !(carrying && carrying.Held) && clock >= grabLockUntil && Mode != PootleMode.GroundPounding;
        private PootleCarrying carrying;
        private bool carryRequested;
        private PootleRecovery recovery;
        internal void BlockGrabs() => grabLockUntil = clock + profile.detachRegrabDelay;
        private int tick;
        private Vector3 spawn;
        private Quaternion spawnRotation;

        private void Reset() { input = GetComponent<PootleInput>(); }
        private void Awake()
        {
            Motor = GetComponent<PootleMotor>();
            traversal = GetComponent<PootleTraversal>();
            railRider = GetComponent<PootleRailRider>();
            platformRider = GetComponent<PootlePlatformRider>();
            hookRider = GetComponent<PootleHookRider>();
            carrying = GetComponent<PootleCarrying>();
            if (!input) input = GetComponent<PootleInput>();
            spawn = transform.position;
            spawnRotation = transform.rotation;
            Facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (Facing.sqrMagnitude < 0.01f) Facing = Vector3.forward;
        }
        private void OnEnable()
        {
            pathAlignment.Reset();
            Velocity = Vector3.zero;
            Mode = PootleMode.Airborne;
            traversal?.ResetTraversal(); railRider?.ResetRide(); platformRider?.ClearRiding(); hookRider?.ResetRide(); SeatedNormal = Vector3.up;
            lastGrounded = float.NegativeInfinity;
            jumpBuffer.Clear(); groundJumpUsed = variableJump = false; grabLockUntil=0; forcedSlopeSlide=false; nextSpringTime = 0; AirJumpsUsed = 0;
        }
        private void OnDisable()
        {
            pathAlignment.Reset();
            ResetSlideRide();
            ResetForces();ResetRolling();carrying?.ReleaseForReset();
            Velocity = Vector3.zero; Support = default; jumpBuffer.Clear();
            traversal?.ResetTraversal(); railRider?.ResetRide(); platformRider?.ClearRiding(); hookRider?.ResetRide();
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
            var policy = movementPolicy && movementPolicy.isActiveAndEnabled ? movementPolicy as IPootleMovementPolicy : null;
            if (PootleRideSeat.Process(this, frame, dt)) return;
            if (PootleIntentGate.Process(this, frame, dt)) return;
            bool groundedAtTickStart = IsGrounded;
            clock += dt; tick++;
#if UNITY_EDITOR
            Motor.Capture = captureDiagnostics ? DebugSnapshot : null;
#else
            Motor.Capture = Debug.isDebugBuild && captureDiagnostics ? DebugSnapshot : null;
#endif
            Motor.Capture?.Begin();
            Motor.BeginTick(dt);
            if (!recovery) recovery=GetComponent<PootleRecovery>();
            carryRequested=frame.carryPressed;
            if(frame.rollPressed)ToggleRolling();
            if (frame.detachPressed && !IsRolling)
            {
                bool attached=Mode==PootleMode.Climbing || Mode==PootleMode.PoleClimbing || Mode==PootleMode.LedgeHanging || Mode==PootleMode.LedgeHop || Mode==PootleMode.Dangling;
                if(attached)
                {
                    bool carried=Mode==PootleMode.Dangling;
                    BlockGrabs(); GetComponent<PootleHookRider>()?.Drop(this); GetComponent<PootleTraversal>()?.Drop(this);
                    if(!carried)Velocity=Vector3.zero;
                }
                else if(!IsGrounded && Mode!=PootleMode.GroundPounding)
                {
                    ResetSlideRide();
                    GetComponent<PootleRailRider>()?.ResetRide(); GetComponent<PootleTraversal>()?.ResetTraversal();
                    jumpBuffer.Clear(); variableJump=false; forcedSlopeSlide=false;
                    Velocity=Vector3.down*profile.groundPoundSpeed; SetMode(PootleMode.GroundPounding,PootleTransition.SlideStarted);
                }
            }
            if (frame.interactPressed)
            {
                var sensor = GetComponent<PootleFrobjectSensor>();
                if (sensor && sensor.TryInteract())
                {
                    // Interactions may deactivate or remove the player (e.g. scene transitions).
                    if (!this || !isActiveAndEnabled) return;
                }
            }
            if (policy != null) jumpBuffer.Clear();
            else if (frame.jumpPressed) jumpBuffer.Press(clock, profile.jumpBufferTime);
            TickEnvironment(dt);
            Vector3 tickStart = transform.position;
            var previousMode = Mode;
            if (!platformRider) platformRider = GetComponent<PootlePlatformRider>();
            if (!hookRider) hookRider = GetComponent<PootleHookRider>();
            platformRider?.Apply(this, dt);
            // A rising support has already moved into last frame's capsule. Carry first;
            // only unresolved overlap after that motion should count as being trapped.
            Motor.ReceiveBodyMotion(this);
            Vector3 before = Velocity;
            recovery?.Check(this,dt,frame);
            if(IsRolling)
            {
                ResetSlideRide();
                TickRolling(dt,frame,out var rollMotion);
                UpdateVisual();Capture(previousMode,PootleMovement.CameraDirection(frame.move,outputCamera,out _),rollMotion,false);return;
            }
            if (hookRider && hookRider.Tick(this, dt, frame, out var hookMotion))
            {
                IsBraking = IsRunning = false;
                UpdateVisual(); Capture(previousMode, Vector3.zero, hookMotion, false); return;
            }
            if (!railRider) railRider = GetComponent<PootleRailRider>();
            if (railRider && railRider.Tick(this, dt, frame, out var railMotion))
            {
                IsBraking = false; IsRunning = frame.run && profile.allowRun;
                UpdateVisual(); Capture(previousMode, Vector3.zero, railMotion, false); return;
            }
            float reach = IsGrounded ? Motor.stepHeight + 0.06f : 0.07f;
            Support = Motor.ProbeGround(transform.position, reach, profile.requireCentralSupport);
            if(TickSlideRide(dt,frame,out var rideMotion))
            {
                railRider?.Catch(this,tickStart,transform.position);
                UpdateVisual();Capture(previousMode,Vector3.zero,rideMotion,false);return;
            }
            if (!traversal) traversal = GetComponent<PootleTraversal>();
            if (traversal && traversal.Tick(this, dt, frame, out var traversalMotion))
            {
                IsBraking = IsRunning = false;
                UpdateVisual(); Capture(previousMode, Vector3.zero, traversalMotion, false); return;
            }
            bool springLaunched = TrySpring();
            bool touching = Support.found && Support.central && Support.distance <= Motor.Skin * 3;
            var slideSurface=Support.collider?Support.collider.GetComponentInParent<PootleSlideSurface>():null;
            bool slippery=slideSurface && slideSurface.isActiveAndEnabled;
            if (Mode==PootleMode.GroundPounding && touching && Support.angle>3 && Support.angle<85) StartPoundSlide();
            bool steep = profile.enableSteepSliding && touching &&
                Support.angle > (forcedSlopeSlide || slippery ? 3 : Motor.slopeLimit + 0.05f) && Support.angle < 85 && Velocity.y <= 0.1f;
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

            if (!springLaunched && (Mode == PootleMode.Gliding || Mode == PootleMode.Airborne && frame.glide && profile.allowGliding && Velocity.y <= 0))
            {
                if (TickGlide(dt, frame, tickStart, out var glideMotion))
                { UpdateVisual(); Capture(previousMode, Vector3.zero, glideMotion, false); return; }
            }
            float policyImpulse = policy != null ? policy.BeforeMovement(this, dt, frame, groundedAtTickStart) : float.NaN;
            bool policyJump = float.IsFinite(policyImpulse);
            if (policyJump)
            {
                Velocity = new Vector3(Velocity.x, policyImpulse, Velocity.z);
                groundJumpUsed = true; variableJump = false; lastGrounded = float.NegativeInfinity;
                SetMode(PootleMode.Airborne, PootleTransition.Jumped);
                Jumped?.Invoke();
            }
            Vector3 direction = PootleMovement.CameraDirection(Mode==PootleMode.GroundPounding ? Vector2.zero : frame.move, outputCamera, out float magnitude);
            direction = pathAlignment.Align(transform.position, direction, frame.moveIsDigital, IsGrounded, dt);
            Vector3 planar = Vector3.ProjectOnPlane(Velocity, Vector3.up) - externalPlanar;
            IsRunning = profile.allowRun && frame.run;
            float topSpeed = IsRunning ? profile.runSpeed : profile.walkSpeed;
            IsBraking = IsGrounded && magnitude > 0 &&
                (IsBraking || planar.magnitude > profile.minimumBrakeSpeed && Vector3.Dot(direction, planar) < profile.brakeThreshold);
            if (policy != null && !IsGrounded)
                planar = policy.AirVelocity(planar, direction, magnitude, dt);
            else if (IsBraking)
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
            if (IsGrounded && profile.applySlopeSpeed && magnitude > 0 && Support.angle > 3)
            {
                Vector3 downhill = Vector3.ProjectOnPlane(Support.normal, Vector3.up);
                float force = Vector3.Dot(downhill, planar) > 0 ? profile.downhillForce : profile.uphillForce;
                planar = Vector3.ClampMagnitude(planar + downhill * force * dt, profile.slopeMaximumSpeed);
            }
            Vector3 edgeSlip = !IsGrounded && Support.found && !Support.central && Velocity.y <= 0
                ? Vector3.ProjectOnPlane(Support.normal, Vector3.up).normalized * profile.edgeSlipSpeed : Vector3.zero;

            float vertical = IsGrounded ? 0 : Velocity.y;
            bool jumped = policyJump || (policy == null && Mode != PootleMode.GroundPounding && TryJump(ref vertical, frame.jumpHeld));
            if (jumped && platformRider)
            {
                planar += Vector3.ProjectOnPlane(platformRider.CarryVelocity, Vector3.up);
                vertical += Mathf.Max(0,platformRider.CarryVelocity.y);
            }
            if (variableJump && !frame.jumpHeld && vertical > profile.jumpReleaseSpeed) vertical = profile.jumpReleaseSpeed;
            float initialVertical = vertical;
            if (!IsGrounded)
            {
                // Integrate gravity over this tick using its average velocity for displacement.
                float gravity = vertical > 0 ? profile.risingGravity : profile.fallingGravity;
                vertical = Mathf.Max(-profile.maximumFallSpeed, vertical - gravity * dt);
            }
            vertical=WindVertical(initialVertical,vertical,dt);
            planar+=externalPlanar;
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
            railRider?.AssistLanding(this,dt);
            if (traversal && traversal.CatchPoleTop(this,tickStart,transform.position))
            { UpdateVisual(); Capture(previousMode,direction,result,false); return; }
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
                    externalPlanar -= normal * Mathf.Min(0, Vector3.Dot(externalPlanar, normal));
                }
                if (speed > 5 && Vector3.Dot(before, contact.normal) < -5) Impact?.Invoke(contact, speed);
            }
            if (!jumped && Velocity.y <= 0)
            {
                Support = Motor.ProbeGround(transform.position, IsGrounded ? Motor.stepHeight + 0.06f : 0.07f, profile.requireCentralSupport);
                if (TrySpring())
                { UpdateVisual(); Capture(previousMode, direction, result, true); return; }
                if (Mode==PootleMode.GroundPounding && Support.central && Support.distance<=Motor.Skin*3 && Support.angle>3 && Support.angle<85)
                { StartPoundSlide(); TickSlide(dt,frame,true,out result); UpdateVisual(); Capture(previousMode,direction,result,false); return; }
                if (Support.walkable)
                {
                    if (!IsGrounded) Land(-Velocity.y);
                    lastGrounded = clock; groundJumpUsed = false; AirJumpsUsed = 0;
                    SetMode(PootleMode.Grounded, PootleTransition.Landed);
                    Velocity = new Vector3(Velocity.x, 0, Velocity.z);
                    float launch = 0;
                    if (policy == null && TryJump(ref launch, frame.jumpHeld)) { Velocity += Vector3.up * launch; jumped = true; }
                }
                else if (IsGrounded) SetMode(PootleMode.Airborne, PootleTransition.WalkedOffEdge);
            }
            if (planar.sqrMagnitude > 0.002f && !IsBraking && !HoldingExternalFacing)
            {
                Vector3 wanted=Vector3.RotateTowards(Facing,planar.normalized,profile.facingSpeed*Mathf.Deg2Rad*dt,0).normalized;
                if(carrying && carrying.Held)carrying.Turn(this,wanted);else Facing=wanted;
            }
            UpdateVisual();
            Capture(previousMode, direction, result, jumped);
        }

        private void UpdateVisual()
        {
            Motor.ApplyStandingForce(Support,IsGrounded);
            platformRider?.Remember(this);
            if(!carrying)carrying=GetComponent<PootleCarrying>();
            if(carryRequested){carryRequested=false;carrying?.HandleAction(this);}
            carrying?.Follow(this);
            if (!visualRoot) return;
            Vector3 up = Mode == PootleMode.Sliding || Mode == PootleMode.RailSliding || SlideRideAirborne
                ? Vector3.Slerp(Vector3.up, SeatedNormal, profile.seatedSlopeNormalBlend)
                : profile.tiltAvatarToGround && IsGrounded ? Support.normal : Vector3.up;
            Vector3 look = Vector3.ProjectOnPlane(Facing, up).normalized;
            if (look.sqrMagnitude > 0.001f) visualRoot.rotation = Quaternion.LookRotation(look, up);
        }

        private bool TickSlide(float dt, PootleInputFrame frame, bool steep, out PootleMotionResult result)
        {
            result = default;
            if (!Support.found || !Support.central || Support.distance > 0.08f)
            {
                // Resolve this tick before deciding whether the slope joins a flat runout.
                result=Motor.Move(Velocity*dt,false,0,true);
                Support=Motor.ProbeGround(transform.position,.07f,profile.requireCentralSupport);
                if(Support.walkable && Support.distance<=Motor.Skin*3)
                {Velocity=Vector3.ProjectOnPlane(Velocity,Support.normal);return true;}
                forcedSlopeSlide=false;SetMode(PootleMode.Airborne,PootleTransition.WalkedOffEdge);return true;
            }
            Vector3 normal = Support.normal;
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, normal).normalized;
            Vector3 flatDownhill = Vector3.ProjectOnPlane(downhill, Vector3.up).normalized;
            if (Mode != PootleMode.Sliding)
            {
                if (Mode == PootleMode.Airborne || Mode == PootleMode.Gliding || Mode == PootleMode.GroundPounding)
                    ReportLanding(Mathf.Max(0, -Vector3.Dot(Velocity, normal)));
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
                    forcedSlopeSlide=false; Velocity = Vector3.zero; SetMode(PootleMode.Grounded, PootleTransition.SlideStopped);
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

        private void StartPoundSlide()
        {
            forcedSlopeSlide=true;
            var downhill=Vector3.ProjectOnPlane(Vector3.down,Support.normal).normalized;
            Velocity=downhill*profile.poundSlideSpeed;
            SetMode(PootleMode.Airborne,PootleTransition.Landed);
        }

        private bool TrySpring()
        {
            if (!Support.walkable || !Support.collider || Velocity.y > 0 || clock < nextSpringTime) return false;
            var spring = Support.collider.GetComponentInParent<PootleSpring>();
            if (!spring || !spring.isActiveAndEnabled) return false;
            Vector3 direction = spring.Direction;
            Velocity = Vector3.ProjectOnPlane(Velocity, direction) * spring.lateralRetention + direction * spring.launchSpeed;
            nextSpringTime = clock + spring.cooldown; variableJump = false; forcedSlopeSlide=false;
            jumpBuffer.Clear(); groundJumpUsed = true; lastGrounded = float.NegativeInfinity; AirJumpsUsed = 0;
            SetMode(PootleMode.Airborne, PootleTransition.Jumped); spring.Launched(); Jumped?.Invoke();
            return true;
        }

        private bool TickGlide(float dt, PootleInputFrame frame, Vector3 tickStart, out PootleMotionResult result)
        {
            result = default;
            if (!frame.glide || !profile.allowGliding)
            { SetMode(PootleMode.Airborne, PootleTransition.Released); return false; }
            float jumpVelocity=Velocity.y;
            if(TryJump(ref jumpVelocity,frame.jumpHeld))
            {Velocity=new Vector3(Velocity.x,jumpVelocity,Velocity.z);return false;}
            IsRunning = IsBraking = false;
            if (Mode != PootleMode.Gliding)
            {
                Velocity = new Vector3(Velocity.x, Mathf.Max(Velocity.y, -profile.glidingMaximumFallSpeed), Velocity.z);
                variableJump = false; SetMode(PootleMode.Gliding, PootleTransition.Supported);
            }
            Vector3 direction = PootleMovement.CameraDirection(frame.move, outputCamera, out float magnitude);
            if (magnitude > 0.1f)
                Facing = Vector3.RotateTowards(Facing, direction, profile.glidingRotationSpeed * Mathf.Deg2Rad * dt, 0).normalized;
            Vector3 planar = Vector3.ProjectOnPlane(Velocity, Vector3.up) - externalPlanar;
            planar = PootleMovement.Accelerate(planar, Facing, magnitude, profile.glidingAcceleration, profile.glidingTurningDrag,
                profile.glidingSpeed, profile.glidingCruiseSpeed, dt);
            planar = Vector3.MoveTowards(planar, planar.normalized * Mathf.Min(planar.magnitude, profile.glidingSpeed), profile.glidingAcceleration * dt);
            float y = WindVertical(Velocity.y,Mathf.Max(-profile.glidingMaximumFallSpeed, Velocity.y - profile.glidingGravity * dt),dt);
            planar+=externalPlanar;
            Velocity = planar + Vector3.up * y;
            result = Motor.Move(Velocity * dt, false);
            for (int i = 0; i < Motor.ContactCount; i++)
                if (Vector3.Dot(Velocity, Motor.Contacts[i].normal) < 0) Velocity = Vector3.ProjectOnPlane(Velocity, Motor.Contacts[i].normal);
            railRider?.AssistLanding(this,dt);
            if (traversal && traversal.CatchPoleTop(this,tickStart,transform.position)) return true;
            if (railRider && railRider.Catch(this, tickStart, transform.position)) return true;
            Support = Motor.ProbeGround(transform.position, 0.07f, profile.requireCentralSupport);
            if (TrySpring()) return true;
            if (Support.walkable)
            {
                Land(-y); Velocity = new Vector3(Velocity.x, 0, Velocity.z);
                lastGrounded = clock; groundJumpUsed = false; AirJumpsUsed = 0;
                SetMode(PootleMode.Grounded, PootleTransition.Landed);
            }
            else if (Support.found && Support.central && Support.angle > Motor.slopeLimit && Support.angle < 85)
                SetMode(PootleMode.Airborne, PootleTransition.Landed);
            return true;
        }

        internal void RailSupported() { lastGrounded = clock; groundJumpUsed = false; AirJumpsUsed = 0; }
        internal bool FinishTraversalTopOut()
        {
            Support=Motor.ProbeGround(transform.position,.1f,profile.requireCentralSupport);
            if(Support.walkable && Support.distance<=Motor.Skin*4)
            {Velocity=Vector3.zero;RailSupported();SetMode(PootleMode.Grounded,PootleTransition.TopOut);return true;}
            return false;
        }

        internal bool ConsumeJump() => jumpBuffer.Consume(clock);
        internal void LaunchTraversal(Vector3 planar, bool held)
        {
            forcedSlopeSlide=false; BlockGrabs();
            variableJump = true;
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
            variableJump = true;
            vertical = held ? profile.jumpLaunchSpeed : Mathf.Min(profile.jumpLaunchSpeed, profile.jumpReleaseSpeed);
            SetMode(PootleMode.Airborne, air ? PootleTransition.AirJumped : PootleTransition.Jumped);
            Jumped?.Invoke();
            return true;
        }

        private void Land(float speed)
        {
            if (profile.landingMomentumConversion > 0 && Support.angle > 3)
            {
                Vector3 planar=Vector3.ProjectOnPlane(Velocity,Vector3.up)+Vector3.ProjectOnPlane(Support.normal,Vector3.up).normalized*speed*profile.landingMomentumConversion;
                Velocity=Vector3.ClampMagnitude(planar,profile.slopeMaximumSpeed)+Vector3.up*Velocity.y;
            }
            Landed?.Invoke(speed);
        }
        internal void SetMode(PootleMode mode, PootleTransition reason)
        {
            if (Mode == mode) return;
            if(mode==PootleMode.RailWalking || mode==PootleMode.RailSliding || mode==PootleMode.Climbing ||
                mode==PootleMode.PoleClimbing || mode==PootleMode.PoleStanding || mode==PootleMode.Dangling ||
                mode==PootleMode.LedgeHanging || mode==PootleMode.LedgeHop)ResetSlideRide();
            if(Mode==PootleMode.Sliding || Mode==PootleMode.RailSliding)PreserveExternalFacing();
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
            input?.ResetMovement();
            pathAlignment.Reset();
            ResetSlideRide();
            ResetForces();ResetRolling();carrying?.ReleaseForReset();carryRequested=false;
            Vector3 delta = position - transform.position;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            Facing = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up).normalized;
            if (Facing.sqrMagnitude < 0.01f) Facing = Vector3.forward;
            Velocity = Vector3.zero; Support = default; IsBraking = IsRunning = false;
            lastGrounded = float.NegativeInfinity; groundJumpUsed = false; AirJumpsUsed = 0;
            jumpBuffer.Clear(); variableJump = false; forcedSlopeSlide=false; grabLockUntil=0; nextSpringTime = 0; Mode = PootleMode.Airborne;
            traversal?.ResetTraversal(); railRider?.ResetRide(); platformRider?.ClearRiding(); hookRider?.ResetRide(); SeatedNormal = Vector3.up;
            DebugSnapshot.frozen = false; DebugSnapshot.Clear(); DebugSnapshot.Transition(PootleTransition.Teleported);
            if (visualRoot) visualRoot.rotation = Quaternion.LookRotation(Facing);
            Physics.SyncTransforms();
            GroundedChanged?.Invoke(false); ModeChanged?.Invoke(Mode);
            Teleported?.Invoke(delta);
        }
    }
}
