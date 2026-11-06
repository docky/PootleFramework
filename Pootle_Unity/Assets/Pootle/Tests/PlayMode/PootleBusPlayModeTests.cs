using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        PootleBusPlatform FirstBus() => Object.FindObjectsByType<PootleBusPlatform>().First(b => b.fleetNumber == 1);

        IEnumerator WalkBusRoute(Vector3 target)
        {
            float dt = 1f / 60;
            int ticks = 0;
            for (; ticks < 360; ticks++)
            {
                Vector3 delta = Vector3.ProjectOnPlane(target - character.transform.position, Vector3.up);
                if (delta.magnitude < .09f) break;
                Vector3 direction = delta.normalized;
                character.Tick(dt, new PootleInputFrame { move = new Vector2(direction.x, direction.z) * .65f });
                yield return null;
            }
            for (int i = 0; i < 12; i++) { character.Tick(dt, default); yield return null; }
            Assert.That(ticks, Is.LessThan(360), "Lily must walk the stairs without jumping or teleporting: " + target + " actual " + character.transform.position);
            Assert.That(character.IsGrounded, Is.True);
            Assert.That(character.transform.position.y, Is.EqualTo(target.y).Within(.35f), "At a landing the capsule can already step onto the next tread");
        }

        [UnityTest]
        public IEnumerator Bus_FourPrimitiveVertexColorVehiclesAreStableWalkablePlatforms()
        {
            var buses = Object.FindObjectsByType<PootleBusPlatform>();
            Assert.That(buses.Length, Is.EqualTo(4));
            Assert.That(buses.Select(b => b.fleetNumber).Distinct().Count(), Is.EqualTo(4));
            yield return new WaitForSeconds(.5f);
            foreach (var bus in buses)
            {
                var car = bus.GetComponent<PootleCar>();
                Assert.That(car.Controller.wheels.Length, Is.EqualTo(6));
                Assert.That(bus.StairsDeployed, Is.True);
                Assert.That(car.Controller.getCarRigidBody().constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
                foreach (var renderer in bus.GetComponentsInChildren<MeshRenderer>())
                {
                    Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Pootle/Vertex Color Lit"));
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount));
                    Assert.That(mesh.colors.Distinct().Count(), Is.GreaterThan(1));
                }
                Assert.That(bus.roofPoint.position.y, Is.GreaterThan(5));
                Assert.That(bus.destinationLanding.position.y, Is.GreaterThan(bus.roofPoint.position.y + .8f));
            }
            var first = FirstBus(); var body = first.GetComponent<PootleCar>().Body;
            character.Teleport(first.roofPoint.position + Vector3.up * .06f, Quaternion.identity);
            Vector3 position = body.position;
            yield return new WaitForSeconds(.8f);
            Assert.That(character.IsGrounded, Is.True);
            Assert.That(character.Support.collider.transform.IsChildOf(body), Is.True);
            Assert.That(Vector3.Distance(position, body.position), Is.LessThan(.005f), "Player weight must not move a parked bus");
        }

        [UnityTest]
        public IEnumerator Bus_WalkStairsToRoofThenJumpToTheHighLanding()
        {
            var bus = FirstBus(); var car = bus.GetComponent<PootleCar>();
            yield return new WaitForSeconds(.5f);
            character.simulateAutomatically = false;
            var camera = character.outputCamera; character.outputCamera = null;
            try
            {
                character.Teleport(bus.stairWaypoints[0].position + Vector3.up * .04f, Quaternion.LookRotation(car.Body.forward));
                for (int i = 0; i < 12; i++) { character.Tick(1f / 60, default); yield return null; }
                foreach (var target in bus.stairWaypoints.Skip(1)) yield return WalkBusRoute(target.position);
                Assert.That(character.Support.collider.transform.IsChildOf(car.Body), Is.True);
                yield return WalkBusRoute(car.Body.TransformPoint(new Vector3(0, 5.45f, 3.6f)));
                Vector3 targetLanding = bus.destinationLanding.position;
                for (int i = 0; i < 180; i++)
                {
                    Vector3 delta = Vector3.ProjectOnPlane(targetLanding - character.transform.position, Vector3.up);
                    Vector3 direction = delta.magnitude > .16f ? delta.normalized * .85f : Vector3.zero;
                    character.Tick(1f / 60, new PootleInputFrame { move = new Vector2(direction.x, direction.z), jumpPressed = i == 0, jumpHeld = i < 30 });
                    yield return null;
                    if (i > 10 && character.IsGrounded && character.Support.collider && character.Support.collider.transform.IsChildOf(bus.destinationLanding.parent)) break;
                }
                Assert.That(character.IsGrounded, Is.True);
                Assert.That(character.Support.collider.transform.IsChildOf(bus.destinationLanding.parent), Is.True, "The bus roof must enable reaching the higher stop");
                Assert.That(character.transform.position.y, Is.GreaterThan(6.3f));
            }
            finally { character.outputCamera = camera; character.simulateAutomatically = true; }
        }

        [UnityTest]
        public IEnumerator Bus_DrivesStowsItsStairsThenExitsAndParksSafely()
        {
            var bus = FirstBus(); var car = bus.GetComponent<PootleCar>();
            character.Teleport(car.Body.TransformPoint(new Vector3(-3.5f, .05f, 3.7f)), Quaternion.LookRotation(car.Body.right));
            yield return new WaitForSeconds(.2f);
            Assert.That(character.GetComponent<PootleFrobjectSensor>().TryInteract(), Is.True);
            yield return null; yield return null;
            Assert.That(bus.StairsDeployed, Is.False);
            Assert.That(car.IsOccupied, Is.True);
            car.Controller.immobilize(); car.Controller.setPosition(new Vector3(0, .02f, -25));
            Vector3 before = car.Body.position;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.8f);
            Assert.That(Vector3.Distance(before, car.Body.position), Is.GreaterThan(.5f));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 });
            yield return new WaitForSeconds(.3f);
            Assert.That(car.Controller.getBrake(), Is.EqualTo(1));
            yield return PressCarButton(GamepadButton.East);
            Assert.That(car.IsOccupied, Is.False); Assert.That(bus.StairsDeployed, Is.True);
            Assert.That(character.gameObject.activeSelf, Is.True);
            Assert.That(character.Motor.CanOccupy(character.transform.position), Is.True, "Unfolding stairs must not trap the exiting player");
            Assert.That(car.Controller.getCarRigidBody().constraints, Is.EqualTo(RigidbodyConstraints.FreezeAll));
            Vector3 parked = car.Body.position;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.25f);
            Assert.That(Vector3.Distance(parked, car.Body.position), Is.LessThan(.005f));
        }
    }
}
