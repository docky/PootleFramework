using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        private readonly List<Object> objects = new List<Object>();
        private readonly Vector3 origin = new Vector3(1000, 0, 1000);
        private PootleCharacter character;
        private PootleProfile profile;
        private GameObject floor;
        private Scene testScene;
        private float lastAirtime;
        private const float Dt = 1f / 60;

        [SetUp]
        public void Setup()
        {
            // The Test Runner has already stored the user's scene setup and opened an untitled
            // bootstrap scene. Single mode replaces that disposable scene without a save prompt.
            testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            floor = Box("Test floor", new Vector3(0, -0.5f, 0), new Vector3(100, 1, 100));
            var go = new GameObject("Test Pootle"); objects.Add(go);
            go.SetActive(false); go.transform.position = origin + Vector3.up * 0.02f;
            go.AddComponent<PootleMotor>();
            character = go.AddComponent<PootleCharacter>();
            profile = ScriptableObject.CreateInstance<PootleProfile>(); objects.Add(profile);
            profile.additionalAirJumps=0;profile.applySlopeSpeed=false;profile.landingMomentumConversion=0;
            profile.automaticGrip=false; character.profile = profile; character.simulateAutomatically = false;
            go.SetActive(true);
            Physics.SyncTransforms();
            Step(2);
        }
        [TearDown]
        public void TearDown()
        {
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]);
            objects.Clear(); Physics.SyncTransforms();
        }
        private GameObject Box(string name, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); objects.Add(go);
            go.name = name; go.transform.position = origin + position; go.transform.localScale = scale;
            return go;
        }
        private void Step(int count, PootleInputFrame frame = default, float dt = Dt)
        {
            for (int i = 0; i < count; i++) { Physics.SyncTransforms(); character.Tick(dt, frame); frame.jumpPressed = false; }
        }
        private void ResetAt(Vector3 offset)
        { character.Teleport(origin + offset, Quaternion.identity); Physics.SyncTransforms(); }

        [Test]
        public void AccelerationKeepsForwardGainAndTurningDragIndependent()
        {
            Vector3 result = PootleMovement.Accelerate(new Vector3(5, 0, 0), Vector3.forward, 1, 16, 30, 7.5f, 2, 0.1f);
            Assert.That(result.x, Is.EqualTo(2).Within(0.0001f));
            Assert.That(result.z, Is.EqualTo(1.6f).Within(0.0001f));
        }
        [Test]
        public void CameraRelativeDiagonalIsNormalisedAndVerticalCameraHasStableFallback()
        {
            var camera = new GameObject("Test camera"); objects.Add(camera);
            camera.transform.rotation = Quaternion.Euler(90, 35, 0);
            var direction = PootleMovement.CameraDirection(Vector2.one, camera.transform, out float magnitude);
            Assert.That(magnitude, Is.EqualTo(1)); Assert.That(direction.magnitude, Is.EqualTo(1).Within(0.001f));
            Assert.That(direction.y, Is.Zero.Within(0.001f));
        }
        [Test]
        public void JumpBufferDoesNotConsumeUntilAcceptedAndExpires()
        {
            var buffer = new PootleJumpBuffer();
            buffer.Press(1, 0.15f);
            Assert.That(buffer.Available(1.1f)); Assert.That(buffer.Available(1.1f));
            Assert.That(buffer.Consume(1.1f)); Assert.That(buffer.Consume(1.1f), Is.False);
            buffer.Press(2, 0.15f); Assert.That(buffer.Consume(2.16f), Is.False);
        }
        [Test]
        public void WalkRunAnalogueReleaseAndReversal()
        {
            Step(90, new PootleInputFrame { move = Vector2.up });
            Assert.That(character.Velocity.z, Is.EqualTo(7.5f).Within(0.05f));
            Step(30, new PootleInputFrame { move = Vector2.up, run = true });
            Assert.That(character.Velocity.z, Is.EqualTo(9).Within(0.05f));
            Step(1, new PootleInputFrame { move = Vector2.down });
            Assert.That(character.IsBraking); Assert.That(character.Velocity.z, Is.GreaterThan(0));
            Step(90, new PootleInputFrame { move = Vector2.down });
            Assert.That(character.Velocity.z, Is.LessThan(-7.4f));
            Step(30); Assert.That(character.Velocity.sqrMagnitude, Is.LessThan(0.001f));
            Step(90, new PootleInputFrame { move = Vector2.up * 0.5f });
            Assert.That(character.Velocity.z, Is.EqualTo(3.75f).Within(0.05f));
        }
        [Test]
        public void BufferedLandingJumpFiresExactlyOnceIncludingEarlyRelease()
        {
            ResetAt(Vector3.up * 0.12f);
            int jumps = 0; character.Jumped += () => jumps++;
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = false });
            Step(100);
            Assert.That(jumps, Is.EqualTo(1)); Assert.That(character.IsGrounded);
        }
        [Test]
        public void CoyoteJumpWorksAndCannotBeReused()
        {
            Object.DestroyImmediate(floor); Physics.SyncTransforms();
            Step(4);
            int jumps = 0; character.Jumped += () => jumps++;
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true });
            Assert.That(jumps, Is.EqualTo(1)); Assert.That(character.Velocity.y, Is.GreaterThan(18));
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true });
            Assert.That(jumps, Is.EqualTo(1));
        }
        [Test]
        public void ExpiredCoyoteDoesNotJumpButConfiguredAirJumpWorks()
        {
            Object.DestroyImmediate(floor); Physics.SyncTransforms(); Step(12);
            int jumps = 0; character.Jumped += () => jumps++;
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true }); Assert.That(jumps, Is.Zero);
            profile.additionalAirJumps = 1;
            Step(1, new PootleInputFrame { jumpHeld = true }); Assert.That(jumps, Is.EqualTo(1));
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true }); Assert.That(jumps, Is.EqualTo(1));
        }
        private float JumpApex(bool hold, float dt)
        {
            ResetAt(Vector3.up * 0.02f); Step(2, dt: dt);
            float apex = 0;
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = hold }, dt);
            lastAirtime = dt;
            bool landed = false;
            for (int i = 0; i < Mathf.CeilToInt(2 / dt); i++)
            {
                Step(1, new PootleInputFrame { jumpHeld = hold }, dt);
                apex = Mathf.Max(apex, character.transform.position.y);
                if (!landed) { lastAirtime += dt; landed = character.IsGrounded; }
            }
            return apex;
        }
        [Test]
        public void EarlyReleaseProducesAShorterJump()
        { Assert.That(JumpApex(false, Dt), Is.LessThan(JumpApex(true, Dt) * 0.6f)); }
        [Test]
        public void JumpApexAndStoppingDistanceVaryByLessThanTenPercentAcrossFrameRates()
        {
            float minApex = float.MaxValue, maxApex = 0, minStop = float.MaxValue, maxStop = 0;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                float dt = 1f / fps;
                float apex = JumpApex(true, dt);
                float airtime = lastAirtime;
                ResetAt(Vector3.up * 0.02f); Step(fps, new PootleInputFrame { move = Vector2.up }, dt);
                float start = character.transform.position.z;
                Step(fps, dt: dt);
                float stopping = character.transform.position.z - start;
                minApex = Mathf.Min(minApex, apex); maxApex = Mathf.Max(maxApex, apex);
                minStop = Mathf.Min(minStop, stopping); maxStop = Mathf.Max(maxStop, stopping);
                ResetAt(Vector3.up * 0.02f); Step(fps, new PootleInputFrame { move = Vector2.up }, dt);
                int reverseFrames = 0;
                while (character.Velocity.z >= 0 && reverseFrames < fps * 2)
                { Step(1, new PootleInputFrame { move = Vector2.down }, dt); reverseFrames++; }
                Debug.Log($"Pootle {fps}fps: apex={apex:F4}m, airtime={airtime:F4}s, stop={stopping:F4}m, reversal={reverseFrames * dt:F4}s");
            }
            Assert.That(maxApex / minApex, Is.LessThan(1.1f));
            Assert.That(maxStop / minStop, Is.LessThan(1.1f));
        }
        [Test]
        public void SmallStepsClimbButTallWallsStaySolid()
        {
            Box("Small step", new Vector3(0, 0.075f, 2), new Vector3(4, 0.15f, 2));
            Box("Tall wall", new Vector3(0, 1.5f, 5), new Vector3(4, 3, 1));
            Step(120, new PootleInputFrame { move = Vector2.up });
            Assert.That(character.transform.position.z - origin.z, Is.InRange(3.5f, 4.1f));
            Assert.That(float.IsFinite(character.Velocity.y));
        }
        [Test]
        public void CeilingStopsJumpAndCornerDoesNotPenetrate()
        {
            Box("Ceiling", new Vector3(0, 2.6f, 0), new Vector3(5, 0.3f, 5));
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true });
            Step(10, new PootleInputFrame { jumpHeld = true });
            Assert.That(character.transform.position.y, Is.LessThan(0.5f));
            Box("Wall Z", new Vector3(0, 1.5f, 3), new Vector3(8, 3, 0.5f));
            Box("Wall X", new Vector3(3, 1.5f, 0), new Vector3(0.5f, 3, 8));
            Step(180, new PootleInputFrame { move = Vector2.one.normalized });
            Assert.That(character.transform.position.x - origin.x, Is.LessThan(2.3f));
            Assert.That(character.transform.position.z - origin.z, Is.LessThan(2.3f));
        }
        [TestCase(25, true)]
        [TestCase(60, false)]
        public void SlopesRespectWalkableLimit(float angle, bool shouldClimb)
        {
            var ramp = Box("Ramp", new Vector3(0, Mathf.Sin(angle * Mathf.Deg2Rad) * 3 - 0.15f, 5), new Vector3(6, 0.3f, 6));
            ramp.transform.rotation = Quaternion.Euler(-angle, 0, 0);
            Step(90, new PootleInputFrame { move = Vector2.up });
            Assert.That(character.transform.position.y > 0.7f, Is.EqualTo(shouldClimb));
            Assert.That(float.IsFinite(character.Velocity.y));
        }
        [Test]
        public void OffsetCapsuleRecoversFromInitialOverlapAndTeleportClearsJump()
        {
            var wall = Box("Overlapping wall", new Vector3(0.4f, 1, 0), new Vector3(1, 3, 2));
            character.Motor.center = new Vector3(0.1f, 1.2f, 0); character.Motor.Configure();
            ResetAt(Vector3.zero); Step(3);
            var capsule = character.GetComponent<CapsuleCollider>();
            Assert.That(Physics.ComputePenetration(capsule, character.transform.position, Quaternion.identity,
                wall.GetComponent<Collider>(), wall.transform.position, Quaternion.identity, out _, out _), Is.False);
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true });
            ResetAt(new Vector3(-5, 0, 0));
            Assert.That(character.Velocity, Is.EqualTo(Vector3.zero));
            Assert.That(character.DebugSnapshot.count, Is.Zero);
            Step(60); Assert.That(character.IsGrounded);
        }
        [Test]
        public void DiagnosticsAndFreezeHaveNoEffectOnPhysicsQueriesOrMotion()
        {
            ResetAt(Vector3.up * 0.02f); character.captureDiagnostics = false;
            Step(45, new PootleInputFrame { move = Vector2.up });
            Vector3 expected = character.transform.position; int queries = character.Motor.QueryCount;
            ResetAt(Vector3.up * 0.02f); character.captureDiagnostics = true;
            Step(45, new PootleInputFrame { move = Vector2.up });
            Assert.That(character.transform.position, Is.EqualTo(expected)); Assert.That(character.Motor.QueryCount, Is.EqualTo(queries));
            character.DebugSnapshot.frozen = true; int tick = character.DebugSnapshot.tick;
            Step(3, new PootleInputFrame { move = Vector2.up }); Assert.That(character.DebugSnapshot.tick, Is.EqualTo(tick));
            Assert.That(character.transform.position.z, Is.GreaterThan(expected.z));
        }

        [Test]
        public void MissingCentralSupportSlipsOffTheCapsuleEdge()
        {
            floor.transform.localScale = new Vector3(2, 1, 2);
            ResetAt(new Vector3(1.3f, 0.02f, 0));
            Step(90);
            Assert.That(character.IsGrounded, Is.False);
            Assert.That(character.transform.position.y, Is.LessThan(-0.2f));
        }

        [Test]
        public void ThinWallTopAllowsWalkingAlongAndOff()
        {
            Box("Thin wall", new Vector3(0, 2, 0), new Vector3(0.5f, 4, 12));
            ResetAt(new Vector3(0, 4.03f, 0)); Step(5);
            Assert.That(character.IsGrounded);
            Step(40, new PootleInputFrame { move = Vector2.up });
            Assert.That(character.transform.position.z - origin.z, Is.GreaterThan(2));
            Step(40, new PootleInputFrame { move = Vector2.right });
            Assert.That(character.transform.position.x - origin.x, Is.GreaterThan(1));
            Assert.That(character.transform.position.y, Is.LessThan(3.5f));
        }
        [TestCase(30)]
        [TestCase(45)]
        [TestCase(70)]
        public void StairsClimbAtAnAngle(float angle)
        {
            for (int i = 0; i < 6; i++)
                Box("Stair", new Vector3(0, (i + 1) * 0.075f, 2 + i * 0.9f), new Vector3(100, (i + 1) * 0.15f, 0.9f));
            float radians = angle * Mathf.Deg2Rad;
            for (int i = 0; i < 240; i++)
            {
                Step(1, new PootleInputFrame { move = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians)) });
                if (character.transform.position.z - origin.z > 6) break;
            }
            Assert.That(character.transform.position.z - origin.z, Is.GreaterThan(6));
            Assert.That(character.transform.position.y, Is.GreaterThan(0.85f));
        }
        private GameObject LandingRamp(float angle)
        {
            var ramp = Box("Landing slope", new Vector3(0, 4, 0), new Vector3(12, 0.3f, 14));
            ramp.transform.rotation = Quaternion.Euler(-angle, 0, 0); return ramp;
        }
        [Test]
        public void LandingOn44DegreeSlopeStaysStillWithoutRisingOrSliding()
        {
            LandingRamp(44); ResetAt(new Vector3(0, 9, 0));
            for (int i = 0; i < 100 && !character.IsGrounded; i++) Step(1);
            Assert.That(character.IsGrounded);
            Vector3 landed = character.transform.position;
            Step(90);
            Assert.That(character.IsGrounded);
            Assert.That(Vector3.Distance(landed, character.transform.position), Is.LessThan(0.04f));
            Assert.That(character.Mode, Is.Not.EqualTo(PootleMode.Sliding));
        }
        [Test]
        public void SteepSlideSnapsRelativeFacingIgnoresInputCoastsAndJumpEscapes()
        {
            LandingRamp(60); ResetAt(new Vector3(0, 9, 0));
            for (int i = 0; i < 100 && character.Mode != PootleMode.Sliding; i++) Step(1);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Sliding));
            Assert.That(Vector3.Dot(character.Facing, Vector3.forward), Is.GreaterThan(0.99f));
            Step(15, new PootleInputFrame { move = Vector2.right, run = true });
            Assert.That(character.Velocity.x, Is.Zero.Within(0.03f));
            Assert.That(character.Velocity.z, Is.LessThan(-1));
            for (int i = 0; i < 200 && character.transform.position.y > 0.1f; i++) Step(1);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Sliding));
            float z = character.transform.position.z;
            Step(12); Assert.That(character.transform.position.z, Is.LessThan(z - 0.4f));
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.Velocity.y, Is.GreaterThan(17));
        }
        [Test]
        public void ClimbableWallAutomaticallyHopsOntoItsTop()
        {
            Box("Climbable wall", new Vector3(0, 1.5f, 1), new Vector3(6, 3, 1)).AddComponent<PootleClimbSurface>();
            ResetAt(new Vector3(0, 0.03f, -0.05f));
            Step(1, new PootleInputFrame { grip = true, move = Vector2.up });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Climbing));
            for (int i = 0; i < 120 && character.Mode != PootleMode.LedgeHop; i++)
                Step(1, new PootleInputFrame { grip = true, move = Vector2.up });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.LedgeHop));
            Step(40);
            Assert.That(character.transform.position.y, Is.GreaterThan(2.95f));
            Assert.That(character.transform.position.z - origin.z, Is.GreaterThan(0.9f));
        }
        [Test]
        public void CylinderClimbFollowsCurvature()
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder); objects.Add(cylinder);
            cylinder.transform.position = origin + new Vector3(0, 2, 1.5f); cylinder.transform.localScale = new Vector3(2, 2, 2);
            cylinder.AddComponent<PootleClimbSurface>();
            ResetAt(new Vector3(0, 0.3f, -0.05f));
            Step(1, new PootleInputFrame { grip = true, move = Vector2.up });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Climbing));
            Step(35, new PootleInputFrame { grip = true, move = Vector2.right });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Climbing));
            Assert.That(character.transform.position.x - origin.x, Is.GreaterThan(0.6f));
            Assert.That(character.Facing.x, Is.LessThan(-0.2f));
        }
        [Test]
        public void LedgeCatchAutomaticallyMiniHopsAndBlockedTopStaysHanging()
        {
            Box("Ledge", new Vector3(0, 1.5f, 1), new Vector3(6, 3, 1));
            var ceiling = Box("Top clearance blocked", new Vector3(0, 4.5f, 1), new Vector3(6, 0.2f, 1));
            ResetAt(new Vector3(0, 1.6f, -0.05f));
            Step(5);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.LedgeHanging));
            Step(30); Assert.That(character.Mode, Is.EqualTo(PootleMode.LedgeHanging));
            Object.DestroyImmediate(ceiling); Step(60);
            Assert.That(character.transform.position.y, Is.GreaterThan(2.95f));
            Assert.That(character.transform.position.z - origin.z, Is.GreaterThan(0.9f));
        }
        [Test]
        public void PoleRetainsFacingAndJumpsThroughPoleInFacingDirection()
        {
            var go = new GameObject("Pole"); objects.Add(go); go.transform.position = origin + new Vector3(0, 3, 0.6f);
            var pole = go.AddComponent<PootlePole>(); pole.length = 6;
            var collider = go.AddComponent<CapsuleCollider>(); collider.isTrigger = true; collider.radius = 0.12f; collider.height = 6;
            ResetAt(Vector3.up * 0.03f);
            Step(1, new PootleInputFrame { grip = true });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.PoleClimbing));
            Vector3 facing = character.Facing;
            Step(30, new PootleInputFrame { grip = true, move = Vector2.up });
            Assert.That(character.Facing, Is.EqualTo(facing));
            Assert.That(character.transform.position.y, Is.GreaterThan(1));
            Step(1, new PootleInputFrame { grip = true, jumpPressed = true, jumpHeld = true });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.Velocity.z, Is.GreaterThan(6));
            Assert.That(character.Velocity.y, Is.GreaterThan(17));
        }

        private PootleRail Rail(params Vector3[] points)
        {
            var go = new GameObject("Test one-way rail"); objects.Add(go); go.transform.position = origin;
            var rail = go.AddComponent<PootleRail>(); rail.points = points; return rail;
        }
        [Test]
        public void RailCanBeJumpedThroughThenLandedOnAndJumpedOff()
        {
            Rail(new Vector3(0, 1.4f, -5), new Vector3(0, 1.4f, 5));
            ResetAt(Vector3.up * 0.02f); Step(2);
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true });
            Step(15, new PootleInputFrame { jumpHeld = true });
            Assert.That(character.transform.position.y, Is.GreaterThan(1.5f));
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
            for (int i = 0; i < 120 && character.Mode != PootleMode.RailWalking; i++) Step(1);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.RailWalking));
            Assert.That(character.transform.position.y, Is.EqualTo(1.4f).Within(0.02f));
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true, move = Vector2.right });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.Velocity.x, Is.GreaterThan(5));
            Assert.That(character.Velocity.y, Is.GreaterThan(17));
        }
        [Test]
        public void RailLandingSnapsFourWaysAndCornerRequiresExplicitTurn()
        {
            Rail(new Vector3(0, 2, -5), new Vector3(0, 2, 3), new Vector3(5, 2, 3));
            character.Teleport(origin + new Vector3(0, 4, 0), Quaternion.Euler(0, 80, 0));
            for (int i = 0; i < 100 && character.Mode != PootleMode.RailWalking; i++) Step(1);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.RailWalking));
            Assert.That(Vector3.Dot(character.Facing, Vector3.right), Is.GreaterThan(0.99f));
            Step(25, new PootleInputFrame { move = Vector2.up });
            Assert.That(Vector3.Dot(character.Facing, Vector3.forward), Is.GreaterThan(0.99f));
            Step(60, new PootleInputFrame { move = Vector2.up });
            Assert.That(character.transform.position.x-origin.x,Is.Zero.Within(.03f));
            Step(30,new PootleInputFrame {move=Vector2.right});
            Assert.That(character.transform.position.x - origin.x, Is.GreaterThan(0.2f));
            Assert.That(character.transform.position.z - origin.z, Is.EqualTo(3).Within(0.03f));
            Assert.That(Vector3.Dot(character.Facing, Vector3.right), Is.GreaterThan(0.99f));
        }
        [Test]
        public void SteepRailSlidesOntoFlatWithMomentumAndFallsOffEnd()
        {
            Rail(new Vector3(0, 7, 0), new Vector3(0, 1, 3), new Vector3(0, 1, 6));
            ResetAt(new Vector3(0, 9, 1));
            for (int i = 0; i < 120 && character.Mode != PootleMode.RailSliding; i++) Step(1);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.RailSliding));
            for (int i = 0; i < 120 && character.transform.position.z - origin.z < 3.1f; i++) Step(1);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.RailSliding));
            Assert.That(character.Velocity.z, Is.GreaterThan(2));
            Step(90);
            Assert.That(character.GetComponent<PootleRailRider>().Rail, Is.Null);
            Assert.That(character.transform.position.z - origin.z, Is.GreaterThan(6));
        }
        [Test]
        public void RailOutsideWidthDoesNotCatchAndTeleportClearsAttachment()
        {
            Rail(new Vector3(0, 2, -5), new Vector3(0, 2, 5));
            ResetAt(new Vector3(1.8f, 5, 0)); Step(90);
            Assert.That(character.IsGrounded);
            ResetAt(new Vector3(0, 5, 0));
            for (int i = 0; i < 90 && character.Mode != PootleMode.RailWalking; i++) Step(1);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.RailWalking));
            ResetAt(new Vector3(4, 0.02f, 0)); Step(3);
            Assert.That(character.GetComponent<PootleRailRider>().Rail, Is.Null);
            Assert.That(character.IsGrounded);
        }

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(-1, 0)]
        public void ExactWallContactCanMoveAwayOrAlongWall(float x, float z)
        {
            var wall = Box("Flush wall", new Vector3(0, 1.25f, -9), new Vector3(6, 2.5f, 3));
            wall.transform.position = new Vector3(-10, 1.25f, -9);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            character.Teleport(new Vector3(-10.428930f, 0, -7.009997f), Quaternion.identity);
            var start = character.transform.position;
            Step(40, new PootleInputFrame { move = new Vector2(x, z) });
            Assert.That(Vector3.Dot(character.transform.position - start, new Vector3(x, 0, z)), Is.GreaterThan(2));
            Assert.That(character.transform.position.z, Is.GreaterThanOrEqualTo(-7.02f));
        }
        [Test]
        public void ExactWallContactStillBlocksInwardMotionAndAllowsJump()
        {
            var wall = Box("Flush wall", new Vector3(0, 1.25f, -9), new Vector3(6, 2.5f, 3));
            wall.transform.position = new Vector3(-10, 1.25f, -9);
            floor.transform.position = new Vector3(0, -0.5f, 0);
            character.Teleport(new Vector3(-10.428930f, 0, -7.009997f), Quaternion.identity);
            Step(40, new PootleInputFrame { move = Vector2.down });
            Assert.That(character.transform.position.z, Is.GreaterThanOrEqualTo(-7.02f));
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true });
            Step(10, new PootleInputFrame { jumpHeld = true });
            Assert.That(character.transform.position.y, Is.GreaterThan(1));
        }

        [Test]
        public void PoleAttachmentSettlesInFrontOfRetainedFacingAndTurnsAroundPole()
        {
            var go = new GameObject("Pole"); objects.Add(go); go.transform.position = origin + new Vector3(0, 3, 0.7f);
            var pole = go.AddComponent<PootlePole>(); pole.length = 6;
            var trigger = go.AddComponent<CapsuleCollider>(); trigger.height = 6; trigger.radius = 0.12f; trigger.isTrigger = true;
            ResetAt(new Vector3(0, 1, 0));
            Step(30, new PootleInputFrame { grip = true });
            Vector3 toPole = Vector3.ProjectOnPlane(go.transform.position - character.transform.position, Vector3.up);
            Assert.That(toPole.magnitude, Is.EqualTo(pole.radius + profile.poleBodyDistance).Within(0.03f));
            Assert.That(Vector3.Dot(toPole.normalized, character.Facing), Is.GreaterThan(0.99f));
            Step(30, new PootleInputFrame { grip = true, move = Vector2.right });
            toPole = Vector3.ProjectOnPlane(go.transform.position - character.transform.position, Vector3.up);
            Assert.That(Vector3.Dot(toPole.normalized, character.Facing), Is.GreaterThan(0.95f));
        }
        [Test]
        public void ForgivingRailLandingCatchesCapsuleEdgeButNotFarMiss()
        {
            Rail(new Vector3(0, 2, -5), new Vector3(0, 2, 5));
            ResetAt(new Vector3(0.45f, 4, 0));
            for (int i = 0; i < 90 && character.Mode != PootleMode.RailWalking; i++) Step(1);
            Assert.That(character.Mode, Is.EqualTo(PootleMode.RailWalking));
            Assert.That(character.transform.position.x - origin.x, Is.Zero.Within(0.03f));
        }
        [Test]
        public void WalkingOntoGroundLevelRailClimbsInclineWithoutJumping()
        {
            Rail(new Vector3(0, 0.02f, 1), new Vector3(0, 2, 7));
            ResetAt(new Vector3(0, 0.02f, 0)); Step(2);
            Step(60, new PootleInputFrame { move = Vector2.up });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.RailWalking));
            Assert.That(character.transform.position.y, Is.GreaterThan(0.8f));
        }
        [Test]
        public void CanStepOntoMiddleStairFromSideButNotHigherRiser()
        {
            Box("Fourth riser", new Vector3(0, 0.3f, 0), new Vector3(4, 0.6f, 3));
            ResetAt(new Vector3(-4, 0.02f, 0)); Step(2);
            Step(45, new PootleInputFrame { move = Vector2.right });
            Assert.That(character.transform.position.y, Is.GreaterThan(0.58f));
            Assert.That(character.transform.position.x - origin.x, Is.GreaterThan(-1.9f));
            Box("Too tall", new Vector3(0, 0.4f, 8), new Vector3(4, 0.8f, 3));
            ResetAt(new Vector3(-4, 0.02f, 8)); Step(2);
            Step(60, new PootleInputFrame { move = Vector2.right });
            Assert.That(character.transform.position.x - origin.x, Is.LessThan(-2.4f));
        }
        [Test]
        public void SteepSlopeDiscardsMostSidewaysLandingMomentum()
        {
            LandingRamp(60); ResetAt(new Vector3(-2, 9, 0));
            for (int i = 0; i < 100 && character.Mode != PootleMode.Sliding; i++)
                Step(1, new PootleInputFrame { move = Vector2.right });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Sliding));
            Assert.That(Mathf.Abs(character.Velocity.x), Is.LessThan(0.8f));
            Step(15);
            Assert.That(Mathf.Abs(character.Velocity.x), Is.LessThan(0.05f));
            Assert.That(character.Velocity.z, Is.LessThan(-2));
        }
        [Test]
        public void TopOutFinishesWithSmallSmoothStepsAndNoResidualLaunch()
        {
            Box("Climbable", new Vector3(0, 1.5f, 1), new Vector3(6, 3, 1)).AddComponent<PootleClimbSurface>();
            ResetAt(new Vector3(0, 0.03f, -0.05f));
            for (int i = 0; i < 120 && character.Mode != PootleMode.LedgeHop; i++) Step(1, new PootleInputFrame { grip = true, move = Vector2.up });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.LedgeHop));
            Vector3 last = character.transform.position; float finalStep = 1;
            for (int i = 0; i < 60 && character.Mode == PootleMode.LedgeHop; i++)
            {
                Step(1); finalStep = Vector3.Distance(last, character.transform.position);
                Assert.That(finalStep, Is.LessThan(0.14f)); last = character.transform.position;
            }
            Assert.That(finalStep, Is.LessThan(0.03f));
            Assert.That(character.Velocity.magnitude, Is.LessThan(0.01f));
        }

        [Test]
        public void MovingPlatformCarriesTranslationAndJumpInheritsSpeed()
        {
            var deck = Box("Moving deck", new Vector3(0, 0.25f, 0), new Vector3(4, 0.5f, 4));
            deck.AddComponent<PootleMovingSurface>(); ResetAt(new Vector3(0, 0.52f, 0)); Step(3);
            for (int i = 0; i < 60; i++) { deck.transform.position += Vector3.right * 0.03f; Step(1); }
            Assert.That(character.IsGrounded);
            Assert.That(character.transform.position.x - deck.transform.position.x, Is.Zero.Within(0.03f));
            deck.transform.position += Vector3.right * 0.03f;
            Step(1, new PootleInputFrame { jumpPressed = true, jumpHeld = true });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.Velocity.x, Is.GreaterThan(1));
            Assert.That(character.GetComponent<PootlePlatformRider>().Surface, Is.Null);
        }
        [Test]
        public void MovingPlatformLiftDoesNotDoubleCarryAndRotationPreservesAnchor()
        {
            var deck = Box("Lift", new Vector3(0, 0.25f, 0), new Vector3(4, 0.5f, 4));
            deck.AddComponent<PootleMovingSurface>(); ResetAt(new Vector3(1, 0.52f, 0)); Step(3);
            float start = character.transform.position.y;
            for (int i = 0; i < 40; i++) { deck.transform.position += Vector3.up * 0.02f; Step(1); }
            Assert.That(character.transform.position.y - start, Is.EqualTo(0.8f).Within(0.04f));
            deck.transform.rotation = Quaternion.Euler(0, 90, 0); Step(1);
            Assert.That(character.transform.position.x - origin.x, Is.Zero.Within(0.04f));
            Assert.That(character.transform.position.z - origin.z, Is.EqualTo(-1).Within(0.04f));
        }
        private PootleDangleHook Hook(Vector3 point)
        {
            var go = new GameObject("Test hook"); objects.Add(go); go.transform.position = origin + point;
            return go.AddComponent<PootleDangleHook>();
        }
        [Test]
        public void HookCarriesUntilGripReleasedOrJumpedAndTeleportClearsIt()
        {
            var hook = Hook(new Vector3(0, 3, 0)); ResetAt(hook.HangingPosition(Vector3.forward)-origin);
            Step(1, new PootleInputFrame { grip = true }); Assert.That(character.Mode, Is.EqualTo(PootleMode.Dangling));
            for (int i = 0; i < 30; i++) { hook.transform.position += Vector3.right * 0.02f; Step(1, new PootleInputFrame { grip = true }); }
            Assert.That(Vector3.Distance(character.transform.position, hook.HangingPosition(character.Facing)), Is.LessThan(0.03f));
            Step(1); Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
            ResetAt(hook.HangingPosition(character.Facing) - origin); Step(1, new PootleInputFrame { grip = true });
            Step(1, new PootleInputFrame { grip = true, jumpPressed = true, jumpHeld = true, move = Vector2.right });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne)); Assert.That(character.Velocity.x, Is.GreaterThan(5));
            ResetAt(new Vector3(4, 0.02f, 0)); Assert.That(character.GetComponent<PootleHookRider>().Hook, Is.Null);
        }
        [Test]
        public void HookDetachesWhenCarriedCapsuleCannotFollowThroughWall()
        {
            var hook = Hook(new Vector3(0, 3, 0));
            var wall = Box("Hook obstruction", new Vector3(1, 2, 0), new Vector3(0.3f, 4, 4));
            ResetAt(hook.HangingPosition(Vector3.forward)-origin); Step(1, new PootleInputFrame { grip = true });
            for (int i = 0; i < 60 && character.Mode == PootleMode.Dangling; i++)
            { hook.transform.position += Vector3.right * 0.05f; Step(1, new PootleInputFrame { grip = true }); }
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.GetComponent<PootleHookRider>().Hook, Is.Null);
            Assert.That(character.transform.position.x - origin.x, Is.LessThan(0.4f));
            Assert.That(Physics.ComputePenetration(character.GetComponent<CapsuleCollider>(), character.transform.position, Quaternion.identity,
                wall.GetComponent<Collider>(), wall.transform.position, wall.transform.rotation, out _, out _), Is.False);
        }
        [Test]
        public void SplineMoverFollowsCurveAtConfiguredSpeedAndReverses()
        {
            var pathObject = new GameObject("Test spline"); objects.Add(pathObject); pathObject.transform.position = origin;
            var container = pathObject.AddComponent<UnityEngine.Splines.SplineContainer>();
            var spline = new UnityEngine.Splines.Spline();
            spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(0,0,0)), UnityEngine.Splines.TangentMode.AutoSmooth);
            spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(2,1,2)), UnityEngine.Splines.TangentMode.AutoSmooth);
            spline.Add(new UnityEngine.Splines.BezierKnot(new Unity.Mathematics.float3(4,0,0)), UnityEngine.Splines.TangentMode.AutoSmooth);
            container.Spline = spline;
            var go = new GameObject("Mover"); objects.Add(go); go.transform.position = origin;
            var mover = go.AddComponent<PootleSplineMover>(); mover.simulateAutomatically = false; mover.path = container; mover.speed = 2; mover.Rebuild();
            float travelled = 0; Vector3 previous = go.transform.position; float maximumX = 0;
            for (int i = 0; i < 300; i++) { mover.Tick(Dt); travelled += Vector3.Distance(previous, go.transform.position); previous = go.transform.position; maximumX = Mathf.Max(maximumX, previous.x - origin.x); }
            Assert.That(travelled, Is.InRange(9.8f, 10.2f)); Assert.That(maximumX, Is.GreaterThan(3.9f));
            Assert.That(go.transform.position.x - origin.x, Is.LessThan(3));
        }

        [Test]
        public void HookTurnsFreelyWithCameraRelativeInputAndJumpRetainsFacing()
        {
            var hook = Hook(new Vector3(0,3,0)); ResetAt(hook.HangingPosition(Vector3.forward)-origin);
            var camera = new GameObject("Hook camera"); objects.Add(camera); camera.transform.rotation = Quaternion.Euler(0,90,0);
            character.outputCamera = camera.transform;
            Step(1,new PootleInputFrame { grip=true });
            Step(60,new PootleInputFrame { grip=true, move=Vector2.up });
            Assert.That(character.Mode, Is.EqualTo(PootleMode.Dangling));
            Assert.That(Vector3.Dot(character.Facing,Vector3.right), Is.GreaterThan(.99f));
            Assert.That(Vector3.Dot(hook.transform.forward,character.Facing), Is.GreaterThan(.99f));
            Step(1,new PootleInputFrame { grip=true,jumpPressed=true,jumpHeld=true });
            Assert.That(character.Velocity.x,Is.GreaterThan(5));
        }
        [Test]
        public void GliderSlowsDescentSteersAndReleaseRestoresGravity()
        {
            ResetAt(new Vector3(0,20,0)); Step(30);
            Step(60,new PootleInputFrame {glide=true,move=Vector2.right});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Gliding));
            Assert.That(character.Velocity.y,Is.EqualTo(-profile.glidingMaximumFallSpeed).Within(.01f));
            Assert.That(character.Velocity.x,Is.GreaterThan(5));
            float height=character.transform.position.y;
            Step(15); Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            Assert.That(character.Velocity.y,Is.LessThan(-5));
            Step(1,new PootleInputFrame {glide=true});
            Assert.That(character.transform.position.y,Is.LessThan(height)); Assert.That(character.Velocity.y,Is.LessThanOrEqualTo(0));
        }
        [Test]
        public void GliderRequiresDescentAndClosesOnLanding()
        {
            Step(3,new PootleInputFrame {glide=true}); Assert.That(character.IsGrounded);
            Step(1,new PootleInputFrame {jumpPressed=true,jumpHeld=true,glide=true});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne)); Assert.That(character.Velocity.y,Is.GreaterThan(0));
            ResetAt(new Vector3(0,2,0)); Step(180,new PootleInputFrame {glide=true});
            Assert.That(character.IsGrounded); Assert.That(character.transform.position.y,Is.LessThan(.05f));
        }
        [Test]
        public void GliderCanLandOnOneWayRail()
        {
            var rail=Rail(new Vector3(-4,1,0),new Vector3(4,1,0));
            character.Teleport(origin+new Vector3(0,2,0),Quaternion.Euler(0,90,0));
            Step(60,new PootleInputFrame {glide=true});
            Assert.That(character.Mode,Is.EqualTo(PootleMode.RailWalking));
            Assert.That(character.transform.position.y,Is.EqualTo(1).Within(.03f));
        }
        [Test]
        public void SpringLaunchIsFullStrengthWithoutJumpHeldAndCanBounceAgain()
        {
            var pad=Box("Spring",new Vector3(0,.2f,0),new Vector3(3,.4f,3)); var spring=pad.AddComponent<PootleSpring>();
            int launches=0; spring.onLaunch.AddListener(()=>launches++);
            ResetAt(new Vector3(0,.42f,0)); Step(1);
            Assert.That(character.Velocity.y,Is.GreaterThan(24)); Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            float peak=0; for(int i=0;i<240;i++){Step(1);peak=Mathf.Max(peak,character.transform.position.y);}
            Assert.That(peak,Is.EqualTo(.4f + spring.launchSpeed * spring.launchSpeed / (2 * profile.risingGravity)).Within(.15f)); Assert.That(launches,Is.GreaterThanOrEqualTo(2));
        }
        [Test]
        public void AngledSpringUsesLaunchDirectionAndDisabledPadDoesNotLaunch()
        {
            var pad=Box("Spring",new Vector3(0,.2f,0),new Vector3(3,.4f,3)); var spring=pad.AddComponent<PootleSpring>();
            spring.localLaunchDirection=new Vector3(1,1,0); spring.enabled=false;
            ResetAt(new Vector3(0,.42f,0));Step(3);Assert.That(character.IsGrounded);
            spring.enabled=true;Step(1);Assert.That(character.Velocity.x,Is.GreaterThan(17));Assert.That(character.Velocity.y,Is.EqualTo(spring.launchSpeed / Mathf.Sqrt(2) - profile.risingGravity * Dt).Within(.03f));
        }

        [Test]
        public void RepeatedCameraSetupReusesObjectsAndCapsuleFitIgnoresOtherRenderers()
        {
            var camera = Pootle.Editor.PootleSceneSetup.ConnectCamera(character);
            objects.Add(camera.gameObject);
            var adapter = Object.FindObjectsByType<PootleOrbitInput>();
            foreach (var a in adapter) if (a.character == character) objects.Add(a.gameObject);
            int count = testScene.GetRootGameObjects().Length;
            Assert.That(Pootle.Editor.PootleSceneSetup.ConnectCamera(character), Is.SameAs(camera));
            Assert.That(testScene.GetRootGameObjects().Length, Is.EqualTo(count));
            var visual = Box("Only this visual", new Vector3(0, 1, 0), new Vector3(1, 2, 1));
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(character.transform, true);
            character.visualRoot = visual.transform;
            Box("Unrelated renderer", new Vector3(30, 10, 0), Vector3.one * 20);
            Pootle.Editor.PootleSceneSetup.FitCapsule(character);
            Assert.That(character.Motor.height, Is.EqualTo(2).Within(0.001f));
            Assert.That(character.Motor.radius, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(camera.gameObject.scene, Is.EqualTo(testScene));
        }
    }
}
