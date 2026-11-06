using UnityEngine;

namespace Pootle
{
    // Each character owns its latch and steering filter; zones contain authoring settings only.
    public sealed class PootlePathInputAlignment
    {
        public PootlePathInputZone ActiveZone { get; private set; }
        public float CorrectionDegrees { get; private set; }
        float velocity, blendTime;
        bool digitalOnly, groundedOnly;

        public Vector3 Align(Vector3 feet, Vector3 direction, bool digital, bool grounded, float dt)
        {
            if (direction.sqrMagnitude < .00001f) { Reset(); return direction; }
            ActiveZone = PootlePathInputZone.FindBest(feet, direction, digital, grounded, ActiveZone, out var target);
            if (ActiveZone)
            {
                blendTime = Mathf.Max(0, ActiveZone.blendTime);
                digitalOnly = ActiveZone.digitalOnly; groundedOnly = ActiveZone.groundedOnly;
            }
            // Never carry digital assistance into analog input or into unsupported airborne movement.
            if (digitalOnly && !digital || groundedOnly && !grounded) { Reset(); return direction; }
            float angle = ActiveZone ? Vector3.SignedAngle(direction, target, Vector3.up) : 0;
            if (blendTime <= 0) { CorrectionDegrees = angle; velocity = 0; }
            else if (float.IsFinite(dt) && dt > 0)
                CorrectionDegrees = Mathf.SmoothDampAngle(CorrectionDegrees, angle, ref velocity, blendTime, Mathf.Infinity, dt);
            return Quaternion.AngleAxis(CorrectionDegrees, Vector3.up) * direction;
        }

        public void Reset()
        {
            ActiveZone = null; CorrectionDegrees = velocity = blendTime = 0;
            digitalOnly = groundedOnly = false;
        }
    }
}
