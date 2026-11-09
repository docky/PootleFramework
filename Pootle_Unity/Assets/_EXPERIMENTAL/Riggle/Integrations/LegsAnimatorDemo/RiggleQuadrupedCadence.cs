using FIMSpace.FProceduralAnimation;
using UnityEngine;

namespace Docky.RiggleTools.Demo
{
    // Optional Legs Animator integration: demo-only scheduling, vendor IK and foot arcs remain authoritative.
    public sealed class RiggleQuadrupedCadence : LegsAnimatorControlModuleBase
    {
        readonly int[] order = { 2, 0, 3, 1 };
        int next, active = -1;
        float cooldown;
        bool sawSwing;
        float[] lengths;
        public override void OnInit(LegsAnimator.LegsAnimatorCustomModuleHelper helper)
        {
            lengths = new float[LA.Legs.Count];
            for (int i=0;i<lengths.Length;i++) lengths[i]=Vector3.Distance(LA.Legs[i].BoneStart.position,LA.Legs[i].BoneMid.position)+Vector3.Distance(LA.Legs[i].BoneMid.position,LA.Legs[i].BoneEnd.position);
        }
        // Hooves need a stable sole point on the actual collider, including at ledge boundaries.
        // Keep the vendor's horizontal glue and swing trajectory, then make its final IK target reachable.
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
                    target.y=leg.G_Attached&&!leg.G_DuringAttaching?floor:Mathf.Max(target.y,floor+.006f);
                    leg.OverrideFinalIKPos(target);
                }
                var delta=leg.BoneStart.position-target;
                float horizontal=delta.x*delta.x+delta.z*delta.z;
                float reach=lengths[i]*.975f;
                float vertical=Mathf.Sqrt(Mathf.Max(.001f,reach*reach-horizontal));
                lower=Mathf.Min(lower,target.y+vertical-leg.BoneStart.position.y);
            }
            LA.Hips.position+=Vector3.up*Mathf.Max(-.25f,lower);
        }
        public override void OnPreLateUpdate(LegsAnimator.LegsAnimatorCustomModuleHelper helper)
        {
            if (LA.Legs.Count != 4) return;
            cooldown -= LA.DeltaTime;
            if (active >= 0)
            {
                sawSwing |= LA.Legs[active].G_DuringAttaching;
                if ((sawSwing && !LA.Legs[active].G_DuringAttaching) || cooldown < -.6f)
                { active = -1; next = (next + 1) % 4; cooldown = .035f; }
            }
            if (active < 0 && cooldown <= 0 && LA.IsMoving)
            {
                active = order[next]; sawSwing = false; cooldown = .3f;
                LA.Legs[active].G_CustomForceDetach = true;
            }
            for (int i = 0; i < 4; i++) LA.Legs[i].G_CustomForceNOTDetach = i != active;
        }
    }
}
