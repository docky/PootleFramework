using NUnit.Framework;
using UnityEngine;
namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void OneWay_JumpThroughAndLandOnTop(int hz)
        {
            var platform=Box("One way",new Vector3(0,1.4f,0),new Vector3(4,.2f,4));platform.AddComponent<PootleOneWayPlatform>();Physics.SyncTransforms();
            float dt=1f/hz,maxY=0;bool ceiling=false;
            for(int i=0;i<hz*3;i++)
            {
                Step(1,new PootleInputFrame{jumpPressed=i==0,jumpHeld=true},dt);
                maxY=Mathf.Max(maxY,character.transform.position.y-origin.y);
                for(int j=0;j<character.Motor.ContactCount;j++)ceiling|=character.Motor.Contacts[j].ceiling;
            }
            Assert.That(ceiling,Is.False);Assert.That(maxY,Is.GreaterThan(1.7f));
            Assert.That(character.IsGrounded);Assert.That(character.transform.position.y-origin.y,Is.EqualTo(1.5f).Within(.06f));
        }
        [Test]
        public void OneWay_PartialAscentDoesNotCatchOrRecoverInside()
        {
            profile.jumpLaunchSpeed=12;
            var platform=Box("Too high to land",new Vector3(0,3,0),new Vector3(4,.3f,4));platform.AddComponent<PootleOneWayPlatform>();Physics.SyncTransforms();
            Step(180,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(character.IsGrounded);Assert.That(character.transform.position.y-origin.y,Is.LessThan(.1f));
            Assert.That(character.GetComponent<PootleRecovery>().RecoveryCount,Is.Zero);
        }
        [Test]
        public void OneWay_HighSpeedFallGroundPoundAndWalkOff()
        {
            var platform=Box("One way",new Vector3(0,2,0),new Vector3(4,.2f,4));platform.AddComponent<PootleOneWayPlatform>();Physics.SyncTransforms();
            ResetAt(new Vector3(0,7,0));Step(60,new PootleInputFrame{detachPressed=true});
            Assert.That(character.IsGrounded);Assert.That(character.transform.position.y-origin.y,Is.EqualTo(2.1f).Within(.06f));
            Step(90,new PootleInputFrame{move=Vector2.right});
            Assert.That(character.transform.position.x-origin.x,Is.GreaterThan(4));Assert.That(character.transform.position.y-origin.y,Is.LessThan(.1f));
        }
        [Test]
        public void OneWay_SidesAndClearanceIgnorePlatformButDisabledMarkerIsSolid()
        {
            var platform=Box("One way",new Vector3(0,1,0),new Vector3(4,.3f,4));var marker=platform.AddComponent<PootleOneWayPlatform>();Physics.SyncTransforms();
            ResetAt(new Vector3(-4,.02f,0));Step(70,new PootleInputFrame{move=Vector2.right});
            Assert.That(character.transform.position.x-origin.x,Is.GreaterThan(2.5f),"The capsule clears the far edge without side collision");
            Assert.That(character.Motor.CanOccupy(origin+new Vector3(0,.02f,0)),Is.True);
            marker.enabled=false;ResetAt(new Vector3(-4,.02f,0));Step(70,new PootleInputFrame{move=Vector2.right});
            Assert.That(character.transform.position.x-origin.x,Is.LessThan(-2));
            Assert.That(character.Motor.CanOccupy(origin+new Vector3(0,.02f,0)),Is.False);
        }
    }
}
