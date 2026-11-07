using UnityEngine;
using UnityEngine.Animations.Rigging;
namespace ControlRig
{
    /// <summary>Choose who owns the body pose. An animation must not fight fixed IK/hip targets.</summary>
    [ExecuteAlways, DefaultExecutionOrder(-10000), DisallowMultipleComponent]
    public sealed class ControlRigAnimation : MonoBehaviour
    {
        public enum PoseSource { Automatic, Animation, Controls }
        [Tooltip("Automatic plays Animator Controllers freely. Choose Animation for external PlayableGraphs; Controls for IK/FK editing.")]
        public PoseSource poseSource;
        [Tooltip("Apply the clip's root translation and rotation when animation owns the pose.")]
        public bool rootMotion = true;
        public bool AnimationOwnsPose => poseSource == PoseSource.Animation ||
            poseSource == PoseSource.Automatic && GetComponent<Animator>() && GetComponent<Animator>().runtimeAnimatorController;
        public void Refresh()
        {
            var animator=GetComponent<Animator>();if(!animator)return;
            bool animation=AnimationOwnsPose;
            var builder=GetComponent<RigBuilder>();
            if(builder && builder.enabled==animation)builder.enabled=!animation;
            foreach(var refs in GetComponentsInChildren<Rig_References>(true))
            {
                var rig=refs.GetComponent<Rig>();if(rig)rig.weight=animation?0:1;
            }
            bool applyMotion=animation && rootMotion;
            if(animator.applyRootMotion!=applyMotion)animator.applyRootMotion=applyMotion;
        }
        void OnEnable()=>Refresh();
        void Update()=>Refresh();
    }
}
