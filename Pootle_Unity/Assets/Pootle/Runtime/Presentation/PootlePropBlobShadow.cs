using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pootle
{
    /// <summary>A configurable shadow for a prop, independent of the character rig.</summary>
    [DefaultExecutionOrder(210), DisallowMultipleComponent, AddComponentMenu("Pootle/Prop Blob Shadow")]
    public sealed class PootlePropBlobShadow : MonoBehaviour
    {
        public enum ShadowTechnique { DepthProjection, TransparentGroundQuad }
        public enum ShadowShape { Circle, Ellipse, RoundedRectangle, CustomTexture }
        public ShadowTechnique technique;
        public ShadowShape shape;
        [Tooltip("World-space footprint. Circle uses the X diameter for both axes.")]
        public Vector2 size = new Vector2(.7f, .7f);
        [Range(0, 1)] public float opacity = .45f;
        [Range(.01f, .95f)] public float softness = .3f;
        [Min(.1f)] public float maximumDrop = 18;
        [Tooltip("Local point above the receiving surfaces; use the bottom of tall props.")]
        public Vector3 localOrigin;
        public bool followYaw = true;
        public float yawOffset;
        public bool replaceModelShadow = true;
        [Tooltip("Alpha channel supplies the shadow shape.")]
        public Texture2D customTexture;
        public Material depthMaterial, transparentMaterial;
        [Tooltip("Used only by the simple ground quad. Depth projection uses visible opaque geometry.")]
        public LayerMask groundLayers = ~0;
        [Range(0, 89)] public float maximumGroundAngle = 65;
        [Min(.001f)] public float groundOffset = .015f;
        public bool ProjectionActive => surface && surface.activeSelf;
        public Transform ProjectionTransform => surface ? surface.transform : null;
        public Texture Mask => instance ? instance.mainTexture : null;
        readonly RaycastHit[] hits = new RaycastHit[64];
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly Dictionary<Renderer, ShadowCastingMode> originalModes = new Dictionary<Renderer, ShadowCastingMode>();
        GameObject surface;
        Material instance, source;
        Texture2D generated;
        ShadowTechnique builtTechnique;
        ShadowShape paintedShape;
        float paintedSoftness = -1;
        const int Resolution = 128;

        void LateUpdate()
        {
            var desired = technique == ShadowTechnique.DepthProjection ? depthMaterial : transparentMaterial;
            if (!desired) { ReleaseProjection(); RestoreShadows(); return; }
            if (!surface || builtTechnique != technique || source != desired)
            {
                ReleaseProjection(); builtTechnique = technique; source = desired;
                surface = GameObject.CreatePrimitive(technique == ShadowTechnique.DepthProjection ? PrimitiveType.Cube : PrimitiveType.Quad);
                surface.name = name + " · prop blob"; surface.hideFlags = HideFlags.DontSave;
                var collider = surface.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
                instance = new Material(desired) { hideFlags = HideFlags.DontSave };
                var renderer = surface.GetComponent<Renderer>(); renderer.sharedMaterial = instance;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            }
            if (replaceModelShadow)
            {
                GetComponentsInChildren(true, renderers);
                foreach (var renderer in renderers)
                    if (!originalModes.ContainsKey(renderer))
                    { originalModes.Add(renderer, renderer.shadowCastingMode); renderer.shadowCastingMode = ShadowCastingMode.Off; }
            }
            else RestoreShadows();
            UpdateMask(); instance.mainTexture = shape == ShadowShape.CustomTexture ? customTexture : generated;
            instance.SetFloat("_Opacity", Mathf.Clamp01(opacity));
            // An unset custom mask should not become an opaque rectangle.
            if (shape == ShadowShape.CustomTexture && !customTexture) { surface.SetActive(false); return; }
            Vector3 origin = transform.TransformPoint(localOrigin) + Vector3.up * .03f;
            float yaw = yawOffset + (followYaw ? transform.eulerAngles.y : 0);
            var footprint = new Vector2(Mathf.Max(.01f, size.x), Mathf.Max(.01f, shape == ShadowShape.Circle ? size.x : size.y));
            float drop = Mathf.Max(.1f, maximumDrop);
            if (technique == ShadowTechnique.DepthProjection)
            {
                surface.SetActive(true);
                surface.transform.SetPositionAndRotation(origin - Vector3.up * drop * .5f, Quaternion.Euler(0, yaw, 0));
                surface.transform.localScale = new Vector3(footprint.x, drop, footprint.y);
            }
            else
            {
                int count = Physics.RaycastNonAlloc(origin, Vector3.down, hits, drop, groundLayers, QueryTriggerInteraction.Ignore);
                int nearest = -1; float distance = float.PositiveInfinity;
                for (int i = 0; i < count; i++)
                {
                    if (hits[i].collider.transform.IsChildOf(transform) || hits[i].distance >= distance) continue;
                    nearest = i; distance = hits[i].distance;
                }
                if (nearest < 0 || Vector3.Angle(hits[nearest].normal, Vector3.up) > maximumGroundAngle)
                { surface.SetActive(false); return; }
                var hit = hits[nearest]; surface.SetActive(true);
                Vector3 forward = Vector3.ProjectOnPlane(Quaternion.Euler(0, yaw, 0) * Vector3.forward, hit.normal).normalized;
                surface.transform.SetPositionAndRotation(hit.point + hit.normal * groundOffset, Quaternion.LookRotation(-hit.normal, forward));
                surface.transform.localScale = new Vector3(footprint.x, footprint.y, 1);
                instance.SetFloat("_Opacity", Mathf.Clamp01(opacity) * Mathf.SmoothStep(0, 1, (drop - distance) / (drop * .15f)));
            }
        }

        void UpdateMask()
        {
            if (shape == ShadowShape.CustomTexture) return;
            float feather = Mathf.Clamp(softness, .01f, .95f);
            if (generated && paintedShape == shape && Mathf.Approximately(paintedSoftness, feather)) return;
            if (!generated) generated = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false, true)
                { name = "Prop blob shape", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[Resolution * Resolution];
            for (int y = 0; y < Resolution; y++) for (int x = 0; x < Resolution; x++)
            {
                var p = new Vector2((x + .5f) / Resolution * 2 - 1, (y + .5f) / Resolution * 2 - 1);
                float d = p.magnitude;
                if (shape == ShadowShape.RoundedRectangle)
                {
                    var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - Vector2.one * .65f;
                    d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) + .65f;
                }
                byte alpha = (byte)(255 * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1 - feather, 1, d))));
                pixels[y * Resolution + x] = new Color32(255, 255, 255, alpha);
            }
            generated.SetPixels32(pixels); generated.Apply(false);
            paintedShape = shape; paintedSoftness = feather;
        }
        void RestoreShadows()
        {
            foreach (var pair in originalModes) if (pair.Key) pair.Key.shadowCastingMode = pair.Value;
            originalModes.Clear();
        }
        void ReleaseProjection()
        {
            if (surface) { surface.SetActive(false); Destroy(surface); }
            if (instance) Destroy(instance);
            surface = null; instance = null; source = null;
        }
        void OnDisable()
        {
            RestoreShadows(); ReleaseProjection();
            if (generated) Destroy(generated);
            generated = null; paintedSoftness = -1;
        }
    }
}
