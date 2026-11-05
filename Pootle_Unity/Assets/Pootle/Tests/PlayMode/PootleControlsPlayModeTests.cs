using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator Controls_BAndLBHoldRunIndependentlyAndOnlyRTGroundPounds()
        {
            character.Teleport(new Vector3(0, .03f, -4), Quaternion.identity);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return null; yield return null;
            Assert.That(character.IsRunning); Assert.That(character.IsGrounded);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East).WithButton(GamepadButton.LeftShoulder));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            yield return null; yield return null; Assert.That(character.IsRunning, "LB keeps running when B is released");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null; yield return null; Assert.That(character.IsRunning, Is.False);
            character.Teleport(new Vector3(0, 12, -4), Quaternion.identity);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return null; yield return null; Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return null; yield return null; Assert.That(character.Mode, Is.EqualTo(PootleMode.GroundPounding));
        }

        [UnityTest]
        public IEnumerator Controls_LeftTriggerStaysBallUntilNextPressAndKeyboardActionsDoNotOverlap()
        {
            character.Teleport(new Vector3(0, .03f, -4), Quaternion.identity);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 });
            yield return new WaitForSeconds(.2f); Assert.That(character.IsRolling);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null; yield return null; Assert.That(character.IsRolling, "Releasing LT keeps ball mode active");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 });
            yield return null; yield return null; Assert.That(character.IsRolling, Is.False);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            character.simulateAutomatically = false;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
                var frame = character.input.Sample(); Assert.That(frame.interactPressed); Assert.That(frame.glide, Is.False); Assert.That(frame.grip, Is.False);
                frame = character.input.Sample(); Assert.That(frame.interactPressed, Is.False, "One interaction per input edge");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F)); yield return null;
                frame = character.input.Sample(); Assert.That(frame.glide); Assert.That(frame.interactPressed, Is.False);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
    }
}
