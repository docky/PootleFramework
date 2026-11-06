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
        IEnumerator PressCarCameraKey(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
        }

        IEnumerator EnterCameraTestCar(PootleCar car)
        {
            yield return ApproachCar(car);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
            car.Controller.immobilize();
            car.Controller.setPosition(new Vector3(0, .02f, -22));
            yield return new WaitForSeconds(.15f);
        }

        [UnityTest]
        public IEnumerator Car_CameraAlpha890SelectAllModesOnBothVehiclesOnlyWhileOccupied()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                foreach (string name in new[] { "Pootle Sedan", "Pootle Pickup" })
                {
                    var car = GameObject.Find(name).GetComponent<PootleCar>();
                    var mode = car.cameraMode;
                    yield return PressCarCameraKey(keyboard, Key.Digit9);
                    Assert.That(car.cameraMode, Is.EqualTo(mode), "Unoccupied vehicles must ignore camera mode keys");
                    yield return ApproachCar(car);
                    Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
                    yield return PressCarCameraKey(keyboard, Key.Digit9);
                    Assert.That(car.cameraMode, Is.EqualTo(PootleCarCameraMode.CameraRelative));
                    yield return PressCarCameraKey(keyboard, Key.Digit0);
                    Assert.That(car.cameraMode, Is.EqualTo(PootleCarCameraMode.Warthog));
                    yield return PressCarCameraKey(keyboard, Key.Digit8);
                    Assert.That(car.cameraMode, Is.EqualTo(PootleCarCameraMode.DemoSwing));
                    Assert.That(car.TryExit(), Is.True);
                    yield return PressCarCameraKey(keyboard, Key.Digit0);
                    Assert.That(car.cameraMode, Is.EqualTo(PootleCarCameraMode.DemoSwing), "Exit releases the private mode actions");
                }
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator Car_DemoCameraLetsTheNoseSwingAtRestThenFollowsAtSpeed()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
            yield return EnterCameraTestCar(car);
            car.SelectCameraMode(PootleCarCameraMode.DemoSwing);
            float originalYaw = car.CameraYaw;
            car.Controller.immobilize(); car.Controller.setRotation(Quaternion.Euler(0, 70, 0));
            yield return new WaitForSeconds(.35f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(originalYaw, car.CameraYaw)), Is.LessThan(5), "Stopped camera keeps its world heading");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(car.Body.eulerAngles.y, car.CameraYaw)), Is.GreaterThan(45), "The front can swing away from the camera");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = .8f });
            yield return new WaitForSeconds(.8f);
            Assert.That(Mathf.DeltaAngle(originalYaw, car.CameraYaw), Is.GreaterThan(3), "Travel gradually pulls the camera around");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator Car_CameraRelativeSteersTowardTheViewAndCorrectsReverseSteering()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
            yield return EnterCameraTestCar(car);
            car.SelectCameraMode(PootleCarCameraMode.CameraRelative);
            var orbit = Object.FindAnyObjectByType<CinemachineOrbitalFollow>();
            float footYaw = orbit.HorizontalAxis.Value;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.right * .5f });
            yield return new WaitForSeconds(.6f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return new WaitForSeconds(.2f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0, car.CameraYaw)), Is.GreaterThan(25));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(footYaw, orbit.HorizontalAxis.Value)), Is.LessThan(.2f), "The suspended character orbit must not consume car look input");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up, rightTrigger = .7f });
            yield return null; yield return null;
            Assert.That(car.Controller.getMotor(), Is.GreaterThan(.6f));
            Assert.That(car.Controller.getSteering(), Is.GreaterThan(.3f));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            car.Controller.immobilize(); car.Controller.setRotation(Quaternion.identity); car.Controller.setPosition(new Vector3(0, .02f, -22));
            yield return new WaitForSeconds(.15f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.down, rightTrigger = .7f });
            yield return null; yield return null;
            Assert.That(car.Controller.getMotor(), Is.LessThan(-.6f));
            Assert.That(car.Controller.getSteering(), Is.LessThan(-.3f), "Reverse wheel angle must invert so the car still reaches the requested heading");
            float before = car.Body.eulerAngles.y;
            yield return new WaitForSeconds(.8f);
            Assert.That(Mathf.DeltaAngle(before, car.Body.eulerAngles.y), Is.GreaterThan(1), "Reversing must rotate the nose toward the camera heading, not away");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator Car_WarthogAimsTheNoseWhereTheCameraLooksWithTriggerOnlyDrive()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
            yield return EnterCameraTestCar(car);
            car.SelectCameraMode(PootleCarCameraMode.CameraRelative);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightStick = Vector2.right * .5f });
            yield return new WaitForSeconds(.6f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = .8f });
            yield return new WaitForSeconds(.15f);
            Assert.That(car.Controller.getSteering(), Is.EqualTo(0).Within(.01f), "Directional mode needs a stick heading");
            car.SelectCameraMode(PootleCarCameraMode.Warthog);
            float initial = car.Body.eulerAngles.y, view = car.CameraYaw;
            yield return null; yield return null;
            Assert.That(car.Controller.getSteering(), Is.GreaterThan(.3f), "Warthog mode steers toward look heading with only RT held");
            yield return new WaitForSeconds(.7f);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(car.Body.eulerAngles.y, view)), Is.LessThan(Mathf.Abs(Mathf.DeltaAngle(initial, view))));
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(view, car.CameraYaw)), Is.LessThan(.1f), "Car turning must not pull the aim camera away");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator Car_MouseOrbitAndObstructionProtectionWorkInTheNewModes()
        {
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
                yield return EnterCameraTestCar(car);
                car.SelectCameraMode(PootleCarCameraMode.CameraRelative);
                float yaw = car.CameraYaw, pitch = car.CameraPitch;
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100, -60), buttons = 2 });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSeconds(.25f);
                Assert.That(Mathf.DeltaAngle(yaw, car.CameraYaw), Is.GreaterThan(5));
                Assert.That(car.CameraPitch, Is.GreaterThan(pitch));
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Vector3 target = car.Body.position + car.lookOffset;
                Vector3 direction = (car.chaseCamera.transform.position - target).normalized;
                wall.transform.SetPositionAndRotation(target + direction * 3, Quaternion.LookRotation(direction));
                wall.transform.localScale = new Vector3(5, 5, .3f); Physics.SyncTransforms();
                foreach (var mode in new[] { PootleCarCameraMode.CameraRelative, PootleCarCameraMode.Warthog })
                {
                    car.SelectCameraMode(mode); yield return new WaitForSeconds(.15f);
                    Assert.That(Vector3.Distance(car.chaseCamera.transform.position, car.Body.position + car.lookOffset), Is.LessThan(3));
                }
                Object.Destroy(wall); yield return null;
                Assert.That(car.TryExit(), Is.True);
                Assert.That(car.chaseCamera.gameObject.activeSelf, Is.False);
            }
            finally { InputSystem.RemoveDevice(mouse); }
        }
    }
}
