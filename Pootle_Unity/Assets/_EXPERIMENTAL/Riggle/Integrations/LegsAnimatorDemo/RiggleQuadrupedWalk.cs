using System;
using System.Collections.Generic;
using System.Linq;
using FIMSpace.FProceduralAnimation;
using UnityEngine;

namespace Docky.RiggleTools.Demo
{
    [DefaultExecutionOrder(-20)]
    public sealed class RiggleQuadrupedWalk : MonoBehaviour
    {
        public float speed = .3f, travel = 12.3f, warmup = 2;
        public float bodyHeightOffset = -.025f;
        public Camera followCamera;
        public string animal;
        public string recordingSubtitle="Procedural four-beat walk - Legs Animator ground alignment + IK";
        public Animator clipAnimator;
        public float clipPlaybackSpeed=1;
        public bool run = true;
        public float elapsed;
        public int[] steps = new int[4];
        public List<float> stanceErrors = new List<float>(), stanceSlides = new List<float>();
        public float maxSwingClearance, maxHeightDifference, maxStretchRatio, maxTerminalBend;
        public int invalidSamples, groundMisses, sampledFrames;
        public int minimumSupports = 4, minimumPhysicalSupports = 4;
        public float distance;
        public string worstStanceDetail,leastSupportDetail;
        float worstStanceError;
        LegsAnimator legs;
        bool[] previousSwing = new bool[4], previousStance = new bool[4];
        Vector3[] previousHeel = new Vector3[4];
        float[] length = new float[4];
        Vector3[] restFeet = new Vector3[4];
        float observeClock;
        public LegsAnimator Legs => legs;
        void Start()
        {
            legs = GetComponent<LegsAnimator>();
            for (int i=0;i<4;i++)
            {
                var leg=legs.Legs[i];
                for(var bone=leg.BoneEnd;bone&&bone!=leg.BoneStart;bone=bone.parent)
                {if(!bone.parent)throw new InvalidOperationException("Leg foot must descend from its upper joint.");length[i]+=Vector3.Distance(bone.position,bone.parent.position);}
                restFeet[i]=transform.InverseTransformPoint(leg.BoneEnd.position);
            }
        }
        void Update()
        {
            if (!legs || !run) return;
            elapsed += Time.deltaTime;
            distance = Mathf.Min(travel,Mathf.Max(0,elapsed-warmup)*speed);
            float y=restFeet.Min(p=>RiggleUnevenCourse.Height(p.x,distance+p.z))+bodyHeightOffset;
            transform.position = new Vector3(0,y,distance);
            bool moving = elapsed > warmup && distance < travel;
            if(clipAnimator)clipAnimator.speed=moving?clipPlaybackSpeed:0;
            legs.User_SetIsMoving(moving); legs.User_SetIsGrounded(true);
            legs.User_SetDesiredMovementDirection(moving ? Vector3.forward : Vector3.zero);
        }
        // Called by the recorder/observer after Legs Animator LateUpdate, not this early-order component's LateUpdate.
        public void Observe()
        {
            if (!legs || !legs.LegsInitialized) return;
            if (followCamera)
            {
                float orbit = Mathf.Sin(elapsed*.17f)*.25f;
                followCamera.transform.position = transform.position + new Vector3(3.5f+orbit,2.1f,3.7f);
                followCamera.transform.LookAt(transform.position + Vector3.up*.95f);
            }
            if (elapsed < warmup + 1) return;
            observeClock+=Time.deltaTime; if(observeClock < 1f/30-.0001f)return; observeClock=0;
            sampledFrames++; int support=0,physical=0; float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
            for(int i=0;i<4;i++)
            {
                var leg=legs.Legs[i];
                // Moving mode follows the clip's detach/attach cycle; it does not run the procedural attaching animation.
                bool swing=clipAnimator?!leg.G_Attached:leg.G_DuringAttaching;
                if(swing&&!previousSwing[i])steps[i]++;
                var heel=leg.BoneEnd.TransformPoint(leg.AnkleToHeel);
                var end=leg.BoneEnd;
                if(end.parent&&end.parent.parent&&end.parent.parent!=leg.BoneStart)
                    maxTerminalBend=Mathf.Max(maxTerminalBend,Vector3.Angle(end.parent.position-end.parent.parent.position,end.position-end.parent.position));
                if(!float.IsFinite(heel.x)||!float.IsFinite(heel.y)||!float.IsFinite(heel.z)){invalidSamples++;continue;}
                minY=Mathf.Min(minY,heel.y);maxY=Mathf.Max(maxY,heel.y);
                maxStretchRatio=Mathf.Max(maxStretchRatio,Vector3.Distance(leg.BoneStart.position,leg.BoneEnd.position)/length[i]);
                if(Physics.Raycast(heel+Vector3.up*1.5f,Vector3.down,out var hit,3,1,QueryTriggerInteraction.Ignore))
                {
                    float error=heel.y-hit.point.y;
                    if(Mathf.Abs(error)<=.02f)physical++;
                    bool stance=leg.G_Attached&&!swing;
                    if(stance)
                    {
                        support++; stanceErrors.Add(Mathf.Abs(error));
                        if(Mathf.Abs(error)>worstStanceError)
                        {
                            worstStanceError=Mathf.Abs(error);
                            var wanted=leg.GetFinalIKPos()+leg.GetFinalIKRot()*Vector3.Scale(leg.AnkleToHeel,leg.BoneEnd.lossyScale);
                            worstStanceDetail=$"t={elapsed} leg={i} error={error} heel={heel:F4} desired={wanted:F4} difference={(heel-wanted):F4} attaching={leg.G_DuringAttaching} rootDistance={Vector3.Distance(leg.BoneStart.position,leg.GetFinalIKPos())} length={length[i]}";
                        }
                        if(previousStance[i])stanceSlides.Add(Vector3.ProjectOnPlane(heel-previousHeel[i],hit.normal).magnitude);
                    }
                    else maxSwingClearance=Mathf.Max(maxSwingClearance,error);
                    previousStance[i]=stance;
                }
                else {groundMisses++;previousStance[i]=false;}
                previousSwing[i]=swing;previousHeel[i]=heel;
            }
            if(support<minimumSupports)leastSupportDetail=$"t={elapsed} support={support}";
            minimumSupports=Mathf.Min(minimumSupports,support);
            minimumPhysicalSupports=Mathf.Min(minimumPhysicalSupports,physical);
            maxHeightDifference=Mathf.Max(maxHeightDifference,maxY-minY);
        }
        static float Percentile(List<float> values,float p)
        {if(values.Count==0)return -1;var a=values.OrderBy(v=>v).ToArray();return a[Mathf.Min(a.Length-1,(int)(a.Length*p))];}
        public string Report()
        {
            return JsonUtility.ToJson(new ReportData{animal=animal,seconds=elapsed,distance=distance,steps=steps,
                sampledFrames=sampledFrames,stanceSamples=stanceErrors.Count,stanceErrorP95=Percentile(stanceErrors,.95f),
                stanceErrorMax=stanceErrors.Count==0?-1:stanceErrors.Max(),stanceSlideP95=Percentile(stanceSlides,.95f),
                maxSwingClearance=maxSwingClearance,maxFootHeightDifference=maxHeightDifference,maxStretchRatio=maxStretchRatio,
                invalidSamples=invalidSamples,groundMisses=groundMisses,minimumSupports=minimumSupports,minimumPhysicalSupports=minimumPhysicalSupports,maxTerminalBend=maxTerminalBend},true);
        }
        [Serializable] class ReportData
        {
            public string animal;public float seconds,distance,stanceErrorP95,stanceErrorMax,stanceSlideP95,maxSwingClearance,maxFootHeightDifference,maxStretchRatio,maxTerminalBend;
            public int[] steps;public int sampledFrames,stanceSamples,invalidSamples,groundMisses,minimumSupports,minimumPhysicalSupports;
        }
    }
}
