using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator PropShadow_BothTechniquesRenderAndSimpleModeTracksGround()
        {
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.transform.position = new Vector3(300, 3, 300);
            // Root origin is inside its own collider; a second child below must also be ignored.
            var self = GameObject.CreatePrimitive(PrimitiveType.Cube); self.transform.SetParent(prop.transform, false); self.transform.localPosition = Vector3.down;
            foreach (var r in prop.GetComponentsInChildren<Renderer>()) r.forceRenderingOff = true;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.layer = 30;
            floor.transform.position = new Vector3(300, -.1f, 300); floor.transform.localScale = new Vector3(5, .2f, 5);
            var red = new Material(Shader.Find("Universal Render Pipeline/Unlit")); red.SetColor("_BaseColor", Color.red); floor.GetComponent<Renderer>().sharedMaterial = red;
            var depth = new Material(Shader.Find("Pootle/Blob Shadow"));
            var simple = new Material(Shader.Find("Pootle/Transparent Blob Shadow"));
            var blob = prop.AddComponent<PootlePropBlobShadow>(); blob.depthMaterial = depth; blob.transparentMaterial = simple; blob.size = Vector2.one; blob.groundLayers = 1 << 30;
            // Include the prop layer so the ray test actually exercises self exclusion.
            blob.groundLayers |= 1 << prop.layer;
            var go = new GameObject("Prop shadow pixel camera"); var camera = go.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 1;
            camera.transform.SetPositionAndRotation(new Vector3(300, 10, 300), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            var rt = new RenderTexture(128, 128, 24); rt.Create(); var pixels = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            try
            {
                foreach (PootlePropBlobShadow.ShadowTechnique mode in System.Enum.GetValues(typeof(PootlePropBlobShadow.ShadowTechnique)))
                {
                    blob.technique = mode; blob.opacity = 0; yield return null; yield return null;
                    var baseline = ReadShadowView(camera, rt, pixels);
                    blob.opacity = .8f; yield return null; yield return null;
                    var shaded = ReadShadowView(camera, rt, pixels);
                    int count = 0; for (int i = 0; i < baseline.Length; i++) if (baseline[i].r - shaded[i].r > 20) count++;
                    Assert.That(count, Is.GreaterThan(500), mode + " must render the circle on the floor");
                    Assert.That(blob.ProjectionActive);
                }
                Assert.That(blob.ProjectionTransform.position.y, Is.EqualTo(blob.groundOffset).Within(.002f), "Ignore own child collider");
                floor.transform.rotation = Quaternion.Euler(0, 0, 20); Physics.SyncTransforms(); yield return null; yield return null;
                Assert.That(Vector3.Dot(-blob.ProjectionTransform.forward, floor.transform.up), Is.GreaterThan(.999f));
                prop.transform.position += Vector3.right * 10; Physics.SyncTransforms(); yield return null; yield return null;
                Assert.That(blob.ProjectionActive, Is.False, "No quad suspended over missing ground");
            }
            finally
            {
                camera.targetTexture = null; rt.Release(); Object.Destroy(go); Object.Destroy(rt); Object.Destroy(pixels);
                Object.Destroy(prop); Object.Destroy(floor); Object.Destroy(red); Object.Destroy(depth); Object.Destroy(simple);
            }
        }

        [UnityTest]
        public IEnumerator PropShadow_ShapeYawCustomMaskAndShadowRestoration()
        {
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube); prop.transform.position = new Vector3(300, 5, 300);
            var depth = new Material(Shader.Find("Pootle/Blob Shadow"));
            var blob = prop.AddComponent<PootlePropBlobShadow>(); blob.depthMaterial = depth; blob.size = new Vector2(2, 1);
            var renderer = prop.GetComponent<Renderer>(); var original = renderer.shadowCastingMode;
            var custom = new Texture2D(2, 2);
            try
            {
                yield return null; yield return null;
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(blob.ProjectionTransform.localScale.x, Is.EqualTo(blob.ProjectionTransform.localScale.z), "Circle ignores ellipse aspect");
                blob.shape = PootlePropBlobShadow.ShadowShape.Ellipse; prop.transform.rotation = Quaternion.Euler(0, 75, 0);
                yield return null; yield return null;
                Assert.That(blob.ProjectionTransform.localScale.z, Is.EqualTo(1));
                Assert.That(Quaternion.Angle(blob.ProjectionTransform.rotation, prop.transform.rotation), Is.LessThan(.1f));
                var mask = (Texture2D)blob.Mask; float ellipseCorner = mask.GetPixel(100, 100).a;
                blob.shape = PootlePropBlobShadow.ShadowShape.RoundedRectangle; yield return null; yield return null;
                Assert.That(((Texture2D)blob.Mask).GetPixel(100, 100).a, Is.GreaterThan(ellipseCorner + .2f));
                blob.shape = PootlePropBlobShadow.ShadowShape.CustomTexture; yield return null; yield return null;
                Assert.That(blob.ProjectionActive, Is.False, "Missing texture must not draw a solid rectangle");
                blob.customTexture = custom; yield return null; yield return null;
                Assert.That(blob.Mask, Is.SameAs(custom)); Assert.That(blob.ProjectionActive);
                blob.enabled = false; yield return null;
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(original)); Assert.That(blob.ProjectionActive, Is.False);
            }
            finally { Object.Destroy(prop); Object.Destroy(depth); Object.Destroy(custom); }
        }
    }
}
