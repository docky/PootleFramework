using NUnit.Framework;
using UnityEngine;

namespace Pootle.Tests
{
    public sealed class PootlePathInputZoneTests
    {
        GameObject go;
        PootlePathInputZone zone;
        [SetUp] public void Setup()
        {
            go = new GameObject("Experimental path test");
            zone = go.AddComponent<PootlePathInputZone>();
            zone.Volume.isTrigger = true; zone.Volume.size = new Vector3(4, 4, 12);
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(go);
        static Vector3 Direction(float yaw) => Quaternion.Euler(0, yaw, 0) * Vector3.forward;
        [TestCase(36)] [TestCase(40)] [TestCase(50)] [TestCase(54)]
        public void DigitalDiagonalsAlignBothWaysWithinTolerance(float angle)
        {
            go.transform.rotation = Quaternion.Euler(0, angle, 0);
            foreach (int sign in new[] { -1, 1 })
            {
                Assert.That(zone.TryAlign(Vector3.zero, Direction(45) * sign, true, true, out var result));
                Assert.That(Vector3.Angle(result, Direction(angle) * sign), Is.LessThan(.03f));
            }
        }
        [Test] public void AnalogAndAirborneInputRemainFreeByDefault()
        {
            go.transform.rotation = Quaternion.Euler(0, 40, 0);
            Assert.That(zone.TryAlign(Vector3.zero, Direction(45), false, true, out _), Is.False);
            Assert.That(zone.TryAlign(Vector3.zero, Direction(45), true, false, out _), Is.False);
            zone.digitalOnly = false; zone.groundedOnly = false;
            Assert.That(zone.TryAlign(Vector3.zero, Direction(45) * .4f, false, false, out var result));
            Assert.That(result.magnitude, Is.EqualTo(.4f).Within(.0001f));
        }
        [Test] public void ToleranceVolumeAndEnabledStateAreRespected()
        {
            go.transform.SetPositionAndRotation(new Vector3(10, 5, -3), Quaternion.Euler(0, 40, 0));
            Assert.That(zone.TryAlign(go.transform.position, Direction(53), true, true, out _), Is.False);
            Assert.That(zone.TryAlign(go.transform.position, Direction(52), true, true, out _), Is.True);
            Assert.That(zone.TryAlign(go.transform.TransformPoint(new Vector3(2.1f, 0, 0)), Direction(45), true, true, out _), Is.False);
            Assert.That(zone.TryAlign(go.transform.position + Vector3.up * 3.1f, Direction(45), true, true, out _), Is.False);
            Assert.That(zone.TryAlign(go.transform.position, Vector3.zero, true, true, out _), Is.False);
            zone.enabled = false;
            Assert.That(zone.TryAlign(go.transform.position, Direction(45), true, true, out _), Is.False);
        }
        [Test] public void AngularAndSpatialHysteresisKeepTheLatchUntilTheExitThreshold()
        {
            var state = new PootlePathInputAlignment();
            state.Align(Vector3.zero, Direction(11), true, true, .1f);
            Assert.That(state.ActiveZone, Is.SameAs(zone));
            foreach (float angle in new[] { 12.5f, 11.8f, 14.9f, 12.1f })
            {
                state.Align(new Vector3(2.2f, 0, 0), Direction(angle), true, true, .02f);
                Assert.That(state.ActiveZone, Is.SameAs(zone));
            }
            state.Align(Vector3.zero, Direction(15.1f), true, true, .02f);
            Assert.That(state.ActiveZone, Is.Null);
            state.Align(Vector3.zero, Direction(13), true, true, .02f);
            Assert.That(state.ActiveZone, Is.Null, "Must cross the entry angle again");
            state.Align(Vector3.zero, Direction(11), true, true, .02f);
            state.Align(new Vector3(2.31f, 0, 0), Direction(11), true, true, .02f);
            Assert.That(state.ActiveZone, Is.Null);
            state.Align(new Vector3(2.1f, 0, 0), Direction(11), true, true, .02f);
            Assert.That(state.ActiveZone, Is.Null, "Must enter the original box again");
        }
        [Test] public void EntryExitAndAngleReleaseBlendWithoutChangingInputStrength()
        {
            var state = new PootlePathInputAlignment();
            var input = Direction(10) * .6f;
            var first = state.Align(Vector3.zero, input, true, true, 1f / 60);
            Assert.That(Vector3.Angle(first, input), Is.InRange(.01f, 1f));
            Vector3 settled = first;
            for (int i = 0; i < 120; i++) settled = state.Align(Vector3.zero, input, true, true, 1f / 60);
            Assert.That(Vector3.Angle(settled, Vector3.forward), Is.LessThan(.03f));
            var outside = new Vector3(4, 0, 0);
            var leaving = state.Align(outside, input, true, true, 1f / 60);
            Assert.That(Vector3.Angle(leaving, settled), Is.LessThan(1));
            Assert.That(Vector3.Angle(leaving, input), Is.GreaterThan(8));
            for (int i = 0; i < 120; i++) leaving = state.Align(outside, input, true, true, 1f / 60);
            Assert.That(Vector3.Angle(leaving, input), Is.LessThan(.03f));
            Assert.That(leaving.magnitude, Is.EqualTo(.6f).Within(.00001f));
            for (int i = 0; i < 120; i++) state.Align(Vector3.zero, input, true, true, 1f / 60);
            float before = state.CorrectionDegrees;
            state.Align(Vector3.zero, Direction(16), true, true, 1f / 60);
            Assert.That(state.ActiveZone, Is.Null);
            Assert.That(Mathf.Abs(state.CorrectionDegrees - before), Is.LessThan(1));
        }
        [Test] public void ReadersAreIndependentAndAnalogAirborneNeutralAndResetDoNotRetainAssistance()
        {
            var a = new PootlePathInputAlignment(); var b = new PootlePathInputAlignment();
            var input = Direction(10);
            for (int i = 0; i < 60; i++) a.Align(Vector3.zero, input, true, true, 1f / 60);
            Assert.That(b.Align(new Vector3(4, 0, 0), input, true, true, .02f), Is.EqualTo(input));
            Assert.That(a.Align(Vector3.zero, input, false, true, .02f), Is.EqualTo(input));
            a.Align(Vector3.zero, input, true, true, .1f);
            Assert.That(a.Align(Vector3.zero, input, true, false, .02f), Is.EqualTo(input));
            a.Align(Vector3.zero, input, true, true, .1f);
            Assert.That(a.Align(Vector3.zero, Vector3.zero, true, true, .02f), Is.EqualTo(Vector3.zero));
            Assert.That(a.ActiveZone, Is.Null);
            a.Align(Vector3.zero, input, true, true, .1f); a.Reset();
            Assert.That(a.CorrectionDegrees, Is.Zero); Assert.That(a.ActiveZone, Is.Null);
        }
        [Test] public void OverlappingZonesKeepEqualPriorityLatchAndBlendIntoHigherPriorityPath()
        {
            var other = new GameObject("Second path");
            try
            {
                var second = other.AddComponent<PootlePathInputZone>(); second.Volume.isTrigger = true;
                second.transform.rotation = Quaternion.Euler(0, 20, 0);
                var state = new PootlePathInputAlignment();
                for (int i = 0; i < 60; i++) state.Align(Vector3.zero, Direction(8), true, true, 1f / 60);
                Assert.That(state.ActiveZone, Is.SameAs(zone));
                state.Align(Vector3.zero, Direction(12), true, true, .02f);
                Assert.That(state.ActiveZone, Is.SameAs(zone));
                float previous = state.CorrectionDegrees; second.priority = 1;
                state.Align(Vector3.zero, Direction(12), true, true, .02f);
                Assert.That(state.ActiveZone, Is.SameAs(second));
                Assert.That(Mathf.Abs(state.CorrectionDegrees - previous), Is.LessThan(2));
            }
            finally { Object.DestroyImmediate(other); }
        }
        [Test] public void BlendIsComparableAtThirtyAndOneTwentyFps()
        {
            var a = new PootlePathInputAlignment(); var b = new PootlePathInputAlignment();
            for (int i = 0; i < 9; i++) a.Align(Vector3.zero, Direction(10), true, true, 1f / 30);
            for (int i = 0; i < 36; i++) b.Align(Vector3.zero, Direction(10), true, true, 1f / 120);
            Assert.That(a.CorrectionDegrees, Is.EqualTo(b.CorrectionDegrees).Within(.05f));
        }
        [Test] public void CameraRelativeInputIsConvertedBeforePathAlignment()
        {
            var camera = new GameObject("Rotated camera");
            try
            {
                camera.transform.rotation = Quaternion.Euler(45, 90, 0);
                go.transform.rotation = Quaternion.Euler(0, 40, 0);
                var world = PootleMovement.CameraDirection(new Vector2(-1, 1), camera.transform, out _);
                var result = PootlePathInputZone.Align(Vector3.zero, world, true, true);
                Assert.That(Vector3.Angle(result, Direction(40)), Is.LessThan(.03f));
                go.SetActive(false);
                Assert.That(PootlePathInputZone.Align(Vector3.zero, world, true, true), Is.EqualTo(world));
            }
            finally { Object.DestroyImmediate(camera); }
        }
    }
}
