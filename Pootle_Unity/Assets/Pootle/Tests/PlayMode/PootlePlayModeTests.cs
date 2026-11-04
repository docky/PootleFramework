using System.Collections;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        private Gamepad gamepad;
        private PootleCharacter character;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("PootlePlayground", LoadSceneMode.Single);
            gamepad = InputSystem.AddDevice<Gamepad>();
            character = Object.FindAnyObjectByType<PootleCharacter>();
            for (int i = 0; i < 5; i++) yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            if (character) character.Teleport(new Vector3(0, 0.03f, -4), Quaternion.identity);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DirectSceneStartsWithAnimatedLilyAndCinemachine()
        {
            Assert.That(character, Is.Not.Null); Assert.That(character.IsGrounded);
            Assert.That(character.visualRoot.GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
            foreach (var animator in character.visualRoot.GetComponentsInChildren<Animator>())
            { Assert.That(animator.enabled); Assert.That(animator.runtimeAnimatorController, Is.Not.Null); Assert.That(animator.applyRootMotion, Is.False); }
            Assert.That(character.outputCamera.GetComponent<CinemachineBrain>(), Is.Not.Null);
            int tick = character.DebugSnapshot.tick;
            yield return null;
            Assert.That(character.DebugSnapshot.tick, Is.GreaterThan(tick));
            Assert.That(character.GetComponents<CapsuleCollider>().Length, Is.EqualTo(1));
            Assert.That(character.GetComponents<PootleAvatar>().Length, Is.EqualTo(1));
            Assert.That(character.GetComponents<PootleSeatedPose>().Length, Is.EqualTo(1));
            Assert.That(character.GetComponents<PootleTraversal>().Length, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator GamepadAnalogueAndTwinStickOrbitUseIndependentActions()
        {
            Vector3 start = character.transform.position;
            var orbit = Object.FindAnyObjectByType<CinemachineOrbitalFollow>();
            float yaw = orbit.HorizontalAxis.Value;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up * 0.6f, rightStick = Vector2.right * 0.8f });
            yield return new WaitForSeconds(0.5f);
            var frame = character.input.Sample();
            Assert.That(frame.move.magnitude, Is.InRange(0.3f, 0.7f));
            Assert.That(Vector3.Distance(start, character.transform.position), Is.GreaterThan(0.2f));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, orbit.HorizontalAxis.Value)), Is.GreaterThan(5));
            Assert.That(character.input.actions.FindAction("Character/Move").enabled, Is.False);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return new WaitForSeconds(0.25f);
            float fixedYaw = orbit.HorizontalAxis.Value;
            character.Teleport(character.transform.position, Quaternion.Euler(0, 130, 0));
            yield return new WaitForSeconds(0.2f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(fixedYaw, orbit.HorizontalAxis.Value)), Is.LessThan(4));
        }

        [UnityTest]
        public IEnumerator KeyboardMouseBackupAndLeftShoulderRunWorkWithGamepadConnected()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.LeftShift));
                yield return new WaitForSeconds(0.15f);
                Assert.That(character.input.Sample().move.y, Is.GreaterThan(0.9f));
                Assert.That(character.IsRunning);
                var orbit = Object.FindAnyObjectByType<CinemachineOrbitalFollow>();
                float pitch = orbit.VerticalAxis.Value;
                for (int i = 0; i < 15; i++)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(4, 4), buttons = 2 });
                    yield return null;
                }
                Assert.That(orbit.VerticalAxis.Value, Is.LessThan(pitch));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
                yield return new WaitForSeconds(0.1f); Assert.That(character.input.Sample().run);
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                yield return new WaitForSeconds(0.1f); Assert.That(character.input.Sample().run, Is.False);
                pitch = orbit.VerticalAxis.Value;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.up * 0.5f });
                yield return new WaitForSeconds(0.15f);
                Assert.That(orbit.VerticalAxis.Value, Is.LessThan(pitch));
            }
            finally { InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); }
        }

        [UnityTest]
        public IEnumerator LilyAnimationChangesForJumpAndClimbWithoutRootMotion()
        {
            var animator = character.GetComponentInChildren<Animator>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSeconds(0.18f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Jump"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            character.Teleport(new Vector3(-11, 0.03f, 24.45f), Quaternion.identity);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up, leftTrigger = 1 });
            yield return new WaitForSeconds(0.25f);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Climbing));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Climb"));
            Assert.That(animator.applyRootMotion, Is.False);
        }

        [UnityTest]
        public IEnumerator GamepadJumpPassesThroughRailAndLandsOnTop()
        {
            character.Teleport(new Vector3(0, 0.02f, 3), Quaternion.identity);
            yield return new WaitForSeconds(0.1f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSeconds(0.25f);
            Assert.That(character.transform.position.y, Is.GreaterThan(1.5f));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return new WaitForSeconds(0.8f);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.RailWalking));
            Assert.That(character.transform.position.y, Is.EqualTo(1.4f).Within(0.03f));
            Assert.That(character.GetComponents<PootleRailRider>().Length, Is.EqualTo(1));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up }.WithButton(GamepadButton.South));
            yield return new WaitForSeconds(0.1f);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
        }

        [UnityTest]
        public IEnumerator SplinePlatformCarriesStandingCharacterInLiveScene()
        {
            PootleMovingSurface surface = null;
            foreach (var candidate in Object.FindObjectsByType<PootleMovingSurface>())
                if (candidate.name.StartsWith("Moving platform")) surface = candidate;
            Assert.That(surface, Is.Not.Null);
            Bounds bounds = surface.GetComponentInChildren<Collider>().bounds;
            character.Teleport(new Vector3(bounds.center.x, bounds.max.y + 0.03f, bounds.center.z), Quaternion.identity);
            yield return new WaitForSeconds(0.15f);
            Vector3 start = character.transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.That(character.IsGrounded);
            Assert.That(character.GetComponent<PootlePlatformRider>().Surface, Is.SameAs(surface));
            Assert.That(Vector3.Distance(start, character.transform.position), Is.GreaterThan(0.2f));
        }
        [UnityTest]
        public IEnumerator MovingHookAttachesAutomaticallyAndBReleasesIt()
        {
            PootleDangleHook hook = null;
            foreach (var candidate in Object.FindObjectsByType<PootleDangleHook>())
                if (candidate.name.StartsWith("Dangle shuttle")) hook = candidate;
            Assert.That(hook, Is.Not.Null);
            character.Teleport(hook.HangingPosition(character.Facing), Quaternion.identity);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return new WaitForSeconds(0.3f);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Dangling));
            Assert.That(Vector3.Distance(character.transform.position, hook.HangingPosition(character.Facing)), Is.LessThan(0.1f));
            Assert.That(character.GetComponents<PootleHookRider>().Length, Is.EqualTo(1));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return new WaitForSeconds(0.1f);
            Assert.That(character.Mode, Is.Not.EqualTo(PootleMode.Dangling));
        }

        [UnityTest]
        public IEnumerator RightShoulderDeploysAnimatedGliderAndReleaseClosesIt()
        {
            character.Teleport(new Vector3(0,12,-4),Quaternion.identity);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return new WaitForSeconds(.4f);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Gliding));
            Assert.That(character.GetComponentInChildren<Animator>().GetCurrentAnimatorStateInfo(0).IsName("Glide"));
            var visual=character.GetComponent<PootleGliderVisual>(); Assert.That(visual.gliderRoot.localScale.x,Is.GreaterThan(.95f));
            InputSystem.QueueStateEvent(gamepad,new GamepadState()); yield return new WaitForSeconds(.3f);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));Assert.That(visual.gliderRoot.localScale.sqrMagnitude,Is.LessThan(.001f));
        }
        [UnityTest]
        public IEnumerator SpringLaunchesWithoutJumpButtonInLiveScene()
        {
            character.Teleport(new Vector3(-5,.43f,-3),Quaternion.identity);
            yield return new WaitForSeconds(.3f);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.transform.position.y,Is.GreaterThan(4));Assert.That(character.Velocity.y,Is.GreaterThan(10));
        }

        [UnityTest]
        public IEnumerator RailAllowsSideFacingAndNeutralJumpStaysVertical()
        {
            character.outputCamera=null;
            character.Teleport(new Vector3(-2,3.5f,3),Quaternion.identity);
            yield return new WaitForSeconds(.5f);Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));
            Vector3 start=character.transform.position;
            InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.up});
            yield return new WaitForSeconds(.25f);
            Assert.That(Vector3.Distance(start,character.transform.position),Is.LessThan(.05f));
            Assert.That(Vector3.Dot(character.Facing,Vector3.forward),Is.GreaterThan(.99f));
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSeconds(.12f);
            Assert.That(Vector3.ProjectOnPlane(character.transform.position-start,Vector3.up).magnitude,Is.LessThan(.05f));
        }
        [UnityTest]
        public IEnumerator HelixExtrusionExistsAndGroundPoundSlidesAlongIt()
        {
            var spline=Object.FindAnyObjectByType<PootleSplineRail>();Assert.That(spline,Is.Not.Null);
            var mesh=spline.GetComponent<MeshFilter>().sharedMesh;Assert.That(mesh.vertexCount,Is.GreaterThan(100));
            var rail=spline.GetComponent<PootleRail>();var point=rail.Point(rail.SegmentCount/2);
            character.Teleport(point+Vector3.up*2,Quaternion.identity);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.East));
            yield return new WaitForSeconds(.35f);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.RailSliding));
            Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.SameAs(rail));
            Assert.That(character.transform.position.y,Is.LessThan(point.y));
        }
        [UnityTest]
        public IEnumerator ShortPostHoldsPositionUntilGamepadJump()
        {
            PootlePole post=null;foreach(var p in Object.FindObjectsByType<PootlePole>())if(p.name=="Short standing post 1")post=p;
            Assert.That(post,Is.Not.Null);character.Teleport(post.Top+Vector3.up*1.5f,Quaternion.identity);
            yield return new WaitForSeconds(.5f);Assert.That(character.Mode,Is.EqualTo(PootleMode.PoleStanding));
            InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.right});yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(character.transform.position,post.Top),Is.LessThan(.03f));
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSeconds(.1f);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            Assert.That(Vector3.ProjectOnPlane(character.transform.position-post.Top,Vector3.up).magnitude,Is.LessThan(.03f));
        }

        [UnityTest]
        public IEnumerator GamepadUpApproachesNearbyTJunctionAndTurnsOntoBranch()
        {
            character.outputCamera=null;character.Teleport(new Vector3(-.7f,3,3),Quaternion.identity);
            yield return new WaitForSeconds(.5f);Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));
            InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.up});
            // Wait for the crossing, not a render-frame boundary near z=4.
            float deadline=Time.time+1f;
            while(character.transform.position.z<=4.05f && Time.time<deadline) yield return null;
            Assert.That(character.GetComponent<PootleRailRider>().Rail.name,Does.StartWith("T junction"));
            Assert.That(character.transform.position.x,Is.Zero.Within(.03f));
            Assert.That(character.transform.position.z,Is.GreaterThan(4));
        }
        [UnityTest]
        public IEnumerator GamepadHelixWalkingDoesNotHopBetweenNearbySamples()
        {
            character.outputCamera=null;
            var spline=Object.FindAnyObjectByType<PootleSplineRail>();var rail=spline.GetComponent<PootleRail>();
            character.Teleport(rail.Point(rail.SegmentCount*3/4)+Vector3.up,Quaternion.identity);
            yield return new WaitForSeconds(.45f);Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));
            for(int frame=0;frame<80;frame++)
            {
                float nearest=float.PositiveInfinity;Vector3 tangent=Vector3.zero;
                for(int i=0;i<rail.SegmentCount;i++)
                {
                    var point=rail.Closest(i,character.transform.position,out float along);
                    float d=(point-character.transform.position).sqrMagnitude;
                    if(d<nearest){nearest=d;tangent=rail.Tangent(i,along);}
                }
                Vector2 input=new Vector2(-tangent.x,-tangent.z).normalized;
                var before=character.transform.position;
                InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=input});yield return null;
                Assert.That(character.GetComponent<PootleRailRider>().Rail,Is.SameAs(rail));
                Assert.That(Vector3.Distance(before,character.transform.position),Is.LessThan(character.profile.walkSpeed*Mathf.Min(Time.deltaTime,.1f)+.025f));
            }
        }

        [UnityTest]
        public IEnumerator HeldJumpFiresOnceAndInputSurvivesEnableDisable()
        {
            int jumps = 0; character.Jumped += () => jumps++;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSeconds(1.2f);
            Assert.That(jumps, Is.EqualTo(1));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            character.gameObject.SetActive(false); yield return null;
            character.gameObject.SetActive(true); yield return null;
            Assert.That(character.GetComponents<PootleInput>().Length, Is.EqualTo(1));
            Assert.That(character.GetComponents<CapsuleCollider>().Length, Is.EqualTo(1));
            character.Teleport(new Vector3(0, 0.03f, -4), Quaternion.identity);
            for (int i = 0; i < 4; i++) yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
            yield return new WaitForSeconds(0.2f);
            Assert.That(character.Velocity.sqrMagnitude, Is.GreaterThan(0.1f));
        }
    }
}
