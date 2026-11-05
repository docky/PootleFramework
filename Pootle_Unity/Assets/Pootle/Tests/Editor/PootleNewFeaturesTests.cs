using NUnit.Framework;
using UnityEngine;
namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        private PootleCarryable CarryCube(Vector3 offset)
        {
            var go=Box("Carry test cube",offset,Vector3.one*.8f);go.AddComponent<Rigidbody>();
            var item=go.AddComponent<PootleCarryable>();Physics.SyncTransforms();return item;
        }
        [Test]
        public void Features_DoubleJumpAcceptsExactlyOneAirJumpAndResetsOnLanding()
        {
            profile.additionalAirJumps=1;int jumps=0;character.Jumped+=()=>jumps++;
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Step(12,new PootleInputFrame{jumpHeld=true});
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Assert.That(character.AirJumpsUsed,Is.EqualTo(1));
            Assert.That(character.Velocity.y,Is.GreaterThan(18));Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(jumps,Is.EqualTo(2));Step(180);Assert.That(character.IsGrounded);Assert.That(character.AirJumpsUsed,Is.Zero);
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Assert.That(jumps,Is.EqualTo(3));
        }
        [Test]
        public void Features_DoubleJumpExitsGlidingAndCannotBeRepeated()
        {
            profile.additionalAirJumps=1;ResetAt(new Vector3(0,9,0));Step(2,new PootleInputFrame{glide=true});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Gliding));
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true,glide=true});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));Assert.That(character.AirJumpsUsed,Is.EqualTo(1));
            Assert.That(character.Velocity.y,Is.GreaterThan(18));
        }
        [Test]
        public void Features_SlopeModifiersMakeDownhillFasterThanUphillWithoutIdleCreep()
        {
            profile.applySlopeSpeed=true;LandingRamp(25);ResetAt(new Vector3(0,8,0));Step(90);
            var start=character.transform.position;Step(30);Assert.That(Vector3.Distance(start,character.transform.position),Is.LessThan(.03f));
            Step(30,new PootleInputFrame{move=Vector2.up});float uphill=Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude;
            ResetAt(start-origin);Step(2);Step(30,new PootleInputFrame{move=Vector2.down});
            Assert.That(Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude,Is.GreaterThan(uphill+1));
            Assert.That(character.Velocity.magnitude,Is.LessThanOrEqualTo(profile.slopeMaximumSpeed+.1f));
        }
        [Test]
        public void Features_RollTogglePersistsAndCoastsUntilExplicitlyCancelled()
        {
            Step(1,new PootleInputFrame{rollPressed=true});Assert.That(character.IsRolling);Assert.That(character.Motor.Height,Is.EqualTo(1).Within(.01f));
            Step(60,new PootleInputFrame{move=Vector2.up});var before=character.transform.position;Step(30);
            Assert.That(character.IsRolling);Assert.That(character.transform.position.z-before.z,Is.GreaterThan(3));
            Step(1,new PootleInputFrame{rollPressed=true});Assert.That(character.IsRolling,Is.False);Assert.That(character.Motor.Height,Is.EqualTo(2).Within(.01f));
        }
        [Test]
        public void Features_UnrollUnderLowCeilingStaysBallAndCanExitTunnel()
        {
            Step(1,new PootleInputFrame{rollPressed=true});Box("Low roof",new Vector3(0,1.3f,0),new Vector3(4,.4f,4));
            Step(1,new PootleInputFrame{rollPressed=true});Assert.That(character.IsRolling);Assert.That(character.LastUnrollBlocked);Assert.That(character.Motor.CanOccupy(character.transform.position));
            Step(45,new PootleInputFrame{move=Vector2.right});Step(1,new PootleInputFrame{rollPressed=true});
            Assert.That(character.IsRolling,Is.False);Assert.That(character.Motor.CanOccupy(character.transform.position));
        }
        [Test]
        public void Features_BallRespondsToSlopeWithoutBecomingSeatedSlide()
        {
            LandingRamp(60);ResetAt(new Vector3(0,9,0));Step(1,new PootleInputFrame{rollPressed=true});
            for(int i=0;i<100 && !character.IsGrounded;i++)Step(1);
            Assert.That(character.IsRolling);Assert.That(character.IsGrounded);var start=character.transform.position;
            Step(20);Assert.That(character.Mode,Is.EqualTo(PootleMode.Rolling));Assert.That(character.transform.position.z,Is.LessThan(start.z-.3f));
        }
        [Test]
        public void Features_RollingSupportsDoubleJumpAndTeleportRestoresCapsule()
        {
            profile.additionalAirJumps=1;Step(1,new PootleInputFrame{rollPressed=true});
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Step(12,new PootleInputFrame{jumpHeld=true});
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Assert.That(character.IsRolling);Assert.That(character.AirJumpsUsed,Is.EqualTo(1));
            Assert.That(character.Velocity.y,Is.GreaterThan(18));ResetAt(Vector3.zero);
            Assert.That(character.IsRolling,Is.False);Assert.That(character.Motor.Height,Is.EqualTo(2));
        }
        [Test]
        public void Features_BallRidesMovingPlatformAndInheritsVelocityOnJump()
        {
            var deck=Box("Ball moving deck",new Vector3(0,.25f,0),new Vector3(4,.5f,4));deck.AddComponent<PootleMovingSurface>();
            ResetAt(new Vector3(0,.52f,0));Step(3);Step(1,new PootleInputFrame{rollPressed=true});
            for(int i=0;i<30;i++){deck.transform.position+=Vector3.right*.03f;Step(1);}
            Assert.That(character.IsRolling);Assert.That(character.IsGrounded);
            Assert.That(character.transform.position.x-deck.transform.position.x,Is.Zero.Within(.03f));
            deck.transform.position+=Vector3.right*.03f;Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(character.IsGrounded,Is.False);Assert.That(character.Velocity.x,Is.GreaterThan(1));
            Assert.That(character.GetComponent<PootlePlatformRider>().Surface,Is.Null);
        }
        [Test]
        public void Features_CarriedBoxOverlapsWallWithoutBlockingFollowAndAllowsRepickup()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            var carry=character.GetComponent<PootleCarrying>();
            var wall=Box("Carry wall",new Vector3(0,1.5f,3),new Vector3(5,3,.3f));
            Step(180,new PootleInputFrame{move=Vector2.up});
            Assert.That(carry.Held,Is.SameAs(item));Assert.That(item.IsHeld);Assert.That(item.Shape.isTrigger);
            Assert.That(item.Shape.bounds.max.z,Is.GreaterThan(wall.GetComponent<Collider>().bounds.min.z),"Held box is allowed to overlap obstacles");
            Vector3 expected=character.transform.position+Vector3.up*character.Motor.Height*.65f+character.Facing*(character.Motor.Radius+item.HalfSize.z+.12f);
            Assert.That(Vector3.Distance(item.Shape.bounds.center,expected),Is.LessThan(.025f),"Box stays attached to the hands at the wall");
            Step(30,new PootleInputFrame{move=Vector2.down});Step(25);
            Step(1,new PootleInputFrame{carryPressed=true});Assert.That(carry.Held,Is.Null);
            Step(1,new PootleInputFrame{carryPressed=true});Assert.That(carry.Held,Is.SameAs(item));
        }
        [TestCase(0f)]
        [TestCase(.4f)]
        [TestCase(.8f)]
        public void Features_CarryTurnsAroundAdjustableForwardPivot(float offset)
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.rotationPivotForward,Is.EqualTo(.4f));
            carry.rotationPivotForward=offset;Vector3 pivot=character.transform.position+character.Facing*offset;
            Assert.That(carry.Turn(character,Vector3.right));
            Assert.That(Vector3.Distance(pivot,character.transform.position+character.Facing*offset),Is.LessThan(.002f));
            Assert.That(Vector3.Dot(character.Facing,Vector3.right),Is.GreaterThan(.999f));Assert.That(carry.Held,Is.SameAs(item));
        }
        [Test]
        public void Features_CarryTurnIgnoresBoxObstructionsButKeepsPlayerClear()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            var carry=character.GetComponent<PootleCarrying>();
            var wall=Box("Carry turn wall",new Vector3(.9f,1.5f,1),new Vector3(.2f,3,3));Physics.SyncTransforms();
            Assert.That(carry.Turn(character,Vector3.right),Is.True);
            Assert.That(carry.Held,Is.SameAs(item));Assert.That(item.IsHeld);
            Assert.That(Vector3.Dot(character.Facing,Vector3.right),Is.GreaterThan(.999f));
            bool overlap=Physics.ComputePenetration(item.Shape,item.transform.position,item.transform.rotation,
                wall.GetComponent<Collider>(),wall.transform.position,wall.transform.rotation,out _,out float depth);
            Assert.That(overlap && depth>.01f,Is.True,"Held geometry is non-blocking");
            Assert.That(character.Motor.CanOccupy(character.transform.position),"The player still respects obstacles");
        }
        [Test]
        public void Features_CarryCubeTouchingWallCanBeLiftedAwayAndPickedUpAgain()
        {
            var item=CarryCube(new Vector3(0,.4f,1.1f));
            Box("Flush pickup wall",new Vector3(0,1.5f,1.6f),new Vector3(4,3,.2f));Physics.SyncTransforms();
            Step(1,new PootleInputFrame{carryPressed=true});var carry=character.GetComponent<PootleCarrying>();
            Assert.That(carry.Held,Is.SameAs(item));
            Step(20,new PootleInputFrame{move=Vector2.down});Step(25);
            Step(1,new PootleInputFrame{carryPressed=true});Assert.That(carry.Held,Is.Null);
            Step(1,new PootleInputFrame{carryPressed=true});Assert.That(carry.Held,Is.SameAs(item));
        }
        [Test]
        public void Features_CarryJumpDoesNotCatchBoxOnOverhangingObstacle()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            Box("Box-only overhang",new Vector3(0,2.3f,1.4f),new Vector3(3,.3f,1.2f));
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Step(25,new PootleInputFrame{jumpHeld=true});
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.Held,Is.SameAs(item));
            Assert.That(character.transform.position.y-origin.y,Is.GreaterThan(3),"The box cannot act as a jump ceiling");
            Assert.That(item.transform.TransformPoint(item.Shape.center).y-character.transform.position.y,Is.EqualTo(character.Motor.Height*.65f).Within(.01f));
            Step(120);Assert.That(character.IsGrounded);Assert.That(character.transform.position.y-origin.y,Is.LessThan(.1f));
            Assert.That(carry.Held,Is.SameAs(item));
        }
        [Test]
        public void Features_CarryJumpThroughOneWayMatchesUnladenJumpAndBoxFollows()
        {
            var platform=Box("Carry one way",new Vector3(0,1.5f,0),new Vector3(4,.2f,4));platform.AddComponent<PootleOneWayPlatform>();
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});float barePeak=0;
            for(int i=0;i<100;i++){Step(1,new PootleInputFrame{jumpHeld=true});barePeak=Mathf.Max(barePeak,character.transform.position.y-origin.y);}
            ResetAt(new Vector3(0,.02f,0));var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.Held,Is.SameAs(item));
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});float carriedPeak=0;
            for(int i=0;i<100;i++)
            {
                Step(1,new PootleInputFrame{jumpHeld=true});carriedPeak=Mathf.Max(carriedPeak,character.transform.position.y-origin.y);
                Assert.That(item.transform.TransformPoint(item.Shape.center).y-character.transform.position.y,Is.EqualTo(character.Motor.Height*.65f).Within(.01f));
            }
            Assert.That(carriedPeak,Is.EqualTo(barePeak).Within(.03f));Assert.That(character.IsGrounded);Assert.That(carry.Held,Is.SameAs(item));
        }
        [Test]
        public void Features_DropBackstepsToMakeRoomThenRestoresSolidBox()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            var wall=Box("Drop wall",new Vector3(0,1.5f,1.3f),new Vector3(4,3,.2f));Physics.SyncTransforms();
            var carry=character.GetComponent<PootleCarrying>();Vector3 before=character.transform.position;
            Assert.That(carry.TryDrop(character));Assert.That(carry.LastDrop,Is.EqualTo(PootleDropResult.InFront));
            Assert.That(before.z-character.transform.position.z,Is.InRange(.2f,.601f));Assert.That(item.Shape.isTrigger,Is.False);Assert.That(item.IsHeld,Is.False);
            Assert.That(item.Shape.bounds.max.z,Is.LessThan(wall.GetComponent<Collider>().bounds.min.z+.005f));
            Assert.That(character.Motor.CanOccupy(character.transform.position));
        }
        [Test]
        public void Features_DropWithNoRoomBehindKeepsGhostBoxHeld()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            Box("Drop front wall",new Vector3(0,1.5f,1.3f),new Vector3(4,3,.2f));
            Box("Drop rear wall",new Vector3(0,1.5f,-.7f),new Vector3(4,3,.2f));Physics.SyncTransforms();
            var carry=character.GetComponent<PootleCarrying>();Vector3 before=character.transform.position;
            Assert.That(carry.TryDrop(character),Is.False);Assert.That(carry.Held,Is.SameAs(item));Assert.That(item.Shape.isTrigger);
            Assert.That(character.transform.position,Is.EqualTo(before));
        }
        [Test]
        public void Features_AirDropEscapesEmbeddedHeldPoseWithoutCrossingWall()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            character.Motor.Move(Vector3.up*3,false);character.GetComponent<PootleCarrying>().Follow(character);Step(1);
            Box("Embedded held wall",new Vector3(0,3,1.2f),new Vector3(4,6,.3f));Physics.SyncTransforms();
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.TryDrop(character));Assert.That(carry.LastDrop,Is.EqualTo(PootleDropResult.Beneath));
            Assert.That(item.Shape.bounds.max.y,Is.LessThanOrEqualTo(character.transform.position.y-.03f));Assert.That(item.IsHeld,Is.False);
        }
        [Test]
        public void Features_DropCannotTeleportGhostBoxThroughSolidWallOrFloor()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            Box("Between player and drop",new Vector3(0,1.5f,.7f),new Vector3(4,3,.2f));Physics.SyncTransforms();
            var carry=character.GetComponent<PootleCarrying>();carry.maximumDropBackstep=0;
            Assert.That(carry.TryDrop(character),Is.False);Assert.That(carry.Held,Is.SameAs(item));Assert.That(item.Shape.isTrigger);
        }
        [Test]
        public void Features_PickingUpOwnSupportCannotCarryPlayerWithGhostBox()
        {
            var item=CarryCube(new Vector3(0,.4f,0));item.gameObject.AddComponent<PootleMovingSurface>();
            ResetAt(new Vector3(0,.82f,0));Step(3);Assert.That(character.Support.collider,Is.SameAs(item.Shape));
            Step(1,new PootleInputFrame{carryPressed=true});float height=character.transform.position.y;
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.Held,Is.SameAs(item));
            Step(1);Assert.That(character.transform.position.y,Is.LessThan(height+.05f),"Held box cannot carry its owner upwards");
            Step(90);Assert.That(character.IsGrounded);Assert.That(character.transform.position.y-origin.y,Is.LessThan(.1f));
        }
        [Test]
        public void Features_BlockedDropCannotPassDownThroughThinOneWayFloor()
        {
            var platform=Box("Thin release floor",new Vector3(0,1.6f,0),new Vector3(4,.02f,4));platform.AddComponent<PootleOneWayPlatform>();
            ResetAt(new Vector3(0,1.63f,0));Step(3);
            var item=CarryCube(new Vector3(0,2.03f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.Held,Is.SameAs(item));carry.maximumDropBackstep=0;
            Box("Release front wall",new Vector3(0,2.5f,1.3f),new Vector3(4,3,.2f));Physics.SyncTransforms();
            Assert.That(carry.TryDrop(character),Is.False);Assert.That(carry.Held,Is.SameAs(item));
        }
        [Test]
        public void Features_FrontDropOnSlopeClearsTheWholeBox()
        {
            floor.SetActive(false);
            var ramp=Box("Carry drop ramp",new Vector3(0,-.5f,0),new Vector3(20,1,20));ramp.transform.rotation=Quaternion.Euler(30,0,0);
            character.Teleport(origin+Vector3.up,Quaternion.Euler(0,180,0));Step(90);Assert.That(character.IsGrounded);
            var item=CarryCube(new Vector3(0,1.5f,-1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.Held,Is.SameAs(item));
            Assert.That(carry.TryDrop(character));Assert.That(carry.LastDrop,Is.EqualTo(PootleDropResult.InFront));
            bool overlap=Physics.ComputePenetration(item.Shape,item.transform.position,item.transform.rotation,ramp.GetComponent<Collider>(),ramp.transform.position,ramp.transform.rotation,out _,out float depth);
            Assert.That(!overlap || depth<.005f,Is.True,"Slope placement accounts for the uphill bottom corner");
        }
        [Test]
        public void Features_CarryStandingDropPlacesInFrontAndRestoresPhysics()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.Held,Is.SameAs(item));Assert.That(item.Body.isKinematic);Assert.That(item.Shape.isTrigger);
            Step(1,new PootleInputFrame{carryPressed=true});Assert.That(carry.Held,Is.Null);Assert.That(carry.LastDrop,Is.EqualTo(PootleDropResult.InFront));
            Assert.That(item.transform.position.z-origin.z,Is.GreaterThan(.9f));Assert.That(item.Shape.bounds.min.y,Is.InRange(0,.1f));
            Assert.That(item.Body.isKinematic,Is.False);Assert.That(item.Shape.isTrigger,Is.False);Assert.That(item.Body.useGravity);
        }
        [Test]
        public void Features_AirborneDropPlacesObjectBeneathFeet()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true});Step(8,new PootleInputFrame{jumpHeld=true});
            Step(1,new PootleInputFrame{carryPressed=true,jumpHeld=true});
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.LastDrop,Is.EqualTo(PootleDropResult.Beneath));
            Assert.That(item.Shape.bounds.max.y,Is.EqualTo(character.transform.position.y-.04f).Within(.01f));
            Assert.That(Vector3.ProjectOnPlane(item.Shape.bounds.center-character.transform.position,Vector3.up).magnitude,Is.LessThan(.01f));
        }
        [Test]
        public void Features_InsufficientUnderfootSpaceFallsBackToFrontDrop()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            Step(1,new PootleInputFrame{jumpPressed=true,jumpHeld=true,carryPressed=true});
            Assert.That(character.IsGrounded,Is.False);Assert.That(character.GetComponent<PootleCarrying>().LastDrop,Is.EqualTo(PootleDropResult.InFront));
            Assert.That(item.Shape.bounds.min.y,Is.GreaterThan(0));Assert.That(item.transform.position.z-origin.z,Is.GreaterThan(.9f));
        }
        [Test]
        public void Features_BlockedFrontDropKeepsObjectHeldAndResetReleasesIt()
        {
            var item=CarryCube(new Vector3(0,.42f,1.2f));Step(1,new PootleInputFrame{carryPressed=true});
            character.GetComponent<PootleCarrying>().maximumDropBackstep=0;
            Box("Blocked front",new Vector3(0,1,1.6f),new Vector3(3,2,.35f));
            Step(1,new PootleInputFrame{carryPressed=true});var carry=character.GetComponent<PootleCarrying>();
            Assert.That(carry.LastDrop,Is.EqualTo(PootleDropResult.Blocked));Assert.That(carry.Held,Is.SameAs(item));
            ResetAt(new Vector3(5,.02f,0));Assert.That(carry.Held,Is.Null);Assert.That(item.Body.isKinematic,Is.False);
        }
        [Test]
        public void Features_CannotPickThroughWallOrRollWhileCarrying()
        {
            var item=CarryCube(new Vector3(0,.42f,1.8f));var wall=Box("Pickup obstruction",new Vector3(0,1,1.3f),new Vector3(3,2,.2f));
            Step(1,new PootleInputFrame{carryPressed=true});Assert.That(character.GetComponent<PootleCarrying>().Held,Is.Null);
            wall.SetActive(false);item.transform.position=origin+new Vector3(0,.42f,1.2f);Step(1,new PootleInputFrame{carryPressed=true});
            Assert.That(character.GetComponent<PootleCarrying>().Held,Is.SameAs(item));Step(1,new PootleInputFrame{rollPressed=true});Assert.That(character.IsRolling,Is.False);
        }
    }
}
