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
