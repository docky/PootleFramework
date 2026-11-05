using UnityEngine;

namespace DavidJalbert.TinyCarController
{
    [System.Serializable]
    public class TCCSurfaceModifier
    {
        [Tooltip("By how much to multiply the car's max speed when driving over this surface.")]
        public float maxSpeedModifier;
        [Tooltip("By how much to multiply the car's acceleration when driving over this surface.")]
        public float accelerationModifier;
        [Tooltip("By how much to multiply the car's tire grip when driving over this surface.")]
        public float tireGripModifier;

        internal TCCSurfaceModifier()
        {
            resetToDefault();
        }

        internal TCCSurfaceModifier(float ms, float a, float tg)
        {
            maxSpeedModifier = ms;
            accelerationModifier = a;
            tireGripModifier = tg;
        }

        public static TCCSurfaceModifier operator +(TCCSurfaceModifier left, TCCSurfaceModifier right) => new TCCSurfaceModifier(
            left.maxSpeedModifier + right.maxSpeedModifier,
            left.accelerationModifier + right.accelerationModifier,
            left.tireGripModifier + right.tireGripModifier
        );

        public static TCCSurfaceModifier operator -(TCCSurfaceModifier left, TCCSurfaceModifier right) => new TCCSurfaceModifier(
            left.maxSpeedModifier - right.maxSpeedModifier,
            left.accelerationModifier - right.accelerationModifier,
            left.tireGripModifier - right.tireGripModifier
        );

        public static TCCSurfaceModifier operator *(TCCSurfaceModifier left, float right) => new TCCSurfaceModifier(
            left.maxSpeedModifier * right,
            left.accelerationModifier * right,
            left.tireGripModifier * right
        );

        public static TCCSurfaceModifier operator /(TCCSurfaceModifier left, float right) => new TCCSurfaceModifier(
            left.maxSpeedModifier / right,
            left.accelerationModifier / right,
            left.tireGripModifier / right
        );

        internal void clear()
        {
            maxSpeedModifier = 0;
            accelerationModifier = 0;
            tireGripModifier = 0;
        }

        internal void resetToDefault()
        {
            maxSpeedModifier = 1;
            accelerationModifier = 1;
            tireGripModifier = 1;
        }
    }
}