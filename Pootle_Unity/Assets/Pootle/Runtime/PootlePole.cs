using UnityEngine;

namespace Pootle
{
    [AddComponentMenu("Pootle/Climbable Pole or Rope")]
    public sealed class PootlePole : MonoBehaviour
    {
        [Min(0.1f)] public float length = 6;
        [Min(0.01f)] public float radius = 0.12f;
        public Vector3 Bottom => transform.position - transform.up * length * 0.5f;
        public Vector3 Top => Bottom + transform.up * length;
        // Use a trigger collider so a facing-directed jump can pass across the pole.
        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c) c.isTrigger = true;
        }
    }
}
