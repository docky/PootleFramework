using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle
{
    public enum PootleCarCameraMode { DemoSwing, CameraRelative, Warthog }

    public sealed partial class PootleCar
    {
        [Header("Car camera and steering")]
        public PootleCarCameraMode cameraMode = PootleCarCameraMode.DemoSwing;
        [Min(0)] public float cameraStickDegreesPerSecond = 135;
        [Min(0)] public float cameraMouseDegreesPerPixel = .12f;
        [Min(.1f)] public float demoCameraFollowUpToSpeed = 12;
        [Min(0)] public float demoCameraRotationSharpness = 3;
        [Min(.1f)] public float cameraSteeringFullLockAngle = 50;
        public float CameraYaw => cameraYaw;
        public float CameraPitch => cameraPitch;
        InputActionMap carCameraActions;
        InputAction demoCameraKey, relativeCameraKey, warthogCameraKey, cameraLook, cameraMouseLook;
        float cameraYaw, cameraPitch, cameraDistance, manualCameraUntil;

        void InitializeCameraControls()
        {
            cameraYaw = Body.eulerAngles.y;
            Vector3 offset = cameraOffset - lookOffset;
            cameraDistance = Mathf.Max(1, offset.magnitude);
            cameraPitch = Mathf.Clamp(Mathf.Atan2(offset.y, -offset.z) * Mathf.Rad2Deg, -8, 65);
            manualCameraUntil = 0;
            cameraLook = inputInstance.FindAction("Camera/Look");
            cameraMouseLook = inputInstance.FindAction("Camera/MouseLook");
            cameraLook?.Enable(); cameraMouseLook?.Enable();
            carCameraActions = new InputActionMap("Vehicle camera modes");
            demoCameraKey = carCameraActions.AddAction("Demo swing", InputActionType.Button, "<Keyboard>/8");
            relativeCameraKey = carCameraActions.AddAction("Camera relative", InputActionType.Button, "<Keyboard>/9");
            warthogCameraKey = carCameraActions.AddAction("Warthog", InputActionType.Button, "<Keyboard>/0");
            carCameraActions.Enable();
        }

        public void SelectCameraMode(PootleCarCameraMode mode)
        {
            cameraMode = mode;
            // Keep the view continuous when changing how the same camera drives the car.
            manualCameraUntil = Time.time + .25f;
        }

        void UpdateCameraInput()
        {
            if (demoCameraKey.WasPressedThisFrame()) SelectCameraMode(PootleCarCameraMode.DemoSwing);
            if (relativeCameraKey.WasPressedThisFrame()) SelectCameraMode(PootleCarCameraMode.CameraRelative);
            if (warthogCameraKey.WasPressedThisFrame()) SelectCameraMode(PootleCarCameraMode.Warthog);
            Vector2 degrees = (cameraLook?.ReadValue<Vector2>() ?? Vector2.zero) * (cameraStickDegreesPerSecond * Time.deltaTime);
            if (Mouse.current != null && Mouse.current.rightButton.isPressed)
                degrees += (cameraMouseLook?.ReadValue<Vector2>() ?? Vector2.zero) * cameraMouseDegreesPerPixel;
            if (degrees.sqrMagnitude > .000001f) manualCameraUntil = Time.time + 1.25f;
            cameraYaw = Mathf.Repeat(cameraYaw + degrees.x, 360);
            cameraPitch = Mathf.Clamp(cameraPitch - degrees.y, -8, 65);
        }

        Vector3 CameraPosition(Vector3 target, bool snap)
        {
            if (cameraMode == PootleCarCameraMode.DemoSwing && !snap && Time.time >= manualCameraUntil)
            {
                // The demo keeps its yaw at rest and increasingly follows travel direction at speed.
                // Heading lags the chassis, letting the front swing out during a turn or drift.
                var velocity = Vector3.ProjectOnPlane(Controller.getCarRigidBody().linearVelocity, Vector3.up);
                float forwardSpeed = Vector3.Dot(velocity, Body.forward);
                Vector3 heading = Vector3.ProjectOnPlane(Body.forward, Vector3.up).normalized;
                if (forwardSpeed > 0 && velocity.sqrMagnitude > .01f)
                    heading = Vector3.Slerp(heading, velocity.normalized, Mathf.Clamp01(forwardSpeed));
                float follow = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / Mathf.Max(.1f, demoCameraFollowUpToSpeed));
                float yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
                cameraYaw = Mathf.LerpAngle(cameraYaw, yaw, 1 - Mathf.Exp(-demoCameraRotationSharpness * follow * Time.deltaTime));
            }
            return target + Quaternion.Euler(cameraPitch, cameraYaw, 0) * Vector3.back * cameraDistance;
        }

        void ResolveDrivingInput(Vector2 axes, float pedal, out float motor, out float steering)
        {
            motor = Mathf.Clamp(pedal + Mathf.Min(0, axes.y), -1, 1);
            steering = axes.x;
            if (cameraMode == PootleCarCameraMode.DemoSwing) return;
            Vector3 forward = Vector3.ProjectOnPlane(chaseCamera.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < .01f) forward = Quaternion.Euler(0, cameraYaw, 0) * Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            if (cameraMode == PootleCarCameraMode.CameraRelative)
            {
                // The stick chooses a world travel direction relative to the view, including reverse.
                // RT supplies speed; pulling back still supports the existing reverse control.
                if (axes.sqrMagnitude < .01f) { motor = pedal; steering = 0; return; }
                Vector3 travel = (forward * axes.y + right * axes.x).normalized;
                bool reverse = Vector3.Dot(Body.forward, travel) < -.1f;
                float amount = pedal > .01f ? pedal : Mathf.Max(0, -axes.y);
                motor = reverse ? -amount : amount;
                steering = HeadingSteering(reverse ? -travel : travel, reverse);
            }
            else
            {
                // Warthog-style: while driving, aim the camera where the nose should go.
                // Sideways stick offsets the travel direction; reversing corrects steering sign.
                bool reverse = motor < -.01f;
                Vector3 travel = forward * (reverse ? -1 : 1) + right * axes.x;
                steering = Mathf.Abs(motor) > .01f || Mathf.Abs(Controller.getForwardVelocity()) > .5f
                    ? HeadingSteering(reverse ? -travel : travel, reverse) : 0;
            }
        }

        float HeadingSteering(Vector3 facing, bool reverse)
        {
            Vector3 carForward = Vector3.ProjectOnPlane(Body.forward, Vector3.up);
            if (carForward.sqrMagnitude < .01f) carForward = Quaternion.Euler(0, Body.eulerAngles.y, 0) * Vector3.forward;
            float angle = Vector3.SignedAngle(carForward, facing, Vector3.up);
            return Mathf.Clamp(angle / Mathf.Max(.1f, cameraSteeringFullLockAngle), -1, 1) * (reverse ? -1 : 1);
        }

        void ReleaseCameraControls()
        {
            carCameraActions?.Dispose(); carCameraActions = null;
            demoCameraKey = relativeCameraKey = warthogCameraKey = cameraLook = cameraMouseLook = null;
        }
    }
}
