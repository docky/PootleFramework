using System;
using System.Collections.Generic;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    /// <summary>
    /// Original first-pass weights: distance to articulated bone segments, followed by
    /// triangle-neighbour diffusion. Not ARP code or a volumetric/heat solver.
    /// </summary>
    internal static class RiggleSkinner
    {
        private struct Capsule
        {
            public Vector3 a, b;
            public float radius;
        }

        public static Mesh BuildSkinnedMesh(Riggle rig, MeshFilter source,
            List<RiggleBuilder.BoneSpec> deformBones)
        {
            Mesh copy = UnityEngine.Object.Instantiate(source.sharedMesh);
            copy.name = source.sharedMesh.name + "_RiggleSkinned";
            try
            {
                int boneCount = deformBones.Count;
                Vector3[] vertices = copy.vertices;
                int vertexCount = vertices.Length;
                if (vertexCount == 0) throw new InvalidOperationException("Mesh has no vertices: " + source.name);
                var topology = new RiggleTopology(copy);
                var parts = topology.GetParts(source.GetComponent<RiggleSkinBinding>());

                float characterHeight = Mathf.Max(0.05f,
                    Vector3.Distance(rig.GetWorld(Riggle.Point.HeadTop),
                        (rig.GetWorld(Riggle.Point.LeftAnkle) + rig.GetWorld(Riggle.Point.RightAnkle)) * 0.5f));
                var capsules = new Capsule[boneCount];
                var boneTransforms = new Transform[boneCount];
                for (int i = 0; i < boneCount; i++)
                {
                    var spec = deformBones[i];
                    float fraction = rig.TrunkRadius;
                    if (spec.region == RiggleBuilder.Region.Head) fraction = rig.HeadRadius;
                    else if (spec.region == RiggleBuilder.Region.Arm) fraction = rig.ArmRadius;
                    else if (spec.region == RiggleBuilder.Region.Leg) fraction = rig.LegRadius;
                    capsules[i] = new Capsule
                    {
                        a = spec.position,
                        b = spec.end,
                        radius = Mathf.Max(0.0001f, characterHeight * fraction)
                    };
                    boneTransforms[i] = spec.transform;
                }

                var bindposes = new Matrix4x4[boneCount];
                Matrix4x4 meshToWorld = source.transform.localToWorldMatrix;
                for (int i = 0; i < boneCount; i++)
                    bindposes[i] = boneTransforms[i].worldToLocalMatrix * meshToWorld;
                copy.bindposes = bindposes;

                // Dense small-bone array keeps the smoothing stage simple. 20 deform bones by default.
                float[] values = new float[vertexCount * boneCount];
                float smoothing = Mathf.Max(0.05f, rig.JointSoftness);
                float exponent = rig.WeightFalloff;
                for (int v = 0; v < vertexCount; v++)
                {
                    Vector3 world = meshToWorld.MultiplyPoint3x4(vertices[v]);
                    int index = v * boneCount;
                    float best = 0;
                    for (int i = 0; i < boneCount; i++)
                    {
                        if (!Allows(parts[v], deformBones[i].human)) continue;
                        Capsule c = capsules[i];
                        float distance = DistanceToSegment(world, c.a, c.b);
                        // Radius offsets govern blending without letting short bones dominate
                        // purely because their anatomical envelopes are small.
                        float score = Mathf.Pow(c.radius / (distance + c.radius * smoothing), exponent);
                        values[index + i] = score;
                        if (score > best) best = score;
                    }

                    float total = 0f;
                    float cutoff = best * 0.004f;
                    for (int i = 0; i < boneCount; i++)
                    {
                        float score = values[index + i];
                        if (score < cutoff) score = 0f;
                        values[index + i] = score;
                        total += score;
                    }
                    float inv = total > 1e-12f ? 1f / total : 0;
                    for (int i = 0; i < boneCount; i++) values[index + i] *= inv;
                }

                if (rig.Weighting == Riggle.WeightingMethod.ContiguousSurface)
                {
                    var graph = new RiggleSurfaceGraph(copy, topology, meshToWorld);
                    var owners = new int[vertexCount];
                    for (int v = 0; v < vertexCount; v++)
                        for (int b = 1; b < boneCount; b++)
                            if (values[v * boneCount + b] > values[v * boneCount + owners[v]]) owners[v] = b;
                    for (int b = 0; b < boneCount; b++)
                    {
                        var seeds = new float[vertexCount];
                        for (int v = 0; v < vertexCount; v++)
                            seeds[v] = owners[v] == b ? DistanceToSegment(meshToWorld.MultiplyPoint3x4(vertices[v]), capsules[b].a, capsules[b].b) : float.PositiveInfinity;
                        var distances = graph.Distances(seeds);
                        for (int v = 0; v < vertexCount; v++)
                            values[v * boneCount + b] = Allows(parts[v], deformBones[b].human) && !float.IsPositiveInfinity(distances[v])
                                ? Mathf.Pow(capsules[b].radius / (distances[v] + capsules[b].radius * smoothing), exponent) : 0;
                    }
                    for (int v = 0; v < vertexCount; v++)
                    {
                        float sum = 0;
                        for (int b = 0; b < boneCount; b++) sum += values[v * boneCount + b];
                        if (sum > 0) for (int b = 0; b < boneCount; b++) values[v * boneCount + b] /= sum;
                    }
                }

                // Surface diffusion only follows triangle connectivity: e.g. a hand close to a
                // trouser leg does not get blended simply because their surfaces are nearby.
                if (rig.SurfaceSmoothSteps > 0 && rig.SurfaceSmoothAmount > 0f)
                    SmoothByTriangleNeighbours(copy.triangles, vertexCount, boneCount, values,
                        rig.SurfaceSmoothSteps, rig.SurfaceSmoothAmount, topology);

                topology.ShareSeamWeights(values, boneCount);

                BoneWeight[] result = new BoneWeight[vertexCount];
                for (int v = 0; v < vertexCount; v++)
                {
                    int at = v * boneCount;
                    int a = 0, b = 0, c = 0, d = 0;
                    float wa = -1, wb = -1, wc = -1, wd = -1;
                    for (int i = 0; i < boneCount; i++)
                    {
                        float w = values[at + i];
                        if (w > wa)
                        {
                            wd = wc; d = c;
                            wc = wb; c = b;
                            wb = wa; b = a;
                            wa = w; a = i;
                        }
                        else if (w > wb)
                        {
                            wd = wc; d = c;
                            wc = wb; c = b;
                            wb = w; b = i;
                        }
                        else if (w > wc)
                        {
                            wd = wc; d = c;
                            wc = w; c = i;
                        }
                        else if (w > wd)
                        {
                            wd = w; d = i;
                        }
                    }
                    wa = Mathf.Max(0, wa); wb = Mathf.Max(0, wb);
                    wc = Mathf.Max(0, wc); wd = Mathf.Max(0, wd);
                    float sum = wa + wb + wc + wd;
                    if (sum < 1e-10f)
                    {
                        result[v] = new BoneWeight { boneIndex0 = 0, weight0 = 1 };
                        continue;
                    }
                    float inverse = 1f / sum;
                    result[v] = new BoneWeight
                    {
                        boneIndex0 = a, weight0 = wa * inverse,
                        boneIndex1 = b, weight1 = wb * inverse,
                        boneIndex2 = c, weight2 = wc * inverse,
                        boneIndex3 = d, weight3 = wd * inverse
                    };
                }

                copy.boneWeights = result;
                return copy;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }
        }

        private static float DistanceToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 v = end - start;
            float lengthSq = v.sqrMagnitude;
            if (lengthSq < 1e-10f) return Vector3.Distance(point, start);
            float t = Mathf.Clamp01(Vector3.Dot(point - start, v) / lengthSq);
            return Vector3.Distance(point, start + t * v);
        }

        private static void SmoothByTriangleNeighbours(int[] triangles, int vertices, int bones,
            float[] weights, int iterations, float amount, RiggleTopology topology)
        {
            if (triangles == null || triangles.Length < 3) return;
            int[] counts = new int[vertices];
            var scratch = new float[weights.Length];
            float[] original = weights; // always keep the same output buffer for the caller
            float[] current = weights;
            float[] next = new float[weights.Length];
            for (int pass = 0; pass < iterations; pass++)
            {
                topology.ShareSeamWeights(current, bones);
                Array.Clear(scratch, 0, scratch.Length);
                Array.Clear(counts, 0, counts.Length);
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t + 1], c = triangles[t + 2];
                    if (a < 0 || b < 0 || c < 0 || a >= vertices || b >= vertices || c >= vertices)
                        continue;
                    AddNeighbours(a, b, c, bones, current, scratch, counts);
                    AddNeighbours(b, a, c, bones, current, scratch, counts);
                    AddNeighbours(c, a, b, bones, current, scratch, counts);
                }

                for (int v = 0; v < vertices; v++)
                {
                    int index = v * bones;
                    float alpha = counts[v] > 0 ? amount : 0;
                    float averageMultiplier = counts[v] > 0 ? alpha / counts[v] : 0;
                    float sum = 0;
                    for (int bone = 0; bone < bones; bone++)
                    {
                        int at = index + bone;
                        float w = (1 - alpha) * current[at] + averageMultiplier * scratch[at];
                        next[at] = w;
                        sum += w;
                    }
                    if (sum > 1e-10f)
                    {
                        float inverse = 1f / sum;
                        for (int bone = 0; bone < bones; bone++) next[index + bone] *= inverse;
                    }
                }
                var temp = current; current = next; next = temp;
            }

            if (current != original) Array.Copy(current, original, current.Length);
        }

        private static bool Allows(RiggleSkinBinding.Part part, HumanBodyBones bone)
        {
            switch (part)
            {
                case RiggleSkinBinding.Part.Automatic: return true;
                case RiggleSkinBinding.Part.Torso: return bone == HumanBodyBones.Hips || bone == HumanBodyBones.Spine || bone == HumanBodyBones.Chest || bone == HumanBodyBones.UpperChest || bone == HumanBodyBones.Neck;
                case RiggleSkinBinding.Part.Head: return bone == HumanBodyBones.Head;
                case RiggleSkinBinding.Part.Hips: return bone == HumanBodyBones.Hips;
                case RiggleSkinBinding.Part.LeftArm: return bone == HumanBodyBones.LeftUpperArm || bone == HumanBodyBones.LeftLowerArm;
                case RiggleSkinBinding.Part.RightArm: return bone == HumanBodyBones.RightUpperArm || bone == HumanBodyBones.RightLowerArm;
                case RiggleSkinBinding.Part.LeftHand: return bone == HumanBodyBones.LeftHand;
                case RiggleSkinBinding.Part.RightHand: return bone == HumanBodyBones.RightHand;
                case RiggleSkinBinding.Part.LeftLeg: return bone == HumanBodyBones.LeftUpperLeg || bone == HumanBodyBones.LeftLowerLeg;
                case RiggleSkinBinding.Part.RightLeg: return bone == HumanBodyBones.RightUpperLeg || bone == HumanBodyBones.RightLowerLeg;
                case RiggleSkinBinding.Part.LeftFoot: return bone == HumanBodyBones.LeftFoot;
                case RiggleSkinBinding.Part.RightFoot: return bone == HumanBodyBones.RightFoot;
                default: return false;
            }
        }

        private static void AddNeighbours(int target, int neighbour1, int neighbour2,
            int bones, float[] from, float[] accum, int[] count)
        {
            int dst = target * bones;
            int a = neighbour1 * bones;
            int b = neighbour2 * bones;
            for (int i = 0; i < bones; i++) accum[dst + i] += from[a + i] + from[b + i];
            count[target] += 2;
        }
    }
}
