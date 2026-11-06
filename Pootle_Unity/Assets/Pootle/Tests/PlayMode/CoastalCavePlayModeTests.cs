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
    public sealed class CoastalCavePlayModeTests
    {
        PootleCharacter player;
        PootleCaveTravel travel;
        PootleZoneCameraDirector director;
        [UnitySetUp] public IEnumerator Setup()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/CoastalHike/CoastalHike.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor prototype scene checks"); yield break;
#endif
            player = Object.FindAnyObjectByType<PootleCharacter>();
            travel = Object.FindAnyObjectByType<PootleCaveTravel>();
            director = Object.FindAnyObjectByType<PootleZoneCameraDirector>();
            Assert.That(travel, Is.Not.Null);
            player.simulateAutomatically = false; player.outputCamera = null;
            yield return null;
        }
        void TickToward(Vector3 point)
        {
            Vector3 delta = Vector3.ProjectOnPlane(point - player.transform.position, Vector3.up).normalized;
            player.Tick(Time.deltaTime, new PootleInputFrame { move = new Vector2(delta.x, delta.z) * .65f });
            Physics.SyncTransforms();
        }
        IEnumerator WalkUntilEntry(PootleCavePortal portal)
        {
            float deadline = Time.time + 7;
            while (!travel.Busy && Time.time < deadline) { TickToward(portal.transform.position); yield return null; }
            Assert.That(travel.Busy, Is.True, "Did not enter " + portal.name + " from " + player.transform.position);
        }
        IEnumerator FinishTravel(bool inside)
        {
            float deadline = Time.time + 8; bool sawBlack = false, sawOpening = false;
            while (travel.Busy && Time.time < deadline)
            {
                if (travel.Phase == CaveTravelPhase.Black) { sawBlack = true; Assert.That(travel.iris.Openness, Is.EqualTo(0)); }
                if (travel.Phase == CaveTravelPhase.Opening)
                {
                    sawOpening = true;
                    Assert.That(director.brain.IsBlending, Is.False, "The offset must be cut while black");
                    Assert.That(Camera.main.transform.position.y > 90, Is.EqualTo(inside));
                }
                yield return null;
            }
            Assert.That(travel.Busy, Is.False); Assert.That(sawBlack && sawOpening, Is.True);
            Assert.That(travel.iris.Openness, Is.EqualTo(1)); Assert.That(player.simulateAutomatically, Is.False);
            Assert.That(player.outputCamera, Is.Null); Assert.That(player.transform.position.y > 90, Is.EqualTo(inside));
        }
        [UnityTest] public IEnumerator AllDoorPairsAutomaticallyEnterAndExitWithoutBounce()
        {
            foreach (var enter in travel.portals.Where(p => !p.dropEntrance && p.name.StartsWith("ENTER")))
            {
                player.Teleport(enter.transform.position - enter.transform.forward * 4 + Vector3.up * .12f, enter.transform.rotation);
                yield return WalkUntilEntry(enter); yield return FinishTravel(true);
                int transfers = travel.TransferCount;
                yield return new WaitForSeconds(1.4f);
                Assert.That(travel.TransferCount, Is.EqualTo(transfers), "Arrival bounced through the exit");
                Assert.That(director.ActiveZone.name, Does.Contain("Cave"));
                var exit = travel.portals.Single(p => p.name == enter.name.Replace("ENTER", "EXIT"));
                yield return WalkUntilEntry(exit); yield return FinishTravel(false);
                Assert.That(player.IsGrounded, Is.True, enter.name + " exterior landing");
            }
        }
        [UnityTest] public IEnumerator PlateauHoleIsOpenAndDropsIntoTheRoom()
        {
            Assert.That(Physics.Raycast(new Vector3(12, 9, 36), Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore), Is.False, "A solid cone still covers the aperture");
            Assert.That(Physics.Raycast(new Vector3(14.3f, 9, 36), Vector3.down, out var rim, 3, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(rim.point.y, Is.EqualTo(7).Within(.1f));
            player.Teleport(new Vector3(14.5f, 7.1f, 36), Quaternion.LookRotation(Vector3.left));
            var hole = travel.portals.Single(p => p.dropEntrance);
            yield return WalkUntilEntry(hole);
            bool sawFall = false; float deadline = Time.time + 8;
            while (travel.Busy && Time.time < deadline)
            {
                if (player.transform.position.y > 100 && player.Velocity.y < -1) sawFall = true;
                yield return null;
            }
            Assert.That(sawFall, Is.True); Assert.That(travel.Busy, Is.False);
            Assert.That(player.IsGrounded, Is.True); Assert.That(player.transform.position.y, Is.EqualTo(103).Within(.1f));
            Assert.That(director.ActiveZone.name, Does.Contain("three-way chamber"));
            Assert.That(travel.TransferCount, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator ChamberConnectsThreeMainPaths()
        {
            foreach (var exit in travel.portals.Where(p => p.name.StartsWith("EXIT") && !p.name.Contains("Hidden") && !p.name.Contains("Summit hut")))
            {
                player.Teleport(new Vector3(9, 103.1f, 34), Quaternion.identity);
                Vector3 bend = exit.name.Contains("South") ? new Vector3(9, 103, 28) :
                    exit.name.Contains("East") ? new Vector3(14.5f, 103, 31) : new Vector3(2, 103, 34);
                float deadline = Time.time + 8;
                while (Vector3.ProjectOnPlane(bend - player.transform.position, Vector3.up).magnitude > .4f && Time.time < deadline)
                { TickToward(bend); yield return null; }
                Assert.That(Time.time, Is.LessThan(deadline), "Blocked chamber branch " + exit.name);
                yield return WalkUntilEntry(exit); yield return FinishTravel(false);
            }
        }
        [UnityTest] public IEnumerator SecretRoomIsDisconnectedAndOnlyHasOneDoor()
        {
            var room = GameObject.Find("Hidden fern room");
            Assert.That(room.GetComponentsInChildren<PootleCavePortal>().Length, Is.EqualTo(1));
            Assert.That(GameObject.Find("Side passage floor"), Is.Null);
            Assert.That(Physics.Raycast(new Vector3(17.5f, 104, 38), Vector3.down, 2, ~0, QueryTriggerInteraction.Ignore), Is.False, "No floor may bridge the caves");
            foreach (bool fromRoom in new[] { false, true })
            {
                player.Teleport(new Vector3(fromRoom ? 20 : 14, 103.1f, 38), Quaternion.identity);
                float deadline = Time.time + 2;
                while (Time.time < deadline)
                {
                    TickToward(new Vector3(fromRoom ? 14 : 22, 103, 38)); yield return null;
                }
                Assert.That(fromRoom ? player.transform.position.x > 18.5f : player.transform.position.x < 16.25f, Is.True, "Closed wall must block passage in both directions");
                Assert.That(travel.Busy, Is.False);
            }
            foreach (var name in new[] { "Fern room west sealed wall", "Fern room south cutaway", "Fern room north wall" })
                Assert.That(GameObject.Find(name).GetComponent<BoxCollider>().bounds.max.y, Is.GreaterThan(114), "Cutaway walls retain jump/glide collision");
            var fill = GameObject.Find("Tunnel · dim ambient fill").GetComponent<Light>();
            Assert.That(fill.enabled && fill.intensity > 0 && fill.range >= 12, Is.True);
        }
        [UnityTest] public IEnumerator BufferRequiresLeavingAndNetworkAlignmentIsExact()
        {
            var network = Object.FindAnyObjectByType<PootleCaveNetwork>();
            Assert.That(network.transform.position, Is.EqualTo(new Vector3(0, 100, 0)));
            foreach (var enter in travel.portals.Where(p => !p.dropEntrance && p.name.StartsWith("ENTER")))
            {
                var exit = travel.portals.Single(p => p.name == enter.name.Replace("ENTER", "EXIT"));
                Assert.That(exit.transform.position - enter.transform.position, Is.EqualTo(network.hiddenOffset));
                Vector3 p = exit.transform.TransformPoint(exit.Volume.center);
                exit.Disarm(0); exit.RefreshArming(p, 100);
                Assert.That(exit.Armed, Is.False, "Time alone must not rearm a doorway");
                exit.RefreshArming(p + exit.transform.forward * 8, 100); Assert.That(exit.Armed, Is.True);
            }
            var drop = travel.portals.Single(p => p.dropEntrance);
            Vector3 relative = drop.destination.position - network.transform.position;
            network.transform.position = Vector3.zero;
            Assert.That(Vector3.Distance(drop.destination.position, relative), Is.LessThan(.0001f));
            Assert.That(new Vector2(relative.x, relative.z), Is.EqualTo(new Vector2(drop.transform.position.x, drop.transform.position.z)));
            network.transform.position = network.hiddenOffset; Physics.SyncTransforms(); yield return null;
        }
        [UnityTest] public IEnumerator CancelRestoresInputCameraAndVisibility()
        {
            player.simulateAutomatically = true; player.outputCamera = Camera.main.transform;
            var portal = travel.portals.First(p => p.name.StartsWith("ENTER"));
            Assert.That(travel.Begin(portal), Is.True); Assert.That(player.simulateAutomatically, Is.False);
            yield return null; travel.Cancel();
            Assert.That(travel.Busy, Is.False); Assert.That(player.simulateAutomatically, Is.True);
            Assert.That(player.outputCamera, Is.EqualTo(Camera.main.transform)); Assert.That(travel.iris.Openness, Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator CaveLightingFollowsAvatarAndRestoresDaylight()
        {
            var lighting = Object.FindAnyObjectByType<PootleCaveLighting>();
            var mode = RenderSettings.ambientMode; var sky = RenderSettings.ambientSkyColor;
            bool sun = lighting.outdoorSun.enabled;
            player.Teleport(new Vector3(22.3f, 103.1f, 38), Quaternion.identity);
            yield return null; yield return null;
            Assert.That(lighting.IsInside, Is.True); Assert.That(lighting.outdoorSun.enabled, Is.False);
            Assert.That(lighting.avatarLight.enabled, Is.True);
            Assert.That(lighting.avatarLight.transform.IsChildOf(player.transform), Is.True);
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(UnityEngine.Rendering.AmbientMode.Flat));
            lighting.enabled = false;
            Assert.That(lighting.avatarLight.enabled, Is.False); Assert.That(lighting.outdoorSun.enabled, Is.EqualTo(sun));
            Assert.That(RenderSettings.ambientMode, Is.EqualTo(mode)); Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sky));
            lighting.enabled = true; yield return null;
            Assert.That(lighting.IsInside, Is.True);
            player.Teleport(new Vector3(-16, .2f, -5), Quaternion.identity);
            yield return null; yield return null;
            Assert.That(lighting.IsInside, Is.False); Assert.That(lighting.avatarLight.enabled, Is.False);
            Assert.That(lighting.outdoorSun.enabled, Is.EqualTo(sun)); Assert.That(RenderSettings.ambientMode, Is.EqualTo(mode));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(sky));
        }
    }
}
