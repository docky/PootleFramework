using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
namespace Pootle.Tests
{
    public sealed class CoastalSeatRidesPlayModeTests
    {
        PootleCharacter player;
        PootleSwing swing;
        PootleChairLift lift;
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/CoastalHike/CoastalHike.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor scene checks");yield break;
#endif
            player=Object.FindAnyObjectByType<PootleCharacter>();player.simulateAutomatically=false;player.outputCamera=null;
            var swings=Object.FindObjectsByType<PootleSwing>();var lifts=Object.FindObjectsByType<PootleChairLift>();
            Assert.That(swings.Length,Is.EqualTo(2));Assert.That(lifts.Length,Is.EqualTo(2));
            foreach(var s in swings){s.simulateAutomatically=false;s.ride.simulateAutomatically=false;}
            foreach(var l in lifts){l.simulateAutomatically=false;l.ride.simulateAutomatically=false;}
            swing=swings.OrderBy(s=>s.name).First();lift=lifts.OrderBy(l=>l.name).First();yield return null;
        }
        void TickPlayer(Vector2 move=default)
        {player.Tick(1f/60,new PootleInputFrame{move=move,moveIsDigital=true});Physics.SyncTransforms();}
        void BoardSwing()
        {
            player.Teleport(swing.transform.position+new Vector3(0,.08f,-1.5f),Quaternion.identity);
            for(int i=0;i<60;i++)TickPlayer();Assert.That(player.IsGrounded);
            Assert.That(swing.ride.Occupied,Is.False,"Standing nearby does not communicate a push");
            for(int i=0;i<150 && !swing.ride.Occupied;i++)TickPlayer(Vector2.up);
            Assert.That(swing.ride.Occupied,Is.True,$"Walk into swing from {player.transform.position}");
        }
        void BoardLift()
        {
            player.Teleport(lift.platform.position+lift.platform.forward*.75f-Vector3.up*.7f,Quaternion.identity);
            TickPlayer();Assert.That(lift.ride.Occupied,Is.True,"Contact catches without pressing an interaction button");
        }
        void SwingStep(float input)
        {swing.ride.TickControls(1f/60,input);swing.Step(1f/60);swing.ride.Present();Physics.SyncTransforms();}
        [UnityTest] public IEnumerator WalkingIntoSwingSeatsAndCameraStaysAtAuthoredPosition()
        {
            BoardSwing();Assert.That(player.enabled,Is.False);Assert.That(player.gameObject.activeSelf);
            var camera=Object.FindObjectsByType<PootleStationaryRideCamera>().Single(c=>c.passenger==swing.ride.passenger);
            var position=camera.transform.position;var rotation=camera.transform.rotation;
            for(int i=0;i<360;i++){SwingStep(Mathf.Sign(swing.AngularSpeed));camera.TickCamera();}
            Assert.That(Mathf.Abs(swing.AngleDegrees),Is.GreaterThan(2));
            Assert.That(camera.transform.position,Is.EqualTo(position));Assert.That(camera.transform.rotation,Is.EqualTo(rotation));
            Assert.That(camera.GetComponent<Unity.Cinemachine.CinemachineCamera>().Priority.Value,Is.GreaterThan(100));
            Assert.That(swing.ride.passenger.SeatWeight,Is.EqualTo(1));yield return null;
            var animator=player.GetComponentInChildren<Animator>();
            Assert.That(Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.Hips).position,swing.ride.passenger.seat.position),Is.LessThan(.05f));
        }
        [UnityTest] public IEnumerator TimedPumpingBuildsAmplitudeAndUnpumpedSwingDamps()
        {
            BoardSwing();float initialPeak=0;
            for(int i=0;i<240;i++){SwingStep(0);initialPeak=Mathf.Max(initialPeak,Mathf.Abs(swing.AngleDegrees));}
            float pumpedPeak=0;
            for(int i=0;i<600;i++){SwingStep(swing.AngularSpeed>=0?1:-1);pumpedPeak=Mathf.Max(pumpedPeak,Mathf.Abs(swing.AngleDegrees));}
            Assert.That(pumpedPeak,Is.GreaterThan(initialPeak+15));Assert.That(pumpedPeak,Is.LessThanOrEqualTo(swing.maximumAngle+.01f));
            for(int i=0;i<1500;i++)SwingStep(0);
            float finalPeak=0;for(int i=0;i<240;i++){SwingStep(0);finalPeak=Mathf.Max(finalPeak,Mathf.Abs(swing.AngleDegrees));}
            Assert.That(finalPeak,Is.LessThan(pumpedPeak*.3f));yield return null;
        }
        [UnityTest] public IEnumerator JumpActionDismountsSwingAndRestoresCameraAndControls()
        {
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                BoardSwing();for(int i=0;i<60;i++)SwingStep(1);
                swing.ride.simulateAutomatically=true;player.simulateAutomatically=true;
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});yield return null;yield return null;
                Assert.That(swing.ride.PumpInput,Is.GreaterThan(.5f));
                InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.down});yield return null;yield return null;
                Assert.That(swing.ride.PumpInput,Is.LessThan(-.5f));
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
                Assert.That(swing.ride.Occupied,Is.False,"Jump releases the seated player");Assert.That(player.enabled);Assert.That(player.input.enabled);
                Assert.That(player.Velocity.y,Is.GreaterThan(1));Assert.That(player.AirJumpsUsed,Is.Zero);Assert.That(player.Motor.CanOccupy(player.transform.position));
                var camera=Object.FindObjectsByType<PootleStationaryRideCamera>().Single(c=>c.passenger==swing.ride.passenger);camera.TickCamera();
                Assert.That(camera.GetComponent<Unity.Cinemachine.CinemachineCamera>().Priority.Value,Is.LessThan(0));
            }
            finally{InputSystem.RemoveDevice(pad);}
        }
        [UnityTest] public IEnumerator LiftCatchesInAirCarriesUprightAndJumpKeepsMomentumWithoutRegrab()
        {
            for(int i=0;i<450;i++)lift.Step(1f/60);BoardLift();
            for(int i=0;i<90;i++){lift.ride.TickControls(1f/60,0);lift.Step(1f/60);lift.ride.Present();}
            Assert.That(Vector3.Dot(lift.platform.up,Vector3.up),Is.GreaterThan(.999f));
            Assert.That(Vector3.Distance(player.transform.position,lift.ride.passenger.seat.position-lift.platform.up*.65f),Is.LessThan(.02f));
            Vector3 velocity=lift.ride.RideVelocity;
            Physics.SyncTransforms();Assert.That(lift.ride.TryRelease(),Is.True);Assert.That(player.enabled);
            Assert.That(Vector3.Dot(Vector3.ProjectOnPlane(player.Velocity,Vector3.up),Vector3.ProjectOnPlane(velocity,Vector3.up).normalized),Is.GreaterThan(1));
            Assert.That(lift.ride.TryBoard(player,default,1),Is.False);
            Assert.That(player.Motor.CanOccupy(player.transform.position));yield return null;
        }
        [UnityTest] public IEnumerator LiftTurnsAroundAtFarEndAndCompletesContinuousLoop()
        {
            Vector3 start=lift.platform.position,forward=lift.platform.forward;
            while(lift.Distance<lift.Length*.58f)lift.Step(.05f);
            Assert.That(Vector3.Dot(lift.platform.forward,forward),Is.LessThan(-.5f),"Return side faces back toward the beach");
            float before=lift.Distance;int guard=0;
            while(lift.Distance>=before && guard++<3000){before=lift.Distance;lift.Step(.05f);}
            Assert.That(guard,Is.LessThan(3000));Assert.That(Vector3.Distance(lift.platform.position,start),Is.LessThan(.3f));
            Assert.That(Vector3.Dot(lift.platform.up,Vector3.up),Is.GreaterThan(.999f));yield return null;
        }
        [UnityTest] public IEnumerator BlockedDismountRetainsSeatAndDisableAlwaysRestoresPlayer()
        {
            BoardSwing();for(int i=0;i<30;i++)SwingStep(0);
            var obstacle=GameObject.CreatePrimitive(PrimitiveType.Cube);obstacle.transform.position=swing.ride.passenger.seat.position+Vector3.up;obstacle.transform.localScale=new Vector3(5,5,5);Physics.SyncTransforms();
            Assert.That(swing.ride.TryRelease(),Is.False);Assert.That(swing.ride.ReleaseBlocked);Assert.That(swing.ride.Occupied);
            Object.Destroy(obstacle);yield return null;
            swing.enabled=false;Assert.That(player.enabled);Assert.That(player.input.enabled);Assert.That(swing.ride.Occupied,Is.False);
            Assert.That(player.GetComponentInChildren<PootleViewTilt>().enabled);
        }
        [UnityTest] public IEnumerator LiftRouteAndBeachApproachClearExistingObstacles()
        {
            for(int i=0;i<400;i++)
            {
                Vector3 p=lift.path.EvaluatePosition(i/400f);
                foreach(var hit in Physics.OverlapSphere(p+Vector3.up*.95f,.65f,~0,QueryTriggerInteraction.Ignore))
                    Assert.That(hit.GetComponentInParent<PootleRideSeat>(),Is.Not.Null,$"Chair lift passenger intersects {hit.name} at {p}");
            }
            player.Teleport(new Vector3(-9,.08f,-11),Quaternion.identity);
            for(int i=0;i<60;i++)TickPlayer();
            for(int i=0;i<300 && player.transform.position.z>-18;i++)TickPlayer(Vector2.down*.6f);
            Assert.That(player.transform.position.z,Is.LessThan(-18));Assert.That(player.IsGrounded);
            yield return null;
        }
    }
}
