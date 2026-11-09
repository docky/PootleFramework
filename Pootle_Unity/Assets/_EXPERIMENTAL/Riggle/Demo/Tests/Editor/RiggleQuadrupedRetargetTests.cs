using System;
using System.Linq;
using Docky.RiggleTools.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    [TestFixture("Pink Pony", "")]
    [TestFixture("Blue Pony", "/Blue Pony")]
    public sealed class RiggleQuadrupedRetargetTests
    {
        readonly string pony,folder;
        public RiggleQuadrupedRetargetTests(string pony,string subfolder){this.pony=pony;folder="Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Horse Retarget"+subfolder;}
        RiggleQuadrupedRetargetProfile Profile()
        {
            var asset=AssetDatabase.LoadAssetAtPath<RiggleQuadrupedRetargetProfile>(folder+"/LP Horse to "+pony+".asset");
            Assert.That(asset,Is.Not.Null,"Build the horse retarget example first.");return Object.Instantiate(asset);
        }
        [Test] public void ReferencePoseAndSampleOrderAreStable()
        {
            var p=Profile();var empty=new AnimationClip();
            try
            {
                using(var s=new RiggleQuadrupedAnimationConverter.Session(p))
                {
                    var rest=s.TargetBones.Select(t=>t.position).ToArray();s.Sample(empty,0);
                    Assert.That(s.TargetBones.Select((t,i)=>Vector3.Distance(t.position,rest[i])).Max(),Is.LessThan(.006f));
                    s.Sample(p.clips[1],.4f);var pose=s.TargetBones.Select(t=>t.localRotation).ToArray();
                    s.Sample(p.clips[4],.7f);s.Sample(p.clips[1],.4f);
                    Assert.That(s.TargetBones.Select((t,i)=>Quaternion.Angle(t.localRotation,pose[i])).Max(),Is.LessThan(.05f));
                }
            }
            finally{Object.DestroyImmediate(p);Object.DestroyImmediate(empty);}
        }
        [Test] public void AllFourFeetAnimateWithoutChangingBoneLengths()
        {
            var p=Profile();
            try
            {
                using(var s=new RiggleQuadrupedAnimationConverter.Session(p))
                {
                    var lengths=s.TargetBones.Select(t=>t.localPosition.magnitude).ToArray();
                    var feet=p.legs.Select(l=>s.TargetBones.Single(t=>t.name==l.targetFoot)).ToArray();
                    var bounds=feet.Select(t=>new Bounds(t.position,Vector3.zero)).ToArray();
                    foreach(var clip in p.clips.Skip(1))for(int f=0;f<30;f++)
                    {
                        s.Sample(clip,clip.length*f/30);
                        for(int i=0;i<feet.Length;i++)bounds[i].Encapsulate(feet[i].position);
                        for(int i=0;i<s.TargetBones.Length;i++)
                        {
                            var t=s.TargetBones[i];Assert.That(float.IsFinite(t.position.x)&&float.IsFinite(t.rotation.w));
                            if(t.name!="Hips"&&t.name!="RiggleGeneric_Bones")Assert.That(t.localPosition.magnitude,Is.EqualTo(lengths[i]).Within(.0001f));
                        }
                    }
                    foreach(var b in bounds)Assert.That(b.size.z,Is.GreaterThan(.12f));
                }
            }
            finally{Object.DestroyImmediate(p);}
        }
        [Test] public void RootTravelCanBeIncludedOrRemoved()
        {
            var p=Profile();
            try
            {
                float travel=0;
                foreach(bool enabled in new[]{false,true})
                {
                    p.includeRootMotion=enabled;
                    using(var s=new RiggleQuadrupedAnimationConverter.Session(p))
                    {
                        var root=s.TargetBones[0];s.Sample(p.clips[1],0);var start=root.position;
                        s.Sample(p.clips[1],p.clips[1].length);travel=(root.position-start).z;
                        if(enabled)Assert.That(travel,Is.GreaterThan(.1f));else Assert.That(Mathf.Abs(travel),Is.LessThan(.0001f));
                    }
                }
            }
            finally{Object.DestroyImmediate(p);}
        }
        [Test] public void InvalidMappingsFailWithoutTouchingSourceAssets()
        {
            var p=Profile();var before=EditorJsonUtility.ToJson(p.sourceModel);
            try
            {
                p.legs[0].targetFoot="MissingFoot";
                Assert.Throws<ArgumentException>(()=>{using(var s=new RiggleQuadrupedAnimationConverter.Session(p)){} });
                Assert.That(EditorJsonUtility.ToJson(p.sourceModel),Is.EqualTo(before));
            }
            finally{Object.DestroyImmediate(p);}
        }
        [Test] public void ImportedMotionRootAxesKeepWalkingFeetNearTheFloor()
        {
            var p=Profile();
            try
            {
                using(var s=new RiggleQuadrupedAnimationConverter.Session(p))
                {
                    float minimum=100;
                    foreach(var clip in p.clips.Skip(1).Take(3))for(int f=0;f<60;f++)
                    {
                        s.Sample(clip,clip.length*f/60);
                        foreach(var leg in p.legs)minimum=Mathf.Min(minimum,s.TargetBones.Single(t=>t.name==leg.targetFoot).TransformPoint(leg.targetSoleOffset).y);
                    }
                    Assert.That(minimum,Is.GreaterThan(-.005f));
                    Assert.That(s.MaxReachClamp,Is.LessThan(.02f),"Forward travel must not become vertical travel through an imported root's rotated axes.");
                }
            }
            finally{Object.DestroyImmediate(p);}
        }
        [Test] public void TerminalSegmentsNeverFoldBackAndHoovesStayConnected()
        {
            var p=Profile();
            try
            {
                using(var s=new RiggleQuadrupedAnimationConverter.Session(p))
                {
                    var feet=p.legs.Select(l=>s.TargetBones.Single(t=>t.name==l.targetFoot)).ToArray();
                    var rest=feet.Select(t=>Quaternion.Inverse(t.rotation)*(t.position-t.parent.position).normalized).ToArray();
                    foreach(var clip in p.clips)for(int f=0;f<=Mathf.CeilToInt(clip.length*120);f++)
                    {
                        s.Sample(clip,Mathf.Min(f/120f,clip.length));
                        for(int i=0;i<feet.Length;i++)
                        {
                            var foot=feet[i];var last=foot.parent;
                            string context=clip.name+" / "+foot.name+" / "+f;
                            Assert.That(Vector3.Angle(last.position-last.parent.position,foot.position-last.position),Is.LessThanOrEqualTo(55.01f),context);
                            Assert.That(Vector3.Angle(foot.rotation*rest[i],foot.position-last.position),Is.LessThanOrEqualTo(35.01f),context);
                            Assert.That(foot.TransformPoint(p.legs[i].targetSoleOffset).y,Is.GreaterThan(-.002f),context);
                        }
                    }
                    Assert.That(s.MaxReachClamp,Is.LessThan(.003f),"Limits must preserve reachable contact targets across every gait.");
                }
            }
            finally{Object.DestroyImmediate(p);}
        }
        [Test] public void BakedClipsReproduceTheConverterPose()
        {
            var p=Profile();var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/"+pony+" Animated.prefab");
            var target=Object.Instantiate(prefab);
            try
            {
                foreach(var behaviour in target.GetComponentsInChildren<Behaviour>())behaviour.enabled=false;
                var clips=target.GetComponent<Animator>().runtimeAnimatorController.animationClips;
                using(var s=new RiggleQuadrupedAnimationConverter.Session(p))
                foreach(var original in p.clips)
                {
                    var clip=clips.Single(c=>c.name.Contains(original.name));float time=20f/p.sampleRate;
                    s.Sample(original,time);clip.SampleAnimation(target,time);
                    foreach(var bone in s.TargetBones)
                    {
                        string path=AnimationUtility.CalculateTransformPath(bone,s.Target.transform);
                        var baked=target.transform.Find(path);
                        Assert.That(Vector3.Distance(baked.localPosition,bone.localPosition),Is.LessThan(.0001f));
                        Assert.That(Quaternion.Angle(baked.localRotation,bone.localRotation),Is.LessThan(.1f));
                    }
                }
            }
            finally{Object.DestroyImmediate(target);Object.DestroyImmediate(p);}
        }
    }
}
