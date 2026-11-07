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
        }
        public bool bunnyEars, longTail, backWings;
        public bool mirrorEars = true, mirrorWings = true;
        public bool showGuides = true;
        public Chain leftEar = new Chain(), rightEar = new Chain();
        public Chain tail = new Chain { segments = 8, radius = .05f };
        public Chain leftWing = new Chain { radius = .065f }, rightWing = new Chain { radius = .065f };
        public bool AnyEnabled => enabled && (bunnyEars || longTail || backWings);
    }
}
