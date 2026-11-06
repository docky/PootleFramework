using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle
{
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Input")]
    public sealed class PootleInput : MonoBehaviour
    {
        [Tooltip("A private runtime copy is enabled; this asset is never enabled/disabled globally.")]
        public InputActionAsset actions;
        [InspectorName("Movement Smoothing")]
        public PootleDigitalMoveSmoothing digitalMovement = new PootleDigitalMoveSmoothing();
        private InputActionAsset instance;
        private InputAction move, run, jump, reset, glide, detach, roll, carry, interact;
        private int lastSample = -1;
        private int lastMovementSample = -1;
        private Vector2 sampledMove;

        private void OnEnable()
        {
            ResetMovement();
            if (!actions) return;
            instance = Instantiate(actions);
            move = instance.FindAction("Character/Move", true);
            run = instance.FindAction("Character/Run", true);
            run.wantsInitialStateCheck = true;
            jump = instance.FindAction("Character/Jump", true);
            detach = instance.FindAction("Character/Detach", true);
            glide = instance.FindAction("Character/Glide", true);
            roll = instance.FindAction("Character/Roll");
            carry = instance.FindAction("Character/Carry");
            interact = instance.FindAction("Character/Interact");
            reset = instance.FindAction("Character/Reset", true);
            instance.FindActionMap("Character", true).Enable();
            lastSample = -1;
        }

        public PootleInputFrame Sample()
        {
            if (!instance || !isActiveAndEnabled) return default;
            bool first = lastSample != Time.frameCount;
            lastSample = Time.frameCount;
            if (lastMovementSample != Time.frameCount)
            {
                sampledMove = digitalMovement.Read(move, Time.deltaTime);
                lastMovementSample = Time.frameCount;
            }
            // A deliberately neutral jump must not turn the release ramp into a directional launch.
            if (first && jump.WasPressedThisFrame() && move.ReadValue<Vector2>() == Vector2.zero) ResetMovement();
            return new PootleInputFrame
            {
                move = sampledMove,
                moveIsDigital = digitalMovement.DigitalActive,
                run = run.IsPressed(), glide = glide.IsPressed(), jumpHeld = jump.IsPressed(),
                jumpPressed = first && jump.WasPressedThisFrame(),
                detachPressed = first && detach.WasPressedThisFrame(),
                rollPressed = first && roll != null && roll.WasPressedThisFrame(),
                carryPressed = first && carry != null && carry.WasPressedThisFrame(),
                interactPressed = first && interact != null && interact.WasPressedThisFrame(),
                resetPressed = first && reset.WasPressedThisFrame()
            };
        }

        public void ResetMovement()
        {
            digitalMovement.Reset();
            sampledMove = Vector2.zero;
            // Resample held movement after a teleport without replaying this frame's button edges.
            lastMovementSample = -1;
        }

        private void OnDisable()
        {
            if (instance) { instance.Disable(); Destroy(instance); }
            instance = null;
            move = run = jump = reset = glide = detach = roll = carry = interact = null;
            lastSample = -1;
            ResetMovement();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            ResetMovement();
            lastSample = -1;
        }
    }
}
