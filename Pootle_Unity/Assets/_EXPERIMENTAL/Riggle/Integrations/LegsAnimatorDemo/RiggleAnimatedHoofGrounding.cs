using FIMSpace.FProceduralAnimation;
using UnityEngine;

namespace Docky.RiggleTools.Demo
{
    // Sole/reach correction only: the imported clip and Legs Animator own step timing.
    public sealed class RiggleAnimatedHoofGrounding : LegsAnimatorControlModuleBase
    {
        float[] lengths;
        public override void OnInit(LegsAnimator.LegsAnimatorCustomModuleHelper helper)
        {
            lengths=new float[LA.Legs.Count];
            for(int i=0;i<lengths.Length;i++)
            {
                var leg=LA.Legs[i];
                for(var bone=leg.BoneEnd;bone&&bone!=leg.BoneStart;bone=bone.parent)
                    lengths[i]+=Vector3.Distance(bone.position,bone.parent.position);
            }
        }
        public override void OnLateUpdatePreApply(LegsAnimator.LegsAnimatorCustomModuleHelper helper)
        {
            float lower=0;
            for(int i=0;i<LA.Legs.Count;i++)
            {
                var leg=LA.Legs[i];var target=leg.GetFinalIKPos();
                var offset=leg.GetFinalIKRot()*Vector3.Scale(leg.AnkleToHeel,leg.BoneEnd.lossyScale);
                var sole=target+offset;
                if(Physics.Raycast(sole+Vector3.up*1.5f,Vector3.down,out var hit,3,LA.GroundMask,QueryTriggerInteraction.Ignore))
                {
                    float floor=hit.point.y-offset.y;
                    target.y=leg.G_Attached?floor:Mathf.Max(target.y,floor+.006f);
                    leg.OverrideFinalIKPos(target);
                }
                var delta=leg.BoneStart.position-target;float horizontal=delta.x*delta.x+delta.z*delta.z,reach=lengths[i]*.99f;
                if(horizontal<reach*reach)lower=Mathf.Min(lower,target.y+Mathf.Sqrt(reach*reach-horizontal)-leg.BoneStart.position.y);
            }
            LA.Hips.position+=Vector3.up*Mathf.Max(-.2f,lower);
        }
    }
}
