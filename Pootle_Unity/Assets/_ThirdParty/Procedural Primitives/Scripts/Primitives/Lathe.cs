using System.Collections.Generic;
using UnityEngine;

namespace ProceduralPrimitivesUtil
{
    /// <summary>
    /// Lathe / Revolve primitive.
    /// Profile: X = radius, Y = height (along the local up axis).
    /// Extends PPCircularBase to reuse slice / UV / pivot options.
    /// </summary>
    [AddComponentMenu("Procedural Primitives/Lathe (Revolve)")]
    public class Lathe : PPCircularBase
    {
        [Tooltip("Profile points from bottom (min Y) to top (max Y). X is radius, Y is height.")]
        public List<Vector2> profile = new List<Vector2>();

        [Min(1)]
        public int profileSubdivisions = 1;

        [Header("Caps")]
        public bool capBottom = true;
        public bool capTop    = true;

        [Header("Hard Edges Along Profile")]
        [Tooltip("Duplicate samples in the 2D profile when the bend angle exceeds the threshold, creating a hard edge around the lathe.")]
        public bool angleBasedSmoothing = false;   // default OFF to keep shapes smooth

        [Range(1f, 180f)]
        public float hardEdgeAngle = 60f;

        [Header("Subdivide Shape")]
        [Range(0f, 1f)]
        [Tooltip("0 = straight linear subdivisions, 1 = fully curved (Catmull-Rom).")]
        public float curvedAmount = 1f;

