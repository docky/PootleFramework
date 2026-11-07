using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Optional override for a named mesh or group in Named Body Parts mode.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Riggle/Riggle Body Part")]
    public sealed class RiggleBodyPart : MonoBehaviour
    {
        public enum Kind { Arm, Leg, Torso, Head, Ear, Tail, Wing }
        public enum Side { Left, Right }
        public Kind kind;
        [Tooltip("Used for arms, legs, ears and wings.")]
        public Side side;
        [Tooltip("Include Hand for an arm, or Foot for a leg. Otherwise only the upper/lower limb bones influence this chunk.")]
        public bool includeHandOrFoot;
        [Tooltip("Include Neck for a torso or head chunk. The torso never includes shoulder or arm bones.")]
        public bool includeNeck;
        [Tooltip("Keep this chunk in its own skinned renderer, sharing the character skeleton. Also applies to unnamed mesh descendants.")]
        public bool keepSeparateRenderer;
    }
}
