using UnityEngine;
using Sirenix.OdinInspector;

namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class PootleCavePortal : MonoBehaviour
    {
        public PootleCaveTravel travel;
        [Tooltip("Feet position after the hidden teleport. The marker's forward is the arrival facing.")]
        public Transform destination;
        public Transform arrivalWalkEnd;
        public Transform approachEnd;
        public bool dropEntrance;
        public bool dropArrival;
        [Min(0)] public float bufferPadding = 1.2f;
        [Min(0)] public float rearmDelay = 1.25f;
        [ShowInInspector, ReadOnly] public bool Armed { get; private set; } = true;
        float readyAt;
        BoxCollider volume;
        public BoxCollider Volume => volume ? volume : (volume = GetComponent<BoxCollider>());
        public bool Contains(Vector3 feet, float padding = 0)
        {
            if (!isActiveAndEnabled || !Volume.enabled) return false;
            Vector3 p = transform.InverseTransformPoint(feet) - Volume.center;
            Vector3 half = Volume.size * .5f + new Vector3(padding, .25f, padding);
            if (Mathf.Abs(p.y) > half.y) return false;
            return dropEntrance ? new Vector2(p.x / half.x, p.z / half.z).sqrMagnitude <= 1
                : Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.z) <= half.z;
        }
        public void Disarm(float now) { Armed = false; readyAt = now + rearmDelay; }
        public void RefreshArming(Vector3 feet, float now)
        {
            if (!Armed && now >= readyAt && !Contains(feet, bufferPadding)) Armed = true;
        }
        [Button("Test entry"), EnableIf("@UnityEngine.Application.isPlaying")]
        public void TestEntry() { if (Application.isPlaying && travel) travel.Begin(this); }
        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = Armed ? Color.cyan : new Color(1, .65f, .2f);
            Gizmos.DrawWireCube(Volume.center, Volume.size);
            Gizmos.color = new Color(1, .65f, .2f, .5f);
            Gizmos.DrawWireCube(Volume.center, Volume.size + new Vector3(bufferPadding * 2, 0, bufferPadding * 2));
            Gizmos.matrix = Matrix4x4.identity;
            if (destination) { Gizmos.DrawLine(transform.position, destination.position); Gizmos.DrawSphere(destination.position, .2f); }
            if (arrivalWalkEnd && destination) Gizmos.DrawLine(destination.position, arrivalWalkEnd.position);
        }
    }
}
