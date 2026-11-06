#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Pootle.Tests
{
    public sealed class PootleDigitalInputTests
    {
        private InputActionAsset actions;
        private InputAction move;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Joystick joystick;
        private PootleDigitalMoveSmoothing smoothing;
        private InputSettings.UpdateMode previousUpdateMode;
        private InputSettings.EditorInputBehaviorInPlayMode previousEditorBehavior;

        [SetUp]
        public void SetUp()
        {
            previousUpdateMode = InputSystem.settings.updateMode;
            previousEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            joystick = InputSystem.AddDevice<Joystick>();
            actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Pootle/Data/PootleControls.inputactions"));
            actions.devices = new InputDevice[] { keyboard, gamepad, joystick };
            move = actions.FindAction("Character/Move", true);
            actions.Enable();
            smoothing = new PootleDigitalMoveSmoothing();
            InputSystem.Update();
        }

        [TearDown]
        public void TearDown()
        {
            actions.Disable();
            Object.DestroyImmediate(actions);
            if (keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (joystick.added) InputSystem.RemoveDevice(joystick);
            InputSystem.settings.updateMode = previousUpdateMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorBehavior;
        }

        private void Keys(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
            InputSystem.Update();
        }

        private void Pad(GamepadState state)
        {
            InputSystem.QueueStateEvent(gamepad, state);
            InputSystem.Update();
        }

        [TestCase(Key.W)]
        [TestCase(Key.UpArrow)]
        public void KeyboardRampsAndReleasesToExactZero(Key key)
        {
            Keys(key);
            Assert.That(smoothing.Read(move, .03f).y, Is.EqualTo(.25f).Within(.001f));
            Assert.That(smoothing.Read(move, .09f).y, Is.EqualTo(1).Within(.001f));
            Keys();
            Assert.That(move.activeControl, Is.Null);
            Assert.That(smoothing.Read(move, .04f).y, Is.EqualTo(.5f).Within(.001f));
            Assert.That(smoothing.Read(move, .04f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void DpadDiagonalIsSmoothedAndNeverFasterThanCardinal()
        {
            Pad(new GamepadState().WithButton(GamepadButton.DpadUp).WithButton(GamepadButton.DpadRight));
            Assert.That(smoothing.Read(move, .03f).magnitude, Is.EqualTo(.25f).Within(.001f));
            var full = smoothing.Read(move, .2f);
            Assert.That(full.magnitude, Is.EqualTo(1).Within(.001f));
            Assert.That(full.x, Is.EqualTo(full.y).Within(.001f));
        }

        [Test]
        public void ReversalAndRightAngleTurnsAreGradual()
        {
            Keys(Key.W);
            smoothing.Read(move, .2f);
            Keys(Key.S);
            Assert.That(smoothing.Read(move, .03f).y, Is.EqualTo(.5f).Within(.001f));
            Assert.That(smoothing.Read(move, .09f).y, Is.EqualTo(-1).Within(.001f));
            Keys(Key.D);
            var turning = smoothing.Read(move, .02f);
            Assert.That(turning.x, Is.InRange(.01f, .99f));
            Assert.That(turning.y, Is.LessThan(0));
            Assert.That(turning.magnitude, Is.LessThanOrEqualTo(1));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void RampDependsOnElapsedTimeNotFrameRate(int rate)
        {
            smoothing.accelerationTime = 1;
            Keys(Key.W);
            Vector2 result = default;
            for (int i = 0; i < rate / 2; i++) result = smoothing.Read(move, 1f / rate);
            Assert.That(result.y, Is.EqualTo(.5f).Within(.0001f));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void ReversalKeepsItsRateThroughZero(int rate)
        {
            smoothing.turnTime = .2f;
            Keys(Key.W);
            smoothing.Read(move, 1);
            Keys(Key.S);
            Vector2 result = default;
            for (int i = 0; i < rate / 5; i++) result = smoothing.Read(move, 1f / rate);
            Assert.That(result.y, Is.EqualTo(-1).Within(.0001f));
        }

        [Test]
        public void AnalogUsesTheSameRampAndReleaseAsDigital()
        {
            Pad(new GamepadState { leftStick = Vector2.up });
            Assert.That(smoothing.Read(move, .03f).y, Is.EqualTo(.25f).Within(.001f));
            Assert.That(smoothing.Read(move, .09f).y, Is.EqualTo(1).Within(.001f));
            Pad(new GamepadState());
            Assert.That(smoothing.Read(move, .04f).y, Is.EqualTo(.5f).Within(.001f));
            Assert.That(smoothing.Read(move, .04f), Is.EqualTo(Vector2.zero));
            Keys(Key.W);
            Assert.That(smoothing.Read(move, .03f).y, Is.EqualTo(.25f).Within(.001f));
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private Vector2 Analog(float degrees, float magnitude = .7f, float dt = 1)
        {
            Pad(new GamepadState { leftStick = Direction(degrees) * magnitude });
            return smoothing.Read(move, dt);
        }

        private static void AssertDirection(Vector2 value, float degrees)
        {
            Assert.That(Vector2.Angle(value, Direction(degrees)), Is.LessThan(.05f));
        }

        [Test]
        public void AnalogSnapsToAllSixteenDirectionsAndPreservesStrength()
        {
            for (int step = 0; step < 16; step++)
            {
                smoothing.Reset();
                var result = Analog(step * 22.5f + 4);
                AssertDirection(result, step * 22.5f);
                Assert.That(result.magnitude, Is.EqualTo(move.ReadValue<Vector2>().magnitude).Within(.0001f));
            }
        }

        [TestCase(0f)]
        [TestCase(180f)]
        [TestCase(-180f)]
        [TestCase(337.5f)]
        public void AnalogDirectionBoundaryHasHysteresisInBothDirections(float center)
        {
            AssertDirection(Analog(center), center);
            AssertDirection(Analog(center + 12), center);
            AssertDirection(Analog(center + 11), center);
            AssertDirection(Analog(center + 14), center);
            AssertDirection(Analog(center + 15), center + 22.5f);
            AssertDirection(Analog(center + 10), center + 22.5f);
            AssertDirection(Analog(center + 8), center);
        }

        [Test]
        public void AnalogDirectionLatchResetsAtNeutralAndWhenChangingControls()
        {
            Analog(0);
            AssertDirection(Analog(12), 0);
            Pad(new GamepadState());
            smoothing.Read(move, 1);
            AssertDirection(Analog(12), 22.5f);
            Analog(0);
            Pad(new GamepadState().WithButton(GamepadButton.DpadUp));
            smoothing.Read(move, 1);
            AssertDirection(Analog(12), 22.5f);
            smoothing.Reset();
            AssertDirection(Analog(10), 0);
        }

        [Test]
        public void AnalogDirectionSettingsCanChangeOrDisableSnapping()
        {
            Analog(0);
            smoothing.analogDirectionSteps = 8;
            AssertDirection(Analog(30), 45);
            smoothing.analogDirectionSteps = 0;
            AssertDirection(Analog(30), 30);
            smoothing.analogDirectionSteps = 16;
            smoothing.analogDirectionHysteresis = 0;
            Analog(0);
            AssertDirection(Analog(12), 22.5f);
        }

        [Test]
        public void AnalogLargeTurnsAndDirectionChangesAreSmoothed()
        {
            Analog(0, 1);
            var turning = Analog(90, 1, .02f);
            Assert.That(turning.x, Is.InRange(.01f, .99f));
            Assert.That(turning.y, Is.InRange(.01f, .99f));
            AssertDirection(smoothing.Read(move, .2f), 90);
            // A large change goes directly to the nearest sector, rather than stepping one sector per frame.
            AssertDirection(Analog(-90, 1), -90);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void AnalogReversalKeepsItsRateWhenStickStrengthVaries(int rate)
        {
            smoothing.turnTime = .2f;
            Analog(90, 1);
            Vector2 result = default;
            for (int i = 0; i < rate / 5; i++)
                result = Analog(-90, i % 2 == 0 ? .9f : 1, 1f / rate);
            Assert.That(result.y, Is.EqualTo(-1).Within(.0001f));
        }

        [Test]
        public void AnalogStrengthChangesUseAccelerationAndReleaseTimes()
        {
            Analog(0, 1);
            var weaker = Analog(0, .5f, .02f);
            Assert.That(weaker.x, Is.EqualTo(.75f).Within(.001f));
            var stronger = Analog(0, 1, .03f);
            Assert.That(stronger.x, Is.EqualTo(1).Within(.001f));
        }

        [Test]
        public void DigitalJoystickAxesCanBeMarkedAndTriggerJumpsImmediately()
        {
            smoothing.digitalControlPaths = new[] { "<Joystick>/stick" };
            QueueTrigger(1);
            InputSystem.QueueDeltaStateEvent(joystick.stick, Vector2.up);
            InputSystem.Update();
            Assert.That(smoothing.Read(move, .03f).y, Is.EqualTo(.25f).Within(.001f));
            var jump = actions.FindAction("Character/Jump", true);
            Assert.That(jump.WasPressedThisFrame());
            Assert.That(jump.IsPressed());
            QueueTrigger(0);
            InputSystem.Update();
            Assert.That(jump.IsPressed(), Is.False);
        }

        private void QueueTrigger(float value)
        {
            using (StateEvent.From(joystick, out var inputEvent))
            {
                joystick.trigger.WriteValueIntoEvent(value, inputEvent);
                InputSystem.QueueEvent(inputEvent);
            }
        }

        [Test]
        public void AtariBindingsMatchTheSourceInputActions()
        {
            var source = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Pootle/InputSystem_Actions.inputactions");
            foreach (var name in new[] { "Move", "Jump" })
            {
                bool found = false;
                foreach (var binding in source.FindAction("Player/" + name, true).bindings)
                {
                    if (!binding.path.Contains("Atari CX")) continue;
                    found = true;
                    Assert.That(actions.FindAction("Character/" + name).bindings.Any(b => b.path == binding.path));
                    if (name == "Move") Assert.That(smoothing.digitalControlPaths, Does.Contain(binding.path));
                }
                Assert.That(found, Is.True);
            }
        }

        [Test]
        public void DisableResetAndDisconnectClearMovement()
        {
            Keys(Key.W);
            smoothing.Read(move, .2f);
            move.Disable();
            Assert.That(smoothing.Read(move, .01f), Is.EqualTo(Vector2.zero));
            move.Enable();
            InputSystem.Update();
            Assert.That(smoothing.Read(move, .03f).y, Is.EqualTo(.25f).Within(.001f));
            smoothing.Reset();
            Assert.That(smoothing.Read(move, 0), Is.EqualTo(Vector2.zero));
            smoothing.Read(move, .2f);
            InputSystem.RemoveDevice(keyboard);
            Assert.That(smoothing.Read(move, .001f), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void SmoothingCanBeDisabledAndZeroTimesSnap()
        {
            Keys(Key.W);
            smoothing.enabled = false;
            Assert.That(smoothing.Read(move, .001f), Is.EqualTo(Vector2.up));
            smoothing.enabled = true;
            smoothing.releaseTime = 0;
            Keys();
            Assert.That(smoothing.Read(move, .001f), Is.EqualTo(Vector2.zero));
            smoothing.accelerationTime = 0;
            Keys(Key.D);
            Assert.That(smoothing.Read(move, .001f), Is.EqualTo(Vector2.right));
        }
    }
}
#endif
