using NUnit.Framework;
using Pootle.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Cinemachine;

namespace Pootle.Tests
{
    public sealed class PootlePlacementPreviewTests
    {
        [Test] public void SignLabelsUseSeparateEntryAndExitRadii()
        {
            var go = new GameObject("Sign hysteresis test");
            try
            {
                var sign = go.AddComponent<PootleSignLabel>(); sign.showDistance = 4; sign.exitDistance = 6;
                Assert.That(sign.EvaluateVisibility(Vector3.right * 4.1f), Is.False);
                Assert.That(sign.EvaluateVisibility(Vector3.right * 3.9f), Is.True);
                Assert.That(sign.EvaluateVisibility(Vector3.right * 5.9f), Is.True);
                Assert.That(sign.EvaluateVisibility(Vector3.right * 6.1f), Is.False);
                Assert.That(sign.EvaluateVisibility(Vector3.right * 5), Is.False);
                Assert.That(sign.EvaluateVisibility(Vector3.up * 10), Is.False, "Stacked floors use 3D distance");
                Assert.That(sign.EvaluateVisibility(Vector3.zero), Is.True);
            }
            finally { Object.DestroyImmediate(go); }
        }
        [Test] public void SharedPolicyDrivesShotDistanceLensAndSupportedTilt()
        {
            var go = new GameObject("Policy shot test"); var visual = new GameObject("View Tilt test"); var cameraObject = new GameObject("View Tilt camera");
            var policy = ScriptableObject.CreateInstance<PootleCameraPolicy>();
            try
            {
                var shot = go.AddComponent<PootleExplorationShot>(); shot.policy = policy; shot.distance = 39;
                policy.viewedSizeMultiplier = 1.3f; policy.fieldOfView = 30; policy.viewTiltDegrees = 35;
                shot.ApplySettings();
                Assert.That(shot.EffectiveDistance, Is.EqualTo(30).Within(.001f));
                Assert.That(shot.Rig.Lens.FieldOfView, Is.EqualTo(30));
                var tilt = visual.AddComponent<PootleViewTilt>(); tilt.policy = policy; tilt.viewCamera = cameraObject.AddComponent<Camera>();
                cameraObject.transform.rotation = Quaternion.Euler(60, 0, 0); tilt.ApplyTilt();
                Assert.That(tilt.AppliedTiltDegrees, Is.EqualTo(35));
                policy.enableViewTilt = false; tilt.ApplyTilt(); Assert.That(tilt.AppliedTiltDegrees, Is.Zero);
                tilt.overridePolicy = true; tilt.tiltBackDegrees = 15; tilt.ApplyTilt(); Assert.That(tilt.AppliedTiltDegrees, Is.EqualTo(15));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(visual); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(policy); }
        }
        [Test] public void VisualTiltIsViewRelativeAndNeverRotatesTheParent()
        {
            var root = new GameObject("Tilt test root"); var view = new GameObject("Tilt test camera");
            try
            {
                var pivot = new GameObject("Visual pivot"); pivot.transform.SetParent(root.transform, false);
                var camera = view.AddComponent<Camera>(); var tilt = pivot.AddComponent<PootleViewTilt>(); tilt.viewCamera = camera;
                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    root.transform.rotation = Quaternion.Euler(0, yaw, 0); camera.transform.rotation = Quaternion.Euler(60, 35, 0);
                    var before = root.transform.rotation; tilt.ApplyTilt();
                    Vector3 expected = Quaternion.AngleAxis(25, camera.transform.right) * Vector3.up;
                    Assert.That(Vector3.Angle(pivot.transform.up, expected), Is.LessThan(.01f));
                    Assert.That(Quaternion.Angle(root.transform.rotation, before), Is.LessThan(.01f));
                    var once = pivot.transform.rotation; tilt.ApplyTilt();
                    Assert.That(Quaternion.Angle(once, pivot.transform.rotation), Is.LessThan(.01f), "No cumulative tilt drift");
                }
                camera.transform.rotation = Quaternion.Euler(10, 0, 0); tilt.ApplyTilt();
                Assert.That(Vector3.Angle(pivot.transform.up, Vector3.up), Is.EqualTo(10).Within(.01f));
                tilt.RestorePose();
                Assert.That(Quaternion.Angle(pivot.transform.localRotation, Quaternion.identity), Is.LessThan(.01f));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(view); }
        }
        [Test] public void EditPlacementSnapsSelectsHeightBlendsAndRestores()
        {
            var scene = EditorSceneManager.OpenScene("Assets/CoastalHike/CoastalHike.unity", OpenSceneMode.Additive);
            try
            {
            // Uses the saved authored scene, without entering Play Mode or mutating its assets.
            var director = Object.FindAnyObjectByType<PootleZoneCameraDirector>();
            Assert.That(director, Is.Not.Null, "Open CoastalHike before running this scene integration test");
            Vector3 original = director.target.position; var mode = director.brain.UpdateMethod;
            var sun = GameObject.Find("Warm coastal afternoon").GetComponent<Light>(); bool sunlight = sun.enabled;
            var lightingMode = RenderSettings.ambientMode;
            var glider = director.character.GetComponent<PootleGliderVisual>().gliderRoot; var gliderScale = glider.localScale;
            using (var preview = new PootlePlacementPreview(director))
            {
                Assert.That(glider.localScale, Is.EqualTo(Vector3.zero));
                Assert.That(preview.SnapNear(new Vector3(35, 2.2f, 36)), Is.True);
                Assert.That(director.target.position.y, Is.EqualTo(2.11f).Within(.05f));
                Assert.That(preview.Region.name, Does.Contain("Lower stacked"));
                preview.Tick(2); var lower = director.brain.OutputCamera.transform.rotation;
                Assert.That(preview.SnapNear(new Vector3(35, 10.2f, 36)), Is.True);
                Assert.That(director.target.position.y, Is.EqualTo(10.11f).Within(.05f));
                Assert.That(preview.Region.name, Does.Contain("Upper stacked"));
                preview.Tick(.6f); var middle = director.brain.OutputCamera.transform.rotation;
                preview.Tick(2); var upper = director.brain.OutputCamera.transform.rotation;
                Assert.That(Quaternion.Angle(lower, upper), Is.GreaterThan(30));
                Assert.That(Quaternion.Angle(middle, lower), Is.GreaterThan(1));
                Assert.That(Quaternion.Angle(middle, upper), Is.GreaterThan(1));
                Assert.That(director.brain.OutputCamera.fieldOfView, Is.EqualTo(30).Within(.01f));
                Assert.That(preview.SnapNear(new Vector3(22.3f, 103.1f, 38)), Is.True);
                preview.Tick(2); Assert.That(preview.Region.name, Does.Contain("hidden fern"));
                Assert.That(sun.enabled, Is.False);
                var beforeMiss = director.target.position;
                Assert.That(preview.Place(new Ray(new Vector3(500, 2, 500), Vector3.down), 5), Is.False);
                Assert.That(director.target.position, Is.EqualTo(beforeMiss));
            }
            Assert.That(glider.localScale, Is.EqualTo(gliderScale));
            Assert.That(director.target.position, Is.EqualTo(original));
            Assert.That(director.brain.UpdateMethod, Is.EqualTo(mode));
            Assert.That(sun.enabled, Is.EqualTo(sunlight)); Assert.That(RenderSettings.ambientMode, Is.EqualTo(lightingMode));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
