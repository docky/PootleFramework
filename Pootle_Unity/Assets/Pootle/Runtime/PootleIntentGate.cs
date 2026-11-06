using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Pootle
{
    public interface IPootleIntentReceiver
    {
        // Return true only after taking ownership of the character.
        bool TryAccept(PootleCharacter character, PootleIntentGate gate);
    }

    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider)), AddComponentMenu("Pootle/Push Intent Gate")]
    public sealed class PootleIntentGate : MonoBehaviour
    {
        [Tooltip("Push along this object's forward axis into the narrow gap.")]
        public MonoBehaviour receiver;
        [Min(.05f)] public float holdSeconds = .35f;
        [Range(0, 80)] public float directionTolerance = 35;
        [Min(0)] public float pushBackSpeed = 3;
        public Transform plunger;
        public Collider barrier;
        public Renderer acknowledgementLamp;
        public UnityEvent<PootleCharacter> onPressed = new UnityEvent<PootleCharacter>();
        public int PressCount { get; private set; }
        public float Pressure { get; private set; }
        public bool IsOpen { get; private set; }
        static readonly List<PootleIntentGate> active = new List<PootleIntentGate>();
        readonly Dictionary<PootleCharacter, bool> latched = new Dictionary<PootleCharacter, bool>();
        PootleCharacter candidate;
        BoxCollider volume;
        Vector3 rest;
        float flash, lastSample;
        MaterialPropertyBlock block;
        void Awake() { volume = GetComponent<BoxCollider>(); volume.isTrigger = true; if (plunger) rest = plunger.localPosition; }
        void OnEnable() { if (!active.Contains(this)) active.Add(this); }
        void OnDisable() { active.Remove(this); latched.Clear(); candidate = null; Pressure = 0; SetOpen(false); }
        void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
        public void SetOpen(bool value) { IsOpen = value; if (barrier) barrier.enabled = !value; }
        bool Contains(Vector3 point, float margin)
        {
            if (!volume) volume = GetComponent<BoxCollider>();
            Vector3 p = transform.InverseTransformPoint(point) - volume.center;
            Vector3 half = volume.size * .5f + Vector3.one * margin;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }
        public static bool Process(PootleCharacter character, PootleInputFrame input, float dt)
        {
            Vector3 direction = PootleMovement.CameraDirection(input.move, character.outputCamera, out float magnitude);
            // Iterate backwards so a receiver can deactivate its gate safely.
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i].Sample(character, direction * magnitude, dt)) return true;
            return false;
        }
        public bool Sample(PootleCharacter character, Vector3 wish, float dt)
        {
            if (!character || !isActiveAndEnabled || dt <= 0) return false;
            bool inside = Contains(character.transform.position + Vector3.up * .7f, 0);
            if (latched.TryGetValue(character, out bool released))
            {
                released |= wish.magnitude < .15f;
                latched[character] = released;
                if (released && !Contains(character.transform.position + Vector3.up * .7f, .35f)) latched.Remove(character);
                return false;
            }
            bool pressing = !IsOpen && inside && character.IsGrounded && !character.IsRolling &&
                wish.magnitude >= .55f && Vector3.Dot(wish.normalized, transform.forward) >= Mathf.Cos(directionTolerance * Mathf.Deg2Rad) &&
                Vector3.Dot(character.Velocity, transform.forward) < 1.2f;
            if (!pressing) { if (candidate == character) { Pressure = 0; candidate = null; } return false; }
            if (candidate != character) { candidate = character; Pressure = 0; }
            lastSample = Time.time;
            Pressure = Mathf.Min(1, Pressure + dt / Mathf.Max(.05f, holdSeconds));
            if (Pressure < 1) return false;
            latched[character] = false; candidate = null; Pressure = 0; flash = .65f; PressCount++;
            onPressed.Invoke(character);
            bool accepted = receiver && receiver.isActiveAndEnabled && receiver is IPootleIntentReceiver target && target.TryAccept(character, this);
            if (!accepted && character && character.isActiveAndEnabled)
                character.ApplyImpulse(-transform.forward * pushBackSpeed + Vector3.up * 1.4f);
            return true;
        }
        void Update()
        {
            flash = Mathf.Max(0, flash - Time.deltaTime);
            if (Time.time - lastSample > .12f) Pressure = 0;
            if (plunger) plunger.localPosition = Vector3.Lerp(plunger.localPosition, rest + Vector3.forward * (IsOpen ? .8f : .12f * Mathf.Max(Pressure, flash > 0 ? 1 : 0)), 1 - Mathf.Exp(-18 * Time.deltaTime));
            if (!acknowledgementLamp) return;
            block ??= new MaterialPropertyBlock();
            Color color = IsOpen || flash > 0 ? new Color(.3f, 1, .55f) : Color.Lerp(new Color(.18f, .25f, .24f), new Color(1, .6f, .1f), Pressure);
            block.SetColor("_BaseColor", color); block.SetColor("_EmissionColor", color * (flash > 0 ? 1.5f : .2f));
            acknowledgementLamp.SetPropertyBlock(block);
        }
        void OnDrawGizmosSelected() { Gizmos.color = Color.cyan; Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 2); }
    }
}
