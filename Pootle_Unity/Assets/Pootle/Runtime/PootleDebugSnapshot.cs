using UnityEngine;

namespace Pootle
{
    public struct PootleProbeRecord
    {
        public PootleProbeKind kind;
        public PootleProbeStatus status;
        public PootleProbeRejection rejection;
        public Vector3 a, b, direction, hitPoint, hitNormal;
        public float radius, distance;
        public Collider collider;
        public int mask;
        public bool capsule;
    }

    // Bounded, preallocated capture. Rendering only reads these values; it never queries physics.
    public sealed class PootleDebugSnapshot
    {
        public bool frozen, overflow;
        public int tick, count, historyCount;
        public float time, bufferRemaining, coyoteRemaining;
        public Vector3 position, center, velocity, inputDirection, requested, resolved, facing;
        public float radius, height, skin, step, slopeLimit;
        public PootleMode mode, previousMode;
        public PootleTransition reason;
        public PootleSupport support;
        public bool jumpAccepted;
        public readonly PootleProbeRecord[] probes = new PootleProbeRecord[64];
        public readonly PootleTransition[] history = new PootleTransition[32];
        public void Begin() { if (!frozen) { count = 0; overflow = false; } }
        public void Record(PootleProbeRecord record)
        {
            if (frozen) return;
            if (count == probes.Length) { overflow = true; return; }
            probes[count++] = record;
        }
        public void Transition(PootleTransition transition)
        {
            if (frozen) return;
            reason = transition;
            history[historyCount++ % history.Length] = transition;
        }
        public void Clear() { count = historyCount = 0; reason = PootleTransition.None; overflow = false; }
    }
}