        protected override void CreateMesh()
        {
            m_mesh.name = "Lathe";

            if (profile == null || profile.Count < 2)
            {
                BuildDefaultPearProfile();
            }

            sides = Mathf.Clamp(sides, 3, 100);
            profileSubdivisions = Mathf.Max(1, profileSubdivisions);

            sliceFrom = Mathf.Clamp(sliceFrom, 0f, 360f);
            sliceTo   = Mathf.Clamp(sliceTo,   sliceFrom, 360f);

            // Sampled 2D profile (radius / height)
            List<Vector2> prof = BuildProfileSamples(profile, profileSubdivisions, curvedAmount);
            if (prof.Count < 2)
                return;

            // Optional: duplicate profile samples at sharp bends so we can get hard edges
            if (angleBasedSmoothing && hardEdgeAngle > 0f && hardEdgeAngle < 179f && prof.Count >= 3)
            {
                prof = ApplyPathHardEdges(prof, hardEdgeAngle);
            }

            int cols = prof.Count;

            // Height range for UVs
            float minY = prof[0].y;
            float maxY = prof[0].y;
            for (int i = 1; i < cols; i++)
            {
                float y = prof[i].y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            float height = Mathf.Max(0.0001f, maxY - minY);

            // We always build sides+1 rings so we have an explicit seam ring at 360°
            int ringCount = sides + 1;

            float totalDeg = sliceOn ? (sliceTo - sliceFrom) : 360f;
            float startDeg = sliceOn ? sliceFrom : 0f;

            // ----------- SIDE SURFACE VERTICES -----------
            for (int r = 0; r < ringCount; r++)
            {
                float tAround = (float)r / sides; // 0..1, last ring = 1
                float angDeg  = startDeg + tAround * totalDeg;
                float angRad  = angDeg * Mathf.Deg2Rad;

                float ca = Mathf.Cos(angRad);
                float sa = Mathf.Sin(angRad);

                for (int c = 0; c < cols; c++)
                {
                    Vector2 pc = prof[c];
                    float radius = Mathf.Max(0f, pc.x);
                    float y      = pc.y;

                    // Position in PPBase frame
                    Vector3 pos =
                        pivotOffset +
                        m_right   * (radius * ca) +
                        m_up      * y +
                        m_forward * (radius * sa);

                    // Tangent along profile in 2D (radius / height)
                    Vector2 t2 = TangentAt(prof, c);

                    // Surface derivatives:
                    // dProfile: movement along profile, rotated around axis
                    Vector3 dProfile =
                        m_right   * (t2.x * ca) +
                        m_up      *  t2.y       +
                        m_forward * (t2.x * sa);

                    // dTheta: movement around the axis, holding radius/height constant
                    Vector3 dTheta =
                        m_right   * (-radius * sa) +
                        m_forward * ( radius * ca);

                    // True lathe normal from cross product of surface tangents
                    Vector3 n = Vector3.Cross(dProfile, dTheta).normalized;
                    if (flipNormals) n = -n;

                    // UVs
                    float v = (y - minY) / height;

                    Vector2 uv;
                    if (generateMappingCoords)
                    {
                        float u = tAround; // seam at 0 / 1
                        if (realWorldMapSize)
                            uv = new Vector2(u * UVTiling.x, v * UVTiling.y) + UVOffset;
                        else
                            uv = new Vector2(u, v);
                    }
                    else
                    {
                        uv = Vector2.zero;
                    }

                    m_builder.m_vertices.Add(pos);
                    m_builder.m_normals.Add(n);
                    m_builder.m_uv.Add(uv);
                }
            }

            // ----------- SIDE SURFACE TRIANGLES -----------
            // Connect ring r to r+1; last ring (r = sides) is only used as "next" of r = sides-1.
            for (int r = 0; r < sides; r++)
            {
                int rNext = r + 1;
                for (int c = 0; c < cols - 1; c++)
                {
                    int a = r      * cols + c;
                    int b = rNext  * cols + c;
                    int d = r      * cols + (c + 1);
                    int e = rNext  * cols + (c + 1);

                    AddTri(a, b, e);
                    AddTri(a, e, d);
                }
            }

            // ----------- BOTTOM CAP -----------
            if (capBottom && prof[0].x > 0f)
            {
                int centerIndex = m_builder.m_vertices.Count;
                float y = prof[0].y;

                Vector3 cpos = pivotOffset + m_up * y;
                Vector3 cn   = -m_up;
                if (flipNormals) cn = -cn;

                m_builder.m_vertices.Add(cpos);
                m_builder.m_normals.Add(cn);
                m_builder.m_uv.Add(new Vector2(0.5f, 0.5f));

                int col = 0;

                if (sliceOn)
                {
                    // Only along the visible arc
                    int maxRing = Mathf.RoundToInt(sides * (totalDeg / 360f));
                    maxRing = Mathf.Clamp(maxRing, 1, sides);
                    for (int r = 0; r < maxRing; r++)
                    {
                        int rNext = r + 1;
                        int a = r      * cols + col;
                        int b = rNext  * cols + col;
                        AddTri(centerIndex, b, a);
                    }
                }
                else
                {
                    // Full 360°
                    for (int r = 0; r < sides; r++)
                    {
                        int rNext = r + 1;
                        int a = r      * cols + col;
                        int b = rNext  * cols + col;
                        AddTri(centerIndex, b, a);
                    }
                }
            }

            // ----------- TOP CAP -----------
            if (capTop && prof[cols - 1].x > 0f)
            {
                int centerIndex = m_builder.m_vertices.Count;
                float y = prof[cols - 1].y;

                Vector3 cpos = pivotOffset + m_up * y;
                Vector3 cn   = m_up;
                if (flipNormals) cn = -cn;

                m_builder.m_vertices.Add(cpos);
                m_builder.m_normals.Add(cn);
                m_builder.m_uv.Add(new Vector2(0.5f, 0.5f));

                int col = cols - 1;

                if (sliceOn)
                {
                    int maxRing = Mathf.RoundToInt(sides * (totalDeg / 360f));
                    maxRing = Mathf.Clamp(maxRing, 1, sides);
                    for (int r = 0; r < maxRing; r++)
                    {
                        int rNext = r + 1;
                        int a = r      * cols + col;
                        int b = rNext  * cols + col;
                        AddTri(centerIndex, a, b);
                    }
                }
                else
                {
                    for (int r = 0; r < sides; r++)
                    {
                        int rNext = r + 1;
                        int a = r      * cols + col;
                        int b = rNext  * cols + col;
                        AddTri(centerIndex, a, b);
                    }
                }
            }

            // ----------- SLICE WALLS -----------
            if (sliceOn)
            {
                BuildSliceWall(prof, cols, minY, height, sliceFrom);
                BuildSliceWall(prof, cols, minY, height, sliceTo);
            }
        }

        // Triangle helper with outward default winding
        void AddTri(int i0, int i1, int i2)
        {
            if (!flipNormals)
            {
                m_builder.m_triangles.Add(i2);
                m_builder.m_triangles.Add(i1);
                m_builder.m_triangles.Add(i0);
            }
            else
            {
                m_builder.m_triangles.Add(i0);
                m_builder.m_triangles.Add(i1);
                m_builder.m_triangles.Add(i2);
            }
        }

        // Build a radial wall at angleDeg, double-sided
        void BuildSliceWall(List<Vector2> prof, int cols, float minY, float height, float angleDeg)
        {
            float angRad = angleDeg * Mathf.Deg2Rad;
            float ca = Mathf.Cos(angRad);
            float sa = Mathf.Sin(angRad);

            Vector3 radial = m_right * ca + m_forward * sa;
            Vector3 planeNormal = Vector3.Cross(radial, m_up).normalized;
            if (flipNormals) planeNormal = -planeNormal;

            int baseIndex = m_builder.m_vertices.Count;

            for (int c = 0; c < cols; c++)
            {
                float radius = Mathf.Max(0f, prof[c].x);
                float y      = prof[c].y;

                Vector3 axisPos = pivotOffset + m_up * y;
                Vector3 surfPos = axisPos + radial * radius;

                float v = (y - minY) / height;

                Vector2 uvAxis, uvSurf;
                if (generateMappingCoords)
                {
                    if (realWorldMapSize)
                    {
                        uvAxis = new Vector2(0f * UVTiling.x, v * UVTiling.y) + UVOffset;
                        uvSurf = new Vector2(1f * UVTiling.x, v * UVTiling.y) + UVOffset;
                    }
                    else
                    {
                        uvAxis = new Vector2(0f, v);
                        uvSurf = new Vector2(1f, v);
                    }
                }
                else
                {
                    uvAxis = uvSurf = Vector2.zero;
                }

                m_builder.m_vertices.Add(axisPos);
                m_builder.m_normals.Add(planeNormal);
                m_builder.m_uv.Add(uvAxis);

                m_builder.m_vertices.Add(surfPos);
                m_builder.m_normals.Add(planeNormal);
                m_builder.m_uv.Add(uvSurf);
            }

            int Axis(int c) => baseIndex + c * 2;
            int Surf(int c) => baseIndex + c * 2 + 1;

            for (int c = 0; c < cols - 1; c++)
            {
                int a = Axis(c);
                int b = Surf(c);
                int d = Axis(c + 1);
                int e = Surf(c + 1);

                // front
                AddTri(a, b, e);
                AddTri(a, e, d);
                // back (double sided)
                AddTri(a, e, b);
                AddTri(a, d, e);
            }
        }

        // Duplicate samples where bend angle exceeds threshold
        static List<Vector2> ApplyPathHardEdges(List<Vector2> src, float angleThresholdDeg)
        {
            int n = src.Count;
            if (n < 3)
                return src;

            var dst = new List<Vector2>(n + 4);
            dst.Add(src[0]);

            for (int i = 1; i < n - 1; i++)
            {
                Vector2 prev = src[i - 1];
                Vector2 cur  = src[i];
                Vector2 next = src[i + 1];

                Vector2 v0 = cur - prev;
                Vector2 v1 = next - cur;

                if (v0.sqrMagnitude < 1e-8f || v1.sqrMagnitude < 1e-8f)
                {
                    dst.Add(cur);
                    continue;
                }

                v0.Normalize();
                v1.Normalize();

                float dot = Mathf.Clamp(Vector2.Dot(v0, v1), -1f, 1f);
                float ang = Mathf.Acos(dot) * Mathf.Rad2Deg;

                dst.Add(cur);

                if (ang >= angleThresholdDeg)
                {
                    dst.Add(cur); // duplicate at sharp bend
                }
            }

            dst.Add(src[n - 1]);
            return dst;
        }

        // Default pear-like profile
        void BuildDefaultPearProfile()
        {
            profile = new List<Vector2>
            {
                new Vector2(0.02f, 0.00f),
                new Vector2(0.10f, 0.02f),
                new Vector2(0.22f, 0.15f),
                new Vector2(0.28f, 0.30f),
                new Vector2(0.22f, 0.45f),
                new Vector2(0.14f, 0.58f),
                new Vector2(0.08f, 0.70f),
                new Vector2(0.05f, 0.82f),
                new Vector2(0.04f, 0.90f)
            };
        }

        // Subdivide profile with optional Catmull-Rom smoothing and padded endpoints
        static List<Vector2> BuildProfileSamples(List<Vector2> src, int subdiv, float curvedAmount)
        {
            int n = src.Count;
            subdiv = Mathf.Max(1, subdiv);

            // Straight linear sampling
            if (curvedAmount <= 0f || n < 2)
            {
                var outLinear = new List<Vector2>(n * subdiv + 1);
                for (int i = 0; i < n - 1; i++)
                {
                    Vector2 p0 = src[i];
                    Vector2 p1 = src[i + 1];
                    for (int s = 0; s < subdiv; s++)
                    {
                        float t = (float)s / subdiv;
                        outLinear.Add(Vector2.Lerp(p0, p1, t));
                    }
                }
                outLinear.Add(src[n - 1]);
                return outLinear;
            }

            // Pad ends with extra control points for Catmull-Rom
            var ctrl = new List<Vector2>(n + 2);
            ctrl.Add(src[0]);
            ctrl.AddRange(src);
            ctrl.Add(src[n - 1]);

            var outPts = new List<Vector2>(n * subdiv + 1);

            for (int seg = 0; seg < n - 1; seg++)
            {
                Vector2 p0 = ctrl[seg];
                Vector2 p1 = ctrl[seg + 1];
                Vector2 p2 = ctrl[seg + 2];
                Vector2 p3 = ctrl[seg + 3];

                for (int s = 0; s < subdiv; s++)
                {
                    float t = (float)s / subdiv;

                    Vector2 linear = Vector2.Lerp(p1, p2, t);
                    Vector2 smooth = CatmullRom(p0, p1, p2, p3, t);
                    Vector2 blended = Vector2.Lerp(linear, smooth, curvedAmount);

                    outPts.Add(blended);
                }
            }

            outPts.Add(src[n - 1]);
            return outPts;
        }

        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3
            );
        }

        // Tangent of profile at index i in a sampled list
        static Vector2 TangentAt(IReadOnlyList<Vector2> pts, int i)
        {
            int n = pts.Count;
            if (n < 2)
                return Vector2.up;

            int i0 = Mathf.Max(0, i - 1);
            int i1 = Mathf.Min(n - 1, i + 1);
            Vector2 t = pts[i1] - pts[i0];
            if (t.sqrMagnitude < 1e-8f)
                t = Vector2.up;
            return t.normalized;
        }
    }
}