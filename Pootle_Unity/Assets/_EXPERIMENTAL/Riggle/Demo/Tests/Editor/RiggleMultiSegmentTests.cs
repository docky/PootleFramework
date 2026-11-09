using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleMultiSegmentTests
    {
        [TestCase(2)][TestCase(4)][TestCase(8)]
        public void SegmentRebuildPreservesEndpointsAndParentOrder(int segments)
        {
            var go=new GameObject("Segment test");
            try
            {
                var rig=go.AddComponent<RiggleGeneric>();rig.SetQuadrupedPreset(new Bounds(Vector3.up,new Vector3(1,2,2)));
                var endpoints=rig.legs.Select(l=>rig.joints.Single(j=>j.name==l.foot).position).ToArray();
                rig.legSegments=segments;rig.RebuildLegSegments();
                for(int i=0;i<4;i++)
                {
                    var chain=rig.legs[i].Chain();Assert.That(chain.Count,Is.EqualTo(segments+1));
                    for(int c=1;c<chain.Count;c++)Assert.That(rig.joints.Single(j=>j.name==chain[c]).parent,Is.EqualTo(chain[c-1]));
                    Assert.That(rig.joints.Single(j=>j.name==chain.Last()).position,Is.EqualTo(endpoints[i]));
                }
                rig.legSegments=2;rig.RebuildLegSegments();Assert.That(rig.legs.All(l=>l.Chain().Count==3));
            }
            finally{Object.DestroyImmediate(go);}
        }
        [TestCase(false)][TestCase(true)] public void FourSegmentIKReachesTargetAndNeverStretches(bool stabilizeTerminal)
        {
            var root=new GameObject("Chain test");
            try
            {
                var bones=new Transform[5];bones[0]=root.transform;
                for(int i=1;i<5;i++){bones[i]=new GameObject("Bone"+i).transform;bones[i].SetParent(bones[i-1],false);bones[i].localPosition=new Vector3(0,-.25f,i%2==0?.06f:-.06f);}
                var lengths=bones.Skip(1).Select(t=>t.localPosition.magnitude).ToArray();var ik=new RiggleChainIK(bones,stabilizeTerminal);
                Assert.That(ik.Solve(new Vector3(.12f,-.82f,.1f),Quaternion.Euler(15,20,0),96),Is.LessThan(.002f));
                if(stabilizeTerminal)
                {
                    Assert.That(Vector3.Angle(bones[3].position-bones[2].position,bones[4].position-bones[3].position),Is.LessThanOrEqualTo(55.01f));
                    var contact=bones[4].position;
                    Assert.That(ik.Solve(contact,Quaternion.Euler(175,20,0),128),Is.LessThan(.002f));
                    Assert.That(Vector3.Angle(bones[3].position-bones[2].position,bones[4].position-bones[3].position),Is.LessThanOrEqualTo(55.01f));
                }
                ik.Solve(new Vector3(0,-5,0),Quaternion.identity);
                for(int i=1;i<5;i++)Assert.That(Vector3.Distance(bones[i].position,bones[i-1].position),Is.EqualTo(lengths[i-1]).Within(.00001f));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
