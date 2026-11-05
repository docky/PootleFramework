using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle
{
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Input")]
    public sealed class PootleInput : MonoBehaviour
    {
        [Tooltip("A private runtime copy is enabled; this asset is never enabled/disabled globally.")]
        public InputActionAsset actions;
        private InputActionAsset instance;
        private InputAction move, run, jump, reset, glide, detach, roll, carry, interact;
        private int lastSample = -1;

        private void OnEnable()
        {
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
            return new PootleInputFrame
            {
                move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1),
                run = run.IsPressed(), glide = glide.IsPressed(), jumpHeld = jump.IsPressed(),
                jumpPressed = first && jump.WasPressedThisFrame(),
                detachPressed = first && detach.WasPressedThisFrame(),
                rollPressed = first && roll != null && roll.WasPressedThisFrame(),
                carryPressed = first && carry != null && carry.WasPressedThisFrame(),
                interactPressed = first && interact != null && interact.WasPressedThisFrame(),
                resetPressed = first && reset.WasPressedThisFrame()
            };
        }

        private void OnDisable()
        {
            if (instance) { instance.Disable(); Destroy(instance); }
            instance = null;
            move = run = jump = reset = glide = detach = roll = carry = interact = null;
            lastSample = -1;
        }
    }
}
