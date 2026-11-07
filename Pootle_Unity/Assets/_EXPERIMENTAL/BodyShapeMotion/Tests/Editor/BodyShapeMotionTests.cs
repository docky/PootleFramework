using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Docky.BodyShapeMotion.Editor;
using Object=UnityEngine.Object;
namespace Docky.BodyShapeMotion.Tests
{
    public sealed class BodyShapeMotionTests
    {
        [TestCase(-1)] [TestCase(1)]
        public void WidenedLegCarriesItsBendPlaneAndTurnsOutWithoutMovingItsFoot(int side)
        {
            var hip=Vector3.up;var knee=new Vector3(0,.5f,.2f);var ankle=Vector3.zero;
            var target=new Vector3(side*.2f,0,0);var outward=Vector3.right*side;
            var axis=(target-hip).normalized;
            var pole=BodyShapeMath.LegPole(hip,knee,ankle,target,Vector3.forward,outward,1,25,1);
            var bend=Vector3.ProjectOnPlane(pole-hip,axis).normalized;
            Assert.That(Vector3.Dot(bend,outward),Is.GreaterThan(.3f),"Knees should follow the widened stance outward");
            Assert.That(Vector3.Dot(bend,Vector3.forward),Is.GreaterThan(.8f));
            Assert.That(BodyShapeMath.LegPole(hip,knee,ankle,target,Vector3.forward,outward,0,25,1),Is.EqualTo(knee));
            var root=new GameObject("leg");root.transform.position=hip;
            var mid=new GameObject("knee").transform;mid.SetParent(root.transform);mid.position=knee;
            var tip=new GameObject("foot").transform;tip.SetParent(mid);tip.position=ankle;tip.rotation=Quaternion.Euler(10,20,30);
            var rotation=tip.rotation;float length=Vector3.Distance(hip,knee);
            try
            {
                BodyShapeMath.SolveTwoBone(root.transform,mid,tip,target,pole);
                Assert.That(Vector3.Distance(tip.position,target),Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(root.transform.position,mid.position),Is.EqualTo(length).Within(.0001f));
                Assert.That(Vector3.Distance(mid.position,tip.position),Is.EqualTo(length).Within(.0001f));
                Assert.That(Quaternion.Angle(rotation,tip.rotation),Is.LessThan(.05f));
            }
            finally{Object.DestroyImmediate(root);}
        }
        [Test] public void NearlyStraightKneesDoNotFlipWithTinyAnimatedBendNoise()
        {
            var hip=Vector3.up;var target=new Vector3(-.2f,0,0);Vector3 previous=Vector3.zero;
            for(int i=0;i<=100;i++)
            {
                var knee=new Vector3(.0001f*Mathf.Sin(i),.5f,.0001f*Mathf.Cos(i));
                var pole=BodyShapeMath.LegPole(hip,knee,Vector3.zero,target,Vector3.forward,Vector3.left,1,25,1);
                var bend=(pole-hip).normalized;
                Assert.That(float.IsFinite(bend.x+bend.y+bend.z),Is.True);
                Assert.That(Vector3.Dot(bend,Vector3.forward),Is.GreaterThan(.8f));
                if(i>0)Assert.That(Vector3.Angle(previous,bend),Is.LessThan(.1f));previous=bend;
            }
        }
        [Test] public void KneeTurnoutFadesContinuouslyAndZeroTurnoutRetainsTheTransportedPlane()
        {
            var hip=Vector3.up;var knee=new Vector3(0,.5f,.2f);var target=new Vector3(.2f,0,0);
            var previous=BodyShapeMath.LegPole(hip,knee,Vector3.zero,target,Vector3.forward,Vector3.right,1,25,0)-hip;
            Assert.That(Vector3.Angle(previous,Vector3.forward),Is.LessThan(.1f));
            for(int i=1;i<=100;i++)
            {
                var next=BodyShapeMath.LegPole(hip,knee,Vector3.zero,target,Vector3.forward,Vector3.right,1,25,i/100f)-hip;
                Assert.That(Vector3.Angle(previous,next),Is.LessThan(.3f));previous=next;
            }
        }
        [Test] public void ProjectionIsFiniteAtCenterAndDoesNotMoveClearPoints()
        {
            var radii=new Vector3(.3f,.4f,.2f);var p=BodyShapeMath.OutsideEllipsoid(Vector3.zero,Vector3.zero,radii,.1f,Vector3.left);
            Assert.That(p.x,Is.EqualTo(-.4f).Within(.00001));Assert.That(float.IsFinite(p.y),Is.True);
            var clear=new Vector3(1,2,3);Assert.That(BodyShapeMath.OutsideEllipsoid(clear,Vector3.zero,radii,.1f,Vector3.left),Is.EqualTo(clear));
        }
        [Test] public void IKPreservesSegmentLengthsAndTipRotationForUnreachableTarget()
        {
            var root=new GameObject("IK test");var mid=new GameObject("elbow").transform;mid.SetParent(root.transform);mid.localPosition=Vector3.right;
            var tip=new GameObject("hand").transform;tip.SetParent(mid);tip.localPosition=Vector3.right;tip.rotation=Quaternion.Euler(25,40,60);var rotation=tip.rotation;
            try{BodyShapeMath.SolveTwoBone(root.transform,mid,tip,new Vector3(8,4,2),Vector3.back);Assert.That(Vector3.Distance(root.transform.position,mid.position),Is.EqualTo(1).Within(.00001));Assert.That(Vector3.Distance(mid.position,tip.position),Is.EqualTo(1).Within(.00001));Assert.That(Quaternion.Angle(rotation,tip.rotation),Is.LessThan(.01f));}
            finally{Object.DestroyImmediate(root);}
        }
        [Test] public void DoggyMatrixHasSixteenClipsAndFourSynchronizedStrengthsWithoutEditingSources()
        {
            EditorSceneManager.OpenScene(DoggyBodyShapeLabBuilder.ScenePath);
            var lab=Object.FindAnyObjectByType<BodyShapeLab>();lab.Focus(0,false);
            try
            {
                Assert.That(lab.samples.Length,Is.EqualTo(64));Assert.That(lab.samples.Select(s=>s.clip).Distinct().Count(),Is.EqualTo(16));
                double baselineHands=0,strongHands=0,baselineFeet=0,strongFeet=0;
                foreach(var row in lab.samples.GroupBy(s=>s.row))
                {
                    var ordered=row.OrderBy(s=>s.column).ToArray();var clip=ordered[0].clip;var path=AssetDatabase.GetAssetPath(clip);var hash=AssetDatabase.GetAssetDependencyHash(path);
                    Assert.That(ordered.Select(s=>s.clip),Is.All.EqualTo(clip));
                    foreach(var sample in ordered)
                    {
                        var correction=sample.GetComponent<BodyShapeAdjustment>();Assert.That(correction.strength,Is.EqualTo(DoggyBodyShapeLabBuilder.Strengths[sample.column]));
                        sample.Initialize();var root=sample.transform.position;var animator=sample.GetComponent<Animator>();
                        var elbow=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);var shoulder=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
                        float upper=Vector3.Distance(elbow.position,shoulder.position),lower=Vector3.Distance(elbow.position,hand.position);
                        for(int p=0;p<12;p++)
                        {
                            sample.Sample(clip.length*(p+.5f)/12,0,true);
                            Assert.That(float.IsFinite(correction.HandPenetration+correction.FootPenetration),Is.True,sample.name);
                            Assert.That(sample.transform.position,Is.EqualTo(root),"Adjustment must not move the root");
                            Assert.That(Vector3.Distance(elbow.position,shoulder.position),Is.EqualTo(upper).Within(.005f));Assert.That(Vector3.Distance(elbow.position,hand.position),Is.EqualTo(lower).Within(.005f));
                            if(sample.column==0){baselineHands+=correction.HandPenetration;baselineFeet+=correction.FootPenetration;Assert.That(correction.MaxDisplacement,Is.Zero);}
                            if(sample.column==3){strongHands+=correction.HandPenetration;strongFeet+=correction.FootPenetration;Assert.That(correction.MaxDisplacement,Is.LessThanOrEqualTo(correction.profile.maximumOffset+.002f));}
                        }
                        sample.Stop();
                    }
                    Assert.That(AssetDatabase.GetAssetDependencyHash(path),Is.EqualTo(hash));
                }
                Assert.That(baselineHands,Is.GreaterThan(.1),"The audit must actually encounter hand overlaps");Assert.That(baselineFeet,Is.GreaterThan(.1));
                Assert.That(strongHands,Is.LessThan(baselineHands*.65),"Strong should reduce aggregate torso/hand proxy penetration");
                Assert.That(strongFeet,Is.LessThan(baselineFeet*.5),"Strong should reduce aggregate shoe proxy penetration");
            }
            finally {foreach(var s in lab.samples)s.Stop();EditorSceneManager.OpenScene(DoggyBodyShapeLabBuilder.ScenePath);}
        }
        [Test] public void ZeroWeightRestoresAnimationAndRepeatedEvaluationDoesNotDrift()
        {
            EditorSceneManager.OpenScene(DoggyBodyShapeLabBuilder.ScenePath);var lab=Object.FindAnyObjectByType<BodyShapeLab>();lab.Focus(0,true);
            var sample=lab.samples.Single(s=>s.row==0 && s.column==3);sample.Initialize();var c=sample.GetComponent<BodyShapeAdjustment>();
            try
            {
                c.strength=0;sample.Sample(sample.clip.length*.35f,0,true);var bones=sample.GetComponentsInChildren<Transform>();var rotations=bones.Select(b=>b.localRotation).ToArray();
                c.strength=1;sample.Sample(sample.clip.length*.35f,0,true);var corrected=bones.Select(b=>b.localRotation).ToArray();
                for(int pass=0;pass<5;pass++)c.Evaluate(0,true);
                for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(corrected[i],bones[i].localRotation),Is.LessThan(.05f));
                c.RestoreAnimationPose();for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(rotations[i],bones[i].localRotation),Is.LessThan(.05f));
            }
            finally{sample.Stop();EditorSceneManager.OpenScene(DoggyBodyShapeLabBuilder.ScenePath);}
        }
    }
}
