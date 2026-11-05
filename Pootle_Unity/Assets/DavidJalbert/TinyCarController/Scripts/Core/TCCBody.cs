using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace DavidJalbert.TinyCarController
{
    public class TCCBody : MonoBehaviour
    {
        [Header("Body parameters")]

        [Tooltip("The mass that will be applied to the body.")]
        public float bodyMass = 10;
        [Tooltip("Whether to apply interpolation to the body. Set this parameter to Interpolate to prevent jittering when moving.")]
        public RigidbodyInterpolation rigidbodyInterpolation = RigidbodyInterpolation.Interpolate;
        [Tooltip("Which collision detection mode to use on the body. Set this parameter to Continuous Dynamic to avoid going through other objects at high speed, or getting stuck.")]
        public CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        [Tooltip("The center of mass of the body in local space. This parameter changes how the rigidbody behaves when not on stable ground. Ideally this should be the center of the car at ground level.")]
        public Vector3 centerOfMass = new Vector3(0, 0, 0);

        [Header("Visuals")]

        [Tooltip("The object containing the car model. Will use the first child object if this field is empty.")]
        public Transform carModel;

        // ---

        private Rigidbody carBody;
        private bool wasInitialized = false;
        private TCCPlayer parentPlayer = null;
        private TCCBodyCollider internalCollider = null;
        private RigidbodyConstraints constraints = 0;
        private Vector3 massOffset = Vector3.zero;
        private Vector3 initialPosition = Vector3.zero;
        private Quaternion initialRotation = Quaternion.identity;

        public Action<Collision> onCollisionStayActions;
        public Action<Collision> onCollisionEnterActions;
        public Action<Collision> onCollisionExitActions;
        public Action<Collider> onTriggerStayActions;
        public Action<Collider> onTriggerEnterActions;
        public Action<Collider> onTriggerExitActions;

        void Update()
        {
            
        }
        
        void FixedUpdate()
        {
            if (!wasInitialized) return;

            refreshPhysics(Time.fixedDeltaTime);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.TransformPoint(centerOfMass));
            Gizmos.DrawWireSphere(transform.TransformPoint(centerOfMass), 0.1f);
        }

        /// <summary>
        /// Returns the <c>Rigidbody</c> component associated with the object.
        /// </summary>
        public Rigidbody getRigidbody()
        {
            if (carBody == null) carBody = GetComponentInChildren<Rigidbody>();
            return carBody;
        }

        /// <summary>
        /// Sets the <c>RigidbodyConstraints</c> parameter for the object's <c>Rigidbody</c>. By default, this parameter is null.
        /// </summary>
        public void setConstraints(RigidbodyConstraints c)
        {
            constraints = c;
        }

        /// <summary>
        /// Returns the angle in degrees of the vehicle on its local X axis.
        /// </summary>
        public float getPitchAngle()
        {
            Vector3 carRight = carBody.transform.right;
            carRight.y = 0;
            Vector3 carForward = Vector3.Cross(carRight, Vector3.up).normalized;
            return -Vector3.Angle(carForward, carBody.transform.forward) * Mathf.Sign(carBody.transform.forward.y);
        }

        /// <summary>
        /// Returns the angle in degrees of the vehicle on its local Z axis.
        /// </summary>
        public float getRollAngle()
        {
            Vector3 carForward = carBody.transform.forward;
            carForward.y = 0;
            carForward *= Mathf.Sign(carBody.transform.up.y);
            Vector3 carRight = Vector3.Cross(Vector3.up, carForward).normalized;
            return Vector3.Angle(carRight, carBody.transform.right) * Mathf.Sign(carBody.transform.right.y);
        }

        /// <summary>
        /// Returns the <c>TCCPlayer</c> object associated with this object.
        /// </summary>
        public TCCPlayer getParentPlayer()
        {
            return parentPlayer;
        }

        /// <summary>
        /// Sets the linear and angular velocities of this object to zero.
        /// </summary>
        public void immobilize()
        {
            carBody.linearVelocity = Vector3.zero;
            carBody.angularVelocity = Vector3.zero;
        }

        /// <summary>
        /// Returns the position of this object in world space.
        /// </summary>
        public Vector3 getPosition()
        {
            return carBody.position;
        }

        /// <summary>
        /// Returns the rotation of this object in world space.
        /// </summary>
        public Quaternion getRotation()
        {
            return carBody.rotation;
        }

        // ---

        internal void initialize(TCCPlayer parent)
        {
            parentPlayer = parent;

            if (carModel == null && transform.childCount > 0) carModel = transform.GetChild(0);
            if (carModel != null)
            {
                initialPosition = carModel.localPosition;
                initialRotation = carModel.localRotation;
            }

            if (getRigidbody() == null)
            {
                carBody = gameObject.AddComponent<Rigidbody>();
            }

            carBody.centerOfMass = centerOfMass;
            carBody.mass = bodyMass;
            carBody.linearDamping = 0;
            carBody.angularDamping = 0;
            carBody.useGravity = true;
            carBody.isKinematic = false;
            carBody.interpolation = rigidbodyInterpolation;
            carBody.collisionDetectionMode = collisionDetectionMode;

            foreach (MeshCollider c in GetComponentsInChildren<MeshCollider>())
            {
                c.convex = true;
                c.enabled = true;
            }

            internalCollider = gameObject.AddComponent<TCCBodyCollider>();
            internalCollider.initialize(this);

            wasInitialized = true;
        }

        internal void refreshPhysics(float delta)
        {
            if (!wasInitialized) return;

            carBody.isKinematic = false;
            carBody.centerOfMass = centerOfMass + massOffset;
            carBody.mass = bodyMass;
            carBody.angularDamping = 0;
            carBody.interpolation = rigidbodyInterpolation;
            carBody.collisionDetectionMode = collisionDetectionMode;
            carBody.constraints = constraints;
        }

        internal void setPosition(Vector3 v)
        {
            carBody.position = v;
        }

        internal void setRotation(Quaternion q)
        {
            carBody.rotation = q;
        }
    }
}