using UnityEngine;
using Sirenix.OdinInspector;

namespace Pootle
{
    [CreateAssetMenu(menuName = "Pootle/Exploration Camera Policy", fileName = "Exploration Camera Policy")]
    public sealed class PootleCameraPolicy : ScriptableObject
    {
        [Min(.25f), Tooltip("1 = authored framing. 1.3 brings cameras closer so objects appear approximately 30% larger. Does not scale characters or colliders.")]
        public float viewedSizeMultiplier = 1.3f;
        [Range(15, 90)] public float fieldOfView = 30;
        public bool enableViewTilt = true;
        [Range(0, 60)] public float viewTiltDegrees = 25;
        public bool limitTiltToCameraElevation = true;
        [ShowInInspector, ReadOnly] public float CameraDistanceMultiplier => 1f / Mathf.Max(.25f, viewedSizeMultiplier);
    }
}
