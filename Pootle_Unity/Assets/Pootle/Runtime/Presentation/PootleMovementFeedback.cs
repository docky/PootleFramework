using UnityEngine;

namespace Pootle
{
    /// <summary>Optional presentation only: distance-based steps and authoritative jump/contact events.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PootleCharacter))]
    [AddComponentMenu("Pootle/Movement Feedback")]
    public sealed class PootleMovementFeedback : MonoBehaviour
    {
        public AudioSource voiceSource, feetSource;
        public AudioClip[] jumps, footsteps;
        public AudioClip landing, heavyLanding;
        public ParticleSystem stepDust, jumpDust, landingDust;
        [Min(.1f)] public float stepDistance = 1.25f;
        [Min(.05f)] public float minimumStepInterval = .14f;
        [Range(0, 1)] public float jumpVolume = .45f, stepVolume = .3f, landingVolume = .5f;
        [Min(0)] public float minimumLandingSpeed = 2.5f;
        [Min(1)] public float heavyLandingSpeed = 24;
        private PootleCharacter character;
        private Vector3 previousPosition;
        private float distance, stepClock, landingCooldown;
        private int lastJump = -1, lastStep = -1;
        private bool rightFoot;

        private void OnEnable()
        {
            character = GetComponent<PootleCharacter>();
            character.Jumped += Jump;
            character.Landed += Land;
            character.Teleported += ResetFeedback;
            ResetFeedback(transform.position);
        }
        private void OnDisable()
        {
            if (character)
            {
                character.Jumped -= Jump;
                character.Landed -= Land;
                character.Teleported -= ResetFeedback;
            }
            ResetFeedback(transform.position);
        }
        private void LateUpdate()
        {
            if (character && character.simulateAutomatically) Tick(Time.deltaTime);
        }
        public void Tick(float dt)
        {
            if (!character || dt <= 0) return;
            stepClock += dt; landingCooldown = Mathf.Max(0, landingCooldown - dt);
            float travelled = Vector3.Distance(previousPosition, transform.position);
            previousPosition = transform.position;
            bool walking = character.Mode == PootleMode.Grounded || character.Mode == PootleMode.RailWalking;
            float speed = character.Velocity.magnitude;
            // Carrying/teleporting the capsule is not a step. A blocked walk also has no travel.
            if (!walking || speed < .5f || travelled > Mathf.Max(2, speed * dt * 3))
            { distance = 0; return; }
            distance += Mathf.Min(travelled, speed * dt);
            if (distance < stepDistance || stepClock < minimumStepInterval) return;
            distance = 0; stepClock = 0; rightFoot = !rightFoot;
            Play(feetSource, Pick(footsteps, ref lastStep), stepVolume, Random.Range(.94f, 1.06f));
            Vector3 side = Vector3.Cross(Vector3.up, character.Facing) * (rightFoot ? .14f : -.14f);
            Puff(stepDust, 3, transform.position + side + Vector3.up * .04f, .7f);
        }
        private void Jump()
        {
            distance = 0; stepClock = 0;
            Play(voiceSource, Pick(jumps, ref lastJump), jumpVolume, Random.Range(.97f, 1.03f));
            Puff(jumpDust, 9, transform.position + Vector3.up * .08f, 1);
        }
        private void Land(float speed)
        {
            if (speed < minimumLandingSpeed || landingCooldown > 0) return;
            landingCooldown = .12f; distance = 0; stepClock = 0;
            float strength = Mathf.InverseLerp(minimumLandingSpeed, heavyLandingSpeed, speed);
            Play(feetSource, speed >= heavyLandingSpeed && heavyLanding ? heavyLanding : landing,
                landingVolume * Mathf.Lerp(.45f, 1, strength), Random.Range(.94f, 1.02f));
            Puff(landingDust, Mathf.RoundToInt(Mathf.Lerp(8, 24, strength)),
                transform.position + Vector3.up * .05f, Mathf.Lerp(.7f, 1.5f, strength));
        }
        private void ResetFeedback(Vector3 position)
        {
            previousPosition = position; distance = stepClock = landingCooldown = 0;
            if (voiceSource) voiceSource.Stop();
            if (feetSource) feetSource.Stop();
            foreach (var particles in new[] { stepDust, jumpDust, landingDust })
                if (particles) particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        private static AudioClip Pick(AudioClip[] clips, ref int last)
        {
            if (clips == null || clips.Length == 0) return null;
            int choice = Random.Range(0, clips.Length);
            if (clips.Length > 1 && choice == last) choice = (choice + 1) % clips.Length;
            last = choice; return clips[choice];
        }
        private static void Play(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            if (!Application.isPlaying || !source || !clip) return;
            source.pitch = pitch; source.PlayOneShot(clip, volume);
        }
        private static void Puff(ParticleSystem particles, int count, Vector3 position, float size)
        {
            if (!particles) return;
            particles.transform.position = position;
            if (!particles.isPlaying) particles.Play();
            // Reuse emitters. World-space particles remain at the contact after Lily moves on.
            var emit = new ParticleSystem.EmitParams { startSize = .25f * size, applyShapeToPosition = true };
            particles.Emit(emit, count);
        }
    }
}
