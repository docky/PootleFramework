using NUnit.Framework;
using UnityEngine;
namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        [TestCase(25)] [TestCase(44)]
        public void Feedback_UphillHasNoStepPulsesOrDownwardJudder(float angle)
        {
            LandingRamp(angle);ResetAt(new Vector3(0,8,0));Step(100);
            float previous=character.transform.position.y;
            for(int i=0;i<40;i++)
            {
                Step(1,new PootleInputFrame{move=Vector2.up});
                float rise=character.transform.position.y-previous;previous=character.transform.position.y;
                Assert.That(rise,Is.GreaterThan(-.005f),"Uphill moved down at tick "+i);
                Assert.That(rise,Is.LessThan(.12f),"Uphill step pulse at tick "+i);
                Assert.That(character.IsGrounded,Is.True,"Lost slope at tick "+i);
            }
        }
        [Test]
        public void Feedback_SmallHopClearsIntermediateBlockButRespectsCeiling()
        {
            profile.smallObstacleHop=true;
            Box("Short block",new Vector3(0,.4f,2),new Vector3(4,.8f,2));
            Step(50,new PootleInputFrame{move=Vector2.up});
            Assert.That(character.transform.position.y,Is.GreaterThan(.78f));
            Assert.That(character.transform.position.z-origin.z,Is.GreaterThan(1.5f));
            ResetAt(new Vector3(0,.02f,0));Box("Ceiling",new Vector3(0,2.35f,2),new Vector3(4,.2f,4));
            Step(45,new PootleInputFrame{move=Vector2.up});
            Assert.That(character.transform.position.y,Is.LessThan(.1f));
            Assert.That(character.Motor.CanOccupy(character.transform.position));
        }
        [Test]
        public void Feedback_GroundPoundLocksOntoPoleTop()
        {
            var pole=Pole(new Vector3(0,2,0),4);pole.standOnTop=true;pole.allowExitTop=true;
            ResetAt(pole.Top-origin+Vector3.up*3);
            Step(1,new PootleInputFrame{detachPressed=true});
            for(int i=0;i<30 && character.Mode!=PootleMode.PoleStanding;i++)Step(1);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleStanding));
            Assert.That(Vector3.Distance(character.transform.position,pole.Top),Is.LessThan(.03f));
        }
        [Test]
        public void Feedback_MovingHookNeutralJumpHasNoPlanarLaunch()
        {
            var hook=Hook(new Vector3(0,3,0));ResetAt(hook.HangingPosition(Vector3.forward)-origin);
            Step(1,new PootleInputFrame{grip=true});hook.transform.position+=Vector3.right*.1f;
            Step(1,new PootleInputFrame{grip=true,jumpPressed=true,jumpHeld=true});
            Assert.That(Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude,Is.LessThan(.01f));
            Assert.That(character.Velocity.y,Is.GreaterThan(17));
        }
        [Test]
        public void Feedback_BumperMomentumDoesNotTurnAvatarAndEventuallyReleasesFacing()
        {
            character.ApplyImpulse(new Vector3(12,4,0));Vector3 facing=character.Facing;
            Step(30);Assert.That(Vector3.Dot(facing,character.Facing),Is.GreaterThan(.99f));
            Assert.That(character.transform.position.x-origin.x,Is.GreaterThan(3));
            Step(75,new PootleInputFrame{move=Vector2.right});Assert.That(character.Facing.x,Is.GreaterThan(.9f));
        }
        [Test]
        public void Feedback_WindLiftsAndPushesWithoutChangingFacing()
        {
            var go=new GameObject("Wind");objects.Add(go);go.transform.position=origin+Vector3.up*5;
            go.AddComponent<BoxCollider>().size=Vector3.one*20;var wind=go.AddComponent<PootleWind>();wind.localVelocity=new Vector3(6,12,0);
            Step(45);Assert.That(character.transform.position.y,Is.GreaterThan(3));
            Assert.That(character.transform.position.x-origin.x,Is.GreaterThan(2));
            Assert.That(Vector3.Dot(character.Facing,Vector3.forward),Is.GreaterThan(.99f));
        }
        [Test]
        public void Feedback_WindResumesAfterReleasingWallGrip()
        {
            var wall=Box("Wind wall",new Vector3(0,3,4),new Vector3(20,6,1));wall.AddComponent<PootleClimbSurface>();
            var go=new GameObject("Side wind");objects.Add(go);go.transform.position=origin;go.AddComponent<BoxCollider>().size=Vector3.one*30;
            var wind=go.AddComponent<PootleWind>();wind.localVelocity=Vector3.right*6;
            ResetAt(new Vector3(0,1,2.98f));Step(30,new PootleInputFrame{grip=true});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));float x=character.transform.position.x;
            Step(1,new PootleInputFrame{detachPressed=true});Step(12);
            Assert.That(character.transform.position.x-x,Is.GreaterThan(.5f));
        }
        [Test]
        public void Feedback_ConveyorCarriesIdlePlayerWithoutTurningAndStopsAtWalls()
        {
            var deck=Box("Belt",new Vector3(0,.15f,0),new Vector3(6,.3f,20));var belt=deck.AddComponent<PootleConveyor>();belt.speed=3;
            ResetAt(new Vector3(0,.32f,0));Step(3);Step(60);
            Assert.That(character.transform.position.z-origin.z,Is.InRange(2.8f,3.3f));
            Assert.That(character.Facing,Is.EqualTo(Vector3.forward));
            Box("Block belt",new Vector3(0,2,4.5f),new Vector3(6,4,.3f));Step(90);
            Assert.That(character.transform.position.z-origin.z,Is.LessThan(4));Assert.That(character.Motor.CanOccupy(character.transform.position));
        }
        [Test]
        public void Feedback_CrateDelaysFallAndPlayerFollowsFastDescentThenJumps()
        {
            var crate=Box("Falling step",new Vector3(0,12,0),Vector3.one*.8f);crate.AddComponent<PootleCarryable>();
            var fall=crate.AddComponent<PootleFallingCrate>();fall.simulateAutomatically=false;
            ResetAt(new Vector3(0,12.42f,0));Step(3);
            for(int i=0;i<15;i++){fall.Tick(Dt);Step(1);}
            Assert.That(fall.FallSpeed,Is.LessThan(1));
            for(int i=0;i<35;i++){fall.Tick(Dt);Step(1);Assert.That(character.transform.position.y-crate.transform.position.y,Is.EqualTo(.42f).Within(.04f));}
            Assert.That(fall.FallSpeed,Is.GreaterThan(20));Assert.That(character.IsGrounded);
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(character.IsGrounded,Is.False);Assert.That(character.Velocity.y,Is.GreaterThan(17));
        }
        [Test]
        public void Feedback_CylinderOrbitIsSmoothAndRotatesViewBySameAngle()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);objects.Add(go);go.transform.position=origin+Vector3.up*3;go.transform.localScale=new Vector3(4,3,4);
            var surface=go.AddComponent<PootleClimbSurface>();surface.cylindrical=true;
            ResetAt(new Vector3(0,2,-2.52f));Step(1,new PootleInputFrame{grip=true});
            float yaw=0;character.ClimbingViewRotated+=delta=>yaw+=delta;
            Vector3 previous=character.transform.position;
            for(int i=0;i<120;i++)
            {
                Step(1,new PootleInputFrame{grip=true,move=Vector2.right});
                float step=Vector3.Distance(previous,character.transform.position);previous=character.transform.position;
                Assert.That(step,Is.InRange(.025f,.06f));
                Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));
                Assert.That(Vector3.ProjectOnPlane(character.transform.position-go.transform.position,Vector3.up).magnitude,Is.EqualTo(2.52f).Within(.015f));
            }
            Assert.That(Mathf.Abs(yaw),Is.GreaterThan(90));
        }
        [TestCase(true,95,true)] [TestCase(false,95,false)] [TestCase(true,85,false)]
        public void Feedback_BoxCornerWrappingHonorsOptionAndAngleLimit(bool wrap,float limit,bool passes)
        {
            var wall=Box("Corner",new Vector3(0,3,0),new Vector3(4,6,4));var marker=wall.AddComponent<PootleClimbSurface>();marker.allowCornerWrapping=wrap;marker.maximumCornerAngle=limit;
            ResetAt(new Vector3(1.7f,2,-2.52f));Step(1,new PootleInputFrame{grip=true});
            Step(60,new PootleInputFrame{grip=true,move=Vector2.right});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));
            Assert.That(character.transform.position.z-origin.z> -1.7f,Is.EqualTo(passes));
        }
    }
}
