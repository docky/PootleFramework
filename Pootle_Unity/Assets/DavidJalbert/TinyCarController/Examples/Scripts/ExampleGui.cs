using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DavidJalbert.TinyCarController
{
    public class ExampleGui : MonoBehaviour
    {
        public static ExampleGui instance;
        public static List<string> additionalInfo = new List<string>();

        [System.Serializable]
        public class CarType
        {
            public TCCPlayer carObject;
            public GameObject marker;
            public string description;
        }

        public Text textDebug;
        public Text textDescription;
        public CarType[] carTypes;
        public TCCCamera carCamera;
        public TCCMobileInput mobileInput;
        public TCCStandardInput standardInput;

        private int carIndex = 0;
        private TCCPlayer carController;
        private bool inputIsMobile = false;

        private void Start()
        {
            instance = this;
            foreach (CarType c in carTypes)
            {
                if (c.marker != null) c.marker.SetActive(false);
            }
            setInputAsMobile(inputIsMobile);
            changeCar(0);
        }

        void LateUpdate()
        {
            if (textDebug == null) return;

            textDebug.text = "";

            if (carController != null)
            {
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.digit1Key.wasPressedThisFrame)
                    {
                        onClickMobileInput();
                    }

                    if (Keyboard.current.digit2Key.wasPressedThisFrame)
                    {
                        onClickCameraAngle();
                    }

                    if (Keyboard.current.digit3Key.wasPressedThisFrame)
                    {
                        onClickDescriptionText();
                    }

                    if (Keyboard.current.digit4Key.wasPressedThisFrame)
                    {
                        onClickChangeCar();
                    }

                    if (Keyboard.current.digit5Key.wasPressedThisFrame)
                    {
                        onClickResetCar();
                    }
                }
                
                textDebug.text += "Car : " + carTypes[carIndex].description + "\n";
                textDebug.text += "Speed : " + (int)carController.getForwardVelocity() + " m/s\n";
                textDebug.text += "Drift speed : " + (int)carController.getRightVelocity() + " m/s\n";
                textDebug.text += "Is grounded : " + (carController.isFullyGrounded() ? "true" : (carController.isPartiallyGrounded() ? "partial" : "false")) + "\n";
                textDebug.text += "Pitch : " + (int)(carController.getPitchAngle()) + " degrees\n";
                textDebug.text += "Roll : " + (int)(carController.getRollAngle()) + " degrees\n";
                TCCSurfaceModifier surfaceModifier = carController.getSurfaceModifier();
                textDebug.text += "Max Speed Modifier : " + surfaceModifier.maxSpeedModifier + "\n";
                textDebug.text += "Acceleration Modifier : " + surfaceModifier.accelerationModifier + "\n";
                textDebug.text += "Tire Grip Modifier : " + surfaceModifier.tireGripModifier + "\n";

                foreach (string line in additionalInfo)
                {
                    textDebug.text += line + "\n";
                }

                additionalInfo.Clear();
            }
        }

        public static void addInfo(string line)
        {
            additionalInfo.Add(line);
        }

        public static bool isCarSelected(TCCPlayer p)
        {
            return instance.carController == p;
        }

        private void changeCar(int i)
        {
            if (carController != null)
            {
                carController.setBrake(0);
                carController.setMotor(0);
                carController.setSteering(0);
            }

            carIndex = i % carTypes.Length;
            carController = carTypes[carIndex].carObject;

            if (carCamera != null) carCamera.carController = carController;
            if (mobileInput != null) mobileInput.carController = carController;
            if (standardInput != null) standardInput.carController = carController;

            carCamera?.resetCamera();
        }

        public void onClickResetCar()
        {
            Vector3 teleportPosition = carTypes[carIndex].marker.transform.position;
            Quaternion teleportRotation = carTypes[carIndex].marker.transform.rotation;
            carController.immobilize();
            carController.setPosition(teleportPosition);
            carController.setRotation(teleportRotation);
        }

        public void onClickChangeCar()
        {
            changeCar(carIndex + 1);
        }

        public void onClickMobileInput()
        {
            setInputAsMobile(!inputIsMobile);
        }

        private void setInputAsMobile(bool e)
        {
            inputIsMobile = e;

            if (mobileInput != null)
            {
                mobileInput.enableInput = e;

                mobileInput.gameObject.SetActive(e);
            }

            if (standardInput != null)
            {
                standardInput.enableInput = !e;
            }
        }

        public void onClickCameraAngle()
        {
            if (carCamera != null)
            {
                carCamera.switchCameraMode();
            }
        }

        public void onClickDescriptionText()
        {
            if (textDebug != null) textDebug.enabled = !textDebug.enabled;
            if (textDescription != null) textDescription.enabled = !textDescription.enabled;
        }
    }
}