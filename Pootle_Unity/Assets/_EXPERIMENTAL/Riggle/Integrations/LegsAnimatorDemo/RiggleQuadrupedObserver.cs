using UnityEngine;
namespace Docky.RiggleTools.Demo
{
    [DefaultExecutionOrder(1000)]
    public sealed class RiggleQuadrupedObserver : MonoBehaviour
    {
        RiggleQuadrupedWalk walk;
        void Start(){walk=GetComponent<RiggleQuadrupedWalk>();}
        void LateUpdate(){if(walk)walk.Observe();}
    }
}
