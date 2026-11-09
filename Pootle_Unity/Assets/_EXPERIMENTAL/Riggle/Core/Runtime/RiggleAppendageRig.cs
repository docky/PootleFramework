using System;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Optional FK driver for generic extra bones. Disable to animate the bones directly.</summary>
    [DefaultExecutionOrder(11000), DisallowMultipleComponent]
    public sealed class RiggleAppendageRig : MonoBehaviour
    {
        [Serializable] public sealed class Chain
        {
            public string name;
            [Tooltip("Leave bones to animation or an external dynamic bone system instead of copying FK controls.")]
            public bool externalMotion;
            public Transform[] bones, controls;
            public Quaternion[] restRotations;
        }
        public Chain[] chains = Array.Empty<Chain>();
        public bool showControls = true;
        void LateUpdate() => Evaluate();
        public void Evaluate()
        {
            foreach (var chain in chains)
            {
                if (chain?.bones == null || chain.controls == null || chain.externalMotion) continue;
                for (int i=0; i<chain.bones.Length && i<chain.controls.Length; i++)
                    if (chain.bones[i] && chain.controls[i]) chain.bones[i].localRotation = chain.controls[i].localRotation;
            }
        }
        void OnDrawGizmosSelected()
        {
            if (!showControls) return;
            Gizmos.color = new Color(.85f,.4f,1);
            foreach (var chain in chains)
            {
                if (chain?.controls == null) continue;
                for (int i=0; i<chain.controls.Length; i++)
                {
                    var t=chain.controls[i]; if (!t) continue;
                    Gizmos.DrawWireSphere(t.position,.025f);
                    if (i>0 && chain.controls[i-1]) Gizmos.DrawLine(chain.controls[i-1].position,t.position);
                }
            }
        }
    }
}
