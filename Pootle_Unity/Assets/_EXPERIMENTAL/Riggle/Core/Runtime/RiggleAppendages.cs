using System;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Optional authoring paths. All positions are in the Riggle root's local space.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Riggle/Riggle Appendages")]
    public sealed class RiggleAppendages : MonoBehaviour
    {
        [Serializable] public sealed class Guide
        {
            public Vector3 position;
            [Tooltip("Optional source origin or empty. With a reference, Offset is in that object's local space.")]
            public Transform reference;
            public Vector3 offset;
        }
        [Serializable] public sealed class Chain
        {
            [Range(2,16)] public int segments = 4;
            [Range(.01f,.2f)] public float radius = .04f;
            public Guide[] guides = Array.Empty<Guide>();
            [Tooltip("Ponytails only: Y rotation around the first guide; 0 faces +Z, 180 faces backward.")]
            public float yawDegrees;
            [Tooltip("Additional pitch, yaw and roll for ponytail/ear guide paths, around their root.")]
            public Vector3 orientationDegrees;
        }
        public bool bunnyEars, longTail, backWings;
        [Tooltip("Leave ear bones to an external dynamic bone system instead of the FK driver.")]
        public bool dynamicEars;
        public bool mirrorEars = true, mirrorWings = true;
        public bool showGuides = true;
        public Chain leftEar = new Chain(), rightEar = new Chain();
        public Chain tail = new Chain { segments = 8, radius = .05f };
        public Chain leftWing = new Chain { radius = .065f }, rightWing = new Chain { radius = .065f };
        public bool middlePonytail, leftPonytail, rightPonytail;
        public Chain middlePonytailChain = new Chain { yawDegrees = 180 };
        public Chain leftPonytailChain = new Chain { yawDegrees = -90 };
        public Chain rightPonytailChain = new Chain { yawDegrees = 90 };
        public bool AnyEnabled => enabled && (bunnyEars || longTail || backWings || middlePonytail || leftPonytail || rightPonytail);
    }
}
