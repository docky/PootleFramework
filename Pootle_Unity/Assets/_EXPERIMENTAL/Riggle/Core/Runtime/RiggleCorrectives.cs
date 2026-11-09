using System;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Runs after animation, IK and twist. Only writes blend shapes owned by Riggle.</summary>
    [DefaultExecutionOrder(10010)]
    public sealed class RiggleCorrectives : MonoBehaviour
    {
        [Serializable] public sealed class Binding
        {
            public SkinnedMeshRenderer skin;
            public Transform bone;
            public string shape;
            public Vector3 axis, restDirection;
            public Quaternion restRotation;
            public float startAngle, fullAngle, strength;
            [NonSerialized] Mesh cachedMesh;
            [NonSerialized] int cachedIndex = -1;
            public int Index
            {
                get
                {
                    if (!skin || !skin.sharedMesh) return -1;
                    if (cachedMesh != skin.sharedMesh || cachedIndex < 0 || cachedIndex >= cachedMesh.blendShapeCount || cachedMesh.GetBlendShapeName(cachedIndex) != shape)
                    { cachedMesh = skin.sharedMesh; cachedIndex = cachedMesh.GetBlendShapeIndex(shape); }
                    return cachedIndex;
                }
            }
        }
        [Range(0,1)] public float weight = 1;
        public Binding[] bindings = Array.Empty<Binding>();
        public static float Activation(Quaternion rotation, Quaternion restRotation, Vector3 axis, Vector3 restDirection, float start, float full)
        {
            Vector3 direction = (Quaternion.Inverse(restRotation) * rotation) * restDirection;
            Vector3 a = Vector3.ProjectOnPlane(restDirection, axis);
            Vector3 b = Vector3.ProjectOnPlane(direction, axis);
            if (a.sqrMagnitude < .000001f || b.sqrMagnitude < .000001f) return 0;
            float angle = Vector3.SignedAngle(a, b, axis);
            float t = Mathf.InverseLerp(start, Mathf.Max(start + .01f, full), angle);
            return t * t * (3 - 2 * t);
        }
        public void Evaluate()
        {
            foreach (var b in bindings)
                if (b.bone && b.Index >= 0)
                    b.skin.SetBlendShapeWeight(b.Index, 100 * Mathf.Clamp01(weight) * Mathf.Max(0,b.strength) *
                        Activation(b.bone.localRotation, b.restRotation, b.axis, b.restDirection, b.startAngle, b.fullAngle));
        }
        void LateUpdate() => Evaluate();
        void OnDisable()
        {
            foreach (var b in bindings) if (b.Index >= 0) b.skin.SetBlendShapeWeight(b.Index, 0);
        }
    }
}
