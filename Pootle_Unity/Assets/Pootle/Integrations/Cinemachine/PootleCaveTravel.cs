using UnityEngine;
using Sirenix.OdinInspector;

namespace Pootle
{
    public enum CaveTravelPhase { Idle, Closing, Black, Opening, WalkClear }

    // Owns only temporary scripted input. The existing character still performs movement/physics.
    [DefaultExecutionOrder(-150), DisallowMultipleComponent]
    public sealed class PootleCaveTravel : MonoBehaviour
    {
        public PootleCharacter character;
        public PootleZoneCameraDirector director;
        public PootleCircleIris iris;
        public PootleCavePortal[] portals;
        [Min(.1f)] public float closeSeconds = .65f;
        [Min(.05f)] public float blackSeconds = .2f;
        [Min(.1f)] public float openSeconds = .75f;
        [Range(.1f, 1)] public float walkInput = .42f;
        [Min(.5f)] public float maximumWalkClearSeconds = 2.5f;
        [ShowInInspector, ReadOnly] public CaveTravelPhase Phase { get; private set; }
        [ShowInInspector, ReadOnly] public int TransferCount { get; private set; }
        public bool Busy => Phase != CaveTravelPhase.Idle;
        public PootleCavePortal ActivePortal { get; private set; }
        float elapsed, clearTime;
        bool originalAutomatic, ownsControl;
        Transform originalOutput;
        PootleCameraZone previousPreview;
        bool wasPreviewing;
        Vector3 safeStart;
        Quaternion safeFacing;
        bool transferred;
        void Update() => Step(Time.unscaledDeltaTime);
        public bool Begin(PootleCavePortal portal)
        {
            if (Busy || !character || !director || !iris || !portal || !portal.destination) return false;
            ActivePortal = portal; transferred = false;
            safeStart = character.transform.position; safeFacing = Quaternion.LookRotation(character.Facing);
            originalAutomatic = character.simulateAutomatically; originalOutput = character.outputCamera; ownsControl = true;
            character.simulateAutomatically = false;
            // Scripted moves are world-space; normal camera-relative input is restored on completion.
            character.outputCamera = null;
            wasPreviewing = director.IsPreviewing; previousPreview = director.ActiveZone;
            director.Preview(director.ActiveZone);
            if (portals != null) foreach (var p in portals) if (p) p.Disarm(Time.unscaledTime);
            Phase = CaveTravelPhase.Closing; elapsed = clearTime = 0; iris.SetOpen(1);
            return true;
        }
        public void Step(float dt)
        {
            if (!character || dt <= 0) return;
            dt = Mathf.Min(dt, .1f);
            if (!Busy)
            {
                if (portals == null) return;
                foreach (var portal in portals)
                {
                    if (!portal) continue;
                    portal.RefreshArming(character.transform.position, Time.unscaledTime);
                    if (portal.Armed && portal.Contains(character.transform.position) && Begin(portal)) break;
                }
                return;
            }
            if (!ActivePortal || !ActivePortal.destination) { Cancel(); return; }
            elapsed += dt;
            if (Phase == CaveTravelPhase.Closing)
            {
                Walk(ActivePortal.approachEnd, dt);
                iris.SetOpen(1 - Mathf.SmoothStep(0, 1, elapsed / closeSeconds));
                if (elapsed >= closeSeconds) { Phase = CaveTravelPhase.Black; elapsed = 0; iris.SetOpen(0); }
            }
            else if (Phase == CaveTravelPhase.Black)
            {
                // Keep a full black frame before AND after relocating. No 100-unit camera flight is exposed.
                if (!transferred && elapsed >= blackSeconds * .5f)
                {
                    character.Teleport(ActivePortal.destination.position, ActivePortal.destination.rotation);
                    director.ReturnToRegions();
                    director.focus.Snap();
                    director.EvaluateAt(Time.time, false, true);
                    if (director.brain) { director.brain.ResetState(); director.brain.ActiveBlend = null; }
                    if (director.movementBasis) director.movementBasis.Release();
                    transferred = true; TransferCount++;
                }
                if (elapsed >= blackSeconds) { Phase = CaveTravelPhase.Opening; elapsed = 0; }
            }
            else
            {
                bool clear = ActivePortal.dropArrival ? Fall(dt) : Walk(ActivePortal.arrivalWalkEnd, dt);
                clearTime += dt;
                if (Phase == CaveTravelPhase.Opening)
                {
                    iris.SetOpen(Mathf.SmoothStep(0, 1, elapsed / openSeconds));
                    if (elapsed >= openSeconds) { Phase = CaveTravelPhase.WalkClear; iris.SetOpen(1); }
                }
                if (Phase == CaveTravelPhase.WalkClear && (clear || clearTime >= maximumWalkClearSeconds)) Complete();
            }
        }
        bool Walk(Transform point, float dt)
        {
            Vector3 delta = point ? Vector3.ProjectOnPlane(point.position - character.transform.position, Vector3.up) : Vector3.zero;
            bool arrived = !point || delta.magnitude < .18f;
            float magnitude = arrived ? 0 : Mathf.Min(walkInput, delta.magnitude / Mathf.Max(.1f, character.profile.walkSpeed * dt));
            var direction = delta.normalized;
            character.Tick(dt, new PootleInputFrame { move = new Vector2(direction.x, direction.z) * magnitude });
            Physics.SyncTransforms();
            return arrived;
        }
        bool Fall(float dt) { character.Tick(dt, default); Physics.SyncTransforms(); return character.IsGrounded; }
        void Complete()
        {
            iris.SetOpen(1); Phase = CaveTravelPhase.Idle; ActivePortal = null;
            RestoreControl();
        }
        void RestoreControl()
        {
            if (!ownsControl) return;
            ownsControl = false;
            if (character) { character.outputCamera = originalOutput; character.simulateAutomatically = originalAutomatic; }
            if (director)
            {
                if (wasPreviewing) director.Preview(previousPreview); else director.ReturnToRegions();
                if (director.movementBasis) director.movementBasis.Release();
            }
        }
        public void Cancel()
        {
            if (ownsControl && character && !transferred) character.Teleport(safeStart, safeFacing);
            if (iris) iris.SetOpen(1);
            Phase = CaveTravelPhase.Idle; ActivePortal = null; RestoreControl();
        }
        void OnDisable() => Cancel();
    }
}
