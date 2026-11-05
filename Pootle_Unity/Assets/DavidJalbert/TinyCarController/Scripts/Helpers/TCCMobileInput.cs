using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DavidJalbert.TinyCarController
{
    public class TCCMobileInput : MonoBehaviour
    {
        public TCCPlayer carController;

        [Header("Input")]
        [Tooltip("Whether to let this script control the vehicle.")]
        public bool enableInput = true;
        [Tooltip("Whether the mouse can be used as input for the on-screen controls.")]
        public bool simulateTouchWithMouse = true;
        [Tooltip("The color of the UI element when idle.")]
        public Color colorIdle = new Color(1f, 1f, 1f, 0.5f);
        [Tooltip("The color of the UI element when touched.")]
        public Color colorTouched = new Color(1f, 1f, 1f, 1f);

        [Tooltip("The UI graphic container for the steering wheel.")]
        public RectTransform steeringWheel;
        [Tooltip("The UI area of the steering wheel that will be checked for touches.")]
        public RectTransform steeringWheelTouchArea;
        [Tooltip("The value by which to multiply the value of the steering. Useful if you want to clamp the steering to its min/max value.")]
        public float steeringWheelMultiplier = 2f;

        [Tooltip("The UI graphic container and touch area for the gas pedal.")]
        public RectTransform gasPedal;
        [Tooltip("The UI graphic container and touch area for the brake pedal.")]
        public RectTransform brakePedal;
        [Tooltip("The UI graphic container and touch area for the boost button.")]
        public RectTransform boostButton;

        [Header("Speed boost")]
        [Tooltip("Acceleration multiplier to apply when using the boost.")]
        public float accelerationBoostMultiplier = 2;
        [Tooltip("Max speed multiplier to apply when using the boost.")]
        public float speedBoostMultiplier = 2;

        private GraphicRaycaster raycaster;
        private Graphic steeringWheelGraphic;
        private Graphic gasPedalGraphic;
        private Graphic brakePedalGraphic;
        private Graphic boostButtonGraphic;

        void Start()
        {
            raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            steeringWheelGraphic = steeringWheel.GetComponentInChildren<Graphic>();
            gasPedalGraphic = gasPedal.GetComponentInChildren<Graphic>();
            brakePedalGraphic = brakePedal.GetComponentInChildren<Graphic>();
            boostButtonGraphic = boostButton.GetComponentInChildren<Graphic>();
        }

        void Update()
        {
            if (enableInput)
            {
                bool steeringWheelTouched = false;
                float steeringWheelDelta = 0;
                bool gasPedalTouched = false;
                bool brakePedalTouched = false;
                bool boostButtonTouched = false;

                List<PointerEventData> pointers = new List<PointerEventData>();

                if (Touchscreen.current != null)
                {
                    foreach (TouchControl touch in Touchscreen.current.touches)
                    {
                        PointerEventData pointer = new PointerEventData(EventSystem.current);
                        pointer.position = touch.position.ReadValue();
                        pointers.Add(pointer);
                    }
                }

                if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                {
                    PointerEventData pointer = new PointerEventData(EventSystem.current);
                    pointer.position = Mouse.current.position.ReadValue();
                    pointers.Add(pointer);
                }

                foreach (PointerEventData pointer in pointers)
                {
                    List<RaycastResult> results = new List<RaycastResult>();
                    raycaster.Raycast(pointer, results);
                    foreach (RaycastResult result in results)
                    {
                        Graphic graphic = result.gameObject.GetComponent<Graphic>();
                        if (graphic != null)
                        {
                            Vector2 uiScreenPosition = RectTransformUtility.PixelAdjustPoint(graphic.transform.position, graphic.transform, graphic.canvas);
                            Vector2 rayScreenPosition = result.screenPosition;
                            Vector2 relativePosition = rayScreenPosition - uiScreenPosition;
                            Vector2 positionDelta = new Vector2(relativePosition.x / (graphic.rectTransform.rect.width * graphic.rectTransform.lossyScale.x), relativePosition.y / (graphic.rectTransform.rect.height * graphic.rectTransform.lossyScale.y)) * 2f;

                            if (steeringWheelTouchArea != null && result.gameObject == steeringWheelTouchArea.gameObject)
                            {
                                steeringWheelTouched = true;
                                steeringWheelDelta = Mathf.Clamp(positionDelta.x * steeringWheelMultiplier, -1, 1);
                            }
                            else if (gasPedal != null && result.gameObject == gasPedal.gameObject)
                            {
                                gasPedalTouched = true;
                            }
                            else if (brakePedal != null && result.gameObject == brakePedal.gameObject)
                            {
                                brakePedalTouched = true;
                            }
                            else if (boostButton != null && result.gameObject == boostButton.gameObject)
                            {
                                boostButtonTouched = true;
                            }
                        }
                    }
                }

                if (carController != null)
                {
                    if (steeringWheelTouched)
                    {
                        if (steeringWheelGraphic != null) steeringWheelGraphic.color = colorTouched;
                        steeringWheel.localRotation = Quaternion.Euler(0, 0, -steeringWheelDelta * 90);
                        carController.setSteering(steeringWheelDelta);
                    }
                    else
                    {
                        if (steeringWheelGraphic != null) steeringWheelGraphic.color = colorIdle;
                        steeringWheel.localRotation = Quaternion.identity;
                        carController.setSteering(0);
                    }

                    if (gasPedalTouched)
                    {
                        if (gasPedalGraphic != null) gasPedalGraphic.color = colorTouched;
                        if (brakePedalGraphic != null) brakePedalGraphic.color = colorIdle;
                        carController.setMotor(1);
                    }
                    else if (brakePedalTouched)
                    {
                        if (gasPedalGraphic != null) gasPedalGraphic.color = colorIdle;
                        if (brakePedalGraphic != null) brakePedalGraphic.color = colorTouched;
                        carController.setMotor(-1);
                    }
                    else
                    {
                        if (gasPedalGraphic != null) gasPedalGraphic.color = colorIdle;
                        if (brakePedalGraphic != null) brakePedalGraphic.color = colorIdle;
                        carController.setMotor(0);
                    }

                    if (boostButtonTouched)
                    {
                        if (boostButtonGraphic != null) boostButtonGraphic.color = colorTouched;
                        carController.accelerationMultiplier = accelerationBoostMultiplier;
                        carController.maxSpeedMultiplier = speedBoostMultiplier;
                    }
                    else
                    {
                        if (boostButtonGraphic != null) boostButtonGraphic.color = colorIdle;
                        carController.accelerationMultiplier = 1f;
                        carController.maxSpeedMultiplier = 1f;
                    }
                }
            }
        }
    }
}