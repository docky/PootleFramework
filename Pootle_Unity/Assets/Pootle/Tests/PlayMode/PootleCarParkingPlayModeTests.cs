using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator Car_AirborneDismountKeepsTheJumpArcAndParksAfterLanding()
        {
            var cars = new[]
            {
                GameObject.Find("Pootle Sedan").GetComponent<PootleCar>(),
                GameObject.Find("Pootle Pickup").GetComponent<PootleCar>(),
                FirstBus().GetComponent<PootleCar>()
            };
            float clearedPositionX = 12;
            foreach (var car in cars)
            {
                var bus = car.GetComponent<PootleBusPlatform>();
                if (bus)
                {
                    character.Teleport(car.Body.TransformPoint(new Vector3(-3.5f, .05f, 3.7f)), Quaternion.LookRotation(car.Body.right));
                    yield return new WaitForSeconds(.2f);
                }
                else yield return ApproachCar(car);
                Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
                yield return null;
                car.Controller.immobilize(); car.Controller.setPosition(new Vector3(0, 1.2f, -22));
                car.Controller.getCarRigidBody().linearVelocity = Vector3.up * 3;
                foreach (var wheel in car.Controller.wheels) wheel.getRigidbody().linearVelocity = Vector3.up * 3;
                yield return PressCarButton(GamepadButton.East);
                Assert.That(car.IsOccupied, Is.False);
                Assert.That(car.IsParked, Is.False, "Dismounting in the air must not apply a world-space parking lock");
                Assert.That(car.Controller.getCarRigidBody().constraints, Is.EqualTo(RigidbodyConstraints.None));
                Assert.That(car.Controller.getCarRigidBody().linearVelocity.y, Is.GreaterThan(0), "The upward jump velocity survives dismounting");
                if (bus) Assert.That(bus.StairsDeployed, Is.False, "Airborne buses must not unfold the access stairs");
                float height = car.Body.position.y;
                yield return new WaitForSeconds(.15f);
                Assert.That(car.Body.position.y, Is.GreaterThan(height + .05f), "The vehicle must continue its arc before falling");
                float deadline = Time.time + 4;
                while (!car.IsParked && Time.time < deadline) yield return null;
                Assert.That(car.IsParked, Is.True);
                Assert.That(car.Body.position.y, Is.LessThan(.2f), "The vehicle lands instead of hovering");
                Assert.That(car.Controller.getCarRigidBody().constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
                if (bus) { yield return null; Assert.That(bus.StairsDeployed, Is.True); }
                // Move the checked vehicle aside before testing the next one's fall.
                car.Controller.setPosition(car.Body.position + Vector3.right * clearedPositionX);
                clearedPositionX += 12;
            }
        }

        [UnityTest]
        public IEnumerator Car_RemovingSupportReleasesTheParkedVehicleToGravity()
        {
            var car = GameObject.Find("Pootle Sedan").GetComponent<PootleCar>();
            yield return new WaitForSeconds(.5f);
            Assert.That(car.IsParked, Is.True);
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Temporary parked car support";
            platform.transform.position = new Vector3(0, 2, -22);
            platform.transform.localScale = new Vector3(5, .2f, 5);
            car.Controller.setPosition(new Vector3(0, 2.11f, -22)); Physics.SyncTransforms();
            yield return new WaitForSeconds(.1f);
            Assert.That(car.IsParked, Is.True);
            Object.Destroy(platform); yield return null;
            yield return new WaitForSeconds(.25f);
            Assert.That(car.IsParked, Is.False);
            Assert.That(car.Body.position.y, Is.LessThan(1.95f), "A parked vehicle cannot remain anchored after its support disappears");
            float deadline = Time.time + 4;
            while (!car.IsParked && Time.time < deadline) yield return null;
            Assert.That(car.IsParked, Is.True);
            Assert.That(car.Body.position.y, Is.LessThan(.2f));
        }
    }
}
