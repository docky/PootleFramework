using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        readonly List<PootleFrobject> frobjectTargets = new List<PootleFrobject>();
        static void Lifecycle(Component component, string method) => component.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);
        [TearDown]
        public void UnregisterFrobjectTestTargets()
        {
            // Removal only touches the managed registry, even if the common teardown already destroyed the object.
            foreach (var target in frobjectTargets) Lifecycle(target, "OnDisable");
            frobjectTargets.Clear();
        }
        PootleFrobject Frobject(Vector3 offset)
        {
            var go = new GameObject("Test Frobject"); objects.Add(go); go.transform.position = origin + offset;
            var target = go.AddComponent<PootleFrobject>();
            // EditMode does not run ordinary MonoBehaviour lifecycle messages.
            Lifecycle(target, "OnEnable");
            frobjectTargets.Add(target);
            return target;
        }
        PootleFrobjectSensor Sensor()
        {
            var sensor = character.gameObject.AddComponent<PootleFrobjectSensor>(); Lifecycle(sensor, "Awake"); return sensor;
        }

        [Test]
        public void Frobject_AwarenessIsOmnidirectionalAndFocusNeedsDistanceAndFacing()
        {
            var sensor = Sensor(); var far = Frobject(new Vector3(0, 0, 6)); var behind = Frobject(new Vector3(0, 0, -2));
            sensor.Refresh(); Assert.That(sensor.Aware.Count, Is.EqualTo(2)); Assert.That(sensor.Focused, Is.Null);
            Assert.That(sensor.GetState(far), Is.EqualTo(PootleFrobjectState.Aware));
            var near = Frobject(new Vector3(0, 0, 2)); sensor.Refresh();
            Assert.That(sensor.Focused, Is.SameAs(near)); Assert.That(sensor.GetState(near), Is.EqualTo(PootleFrobjectState.Ready));
            near.transform.position += Vector3.right * 4; sensor.Refresh(); Assert.That(sensor.Focused, Is.Null);
            behind.transform.position += Vector3.back * 10; sensor.Refresh(); Assert.That(sensor.GetState(behind), Is.EqualTo(PootleFrobjectState.Hidden));
        }
        [Test]
        public void Frobject_SelectsOneBestFacingTargetAndDispatchesOnlyOnce()
        {
            var sensor = Sensor(); var ahead = Frobject(new Vector3(0, 0, 2)); var side = Frobject(new Vector3(1, 0, 1));
            int first = 0, second = 0; ahead.onInteract.AddListener(() => first++); side.onInteract.AddListener(() => second++);
            sensor.Refresh(); Assert.That(sensor.Focused, Is.SameAs(ahead)); Assert.That(sensor.TryInteract(), Is.True);
            Assert.That(first, Is.EqualTo(1)); Assert.That(second, Is.Zero);
        }
        [Test]
        public void Frobject_ReadyRequiresGroundedSlowEnabledPlayerAndAvailableTarget()
        {
            var sensor = Sensor(); var target = Frobject(new Vector3(0, 0, 2)); sensor.Refresh(); Assert.That(sensor.CanInteract);
            sensor.interactionEnabled = false; Assert.That(sensor.TryInteract(), Is.False); Assert.That(sensor.GetState(target), Is.EqualTo(PootleFrobjectState.Aware));
            sensor.interactionEnabled = true; Step(8, new PootleInputFrame { move = Vector2.up }); Assert.That(sensor.PlayerReady, Is.False);
            ResetAt(Vector3.up * 2); sensor.Refresh(); Assert.That(sensor.TryInteract(), Is.False);
            ResetAt(Vector3.zero); Step(2); target.interactable = false; sensor.Refresh(); Assert.That(sensor.CanInteract, Is.False);
            Assert.That(sensor.GetState(target), Is.EqualTo(PootleFrobjectState.Aware));
        }
        [Test]
        public void Frobject_WallOcclusionLayersAndDisableClearTarget()
        {
            var sensor = Sensor(); var target = Frobject(new Vector3(0, 0, 2));
            var wall = Box("Frobject wall", new Vector3(0, 1, 1), new Vector3(2, 2, .2f)); Physics.SyncTransforms();
            sensor.Refresh(); Assert.That(sensor.GetState(target), Is.EqualTo(PootleFrobjectState.Hidden)); Assert.That(sensor.TryInteract(), Is.False);
            wall.SetActive(false); Physics.SyncTransforms(); sensor.Refresh(); Assert.That(sensor.CanInteract);
            sensor.targetLayers = 0; sensor.Refresh(); Assert.That(sensor.Focused, Is.Null);
            sensor.targetLayers = ~0; sensor.Refresh(); target.enabled = false;
            Assert.That(sensor.TryInteract(), Is.False); Assert.That(sensor.Aware, Is.Empty);
            target.enabled = true; sensor.Refresh(); Assert.That(sensor.CanInteract);
            sensor.enabled = false; Lifecycle(sensor, "OnDisable"); Assert.That(sensor.Focused, Is.Null); Assert.That(sensor.GetState(target), Is.EqualTo(PootleFrobjectState.Hidden));
        }
        [Test]
        public void Frobject_SharedJumpPressInteractsWithoutJumpOrBufferedJump()
        {
            Sensor(); var target = Frobject(new Vector3(0, 0, 2)); int calls = 0; target.onInteract.AddListener(() => calls++);
            Step(1, new PootleInputFrame { interactPressed = true, jumpPressed = true, jumpHeld = true });
            Assert.That(calls, Is.EqualTo(1)); Assert.That(character.IsGrounded); Step(10, new PootleInputFrame { jumpHeld = true }); Assert.That(character.IsGrounded);
            target.gameObject.SetActive(false);
            Step(1, new PootleInputFrame { interactPressed = true, jumpPressed = true, jumpHeld = true }); Assert.That(character.IsGrounded, Is.False);
        }
        [Test]
        public void Frobject_CooldownPreventsRepeatedInteractionAndCallbacksMayDisableTarget()
        {
            var sensor = Sensor(); var target = Frobject(new Vector3(0, 0, 2)); int calls = 0; target.onInteract.AddListener(() => calls++);
            Assert.That(sensor.TryInteract()); Assert.That(sensor.TryInteract(), Is.False); Assert.That(calls, Is.EqualTo(1));
            target.enabled = false; target.enabled = true; Lifecycle(target, "OnEnable"); target.onInteract.AddListener(() => target.gameObject.SetActive(false));
            Assert.That(sensor.TryInteract()); Assert.That(sensor.Focused, Is.Null); Assert.That(sensor.Aware, Is.Empty);
        }
    }
}
