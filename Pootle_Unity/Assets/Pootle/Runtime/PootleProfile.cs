using UnityEngine;

namespace Pootle
{
    [CreateAssetMenu(menuName = "Pootle/Movement Profile", fileName = "PootleProfile")]
    public sealed class PootleProfile : ScriptableObject
    {
        [Header("Small obstacle hop")]
        public bool smallObstacleHop=true;
        [Range(.6f,1.4f)] public float smallHopHeight=1.05f;
        [Range(.2f,.6f)] public float smallHopDuration=.3f;
        [Header("External momentum")]
        [Min(0)] public float externalFacingHold=.9f;

        [Header("Ground movement · m/s, m/s²")]
        [Min(0)] public float walkSpeed = 7.5f;
        [Min(0)] public float runSpeed = 12f;
        public bool allowRun = true;
        [Min(0)] public float minimumAnalogueSpeed = 2f;
        [Min(0)] public float groundAcceleration = 16f;
        [Min(0)] public float runAcceleration = 20f;
        [Min(0)] public float releaseDeceleration = 28f;
        [Min(0)] public float turningDrag = 30f;
        [Min(0)] public float runTurningDrag = 14f;
        [Min(0)] public float brakeDeceleration = 30f;
        [Min(0)] public float minimumBrakeSpeed = 5f;
        [Tooltip("Dot(input direction, velocity in m/s), deliberately unnormalised like the source.")]
        public float brakeThreshold = -0.8f;
        [Min(0)] public float excessSpeedDeceleration = 10f;
        public bool decelerateExcessSpeed = true;
        [Header("Air and jump · m/s, m/s², seconds")]
        [Min(0)] public float airAcceleration = 32f;
        [Min(0)] public float airTurningDrag = 60f;
        [Min(0.01f)] public float risingGravity = 49f;
        [Min(0.01f)] public float fallingGravity = 65f;
        [Min(0)] public float maximumFallSpeed = 50f;
        [Min(0)] public float jumpLaunchSpeed = 19f;
        [Min(0)] public float jumpReleaseSpeed = 10f;
        [Range(0, 0.5f)] public float coyoteTime = 0.15f;
        [Range(0, 0.5f)] public float jumpBufferTime = 0.15f;
        [Range(0, 4)] public int additionalAirJumps = 1;
        [Header("Surfaces")]
        [Min(0)] public float groundSnapSpeed = 15f;
        public bool applySlopeSpeed = true;
        [Min(0)] public float uphillForce = 10f;
        [Min(0)] public float downhillForce = 16f;
        [Min(0)] public float slopeMaximumSpeed = 12f;
        [Range(0, 1)] public float landingMomentumConversion = .2f;
        public bool requireCentralSupport = true;
        [Min(0)] public float edgeSlipSpeed = 2f;
        [Header("Seated slide")]
        public bool enableSteepSliding = true;
        [Min(0)] public float slideGravity = 38f;
        [Min(0)] public float slideSlopeFriction = 0.6f;
        [Min(0)] public float slideFlatDeceleration = 5f;
        [Min(0)] public float slideMaximumSpeed = 22f;
        [Min(0)] public float slideMinimumTime = 0.4f;
        [Min(0)] public float slideStopSpeed = 0.7f;
        [Range(0, 1), Tooltip("0 = seated upright, 1 = seated up follows the slope normal.")]
        public float seatedSlopeNormalBlend = 0.65f;
        [Min(0)] public float seatedPoseSpeed = 12f;
        [Header("Climbing · hold grip (LT/L2 or E)")]
        [Min(0)] public float climbSpeed = 3f;
        [Min(0)] public float climbSidewaysSpeed = 2.5f;
        [Min(0)] public float climbReach = 0.3f;
        [Min(0)] public float climbJumpSpeed = 7f;
        [Min(0)] public float regrabDelay = 0.3f;
        [Min(0)] public float ledgeHandHeight = 1.65f;
        [Min(0)] public float ledgeCatchReach = 0.4f;
        [Range(0,45),Tooltip("Maximum top slope for automatic ledge catches. Marked climbing walls use the motor slope limit.")]
        public float ledgeMaximumTopAngle = 20;
        [Min(0)] public float topOutDuration = 0.5f;
        [Min(0)] public float topOutHopHeight = 0.4f;
        [Min(.1f)] public float topOutLiftSpeed=7;
        [Min(0)] public float poleTurnSpeed = 180f;
        [Min(0.05f)] public float poleBodyDistance = 0.45f;
        [Min(0.1f)] public float poleAttachmentSpeed = 4f;
        [Range(0, 1)] public float slideLateralRetention = 0.1f;
        [Min(0)] public float slideLateralDamping = 18f;
        [Range(0, 1)] public float railLandingRadiusFraction = 0.6f;
        [Header("Automatic grip and landing assistance")]
        public bool automaticGrip = true;
        [Min(0.1f)] public float detachRegrabDelay = 0.65f;
        [Min(0)] public float railMagnetRadius = 1.05f;
        [Min(0)] public float railMagnetSpeed = 6;
        [Min(0)] public float railMagnetHeight = 1.8f;
        [Tooltip("Press toward a connected branch within this distance to walk to its junction and turn.")]
        [Min(0)] public float railJunctionApproach = .9f;
        [Header("Rail side dismount")]
        [Min(0)] public float railDismountSettleTime=.18f;
        [Min(0)] public float railDismountHoldTime=.3f;
        [Range(0,1)] public float railDismountPressure=.75f;
        [Min(0)] public float railDismountSpeed=2.5f;
        [Header("Ground pound")]
        [Min(1)] public float groundPoundSpeed = 28;
        [Min(0)] public float poundSlideSpeed = 7;
        [Header("Gliding · hold RB / R1 or F in the air")]
        public bool allowGliding = true;
        [Min(0)] public float glidingSpeed = 9f;
        [Min(0)] public float glidingCruiseSpeed = 3f;
        [Min(0)] public float glidingAcceleration = 12f;
        [Min(0)] public float glidingTurningDrag = 8f;
        [Min(0)] public float glidingGravity = 10f;
        [Min(0)] public float glidingMaximumFallSpeed = 2f;
        [Min(0)] public float glidingRotationSpeed = 180f;
        [Header("Rolling · toggle X / gamepad West")]
        [Min(0)] public float rollingSpeed = 9f;
        [Min(0)] public float rollingControl = 42f;
        [Min(0)] public float rollingFriction = .8f;
        [Min(0)] public float rollingGravity = 38f;
        [Min(0)] public float rollingMaximumSpeed = 22f;
        [Header("Presentation")]
        [Min(0), Tooltip("Degrees per second.")] public float facingSpeed = 970f;
        public bool tiltAvatarToGround;
    }
}
