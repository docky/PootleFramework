using UnityEngine;
using Sirenix.OdinInspector;

namespace Pootle
{
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class PootleCameraFocus : MonoBehaviour
    {
        public Transform player;
        public PootleCharacter character;
        [FoldoutGroup("Tracking")] public Vector3 offset = new Vector3(0, 1.5f, 0);
        [FoldoutGroup("Tracking"), Min(.01f)] public float horizontalDamping = .3f;
        [FoldoutGroup("Tracking"), Min(.01f)] public float verticalDamping = .8f;
        [FoldoutGroup("Tracking"), Range(0, .4f)] public float lookahead = .16f;
        [FoldoutGroup("Tracking"), Min(0)] public float maxLookahead = 1.5f;
        [FoldoutGroup("Tracking"), Min(0)] public float climbingHeadroom = 1.5f;
        [FoldoutGroup("Tracking"), Min(1)] public float teleportDistance = 10;
        public bool IsAirborne => character && (character.Mode == PootleMode.Airborne || character.Mode == PootleMode.Gliding || character.Mode == PootleMode.GroundPounding);
        Vector3 previous, velocity, smoothVelocity;
        bool initialized;
        void OnEnable() { if (character) character.Teleported += OnTeleport; Snap(); }
        void OnDisable() { if (character) character.Teleported -= OnTeleport; initialized = false; }
        void OnTeleport(Vector3 position) => Snap();
        [Button("Snap focus to player")]
        public void Snap()
        {
            if (!player) return;
            previous = player.position; velocity = smoothVelocity = Vector3.zero;
            transform.SetPositionAndRotation(player.position + offset, Quaternion.identity);
            initialized = true;
        }
        void LateUpdate() => Step(Time.deltaTime);
        public void Step(float dt)
        {
            if (!player || dt <= 0) return;
            if (!initialized || Vector3.Distance(previous, player.position) > teleportDistance) { Snap(); return; }
            var measured = Vector3.ProjectOnPlane((player.position - previous) / dt, Vector3.up);
            previous = player.position;
            velocity = Vector3.Lerp(velocity, measured, 1 - Mathf.Exp(-dt / .2f));
            var desired = player.position + offset + Vector3.ClampMagnitude(velocity * lookahead, maxLookahead);
            if (character && (character.Mode == PootleMode.Climbing || character.Mode == PootleMode.LedgeHanging)) desired.y += climbingHeadroom;
            var p = transform.position;
            p.x = Mathf.SmoothDamp(p.x, desired.x, ref smoothVelocity.x, horizontalDamping, Mathf.Infinity, dt);
            p.z = Mathf.SmoothDamp(p.z, desired.z, ref smoothVelocity.z, horizontalDamping, Mathf.Infinity, dt);
            p.y = Mathf.SmoothDamp(p.y, desired.y, ref smoothVelocity.y, verticalDamping, Mathf.Infinity, dt);
            transform.SetPositionAndRotation(p, Quaternion.identity);
        }
    }
}
