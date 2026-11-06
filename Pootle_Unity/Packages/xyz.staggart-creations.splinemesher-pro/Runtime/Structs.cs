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
using Unity.Mathematics;
using UnityEngine;

namespace sc.splinemesher.pro.runtime
{
    public static class Structs
    {
        public struct InputMeshData
        {
            [ReadOnly]
            public NativeArray<float3> positions;
            public ushort vertexCount;
            [ReadOnly]
            public NativeArray<float3> normals;
            [ReadOnly]
            public NativeArray<float4> tangents;
            [ReadOnly]
            public NativeArray<float2> uv;
            public NativeArray<float4> colors;
            
            //Array may be written to if triangle order is reversed
            public NativeArray<ushort> sourceTriangles;
            public int submeshCount;
            [ReadOnly]
            public NativeArray<int2> sourceSubmeshRanges;
            
            public BoundsData bounds;
            
            public bool IsCreated => positions.IsCreated;

            public void CreateEmpty()
            {
                vertexCount = 0;
                positions = new NativeArray<float3>(0, Allocator.Persistent);
                normals = new NativeArray<float3>(0, Allocator.Persistent);
                tangents = new NativeArray<float4>(0, Allocator.Persistent);
                uv = new NativeArray<float2>(0, Allocator.Persistent);
                colors = new NativeArray<float4>(0, Allocator.Persistent);
                
                sourceTriangles = new NativeArray<ushort>(0, Allocator.Persistent);
                sourceSubmeshRanges = new NativeArray<int2>(0, Allocator.Persistent);
            }
            
            public void Dispose()
            {
                if (!IsCreated)
                {
                    //Debug.LogWarning("Mesh data was already disposed");
                    return;
                }
                
                positions.Dispose();
                normals.Dispose();
                tangents.Dispose();
                uv.Dispose();
                colors.Dispose();
                
                sourceTriangles.Dispose();
                sourceSubmeshRanges.Dispose();
            }
        }

        //[BurstCompile]
        public struct SplinePoint
        {
            public float3 position;
            public float3 tangent;
            public float3 up;
            
            public SplinePoint(float3 position, float3 tangent, float3 up)
            {
                this.position = position;
                this.tangent = tangent;
                this.up = up;
            }

            [BurstCompile]
            public static void Lerp(in SplinePoint a, in SplinePoint b, float t, out SplinePoint result)
            {
                result = new SplinePoint(
                    math.lerp(a.position, b.position, t),
                    math.lerp(a.tangent, b.tangent, t),
                    math.lerp(a.up, b.up, t)
                );
            }
        }
      
        public struct BoundsData
        {
            public float3 size;
            public float3 min;
            public float3 max;

            public BoundsData(float3 min, float3 max)
            {
                this.min = min;
                this.max = max;
                size = max - min;
            }
            
            public void SetMinMax(float3 min, float3 max)
            {
                this.min = min;
                this.max = max;
                size = max - min;
            }
            
            public float3 CalculatedCenter => (min + max) * 0.5f;
        }
        
        //Helper to pack 4 signed bytes
        public struct sbyte4
        {
            //Used for the SNorm8 format
            private const float NORMALIZATION_SCALAR = 127;
            
            public sbyte x, y, z, w;
            
            public sbyte4(float4 value)
            {
                this.x = (sbyte)(value.x * NORMALIZATION_SCALAR);
                this.y = (sbyte)(value.y * NORMALIZATION_SCALAR);
                this.z = (sbyte)(value.z * NORMALIZATION_SCALAR);
                this.w = (sbyte)(value.w * NORMALIZATION_SCALAR);
            }
            
            public sbyte4(float3 value)
            {
                this.x = (sbyte)(value.x * NORMALIZATION_SCALAR);
                this.y = (sbyte)(value.y * NORMALIZATION_SCALAR);
                this.z = (sbyte)(value.z * NORMALIZATION_SCALAR);
                this.w = 0;
            }

            public sbyte4(sbyte x, sbyte y, sbyte z, sbyte w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }
        }
        
        //Helper to pack 4 signed bytes
        public struct byte4
        {
            private const float NORMALIZATION_SCALAR = 255;
            
            public byte x, y, z, w;

            public byte4(float4 value)
            {
                this.x = (byte)(value.x * NORMALIZATION_SCALAR);
                this.y = (byte)(value.y * NORMALIZATION_SCALAR);
                this.z = (byte)(value.z * NORMALIZATION_SCALAR);
                this.w = (byte)(value.w * NORMALIZATION_SCALAR);
            }
            
            public byte4(byte x, byte y, byte z, byte w)
            {
                this.x = x;
                this.y = y;
                this.z = z;
                this.w = w;
            }
        }
        
        [Serializable]
        public struct CurveSegmentInfo
        {
            /// <summary>
            /// Range it occupies on the spline. X: Start, Y: End
            /// </summary>
            public float2 curveRange;
            public int index;
            /// <summary>
            /// Number of tiles repeated along this segment
            /// </summary>
            public int tileCount;
            /// <summary>
            /// Length of the tile mesh that's repeated, may be slightly scaled if the <see cref="CurveMeshSettings.Distribution.scaleToFit"/> option is enabled
            /// </summary>
            public float tileLength;
            /// <summary>
            /// <see cref="tileLength"/>, including any empty spacing
            /// </summary>
            public float tileOccupiedLength;
            
            /// <summary>
            /// Returns the total length of the curve segment
            /// </summary>
            /// <returns></returns>
            public float GetLength() => curveRange.y - curveRange.x;
        }
        
        public enum PointState
        {
            Inside,
            Edge,
            Outside
        }
        
        public enum Accuracy
        {
            BestPerformance,
            PreferPerformance,
            Balanced,
            PreferPrecision,
            HighestPrecision
        }
        
        public enum VertexColorChannel
        {
            Red,
            Green,
            Blue,
            Alpha
        }
        
        public enum Alignment
        {
            PivotPoint,
            TopLeft,
            TopCenter,
            TopRight,
            MiddleLeft,
            MiddleCenter,
            MiddleRight,
            BottomLeft,
            BottomCenter,
            BottomRight
        }

    }
}