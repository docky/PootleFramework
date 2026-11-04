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
    public sealed class PootlePlayModeTests
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
