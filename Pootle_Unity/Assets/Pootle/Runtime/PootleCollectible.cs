using Obvious.Soap;
using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Soap Collectible")]
    public sealed class PootleCollectible : MonoBehaviour
    {
        public IntVariable counter;
        [Min(1)] public int amount = 1;
        [Min(0)] public float bobHeight = .16f;
        [Min(0)] public float bobFrequency = .65f;
        public float spinSpeed = 120;
        public float phase;
        public bool Collected { get; private set; }
        Vector3 origin;
        Quaternion rotation;
        float elapsed;
        void OnEnable()
        {
            origin = transform.localPosition; rotation = transform.localRotation;
            elapsed = 0; Collected = false;
        }
        void Update()
        {
            elapsed += Time.deltaTime;
            transform.localPosition = origin + Vector3.up * (Mathf.Sin(elapsed * bobFrequency * Mathf.PI * 2 + phase) * bobHeight);
            transform.localRotation = rotation * Quaternion.Euler(0, elapsed * spinSpeed, 0);
        }
        void OnTriggerEnter(Collider other) => TryCollect(other.GetComponentInParent<PootleCharacter>());
        void OnTriggerStay(Collider other) => TryCollect(other.GetComponentInParent<PootleCharacter>());
        public bool TryCollect(PootleCharacter collector)
        {
            if (!isActiveAndEnabled || Collected || !collector || !collector.isActiveAndEnabled || !counter) return false;
            Collected = true;
            counter.Add(Mathf.Max(1, amount));
            gameObject.SetActive(false);
            return true;
        }
    }
}
