using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProceduralPrimitivesUtil
{
    [ExecuteInEditMode]
    public abstract class PPBase : MonoBehaviour
    {
        public Color vertexColor = Color.yellow;

        public Vector3 pivotOffset;
        public Vector3 rotation;
        public bool generateMappingCoords = true;
        public bool realWorldMapSize = false;
        public Vector2 UVOffset = new Vector2(0.0f, 0.0f);
        public Vector2 UVTiling = new Vector2(1.0f, 1.0f);
        public bool flipNormals = false;
        public bool smooth = true;
        public bool autoUpdateCollider = true;

        // -------- Height-based taper (builder-space) --------
        [Header("Final Taper (Height-Based)")]
        [Tooltip("0 = no taper. Scale factor = 1 + (y + offset) * taperScale (local space).")]
        public float finalTaperScale = 0f;

        [Tooltip("Offset added to vertex.y before computing taper. Helps tune where taper starts/ends.")]
        public float finalTaperOffset = 0f;
        // ----------------------------------------------------

        // -------- Final-stage lateral noise (PathMesher-style) --------
        public enum NoiseDirectionMode { RadialXZ, ObjectRightXZ }

        [Header("Final Stage Noise")]
        public bool noiseEnabled = false;                  // toggle (default OFF)
        [Range(0, 99999)] public int noiseSeed = 12345;
        [Range(1, 8)] public int noiseOctaves = 3;
        [Range(0f, 1f)] public float noiseAmplitude = 0.2f;
        [Range(0f, 1f)] public float noiseFrequency = 0.25f;
        [Range(0f, 1f)] public float noisePersistence = 0.5f;
        [Range(1f, 10f)] public float noiseLacunarity = 2f;
        [Tooltip("Optional frequency boost by (relative) vertical layer. For PP shapes without layers, this is unused.")]
        [Range(0f, 5f)] public float noiseHeightLacunarity = 0f;
        [Tooltip("Feature size scaler used with Frequency; rough perimeter/edge length works well.")]
        public float noiseScale = 6f;
        [Tooltip("RadialXZ: push out/in from pivot in XZ. ObjectRightXZ: push along local Right projected to XZ.")]
        public NoiseDirectionMode noiseDirectionMode = NoiseDirectionMode.RadialXZ;
        // --------------------------------------------------------------

        private Collider m_collider;
        private MeshFilter m_meshFilter;
        private bool m_dirty = false;

        protected Mesh m_mesh;
        protected Quaternion m_rotation;
        protected PPMeshBuilder m_builder;

        protected Vector3 m_up;
        protected Vector3 m_down;
        protected Vector3 m_left;
        protected Vector3 m_right;
        protected Vector3 m_forward;
        protected Vector3 m_back;

        public Mesh mesh { get { return m_mesh; } }
        public bool isDirty { get { return m_dirty; } }

        private void Awake()
        {
            m_mesh = new Mesh();
            Apply();
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            if (m_mesh != null && !AssetDatabase.Contains(mesh)) DestroyImmediate(mesh);
#else
            if (m_mesh != null) Destroy(m_mesh);
#endif
        }

        private void Reset()
        {
            Apply();
        }

        private void OnValidate()
        {
            m_dirty = true;
        }

        public GameObject CreateInstance()
        {
            return ProceduralPrimitives.CreateInstance(this);
        }

        protected abstract void CreateMesh();

        public void Apply()
        {
            if (m_mesh == null) m_mesh = new Mesh();
            m_builder = new PPMeshBuilder();
            m_rotation = Quaternion.Euler(rotation);
            m_up = m_rotation * Vector3.up;
            m_down = -m_up;
            m_right = m_rotation * Vector3.right;
            m_left = -m_right;
            m_forward = m_rotation * Vector3.forward;
            m_back = -m_forward;

            // Build the primitive into m_builder
            CreateMesh();

            // ---- Final height-based taper on builder verts BEFORE noise ----
            TryApplyFinalTaper();

            // ---- Final-stage noise on builder verts BEFORE we bake to Mesh ----
            if (noiseEnabled)
            {
                TryApplyFinalStageNoise();
            }

            // Bake to Mesh
            m_mesh.Clear();
            m_builder.Apply(m_mesh);

            // Vertex colors (optional helper)
            int vc = m_mesh.vertexCount;
            if (vc > 0)
            {
                var colors = new Color[vc];
                for (int i = 0; i < vc; i++) colors[i] = vertexColor;
                m_mesh.colors = colors;
            }
            else
            {
                m_mesh.colors = null;
            }

            RefreshMeshFilter();
            if (autoUpdateCollider) RefreshCollider();

            // clear
            m_builder.Clear();
            m_builder = null;
            m_dirty = false;
        }

#if PRIMITIVE_EDGES
        //use this function to get construnction edges
        public void Apply(ref List<PPMeshBuilder.Edge> horizontalEdges, ref List<PPMeshBuilder.Edge> verticalEdges)
        {
            if (m_mesh == null) m_mesh = new Mesh();
            m_rotation = Quaternion.Euler(rotation);

            CreateMesh();

            // Height-based taper for edge-preview as well (if desired)
            TryApplyFinalTaper();

            // Optional: apply noise here as well if you need edge preview noisy
            if (noiseEnabled) TryApplyFinalStageNoise();

            //set mesh
            m_mesh.Clear();
            m_builder.Apply(m_mesh);
            m_builder.GetEdges(ref horizontalEdges, ref verticalEdges);

            RefreshMeshFilter();
            if (autoUpdateCollider) RefreshCollider();

            //clear
            m_builder.Clear();
            m_builder = null;
            m_dirty = false;
        }
#endif

        public void RefreshMesh()
        {
            m_mesh = null;
            Apply();
        }

        public Collider GetCollider()
        {
            if (m_collider == null) m_collider = GetComponent<Collider>();
            return m_collider;
        }

        public bool HasCollider()
        {
            return GetCollider() != null;
        }

        public Collider AddBoxCollider()
        {
            if (HasCollider()) return m_collider;
            m_collider = gameObject.AddComponent<BoxCollider>();
            RefreshCollider();
            return m_collider;
        }

        public Collider AddSphereCollider()
        {
            if (HasCollider()) return m_collider;
            m_collider = gameObject.AddComponent<SphereCollider>();
            RefreshCollider();
            return m_collider;
        }

        public Collider AddCapsuleCollider()
        {
            if (HasCollider()) return m_collider;
            m_collider = gameObject.AddComponent<CapsuleCollider>();
            RefreshCollider();
            return m_collider;
        }

        public Collider AddMeshCollider()
        {
            if (HasCollider()) return m_collider;
            m_collider = gameObject.AddComponent<MeshCollider>();
            RefreshCollider();
            return m_collider;
        }

        private void RefreshMeshFilter()
        {
            if (m_meshFilter == null) m_meshFilter = GetComponent<MeshFilter>();
            if (m_meshFilter == null) m_meshFilter = gameObject.AddComponent<MeshFilter>();
            m_meshFilter.sharedMesh = m_mesh;
        }

        private void RefreshCollider()
        {
            if (!HasCollider()) return;
            if (m_collider is MeshCollider)
            {
                MeshCollider collider = (m_collider as MeshCollider);
                collider.sharedMesh = m_mesh;
            }
            else if (m_collider is BoxCollider)
            {
                BoxCollider collider = (m_collider as BoxCollider);
                collider.center = m_mesh.bounds.center;
                collider.size = m_mesh.bounds.size;
            }
            else if (m_collider is SphereCollider)
            {
                SphereCollider collider = (m_collider as SphereCollider);
                collider.center = m_mesh.bounds.center;
                float max = Mathf.Max(m_mesh.bounds.size.x, m_mesh.bounds.size.y, m_mesh.bounds.size.z);
                collider.radius = max * 0.5f;
            }
            else if (m_collider is CapsuleCollider)
            {
                CapsuleCollider collider = (m_collider as CapsuleCollider);
                collider.center = m_mesh.bounds.center;
                float max = Mathf.Max(m_mesh.bounds.size.x, m_mesh.bounds.size.z);
                collider.radius = max * 0.5f;
                collider.height = m_mesh.bounds.size.y;
            }
        }

        // ------------------ Taper implementation ------------------
        // Height-based scale on XZ in builder-local space:
        // factor = 1 + ( (y + finalTaperOffset) * finalTaperScale )
        // finalTaperScale == 0 → disabled
        private void TryApplyFinalTaper()
        {
            if (m_builder == null || m_builder.m_vertices == null || m_builder.m_vertices.Count == 0)
                return;

            if (Mathf.Abs(finalTaperScale) <= 1e-6f)
                return;

            var verts = m_builder.m_vertices;
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i];

                float y = p.y + finalTaperOffset;
                float factor = 1f + (y * finalTaperScale);

                // Optional safety: avoid zero/negative factors unless you explicitly want inversion.
                if (factor <= 0f)
                    factor = 0.0001f;

                p.x *= factor;
                p.z *= factor;

                verts[i] = p;
            }
        }

        // ------------------ Noise implementation ------------------

        // Applies a lateral wobble similar to PathMesher's XZ noise:
        // p' = p + dir * width * noise(t)
        // where dir is lateral in XZ, width = projection length of (p - center) onto dir, and t is a smooth [0..1) param around the object.
        private void TryApplyFinalStageNoise()
        {
            if (m_builder == null || m_builder.m_vertices == null || m_builder.m_vertices.Count == 0) return;

            // All PP shapes build in local space. We'll stay in local and use pivotOffset as the center.
            Vector3 centerLocal = pivotOffset;

            // Precompute a lateral direction provider
            System.Func<int, Vector3> dir = (i) =>
            {
                if (noiseDirectionMode == NoiseDirectionMode.ObjectRightXZ)
                {
                    var d = m_right; // already rotated by 'rotation'
                    d.y = 0f;
                    if (d.sqrMagnitude < 1e-8f) d = Vector3.right;
                    return d.normalized;
                }
                else // RadialXZ
                {
                    Vector3 v = m_builder.m_vertices[i] - centerLocal;
                    v.y = 0f;
                    if (v.sqrMagnitude < 1e-8f) v = m_right; // fallback
                    return v.normalized;
                }
            };

            // Parameter t around the object (angle around pivot in XZ)
            System.Func<int, float> t = (i) =>
            {
                Vector3 v = m_builder.m_vertices[i] - centerLocal;
                float ang = Mathf.Atan2(v.x, v.z);       // [-π, π]
                float u = ang * (1f / (2f * Mathf.PI));  // [-0.5, 0.5]
                if (u < 0f) u += 1f;                     // [0,1)
                return u;
            };

            // Apply in-place
            var verts = m_builder.m_vertices;
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 p = verts[i];
                Vector3 d = dir(i);
                float   width = Mathf.Abs(Vector3.Dot(p - centerLocal, d)); // projection length along lateral dir
                float   ti = Mathf.Clamp01(t(i));

                float n = EvaluateNoise1D(
                    noiseSeed,
                    noiseOctaves,
                    noiseAmplitude,
                    noiseFrequency,
                    noisePersistence,
                    noiseLacunarity,
                    noiseHeightLacunarity,
                    /*layer*/ 0, /*heightDetail*/ 1,
                    ti,
                    noiseScale,
                    -1f, 1f);

                p += d * width * n;
                verts[i] = p;
            }
        }

        // Perlin octave stack along 1D parameter t
        private static float EvaluateNoise1D(
            int seed,
            int octaves,
            float amplitude,
            float frequency,
            float persistence,
            float lacunarity,
            float heightLacunarity,
            int layer,
            int heightDetail,
            float t,
            float scale,
            float min, float max)
        {
            float heightNormalized = heightDetail > 0 ? (float)layer / Mathf.Max(heightDetail, 1) : 0f;
            int   o = Mathf.Max(1, octaves);
            float amp = amplitude;
            float freq = frequency * (1f + heightLacunarity * heightNormalized);

            float sum = 0f;
            for (int i = 0; i < o; i++)
            {
                // Mathf.PerlinNoise needs 2D; use (seed, t*freq*scale) as a 1D stream
                float perlin = Mathf.PerlinNoise(seed, t * freq * scale);
                perlin = Mathf.Clamp01(perlin);
                sum += amp * Mathf.Lerp(min, max, perlin);

                amp  *= persistence;
                freq *= lacunarity;
            }
            return sum;
        }
    }
}
