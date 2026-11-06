using System.Collections;
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
    public sealed class CoastalCoasterPlayModeTests
    {
        PootleCharacter player;
        PootleRollercoaster coaster;
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/CoastalHike/CoastalHike.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor scene checks"); yield break;
#endif
            player = Object.FindAnyObjectByType<PootleCharacter>(); coaster = Object.FindAnyObjectByType<PootleRollercoaster>();
            Assert.That(coaster, Is.Not.Null);
            player.simulateAutomatically = false; player.outputCamera = null; coaster.simulateAutomatically = false;
            yield return null;
        }
        void Settle(Vector3 position)
        { player.Teleport(position, Quaternion.identity); for (int i = 0; i < 60; i++) { player.Tick(1f/60, default); Physics.SyncTransforms(); } Assert.That(player.IsGrounded); }
        void Steps(float seconds, bool jump = false)
        { for (int i = 0; i < Mathf.CeilToInt(seconds * 60); i++) coaster.Step(1f/60, jump && i == 0); }
        void Board()
        { Settle(coaster.stations[0].exitPoint.position); Assert.That(coaster.RequestBoard(coaster.stations[0], player), Is.True); }
        [UnityTest] public IEnumerator FixedRideCameraKeepsWorldAngleThroughTurnsAndSharesExplorationFraming()
        {
            var camera=coaster.GetComponentInChildren<PootleMachineCamera>();
            Assert.That(camera.fixedThirdPerson,Is.True);Assert.That(camera.explorationPolicy,Is.Not.Null);
            Board();camera.TickCamera(1f/60);
            var rig=camera.GetComponent<Unity.Cinemachine.CinemachineCamera>();
            Quaternion angle=camera.transform.rotation;Vector3 before=camera.transform.position;
            coaster.cart.rotation=Quaternion.Euler(35,140,15);camera.TickCamera(.1f);
            Assert.That(Quaternion.Angle(camera.transform.rotation,angle),Is.LessThan(.05f),"Track pitch, yaw and roll must not rotate the fixed view");
            Assert.That(Vector3.Distance(before,camera.transform.position),Is.LessThan(.001f));
            Vector3 movement=new Vector3(4,2,-3);coaster.cart.position+=movement;
            for(int i=0;i<120;i++)camera.TickCamera(1f/60);
            Assert.That(Vector3.Distance(camera.transform.position,before+movement),Is.LessThan(.01f));
            Assert.That(rig.Lens.FieldOfView,Is.EqualTo(camera.explorationPolicy.fieldOfView).Within(.01f));
            Assert.That(Vector3.Distance(camera.transform.position,coaster.cart.position+Vector3.up*camera.focusHeight),Is.EqualTo(camera.EffectiveFixedDistance).Within(.01f));
            yield return null;
        }
        [UnityTest] public IEnumerator RideCameraCanBlendBackToChaseAndReleasesOnExit()
        {
            var camera=coaster.GetComponentInChildren<PootleMachineCamera>();Board();camera.TickCamera(.02f);
            var rig=camera.GetComponent<Unity.Cinemachine.CinemachineCamera>();
            Vector3 before=camera.transform.position;Quaternion angle=camera.transform.rotation;
            camera.fixedThirdPerson=false;camera.TickCamera(1f/60);
            Assert.That(Vector3.Distance(before,camera.transform.position),Is.LessThan(2),"Switching modes blends rather than cutting");
            for(int i=0;i<180;i++)camera.TickCamera(1f/60);
            Assert.That(Vector3.Distance(camera.transform.position,coaster.cart.position+coaster.cart.rotation*camera.offset),Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(camera.transform.rotation,angle),Is.GreaterThan(10));
            Assert.That(rig.Lens.FieldOfView,Is.EqualTo(camera.chaseFieldOfView).Within(.01f));
            camera.fixedThirdPerson=true;for(int i=0;i<180;i++)camera.TickCamera(1f/60);
            Assert.That(Quaternion.Angle(camera.transform.rotation,Quaternion.Euler(camera.fixedPitchYaw.x,camera.fixedPitchYaw.y,0)),Is.LessThan(.05f));
            coaster.Abort();camera.TickCamera(.02f);Assert.That(rig.Priority.Value,Is.LessThan(0));
            Assert.That(player.enabled);yield return null;
        }
        [UnityTest] public IEnumerator GateRequiresPushThenBoardsWithoutInteractionButton()
        {
            var gate = coaster.stations[0].gate;
            Settle(gate.transform.TransformPoint(new Vector3(0,.08f,-1.05f)));
            for(int i=0;i<120 && player.enabled;i++)
            { Vector3 d=gate.transform.forward;player.Tick(1f/60,new PootleInputFrame{move=new Vector2(d.x,d.z),moveIsDigital=true});Physics.SyncTransforms(); }
            Assert.That(gate.PressCount, Is.EqualTo(1)); Assert.That(coaster.State, Is.EqualTo(PootleCoasterState.Boarding));
            Assert.That(player.enabled, Is.False); Assert.That(player.gameObject.activeSelf); Assert.That(player.GetComponentInChildren<Animator>().enabled);
            Steps(1.7f);Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.Countdown));Assert.That(coaster.passenger.SeatWeight,Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator SpringButtonAcknowledgesOnceAndPushesBack()
        {
            var gate=GameObject.Find("Spring-back intent button").GetComponent<PootleIntentGate>();
            Settle(gate.transform.TransformPoint(new Vector3(0,.08f,-.95f)));
            Assert.That(gate.Sample(player,-gate.transform.forward,1),Is.False);
            Assert.That(gate.Sample(player,gate.transform.forward,.1f),Is.False);
            Assert.That(gate.Sample(player,gate.transform.forward,.3f),Is.True);
            Assert.That(Vector3.Dot(player.Velocity,gate.transform.forward),Is.LessThan(-2));
            for(int i=0;i<60;i++)gate.Sample(player,gate.transform.forward,.1f);
            Assert.That(gate.PressCount,Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator CountdownThenJumpStopsAtSummitAndRestoresControl()
        {
            Board();Steps(1.7f);float parked=coaster.Distance;
            Steps(.1f);Assert.That(coaster.CountdownLightCount,Is.EqualTo(1));
            Steps(1);Assert.That(coaster.CountdownLightCount,Is.EqualTo(2));
            Steps(1);Assert.That(coaster.CountdownLightCount,Is.EqualTo(3));Assert.That(coaster.Distance,Is.EqualTo(parked));
            Steps(1);Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.Riding));
            coaster.RequestStop();Assert.That(coaster.RequestedStation,Is.EqualTo(coaster.stations[1]));
            Steps(80);
            Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.Parked));Assert.That(coaster.CurrentStation,Is.EqualTo(coaster.stations[1]));
            Assert.That(coaster.passenger.Occupied,Is.False);Assert.That(player.enabled);Assert.That(player.input.enabled);
            Assert.That(Vector3.Distance(player.transform.position,coaster.stations[1].exitPoint.position),Is.LessThan(.01f));
            Assert.That(player.Motor.CanOccupy(player.transform.position)); yield return null;
        }
        [UnityTest] public IEnumerator NoJumpKeepsRidingAndCallBringsEmptyCartToOtherStation()
        {
            Assert.That(coaster.RequestBoard(coaster.stations[1],player),Is.False);Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.Calling));
            Steps(80);Assert.That(coaster.CurrentStation,Is.EqualTo(coaster.stations[1]));Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.Parked));
            Settle(coaster.stations[1].exitPoint.position);Assert.That(coaster.RequestBoard(coaster.stations[1],player));Steps(80);
            Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.Riding));Assert.That(coaster.passenger.Occupied);Assert.That(coaster.StopRequested,Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator BlockedExitWaitsAndDisableReturnsRiderSafely()
        {
            Board();Vector3 entry=coaster.stations[0].exitPoint.position;Steps(5);coaster.RequestStop();
            var block=GameObject.CreatePrimitive(PrimitiveType.Cube);block.transform.position=coaster.stations[1].exitPoint.position+Vector3.up;block.transform.localScale=new Vector3(2,3,2);Physics.SyncTransforms();
            Steps(80);Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.WaitingForExit));Assert.That(player.enabled,Is.False);
            Object.Destroy(block);yield return null;Physics.SyncTransforms();Steps(2);
            Assert.That(player.enabled);Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.Parked));
            Assert.That(coaster.RequestBoard(coaster.stations[1],player));Steps(8);coaster.enabled=false;
            Assert.That(player.enabled);Assert.That(player.GetComponentInChildren<PootleViewTilt>().enabled);Assert.That(coaster.passenger.Occupied,Is.False);
            Assert.That(player.Motor.CanOccupy(player.transform.position));
            coaster.enabled=true;yield return null;
            Assert.That(coaster.Length,Is.GreaterThan(300));Assert.That(coaster.State,Is.EqualTo(PootleCoasterState.Parked));
        }
        [UnityTest] public IEnumerator JumpActionQueuesStopWhileNormalPlayerInputIsSuspended()
        {
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                Board();coaster.simulateAutomatically=true;
                yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));
                yield return null;yield return null;
                Assert.That(coaster.StopRequested);Assert.That(coaster.RequestedStation,Is.EqualTo(coaster.stations[1]));
                Assert.That(player.enabled,Is.False);Assert.That(player.gameObject.activeSelf);
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                coaster.enabled=false;Assert.That(player.enabled);Assert.That(player.input.enabled);
            }
            finally { InputSystem.RemoveDevice(pad); }
        }
        [UnityTest] public IEnumerator TrackUsesSplineMesherAndClearsExistingTerrain()
        {
            int meshers=0;
            foreach(var component in coaster.GetComponentsInChildren<MonoBehaviour>()) if(component.GetType().Name=="SplineCurveMesher")meshers++;
            Assert.That(meshers,Is.EqualTo(4));Assert.That(coaster.path.Spline.Closed);Assert.That(coaster.Length,Is.GreaterThan(300));
            for(int i=0;i<400;i++)
            {
                Vector3 p=coaster.path.EvaluatePosition(i/400f);
                foreach(var hit in Physics.OverlapSphere(p+Vector3.up*.65f,.6f,~0,QueryTriggerInteraction.Ignore))
                    Assert.That(hit.transform.IsChildOf(coaster.transform),Is.True,$"Track intersects {hit.name} at {p}");
            }
            yield return null;
        }
    }
}
