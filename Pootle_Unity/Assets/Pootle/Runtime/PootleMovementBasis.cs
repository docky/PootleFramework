using UnityEngine;
using UnityEngine.InputSystem;
using Sirenix.OdinInspector;

namespace Pootle
{
    // Opt-in: attach to the rendered Camera. PootleMovement queries it only when enabled.
    [DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class PootleMovementBasis : MonoBehaviour
    {
        public PootleInput playerInput;
        public bool preserveHeldInput = true;
        [Range(0, 90)] public float significantYaw = 12;
        [Range(.01f, .5f)] public float releaseThreshold = .12f;
        [ShowInInspector, ReadOnly] public bool IsLocked { get; private set; }
        [ShowInInspector, ReadOnly] public Vector3 Forward { get; private set; } = Vector3.forward;
        public Vector2 Input { get; private set; }
        public float Yaw => Mathf.Atan2(Forward.x, Forward.z) * Mathf.Rad2Deg;
        InputAction move;
        bool wasHeld;
        float transitionUntil;

        void OnEnable()
        {
            Release();
            if (!Application.isPlaying || !playerInput || !playerInput.actions) return;
            move = playerInput.actions.FindAction("Character/Move", true).Clone();
            move.Enable();
        }
        void OnDisable() { move?.Dispose(); move = null; IsLocked = false; }
        void Update() => Sample(move != null ? move.ReadValue<Vector2>() : Vector2.zero, transform.rotation, Time.time);
        public void Sample(Vector2 input, Quaternion viewRotation, float now)
        {
            Input = input;
            bool held = input.sqrMagnitude > releaseThreshold * releaseThreshold;
            if (!preserveHeldInput || !held) IsLocked = false;
            if (!IsLocked) Forward = HorizontalForward(viewRotation);
            // A fresh press during a blend uses the visible view, then holds that basis.
            if (preserveHeldInput && held && !wasHeld && now < transitionUntil) IsLocked = true;
            wasHeld = held;
        }
        public void BeginHandover(float incomingYaw, float duration, float now)
        {
            if (!preserveHeldInput || Mathf.Abs(Mathf.DeltaAngle(Yaw, incomingYaw)) < significantYaw) return;
            transitionUntil = now + duration + .1f;
            if (Input.sqrMagnitude > releaseThreshold * releaseThreshold) IsLocked = true;
        }
        [Button("Release movement basis")]
        public void Release()
        {
            IsLocked = wasHeld = false; transitionUntil = 0;
            Forward = HorizontalForward(transform.rotation);
        }
        public static Vector3 HorizontalForward(Quaternion rotation)
        {
            Vector3 f = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (f.sqrMagnitude < .0001f) f = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
            return f.normalized;
        }
    }
}
