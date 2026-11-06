using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace Pootle.Tests
{
    public sealed class PootleCameraZoneTests
    {
        GameObject root;
        PootleZoneCameraDirector director;
        PootleCameraZone low, high;
        [SetUp] public void Setup()
        {
            root = new GameObject("Camera zone test");
            director = root.AddComponent<PootleZoneCameraDirector>();
            director.target = Child("Target").transform;
            director.fallbackCamera = Child("Fallback").AddComponent<CinemachineCamera>();
            low = MakeZone("Low", 1); high = MakeZone("High", 2);
            director.zones = new[] { low, high };
        }
        GameObject Child(string name) { var go = new GameObject(name); go.transform.SetParent(root.transform); return go; }
        PootleCameraZone MakeZone(string name, int priority)
        {
            var go = Child(name); var zone = go.AddComponent<PootleCameraZone>();
            go.GetComponent<BoxCollider>().size = Vector3.one * 4;
            zone.cameraRig = Child(name + " camera").AddComponent<CinemachineCamera>();
            zone.precedence = priority; zone.exitPadding = .75f; return zone;
        }
        [TearDown] public void Cleanup() => Object.DestroyImmediate(root);
        [Test] public void OverlapSelectsHighestPriorityAndFallsBackAfterTeleport()
        {
            director.Evaluate(); Assert.That(director.ActiveCamera, Is.EqualTo(high.cameraRig));
            director.target.position = Vector3.one * 20; director.Evaluate();
            Assert.That(director.ActiveCamera, Is.EqualTo(director.fallbackCamera));
            Assert.That(high.cameraRig.Priority.Value, Is.EqualTo(0));
        }
        [Test] public void ExitPaddingPreventsBoundaryChatterButDoesNotDelayHigherPriorityEntry()
        {
            high.transform.position = Vector3.right * 3;
            director.Evaluate(); Assert.That(director.ActiveZone, Is.EqualTo(low));
            director.target.position = Vector3.right * 1.1f; director.Evaluate();
            Assert.That(director.ActiveZone, Is.EqualTo(high));
            director.target.position = Vector3.right * .5f; director.Evaluate();
            Assert.That(director.ActiveZone, Is.EqualTo(high));
            director.target.position = Vector3.zero; director.Evaluate();
            Assert.That(director.ActiveZone, Is.EqualTo(low));
        }
        [Test] public void RotatedScaledBoundsAndDisabledZonesAreRespected()
        {
            high.transform.SetPositionAndRotation(new Vector3(5,2,7), Quaternion.Euler(0,43,0));
            high.transform.localScale = new Vector3(2,1,.5f);
            Assert.That(high.Contains(high.transform.TransformPoint(new Vector3(1.9f,0,1.9f))), Is.True);
            Assert.That(high.Contains(high.transform.TransformPoint(new Vector3(2.1f,0,0))), Is.False);
            director.target.position = high.transform.position; director.Evaluate();
            Assert.That(director.ActiveZone, Is.EqualTo(high));
            high.enabled = false; director.Evaluate();
            Assert.That(director.ActiveCamera, Is.EqualTo(director.fallbackCamera));
        }
    }
}
