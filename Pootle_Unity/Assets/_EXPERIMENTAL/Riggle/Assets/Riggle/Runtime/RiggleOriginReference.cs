using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Opt-in landmark measured from a source part's pivot, without moving its geometry.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Riggle/Riggle Origin Reference")]
    public sealed class RiggleOriginReference : MonoBehaviour
    {
        public Riggle.Point landmark = Riggle.Point.LeftShoulder;
        [Tooltip("Zero uses this object's origin. Offset is in the source object's local space, including its rotation and scale. Landmark handles edit this offset, never the mesh transform.")]
        public Vector3 localOffset;
        public Vector3 WorldPosition => transform.TransformPoint(localOffset);
    }
}
