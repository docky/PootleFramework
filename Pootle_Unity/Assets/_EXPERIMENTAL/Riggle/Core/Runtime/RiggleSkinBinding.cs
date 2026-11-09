using System;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Optional anatomical constraints for separate parts and disconnected mesh islands.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(MeshFilter))]
    public sealed class RiggleSkinBinding : MonoBehaviour
    {
        public enum Part { Automatic, Torso, Head, LeftArm, RightArm, LeftHand, RightHand, LeftLeg, RightLeg, LeftFoot, RightFoot, Hips, LeftEar, RightEar, Tail, LeftWing, RightWing, MiddlePonytail, LeftPonytail, RightPonytail }
        [Serializable]
        public struct Island
        {
            [Tooltip("Any vertex in the connected island, in the original source mesh. Recheck after replacing or reimporting its topology.")]
            public int seedVertex;
            public Part part;
        }

        [Tooltip("Restricts this mesh to the selected anatomical chain. Automatic retains the unrestricted envelope solver.")]
        public Part wholeMesh;
        [Tooltip("Overrides individual connected islands, including vertices split at UV or normal seams. Source topology is unchanged.")]
        public Island[] islands = Array.Empty<Island>();
    }
}
