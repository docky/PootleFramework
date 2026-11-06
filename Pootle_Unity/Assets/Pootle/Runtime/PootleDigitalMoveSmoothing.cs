using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Pootle
{
    /// <summary>Per-reader movement smoothing with stable direction snapping for analog controls.</summary>
    [Serializable]
    public sealed class PootleDigitalMoveSmoothing
    {
        [Tooltip("Smooth both analog and digital movement. Direction snapping is configured separately below.")]
        public bool enabled = true;
        [Tooltip("Seconds from rest to full input. Zero responds immediately."), Min(0)]
        public float accelerationTime = .12f;
        [Tooltip("Seconds from full input to rest. Zero stops immediately."), Min(0)]
        public float releaseTime = .08f;
        [Tooltip("Seconds for a full direction reversal. Zero turns immediately."), Min(0)]
        public float turnTime = .12f;
        [Tooltip("Analog direction steps around a full circle. 16 gives 22.5 degree steps; zero disables snapping. Stick magnitude is preserved."), Range(0, 64)]
        public int analogDirectionSteps = 16;
        [Tooltip("Extra degrees beyond a direction boundary before switching sectors. Prevents jitter near the boundary."), Min(0)]
        public float analogDirectionHysteresis = 3f;
        [Tooltip("Extra digital controls exposed by hardware as analog axes. Buttons and D-pads are detected automatically.")]
        public string[] digitalControlPaths = { "<HID:: Retro Games LTD  Atari CX Wireless Controller>/stick" };

        [NonSerialized] private Vector2 value;
        [NonSerialized] private bool digital;
        [NonSerialized] private InputDevice device;
        [NonSerialized] private Vector2 previousTarget;
        [NonSerialized] private bool turning;
        [NonSerialized] private InputControl directionControl;
        [NonSerialized] private bool hasAnalogDirection;
        [NonSerialized] private float analogDirection;
        [NonSerialized] private int directionSteps;
        public bool DigitalActive => digital;

        public Vector2 Read(InputAction action, float deltaTime)
        {
            if (action == null || !action.enabled) { Reset(); return Vector2.zero; }
            var target = Vector2.ClampMagnitude(action.ReadValue<Vector2>(), 1);
            var control = action.activeControl;
            if (control != null)
            {
                if (directionControl != control) hasAnalogDirection = false;
                directionControl = control;
                device = control.device;
                digital = IsDigital(control);
            }
            // A canceled action has no active control: retain its source while releasing.
            if (device != null && !device.added) { Reset(); return Vector2.zero; }
            if (!digital) target = SnapAnalogDirection(target);
            else hasAnalogDirection = false;
            // Magnitude changes must not restart an in-progress reversal at its zero crossing.
            if ((target == Vector2.zero) != (previousTarget == Vector2.zero) ||
                (target != Vector2.zero && previousTarget != Vector2.zero &&
                 Vector2.Dot(target.normalized, previousTarget.normalized) < .999f))
            {
                turning = target != Vector2.zero && value.sqrMagnitude > 0 &&
                    Vector2.Dot(value.normalized, target.normalized) < .999f;
            }
            previousTarget = target;
            if (!enabled) { turning = false; return value = target; }
            if (!float.IsFinite(deltaTime) || deltaTime <= 0) return value;

            float duration = target.sqrMagnitude < value.sqrMagnitude ? releaseTime : accelerationTime;
            float distance = 1;
            if (turning)
            {
                duration = turnTime;
                distance = 2;
            }
            // A bounded, time-based ramp reaches exact zero/full input and cannot overshoot.
            value = duration <= 0 ? target : Vector2.MoveTowards(value, target, distance * deltaTime / duration);
            if (value == target) turning = false;
            return value;
        }

        private Vector2 SnapAnalogDirection(Vector2 target)
        {
            int steps = Mathf.Clamp(analogDirectionSteps, 0, 64);
            if (steps == 0 || target == Vector2.zero)
            {
                hasAnalogDirection = false;
                return target;
            }
            float step = 360f / steps;
            float angle = Mathf.Atan2(target.y, target.x) * Mathf.Rad2Deg;
            float hysteresis = Mathf.Clamp(analogDirectionHysteresis, 0, step * .49f);
            if (!hasAnalogDirection || directionSteps != steps ||
                Mathf.Abs(Mathf.DeltaAngle(analogDirection, angle)) > step * .5f + hysteresis)
            {
                analogDirection = Mathf.Round(angle / step) * step;
                directionSteps = steps;
                hasAnalogDirection = true;
            }
            float radians = analogDirection * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * target.magnitude;
        }

        private bool IsDigital(InputControl control)
        {
            if (control is ButtonControl || control is DpadControl) return true;
            if (digitalControlPaths != null)
                foreach (var path in digitalControlPaths)
                    if (!string.IsNullOrEmpty(path) && InputControlPath.Matches(path, control)) return true;
            return false;
        }

        public void Reset()
        {
            value = Vector2.zero;
            digital = false;
            device = null;
            previousTarget = Vector2.zero;
            turning = false;
            directionControl = null;
            hasAnalogDirection = false;
            analogDirection = 0;
            directionSteps = 0;
        }
    }
}
