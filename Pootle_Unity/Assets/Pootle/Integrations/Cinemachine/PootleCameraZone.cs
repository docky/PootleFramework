using Unity.Cinemachine;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class PootleCameraZone : MonoBehaviour
    {
        public CinemachineCamera cameraRig;
        public int precedence;
        [Tooltip("Equal priorities use this order, then their order in the director's array. Lower wins.")]
        public int tieBreakOrder;
        [Min(0)] public float exitPadding = .75f;
        [Min(0), Tooltip("Separate vertical hysteresis keeps stacked floors distinct.")]
        public float verticalExitPadding = .15f;
        [FoldoutGroup("Overrides")] public bool overrideBlend;
        [FoldoutGroup("Overrides"), Min(0)] public float blendSeconds = 1.2f;
        [FoldoutGroup("Overrides")] public bool overrideDwell;
        [FoldoutGroup("Overrides"), Min(0)] public float minimumDwell = .65f;
        [FoldoutGroup("Testing")] public Transform playerArrival;
        [FoldoutGroup("Testing")] public bool drawGizmos = true;
        [ShowInInspector, ReadOnly] public ExplorationCameraMode Mode => cameraRig && cameraRig.TryGetComponent<PootleExplorationShot>(out var shot) ? shot.mode : ExplorationCameraMode.FixedAngleFollow;
        public bool IsAvailable => isActiveAndEnabled && GetComponent<BoxCollider>().enabled && cameraRig && cameraRig.isActiveAndEnabled;
        public bool Contains(Vector3 worldPoint, bool retaining = false)
        {
            var box = GetComponent<BoxCollider>();
            if (!IsAvailable) return false;
            var point = transform.InverseTransformPoint(worldPoint) - box.center;
            var padding = retaining ? new Vector3(exitPadding, verticalExitPadding, exitPadding) : Vector3.zero;
            var half = box.size * .5f + padding;
            return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y && Mathf.Abs(point.z) <= half.z;
        }
        PootleZoneCameraDirector Director => FindAnyObjectByType<PootleZoneCameraDirector>();
        [Button("Preview This Shot / Activate Camera"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void PreviewThisShot() { if (Application.isPlaying && Director) Director.Preview(this); }
        [Button("Move Player to Region"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void MovePlayerToRegion()
        {
            if (!Application.isPlaying || !Director || !Director.target) return;
            Vector3 p = playerArrival ? playerArrival.position : transform.TransformPoint(GetComponent<BoxCollider>().center);
            if (Director.character) Director.character.Teleport(p, Quaternion.identity); else Director.target.position = p;
            Director.ReturnToRegions(); Director.EvaluateAt(Time.time, false, true);
        }
        [Button("Test Handover · fallback to this shot"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void TestHandover() { if (Application.isPlaying && Director) Director.TestHandover(this); }
        void Reset() => GetComponent<BoxCollider>().isTrigger = true;
        void OnDrawGizmos()
        {
            if (!drawGizmos) return;
            var box=GetComponent<BoxCollider>(); if (!box) return;
            bool selected = Application.isPlaying && Director && Director.ActiveZone == this;
            Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.color=selected ? new Color(1,.7f,.2f,.8f) : new Color(.2f,.8f,.75f,.35f);
            Gizmos.DrawWireCube(box.center,box.size);
            if(selected) { Gizmos.color=new Color(1,.7f,.2f,.08f);Gizmos.DrawCube(box.center,box.size); }
#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.TransformPoint(box.center+Vector3.up*box.size.y*.5f), name+" · P"+precedence+" · "+Mode);
#endif
            Gizmos.matrix=Matrix4x4.identity;
        }
        void OnDrawGizmosSelected()
        {
            var box=GetComponent<BoxCollider>(); if(!box)return;
            Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=new Color(1,.6f,.2f,.7f);
            Gizmos.DrawWireCube(box.center,box.size+new Vector3(exitPadding,verticalExitPadding,exitPadding)*2);
            Gizmos.matrix=Matrix4x4.identity;
        }
    }
}
