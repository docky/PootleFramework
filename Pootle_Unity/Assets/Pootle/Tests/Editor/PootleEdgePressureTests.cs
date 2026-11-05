using NUnit.Framework;
using UnityEngine;
namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        [TestCase(.1f,true)] [TestCase(.35f,true)] [TestCase(.9f,true)] [TestCase(1.5f,true)] [TestCase(.9f,false)]
        public void EdgeBoundary_HighLandingResolvesToTopWithoutClimbInput(float belowTop,bool climbable)
        {
            var box=Box("Landing wall",new Vector3(0,1.5f,2),new Vector3(6,3,3));
            if(climbable)box.AddComponent<PootleClimbSurface>();profile.automaticGrip=true;
            ResetAt(new Vector3(0,3-belowTop,-.02f));
            Step(1);Assert.That(character.Mode,Is.EqualTo(PootleMode.LedgeHop));
            float prior=character.transform.position.y;
            for(int i=0;i<60 && character.Mode==PootleMode.LedgeHop;i++)
            {
                Step(1);Assert.That(character.transform.position.y,Is.GreaterThan(prior-.12f),"No snap down into a lower hang");prior=character.transform.position.y;
            }
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Grounded));
            Assert.That(character.transform.position.y-origin.y,Is.EqualTo(3).Within(.08f));
        }
        [Test]
        public void EdgeBoundary_ClimbingStopsAtHeadHeightAndBlockedTopHangsBelowLip()
        {
            Box("Climb wall",new Vector3(0,1.5f,2),new Vector3(6,3,3)).AddComponent<PootleClimbSurface>();
            Box("Blocked top",new Vector3(0,4,2),new Vector3(6,1.5f,3));profile.automaticGrip=true;
            ResetAt(new Vector3(0,.1f,-.02f));
            for(int i=0;i<90;i++)
            {
                Step(1,new PootleInputFrame{move=Vector2.up});
                if(character.Mode==PootleMode.Climbing || character.Mode==PootleMode.LedgeHanging)
                    Assert.That(character.transform.position.y-origin.y,Is.LessThanOrEqualTo(3-profile.ledgeHandHeight+.1f));
            }
            Assert.That(character.Mode,Is.EqualTo(PootleMode.LedgeHanging));
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void RailPressure_NeedsSettledNeutralThenSustainedSideInput(int hz)
        {
            var rail=Rail(new Vector3(0,4,-5),new Vector3(0,4,5));ResetAt(new Vector3(0,4,0));
            var rider=character.GetComponent<PootleRailRider>();Assert.That(rider.TryMount(character,.1f,.2f));float dt=1f/hz;
            Step(hz,new PootleInputFrame{move=Vector2.right},dt);Assert.That(rider.Rail,Is.SameAs(rail),"Held landing input must not dismount");
            Step(hz/3,default,dt);Vector3 start=character.transform.position;
            Step(hz/6,new PootleInputFrame{move=Vector2.right},dt);Assert.That(rider.Rail,Is.SameAs(rail));
            Assert.That(Vector3.Distance(start,character.transform.position),Is.LessThan(.01f));
            for(int i=0;i<hz && rider.Rail;i++)Step(1,new PootleInputFrame{move=Vector2.right},dt);
            Assert.That(rider.Rail,Is.Null);Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.Velocity.x,Is.GreaterThan(1));Assert.That(character.Velocity.y,Is.LessThan(0));
            Step(hz/5,new PootleInputFrame{move=Vector2.right},dt);Assert.That(rider.Rail,Is.Null);
            Assert.That(character.transform.position.x,Is.GreaterThan(start.x+.5f));
        }
        [Test]
        public void RailPressure_JunctionTakesPriorityOverArmedDismount()
        {
            Rail(new Vector3(-4,3,0),new Vector3(4,3,0));var branch=Rail(new Vector3(0,3,0),new Vector3(0,3,6));
            ResetAt(new Vector3(-.6f,3,0));var rider=character.GetComponent<PootleRailRider>();Assert.That(rider.TryMount(character,.1f,.2f));
            Step(30);Step(35,new PootleInputFrame{move=Vector2.up});
            Assert.That(rider.Rail,Is.SameAs(branch));Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));
        }
    }
}
