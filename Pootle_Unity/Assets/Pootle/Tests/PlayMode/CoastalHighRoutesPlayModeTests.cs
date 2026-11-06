using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Pootle.Tests
{
    public sealed class CoastalHighRoutesPlayModeTests
    {
        PootleCharacter player;
        PootleCaveTravel travel;
        PootleZoneCameraDirector director;

        [UnitySetUp]
        public IEnumerator Setup()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/CoastalHike/CoastalHike.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor prototype scene checks"); yield break;
#endif
            player = Object.FindAnyObjectByType<PootleCharacter>();
            travel = Object.FindAnyObjectByType<PootleCaveTravel>();
            director = Object.FindAnyObjectByType<PootleZoneCameraDirector>();
            player.simulateAutomatically = false; player.outputCamera = null;
            yield return null;
        }

        void TickToward(Vector3 target, float dt)
        {
            var delta = Vector3.ProjectOnPlane(target - player.transform.position, Vector3.up).normalized;
            player.Tick(dt, new PootleInputFrame { move = new Vector2(delta.x, delta.z) * .65f });
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator ViewTiltRotatesTheAvatarThroughItsParentPivot()
        {
            var tilt = player.GetComponentInChildren<PootleViewTilt>();
            var animator = player.GetComponentInChildren<Animator>();
            Assert.That(animator.transform.IsChildOf(tilt.transform), Is.True, "The visible avatar must be a child of the tilt pivot");
            Assert.That(tilt.HasVisualChildren, Is.True);
            Assert.That(player.Motor.transform.IsChildOf(tilt.transform), Is.False);
            tilt.overridePolicy = true; tilt.limitToCameraElevation = false; tilt.tiltBackDegrees = 25;
            tilt.RestorePose(); var before = animator.transform.rotation;
            tilt.ApplyTilt();
            Assert.That(Quaternion.Angle(before, animator.transform.rotation), Is.EqualTo(25).Within(.1f));
            yield return null;
            Assert.That(tilt.AppliedTiltDegrees, Is.EqualTo(25).Within(.1f));
        }

        [UnityTest]
        public IEnumerator ClimbingUsesRockNormalAndDetachingBlendsBackToViewTilt()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                wall.name = "Tilted rock test";
                wall.transform.SetPositionAndRotation(new Vector3(1000, 4, 1001), Quaternion.Euler(10, 25, 0));
                wall.transform.localScale = new Vector3(6, 8, 1);
                wall.AddComponent<PootleClimbSurface>();
                var normal = -wall.transform.forward;
                var feet = wall.transform.TransformPoint(new Vector3(0, 0, -.5f)) + normal * (player.Motor.Radius + .04f) - player.Motor.center;
                var tilt = player.GetComponentInChildren<PootleViewTilt>();
                tilt.overridePolicy = true; tilt.tiltBackDegrees = 27.6f; tilt.limitToCameraElevation = false;
                player.Teleport(feet, Quaternion.LookRotation(Vector3.ProjectOnPlane(-normal, Vector3.up)));
                Physics.SyncTransforms();
                for (int i = 0; i < 3; i++) player.Tick(1f / 60, new PootleInputFrame { grip = true });
                Assert.That(player.Mode, Is.EqualTo(PootleMode.Climbing));
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    tilt.viewCamera.transform.rotation = Quaternion.Euler(60, yaw, 0);
                    tilt.TickTilt(1f / 60);
                    Assert.That(tilt.AppliedTiltDegrees, Is.Zero);
                    Assert.That(Vector3.Angle(tilt.transform.forward, -normal), Is.LessThan(.1f));
                }
                var attached = tilt.transform.localRotation;
                player.Tick(1f / 60, new PootleInputFrame { detachPressed = true });
                Assert.That(player.Mode, Is.Not.EqualTo(PootleMode.Climbing));
                tilt.TickTilt(0);
                Assert.That(Quaternion.Angle(attached, tilt.transform.localRotation), Is.LessThan(.05f));
                tilt.TickTilt(tilt.detachBlendTime * .5f);
                Assert.That(tilt.AppliedTiltDegrees, Is.EqualTo(13.8f).Within(.02f));
                tilt.TickTilt(tilt.detachBlendTime * .5f);
                Assert.That(tilt.AppliedTiltDegrees, Is.EqualTo(27.6f).Within(.02f));
                var completed = tilt.transform.rotation; tilt.ApplyTilt();
                Assert.That(Quaternion.Angle(completed, tilt.transform.rotation), Is.LessThan(.05f));
                player.Teleport(feet, Quaternion.LookRotation(Vector3.ProjectOnPlane(-normal, Vector3.up)));
                Physics.SyncTransforms();
                player.Tick(1f / 60, new PootleInputFrame { grip = true }); tilt.TickTilt(.01f);
                Assert.That(player.Mode, Is.EqualTo(PootleMode.Climbing));
                Assert.That(tilt.AppliedTiltDegrees, Is.Zero, "Reattaching must immediately cancel the return blend");
                yield return null;
                Assert.That(tilt.AppliedTiltDegrees, Is.Zero, "Actual LateUpdate must keep the climb pose");
            }
            finally { Object.Destroy(wall); }
        }

        [UnityTest]
        public IEnumerator EveryBridgeCanBeWalkedBothWaysWithoutJumping()
        {
            var root = GameObject.Find("07 · Mountain skywalk and summit hut");
            var routes = root.GetComponentsInChildren<Transform>().Where(t => t.name == "Walking route").ToArray();
            Assert.That(routes.Length, Is.EqualTo(6));
            foreach (var route in routes)
            foreach (bool reverse in new[] { false, true })
            {
                var points = route.Cast<Transform>().Select(t => t.position).ToArray();
                if (reverse) System.Array.Reverse(points);
                player.Teleport(points[0] + Vector3.up * .05f, Quaternion.identity);
                for (int i = 0; i < 20; i++) player.Tick(1f / 60, default);
                foreach (var point in points.Skip(1))
                {
                    int steps = 0;
                    while (Vector3.ProjectOnPlane(point - player.transform.position, Vector3.up).magnitude > .22f && steps++ < 300)
                        TickToward(point, 1f / 60);
                    Assert.That(steps, Is.LessThan(300), route.parent.name + " blocked at " + player.transform.position + ", target " + point);
                    Assert.That(player.transform.position.y, Is.EqualTo(point.y).Within(.45f), route.parent.name + " lost the deck");
                }
                Assert.That(player.IsGrounded, Is.True, route.parent.name);
                director.EvaluateAt(Time.time, false, true);
                Assert.That(director.ActiveZone, Is.Not.Null);
                Assert.That(director.ActiveZone.precedence, Is.GreaterThanOrEqualTo(65));
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator HutDoorEntersFurnishedInteriorAndReturnsWithoutBounce()
        {
            var enter = travel.portals.Single(p => p.name == "ENTER · Summit hut");
            var exit = travel.portals.Single(p => p.name == "EXIT · Summit hut");
            Assert.That(exit.transform.position - enter.transform.position, Is.EqualTo(new Vector3(0, 100, 0)));
            var opening = GameObject.Find("Summit hut · exterior").GetComponentsInChildren<Transform>().Single(t => t.name.StartsWith("Pitch-black doorway"));
            Assert.That(opening.GetComponent<Collider>(), Is.Null);
            Assert.That(opening.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(Color.black));
            player.Teleport(enter.transform.position + Vector3.back * 4 + Vector3.up * .08f, Quaternion.identity);
            foreach (var portal in new[] { enter, exit })
            {
                float deadline = Time.time + 8;
                while (!travel.Busy && Time.time < deadline) { TickToward(portal.transform.position, Time.deltaTime); yield return null; }
                Assert.That(travel.Busy, Is.True, "Door did not trigger: " + portal.name);
                bool sawBlack = false;
                deadline = Time.time + 8;
                while (travel.Busy && Time.time < deadline)
                {
                    sawBlack |= travel.iris.Openness == 0;
                    yield return null;
                }
                Assert.That(travel.Busy, Is.False); Assert.That(sawBlack, Is.True);
                Assert.That(player.transform.position.y > 100, Is.EqualTo(portal == enter));
                Assert.That(player.IsGrounded, Is.True);
                if (portal == enter)
                {
                    Assert.That(director.ActiveZone.name, Does.Contain("hut interior"));
                    Assert.That(Object.FindAnyObjectByType<PootleCaveLighting>().IsInside, Is.True);
                }
                int transfers = travel.TransferCount;
                yield return new WaitForSeconds(1.4f);
                Assert.That(travel.TransferCount, Is.EqualTo(transfers), "Arrival bounced back");
            }
            Assert.That(Object.FindAnyObjectByType<PootleCaveLighting>().IsInside, Is.False);
            Assert.That(travel.TransferCount, Is.EqualTo(2));
            Debug.Log("Verified summit hut: black doorway, furnished interior camera and lighting, return exit, no bounce.");
        }
    }
}
