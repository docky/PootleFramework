using UnityEngine;

namespace Pootle
{
    // The ocean is scenery; return to the trailhead if the explorer falls off the island.
    [RequireComponent(typeof(PootleCharacter))]
    public sealed class PootleCoastalRecovery : MonoBehaviour
    {
        public Vector3 trailhead = new Vector3(-16, .2f, -5);
        public float waterLevel = -.8f;
        private PootleCharacter character;
        private void Awake() => character = GetComponent<PootleCharacter>();
        private void Update()
        {
            if (transform.position.y < waterLevel) character.Teleport(trailhead, Quaternion.identity);
        }
    }
}
