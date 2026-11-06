using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Pootle
{
    // Box membership is sampled in 3D, so teleports and controllers without Rigidbody triggers work.
    [DefaultExecutionOrder(-80), DisallowMultipleComponent]
    public sealed class PootleZoneCameraDirector : MonoBehaviour
    {
        public Transform target;
        public PootleCharacter character;
        public PootleCameraFocus focus;
        public CinemachineBrain brain;
        public PootleMovementBasis movementBasis;
        public CinemachineCamera fallbackCamera;
        public PootleCameraZone[] zones;
        [FoldoutGroup("Handovers"), Min(0)] public float blendSeconds = 1.2f;
        [FoldoutGroup("Handovers"), Min(0)] public float minimumDwell = .65f;
        [FoldoutGroup("Handovers")] public bool deferYawChangesInAir = true;
        [FoldoutGroup("Handovers"), Range(0, 90)] public float airborneYawThreshold = 12;
        [ShowInInspector, ReadOnly] public PootleCameraZone ActiveZone { get; private set; }
        [ShowInInspector, ReadOnly] public CinemachineCamera ActiveCamera { get; private set; }
        [ShowInInspector, ReadOnly] public string Status { get; private set; } = "Waiting for target";
        public bool IsPreviewing { get; private set; }
        float lastSwitch = float.NegativeInfinity;
        PootleCameraZone previewZone;
        Coroutine handoverTest;
        void OnEnable() { if(character)character.Teleported += OnTeleport; }
        void OnDisable()
        {
            if(character)character.Teleported -= OnTeleport;
            if(ActiveCamera)ActiveCamera.Priority=0;
            if(fallbackCamera)fallbackCamera.Priority=10;
            ActiveCamera=null;ActiveZone=null;IsPreviewing=false;lastSwitch=float.NegativeInfinity;
            if(handoverTest!=null)StopCoroutine(handoverTest);handoverTest=null;
        }
        void OnTeleport(Vector3 position)
        {
            if(focus)focus.Snap(); if(movementBasis)movementBasis.Release();
            if(zones!=null)foreach(var zone in zones)if(zone && zone.cameraRig && zone.cameraRig.TryGetComponent<PootleExplorationShot>(out var shot))shot.ResetTracking();
            if(fallbackCamera && fallbackCamera.TryGetComponent<PootleExplorationShot>(out var fallback))fallback.ResetTracking();
            EvaluateAt(Time.time,false,true);
        }
        void LateUpdate() => Evaluate();
        public void Evaluate() => EvaluateAt(Time.time, focus && focus.IsAirborne, !Application.isPlaying);
        public PootleCameraZone SelectRegion(Vector3 position, bool useHysteresis = true)
        {
            PootleCameraZone best=null;
            if(zones==null)return null;
            // Stable array order is the final tie breaker, never instance IDs or trigger entry order.
            foreach(var zone in zones)
            {
                if(!zone || !zone.Contains(position, useHysteresis && zone==ActiveZone))continue;
                if(!best || zone.precedence>best.precedence || zone.precedence==best.precedence && zone.tieBreakOrder<best.tieBreakOrder)best=zone;
            }
            return best;
        }
        public void EvaluateAt(float now, bool airborne, bool force=false)
        {
            if(!target)return;
            var selected=IsPreviewing ? previewZone : SelectRegion(target.position);
            var next=selected && selected.IsAvailable ? selected.cameraRig : fallbackCamera;
            if(next==ActiveCamera) { ActiveZone=selected;Status=IsPreviewing?"Preview override":"Tracking";return; }
            bool currentAvailable=ActiveCamera && ActiveCamera.isActiveAndEnabled && (!ActiveZone || ActiveZone.IsAvailable);
            float dwell=ActiveZone && ActiveZone.overrideDwell ? ActiveZone.minimumDwell : minimumDwell;
            if(!force && currentAvailable && now-lastSwitch<dwell) { Status="Holding minimum dwell";return; }
            float yaw=ShotYaw(next);
            if(!force && currentAvailable && airborne && deferYawChangesInAir && Mathf.Abs(Mathf.DeltaAngle(ShotYaw(ActiveCamera),yaw))>airborneYawThreshold)
            { Status="Yaw handover pending landing";return; }
            float blend=selected && selected.overrideBlend ? selected.blendSeconds : blendSeconds;
            if(brain)brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,blend);
            // Lock BEFORE the Brain starts blending, preserving the movement basis used this frame.
            if(ActiveCamera && movementBasis)movementBasis.BeginHandover(yaw,blend,now);
            if(fallbackCamera)fallbackCamera.Priority=0;
            if(zones!=null)foreach(var zone in zones)if(zone && zone.cameraRig)zone.cameraRig.Priority=0;
            ActiveCamera=next;ActiveZone=selected;lastSwitch=now;
            if(next)next.Priority=100;
            Status=IsPreviewing?"Preview override":"Handing over";
        }
        static float ShotYaw(CinemachineCamera camera) => !camera ? 0 : camera.TryGetComponent<PootleExplorationShot>(out var shot) ? shot.AuthoredYaw : camera.transform.eulerAngles.y;
        public void Preview(PootleCameraZone region) { IsPreviewing=true;previewZone=region;EvaluateAt(Time.time,false,true); }
        [Button("Return to automatic regions")]
        public void ReturnToRegions()
        {
            if(handoverTest!=null)StopCoroutine(handoverTest);handoverTest=null;
            IsPreviewing=false;previewZone=null;EvaluateAt(Time.time,false,true);
        }
        public void TestHandover(PootleCameraZone region)
        {
            if(handoverTest!=null)StopCoroutine(handoverTest);
            handoverTest=StartCoroutine(HandoverRoutine(region));
        }
        IEnumerator HandoverRoutine(PootleCameraZone region)
        { Preview(null);yield return new WaitForSeconds(blendSeconds+.3f);Preview(region);handoverTest=null; }
    }
}
