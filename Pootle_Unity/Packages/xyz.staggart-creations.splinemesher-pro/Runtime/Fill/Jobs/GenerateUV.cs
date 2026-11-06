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

namespace sc.splinemesher.pro.runtime
{
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    public struct GenerateUV : IJob, IDisposable
    {
        [ReadOnly] private NativeArray<float3> points;
        [ReadOnly] private NativeArray<float> distances;
        private float3 worldPosition;

        private FillMeshSettings.UV settings;
        private float3 boundsMin, boundsMax, boundsSize;
        
        [WriteOnly] private NativeArray<float4> uv0;
        [WriteOnly] private NativeArray<float2> uv1;
        
        public void Setup(float4x4 worldToLocal, NativeArray<float3> points, NativeArray<float> distances, Vector3 boundsMin, Vector3 boundsMax, FillMeshSettings.UV uvSettings)
        {
            this.points = points;
            this.distances = distances;
            this.boundsMin = boundsMin;
            this.boundsMax = boundsMax;
            this.boundsSize = (boundsMax - boundsMin) * 0.5f;
            this.worldPosition = -worldToLocal.c3.xyz;

            this.settings = uvSettings;

            uv0 = new NativeArray<float4>(points.Length, Allocator.Persistent);
            uv1 = new NativeArray<float2>(points.Length, Allocator.Persistent);
        }
        
        public void Execute()
        {
            float maxDistance = float.MinValue;
            for (int i = 0; i < distances.Length; i++)
            {
                maxDistance = math.max(maxDistance, distances[i]);
            }
            
            //Vertex positions are in world-space
            for (int i = 0; i < points.Length; i++)
            {
                //Convert world position to bounds-local coordinates
                float3 localPos = (points[i] - boundsMin) / boundsMax;
                localPos *= 0.5f;
                
                float4 uv = new float4(localPos.x, localPos.z, distances[i], distances[i] / maxDistance);
                
                //Lightmap UV, fit to the mesh. Need to be normalized with a small margin
                float2 lightmapUV = new float2(0.01f + (uv.x * 0.99f), 0.01f + (uv.y * 0.99f));
                
                uv.xy = new float2(points[i].x + worldPosition.x, points[i].z + worldPosition.z);
                
                if (settings.fitToMesh)
                {
                    uv.xy = lightmapUV;
                }
                
                //Classic tiling & offset
                uv.xy *= settings.tiling;
                uv.xy += settings.offset;

                //Rotate 90 degrees
                if (settings.rotate) (uv.x, uv.y) = (uv.y, uv.x);
                
                uv.x = math.select(uv.x, 1f - uv.x, settings.FlipX);
                uv.y = math.select(uv.y, 1f - uv.y, settings.FlipY);

                uv0[i] = uv;
                uv1[i] = lightmapUV;
            }
        }

        public NativeArray<float4> GetUV0()
        {
            return uv0;
        }
        
        public NativeArray<float2> GetUV1()
        {
            return uv1;
        }

        public void Dispose()
        {
            uv0.Dispose();
            uv1.Dispose();
        }
    }
}