using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator MountainSlide_FullRideClearsBothJumpsAtThirtySixtyAndOneTwentyHz()
        {
            var slide=GameObject.Find("Mountain slide · banked downhill");Assert.That(slide,Is.Not.Null);
            var mesh=slide.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
            int count=vertices.Length/66-1;
            foreach(int hz in new[]{30,60,120})
            {
                character.simulateAutomatically=false;
                character.Teleport(vertices[32]+Vector3.up*.08f,Quaternion.identity);
                int progress=0,flight=0;bool firstJump=false,secondJump=false;float peak=0;
                for(int i=0;i<hz*35;i++)
                {
                    Physics.SyncTransforms();character.Tick(1f/hz,default);
                    float nearest=float.PositiveInfinity;int best=progress;
                    for(int j=Mathf.Max(0,progress-12);j<=Mathf.Min(count,progress+90);j++)
                    {
                        float d=Vector3.Distance(character.transform.position,vertices[j*66+32]);
                        if(d<nearest){nearest=d;best=j;}
                    }
                    progress=best;float distance=progress*413.2891f/count;
                    peak=Mathf.Max(peak,character.Velocity.magnitude);
                    Assert.That(nearest,Is.LessThan(12),"Left course at "+distance+" m / "+hz+" Hz");
                    Assert.That(character.Mode,Is.Not.EqualTo(PootleMode.LedgeHanging));
                    if(character.Mode==PootleMode.Airborne)
                    {flight++;if(distance>93 && distance<110)firstJump=true;if(distance>211 && distance<234)secondJump=true;}
                    if(progress>=count-12)break;
                }
                Assert.That(progress,Is.GreaterThanOrEqualTo(count-12),"Did not finish at "+hz+" Hz");
                Assert.That(firstJump && secondJump,Is.True,"Both open gaps must be crossed in free flight");
                Assert.That(flight,Is.GreaterThan(hz/3));Assert.That(peak,Is.GreaterThan(26));
                Assert.That(character.Velocity.magnitude,Is.GreaterThan(15),"Retain momentum at finish");
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator MountainSlide_BallisticRideCanLandOnRailAndClearsSlideState()
        {
            var vertices=GameObject.Find("Mountain slide · banked downhill").GetComponent<MeshFilter>().sharedMesh.vertices;
            character.simulateAutomatically=false;character.Teleport(vertices[32]+Vector3.up*.08f,Quaternion.identity);
            for(int i=0;i<600;i++)
            {
                RideTick();
                if(character.SlideRideAirborne && character.Velocity.y<-2)break;
            }
            Assert.That(character.SlideRideAirborne);
            var go=new GameObject("Temporary gap rail");
            try
            {
                Vector3 next=character.transform.position+character.Velocity/60;
                Vector3 side=Vector3.Cross(Vector3.up,character.Velocity).normalized;
                Vector3 center=next+Vector3.up*.015f;
                var rail=go.AddComponent<PootleRail>();rail.points=new[]{center-side*5,center+side*5};
                RideTick();
                Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.SameAs(rail));
                Assert.That(character.SlideRideActive,Is.False);
                Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));
            }
            finally{Object.Destroy(go);}
            yield return null;
        }
        [UnityTest]
        public IEnumerator MountainSlide_BWhileOverJumpGapStartsGroundPound()
        {
            var vertices=GameObject.Find("Mountain slide · banked downhill").GetComponent<MeshFilter>().sharedMesh.vertices;
            character.simulateAutomatically=false;character.Teleport(vertices[32]+Vector3.up*.08f,Quaternion.identity);
            for(int i=0;i<600 && !character.SlideRideAirborne;i++)RideTick();
            Assert.That(character.SlideRideAirborne);
            RideTick(1f/60,new PootleInputFrame{detachPressed=true});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.GroundPounding));
            Assert.That(character.SlideRideActive,Is.False);
            Assert.That(character.Velocity.y,Is.LessThan(-20));
            yield return null;
        }
        [UnityTest]
        public IEnumerator MountainSlide_GapsAreOpenAndHaveWideLandingTroughs()
        {
            var slide=GameObject.Find("Mountain slide · banked downhill").GetComponent<MeshCollider>();
            var vertices=slide.sharedMesh.vertices;int count=vertices.Length/66-1;
            foreach(float distance in new[]{97f,215.5f})
            {
                int i=Mathf.RoundToInt(distance/413.2891f*count);Vector3 center=vertices[i*66+32];
                Assert.That(slide.Raycast(new Ray(center+Vector3.up*3,Vector3.down),out _,6),Is.False,"Gap contains collision");
                int landing=Mathf.RoundToInt((distance+6)/413.2891f*count);
                Assert.That(Vector3.Distance(vertices[landing*66],vertices[landing*66+64]),Is.GreaterThan(12));
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator MountainSlide_AccessPadAndReturnPadWorkInLiveScene()
        {
            var pad=GameObject.Find("Mountain slide access pad").GetComponent<PootleTravelPad>();
            character.Teleport(pad.transform.position+Vector3.up*.1f,Quaternion.identity);
            yield return new WaitForSeconds(.3f);
            Assert.That(Vector3.Distance(character.transform.position,pad.destination),Is.LessThan(.3f));
            Assert.That(character.IsGrounded);
            var back=GameObject.Find("Mountain slide return pad").GetComponent<PootleTravelPad>();
            character.Teleport(back.transform.position+Vector3.up*.1f,Quaternion.identity);
            yield return new WaitForSeconds(.3f);
            Assert.That(Vector3.Distance(character.transform.position,back.destination),Is.LessThan(.3f));
        }
    }
}
