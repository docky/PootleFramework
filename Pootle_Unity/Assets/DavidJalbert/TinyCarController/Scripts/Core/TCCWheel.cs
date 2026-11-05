using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DavidJalbert.TinyCarController
{
    public class TCCWheel : MonoBehaviour
    {
        [Header("Physics")]
        [Tooltip("Radius of the wheel collider.")]
        public float wheelRadius = 1f;
        [Tooltip("Mass of the wheel rigidbody. Should be lower than the mass of the car body.")]
        public float wheelMass = 1f;
        [Tooltip("Whether to use interpolation for the wheel rigidbody. Set this parameter to Interpolate to prevent jittering when moving.")]
        public RigidbodyInterpolation rigidbodyInterpolation = RigidbodyInterpolation.Interpolate;
        [Tooltip("Which collision detection mode to use for the wheel collider. Set this parameter to Continuous Dynamic to avoid going through other objects at high speed, or getting stuck.")]
        public CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        [Header("Suspension")]
        [Tooltip("Force applied to the suspension. Higher values make the suspension stiffer.")]
        public float suspensionSpring = 1000;
        [Tooltip("Damper applied to the suspension. Higher values make the suspension settle faster.")]
        public float suspensionDamper = 20;
        [Tooltip("Shifts the position of the wheel relative to its initial position. Useful to mimic hydraulics.")]
        public Vector3 suspensionOffset = Vector3.zero;
        [Header("Visuals")]
        [Tooltip("The object containing the wheel model. Will be locally rotated according to steering and velocity. Will use the first child object if this field is empty.")]
        public Transform wheelModel;
        [Tooltip("The maximum angle in degrees by which the wheel will turn when steering. A negative value will turn the wheel in the opposite direction.")]
        public float steeringMaxRotation = 0;
        [Tooltip("The speed at which the wheel will turn when steering. Set to zero to disable interpolation.")]
        public float steeringInterpolation = 0;

        [Header("Debug")]
        [Tooltip("Show or hide the generated components in the inspector when running the game.")]
        public bool showComponents;

        private Rigidbody carBody;
        private TCCWheelCollider internalCollider;
        private SphereCollider wheelCollider;
        private Rigidbody wheelBody;
        private ConfigurableJoint springJoint;
        private SoftJointLimitSpring springLimitSpring;
        private SoftJointLimit springLimit;
        private Vector3 initialAnchor;
        private TCCPlayer parentPlayer;
        private bool wasInitialized = false;
        private RigidbodyConstraints constraints = 0;
        private PhysicsMaterial wheelMaterial;
        private float currentWheelXAngle = 0;
        private float currentWheelYAngle = 0;
        private Vector3 relativePosition;
        private Quaternion relativeRotation;
        private Vector3 initialModelPosition;
        private Quaternion initialModelRotation;

        public Action<Collision> onCollisionStayActions;
        public Action<Collision> onCollisionEnterActions;
        public Action<Collision> onCollisionExitActions;
        public Action<Collider> onTriggerStayActions;
        public Action<Collider> onTriggerEnterActions;
        public Action<Collider> onTriggerExitActions;

        internal void reset()
        {
            wheelBody.centerOfMass = Vector3.down * wheelRadius;
            wheelBody.mass = wheelMass;
            wheelBody.linearDamping = 0;
            wheelBody.angularDamping = 0;
            wheelBody.useGravity = true;
            wheelBody.isKinematic = false;
            wheelBody.interpolation = rigidbodyInterpolation;
            wheelBody.collisionDetectionMode = collisionDetectionMode;
            wheelBody.constraints = constraints;

            wheelCollider.isTrigger = false;
            wheelCollider.material = wheelMaterial;
            wheelCollider.center = Vector3.zero;
            wheelCollider.radius = wheelRadius;

            springJoint.connectedBody = carBody;
            springJoint.anchor = Vector3.zero;
            springJoint.axis = Vector3.right;
            springJoint.secondaryAxis = Vector3.up;
            springJoint.autoConfigureConnectedAnchor = false;
            springJoint.connectedAnchor = initialAnchor + suspensionOffset;
            springJoint.enableCollision = false;
            springJoint.enablePreprocessing = false;
            springJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            springJoint.xMotion = ConfigurableJointMotion.Locked;
            springJoint.yMotion = ConfigurableJointMotion.Limited;
            springJoint.zMotion = ConfigurableJointMotion.Locked;
            springJoint.angularXMotion = ConfigurableJointMotion.Locked;
            springJoint.angularYMotion = ConfigurableJointMotion.Locked;
            springJoint.angularZMotion = ConfigurableJointMotion.Locked;

            springLimitSpring = new SoftJointLimitSpring();
            springLimitSpring.spring = suspensionSpring;
            springLimitSpring.damper = suspensionDamper;
            springJoint.linearLimitSpring = springLimitSpring;

            springLimit = new SoftJointLimit();
            springLimit.limit = 0.001f;
            springLimit.bounciness = 0;
            springLimit.contactDistance = 1000;
            springJoint.linearLimit = springLimit;

            foreach (Collider c in GetComponentsInChildren<Collider>())
            {
                if (c != wheelCollider) c.enabled = false;
            }

            foreach (Collider c in carBody.GetComponentsInChildren<Collider>())
            {
                Physics.IgnoreCollision(wheelCollider, c);
            }
        }

        internal void initialize(TCCPlayer parent)
        {
            if (wheelModel == null && transform.childCount > 0) wheelModel = transform.GetChild(0);
            if (wheelModel != null)
            {
                initialModelPosition = wheelModel.localPosition;
                initialModelRotation = wheelModel.localRotation;
            }

            initialAnchor = Quaternion.Inverse(parent.getCarBody().transform.rotation) * (transform.position - parent.getCarBody().transform.position);
            initialAnchor.x /= transform.lossyScale.x;
            initialAnchor.y /= transform.lossyScale.y;
            initialAnchor.z /= transform.lossyScale.z;

            parentPlayer = parent;

            carBody = parentPlayer.getCarBody().getRigidbody();

            // get initial position and rotation

            relativeRotation = Quaternion.Inverse(carBody.transform.rotation) * transform.rotation;
            relativePosition = Quaternion.Inverse(carBody.transform.rotation) * (transform.position - carBody.transform.position);
            
            // physics components

            wheelMaterial = new PhysicsMaterial()
            {
                bounceCombine = PhysicsMaterialCombine.Minimum,
                bounciness = 0,
                dynamicFriction = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                staticFriction = 0
            };

            wheelBody = gameObject.AddComponent<Rigidbody>();
            wheelCollider = gameObject.AddComponent<SphereCollider>();
            wheelCollider.enabled = true;
            wheelCollider.material = wheelMaterial;
            springJoint = gameObject.AddComponent<ConfigurableJoint>();

            internalCollider = gameObject.AddComponent<TCCWheelCollider>();
            internalCollider.initialize(this);

            wasInitialized = true;

            reset();
        }

        void OnEnable()
        {
            if (wasInitialized) reset();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, wheelRadius);
        }

        void Update()
        {
            if (!wasInitialized) return;

            updateVisuals(Time.deltaTime);
        }

        internal void updateVisuals(float delta)
        {
            // show/hide components in inspector.

            HideFlags componentsHideFlag = showComponents ? HideFlags.None : HideFlags.HideInHierarchy | HideFlags.HideInInspector;
            wheelBody.hideFlags = componentsHideFlag;
            wheelCollider.hideFlags = componentsHideFlag;
            springJoint.hideFlags = componentsHideFlag;
            internalCollider.hideFlags = componentsHideFlag;

            // rotate wheel model.

            if (wheelModel != null)
            {
                float fullRotationLength = wheelRadius * 2 * Mathf.PI;
                float rotationPerSecond = (parentPlayer.getForwardVelocity() / fullRotationLength) * (1f - parentPlayer.getSmoothBrake());

                currentWheelXAngle = Mathf.Repeat(currentWheelXAngle + rotationPerSecond * 360 * Time.deltaTime, 360);
                Quaternion wheelXRotation = Quaternion.Euler(currentWheelXAngle, 0, 0);

                if (steeringInterpolation > 0) currentWheelYAngle = Mathf.Lerp(currentWheelYAngle, parentPlayer.getSteering() * steeringMaxRotation, Mathf.Clamp01(steeringInterpolation * Time.deltaTime));
                else currentWheelYAngle = parentPlayer.getSteering() * steeringMaxRotation;
                Quaternion wheelYRotation = Quaternion.Euler(0, currentWheelYAngle, 0);

                wheelModel.transform.localRotation = wheelYRotation * wheelXRotation * initialModelRotation;
            }
        }

        void FixedUpdate()
        {
            if (!wasInitialized) return;

            refreshPhysics(Time.fixedDeltaTime);
        }

        internal void refreshPhysics(float delta)
        {
            if (!wasInitialized) return;

            wheelBody.mass = wheelMass;
            wheelBody.interpolation = rigidbodyInterpolation;
            wheelBody.collisionDetectionMode = collisionDetectionMode;
            wheelBody.constraints = constraints;
            springLimitSpring.spring = suspensionSpring;
            springLimitSpring.damper = suspensionDamper;
            springJoint.linearLimitSpring = springLimitSpring;
            springJoint.connectedAnchor = initialAnchor + suspensionOffset;
        }

        /// <summary>
        /// Returns the <c>Rigidbody</c> component associated with the object.
        /// </summary>
        public Rigidbody getRigidbody()
        {
            return wheelBody;
        }

        /// <summary>
        /// Sets the <c>RigidbodyConstraints</c> parameter for the object's <c>Rigidbody</c>. By default, this parameter is null.
        /// </summary>
        public void setConstraints(RigidbodyConstraints c)
        {
            constraints = c;
        }

        /// <summary>
        /// Returns the <c>TCCPlayer</c> object associated with this object.
        /// </summary>
        public TCCPlayer getParentPlayer()
        {
            return parentPlayer;
        }

        /// <summary>
        /// Returns the <c>SphereCollider</c> associated with this object.
        /// </summary>
        public SphereCollider getCollider()
        {
            return wheelCollider;
        }

        /// <summary>
        /// Returns the radius of the <c>SphereCollider</c> associated with this object.
        /// </summary>
        public float getColliderRadius()
        {
            return wheelCollider.radius;
        }

        /// <summary>
        /// Returns whether the wheel is touching a surface.
        /// </summary>
        public bool isTouchingGround()
        {
            return internalCollider.isTouchingGround();
        }

        /// <summary>
        /// Returns whether the wheel is touching a surface at a maximum lateral angle of 45 degrees. Will return false if the wheel is touching the side of a curb, for instance.
        /// </summary>
        public bool isStableOnGround()
        {
            return internalCollider.isStableOnGround();
        }

        /// <summary>
        /// Returns the position of this object in world space.
        /// </summary>
        public Vector3 getPosition()
        {
            return wheelBody.position;
        }

        /// <summary>
        /// Returns the rotation of this object in world space.
        /// </summary>
        public Quaternion getRotation()
        {
            return wheelBody.rotation;
        }

        /// <summary>
        /// Sets the linear and angular velocities of this object to zero.
        /// </summary>
        public void immobilize()
        {
            wheelBody.linearVelocity = Vector3.zero;
            wheelBody.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Returns a <c>TCCSurfaceModifier</c> object representing the average surface values this wheel is currently standing on.
        /// </summary>
        public TCCSurfaceModifier getSurfaceModifier()
        {
            return internalCollider.getSurfaceModifier();
        }

        // ---

        internal Vector3 getRelativePosition()
        {
            return relativePosition;
        }

        internal Quaternion getRelativeRotation()
        {
            return relativeRotation;
        }

        internal void setRelativePosition(Vector3 v)
        {
            wheelBody.position = v + carBody.rotation * relativePosition;
        }

        internal void setRelativeRotation(Quaternion q)
        {
            wheelBody.rotation = q;
        }
    }
}