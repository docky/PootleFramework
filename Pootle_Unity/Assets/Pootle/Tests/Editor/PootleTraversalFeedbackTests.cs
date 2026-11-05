using NUnit.Framework;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        private PootlePole Pole(Vector3 center,float length)
        {
            var go=new GameObject("Feedback pole");objects.Add(go);go.transform.position=origin+center;
            var p=go.AddComponent<PootlePole>();p.length=length;
            var collider=go.AddComponent<CapsuleCollider>();collider.radius=p.radius;collider.height=length;collider.isTrigger=true;
            Physics.SyncTransforms();return p;
        }
        [Test]
        public void SidewaysRailStandingAndIdleJumpHaveNoForwardBoost()
        {
            Rail(new Vector3(0,2,-5),new Vector3(0,2,5));ResetAt(new Vector3(0,4,0));
            for(int i=0;i<90&&character.Mode!=PootleMode.RailWalking;i++)Step(1);
            Vector3 start=character.transform.position;Step(12,new PootleInputFrame{move=Vector2.right});
            Assert.That(Vector3.Distance(start,character.transform.position),Is.LessThan(.03f));
            Assert.That(Vector3.Dot(character.Facing,Vector3.right),Is.GreaterThan(.99f));
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude,Is.LessThan(.01f));
            Assert.That(character.Velocity.y,Is.GreaterThan(15));
        }
        [Test]
        public void FallingOntoInclinedRailDoesNotConvertFallSpeedToForwardSpeed()
        {
            Rail(new Vector3(0,1,-5),new Vector3(0,5,5));ResetAt(new Vector3(0,8,0));
            for(int i=0;i<120&&character.Mode!=PootleMode.RailWalking;i++)Step(1);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));Assert.That(character.Velocity.magnitude,Is.LessThan(.01f));
        }
        [Test]
        public void RailMagnetismAssistsNearMissButDoesNotCatchDistantJump()
        {
            Rail(new Vector3(0,2,-5),new Vector3(0,2,5));ResetAt(new Vector3(.9f,4,0));Step(90);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));
            ResetAt(new Vector3(1.6f,4,0));Step(90);Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.Null);
        }
        [Test]
        public void TJunctionTransfersBetweenSeparateRailsOnlyWithBranchInput()
        {
            var main=Rail(new Vector3(-4,2,0),new Vector3(4,2,0));var branch=Rail(new Vector3(0,2,0),new Vector3(0,2,5));
            ResetAt(new Vector3(-1.5f,4,0));Step(45);
            for(int i=0;i<100&&character.transform.position.x-origin.x<-.14f;i++)Step(1,new PootleInputFrame{move=Vector2.right});
            Step(30,new PootleInputFrame{move=Vector2.up});
            Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.SameAs(branch));
            Assert.That(character.transform.position.z-origin.z,Is.GreaterThan(.5f));
        }
        [TestCase(-.75f)]
        [TestCase(.75f)]
        [TestCase(-.3f)]
        public void JunctionBranchInputGuidesFromNearbyAlongExistingRail(float offset)
        {
            var main=Rail(new Vector3(-4,2,0),new Vector3(4,2,0));
            var branch=Rail(new Vector3(0,2,0),new Vector3(0,4,6));
            ResetAt(new Vector3(offset,4,0));Step(50);
            Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.SameAs(main));
            for(int i=0;i<60;i++)
            {
                var before=character.transform.position;Step(1,new PootleInputFrame{move=Vector2.up});
                Assert.That(Vector3.Distance(before,character.transform.position),Is.LessThan(profile.walkSpeed*Dt+.025f),"No snap to branch");
                if(character.GetComponent<PootleRailRider>().Rail==main)
                    Assert.That(character.transform.position.z-origin.z,Is.Zero.Within(.01f),"Stay on support during approach");
            }
            Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.SameAs(branch));
            Assert.That(character.transform.position.z-origin.z,Is.GreaterThan(2));
            Assert.That(character.transform.position.y,Is.GreaterThan(2.5f));
        }
        [Test]
        public void NearbyDisconnectedRailDoesNotActAsJunction()
        {
            var main=Rail(new Vector3(-4,2,0),new Vector3(4,2,0));
            Rail(new Vector3(0,2,.4f),new Vector3(0,2,6));
            ResetAt(new Vector3(-.5f,4,0));Step(50);var start=character.transform.position;
            Step(12,new PootleInputFrame{move=Vector2.up});
            Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.SameAs(main));
            Assert.That(Vector3.Distance(start,character.transform.position),Is.LessThan(.01f));
        }
        [TestCase(.15f,60)]
        [TestCase(1.5f,30)]
        [TestCase(1.5f,120)]
        public void HelixWalkingFollowsActualSplineAtBoundedSpeedWithoutSampleHops(float spacing,int hz)
        {
            var go=new GameObject("Continuous helix");objects.Add(go);go.transform.position=origin;
            var path=go.AddComponent<SplineContainer>();var spline=new Spline();
            for(int i=0;i<=32;i++)
            {
                float t=i/32f,a=t*Mathf.PI*4;
                spline.Add(new BezierKnot(new float3(Mathf.Cos(a)*4,2+t*7,Mathf.Sin(a)*4)),TangentMode.AutoSmooth);
            }
            path.Spline=spline;var adapter=go.AddComponent<PootleSplineRail>();adapter.path=path;adapter.sampleSpacing=spacing;adapter.Rebuild();
            var rail=go.GetComponent<PootleRail>();var rider=character.GetComponent<PootleRailRider>();
            ResetAt((Vector3)path.EvaluatePosition(.65f)+Vector3.up*adapter.surfaceOffset-origin);
            Assert.That(rider.TryMount(character,.1f,.2f));float dt=1f/hz;
            for(int i=0;i<hz*3;i++)
            {
                var local=go.transform.InverseTransformPoint(character.transform.position-Vector3.up*adapter.surfaceOffset);
                SplineUtility.GetNearestPoint(spline,(float3)local,out float3 nearest,out float t,32,3);
                Vector3 direction=-((Vector3)path.EvaluateTangent(t)).normalized;
                Vector2 input=new Vector2(direction.x,direction.z).normalized;
                var before=character.transform.position;var facing=character.Facing;float speed=Mathf.Abs(rider.Speed);
                Step(1,new PootleInputFrame{move=input},dt);
                Assert.That(rider.Rail,Is.SameAs(rail));Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));
                float travel=Vector3.Distance(before,character.transform.position);
                Assert.That(travel,Is.EqualTo((speed+Mathf.Abs(rider.Speed))*.5f*dt).Within(.004f),"No branch snapping or speed spikes at sample boundaries");
                if(i>hz/2)Assert.That(Mathf.Abs(rider.Speed),Is.EqualTo(profile.walkSpeed).Within(.02f));
                if(i>0)Assert.That(Vector3.Angle(facing,character.Facing),Is.LessThan(6));
                local=go.transform.InverseTransformPoint(character.transform.position-Vector3.up*adapter.surfaceOffset);
                SplineUtility.GetNearestPoint(spline,(float3)local,out nearest,out _,32,3);
                Assert.That(Vector3.Distance(local,(Vector3)nearest),Is.LessThan(.004f),"Resolve to spline, not sample chords");
            }
        }
        [Test]
        public void PoleTopNeutralJumpReturnsToSamePointWithAnyFacing()
        {
            var pole=Pole(new Vector3(0,.5f,0),1);pole.climbable=false;
            ResetAt(new Vector3(0,3,0));Step(60);Step(10,new PootleInputFrame{move=Vector2.left});
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            for(int i=0;i<150 && character.Mode!=PootleMode.PoleStanding;i++)
            {
                Step(1,new PootleInputFrame{jumpHeld=true});
                Assert.That(Vector3.ProjectOnPlane(character.transform.position-pole.Top,Vector3.up).magnitude,Is.LessThan(.01f));
            }
            Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleStanding));
        }
        [Test]
        public void AutomaticWallGripPersistsUntilBAndDoesNotImmediatelyRegrab()
        {
            profile.automaticGrip=true;Box("Auto wall",new Vector3(0,2,1),new Vector3(5,4,1)).AddComponent<PootleClimbSurface>();
            ResetAt(new Vector3(0,.02f,-.05f));Step(2);Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));
            Step(20);Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));
            Step(1,new PootleInputFrame{detachPressed=true});Step(15);
            Assert.That(character.Mode,Is.Not.EqualTo(PootleMode.Climbing));
        }
        [Test]
        public void AutomaticHookStaysWithoutGripAndBDetachesWithoutPounding()
        {
            profile.automaticGrip=true;var hook=Hook(new Vector3(0,4,0));ResetAt(hook.HangingPosition(Vector3.forward)-origin);
            Step(20);Assert.That(character.Mode,Is.EqualTo(PootleMode.Dangling));
            hook.transform.position+=Vector3.right*.03f;Step(1);
            Step(1,new PootleInputFrame{detachPressed=true});
            Assert.That(character.Velocity.x,Is.GreaterThan(1));Step(10);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));Assert.That(character.Velocity.y,Is.GreaterThan(-profile.groundPoundSpeed));
        }
        [Test]
        public void ShortPostsCannotBeClimbedButCanBeLandedOnAndStayUntilJump()
        {
            var pole=Pole(new Vector3(0,.5f,0),1);pole.climbable=false;
            ResetAt(new Vector3(0,2.5f,0));Step(60);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleStanding));
            Step(60,new PootleInputFrame{move=Vector2.right,grip=true});
            Assert.That(Vector3.Distance(character.transform.position,pole.Top),Is.LessThan(.02f));
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            Assert.That(Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude,Is.LessThan(.01f));
        }
        [Test]
        public void PoleTopJumpCanBeSteeredOntoNextPostInTheAir()
        {
            var first=Pole(new Vector3(0,.5f,0),1);first.climbable=false;
            var second=Pole(new Vector3(0,.6f,2f),1.2f);second.climbable=false;
            ResetAt(new Vector3(0,2,0));Step(60);Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleStanding));
            Step(1,new PootleInputFrame{jumpPressed=true});
            for(int i=0;i<60 && character.Mode!=PootleMode.PoleStanding;i++)Step(1,new PootleInputFrame{move=Vector2.up});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleStanding));
            Assert.That(character.GetComponent<PootleTraversal>().AttachedPole,Is.SameAs(second));
        }
        [Test]
        public void ClimbingToPoleTopTransitionsToFixedStandingPoint()
        {
            var pole=Pole(new Vector3(0,2,.6f),4);ResetAt(new Vector3(0,.02f,0));
            Step(170,new PootleInputFrame{grip=true,move=Vector2.up});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleStanding));
            Assert.That(Vector3.Distance(character.transform.position,pole.Top),Is.LessThan(.03f));
        }
        [Test]
        public void ElevatedPoleDropsFromOpenBottomAndLockedEndsRemainAttached()
        {
            var pole=Pole(new Vector3(0,4,.6f),4);ResetAt(new Vector3(0,2,0));
            Step(80,new PootleInputFrame{grip=true,move=Vector2.down});
            Assert.That(character.Mode,Is.Not.EqualTo(PootleMode.PoleClimbing));
            pole.allowExitBottom=pole.allowExitTop=false;ResetAt(new Vector3(0,2,0));
            Step(180,new PootleInputFrame{grip=true,move=Vector2.up});Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleClimbing));
            Step(240,new PootleInputFrame{grip=true,move=Vector2.down});Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleClimbing));
            Assert.That((character.transform.position+character.Motor.center).y,Is.GreaterThanOrEqualTo(pole.Bottom.y-.03f));
        }
        [Test]
        public void PoleFacingRangeAndLadderLockConstrainTurning()
        {
            var pole=Pole(new Vector3(0,3,.6f),6);pole.limitFacing=true;pole.minimumYaw=-30;pole.maximumYaw=30;
            ResetAt(new Vector3(0,.02f,0));Step(60,new PootleInputFrame{grip=true,move=Vector2.right});
            Assert.That(Vector3.SignedAngle(Vector3.forward,character.Facing,Vector3.up),Is.EqualTo(30).Within(.1f));
            pole.minimumYaw=pole.maximumYaw=0;Step(60,new PootleInputFrame{grip=true,move=Vector2.left});
            Assert.That(Vector3.Dot(character.Facing,Vector3.forward),Is.GreaterThan(.999f));
        }
        [Test]
        public void ClimbingNarrowWallAutomaticallyMountsItsTopRail()
        {
            Box("Narrow wall",new Vector3(0,1.5f,1),new Vector3(5,3,.3f)).AddComponent<PootleClimbSurface>();
            var rail=Rail(new Vector3(-2.5f,3.03f,1),new Vector3(2.5f,3.03f,1));
            ResetAt(new Vector3(0,.02f,.3f));Step(140,new PootleInputFrame{grip=true,move=Vector2.up});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.SameAs(rail));
        }
        [Test]
        public void GroundPoundStartsDownhillSlideOnShallowSlope()
        {
            LandingRamp(20);ResetAt(new Vector3(0,9,0));Step(1,new PootleInputFrame{detachPressed=true});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.GroundPounding));
            for(int i=0;i<90&&character.Mode!=PootleMode.Sliding;i++)Step(1);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Sliding));Assert.That(character.Velocity.z,Is.LessThan(-1));
        }
        [Test]
        public void GroundPoundStartsSlideOnShallowRail()
        {
            Rail(new Vector3(0,1,-5),new Vector3(0,4,5));ResetAt(new Vector3(0,7,0));
            Step(1,new PootleInputFrame{detachPressed=true});
            for(int i=0;i<90&&character.Mode!=PootleMode.RailSliding;i++)Step(1);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.RailSliding));Assert.That(character.Velocity.z,Is.LessThan(-1));
        }
        [Test]
        public void SeatedSlideRequiresContactAndEndsWhenLeavingSlope()
        {
            var ramp=LandingRamp(60);ResetAt(new Vector3(0,9,0));Step(5);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            for(int i=0;i<100&&character.Mode!=PootleMode.Sliding;i++)Step(1);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Sliding));
            ramp.SetActive(false);Step(1);Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
        }
        [Test]
        public void NarrowGapFailsafeRecoversButOrdinaryWallDoesNotTeleport()
        {
            ResetAt(new Vector3(-3,.02f,0));Step(3);
            var a=Box("Gap left",new Vector3(-.6f,2,0),new Vector3(.4f,4,4));
            Box("Gap right",new Vector3(.6f,2,0),new Vector3(.4f,4,4));
            character.transform.position=origin+new Vector3(0,.02f,0);Physics.SyncTransforms();
            Step(100,new PootleInputFrame{move=Vector2.up});
            Assert.That(character.GetComponent<PootleRecovery>().RecoveryCount,Is.EqualTo(1));
            Assert.That(character.Motor.CanOccupy(character.transform.position));
            int count=character.GetComponent<PootleRecovery>().RecoveryCount;
            ResetAt(new Vector3(-1.3f,.02f,0));Step(120,new PootleInputFrame{move=Vector2.right});
            Assert.That(character.GetComponent<PootleRecovery>().RecoveryCount,Is.EqualTo(count));
        }
    }
}
