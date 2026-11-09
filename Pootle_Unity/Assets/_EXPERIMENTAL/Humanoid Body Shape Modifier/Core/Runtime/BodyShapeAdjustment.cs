using UnityEngine;
namespace Docky.BodyShapeMotion
{
    [AddComponentMenu("Animation/Humanoid Body Shape Modifier")]
    [DefaultExecutionOrder(-9000),DisallowMultipleComponent,RequireComponent(typeof(Animator))]
    public sealed class BodyShapeAdjustment : MonoBehaviour
    {
        public BodyShapeProfile profile;
        [Range(0,1)] public float strength=1;
        [Range(0,1)] public float armWeight=1, footWeight=1;
        public bool floorClearance=true;
        [Tooltip("World-space Y of the flat comparison floor; not a terrain solver.")] public float floorHeight;
        [Tooltip("External preview/Playable driver calls Evaluate itself.")] public bool manualEvaluation;
        public bool showVolumes;
        public float HandPenetration {get;private set;}
        public float FootPenetration {get;private set;}
        public float MaxDisplacement {get;private set;}
        Transform[] bones;
        Quaternion[] original;
        Transform[] volumeBones;
        HumanBodyBones[] volumeMapping;
        BodyShapeProfile boundProfile;
        Vector3[] offsets=new Vector3[4], poles=new Vector3[2];
        bool captured;
        float footSide=1;
        static readonly HumanBodyBones[] Mapping={HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot};
        public bool Bind()
        {
            RestoreAnimationPose();
            var a=GetComponent<Animator>();if(!a.avatar || !a.isHuman || !profile)return false;
            bones=new Transform[Mapping.Length];original=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++){bones[i]=a.GetBoneTransform(Mapping[i]);if(!bones[i])return false;}
            boundProfile=profile;volumeBones=new Transform[profile.torso.Length];volumeMapping=new HumanBodyBones[profile.torso.Length];
            for(int i=0;i<volumeBones.Length;i++){volumeMapping[i]=profile.torso[i].bone;volumeBones[i]=VolumeBone(a,volumeMapping[i]);}
            return true;
        }
        void Update(){if(!manualEvaluation)RestoreAnimationPose();}
        void LateUpdate(){if(!manualEvaluation)Evaluate(Time.deltaTime);}
        void OnDisable(){RestoreAnimationPose();ResetBlend();}
        public void RestoreAnimationPose()
        {
            if(!captured || bones==null)return;
            for(int i=0;i<bones.Length;i++)if(bones[i])bones[i].localRotation=original[i];
            captured=false;
        }
        public void ResetBlend(){for(int i=0;i<4;i++)offsets[i]=Vector3.zero;for(int i=0;i<2;i++)poles[i]=Vector3.zero;footSide=1;}
        Vector3 Project(Vector3 world,float radius,float side)
        {
            var result=world;
            for(int pass=0;pass<2;pass++)for(int i=0;i<volumeBones.Length;i++)
            {
                var anchor=volumeBones[i];if(!anchor)continue;var volume=profile.torso[i];
                var p=anchor.InverseTransformPoint(result);
                result=anchor.TransformPoint(BodyShapeMath.OutsideEllipsoid(p,volume.center,volume.radii,radius+profile.clearance,anchor.InverseTransformDirection(transform.right*side)));
            }
            return result;
        }
        float Scale => Mathf.Max(.0001f,Mathf.Abs(transform.lossyScale.x));
        Vector3 Bounded(Vector3 offset)=>Vector3.ClampMagnitude(offset,profile.maximumOffset*Scale);
        Vector3 HandCenter(int side)=>bones[side*3+2].TransformPoint(side==0?profile.leftHandCenter:profile.rightHandCenter);
        public void Evaluate(float deltaTime,bool instant=false)
        {
            if(profile==null)return;
            bool rebind=bones==null || boundProfile!=profile || volumeBones==null || volumeBones.Length!=profile.torso.Length;
            if(!rebind)for(int i=0;i<volumeMapping.Length;i++)if(volumeMapping[i]!=profile.torso[i].bone)rebind=true;
            if(rebind && !Bind())return;
            // Repeated editor evaluations without a fresh animation sample must not accumulate corrections.
            if(captured)RestoreAnimationPose();
            MaxDisplacement=0;
            for(int i=0;i<bones.Length;i++)original[i]=bones[i].localRotation;
            captured=true;
            float alpha=instant || profile.smoothingSeconds<=0?1:1-Mathf.Exp(-Mathf.Max(0,deltaTime)/profile.smoothingSeconds);
            float armAmount=Mathf.Clamp01(strength*armWeight), footAmount=Mathf.Clamp01(strength*footWeight);
            for(int side=0;side<2;side++)
            {
                int index=side*3;var elbow=bones[index+1].position;var hand=bones[index+2].position;float sign=side==0?-1:1;
                var center=HandCenter(side);var target=hand+Project(center,profile.handRadius,sign)-center;
                var elbowTarget=Project(elbow,profile.forearmRadius,sign);
                // Forearm samples catch a segment crossing the belly with a clear wrist.
                for(int step=1;step<=3;step++)
                {
                    float t=step*.25f;var point=Vector3.Lerp(elbowTarget,target,t);
                    var correction=Project(point,profile.forearmRadius,sign)-point;
                    target+=correction;elbowTarget+=correction*(1-t);
                }
                var desired=Bounded(target-hand);var poleDelta=Bounded(elbowTarget-elbow);
                offsets[side]=Vector3.Lerp(offsets[side],desired,alpha);poles[side]=Vector3.Lerp(poles[side],poleDelta,alpha);
                if(armAmount>0 && (offsets[side].sqrMagnitude+poles[side].sqrMagnitude)>.00000001f)
                {
                    BodyShapeMath.SolveTwoBone(bones[index],bones[index+1],bones[index+2],hand+offsets[side]*armAmount,elbow+poles[side]*armAmount);
                    MaxDisplacement=Mathf.Max(MaxDisplacement,Vector3.Distance(hand,bones[index+2].position));
                }
            }
            FootBounds(0,out var lc,out var le);FootBounds(1,out var rc,out var re);
            float overlapX=le.x+re.x+profile.clearance-Mathf.Abs(rc.x-lc.x);
            float overlapY=le.y+re.y-Mathf.Abs(rc.y-lc.y), overlapZ=le.z+re.z+profile.clearance-Mathf.Abs(rc.z-lc.z);
            if(Mathf.Abs(rc.x-lc.x)>.02f)footSide=Mathf.Sign(rc.x-lc.x);
            float split=overlapY>0 && overlapZ>0?Mathf.Max(0,overlapX)*.5f:0;
            for(int side=0;side<2;side++)
            {
                int index=6+side*3;var foot=bones[index+2];var start=foot.position;
                var desired=transform.TransformVector(Vector3.right*split*(side==0?-footSide:footSide));
                if(floorClearance)
                {
                    float sole=FootSole(side);
                    desired+=Vector3.up*Mathf.Max(0,floorHeight-sole);
                }
                offsets[side+2]=Vector3.Lerp(offsets[side+2],Bounded(desired),alpha);
                if(footAmount>0 && offsets[side+2].sqrMagnitude>.00000001f)
                {
                    var hip=bones[index].position;var knee=bones[index+1].position;
                    var target=start+offsets[side+2]*footAmount;
                    float length=Vector3.Distance(hip,knee)+Vector3.Distance(knee,start);
                    float lateral=Mathf.Abs(Vector3.Dot(offsets[side+2],transform.right));
                    float turnout=Mathf.SmoothStep(0,1,lateral/Mathf.Max(.00001f,length*.15f))*footAmount;
                    var pole=BodyShapeMath.LegPole(hip,knee,start,target,transform.forward,
                        transform.right*(side==0?-1:1),profile.kneeFollow*footAmount,profile.kneeOutwardDegrees,turnout);
                    BodyShapeMath.SolveTwoBone(bones[index],bones[index+1],foot,target,pole);
                    MaxDisplacement=Mathf.Max(MaxDisplacement,Vector3.Distance(start,foot.position));
                }
            }
            Measure();
        }
        void FootBounds(int side,out Vector3 center,out Vector3 extents)
        {
            var foot=bones[8+side*3];var box=side==0?profile.leftFoot:profile.rightFoot;
            center=transform.InverseTransformPoint(foot.TransformPoint(box.center));extents=Vector3.zero;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var v=transform.InverseTransformPoint(foot.TransformPoint(box.center+Vector3.Scale(box.halfExtents,new Vector3(x,y,z))))-center;
                extents=Vector3.Max(extents,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));
            }
        }
        float FootSole(int side)
        {
            var foot=bones[8+side*3];var box=side==0?profile.leftFoot:profile.rightFoot;float min=float.PositiveInfinity;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                min=Mathf.Min(min,foot.TransformPoint(box.center+Vector3.Scale(box.halfExtents,new Vector3(x,y,z))).y);
            return min;
        }
        public void Measure()
        {
            HandPenetration=0;
            for(int side=0;side<2;side++){var p=HandCenter(side);HandPenetration+=Vector3.Distance(p,Project(p,profile.handRadius,side==0?-1:1));}
            FootBounds(0,out var lc,out var le);FootBounds(1,out var rc,out var re);
            float x=le.x+re.x-Mathf.Abs(rc.x-lc.x),y=le.y+re.y-Mathf.Abs(rc.y-lc.y),z=le.z+re.z-Mathf.Abs(rc.z-lc.z);
            FootPenetration=Mathf.Max(0,Mathf.Min(x,Mathf.Min(y,z)))*Scale;
        }
        static Transform VolumeBone(Animator animator,HumanBodyBones bone) => (int)bone>=0 && bone<HumanBodyBones.LastBone ? animator.GetBoneTransform(bone) : null;
        void OnDrawGizmosSelected()
        {
            if(!showVolumes || !profile)return;
            var animator=GetComponent<Animator>();if(!animator || !animator.avatar || !animator.isHuman)return;
            var old=Gizmos.matrix;var oldColor=Gizmos.color;Gizmos.color=new Color(1,.55f,.1f,.7f);
            foreach(var v in profile.torso){var bone=VolumeBone(animator,v.bone);if(!bone)continue;Gizmos.matrix=bone.localToWorldMatrix*Matrix4x4.TRS(v.center,Quaternion.identity,v.radii);Gizmos.DrawWireSphere(Vector3.zero,1);}
            for(int side=0;side<2;side++){var foot=animator.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);if(!foot)continue;var f=side==0?profile.leftFoot:profile.rightFoot;Gizmos.matrix=foot.localToWorldMatrix;Gizmos.DrawWireCube(f.center,f.halfExtents*2);}
            Gizmos.matrix=old;Gizmos.color=oldColor;
        }
    }
}
