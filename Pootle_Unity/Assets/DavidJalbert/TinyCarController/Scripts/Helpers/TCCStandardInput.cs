using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DavidJalbert.TinyCarController
{
    public class TCCStandardInput : MonoBehaviour
    {
        public TCCPlayer carController;

        [Header("Speed boost")]
        [Tooltip("Acceleration multiplier to apply when using the boost.")]
        public float accelerationBoostMultiplier = 2;
        [Tooltip("Max speed multiplier to apply when using the boost.")]
        public float speedBoostMultiplier = 2;

        [Header("Input")]
        [Tooltip("Whether to let this script control the vehicle.")]
        public bool enableInput = true;
        [Tooltip("Input action to make the vehicle move forward.")]
        public InputAction forwardInput = new InputAction("forward", InputActionType.Value, "<Keyboard>/w");
        [Tooltip("Input action to make the vehicle move backward.")]
        public InputAction reverseInput = new InputAction("reverse", InputActionType.Value, "<Keyboard>/S");
        [Tooltip("Input action to make the vehicle turn right.")]
        public InputAction steerRightInput = new InputAction("right", InputActionType.Value, "<Keyboard>/D");
        [Tooltip("Input action to make the vehicle turn left.")]
        public InputAction steerLeftInput = new InputAction("left", InputActionType.Value, "<Keyboard>/A");
        [Tooltip("Input action to apply the handbrake.")]
        public InputAction brakeInput = new InputAction("brake", InputActionType.Value, "<Keyboard>/space");
        [Tooltip("Input action to give the vehicle a speed boost.")]
        public InputAction boostInput = new InputAction("boost", InputActionType.Value, "<Keyboard>/leftShift");

        void Start()
        {
            forwardInput.Enable();
            reverseInput.Enable();
            steerRightInput.Enable();
            steerLeftInput.Enable();
            brakeInput.Enable();
            boostInput.Enable();
        }
        
        void Update()
        {
            if (enableInput && carController != null)
            {
                float motorDelta = getInput(forwardInput) - getInput(reverseInput);
                float steeringDelta = getInput(steerRightInput) - getInput(steerLeftInput);
                float brake = getInput(brakeInput);
                float boostDelta = Mathf.Abs(getInput(boostInput));

                carController.setMotor(motorDelta);
                carController.setSteering(steeringDelta);
                carController.setBrake(brake);
                carController.maxSpeedMultiplier = Mathf.Lerp(1f, speedBoostMultiplier, boostDelta);
                carController.accelerationMultiplier = Mathf.Lerp(1f, accelerationBoostMultiplier, boostDelta);
            }
        }

        internal float getInput(InputAction action)
        {
            return action.ReadValue<float>();
        }

        public float getInputForward()
        {
            return getInput(forwardInput);
        }

        public float getInputReverse()
        {
            return getInput(reverseInput);
        }

        public float getInputLeft()
        {
            return getInput(steerLeftInput);
        }

        public float getInputRight()
        {
            return getInput(steerRightInput);
        }

        public float getInputBrake()
        {
            return getInput(brakeInput);
        }

        public float getInputBoost()
        {
            return getInput(boostInput);
        }
    }
}