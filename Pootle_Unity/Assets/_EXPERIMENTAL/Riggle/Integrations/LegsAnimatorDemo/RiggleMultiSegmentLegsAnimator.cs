using System;
using System.Linq;
using FIMSpace.FProceduralAnimation;
using UnityEngine;

namespace Docky.RiggleTools
{
    [AddComponentMenu("Riggle/Multi Segment Legs Animator")]
    public sealed class RiggleMultiSegmentLegsAnimator : LegsAnimator
    {
        RiggleChainIK[] chains;
        Vector3[] contacts,offsets;
        Quaternion[] rotations;
        protected override bool UseCustomIK=>true;
        protected override void CustomIK_Initialize()
        {
            var rig=GetComponent<RiggleGeneric>();
            if(!rig||!rig.generatedBones)throw new InvalidOperationException("Multi-segment Legs Animator needs a baked Riggle Generic component.");
            var bones=rig.generatedBones.GetComponentsInChildren<Transform>().ToDictionary(t=>t.name);
            chains=Legs.Select(l=>
            {
                var definition=rig.legs.Single(d=>d.upper==l.BoneStart.name&&d.foot==l.BoneEnd.name);
                return new RiggleChainIK(definition.Chain().Select(n=>bones[n]).ToArray(),true);
            }).ToArray();
            contacts=new Vector3[chains.Length];offsets=new Vector3[chains.Length];rotations=new Quaternion[chains.Length];
        }
        protected override void CustomIK_ApplyIK()
        {
            if(chains==null)return;
            for(int i=0;i<chains.Length;i++)
            {
                var leg=Legs[i];float weight=leg.IKProcessor.IKWeight;
                rotations[i]=Quaternion.Slerp(leg.BoneEnd.rotation,leg.GetFinalIKRot(),weight);
                var position=Vector3.Lerp(leg.BoneEnd.position,leg.GetFinalIKPos(),weight);
                offsets[i]=Vector3.Scale(leg.AnkleToHeel,leg.BoneEnd.lossyScale);
                contacts[i]=position+rotations[i]*offsets[i];
            }
            // Rotation limiting changes the pivot required to hold a planted sole.
            // Resolve all feet, then share any necessary reach correction through the hips.
            for(int bodyPass=0;bodyPass<2;bodyPass++)
            {
                float lower=0;
                for(int i=0;i<chains.Length;i++)
                {
                    var leg=Legs[i];if(leg.IKProcessor.IKWeight<=0)continue;
                    for(int pass=0;pass<64;pass++)
                    {
                        chains[i].Solve(contacts[i]-rotations[i]*offsets[i],rotations[i],1024);
                        var limited=leg.BoneEnd.rotation;
                        if(Quaternion.Angle(rotations[i],limited)<.01f)break;
                        rotations[i]=Quaternion.Slerp(rotations[i],limited,.5f);
                    }
                    var target=contacts[i]-leg.BoneEnd.rotation*offsets[i];
                    var delta=leg.BoneStart.position-target;
                    float horizontal=delta.x*delta.x+delta.z*delta.z,reach=chains[i].TotalLength*.999f;
                    if(horizontal<reach*reach)lower=Mathf.Min(lower,target.y+Mathf.Sqrt(reach*reach-horizontal)-leg.BoneStart.position.y);
                }
                if(bodyPass==0&&lower<-.0001f)Hips.position+=Vector3.up*Mathf.Max(-.2f,lower);
                else break;
            }
        }
    }
}
