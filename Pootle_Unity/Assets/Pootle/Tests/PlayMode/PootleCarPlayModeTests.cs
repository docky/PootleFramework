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
        [UnityTest]
        public IEnumerator Car_BothVehiclesUseAnalogueRTAccelerationAndLTBrake()
        {
            foreach (string name in new[] { "Pootle Sedan", "Pootle Pickup" })
            {
                var car = GameObject.Find(name).GetComponent<PootleCar>();
                Assert.That(car, Is.Not.Null);
                yield return ApproachCar(car);
                Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                yield return new WaitForSeconds(.1f);
                Assert.That(car.Controller.getMotor(), Is.Zero, "Forward stick is not an accelerator");
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = .4f, leftTrigger = .25f, leftStick = Vector2.right * .3f });
                yield return null; yield return null;
                Assert.That(car.Controller.getMotor(), Is.EqualTo(.4f).Within(.02f));
                Assert.That(car.Controller.getBrake(), Is.EqualTo(.25f).Within(.02f));
                Assert.That(car.Controller.getSteering(), Is.EqualTo(gamepad.leftStick.ReadValue().x).Within(.02f), "Steering preserves the Input System stick dead zone");
                Vector3 start = car.Body.position;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return new WaitForSeconds(.5f);
                Assert.That(Vector3.Distance(start, car.Body.position), Is.GreaterThan(.3f));
                float speed = car.Controller.getCarRigidBody().linearVelocity.magnitude;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 });
                yield return new WaitForSeconds(.4f);
                Assert.That(car.Controller.getMotor(), Is.Zero);
                Assert.That(car.Controller.getBrake(), Is.EqualTo(1));
                Assert.That(car.Controller.getCarRigidBody().linearVelocity.magnitude, Is.LessThan(speed * .5f));
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.down });
                yield return null; yield return null;
                Assert.That(car.Controller.getMotor(), Is.EqualTo(-1));
                yield return PressCarButton(GamepadButton.East);
                Assert.That(car.IsOccupied, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator Car_PickupHasScaledMaterialsAbilitiesAndUsableDemoColliders()
        {
            var truck = GameObject.Find("Pootle Pickup").GetComponent<PootleCar>();
            Assert.That(truck.transform.localScale, Is.EqualTo(Vector3.one * 1.7f));
            Assert.That(truck.extraAirJumps, Is.EqualTo(4)); Assert.That(truck.BoostCharges, Is.EqualTo(4));
            foreach (var renderer in truck.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials) Assert.That(material.shader.name, Does.StartWith("Universal Render Pipeline/"));
            foreach (string name in new[] { "Demo race ramp", "Demo quarter pipe", "Demo mound" })
            {
                var prop = GameObject.Find(name); Assert.That(prop, Is.Not.Null);
                Assert.That(prop.transform.localScale, Is.EqualTo(Vector3.one * 1.7f));
                Assert.That(prop.GetComponentsInChildren<Collider>().Length, Is.GreaterThan(0));
                foreach (var renderer in prop.GetComponentsInChildren<Renderer>())
                    foreach (var material in renderer.sharedMaterials) Assert.That(material.shader.name, Does.StartWith("Universal Render Pipeline/"));
                Assert.That(Physics.Raycast(prop.GetComponentInChildren<Renderer>().bounds.center + Vector3.up * 20, Vector3.down, out var hit, 30), Is.True);
                Assert.That(hit.collider.transform.IsChildOf(prop.transform), Is.True, "The authored ramp surface must be driveable");
            }
            yield return ApproachCar(truck);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
            yield return new WaitForSeconds(.1f);
            Assert.That(truck.TryJump(), Is.True); yield return new WaitForSeconds(.2f);
            Assert.That(truck.AirJumpsRemaining, Is.EqualTo(4));
            Assert.That(truck.Body.position.y, Is.GreaterThan(.4f));
            Assert.That(truck.TryBoost(), Is.True); Assert.That(truck.BoostCharges, Is.EqualTo(3));
            truck.enabled = false;
            Assert.That(character.gameObject.activeSelf, Is.True);
        }

        IEnumerator PressCarButton(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
        }

        [UnityTest]
        public IEnumerator Car_JumpAllowsFourAirJumpsThenRefillsOnlyOnLanding()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
            yield return ApproachCar(car);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return new WaitForSeconds(.25f);
            Assert.That(car.Body.position.y, Is.GreaterThan(.5f));
            Assert.That(car.AirJumpsRemaining, Is.EqualTo(4), "Ground jump and holding A must not spend air jumps");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            for (int remaining = 3; remaining >= 0; remaining--)
            {
                yield return PressCarButton(GamepadButton.South);
                Assert.That(car.AirJumpsRemaining, Is.EqualTo(remaining));
                Assert.That(car.Controller.getCarRigidBody().linearVelocity.y, Is.GreaterThan(5));
                foreach (var wheel in car.Controller.wheels)
                    Assert.That(wheel.getRigidbody().linearVelocity.y, Is.GreaterThan(4), "Suspension must jump with the car");
                yield return new WaitForSeconds(.18f);
            }
            Assert.That(car.TryJump(), Is.False, "Fifth air jump must be rejected");
            float deadline = Time.time + 6;
            while (car.AirJumpsRemaining == 0 && Time.time < deadline) yield return null;
            Assert.That(car.AirJumpsRemaining, Is.EqualTo(4));
            Assert.That(car.Body.position.y, Is.LessThan(.3f), "Refill requires landing, not the apex or stale wheel contacts");
            Assert.That(car.TryJump(), Is.True);
            Assert.That(car.AirJumpsRemaining, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator Car_FourBoostsRechargeWhileParkedWithoutEntryRefill()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
            Assert.That(car.BoostCharges, Is.EqualTo(4));
            Assert.That(car.TryBoost(), Is.False, "An unoccupied car cannot spend charges");
            yield return ApproachCar(car);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return new WaitForSeconds(.15f);
            Assert.That(car.BoostCharges, Is.EqualTo(3), "Holding RB consumes only one charge");
            Assert.That(car.IsBoosting, Is.True);
            Assert.That(car.Controller.getCarRigidBody().linearVelocity.z, Is.GreaterThan(4));
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            for (int remaining = 2; remaining >= 0; remaining--)
            {
                yield return PressCarButton(GamepadButton.RightShoulder);
                Assert.That(car.BoostCharges, Is.EqualTo(remaining));
            }
            Assert.That(car.TryBoost(), Is.False);
            Assert.That(car.TryExit(), Is.True);
            Assert.That(car.IsBoosting, Is.False);
            Assert.That(car.Controller.maxSpeedMultiplier, Is.EqualTo(1));
            Assert.That(car.Controller.accelerationMultiplier, Is.EqualTo(1));
            yield return ApproachCar(car);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
            Assert.That(car.BoostCharges, Is.Zero, "Re-entering must not refill the boost tank");
            Assert.That(car.TryExit(), Is.True);
            yield return new WaitForSeconds(car.boostRechargeSeconds);
            Assert.That(car.BoostCharges, Is.EqualTo(1), "Charges recharge one at a time");
            yield return new WaitForSeconds(car.boostRechargeSeconds * 3);
            Assert.That(car.BoostCharges, Is.EqualTo(4));
            yield return new WaitForSeconds(.2f);
            Assert.That(car.BoostCharges, Is.EqualTo(4), "Recharge is capped at four");
            Assert.That(car.Controller.getCarRigidBody().linearVelocity.sqrMagnitude, Is.LessThan(.001f));
        }

        [UnityTest]
        public IEnumerator Car_KeyboardJumpAndAirBoostExpireCleanly()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
                yield return ApproachCar(car);
                Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
                yield return new WaitForSeconds(.2f);
                Assert.That(car.Body.position.y, Is.GreaterThan(.5f));
                Assert.That(car.Controller.getBrake(), Is.Zero);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                yield return new WaitForSeconds(.15f);
                Assert.That(car.IsBoosting, Is.True);
                Assert.That(car.BoostCharges, Is.EqualTo(3));
                Assert.That(car.Controller.getCarRigidBody().linearVelocity.z, Is.GreaterThan(4));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSeconds(car.boostDuration);
                Assert.That(car.IsBoosting, Is.False);
                Assert.That(car.Controller.maxSpeedMultiplier, Is.EqualTo(1));
                Assert.That(car.Controller.accelerationMultiplier, Is.EqualTo(1));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                yield return null; yield return null;
                Assert.That(car.Controller.getBrake(), Is.EqualTo(1));
                car.enabled = false;
                Assert.That(character.gameObject.activeSelf, Is.True);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        IEnumerator ApproachCar(PootleCar car)
        {
            var destination = car.Body.position - car.Body.right * 1.7f;
            destination.y = .06f;
            character.Teleport(destination, Quaternion.LookRotation(car.Body.right));
            yield return new WaitForSeconds(.2f);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().Focused, Is.EqualTo(car.interaction));
            Assert.That(character.GetComponent<PootleFrobjectSensor>().CanInteract, Is.True);
        }

        [UnityTest]
        public IEnumerator Car_XEntersTriggerDrivesBStopsExitsAndCanReenter()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>(); Assert.That(car, Is.Not.Null);
            yield return ApproachCar(car);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.West));
            yield return null; yield return null;
            Assert.That(car.Driver, Is.EqualTo(character));
            Assert.That(character.gameObject.activeSelf, Is.False);
            Assert.That(car.interaction.interactable, Is.False);
            var brain = character.outputCamera.GetComponent<CinemachineBrain>();
            Vector3 start = car.Body.position;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.25f, 0), rightTrigger = 1 });
            yield return new WaitForSeconds(.65f);
            Assert.That(Vector3.Distance(start, car.Body.position), Is.GreaterThan(.5f));
            Assert.That(car.Controller.getSteering(), Is.GreaterThan(.1f));
            Assert.That(brain.ActiveVirtualCamera, Is.EqualTo(car.chaseCamera));
            Assert.That(Vector3.Dot(car.chaseCamera.transform.position - car.Body.position, car.Body.forward), Is.LessThan(0));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East));
            yield return null; yield return null;
            Assert.That(car.IsOccupied, Is.False);
            Assert.That(character.gameObject.activeSelf, Is.True);
            Assert.That(car.chaseCamera.gameObject.activeSelf, Is.False);
            Assert.That(car.Controller.getCarRigidBody().linearVelocity.magnitude, Is.LessThan(.01f));
            Assert.That(character.Motor.CanOccupy(character.transform.position), Is.True);
            Vector3 parked = car.Body.position;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(parked, car.Body.position), Is.LessThan(.01f));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return ApproachCar(car);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
            Assert.That(car.IsOccupied, Is.True);
            Assert.That(car.TryExit(), Is.True);
        }

        [UnityTest]
        public IEnumerator Car_ParkedIgnoresInputAndUsesScaledUrpSedan()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
            Assert.That(car.transform.localScale, Is.EqualTo(Vector3.one * 1.6f));
            Assert.That(car.GetComponentsInChildren<Transform>(), Has.Some.Matches<Transform>(t => t.name == "Sedan"));
            foreach (var renderer in car.GetComponentsInChildren<Renderer>())
                foreach (var material in renderer.sharedMaterials)
                { Assert.That(material, Is.Not.Null); Assert.That(material.shader.name, Does.StartWith("Universal Render Pipeline/")); }
            yield return new WaitForSeconds(.5f);
            Vector3 start = car.Body.position;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
            yield return new WaitForSeconds(.3f);
            Assert.That(car.Controller.getMotor(), Is.Zero);
            Assert.That(Vector3.Distance(start, car.Body.position), Is.LessThan(.01f));
            Assert.That(car.IsOccupied, Is.False);
            Assert.That(car.chaseCamera.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator Car_BlockedExitStaysSeatedThenDisableRestoresPlayer()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
            yield return ApproachCar(car);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Blocked car exits test";
            blocker.transform.position = car.Body.position + Vector3.up * 2;
            blocker.transform.localScale = new Vector3(9, 5, 9);
            Physics.SyncTransforms();
            Assert.That(car.TryExit(), Is.False);
            Assert.That(car.IsOccupied, Is.True);
            Assert.That(character.gameObject.activeSelf, Is.False);
            Object.Destroy(blocker); yield return null;
            car.enabled = false;
            Assert.That(character.gameObject.activeSelf, Is.True);
            Assert.That(car.IsOccupied, Is.False);
            Assert.That(car.chaseCamera.gameObject.activeSelf, Is.False);
            Assert.That(character.Motor.CanOccupy(character.transform.position), Is.True);
            Assert.That(car.interaction.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator Car_KeyboardEEntersWasdDrivesBExits()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
                yield return ApproachCar(car);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
                yield return null; yield return null;
                Assert.That(car.IsOccupied, Is.True);
                Vector3 start = car.Body.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(.4f);
                Assert.That(Vector3.Distance(start, car.Body.position), Is.GreaterThan(.2f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.B));
                yield return null; yield return null;
                Assert.That(car.IsOccupied, Is.False);
                Assert.That(character.gameObject.activeSelf, Is.True);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }
    }
}
