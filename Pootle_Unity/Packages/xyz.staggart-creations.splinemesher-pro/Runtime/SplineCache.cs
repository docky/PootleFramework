// Spline Mesher Pro © Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//
// ⚠️ WARNING: UNAUTHORIZED USE OR DISTRIBUTION IS STRICTLY PROHIBITED
// • Copying, referencing, or reverse-engineering this source code for the creation of new Asset Store or derivative products,
//   or any other publicly distributed content is strictly forbidden and will result in legal action.
// • Studying this file for the purpose of reproducing its functionality in your own assets or tools is not permitted.
// • If you are viewing this file as a reference, please close it immediately to avoid unintentional design influence or potential EULA violations.
// • Uploading this file or any derivative of it to a public GitHub or similar repository will trigger an automated DMCA takedown request.
// • Studying to understand for personal, educational or integration purposes is allowed, studying to reproduce is not.

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using SplinePoint = sc.splinemesher.pro.runtime.Structs.SplinePoint;

namespace sc.splinemesher.pro.runtime
{
	[BurstCompile]
    //Cache pre-evaluated spline points for much faster (repeated) sampling
    public struct SplineCache : IJobParallelFor
    {
        private readonly int sampleCount;
        private readonly NativeSpline spline;
        private readonly bool closed;
        private readonly bool smoothLinearTangents;

        private NativeArray<SplinePoint> points;
        public NativeArray<SplinePoint> Points => points;
        
        private const float TANGENT_BLEND_RANGE = 0.01f; //1cm on each side of the sample

        public SplineCache(NativeSpline nativeSpline, float sampleDistance, bool smoothLinearTangents = false)
        {
            this.spline = nativeSpline;
            this.closed = nativeSpline.Closed;
            this.smoothLinearTangents = smoothLinearTangents;
            
            sampleCount = Mathf.CeilToInt(nativeSpline.GetLength() / sampleDistance);
            sampleCount = Mathf.Max(4, sampleCount);
            
            //Add 1 extra position for closed splines to store the closing point
            int arraySize = closed ? sampleCount + 1 : sampleCount;
            points = new NativeArray<SplinePoint>(arraySize, Allocator.TempJob);
            
            //Schedule jobs for all positions (including the extra closing point for closed splines)
            int jobCount = points.Length;

            JobHandle jobHandle = this.Schedule(jobCount, 64);
            jobHandle.Complete();
        }
        
        public void Execute(int i)
        {
            float t = i / (float)(sampleCount-1);
            bool start = i == 0;
            bool end = i == sampleCount-1;
            
            spline.Evaluate(t, out float3 position, out float3 tangent, out float3 up);
            
            float knotT = spline.ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Knot);
            int knotIndex = math.min((int)math.round(knotT), spline.Count-1);
            
            BezierKnot knot = spline.Knots[knotIndex];
            var linearKnot = math.lengthsq(knot.TangentOut + knot.TangentIn) < (0.002f);
            
            //A closed spline with its first knot being linear will cause a gap, as the tangents diverge
            bool seam = closed && (start || end) && math.length(spline.Knots[0].TangentOut) < 0.01f;
            var blendTangents = (smoothLinearTangents && linearKnot) || seam;
            //blendTangents = false;
            
            if (blendTangents)
            {
                GetBlendedTangents(spline, t, out tangent, out up);
            }
            else
            {
                if (start)
                {
                    //At the very start of the spline the tangent is often not valid. Sample up ahead a fraction
                    spline.Evaluate(Utilities.MIN_T_VALUE, out _, out tangent, out up);
                }

                if (end)
                {
                    spline.Evaluate(Utilities.MAX_T_VALUE, out _, out tangent, out up);
                }
            }
            
            #if DEBUG
            //If knots have a NaN rotation, or are overlapping, the spline data will be invalid as well
            if (float.IsNaN(position.x) || float.IsNaN(tangent.x) || float.IsNaN(up.y))
            {
                Debug.LogError(
                    $"[Spline Mesher] Spline used with an invalid curve (NaN at {t}). Knot rotations may be invalid, or knots are exactly overlapping. This safeguard not available in release builds and will lead to memory leaks, ensure you provide only valid splines.");
                tangent = math.forward();
                up = math.up();
            }
            
            //if(math.length(tangent) <= 0.01f) Debug.LogWarning($"[Spline Mesher] Invalid spline tangent at {t}. Knot rotations may be invalid, or knots are exactly overlapping");
            #endif
            
            points[i] = new SplinePoint(position, tangent, up);
        }

        //Sample the spline around the 't' point and average the tangent/up vectors
        private static void GetBlendedTangents(NativeSpline spline, float t, out float3 tangent, out float3 up)
        {
            //Normalized distance
            float offset = math.max(0.01f, TANGENT_BLEND_RANGE / spline.GetLength());
            
            var t0 = Utilities.WrapT(t - offset);
            var t1 = Utilities.WrapT(t + offset);
            spline.Evaluate(t0, out _, out float3 tangentA, out float3 upA);
            spline.Evaluate(t1, out _, out float3 tangentB, out float3 upB);

            tangent = math.lerp(tangentA, tangentB, 0.5f);
            up = math.lerp(upA, upB, 0.5f);
        }

        public void Dispose()
        {
            if (points.IsCreated) points.Dispose();
        }

        [BurstCompile]
        public static void Sample(ref NativeArray<SplinePoint> points, float t, out float3 position, out float3 tangent, out float3 up)
        {
            int pointCount = points.Length;
            
            if (pointCount == 0)
            {
                position = default;
                tangent = default;
                up = default;
                return;
            }
            
            float i = t * (float)(pointCount - 1);

            int prev = (int)math.floor(i);
            int next = (int)math.floor(i + 1);

            if (next >= pointCount)
            {
                position = points[pointCount - 1].position;
                tangent = points[pointCount - 1].tangent;
                up = points[pointCount - 1].up;
                return;
            }

            if (prev < 0)
            {
                position = points[0].position;
                tangent = points[0].tangent;
                up = points[0].up;
                return;
            }

            SplinePoint a = points[prev];
            SplinePoint b = points[next];

            t = i - prev;
            
            position = math.lerp(a.position, b.position, t);
            tangent = math.lerp(a.tangent, b.tangent, t);
            up = math.lerp(a.up, b.up, t);
        }
    }
}