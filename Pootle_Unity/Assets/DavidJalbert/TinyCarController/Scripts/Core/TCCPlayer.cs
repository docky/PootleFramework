using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DavidJalbert.TinyCarController
{
    public class TCCPlayer : MonoBehaviour
    {
        public enum CounteringMode
        {
            Never, Ungrounded, NotFullyGrounded, Always
        }

        [Header("Components")]
        [Tooltip("Object containing the TCCBody script. If empty, will search for a suitable child.")]
        public TCCBody carBody;
        [Tooltip("Objects containing the TCCWheel scripts. If empty, will search for suitable children.")]
        public TCCWheel[] wheels;

        [Header("Motor")]
        [Tooltip("The maximum speed at which the car should go.")]
        public float motorMaxSpeed = 20;
        [Tooltip("The base acceleration speed of the car.")]
        public float motorAcceleration = 100;
        [Tooltip("How fast the car should slow down when braking.")]
        public float brakeForce = 200;
        [Tooltip("How much smoothing to apply to the motor input. Set to zero to change the motor input instantly.")]
        public float motorInputSmoothing = 10;
        [Tooltip("How much smoothing to apply to the brake input. Set to zero to change the brake input instantly.")]
        public float brakeInputSmoothing = 0;
        [Tooltip("How fast the car should slow down when its speed is above the maximum speed.")]
        public float speedOverMaxDamping = 100;
        [Tooltip("The multiplier to apply to the acceleration (Y axis) depending on the current speed of the car relative to its maximum speed (X axis).")]
        public AnimationCurve accelerationOverSpeed = new AnimationCurve()
        {
            keys = new Keyframe[]{
            new Keyframe { time = 0, value = 1},
            new Keyframe() { time = 1, value = 0}
        }
        };
        [Tooltip("How quickly the car will slow down when the motor is not accelerating.")]
        public float speedGroundDamping = 10f;
        [Tooltip("How quickly the car will slow down on its local Z axis when in the air.")]
        public float speedAirDamping = 0;
        [Tooltip("The slope angle at which the downhill multipliers will be applied fully. Will be clamped between 0 and 90 degrees.")]
        public float downhillMaxAngle = 90f;
        [Tooltip("The multiplier to apply to the max speed when going downhill. Will be interpolated between 0 and 'Downhill Max Angle'.")]
        public float downhillMaxSpeedMultiplier = 1.25f;
        [Tooltip("The multiplier to apply to the acceleration when going downhill. Will be interpolated between 0 and 'Downhill Max Angle'.")]
        public float downhillAccelerationMultiplier = 2f;
        [Tooltip("The slope angle at which the uphill multipliers will be applied fully. Will be clamped between 0 and 90 degrees.")]
        public float uphillMaxAngle = 90f;
        [Tooltip("The multiplier to apply to the max speed when going uphill. Will be interpolated between 0 and 'Uphill Max Angle'.")]
        public float uphillMaxSpeedMultiplier = 0.75f;
        [Tooltip("The multiplier to apply to the acceleration when going uphill. Will be interpolated between 0 and 'Uphill Max Angle'.")]
        public float uphillAccelerationMultiplier = 0.25f;

        [Header("Steering")]
        [Tooltip("How much smoothing to apply to the steering input. Set to zero to change the steering input instantly.")]
        public float steeringInputSmoothing = 10;
        [Tooltip("How fast the car will turn when steering.")]
        public float steeringSpeed = 100;
        [Tooltip("How fast the car will turn when steering in the air.")]
        public float steeringAirSpeed = 20;
        [Tooltip("The minimum speed of the car at which it can steer fully. Below this value, the car will steer progressively slower as the speed goes down.")]
        public float steeringMinSpeed = 5;
        [Tooltip("How fast to slow down the car rotation while in the air.")]
        public float steeringAirDamping = 5;
        [Tooltip("How much the wheels will stick to the surface. Lower values will make the car drift more.")]
        public float tireGrip = 100;
        [Tooltip("The multiplier to apply to the tire grip (Y axis) depending on the current speed of the car relative to its maximum speed (X axis).")]
        public AnimationCurve tireGripOverSpeed = new AnimationCurve()
        {
            keys = new Keyframe[]{
            new Keyframe { time = 0, value = 1},
            new Keyframe() { time = 1, value = 1}
        }
        };
        [Tooltip("Minimum speed at which the car will move laterally (drifting) before it's set to zero. Helps stabilize the car on slippery surfaces.")]
        public float driftMinVelocity = 0.1f;
        [Tooltip("How quickly the car will slow down on its local X axis when in the air.")]
        public float driftAirDamping = 0;

        [Header("Roll Countering")]
        [Tooltip("When the car should stabilize itself by countering its roll angle. Set the parameter to Never to disable this feature.")]
        public CounteringMode rollCounterMode = CounteringMode.NotFullyGrounded;
        [Tooltip("The speed at which the car should turn when countering its roll angle.")]
        public float rollCounterSpeed = 10;
        [Tooltip("How fast the car will reach its maximum turn speed when countering its roll angle.")]
        public float rollCounterAcceleration = 10;
        [Tooltip("The angle at which the car will try to set itself when countering its roll angle.")]
        public float rollCounterAngle = 0;

        [Header("Pitch Countering")]
        [Tooltip("When the car should stabilize itself by countering its pitch angle. Set the parameter to Never to disable this feature.")]
        public CounteringMode pitchCounterMode = CounteringMode.NotFullyGrounded;
        [Tooltip("The speed at which the car should turn when countering its pitch angle.")]
        public float pitchCounterSpeed = 10;
        [Tooltip("How fast the car will reach its maximum turn speed when countering its pitch angle.")]
        public float pitchCounterAcceleration = 10;
        [Tooltip("The angle at which the car will try to set itself when countering its pitch angle.")]
        public float pitchCounterAngle = 0;

        [Header("Modifiers")]
        [Tooltip("Multiplies the max speed of the car by this value. Useful to simulate a temporary boost.")]
        public float maxSpeedMultiplier = 1;
        [Tooltip("Multiplies the acceleration of the car by this value.")]
        public float accelerationMultiplier = 1;
        [Tooltip("Multiplies the tire grip of the car by this value.")]
        public float tireGripMultiplier = 1;

        // ---

        private Vector3 bodyForward, bodyRight, bodyUp;
        private float groundVelocity;
        private float velocityForward, velocityRight, velocityUp;
        private float bodyPitch, bodyRoll, bodyYaw;

        private float inputEngine, inputSteering, inputBrake;
        private float lerpedInputEngine, lerpedInputSteering, lerpedInputBrake;

        void Awake()
        {
            if (carBody == null) carBody = GetComponentInChildren<TCCBody>();
            if (wheels == null || wheels.Length == 0) wheels = GetComponentsInChildren<TCCWheel>();

            carBody.initialize(this);

            foreach (TCCWheel wheel in wheels)
            {
                wheel.initialize(this);
            }
        }

        void Update()
        {
            updateVisuals(Time.deltaTime);
        }

        void FixedUpdate()
        {
            updatePhysics(Time.fixedDeltaTime);
        }

        internal void updateVisuals(float delta) { }

        internal void updatePhysics(float delta)
        {
            // initial directions and velocities

            bodyForward = getCarRigidBody().transform.forward;
            bodyRight = getCarRigidBody().transform.right;
            bodyUp = getCarRigidBody().transform.up;

            groundVelocity = new Vector3(getCarRigidBody().linearVelocity.x, 0, getCarRigidBody().linearVelocity.z).magnitude;

            velocityForward = Vector3.Dot(getCarRigidBody().linearVelocity, bodyForward);
            velocityRight = Vector3.Dot(getCarRigidBody().linearVelocity, bodyRight);
            velocityUp = Vector3.Dot(getCarRigidBody().linearVelocity, bodyUp);

            bodyPitch = Vector3.Dot(bodyForward, Vector3.up);
            bodyYaw = Vector3.Dot(bodyForward, Vector3.forward);
            bodyRoll = Vector3.Dot(bodyRight, Vector3.up);

            float torqueX = Vector3.Dot(getCarRigidBody().angularVelocity, bodyRight);
            float torqueY = Vector3.Dot(getCarRigidBody().angularVelocity, bodyUp);
            float torqueZ = Vector3.Dot(getCarRigidBody().angularVelocity, bodyForward);

            float inverseDelta = 1f / delta;

            // initialize surface modifiers

            TCCSurfaceModifier surfaceModifier = getSurfaceModifier();

            float maxSpeedTotalMultiplier = maxSpeedMultiplier * surfaceModifier.maxSpeedModifier;
            float accelerationTotalMultiplier = accelerationMultiplier * surfaceModifier.accelerationModifier;
            float tireGripTotalMultiplier = tireGripMultiplier * surfaceModifier.tireGripModifier;

            // acceleration and max speed over pitch multiplier

            float motorSign = Mathf.Sign(getSmoothMotor());
            if (motorSign == 0) motorSign = 1f;
            float motorForce = Mathf.Abs(getSmoothMotor());

            float forwardPitchSign = Mathf.Lerp(Mathf.Sign(velocityForward), motorSign, motorForce) * Mathf.Sign(getCarBody().getPitchAngle());
            //forwardPitchSign = Mathf.Lerp(forwardPitchSign, motorSign, motorForce);
            float forwardPitchDelta = 0;

            if (forwardPitchSign > 0)
            {
                // going downhill
                forwardPitchDelta = Mathf.Clamp01(Mathf.Abs(Mathf.Sign(velocityForward) * getCarBody().getPitchAngle() / Mathf.Clamp(downhillMaxAngle, 0, 90)));
                
                accelerationTotalMultiplier *= Mathf.Lerp(1, downhillAccelerationMultiplier, forwardPitchDelta);
                maxSpeedTotalMultiplier *= Mathf.Lerp(1, downhillMaxSpeedMultiplier, forwardPitchDelta);
            }
            else
            {
                // going uphill
                forwardPitchDelta = Mathf.Clamp01(Mathf.Abs(Mathf.Sign(velocityForward) * getCarBody().getPitchAngle() / Mathf.Clamp(uphillMaxAngle, 0, 90)));
                
                accelerationTotalMultiplier *= Mathf.Lerp(1, uphillAccelerationMultiplier, forwardPitchDelta);
                maxSpeedTotalMultiplier *= Mathf.Lerp(1, uphillMaxSpeedMultiplier, forwardPitchDelta);
            }
            
            // input lerp

            lerpedInputEngine = Mathf.Lerp(lerpedInputEngine, inputEngine, motorInputSmoothing > 0 ? Mathf.Clamp(delta * motorInputSmoothing, 0, 1) : 1);
            lerpedInputSteering = Mathf.Lerp(lerpedInputSteering, inputSteering, steeringInputSmoothing > 0 ? Mathf.Clamp(delta * steeringInputSmoothing, 0, 1) : 1);
            lerpedInputBrake = Mathf.Lerp(lerpedInputBrake, inputBrake, brakeInputSmoothing > 0 ? Mathf.Clamp(delta * brakeInputSmoothing, 0, 1) : 1);

            // motor

            float targetVelocityForward = 0;
            float newVelocityForward = 0;

            if (Mathf.Abs(velocityForward) > motorMaxSpeed * maxSpeedTotalMultiplier)
            {
                newVelocityForward = -velocityForward * speedOverMaxDamping * delta;
            }
            else if (!isUngrounded())
            {
                float throttleVelocity = 0;
                float idleVelocity = 0;

                float accelerationDelta = accelerationOverSpeed.Evaluate(Mathf.Clamp(Mathf.Abs(velocityForward / (motorMaxSpeed * maxSpeedTotalMultiplier)), 0, 1));

                float currentAcceleration = accelerationTotalMultiplier * inputEngine * accelerationDelta * motorAcceleration * delta;
                throttleVelocity = velocityForward + currentAcceleration;

                idleVelocity = Mathf.MoveTowards(velocityForward, 0, speedGroundDamping * delta);

                targetVelocityForward = idleVelocity;
                targetVelocityForward = Mathf.Lerp(targetVelocityForward, throttleVelocity, Mathf.Abs(lerpedInputEngine));

                newVelocityForward = (targetVelocityForward - velocityForward) * inverseDelta;
            }
            else
            {
                newVelocityForward = -velocityForward * speedAirDamping * delta;
            }

            getCarRigidBody().AddForce(bodyForward * newVelocityForward * delta, ForceMode.Impulse);

            // brake

            if (!isUngrounded())
            {
                float brakeDelta = brakeForce * lerpedInputBrake * delta;
                getCarRigidBody().AddForce(bodyForward * brakeDelta * -velocityForward + bodyRight * brakeDelta * -velocityRight, ForceMode.Impulse);
            }

            // steering

            float targetTorqueUp = 0;
            if (isUngrounded()) targetTorqueUp = lerpedInputSteering * steeringAirSpeed * delta;
            else targetTorqueUp = lerpedInputSteering * steeringSpeed * Mathf.Clamp(velocityForward / steeringMinSpeed, -1, 1) * delta;

            float newTorqueUp = (targetTorqueUp - torqueY) * inverseDelta;

            if (isUngrounded())
            {
                newTorqueUp *= Mathf.Clamp01(steeringAirDamping * delta);
            }

            getCarRigidBody().AddTorque(bodyUp * newTorqueUp * delta, ForceMode.Impulse);

            // drift speed

            float tireGripDelta = tireGripOverSpeed.Evaluate(Mathf.Clamp(Mathf.Abs(velocityForward / motorMaxSpeed), 0, 1));
            float targetVelocityRight = Mathf.MoveTowards(velocityRight, 0, tireGrip * tireGripDelta * tireGripTotalMultiplier * delta);
            if (Mathf.Abs(targetVelocityRight) < driftMinVelocity) targetVelocityRight = 0;

            float newVelocityRight = 0;
            if (!isUngrounded())
            {
                newVelocityRight = (targetVelocityRight - velocityRight) * inverseDelta;
            }
            else
            {
                newVelocityRight = -velocityRight * driftAirDamping * delta;
            }

            getCarRigidBody().AddForce(bodyRight * newVelocityRight * delta, ForceMode.Impulse);

            // roll counter

            float rollDesiredAngle = getRollAngle() - rollCounterAngle;
            float rollDelta = Mathf.Sign(rollDesiredAngle) * Mathf.Pow(Mathf.Clamp(rollDesiredAngle / 90, -1, 1), 2);

            if (canCounterRoll())
            {
                float targetRollTorque = -rollDelta * rollCounterSpeed;
                getCarRigidBody().AddTorque(bodyForward * (targetRollTorque - torqueZ) * rollCounterAcceleration * delta, ForceMode.Impulse);
            }

            // pitch counter

            float pitchDesiredAngle = getPitchAngle() - pitchCounterAngle;
            float pitchDelta = Mathf.Sign(pitchDesiredAngle) * Mathf.Pow(Mathf.Clamp(pitchDesiredAngle / 90, -1, 1), 2);

            if (canCounterPitch())
            {
                float targetPitchTorque = -pitchDelta * pitchCounterSpeed;
                getCarRigidBody().AddTorque(bodyRight * (targetPitchTorque - torqueX) * pitchCounterAcceleration * delta, ForceMode.Impulse);
            }
        }

        internal bool canCounterPitch()
        {
            switch (pitchCounterMode)
            {
                case CounteringMode.Always: return true;
                case CounteringMode.Ungrounded: return isUngrounded();
                case CounteringMode.NotFullyGrounded: return !isFullyGrounded();
            }
            return false;
        }

        internal bool canCounterRoll()
        {
            switch (rollCounterMode)
            {
                case CounteringMode.Always: return true;
                case CounteringMode.Ungrounded: return isUngrounded();
                case CounteringMode.NotFullyGrounded: return !isFullyGrounded();
            }
            return false;
        }

        /// <summary>
        /// Returns the <c>TCCBody</c> component associated with this object.
        /// </summary>
        public TCCBody getCarBody()
        {
            return carBody;
        }

        /// <summary>
        /// Returns the <c>Rigidbody</c> component associated with this object.
        /// </summary>
        public Rigidbody getCarRigidBody()
        {
            return carBody.getRigidbody();
        }

        /// <summary>
        /// Returns a <c>TCCSurfaceModifier</c> object representing the average surface values this vehicle is currently standing on.
        /// </summary>
        public TCCSurfaceModifier getSurfaceModifier()
        {
            TCCSurfaceModifier surface = new TCCSurfaceModifier();
            if (wheels.Length > 0)
            {
                surface.clear();
                foreach (TCCWheel wheel in wheels)
                {
                    surface += wheel.getSurfaceModifier();
                }
                surface /= wheels.Length;
            }
            return surface;
        }

        /// <summary>
        /// Returns the linear velocity of this object in world space.
        /// </summary>
        public Vector3 getVelocity()
        {
            return getCarRigidBody().linearVelocity;
        }

        /// <summary>
        /// Returns the velocity of this object on its local Z axis.
        /// </summary>
        public float getForwardVelocity()
        {
            return Vector3.Dot(getCarRigidBody().linearVelocity, getCarRigidBody().transform.forward);
        }

        /// <summary>
        /// Returns the velocity of this object on its local X axis.
        /// </summary>
        public float getRightVelocity()
        {
            return Vector3.Dot(getCarRigidBody().linearVelocity, getCarRigidBody().transform.right);
        }

        /// <summary>
        /// Returns the velocity of this object on its local Y axis.
        /// </summary>
        public float getUpVelocity()
        {
            return Vector3.Dot(getCarRigidBody().linearVelocity, getCarRigidBody().transform.up);
        }

        /// <summary>
        /// Returns the velocity of this object on the XZ plane in world space.
        /// </summary>
        public float getGroundVelocity()
        {
            return groundVelocity;
        }

        /// <summary>
        /// Returns the angle in degrees of the vehicle on its local X axis.
        /// </summary>
        public float getPitchAngle()
        {
            return getCarBody()?.getPitchAngle() ?? 0;
        }

        /// <summary>
        /// Returns the angle in degrees of the vehicle on its local Z axis.
        /// </summary>
        public float getRollAngle()
        {
            return getCarBody()?.getRollAngle() ?? 0;
        }

        /// <summary>
        /// Returns the angular velocity of the vehicle on its local X axis.
        /// </summary>
        public float getPitchVelocity()
        {
            return Vector3.Dot(getCarRigidBody().angularVelocity, getCarRigidBody().transform.right);
        }

        /// <summary>
        /// Returns the angular velocity of the vehicle on its local Z axis.
        /// </summary>
        public float getRollVelocity()
        {
            return Vector3.Dot(getCarRigidBody().angularVelocity, getCarRigidBody().transform.forward);
        }

        /// <summary>
        /// Returns the angular velocity of the vehicle on its local Y axis.
        /// </summary>
        public float getYawVelocity()
        {
            return Vector3.Dot(getCarRigidBody().angularVelocity, getCarRigidBody().transform.up);
        }

        /// <summary>
        /// Returns whether the vehicle has at least one wheel on the ground but not all of them.
        /// </summary>
        public bool isPartiallyGrounded()
        {
            return !isUngrounded() && !isFullyGrounded();
        }

        /// <summary>
        /// Returns whether the vehicle's wheels are all off the ground.
        /// </summary>
        public bool isUngrounded()
        {
            foreach (TCCWheel w in wheels)
            {
                if (w.isStableOnGround()) return false;
            }
            return true;
        }

        /// <summary>
        /// Returns whether the vehicle's wheels are all on the ground.
        /// </summary>
        public bool isFullyGrounded()
        {
            foreach (TCCWheel w in wheels)
            {
                if (!w.isStableOnGround()) return false;
            }
            return true;
        }

        /// <summary>
        /// Sets the input of the motor between -1 and 1, where 1 is going at full speed, and -1 is going fully in reverse.
        /// </summary>
        public void setMotor(float d)
        {
            inputEngine = d;
        }

        /// <summary>
        /// Sets the input of the steering between -1 and 1, where -1 is fully to the left, and 1 is fully to the right. Zero is centered.
        /// </summary>
        public void setSteering(float d)
        {
            inputSteering = d;
        }

        /// <summary>
        /// Sets the input of the brakes between 0 and 1, where 0 is not braking at all, and 1 is fully braking.
        /// </summary>
        public void setBrake(float d)
        {
            inputBrake = d;
        }

        /// <summary>
        /// Gets the current input of the motor.
        /// </summary>
        public float getMotor()
        {
            return inputEngine;
        }

        /// <summary>
        /// Gets the current input of the steering.
        /// </summary>
        public float getSteering()
        {
            return inputSteering;
        }

        /// <summary>
        /// Gets the current input of the brakes.
        /// </summary>
        public float getBrake()
        {
            return inputBrake;
        }

        /// <summary>
        /// Gets the current input of the motor with smoothing applied.
        /// </summary>
        public float getSmoothMotor()
        {
            return lerpedInputEngine;
        }

        /// <summary>
        /// Gets the current input of the steering with smoothing applied.
        /// </summary>
        public float getSmoothSteering()
        {
            return lerpedInputSteering;
        }

        /// <summary>
        /// Gets the current input of the brakes with smoothing applied.
        /// </summary>
        public float getSmoothBrake()
        {
            return lerpedInputBrake;
        }

        /// <summary>
        /// Sets the linear and angular velocities of the vehicle to zero.
        /// </summary>
        public void immobilize()
        {
            inputEngine = 0;
            inputBrake = 0;
            inputSteering = 0;
            lerpedInputEngine = 0;
            lerpedInputBrake = 0;
            lerpedInputSteering = 0;

            carBody.immobilize();
            foreach (TCCWheel w in wheels)
            {
                w.immobilize();
            }
        }

        /// <summary>
        /// Returns the position of the vehicle in world space.
        /// </summary>
        public Vector3 getPosition()
        {
            return carBody.getPosition();
        }

        /// <summary>
        /// Sets the position of the vehicle in world space.
        /// </summary>
        public void setPosition(Vector3 v)
        {
            carBody.setPosition(v);
            foreach (TCCWheel w in wheels)
            {
                w.setRelativePosition(v);
            }
        }

        /// <summary>
        /// Returns the rotation of the vehicle in world space.
        /// </summary>
        public Quaternion getRotation()
        {
            return carBody.getRotation();
        }

        /// <summary>
        /// Sets the rotation of the vehicle in world space.
        /// </summary>
        public void setRotation(Quaternion q)
        {
            carBody.setRotation(q);
            foreach (TCCWheel w in wheels)
            {
                w.setRelativeRotation(q);
            }
        }

        /// <summary>
        /// Moves the vehicle by the value provided.
        /// </summary>
        public void translate(Vector3 v)
        {
            setPosition(getPosition() + v);
        }
        
        /// <summary>
        /// Rotates the vehicle by the value provided.
        /// </summary>
        public void rotate(Quaternion q)
        {
            setRotation(getRotation() * q);
        }
    }
}