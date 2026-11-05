using NUnit.Framework;
using UnityEngine;
namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        [TestCase(44)] [TestCase(45)]
        public void LedgeCatchRejectsRampSideEvenWhenTopIsWalkable(float angle)
        {
            var ramp=Box("Ramp side",new Vector3(0,2,2),new Vector3(5,.4f,8));
            ramp.transform.rotation=Quaternion.Euler(-angle,0,0);
            // Approach the vertical x side at a hand-height matching the slope.
            character.Teleport(origin+new Vector3(-3.05f,.8f,2),Quaternion.Euler(0,90,0));
            Physics.SyncTransforms();
            Assert.That(character.Motor.Ray(character.transform.position+Vector3.up*1.1f,Vector3.right,.8f,PootleProbeKind.Wall,out var face),Is.True);
            Assert.That(Mathf.Abs(face.normal.y),Is.LessThan(.01f),"Fixture reaches the vertical ramp side");
            for(int i=0;i<25;i++)
            {
                Step(1);
                Assert.That(character.Mode,Is.Not.EqualTo(PootleMode.LedgeHanging));
                Assert.That(character.Mode,Is.Not.EqualTo(PootleMode.LedgeHop));
            }
        }
        [Test]
        public void LedgeCannotUseUnrelatedPlatformBehindItsWall()
        {
            Box("Thin face",new Vector3(0,1.2f,.6f),new Vector3(5,2.4f,.1f));
            Box("Separate high platform",new Vector3(0,2.9f,1.1f),new Vector3(5,.2f,.6f));
            ResetAt(new Vector3(0,1.5f,-.05f));Step(5);
            Assert.That(character.Mode,Is.Not.EqualTo(PootleMode.LedgeHanging));
        }
        [Test]
        public void TopOutTransfersDirectlyToGroundWithoutAirbornePose()
        {
            Box("Wall",new Vector3(0,1.5f,1),new Vector3(6,3,1)).AddComponent<PootleClimbSurface>();
            ResetAt(new Vector3(0,.02f,-.05f));
            for(int i=0;i<120 && character.Mode!=PootleMode.LedgeHop;i++)Step(1,new PootleInputFrame{grip=true,move=Vector2.up});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.LedgeHop));
            bool airborne=false;character.ModeChanged+=mode=>airborne|=mode==PootleMode.Airborne;
            for(int i=0;i<90 && character.Mode==PootleMode.LedgeHop;i++)Step(1);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Grounded));
            Assert.That(airborne,Is.False,"No transient airborne event during the handoff");
            Assert.That(character.Velocity.magnitude,Is.LessThan(.01f));
        }
        [TestCase(false)] [TestCase(true)]
        public void DisabledWallDropsAndCannotReattachImmediately(bool disableMarker)
        {
            profile.automaticGrip=true;
            var wall=Box("Wall",new Vector3(0,2,1),new Vector3(5,4,1)).AddComponent<PootleClimbSurface>();
            ResetAt(new Vector3(0,1,-.05f));Step(2);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));
            if(disableMarker)wall.enabled=false;else wall.GetComponent<Collider>().enabled=false;
            Step(1);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            wall.enabled=true;wall.GetComponent<Collider>().enabled=true;Step(10);
            Assert.That(character.Mode,Is.Not.EqualTo(PootleMode.Climbing));
        }
        [Test]
        public void ClimbJumpDoesNotGetCapturedByNearbyHook()
        {
            profile.automaticGrip=true;
            Box("Wall",new Vector3(0,2,1),new Vector3(5,4,1)).AddComponent<PootleClimbSurface>();
            ResetAt(new Vector3(0,1,-.05f));Step(2);
            var hook=Hook(character.transform.position-origin+Vector3.up*profile.ledgeHandHeight);
            hook.grabRadius=3;
            Step(5);Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Step(8,new PootleInputFrame{jumpHeld=true});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.GetComponent<PootleHookRider>().Hook,Is.Null);
        }
    }
}
