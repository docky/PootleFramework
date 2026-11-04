using System.Collections;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        private void StartSlideRide()
        {
            character.simulateAutomatically=false;
            character.Teleport(new Vector3(74,18.08f,12.4f),Quaternion.identity);
        }
        private void RideTick(float dt=1f/60,PootleInputFrame input=default)
        {Physics.SyncTransforms();character.Tick(dt,input);}
        [UnityTest]
        public IEnumerator SlideRide_DefaultRunHasMomentumWithoutBankModeChatter()
        {
            StartSlideRide();int changes=0,airFrames=0;float peak=0;var previous=PootleMode.Sliding;
            for(int i=0;i<360;i++)
            {
                RideTick();peak=Mathf.Max(peak,character.Velocity.magnitude);
                if(i>10 && character.transform.position.y>1)
                {
                    if(character.Mode!=previous)changes++;
                    if(character.Mode==PootleMode.Airborne)airFrames++;
                    Assert.That(character.Mode,Is.Not.EqualTo(PootleMode.Grounded),"Standing on a bank at tick "+i);
                }
                previous=character.Mode;
                if(character.transform.position.y<.4f && character.transform.position.x<63)break;
            }
            Assert.That(peak,Is.GreaterThan(20));Assert.That(changes,Is.LessThanOrEqualTo(2));
            Assert.That(airFrames,Is.LessThan(8),"The normal ride should stay in the trough");
            Assert.That(character.transform.position.x,Is.LessThan(63));
            Assert.That(character.Velocity.magnitude,Is.GreaterThan(12),"Keep momentum into the runout");
            yield return null;
        }
        [UnityTest]
        public IEnumerator SlideRide_ThirtyAndOneTwentyFpsFollowTheSamePath()
        {
            StartSlideRide();for(int i=0;i<60;i++)RideTick(1f/30);
            Vector3 position=character.transform.position,velocity=character.Velocity;
            StartSlideRide();for(int i=0;i<240;i++)RideTick(1f/120);
            Assert.That(Vector3.Distance(position,character.transform.position),Is.LessThan(.3f));
            Assert.That(Vector3.Distance(velocity,character.Velocity),Is.LessThan(.6f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator SlideRide_FastOutwardMomentumClimbsBankAndLeavesRim()
        {
            foreach(float dt in new[]{1f/30,1f/60,1f/120})
            {
            StartSlideRide();for(int i=0;i<70;i++)RideTick();
            Vector3 radial=Vector3.ProjectOnPlane(character.transform.position-new Vector3(66,0,12),Vector3.up).normalized;
            character.ApplyImpulse(radial*38+Vector3.up*3);
            bool launched=false;float upward=0;int changes=0;var previous=character.Mode;
            for(int i=0;i<50;i++)
            {
                RideTick(dt);upward=Mathf.Max(upward,character.Velocity.y);
                if(character.Mode!=previous)changes++;previous=character.Mode;
                float radius=Vector3.ProjectOnPlane(character.transform.position-new Vector3(66,0,12),Vector3.up).magnitude;
                if(radius>10.8f && character.Mode==PootleMode.Airborne && character.Velocity.y>2)
                {launched=true;Assert.That(Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude,Is.GreaterThan(10));break;}
            }
            Assert.That(launched,Is.True,"High speed should leave the lip, not clamp to it");
            Assert.That(upward,Is.GreaterThan(8));Assert.That(changes,Is.LessThanOrEqualTo(3),"Mode chatter at dt "+dt);
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator SlideRide_BackInputBrakesAndJumpRetainsSpeed()
        {
            StartSlideRide();var camera=character.outputCamera;character.outputCamera=null;
            try
            {
                for(int i=0;i<120;i++)RideTick();float speed=character.Velocity.magnitude;
                for(int i=0;i<45;i++)
                {
                    Vector3 reverse=-Vector3.ProjectOnPlane(character.Velocity,Vector3.up).normalized;
                    RideTick(1f/60,new PootleInputFrame{move=new Vector2(reverse.x,reverse.z)});
                }
                Assert.That(character.Velocity.magnitude,Is.LessThan(speed-2));
                float planar=Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude;
                RideTick(1f/60,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
                Assert.That(character.Velocity.y,Is.GreaterThan(15));Assert.That(character.SlideRideActive,Is.False);
                Assert.That(Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude,Is.GreaterThan(planar*.9f));
            }
            finally{character.outputCamera=camera;}
            yield return null;
        }
        [UnityTest]
        public IEnumerator SlideRide_CameraSevenTracksTravelAndAllowsManualLook()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();var styles=Object.FindAnyObjectByType<PootleCameraStyles>();
            var orbit=Object.FindAnyObjectByType<CinemachineOrbitalFollow>();
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit7));yield return null;yield return null;
                Assert.That(styles.Style,Is.EqualTo(PootleCameraStyle.SlideFollow));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());
                character.Teleport(new Vector3(74,18.08f,12.4f),Quaternion.identity);
                yield return new WaitForSeconds(1.6f);
                Assert.That(styles.SlideFollowActive);Assert.That(orbit.Radius,Is.InRange(7.9f,10.1f));
                float heading=Mathf.Atan2(character.Velocity.x,character.Velocity.z)*Mathf.Rad2Deg;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(heading,orbit.HorizontalAxis.Value)),Is.LessThan(45));
                float yaw=orbit.HorizontalAxis.Value;
                InputSystem.QueueStateEvent(gamepad,new GamepadState{rightStick=Vector2.right});
                yield return new WaitForSeconds(.25f);
                Assert.That(Mathf.DeltaAngle(yaw,orbit.HorizontalAxis.Value),Is.GreaterThan(15));
                InputSystem.QueueStateEvent(gamepad,new GamepadState());
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit1));yield return null;
                character.Teleport(new Vector3(0,.03f,-4),Quaternion.identity);
                yield return new WaitForSeconds(1.3f);Assert.That(styles.SlideFollowActive,Is.False);
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }
    }
}
