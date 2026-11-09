using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Pootle;
namespace OinkRomp.Tests
{
    public sealed class OinkRompPlayModeTests
    {
        OinkRompController pig;
        InputSettings.EditorInputBehaviorInPlayMode previousInputBehavior;
        InputSettings.UpdateMode previousUpdateMode;
        InputSettings.BackgroundBehavior previousBackground;
        [TearDown] public void RestoreInputRouting(){InputSystem.settings.editorInputBehaviorInPlayMode=previousInputBehavior;InputSystem.settings.updateMode=previousUpdateMode;InputSystem.settings.backgroundBehavior=previousBackground;}
        [UnitySetUp] public IEnumerator Setup()
        {
            previousInputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            previousUpdateMode=InputSystem.settings.updateMode;previousBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#if UNITY_EDITOR
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/OinkRomp/OinkRomp.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            yield return SceneManager.LoadSceneAsync("OinkRomp");
#endif
            pig=Object.FindAnyObjectByType<OinkRompController>();pig.simulateAutomatically=false;pig.Character.outputCamera=null;
            for(int i=0;i<20;i++)Step();
        }
        void Step(bool press=false,bool held=false,Vector2 move=default,bool run=false)
        {Physics.SyncTransforms();pig.Step(.02f,new PootleInputFrame{jumpPressed=press,jumpHeld=held,move=move,run=run});}
        void Tap(){Step(true,true);Step();}
        void Arena()
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Test arena";floor.transform.position=new Vector3(100,9.5f,0);floor.transform.localScale=new Vector3(30,1,30);
            pig.Character.Teleport(new Vector3(100,10.04f,0),Quaternion.identity);for(int i=0;i<10;i++)Step();
        }
        [Test] public void BirdsDefaultToIdleAndReenableInIdle()
        {
            foreach(var bird in pig.course.birds)
            {
#if UNITY_EDITOR
                var controller=bird.animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                Assert.That(controller.layers[0].stateMachine.defaultState.name,Is.EqualTo("Idle_A"),bird.name);
#endif
                bird.gameObject.SetActive(false);bird.gameObject.SetActive(true);
                bird.animator.Update(0);
                Assert.That(bird.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle_A"),Is.True,bird.name);
                bird.Face(pig,false);bird.animator.Update(.3f);
                Assert.That(bird.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle_A"),Is.True,bird.name);
                bird.Face(pig,true);bird.animator.Update(.3f);bird.animator.Update(0);
                Assert.That(bird.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle_B"),Is.True,bird.name);
                bird.Face(pig,false);bird.animator.Update(.3f);bird.animator.Update(0);
                Assert.That(bird.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle_A"),Is.True,bird.name);
            }
#if UNITY_EDITOR
            var pigController=pig.animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
            Assert.That(pigController.layers[0].stateMachine.defaultState.name,Is.EqualTo("Idle_A"));
#endif
        }
        [TestCase(9,34)]
        [TestCase(22,38)]
        [TestCase(9,24)]
        public void CaveDeploymentPopsWithoutLiftAndRechargesOutside(float x,float z)
        {
            pig.AddBalloons(2);pig.Character.Teleport(new Vector3(x,103.03f,z),Quaternion.identity);
            for(int i=0;i<10;i++)Step();
            Assert.That(pig.Character.IsGrounded,Is.True);
            int capacity=pig.BalloonCapacity; // The fern room also contains a real upgrade pickup.
            Tap();Assert.That(pig.Hop,Is.EqualTo(1));float velocity=pig.Character.Velocity.y;
            Step(true,true);
            Assert.That(pig.InflatedCount,Is.Zero);
            Assert.That(pig.Character.Velocity.y,Is.EqualTo(velocity-pig.Character.profile.risingGravity*.02f).Within(.02f),"A popped balloon must not add lift");
            Assert.That(pig.FastDrop,Is.False,"Automatic cave pop is not a slam");
            Assert.That(pig.AvailableBalloons,Is.EqualTo(capacity-1));Assert.That(pig.BalloonCapacity,Is.EqualTo(capacity));
            for(int i=0;i<150&&!pig.Character.IsGrounded;i++)Step();Step();
            Assert.That(pig.AvailableBalloons,Is.EqualTo(capacity));
            Arena();Tap();Tap();Assert.That(pig.InflatedCount,Is.EqualTo(1));
        }
        [Test] public void StartsWithOneRegularJumpAndNoAirUpgrades()
        {
            Arena();Assert.That(pig.BalloonCapacity,Is.Zero);Assert.That(pig.Character.IsGrounded,Is.True);
            Step(true,true);float launch=pig.Character.Velocity.y;
            Assert.That(launch,Is.InRange(17,19));Step();Tap();
            Assert.That(pig.Hop,Is.EqualTo(1));Assert.That(pig.InflatedCount,Is.Zero);Assert.That(pig.Character.Velocity.y,Is.LessThan(launch));
        }
        [Test] public void SevenPermanentUpgradesRechargeAfterPopLandingAndRecovery()
        {
            Arena();pig.AddBalloons(7);Tap();for(int i=0;i<7;i++)Tap();
            Assert.That(pig.Hop,Is.EqualTo(8));Assert.That(pig.InflatedCount,Is.EqualTo(7));Assert.That(pig.AvailableBalloons,Is.Zero);Tap();Assert.That(pig.Hop,Is.EqualTo(8));
            for(int i=0;i<22;i++)Step(false,true);
            Assert.That(pig.FastDrop,Is.True);Assert.That(pig.InflatedCount,Is.Zero);Assert.That(pig.AvailableBalloons,Is.Zero);
            for(int i=0;i<300&&!pig.Character.IsGrounded;i++)Step();Step();
            Assert.That(pig.BalloonCapacity,Is.EqualTo(7));Assert.That(pig.AvailableBalloons,Is.EqualTo(7));
            Tap();Tap();Assert.That(pig.InflatedCount,Is.EqualTo(1));pig.Respawn();Assert.That(pig.AvailableBalloons,Is.EqualTo(7));
        }
        [Test] public void BalloonsShrinkWithoutLosingCapacityOrRedeployingUsedSlots()
        {
            Arena();pig.AddBalloons(1);pig.balloonLifetime=.2f;Tap();Tap();float life=pig.BalloonLives[0];Step();Assert.That(pig.BalloonLives[0],Is.LessThan(life));
            for(int i=0;i<12;i++)Step();Assert.That(pig.InflatedCount,Is.Zero);Assert.That(pig.BalloonCapacity,Is.EqualTo(1));Tap();Assert.That(pig.InflatedCount,Is.Zero);
        }
        [Test] public void ExactlySevenUniqueFindsAndApplesPlayEat()
        {
            var upgrades=pig.course.pickups.Where(p=>p.kind==OinkRompPickup.Kind.Balloons).ToArray();Assert.That(upgrades.Length,Is.EqualTo(7));
            foreach(var p in upgrades){pig.Character.Teleport(p.transform.position-Vector3.up*.6f,Quaternion.identity);Assert.That(p.TryCollect(pig),Is.True);Assert.That(p.TryCollect(pig),Is.False);}
            Assert.That(pig.BalloonCapacity,Is.EqualTo(7));Assert.That(pig.AddBalloons(1),Is.Zero);
            var apple=pig.course.pickups.First(p=>p.kind==OinkRompPickup.Kind.Apple);pig.Character.Teleport(apple.transform.position-Vector3.up*.6f,Quaternion.identity);
            Assert.That(apple.TryCollect(pig),Is.True);Assert.That(pig.Apples,Is.EqualTo(1));Assert.That(pig.AnimationState,Is.EqualTo("Eat"));
        }
        [Test] public void NativeSpringUsesFirstHopThenDeploysAnUpgrade()
        {
            pig.AddBalloons(1);pig.balloonLifetime=.2f;var spring=Object.FindObjectsByType<PootleSpring>().First(s=>s.transform.IsChildOf(pig.course.transform));
            var b=spring.GetComponent<Collider>().bounds;pig.Character.Teleport(new Vector3(b.center.x,b.max.y+.05f,b.center.z),Quaternion.identity);
            for(int i=0;i<20&&pig.Hop==0;i++)Step();
            Assert.That(pig.Hop,Is.EqualTo(1));Assert.That(pig.Character.Velocity.y,Is.GreaterThan(10));Tap();Assert.That(pig.InflatedCount,Is.EqualTo(1));
            for(int i=0;i<300&&pig.AvailableBalloons==0;i++)Step();
            Assert.That(pig.AvailableBalloons,Is.EqualTo(1),"Landing on a spring must recharge the unlocked balloon");
        }
        [Test] public void HighSlamBreaksFloorAndLowContactDoesNot()
        {
            var floor=pig.course.smashFloors[0];var surface=floor.surface.bounds;
            Assert.That(floor.TrySmash(surface.center+Vector3.up*.1f,.5f),Is.False);
            pig.Character.Teleport(new Vector3(surface.center.x,surface.max.y+9,surface.center.z),Quaternion.identity);
            for(int i=0;i<200&&pig.SlamCount==0;i++)Step(false,true);
            Assert.That(pig.SlamCount,Is.EqualTo(1));Assert.That(pig.LastSlamHeight,Is.GreaterThan(2));Assert.That(pig.LastSlamStrength,Is.GreaterThan(.15f));Assert.That(floor.Broken,Is.True);Assert.That(floor.surface.enabled,Is.False);
        }
        [Test] public void ShovingButtonOpensGateAndPlaysAttack()
        {
            var gate=pig.course.gates[0];var pos=gate.button.position-gate.transform.forward*.9f;pos.y=gate.transform.position.y+.03f;pig.Character.Teleport(pos,Quaternion.identity);for(int i=0;i<12;i++)Step();
            for(int i=0;i<20;i++)gate.Sample(pig,default,.02f);Assert.That(gate.IsOpen,Is.False);
            for(int i=0;i<25;i++)gate.Sample(pig,new PootleInputFrame{move=Vector2.up},.02f);
            Assert.That(gate.IsOpen,Is.True);Assert.That(gate.blocker.enabled,Is.False);Assert.That(pig.AnimationState,Is.EqualTo("Attack"));
        }
        [Test] public void SpiralRailAndHangingHandleKeepNativeJumpRelease()
        {
            pig.AddBalloons(2);var rail=pig.course.GetComponentInChildren<PootleRail>();var point=rail.Position(rail.SegmentCount-1,rail.Length(rail.SegmentCount-1)*.7f);
            pig.Character.Teleport(point+Vector3.up*.2f,Quaternion.identity);
            for(int i=0;i<35&&!pig.GetComponent<PootleRailRider>().Rail;i++)Step();
            Assert.That(pig.GetComponent<PootleRailRider>().Rail,Is.EqualTo(rail));Step(true,true);Assert.That(pig.Character.Velocity.y,Is.GreaterThan(5));Assert.That(pig.GetComponent<PootleRailRider>().Rail,Is.Null);Assert.That(pig.InflatedCount,Is.Zero,"Jump off the rail must not also deploy a balloon");
            var hook=pig.course.GetComponentsInChildren<PootleDangleHook>().First();pig.Character.Teleport(hook.HangingPosition(Vector3.forward),Quaternion.identity);
            for(int i=0;i<5;i++)Step();Assert.That(pig.Character.Mode,Is.EqualTo(PootleMode.Dangling));Step(true,true);
            Assert.That(pig.GetComponent<PootleHookRider>().Hook,Is.Null);Assert.That(pig.Character.Velocity.y,Is.GreaterThan(1));Assert.That(pig.InflatedCount,Is.Zero);
        }
        [Test] public void SlipperyRampSlidesAndCanJumpOff()
        {
            var ramp=GameObject.Find("Slippery lagoon ramp");pig.Character.Teleport(new Vector3(9,12.1f,30.5f),Quaternion.identity);
            bool slid=false;for(int i=0;i<35;i++){Step();slid|=pig.Character.Mode==PootleMode.Sliding;}
            Assert.That(slid,Is.True);Assert.That(pig.transform.position.z,Is.LessThan(30));
            Step(true,true);Assert.That(pig.Character.Velocity.y,Is.GreaterThan(5));
        }
        [Test] public void CoastalMapControlsAndHigherCameraZonesAreRetained()
        {
            foreach(string prefix in new[]{"01","02","03","04","05","06","07","08","09","Cave Network"})Assert.That(SceneManager.GetActiveScene().GetRootGameObjects().Any(r=>r.name.StartsWith(prefix)&&r.activeSelf),Is.True,prefix);
            Assert.That(Object.FindObjectsByType<PootleCameraZone>().Length,Is.EqualTo(35));
            foreach(var shot in Object.FindObjectsByType<PootleExplorationShot>())Assert.That(shot.pitchYaw.x,Is.GreaterThanOrEqualTo(65));
#if UNITY_EDITOR
            var source=UnityEditor.AssetDatabase.LoadAssetAtPath<PootleProfile>("Assets/Pootle/Samples/Pootle multijump.asset");
            if(source){Assert.That(pig.Character.profile.walkSpeed,Is.EqualTo(source.walkSpeed));Assert.That(pig.Character.profile.airAcceleration,Is.EqualTo(source.airAcceleration));}
#endif
            Assert.That(pig.Character.profile.walkSpeed,Is.EqualTo(7.5f));Assert.That(pig.Character.profile.runSpeed,Is.EqualTo(12));Assert.That(pig.Character.profile.automaticGrip,Is.True);
            var blob=pig.GetComponent<PootleBlobShadow>();Assert.That(blob.CircleCount,Is.EqualTo(7));Assert.That(blob.Mask,Is.Not.Null);
            foreach(var balloon in pig.balloons.envelopes){var m=balloon.GetComponent<Renderer>().sharedMaterial;Assert.That(m.renderQueue,Is.EqualTo(3000));Assert.That(m.GetColor("_BaseColor").a,Is.LessThan(.6f));}
        }
        [UnityTest] public IEnumerator CaveTransitionOwnsMotionAndPreservesPermanentBalloons()
        {
            pig.AddBalloons(2);var travel=Object.FindAnyObjectByType<PootleCaveTravel>();var portal=travel.portals.First(p=>p.name=="ENTER · Hidden fern gate");
            pig.Character.Teleport(portal.transform.position,portal.transform.rotation);Assert.That(travel.Begin(portal),Is.True);
            var before=pig.transform.position;pig.Step(.1f,new PootleInputFrame{move=Vector2.one,jumpPressed=true,jumpHeld=true});Assert.That(pig.transform.position,Is.EqualTo(before));
            float end=Time.time+8;while(travel.Busy && Time.time<end)yield return null;
            Assert.That(travel.Busy,Is.False);Assert.That(pig.transform.position.y,Is.GreaterThan(100));Assert.That(pig.BalloonCapacity,Is.EqualTo(2));
            Assert.That(Object.FindAnyObjectByType<PootleZoneCameraDirector>().ActiveZone.name,Does.Contain("Cave"));Assert.That(Object.FindAnyObjectByType<PootleCaveLighting>().IsInside,Is.True);
        }
        [Test] public void SmoothedDigitalTurnsBlendAndAnalogueRetainsFullDirection()
        {
            var smooth=JsonUtility.FromJson<PootleDigitalMoveSmoothing>(JsonUtility.ToJson(pig.Character.input.digitalMovement));
            Assert.That(smooth.turnTime,Is.GreaterThanOrEqualTo(.4f));Assert.That(smooth.analogDirectionSteps,Is.Zero);
            var keyboard=InputSystem.AddDevice<Keyboard>();var gamepad=InputSystem.AddDevice<Gamepad>();
            using(var action=new InputAction("Smooth test",InputActionType.Value))
            {
                action.AddCompositeBinding("2DVector").With("Up","<Keyboard>/w").With("Right","<Keyboard>/d").With("Down","<Keyboard>/s");action.AddBinding("<Gamepad>/leftStick");action.Enable();
                try
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));InputSystem.Update();Vector2 v=default;for(int i=0;i<20;i++)v=smooth.Read(action,.02f);Assert.That(v.y,Is.EqualTo(1).Within(.01f));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.D));InputSystem.Update();v=smooth.Read(action,.02f);Assert.That(v.x,Is.InRange(.01f,.2f));Assert.That(v.y,Is.GreaterThan(.85f));
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();for(int i=0;i<8;i++)v=smooth.Read(action,.02f);Assert.That(v,Is.EqualTo(Vector2.zero));
                    InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=new Vector2(.31f,.72f)});InputSystem.Update();for(int i=0;i<30;i++)v=smooth.Read(action,.02f);
                    Assert.That(Vector2.Angle(v,new Vector2(.31f,.72f)),Is.LessThan(.1f));Assert.That(v.magnitude,Is.LessThan(1));
                }
                finally{InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(gamepad);}
            }
        }
        [UnityTest] public IEnumerator KeyboardAndJoystickBindingsProduceMovementAndJump()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.Space));InputSystem.Update();yield return null;
                var frame=pig.Character.input.Sample();Assert.That(frame.move.y,Is.GreaterThan(0));Assert.That(frame.jumpHeld,Is.True);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return null;
                var joystick=InputSystem.AddDevice<Joystick>();
                try{using(StateEvent.From(joystick,out var ev)){joystick.stick.WriteValueIntoEvent(Vector2.right,ev);joystick.trigger.WriteValueIntoEvent(1f,ev);InputSystem.QueueEvent(ev);}InputSystem.Update();yield return null;frame=pig.Character.input.Sample();Assert.That(frame.move.x,Is.GreaterThan(0));Assert.That(frame.jumpHeld,Is.True);}
                finally{InputSystem.RemoveDevice(joystick);}
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }
    }
}
