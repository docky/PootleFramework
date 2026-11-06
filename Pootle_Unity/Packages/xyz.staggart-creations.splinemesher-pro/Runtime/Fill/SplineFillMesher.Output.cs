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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using PointState = sc.splinemesher.pro.runtime.Structs.PointState;
using sbyte4 = sc.splinemesher.pro.runtime.Structs.sbyte4;

namespace sc.splinemesher.pro.runtime
{
    public partial class SplineFillMesher
    {
        /// <summary>
        /// Format of the mesh indices. 32-bit is used to allow for relatively large meshes.
        /// </summary>
        public const IndexFormat indexFormat = IndexFormat.UInt32;
        
        public struct Vertex
        {
#if SPLINE_MESHER_HALF_PRECISION
            //Note: Using half/16bit precision for the position limits the size of a mesh to about 100 units
            public half4 position;
#else
            //To avoid a visible "wobble" in the vertices, 32-bit precision is used. Primarily for a smoother UX
            public float4 position;
#endif
            public sbyte4 normal;
            public sbyte4 tangent;
            public half4 color;
            public float4 uv0;
            public Structs.byte4 uv1;
            
            //Creates a new vertex in the packed format
            public Vertex(float3 inputPosition, float3 inputNormal, float4 inputTangent, float4 inputColor, float4 inputUv0, float2 inputUv1)
            {
                #if SM_DEV
                if (float.IsNaN(inputPosition.x) || float.IsNaN(inputPosition.y)|| float.IsNaN(inputPosition.z))
                {
                    Debug.LogWarning("Vertex position is NaN: " + inputPosition);
                    inputPosition = new float3(0, 0, 0);
                }
                #endif
#if SPLINE_MESHER_HALF_PRECISION
                position = new half4((half3)inputPosition.xyz, (half)0);
#else
                position = new float4(inputPosition.xyz, 0);
#endif
                normal = new sbyte4((sbyte)(inputNormal.x * 127), (sbyte)(inputNormal.y * 127), (sbyte)(inputNormal.z * 127), 0);
                tangent = new sbyte4((sbyte)(inputTangent.x * 127), (sbyte)(inputTangent.y * 127), (sbyte)(inputTangent.z * 127), (sbyte)(inputTangent.w * 127));
                color = new half4(inputColor);
                uv0 = inputUv0;
                //Lightmap UV, always normalized, so byte format is used
                uv1 = new Structs.byte4((byte)(inputUv1.x * 255), (byte)(inputUv1.y * 255), 0, 0);
                //uv1 = new half4((half)inputUv1.x, (half)inputUv1.y , (half)0, (half)0);
            }
        }
        
        //32-bit variant. Currently only used to convert meshes to this format for lightmap UV generation
        public struct Vertex32Bit
        {
            public float3 position;
            public float3 normal;
            public float4 tangent;
            public float4 color;
            public float4 uv0;
            public float4 uv1;

            //Unpack back into 32-bit/float data
            public Vertex32Bit(Vertex vertex)
            {
                position = new float3(vertex.position.x, vertex.position.y, vertex.position.z);
                normal = new float3(vertex.normal.x, vertex.normal.y, vertex.normal.z) / 127f;
                tangent = new float4(vertex.tangent.x, vertex.tangent.y, vertex.tangent.z, vertex.tangent.w) / 127f;
                color = new float4(vertex.color.x, vertex.color.y, vertex.color.z, vertex.color.w);
                uv0 = new float4(vertex.uv0.x, vertex.uv0.y, vertex.uv0.z, vertex.uv0.w);
                uv1 = new float4(vertex.uv1.x, vertex.uv1.y, vertex.uv1.z, vertex.uv1.w) / 127f;
            }
        }
        
        public readonly VertexAttributeDescriptor[] VertexAttributes = new[]
        {
#if SPLINE_MESHER_HALF_PRECISION
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
#else
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 4),
#endif
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.SNorm8, 4),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float16, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1, VertexAttributeFormat.UNorm8, 4),
        };
        
        public static readonly VertexAttributeDescriptor[] VertexAttributes32Bit = new VertexAttributeDescriptor[]
        {
            new (VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new (VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new (VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new (VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
            new (VertexAttribute.TexCoord1, VertexAttributeFormat.Float32, 4),
        };
        
        private Mesh CreateMesh(NativeArray<Vertex> vertices, Bounds bounds, bool readable, ref Mesh mesh)
        {
            if (Application.isPlaying) readable = true;
            
            //Additional triangles are created for LODs, which creates a mismatch when trying to update the original triangles
            //Clear the mesh, requiring LODs to be recreated.
            #if UNITY_6000_2_OR_NEWER
            if (mesh.lodCount > 1)
            {
                mesh.Clear();
            }
            #endif
            
            #if UNITY_EDITOR
            //Destroy and recreate if readable state doesn't match
            if (mesh.isReadable != readable)
            {
                Utilities.Destroy(mesh);
                mesh = new Mesh();
            }
            #endif
            
            int vertexCount = vertices.Length;
            mesh.SetVertexBufferParams(vertexCount, VertexAttributes);
            
            mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, Utilities.MeshValidationFlags);
            
            var triangleValidation = Utilities.MeshValidationFlags;
            
            #if UNITY_EDITOR
            //Gizmo mesh drawing requires validated indices
            if(drawWireFrame) triangleValidation &= MeshUpdateFlags.DontValidateIndices;
            #endif
            
            int indexCount = triangles.Length;
            
            mesh.SetIndexBufferParams(indexCount, indexFormat);
            mesh.SetIndexBufferData(triangles.AsArray(), 0, 0, indexCount, triangleValidation);
            
            mesh.subMeshCount = 1;
            SubMeshDescriptor[] descriptors = new SubMeshDescriptor[1];
            descriptors[0] = new SubMeshDescriptor(0, indexCount);
            mesh.SetSubMeshes(descriptors, Utilities.MeshValidationFlags);
            
            mesh.bounds = bounds;
            
            //Only works once
            mesh.UploadMeshData(!readable);
            
            return mesh;
        }
    }
}