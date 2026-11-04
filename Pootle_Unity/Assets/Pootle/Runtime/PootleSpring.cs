using UnityEngine;
using UnityEngine.Events;
namespace Pootle
{
    // Selected launch behavior from PLAYER TWO Spring; no tags or gameplay-state dependencies.
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource)), AddComponentMenu("Pootle/Spring")]
    public sealed class PootleSpring : MonoBehaviour
    {
        [Min(0)] public float launchSpeed = 25;
        public Vector3 localLaunchDirection = Vector3.up;
        [Range(0, 1)] public float lateralRetention = 0.5f;
        [Min(0.05f)] public float cooldown = 0.2f;
        public AudioClip sound;
        public UnityEvent onLaunch = new UnityEvent();
        public Vector3 Direction => localLaunchDirection.sqrMagnitude > 0.001f ? transform.TransformDirection(localLaunchDirection).normalized : Vector3.up;
        private void Awake() { var source = GetComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 1; }
        public void Launched() { if (sound) GetComponent<AudioSource>().PlayOneShot(sound); onLaunch.Invoke(); }
        private void OnDrawGizmosSelected() { Gizmos.color = Color.yellow; Gizmos.DrawRay(transform.position, Direction * 2); }
    }
}
