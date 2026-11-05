using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pootle
{
    public enum PootleFrobjectState { Hidden, Aware, Ready }

    [DisallowMultipleComponent, RequireComponent(typeof(PootleCharacter)), AddComponentMenu("Pootle/Frobject/Sensor")]
    public sealed class PootleFrobjectSensor : MonoBehaviour
    {
        [Min(0)] public float awarenessDistance = 8;
        [Min(0)] public float focusDistance = 2.5f;
        [Tooltip("Half-angle either side of the character's facing, in degrees.")]
        [Range(0, 180)] public float focusHalfAngle = 45;
        public Vector3 localOrigin = Vector3.up;
        public LayerMask targetLayers = ~0;
        public bool requireLineOfSight = true;
        public LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
        public bool interactionEnabled = true;
        [Min(0)] public float maximumReadySpeed = 1;
        public PootleFrobject Focused { get; private set; }
        public IReadOnlyList<PootleFrobject> Aware => aware;
        public event Action<PootleFrobject> FocusChanged;
        public PootleCharacter Character { get; private set; }
        public bool PlayerReady => isActiveAndEnabled && interactionEnabled && Character && Character.isActiveAndEnabled &&
            Character.Mode == PootleMode.Grounded && !Character.SlideRideActive && !Character.IsRolling &&
            (!carrying || !carrying.Held) && Character.Velocity.magnitude <= Mathf.Max(0, maximumReadySpeed);
        public bool CanInteract => PlayerReady && Focused && Focused.Available;
        readonly List<PootleFrobject> aware = new List<PootleFrobject>();
        RaycastHit[] hits = new RaycastHit[16];
        PootleCarrying carrying;
        bool invoking;

        void Awake() { Character = GetComponent<PootleCharacter>(); carrying = GetComponent<PootleCarrying>(); }
        void LateUpdate() => Refresh();
        void OnDisable() { aware.Clear(); SetFocus(null); }

        public PootleFrobjectState GetState(PootleFrobject target)
        {
            if (!isActiveAndEnabled || !Character || !Character.isActiveAndEnabled || !target || !target.isActiveAndEnabled || !aware.Contains(target))
                return PootleFrobjectState.Hidden;
            return target == Focused && CanInteract ? PootleFrobjectState.Ready : PootleFrobjectState.Aware;
        }

        public void Refresh()
        {
            aware.Clear();
            if (!isActiveAndEnabled || !Character || !Character.isActiveAndEnabled) { SetFocus(null); return; }
            Vector3 origin = transform.TransformPoint(localOrigin);
            Vector3 facing = Vector3.ProjectOnPlane(Character.Facing, Vector3.up).normalized;
            float radius = Mathf.Max(0, awarenessDistance), reach = Mathf.Clamp(focusDistance, 0, radius);
            float angle = Mathf.Clamp(focusHalfAngle, 0, 180), bestScore = float.PositiveInfinity;
            PootleFrobject best = null;
            foreach (var target in PootleFrobject.Active)
            {
                if (!target || !target.isActiveAndEnabled || (targetLayers.value & (1 << target.gameObject.layer)) == 0) continue;
                Vector3 offset = target.Position - origin;
                float distance = offset.magnitude;
                if (distance > radius || requireLineOfSight && !Visible(target, origin, offset, distance)) continue;
                aware.Add(target);
                if (distance > reach || !target.Available) continue;
                Vector3 planar = Vector3.ProjectOnPlane(offset, Vector3.up);
                float facingAngle = planar.sqrMagnitude < .0001f ? 0 : Vector3.Angle(facing, planar);
                if (facingAngle > angle) continue;
                float score = facingAngle / Mathf.Max(1, angle) + .25f * distance / Mathf.Max(.001f, reach);
                // Stable ties prevent flicker between overlapping interaction points.
                if (score < bestScore - .0001f || Mathf.Abs(score - bestScore) <= .0001f && target == Focused)
                { best = target; bestScore = score; }
            }
            SetFocus(best);
        }

        bool Visible(PootleFrobject target, Vector3 origin, Vector3 offset, float distance)
        {
            if (distance < .001f) return true;
            int count;
            // Grow on saturation so a crowded scene cannot hide an obstruction beyond a fixed buffer.
            while ((count = Physics.RaycastNonAlloc(origin, offset / distance, hits, distance, obstructionLayers, QueryTriggerInteraction.Ignore)) == hits.Length)
                Array.Resize(ref hits, hits.Length * 2);
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].collider.transform;
                if (hit.IsChildOf(transform) || hit.IsChildOf(target.transform)) continue;
                return false;
            }
            return true;
        }

        void SetFocus(PootleFrobject target)
        {
            if (Focused == target) return;
            Focused = target;
            FocusChanged?.Invoke(target);
        }

        public bool TryInteract()
        {
            if (invoking) return false;
            Refresh(); // Never act on a target cached before a move, disable, or readiness change.
            if (!CanInteract) return false;
            invoking = true;
            try { return Focused.InvokeInteraction(this); }
            finally { invoking = false; Refresh(); }
        }

        void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.TransformPoint(localOrigin);
            Gizmos.color = new Color(.4f, .8f, 1, .5f); Gizmos.DrawWireSphere(origin, awarenessDistance);
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(origin, Mathf.Min(focusDistance, awarenessDistance));
            Vector3 forward = Character ? Character.Facing : transform.forward;
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(focusHalfAngle, Vector3.up) * forward * focusDistance);
            Gizmos.DrawRay(origin, Quaternion.AngleAxis(-focusHalfAngle, Vector3.up) * forward * focusDistance);
        }
    }
}
