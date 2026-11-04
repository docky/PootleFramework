using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle
{
    // Standard Cinemachine axis driver, with device-specific units and privately owned actions.
    [DefaultExecutionOrder(-50), RequireComponent(typeof(CinemachineInputAxisController))]
    [AddComponentMenu("Pootle/Cinemachine Orbit Input")]
    public sealed class PootleOrbitInput : MonoBehaviour
    {
        public InputActionAsset actions;
        public PootleCharacter character;
        [Min(0)] public float stickDegreesPerSecond = 150;
        [Min(0)] public float mouseDegreesPerPixel = 0.12f;
        public bool invertY = true;
        private InputActionAsset instance;
        private InputActionReference lookReference;
        private InputAction recenter, mouseLook;
        private CinemachineInputAxisController driver;
        private CinemachineOrbitalFollow orbit;
        private CinemachineCamera cameraRig;

        private void OnEnable()
        {
            driver = GetComponent<CinemachineInputAxisController>();
            orbit = GetComponent<CinemachineOrbitalFollow>();
            cameraRig = GetComponent<CinemachineCamera>();
            driver.AutoEnableInputs = false;
            if (!actions) return;
            instance = Instantiate(actions);
            instance.FindActionMap("Camera", true).Enable();
            mouseLook = instance.FindAction("Camera/MouseLook", true);
            recenter = instance.FindAction("Camera/Recenter", true);
            lookReference = InputActionReference.Create(instance.FindAction("Camera/Look", true));
            driver.SynchronizeControllers();
            foreach (var c in driver.Controllers)
            {
                c.Enabled = c.Name != "Orbit Scale";
                c.Input = new CinemachineInputAxisController.Reader { InputAction = lookReference, Gain = 1, CancelDeltaTime = false };
                c.Driver = new DefaultInputAxisDriver { AccelTime = 0.08f, DecelTime = 0.08f };
            }
            driver.ReadControlValueOverride = Read;
            if (character) character.Teleported += OnTeleport;
        }
        private float Read(InputAction action, IInputAxisOwner.AxisDescriptor.Hints hint,
            Object context, CinemachineInputAxisController.Reader.ControlValueReader fallback)
        {
            Vector2 value = action.ReadValue<Vector2>() * stickDegreesPerSecond;
            if (Mouse.current != null && Mouse.current.rightButton.isPressed && mouseLook != null)
                value += mouseLook.ReadValue<Vector2>() * (mouseDegreesPerPixel / Mathf.Max(Time.deltaTime, 0.00001f));
            return hint == IInputAxisOwner.AxisDescriptor.Hints.Y ? value.y * (invertY ? -1 : 1) : value.x;
        }
        private void Update()
        {
            if (recenter != null && recenter.WasPressedThisFrame() && orbit)
            {
                Vector3 facing = character ? character.Facing : Vector3.forward;
                orbit.HorizontalAxis.Value = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                orbit.VerticalAxis.Value = orbit.VerticalAxis.Center;
            }
        }
        private void OnTeleport(Vector3 delta)
        {
            if (cameraRig && character && character.cameraTarget)
            {
                cameraRig.OnTargetObjectWarped(character.cameraTarget, delta);
                cameraRig.PreviousStateIsValid = false;
            }
        }
        private void OnDisable()
        {
            if (character) character.Teleported -= OnTeleport;
            if (driver) driver.ReadControlValueOverride = null;
            if (instance) { instance.Disable(); Destroy(instance); }
            if (lookReference) Destroy(lookReference);
            instance = null; recenter = null;
        }
    }
}
