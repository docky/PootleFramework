using System.Collections.Generic;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    // Weld only for weight calculation; keep the actual UVs, normals and mesh indices untouched.
    internal sealed class RiggleTopology
    {
        public readonly int[] welded;
        public readonly int[] islands;
        readonly int[] counts;

        public RiggleTopology(Mesh mesh)
        {
            var vertices = mesh.vertices;
            welded = new int[vertices.Length];
            islands = new int[vertices.Length];
            counts = new int[vertices.Length];
            float tolerance = Mathf.Max(mesh.bounds.size.magnitude * 0.000001f, 0.0000001f);
            var buckets = new Dictionary<Vector3Int, List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                var cell = Vector3Int.FloorToInt(vertices[i] / tolerance);
                int representative = -1;
                for (int x = -1; x <= 1 && representative < 0; x++)
                for (int y = -1; y <= 1 && representative < 0; y++)
                for (int z = -1; z <= 1 && representative < 0; z++)
                    if (buckets.TryGetValue(cell + new Vector3Int(x, y, z), out var candidates))
                        foreach (int candidate in candidates)
                            if ((vertices[i] - vertices[candidate]).sqrMagnitude <= tolerance * tolerance)
                            { representative = candidate; break; }
                if (representative < 0)
                {
                    representative = i;
                    if (!buckets.TryGetValue(cell, out var list)) buckets[cell] = list = new List<int>();
                    list.Add(i);
                }
                welded[i] = representative;
                islands[i] = representative;
                counts[representative]++;
            }
            var triangles = mesh.triangles;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Join(triangles[i], triangles[i + 1]);
                Join(triangles[i], triangles[i + 2]);
            }
            for (int i = 0; i < islands.Length; i++) islands[i] = Find(i);
        }

        int Find(int i) => islands[i] == i ? i : islands[i] = Find(islands[i]);
        void Join(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) islands[Mathf.Max(ra, rb)] = Mathf.Min(ra, rb); }

        public void ShareSeamWeights(float[] weights, int bones)
        {
            var sums = new float[weights.Length];
            for (int v = 0; v < welded.Length; v++)
                for (int b = 0; b < bones; b++) sums[welded[v] * bones + b] += weights[v * bones + b];
            for (int v = 0; v < welded.Length; v++)
                for (int b = 0; b < bones; b++) weights[v * bones + b] = sums[welded[v] * bones + b] / counts[welded[v]];
        }

        public RiggleSkinBinding.Part[] GetParts(RiggleSkinBinding binding)
        {
            var parts = new RiggleSkinBinding.Part[welded.Length];
            if (!binding) return parts;
            var overrides = new Dictionary<int, RiggleSkinBinding.Part>();
            foreach (var island in binding.islands)
            {
                if (island.seedVertex < 0 || island.seedVertex >= islands.Length)
                    throw new System.InvalidOperationException("Riggle island seed is outside the source mesh: " + binding.name);
                overrides[islands[island.seedVertex]] = island.part;
            }
            for (int i = 0; i < parts.Length; i++)
                parts[i] = overrides.TryGetValue(islands[i], out var part) ? part : binding.wholeMesh;
            return parts;
        }
    }
}
