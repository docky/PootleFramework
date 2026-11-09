using System.Collections.Generic;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    /// <summary>Multi-source Dijkstra distances on welded mesh edges. Disconnected islands never exchange influence.</summary>
    internal sealed class RiggleSurfaceGraph
    {
        readonly List<Edge>[] neighbours;
        readonly int[] welded;
        struct Edge { public int vertex; public float length; public Edge(int v, float d) { vertex = v; length = d; } }
        public RiggleSurfaceGraph(Mesh mesh, RiggleTopology topology, Matrix4x4 toWorld)
        {
            welded = topology.welded;
            var vertices = mesh.vertices;
            neighbours = new List<Edge>[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            { vertices[i] = toWorld.MultiplyPoint3x4(vertices[i]); neighbours[i] = new List<Edge>(); }
            var edges = new HashSet<ulong>();
            void Add(int a, int b)
            {
                a = welded[a]; b = welded[b]; if (a == b) return;
                ulong key = ((ulong)(uint)Mathf.Min(a, b) << 32) | (uint)Mathf.Max(a, b);
                if (!edges.Add(key)) return;
                float distance = Vector3.Distance(vertices[a], vertices[b]);
                neighbours[a].Add(new Edge(b, distance)); neighbours[b].Add(new Edge(a, distance));
            }
            var triangles = mesh.triangles;
            for (int t = 0; t < triangles.Length; t += 3)
            { Add(triangles[t], triangles[t + 1]); Add(triangles[t + 1], triangles[t + 2]); Add(triangles[t + 2], triangles[t]); }
        }

        public float[] Distances(float[] seeds)
        {
            var distance = new float[welded.Length];
            for (int i = 0; i < distance.Length; i++) distance[i] = float.PositiveInfinity;
            var heap = new MinHeap();
            for (int i = 0; i < seeds.Length; i++)
                if (seeds[i] < distance[welded[i]])
                { distance[welded[i]] = seeds[i]; heap.Push(new Edge(welded[i], seeds[i])); }
            while (heap.Count > 0)
            {
                var current = heap.Pop();
                if (current.length > distance[current.vertex]) continue;
                foreach (var edge in neighbours[current.vertex])
                {
                    float next = current.length + edge.length;
                    if (next >= distance[edge.vertex]) continue;
                    distance[edge.vertex] = next; heap.Push(new Edge(edge.vertex, next));
                }
            }
            for (int i = 0; i < distance.Length; i++) distance[i] = distance[welded[i]];
            return distance;
        }

        sealed class MinHeap
        {
            readonly List<Edge> items = new List<Edge>();
            public int Count => items.Count;
            public void Push(Edge item)
            {
                int index = items.Count; items.Add(item);
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (items[parent].length <= item.length) break;
                    items[index] = items[parent]; index = parent;
                }
                items[index] = item;
            }
            public Edge Pop()
            {
                var result = items[0]; var last = items[items.Count - 1]; items.RemoveAt(items.Count - 1);
                if (items.Count == 0) return result;
                int index = 0;
                while (index * 2 + 1 < items.Count)
                {
                    int child = index * 2 + 1;
                    if (child + 1 < items.Count && items[child + 1].length < items[child].length) child++;
                    if (items[child].length >= last.length) break;
                    items[index] = items[child]; index = child;
                }
                items[index] = last; return result;
            }
        }
    }
}
