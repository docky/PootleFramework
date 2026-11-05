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
        public IEnumerator Frobject_GamepadAInteractsOnceWithoutJumpAndFallsBackToJump()
        {
            var button = Object.FindAnyObjectByType<PootleFrobjectButton>();
            Assert.That(button, Is.Not.Null);
            character.Teleport(button.transform.position + new Vector3(0, .03f, 1.7f), Quaternion.Euler(0, 180, 0));
            yield return null; yield return null;
            var sensor = character.GetComponent<PootleFrobjectSensor>(); Assert.That(sensor.CanInteract);
            var indicator = button.GetComponent<PootleFrobjectIndicator>(); Assert.That(indicator.State, Is.EqualTo(PootleFrobjectState.Ready));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null; yield return null;
            Assert.That(button.Value); Assert.That(character.IsGrounded);
            yield return new WaitForSeconds(.35f); Assert.That(button.Value, Is.True, "Holding A must not retrigger"); Assert.That(character.IsGrounded);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            button.GetComponent<PootleFrobject>().interactable = false;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null; yield return null;
            Assert.That(character.IsGrounded, Is.False, "No ready target leaves A available for jumping");
        }

        [UnityTest]
        public IEnumerator Frobject_SpeechAdvancesTimesOutAndHidesWhenLeavingAwareness()
        {
            var speech = Object.FindAnyObjectByType<PootleFrobjectSpeech>();
            character.Teleport(speech.transform.position + new Vector3(0, .03f, 1.7f), Quaternion.Euler(0, 180, 0));
            yield return null; yield return null;
            var sensor = character.GetComponent<PootleFrobjectSensor>(); Assert.That(sensor.CanInteract);
            Assert.That(sensor.TryInteract()); yield return null;
            Assert.That(speech.IsShowing); var bubble = GameObject.Find("Speech bubble (runtime)"); var label = bubble.GetComponentInChildren<TextMesh>();
            string first = label.text;
            yield return new WaitForSeconds(.3f); Assert.That(sensor.TryInteract()); yield return null;
            Assert.That(label.text, Is.Not.EqualTo(first));
            speech.lines = new[] { new string('W', 32) };
            yield return new WaitForSeconds(.3f); Assert.That(sensor.TryInteract()); yield return null;
            Assert.That(label.GetComponent<Renderer>().localBounds.size.x * label.transform.localScale.x, Is.LessThanOrEqualTo(3.201f), "Wide dialogue must fit inside the bubble");
            character.Teleport(new Vector3(0, .03f, -4), Quaternion.identity); yield return null; yield return null;
            // Explicitly exceed the default awareness range.
            character.Teleport(new Vector3(12, .03f, -8), Quaternion.identity); yield return null; yield return null;
            Assert.That(speech.IsShowing, Is.False);
            character.Teleport(speech.transform.position + new Vector3(0, .03f, 1.7f), Quaternion.Euler(0, 180, 0));
            yield return new WaitForSeconds(.3f); speech.secondsVisible = .1f;
            Assert.That(sensor.TryInteract()); yield return new WaitForSeconds(.2f); Assert.That(speech.IsShowing, Is.False);
            speech.enabled = false; Assert.That(speech.IsShowing, Is.False);
        }
    }
}
