using System;
using UnityEngine;

namespace Docky.RiggleTools
{
    [DefaultExecutionOrder(10000)]
    public sealed class RiggleTwistDistribution : MonoBehaviour
    {
        [Serializable] public sealed class Chain
        {
            public Transform start, end;
            public Transform[] twists;
            public Vector3 axis;
            public Quaternion restRelative;
        }
        public Chain[] chains = Array.Empty<Chain>();
        [Range(0, 1)] public float weight = 1;
        void LateUpdate() => Evaluate();
        public void Evaluate()
        {
            foreach (var chain in chains)
            {
                if (!chain.start || !chain.end) continue;
                Quaternion relative = Quaternion.Inverse(chain.start.rotation) * chain.end.rotation * Quaternion.Inverse(chain.restRelative);
                Vector3 vector = new Vector3(relative.x, relative.y, relative.z);
                float projected = Vector3.Dot(vector, chain.axis.normalized);
                float angle = Mathf.DeltaAngle(0, 2 * Mathf.Atan2(projected, relative.w) * Mathf.Rad2Deg);
                for (int i = 0; i < chain.twists.Length; i++)
                    if (chain.twists[i]) chain.twists[i].localRotation = Quaternion.AngleAxis(angle * weight * (i + 1f) / chain.twists.Length, chain.axis);
            }
        }
    }
}
