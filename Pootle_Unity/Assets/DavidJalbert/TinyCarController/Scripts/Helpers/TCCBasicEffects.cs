using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DavidJalbert.TinyCarController
{
    public class TCCBasicEffects : MonoBehaviour
    {
        [Tooltip("The TCCPlayer script associated with this object. If empty, will search for a suitable parent.")]
        public TCCPlayer playerObject;

        [Header("Engine")]
        [Tooltip("Audio source for the engine.")]
        public AudioSource audioEngine;
        [Tooltip("Pitch to which to set the engine audio when not moving.")]
        public float minEnginePitch = 0.25f;
        [Tooltip("Pitch to which to set the engine audio when moving at full speed.")]
        public float maxEnginePitch = 1f;
        [Tooltip("How much to interpolate the pitch of the engine audio.")]
        public float enginePitchInterpolation = 10f;
        [Tooltip("How velocity and input will be blended together to calculate the pitch of the engine sound. For instance, a value of 0.75 will blend 75% of the velocity and 25% of the input together.")]
        public float velocityToInputRatio = 0.66f;

        [Header("Braking and skid marks")]
        [Tooltip("Audio source for the tires screeching.")]
        public AudioSource audioScreeching;
        [Tooltip("How much to interpolate the volume of the tire audio.")]
        public float screechingVolumeInterpolation = 5;
        [Tooltip("Prefab for the skid mark. Must be a Trail Renderer that automatically destroys itself when not moving.")]
        public TrailRenderer skidMarkPrefab;
        [Tooltip("Prefab for the tire smoke. Must be a Particle System that loops indefinitely.")]
        public ParticleSystem smokePrefab;
        [Tooltip("Minimum velocity at which to enable the tire screeching effect.")]
        public float minScreechingVelocity = 3;
        [Tooltip("Minimum lateral delta value at which to enable the tire screeching effect. Equals to the difference between the lateral and forward velocities, between 0 and 1, where 0 is completely straight and 1 is completely sideways.")]
        public float minScreechingLateralDelta = 0.5f;

        [Header("Collisions")]
        [Tooltip("Audio source for the body hitting something.")]
        public AudioSource audioHit;
        [Tooltip("Prefab for the collision particles. Must be a Particle System that plays once and gets destroyed.")]
        public ParticleSystem hitPrefab;
        [Tooltip("Minimum hit velocity for the sound to be played at all.")]
        public float minHitForce = 1;
        [Tooltip("Maximum hit velocity for the sound to be played at full volume.")]
        public float maxHitForce = 10;

        [Header("Boost")]
        [Tooltip("Prefab for the boost particles. Must be a Particle System that loops.")]
        public ParticleSystem boostPrefab;

        private TCCWheel[] wheels;
        private GameObject objectRoot;
        private TrailRenderer[] skidMarkObjects;
        private ParticleSystem[] smokeObjects;
        private ParticleSystem boostObject;

        private float audioScreechingVolume = 0;
        private float audioEngineDelta = 0;

        void Start()
        {
            if (playerObject == null) playerObject = GetComponentInParent<TCCPlayer>();
            
            objectRoot = new GameObject("skid marks");
            objectRoot.transform.SetParent(transform);

            if (skidMarkPrefab != null)
            {
                skidMarkObjects = new TrailRenderer[getWheels().Length];
            }

            if (smokePrefab != null)
            {
                smokeObjects = new ParticleSystem[getWheels().Length];
                for (int i = 0; i < getWheels().Length; i++)
                {
                    smokeObjects[i] = Instantiate(smokePrefab, Vector3.zero, Quaternion.identity, objectRoot.transform);
                }
            }

            if (boostPrefab != null)
            {
                boostObject = Instantiate(boostPrefab, Vector3.zero, Quaternion.identity, objectRoot.transform);
            }

            resetAudio();

            playerObject.getCarBody().onCollisionEnterActions += onBodyEnterCollision;
        }

        void OnEnable()
        {
            resetAudio();
        }

        void Update()
        {
            // engine sound

            if (audioEngine != null)
            {
                float velocityDelta = Mathf.Abs(playerObject.getForwardVelocity() / playerObject.motorMaxSpeed);
                float inputDelta = Mathf.Abs(playerObject.getSmoothMotor());
                float totalDelta = Mathf.Lerp(inputDelta, velocityDelta, velocityToInputRatio);

                audioEngineDelta = Mathf.Lerp(audioEngineDelta, totalDelta, Mathf.Clamp01(enginePitchInterpolation == 0 ? 1 : Time.deltaTime * enginePitchInterpolation));
                audioEngine.pitch = Mathf.LerpUnclamped(minEnginePitch, maxEnginePitch, audioEngineDelta);
                audioEngine.transform.position = playerObject.getPosition();
            }

            // tires screeching

            float maxTireVolume = 0;
            float sideVelocityDelta = 0;
            if (playerObject.getGroundVelocity() >= minScreechingVelocity) sideVelocityDelta = Mathf.Abs(playerObject.getRightVelocity()) / Mathf.Abs(playerObject.getGroundVelocity());
            bool isBraking = playerObject.getBrake() != 0 && playerObject.getForwardVelocity() >= minScreechingVelocity;

            for (int i = 0; i < getWheels().Length; i++)
            {
                TCCWheel wheel = getWheels()[i];

                bool skidding = wheel.isTouchingGround() && (isBraking || sideVelocityDelta >= minScreechingLateralDelta);

                if (skidding)
                {
                    if (skidMarkObjects != null)
                    {
                        if (skidMarkObjects[i] == null)
                        {
                            skidMarkObjects[i] = Instantiate(skidMarkPrefab);
                            skidMarkObjects[i].transform.SetParent(objectRoot.transform);
                        }
                        skidMarkObjects[i].transform.position = wheel.getPosition() + Vector3.down * wheel.getColliderRadius() * 0.9f;
                    }

                    if (smokeObjects != null && !smokeObjects[i].isPlaying) smokeObjects[i].Play();

                    maxTireVolume = Mathf.Max(maxTireVolume, 1);
                }
                else
                {
                    if (skidMarkObjects != null)
                    {
                        if (skidMarkObjects[i] != null)
                        {
                            skidMarkObjects[i] = null;
                        }
                    }
                    if (smokeObjects != null && smokeObjects[i].isPlaying) smokeObjects[i].Stop();

                    maxTireVolume = Mathf.Max(maxTireVolume, 0);
                }

                if (smokeObjects != null) smokeObjects[i].transform.position = wheel.getPosition() + Vector3.down * wheel.getColliderRadius() * 0.9f;
            }

            audioScreechingVolume = Mathf.Lerp(audioScreechingVolume, maxTireVolume, Mathf.Clamp01(Time.deltaTime * screechingVolumeInterpolation));
            if (audioScreeching != null)
            {
                audioScreeching.volume = audioScreechingVolume;
                audioScreeching.transform.position = playerObject.getPosition();
            }

            // collision hit

            if (audioHit != null)
            {
                audioHit.transform.position = playerObject.getPosition();
            }

            // boost particles

            if (boostObject != null)
            {
                boostObject.transform.position = playerObject.getPosition();
                boostObject.transform.rotation = playerObject.getRotation();

                ParticleSystem.EmissionModule boostEmission = boostObject.emission;
                boostEmission.enabled = (playerObject.maxSpeedMultiplier > 1f);
            }
        }

        internal void resetAudio()
        {
            if (audioScreeching != null)
            {
                if (!audioScreeching.isPlaying) audioScreeching.Play();
                audioScreeching.volume = 0;
            }

            if (audioEngine != null && !audioEngine.isPlaying)
            {
                audioEngineDelta = 0;
                audioEngine.Play();
                audioEngine.volume = 1;
            }
        }

        internal void onBodyEnterCollision(Collision c)
        {
            if (audioHit != null && c.relativeVelocity.magnitude >= minHitForce)
            {
                audioHit.volume = maxHitForce > minHitForce ? Mathf.Clamp01((c.relativeVelocity.magnitude - minHitForce) / (maxHitForce - minHitForce)) : 1f;
                audioHit.Play();

                ParticleSystem hitParticles = Instantiate(hitPrefab);
                hitParticles.transform.position = c.contacts[0].point;
            }
        }

        /// <summary>
        /// Returns the <c>TCCPlayer</c> object associated with this script.
        /// </summary>
        public TCCPlayer getPlayer()
        {
            if (playerObject == null) playerObject = GetComponentInChildren<TCCPlayer>();
            return playerObject;
        }

        /// <summary>
        /// Returns the <c>TCCWheel</c> objects associated with this script.
        /// </summary>
        public TCCWheel[] getWheels()
        {
            if (wheels == null) wheels = playerObject.GetComponentsInChildren<TCCWheel>();
            return wheels;
        }
    }
}