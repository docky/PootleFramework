using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Editor
{
    public static class RiggleQuadrupedAnimationConverter
    {
        [MenuItem("Tools/Riggle/Convert Selected Quadruped Animation Profile")]
        public static void ConvertSelected()
        {
            var p = Selection.activeObject as RiggleQuadrupedRetargetProfile;
            if (!p) throw new InvalidOperationException("Select a Riggle Quadruped Retarget Profile asset first.");
            Bake(p);
        }

        public static AnimationClip[] Bake(RiggleQuadrupedRetargetProfile profile)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Convert animation clips in Edit Mode.");
            if (!profile || profile.clips == null || profile.clips.Length == 0 || profile.clips.Any(c => !c))
                throw new ArgumentException("Assign source clips.");
            if (profile.sampleRate < 15 || profile.sampleRate > 120 || profile.strideScale <= 0)
                throw new ArgumentException("Invalid sample rate or stride scale.");
            string folder = profile.outputFolder.Replace('\\', '/').TrimEnd('/');
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) || folder.Split('/').Contains(".."))
                throw new ArgumentException("Output must be a folder beneath Assets.");
            using (var session = new Session(profile))
            {
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                var output = new List<AnimationClip>();
                foreach (var sourceClip in profile.clips)
                {
                    if (sourceClip.isHumanMotion || sourceClip.length <= 0)
                        throw new ArgumentException(sourceClip.name + ": requires a nonempty Generic transform clip.");
                    var clip = new AnimationClip { name = profile.targetPrefab.name + " - " + sourceClip.name, frameRate = profile.sampleRate };
                    var bones = session.TargetBones;
                    var curves = bones.Select(_ => Enumerable.Range(0, 7).Select(i => new AnimationCurve()).ToArray()).ToArray();
                    int frames = Mathf.CeilToInt(sourceClip.length * profile.sampleRate);
                    for (int f = 0; f <= frames; f++)
                    {
                        float time = Mathf.Min(f / (float)profile.sampleRate, sourceClip.length);
                        session.Sample(sourceClip, time);
                        for (int i = 0; i < bones.Length; i++)
                        {
                            var p = bones[i].localPosition; var q = bones[i].localRotation;
                            float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                            for (int c = 0; c < 7; c++) curves[i][c].AddKey(time, values[c]);
                        }
                    }
                    string[] properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                    for (int i = 0; i < bones.Length; i++) for (int c = 0; c < 7; c++)
                    {
                        for (int k = 0; k < curves[i][c].length; k++)
                        {
                            AnimationUtility.SetKeyLeftTangentMode(curves[i][c], k, AnimationUtility.TangentMode.Linear);
                            AnimationUtility.SetKeyRightTangentMode(curves[i][c], k, AnimationUtility.TangentMode.Linear);
                        }
                        clip.SetCurve(AnimationUtility.CalculateTransformPath(bones[i], session.Target.transform), typeof(Transform), properties[c], curves[i][c]);
                    }
                    clip.EnsureQuaternionContinuity();
                    var settings = AnimationUtility.GetAnimationClipSettings(clip);
                    settings.loopTime = AnimationUtility.GetAnimationClipSettings(sourceClip).loopTime;
                    AnimationUtility.SetAnimationClipSettings(clip, settings);
                    string safe = string.Concat(clip.name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == ' ' ? c : '_'));
                    // Unique paths preserve earlier conversions and any clips the user has edited.
                    string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + safe + ".anim");
                    AssetDatabase.CreateAsset(clip, path); output.Add(clip);
                }
                AssetDatabase.SaveAssets(); return output.ToArray();
            }
        }

        public sealed class Session : IDisposable
        {
            readonly Scene scene;
            readonly RiggleQuadrupedRetargetProfile profile;
            readonly GameObject source;
            readonly Transform motion, sourceHips, targetHips, carrier;
            readonly Vector3 motionRest, hipsRestSource, hipsRestTarget;
            readonly Quaternion motionRestRotation, alignment;
            readonly BonePose[] sourceRest, targetRest;
            readonly Body[] body;
            readonly Leg[] legs;
            public GameObject Target { get; private set; }
            public Transform[] TargetBones { get; private set; }
            public float MotionScale { get; private set; }
            public float MaxReachClamp { get; private set; }
            struct BonePose
            {
                public Transform bone; public Vector3 position, scale; public Quaternion rotation;
                public BonePose(Transform t) { bone=t; position=t.localPosition; rotation=t.localRotation; scale=t.localScale; }
                public void Restore() { bone.SetLocalPositionAndRotation(position,rotation); bone.localScale=scale; }
            }
            sealed class Body { public Transform source,target; public Quaternion sr,tr; public float weight; }
            sealed class Leg
            {
                public Transform sourceFoot, upper, middle, foot;
                public Vector3 sourceRest, targetRest, pole, sourceSole, targetSole;
                public Quaternion sourceRotation, targetRotation;
                public float a,b;
                public Transform[] chain, sourceChain;
                public Vector3[] sourceDirections,targetDirections;
                public RiggleChainIK solver;
                public Vector3 end; public Quaternion rotation;public float length;
            }
            public Session(RiggleQuadrupedRetargetProfile p)
            {
                profile=p;
                if (!p || !p.sourceModel || !p.targetPrefab || p.legs.Count != 4)
                    throw new ArgumentException("Assign source, target and exactly four leg mappings.");
                scene=EditorSceneManager.NewPreviewScene();
                try
                {
                    source=Object.Instantiate(p.sourceModel); Target=Object.Instantiate(p.targetPrefab);
                    SceneManager.MoveGameObjectToScene(source,scene); SceneManager.MoveGameObjectToScene(Target,scene);
                    foreach(var go in new[]{source,Target})
                    {
                        go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); go.transform.localScale=Vector3.one;
                        foreach(var b in go.GetComponentsInChildren<Behaviour>(true)) b.enabled=false;
                    }
                    var rig=Target.GetComponent<RiggleGeneric>();
                    if(!rig || !rig.generatedBones) throw new ArgumentException("Target needs a baked Riggle Generic skeleton.");
                    carrier=rig.generatedBones;
                    Transform Bone(string name)
                    {
                        var matches=carrier.GetComponentsInChildren<Transform>().Where(t=>t.name==name).ToArray();
                        if(matches.Length!=1)throw new ArgumentException("Missing or ambiguous target joint: "+name);
                        return matches[0];
                    }
                    motion=Source(p.sourceMotionRoot);sourceHips=Source(p.sourceHips);targetHips=Bone(p.targetHips);
                    if(p.sourceReferencePose)p.sourceReferencePose.SampleAnimation(source,p.referenceTime);
                    alignment=Quaternion.Euler(p.sourceToTargetEuler);
                    motionRest=motion.position;motionRestRotation=motion.rotation;
                    hipsRestSource=SourcePoint(sourceHips.position); hipsRestTarget=targetHips.position;
                    body=p.body.Select(b=>new Body{source=Source(b.sourcePath),target=Bone(b.targetJoint),weight=b.rotationWeight}).OrderBy(b=>Depth(b.target)).ToArray();
                    if(body.Select(b=>b.target).Distinct().Count()!=body.Length)throw new ArgumentException("Duplicate body target mapping.");
                    foreach(var b in body) { b.sr=b.source.rotation; b.tr=b.target.rotation; }
                    legs=p.legs.Select(l=>new Leg{sourceFoot=Source(l.sourceFoot),upper=Bone(l.targetUpper),middle=Bone(l.targetMiddle),foot=Bone(l.targetFoot),sourceSole=l.sourceSoleOffset,targetSole=l.targetSoleOffset,pole=l.bendDirection,
                        chain=new[]{l.targetUpper,l.targetMiddle}.Concat(l.additionalTargetJoints??Array.Empty<string>()).Concat(new[]{l.targetFoot}).Select(Bone).ToArray(),
                        sourceChain=(l.sourceShapeChain??Array.Empty<string>()).Select(Source).ToArray()}).ToArray();
                    var allLegBones=legs.SelectMany(l=>l.chain).ToArray();
                    if(allLegBones.Distinct().Count()!=allLegBones.Length)throw new ArgumentException("Legs cannot share or repeat target joints.");
                    float ratio=0;
                    for(int i=0;i<legs.Length;i++)
                    {
                        var l=legs[i];var upper=Source(p.legs[i].sourceUpper);
                        if(!l.sourceFoot.IsChildOf(upper))throw new ArgumentException("Source foot must descend from its upper joint.");
                        for(int c=1;c<l.chain.Length;c++)if(l.chain[c].parent!=l.chain[c-1])throw new ArgumentException("Target leg mapping must include every joint in parent order.");
                        l.solver=new RiggleChainIK(l.chain,true);
                        for(int c=1;c<l.chain.Length;c++)l.length+=Vector3.Distance(l.chain[c-1].position,l.chain[c].position);
                        if(l.sourceChain.Length>0)
                        {
                            if(l.sourceChain.Length!=l.chain.Length)throw new ArgumentException("Source shape chain must match target chain joint count, or be empty for trajectory-only conversion.");
                            l.sourceDirections=new Vector3[l.chain.Length-1];l.targetDirections=new Vector3[l.chain.Length-1];
                            for(int c=0;c<l.chain.Length-1;c++)
                            {
                                if(!l.sourceChain[c+1].IsChildOf(l.sourceChain[c]))throw new ArgumentException("Source shape joints must follow hierarchy order.");
                                l.sourceDirections[c]=SourcePoint(l.sourceChain[c+1].position)-SourcePoint(l.sourceChain[c].position);
                                l.targetDirections[c]=l.chain[c+1].position-l.chain[c].position;
                            }
                        }
                        l.a=Vector3.Distance(l.upper.position,l.middle.position);l.b=Vector3.Distance(l.middle.position,l.foot.position);
                        float sourceReach=Vector3.Distance(upper.position,l.sourceFoot.position);
                        if(l.a<.001f||l.b<.001f||sourceReach<.001f)throw new ArgumentException("Leg has zero length.");
                        ratio+=Vector3.Distance(l.upper.position,l.foot.position)/sourceReach;
                        l.sourceRest=SourcePoint(l.sourceFoot.TransformPoint(l.sourceSole));
                        l.targetRest=l.foot.TransformPoint(l.targetSole);
                        l.sourceRotation=l.sourceFoot.rotation;l.targetRotation=l.foot.rotation;
                        if(l.pole.sqrMagnitude<.0001f)l.pole=Vector3.ProjectOnPlane(l.middle.position-l.upper.position,l.foot.position-l.upper.position);
                        if(l.pole.sqrMagnitude<.0001f)throw new ArgumentException("Straight target leg needs an explicit bend direction.");
                        l.pole.Normalize();
                    }
                    MotionScale=ratio/4;
                    TargetBones=carrier.GetComponentsInChildren<Transform>();
                    sourceRest=source.GetComponentsInChildren<Transform>().Select(t=>new BonePose(t)).ToArray();
                    targetRest=TargetBones.Select(t=>new BonePose(t)).ToArray();
                }
                catch { Dispose(); throw; }
            }
            Transform Source(string path)
            {
                var t=string.IsNullOrEmpty(path)?source.transform:source.transform.Find(path);
                if(!t)throw new ArgumentException("Missing source path: "+path); return t;
            }
            Vector3 SourcePoint(Vector3 world) => motionRestRotation * Quaternion.Inverse(motion.rotation) * (world-motion.position);
            static int Depth(Transform t) { int n=0;while(t.parent){n++;t=t.parent;}return n; }
            Quaternion Rotation(Transform sourceBone,Quaternion reference,Quaternion targetReference)
            {
                var delta=motionRestRotation*Quaternion.Inverse(motion.rotation)*sourceBone.rotation*Quaternion.Inverse(reference);
                return alignment*delta*Quaternion.Inverse(alignment)*targetReference;
            }
            public void Sample(AnimationClip clip,float time)
            {
                foreach(var p in sourceRest)p.Restore();foreach(var p in targetRest)p.Restore();
                clip.SampleAnimation(source,time);
                var rootDelta=alignment*(motion.position-motionRest)*MotionScale;
                var rootRotation=alignment*(motion.rotation*Quaternion.Inverse(motionRestRotation))*Quaternion.Inverse(alignment);
                carrier.localPosition=profile.includeRootMotion?rootDelta:new Vector3(0,rootDelta.y,0);
                carrier.localRotation=profile.includeRootMotion?rootRotation:Quaternion.identity;
                var hipDelta=alignment*(SourcePoint(sourceHips.position)-hipsRestSource)*MotionScale;
                targetHips.position=carrier.TransformPoint(hipsRestTarget+hipDelta);
                foreach(var b in body)b.target.rotation=carrier.rotation*Quaternion.Slerp(b.tr,Rotation(b.source,b.sr,b.tr),b.weight);
                float lower=0;
                foreach(var l in legs)
                {
                    var delta=alignment*(SourcePoint(l.sourceFoot.TransformPoint(l.sourceSole))-l.sourceRest)*MotionScale;
                    delta.x*=profile.strideScale;delta.z*=profile.strideScale;
                    l.rotation=carrier.rotation*Rotation(l.sourceFoot,l.sourceRotation,l.targetRotation);
                    var sole=l.targetRest+delta;
                    if(profile.clampFeetToReferenceFloor)sole.y=Mathf.Max(l.targetRest.y,sole.y);
                    l.end=carrier.TransformPoint(sole)-l.rotation*l.targetSole;
                    var difference=l.upper.position-l.end;float horizontal=difference.x*difference.x+difference.z*difference.z;
                    float reach=l.length*.999f;
                    if(horizontal<reach*reach)lower=Mathf.Min(lower,l.end.y+Mathf.Sqrt(reach*reach-horizontal)-l.upper.position.y);
                }
                targetHips.position+=Vector3.up*Mathf.Max(-profile.maxBodyLowering,lower);
                foreach(var l in legs)
                {
                    var end=l.end;var rotation=l.rotation;
                    if(l.chain.Length>3)
                    {
                        if(l.sourceChain.Length>0)for(int c=0;c<l.chain.Length-1;c++)
                        {
                            var sourceDirection=SourcePoint(l.sourceChain[c+1].position)-SourcePoint(l.sourceChain[c].position);
                            var change=Quaternion.FromToRotation(l.sourceDirections[c],sourceDirection);
                            var desired=carrier.rotation*alignment*change*Quaternion.Inverse(alignment)*l.targetDirections[c];
                            l.chain[c].rotation=Quaternion.FromToRotation(l.chain[c+1].position-l.chain[c].position,desired)*l.chain[c].rotation;
                        }
                        // Foot orientation limits change the pivot-to-sole offset. Re-solve at
                        // the same contact point so limiting the ankle cannot bury the hoof.
                        var contact=end+rotation*l.targetSole;
                        float error=0;
                        for(int pass=0;pass<32;pass++)
                        {
                            error=l.solver.Solve(contact-rotation*l.targetSole,rotation,1024);
                            var limited=l.foot.rotation;
                            if(Quaternion.Angle(rotation,limited)<.01f)break;
                            rotation=limited;
                        }
                        MaxReachClamp=Mathf.Max(MaxReachClamp,error);continue;
                    }
                    var from=l.upper.position;var direction=end-from;
                    float reach=Mathf.Clamp(direction.magnitude,Mathf.Abs(l.a-l.b)+.0001f,(l.a+l.b)*.999f);
                    MaxReachClamp=Mathf.Max(MaxReachClamp,Mathf.Abs(direction.magnitude-reach));
                    var axis=direction.sqrMagnitude>.000001f?direction.normalized:Vector3.down;
                    var pole=Vector3.ProjectOnPlane(carrier.rotation*l.pole,axis).normalized;
                    if(pole.sqrMagnitude<.001f)pole=Vector3.Cross(axis,Vector3.right).normalized;
                    float along=(l.a*l.a-l.b*l.b+reach*reach)/(2*reach);
                    var knee=from+axis*along+pole*Mathf.Sqrt(Mathf.Max(0,l.a*l.a-along*along));
                    l.upper.rotation=Quaternion.FromToRotation(l.middle.position-from,knee-from)*l.upper.rotation;
                    var reachable=from+axis*reach;
                    l.middle.rotation=Quaternion.FromToRotation(l.foot.position-l.middle.position,reachable-l.middle.position)*l.middle.rotation;
                    l.foot.rotation=rotation;
                }
            }
            public void Dispose() { if(scene.IsValid())EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
