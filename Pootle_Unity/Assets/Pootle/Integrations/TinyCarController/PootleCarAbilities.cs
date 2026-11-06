using UnityEngine;

namespace Pootle
{
    public sealed partial class PootleCar
    {
        [Header("Jump")]
        [Min(0)] public int extraAirJumps = 4;
        [Min(.1f)] public float jumpSpeed = 7;
        [Header("Boost")]
        [Min(1)] public int boostCapacity = 4;
        [Min(.1f)] public float boostRechargeSeconds = 3;
        [Min(.1f)] public float boostDuration = 1;
        [Min(1)] public float boostSpeedMultiplier = 2;
        [Min(1)] public float boostAccelerationMultiplier = 3;
        [Min(0)] public float boostKickSpeed = 6;
        [Min(0)] public float boostAcceleration = 18;

        public int AirJumpsRemaining { get; private set; }
        public int BoostCharges { get; private set; }
        public bool IsBoosting => boostRemaining > 0;
        public float BoostRechargeProgress => BoostCharges >= boostCapacity ? 1 : Mathf.Clamp01(rechargeTime / Mathf.Max(.1f, boostRechargeSeconds));
        float boostRemaining, rechargeTime, nextJumpTime;
        float normalMaxSpeedMultiplier, normalAccelerationMultiplier;
        readonly RaycastHit[] groundHits = new RaycastHit[32];

        void InitializeAbilities()
        {
            AirJumpsRemaining = Mathf.Max(0, extraAirJumps);
            BoostCharges = Mathf.Max(1, boostCapacity);
            normalMaxSpeedMultiplier = Controller.maxSpeedMultiplier;
            normalAccelerationMultiplier = Controller.accelerationMultiplier;
        }

        bool HasJumpSupport()
        {
            // Wheel contact flags can lag takeoff or report walls as ground. Check below the tires
            // and require two supported wheels before restoring the airborne jump allowance.
            if (Time.time < nextJumpTime || Controller.getCarRigidBody().linearVelocity.y > .5f) return false;
            return HasWheelSupport(false, .06f);
        }

        bool HasWheelSupport(bool stationaryOnly, float tolerance)
        {
            int supported = 0;
            foreach (var wheel in Controller.wheels)
            {
                var shape = wheel.getCollider();
                float radius = shape.radius * Mathf.Max(Mathf.Abs(wheel.transform.lossyScale.x), Mathf.Abs(wheel.transform.lossyScale.y), Mathf.Abs(wheel.transform.lossyScale.z));
                int count = Physics.RaycastNonAlloc(wheel.getRigidbody().position, Vector3.down, groundHits, radius + tolerance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                // Conservatively refuse a saturated query rather than refilling against an unseen obstruction.
                if (count == groundHits.Length) continue;
                RaycastHit nearest = default; float distance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                    if (!OwnCollider(groundHits[i].collider) && groundHits[i].distance < distance)
                    { nearest = groundHits[i]; distance = nearest.distance; }
                if (!nearest.collider || nearest.normal.y < .65f) continue;
                // Dynamic/kinematic platforms must remain free to carry the braked vehicle.
                if (stationaryOnly && (nearest.rigidbody || nearest.collider.GetComponentInParent<PootleMovingSurface>())) continue;
                if (++supported >= 2) return true;
            }
            return false;
        }

        public bool TryJump()
        {
            if (!initialized || !isActiveAndEnabled || !IsOccupied || Time.time < nextJumpTime) return false;
            if (HasJumpSupport()) AirJumpsRemaining = Mathf.Max(0, extraAirJumps);
            else
            {
                if (AirJumpsRemaining <= 0) return false;
                AirJumpsRemaining--;
            }
            nextJumpTime = Time.time + .15f;
            // Move the body and every suspension rigidbody together so the joints do not absorb the jump.
            AddVelocity(Vector3.up * Mathf.Max(0, jumpSpeed - Controller.getCarRigidBody().linearVelocity.y));
            return true;
        }

        public bool TryBoost()
        {
            if (!initialized || !isActiveAndEnabled || !IsOccupied || BoostCharges <= 0) return false;
            BoostCharges--;
            // Repeated presses chain bursts; holding the button never consumes another charge.
            boostRemaining += Mathf.Max(.1f, boostDuration);
            Controller.maxSpeedMultiplier = normalMaxSpeedMultiplier * boostSpeedMultiplier;
            Controller.accelerationMultiplier = normalAccelerationMultiplier * boostAccelerationMultiplier;
            ApplyBoostVelocity(boostKickSpeed);
            return true;
        }

        void TickAbilities(float dt)
        {
            if (BoostCharges < boostCapacity)
            {
                rechargeTime += dt;
                float seconds = Mathf.Max(.1f, boostRechargeSeconds);
                while (rechargeTime >= seconds && BoostCharges < boostCapacity)
                { rechargeTime -= seconds; BoostCharges++; }
            }
            if (BoostCharges >= boostCapacity) rechargeTime = 0;
            if (boostRemaining > 0)
            {
                boostRemaining = Mathf.Max(0, boostRemaining - dt);
                if (!IsBoosting) StopBoost();
            }
        }

        void FixedUpdate()
        {
            if (!initialized) return;
            RefreshParking();
            if (HasJumpSupport()) AirJumpsRemaining = Mathf.Max(0, extraAirJumps);
            if (IsOccupied && IsBoosting) ApplyBoostVelocity(boostAcceleration * Time.fixedDeltaTime);
        }

        void ApplyBoostVelocity(float amount)
        {
            Vector3 direction = Vector3.ProjectOnPlane(Body.forward, Vector3.up).normalized;
            float limit = Controller.motorMaxSpeed * Controller.maxSpeedMultiplier;
            float speed = Vector3.Dot(Controller.getCarRigidBody().linearVelocity, direction);
            AddVelocity(direction * Mathf.Clamp(limit - speed, 0, Mathf.Max(0, amount)));
        }

        void AddVelocity(Vector3 delta)
        {
            Controller.getCarRigidBody().linearVelocity += delta;
            foreach (var wheel in Controller.wheels) wheel.getRigidbody().linearVelocity += delta;
        }

        void StopBoost()
        {
            boostRemaining = 0;
            if (!initialized || !Controller) return;
            Controller.maxSpeedMultiplier = normalMaxSpeedMultiplier;
            Controller.accelerationMultiplier = normalAccelerationMultiplier;
        }
    }
}
