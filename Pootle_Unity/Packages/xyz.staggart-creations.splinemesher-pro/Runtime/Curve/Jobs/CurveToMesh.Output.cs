using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using sbyte4 = sc.splinemesher.pro.runtime.Structs.sbyte4;
using byte4 = sc.splinemesher.pro.runtime.Structs.byte4;

namespace sc.splinemesher.pro.runtime
{
    public partial struct CurveToMesh
    {
        private const float MIN_AVERAGE_SCALE = 0.01f;
            
        //Output vertex layout. The order is important and what Unity excepts
        //16-bit precision is used for positions and UVs (max values of 65k)
        //8-bit precision is used for normals, tangents and vertex colors. As these values never exceed -1/+1
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
            public byte4 color;
            public half4 uv0;
            //UV1 is appended when lightmap UV is generated
            
            //Creates a new vertex in the packed format
            public Vertex(float3 inputPosition, float3 inputNormal, float4 inputTangent, float4 inputColor, float4 inputUv0)
            {
                #if SPLINE_MESHER_HALF_PRECISION
                position = new half4((half3)inputPosition.xyz, (half)0);
                #else
                position = new float4(inputPosition.xyz, 0);
                #endif
                normal = new sbyte4(inputNormal);
                tangent = new sbyte4(inputTangent);
                color = new byte4(inputColor);
                uv0 = (half4)inputUv0;
            }
        }
        
        //32-bit variant. Currently only used to convert meshes to this format for lightmap UV generation
        public struct Vertex32Bit
        {
            public float3 position;
            public float3 normal;
            public float4 tangent;
            public float4 color;
            public float4 uv;

            //Unpack back into 32-bit/float data
            public Vertex32Bit(Vertex vertex)
            {
                position = new float3(vertex.position.x, vertex.position.y, vertex.position.z);
                normal = new float3(vertex.normal.x, vertex.normal.y, vertex.normal.z) / 127f;
                tangent = new float4(vertex.tangent.x, vertex.tangent.y, vertex.tangent.z, vertex.tangent.w) / 127f;
                color = new float4(vertex.color.x, vertex.color.y, vertex.color.z, vertex.color.w);
                uv = new float4(vertex.uv0.x, vertex.uv0.y, vertex.uv0.z, vertex.uv0.w);
            }
        }
        
        //Note, order is important, as it is what Unity expects
        public static readonly VertexAttributeDescriptor[] VertexAttributes = new VertexAttributeDescriptor[]
        {
            #if SPLINE_MESHER_HALF_PRECISION
            new (VertexAttribute.Position, VertexAttributeFormat.Float16, 4),
            #else
            new (VertexAttribute.Position, VertexAttributeFormat.Float32, 4),
            #endif
            new (VertexAttribute.Normal, VertexAttributeFormat.SNorm8, 4),
            new (VertexAttribute.Tangent, VertexAttributeFormat.SNorm8, 4),
            new (VertexAttribute.Color, VertexAttributeFormat.UNorm8, 4),
            new (VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 4),
        };
        
        public static readonly VertexAttributeDescriptor[] VertexAttributes32Bit = new VertexAttributeDescriptor[]
        {
            new (VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new (VertexAttribute.Normal, VertexAttributeFormat.Float32, 3),
            new (VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4),
            new (VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
            new (VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 4),
        };
        
        public bool HasInvalidBounds()
        {
            return float.IsNaN(finalBoundsMin.x) || finalBoundsMin.x >= float.PositiveInfinity;
        }
        
        public Mesh CreateMesh(ref Mesh mesh, int index, bool readable, bool validateIndices = false)
        {
            /*
            //Old slower API
            mesh.indexFormat = vertexCount >= 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices, 0, vertexCount);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            mesh.SetNormals(normals, 0, vertexCount);
            mesh.SetTangents(tangents, 0, vertexCount);
            mesh.SetUVs(0, uv0);

            return mesh;
            */

            if (zeroScaleMesh || vertexCount < 3)
            {
                //Debug.LogError("Cannot create a mesh with zero scale.");
                
                Utilities.Destroy(mesh);
                mesh = new Mesh();
                return mesh;
            }

            if (Application.isPlaying) readable = true;
            
            #if UNITY_EDITOR
            //Destroy and recreate if readable state doesn't match
            if (mesh.isReadable != readable)
            {
                Utilities.Destroy(mesh);
                mesh = new Mesh();
                
                #if SM_DEV
                //Debug.Log($"SplineMesher: Recreated mesh at index {index}. Read state changed to {readable}");
                #endif
            }
            #endif
            
            //Additional triangles are created for LODs, which creates a mismatch when trying to update the original triangles
            //Clear the mesh, requiring LODs to be recreated.
#if UNITY_6000_2_OR_NEWER
            if (mesh.lodCount > 1)
            {
                mesh.Clear();
            }
#endif
            if (Application.isEditor == false)
            {
                bool supportsPackedNormals = SystemInfo.SupportsVertexAttributeFormat(VertexAttributeFormat.SNorm8, 4);
                bool supportsHalfPosition = SystemInfo.SupportsVertexAttributeFormat(VertexAttributeFormat.Float16, 4);

                if (supportsPackedNormals == false)
                {
                    Debug.LogError("Spline Curve Mesher: The current hardware/graphics API does not support the SNorm8 vertex attribute format. Generated mesh may not be visible.");
                }
                if (supportsHalfPosition == false)
                {
                    Debug.LogError("Spline Curve Mesher: The current hardware/graphics API does not support the Float16 vertex attribute format. Generated mesh may not be visible.");
                }
            }

            mesh.SetVertexBufferParams(vertexCount, VertexAttributes);

            mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, Utilities.MeshValidationFlags);
            
            //Triangles
            var triangleValidation = Utilities.MeshValidationFlags;
            
            #if UNITY_EDITOR
            //Gizmo mesh drawing requires validated indices
            if(validateIndices) triangleValidation &= MeshUpdateFlags.DontValidateIndices;
            #endif
            
            //Set proper index format based on number of vertices added.
            Utilities.SetMeshIndices(mesh, indices, vertexCount, triangleValidation);
            
            //Debug.Log($"Creating mesh segment #{index}: Vertices:{vertexCount}. Tris:{triangleCount}. Submeshes:{submeshCount}. Format:{mesh.indexFormat}: Tiles:{tileCount}");
            
            //mesh.subMeshCount = submeshCount;

            SubMeshDescriptor[] descriptors = new SubMeshDescriptor[submeshCount];
            int currentIndexOffset = 0;
            for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
            {
                //Start and end indices for this submesh
                //int start = sourceSubmeshRanges[submeshIndex].x;
                int count = sourceSubmeshRanges[submeshIndex].y;
                
                int sourceIndexCount = count;
                int totalIndexCount = sourceIndexCount * tileCount;
        
                SubMeshDescriptor subMeshDescriptor = new SubMeshDescriptor
                {
                    indexStart = currentIndexOffset,
                    indexCount = totalIndexCount,
                    topology = MeshTopology.Triangles,
                    firstVertex = 0,
                    baseVertex = 0,
                    vertexCount = vertexCount
                };
                
                //if(submeshCount > 1) Debug.Log($"[SplineToMesh] Submesh #{submeshIndex}. Start:{currentIndexOffset}. End:{totalIndexCount}. Vertices:{subMeshDescriptor.vertexCount}. Max triangle index: {indices[^1]}.");
                
                descriptors[submeshIndex] = subMeshDescriptor;
                
                currentIndexOffset += totalIndexCount;
            }
            mesh.SetSubMeshes(descriptors, Utilities.MeshValidationFlags);
            
            Bounds bounds = mesh.bounds;
            bounds.SetMinMax(finalBoundsMin, finalBoundsMax);
            mesh.bounds = bounds;
            
            mesh.UploadMeshData(!readable);
            
            return mesh;
        }

        public Mesh CreateCollider(ref Mesh mesh, string name = "SplineMesh")
        {
            if (zeroScaleCollider || colliderVertexCount < 3)
            {
                //Debug.LogError("Cannot create a mesh with zero scale.");
                
                Utilities.Destroy(mesh);
                return mesh;
            }
            
            mesh.SetVertexBufferParams(colliderVertexCount, VertexAttributes);
            
            mesh.SetVertexBufferData(colliderVertices, 0, 0, colliderVertexCount, 0, Utilities.MeshValidationFlags);
            
            var triangleValidation = Utilities.MeshValidationFlags;
            //Collider creation requires the "notify mesh users" flag enabled. Assumingly for collider cooking.
            triangleValidation &= ~MeshUpdateFlags.DontNotifyMeshUsers;
            
            Utilities.SetMeshIndices(mesh, colliderIndices, colliderVertexCount, triangleValidation);
            
            int indexCount = colliderIndices.Length;
            
            //Debug.Log($"Creating spline mesh: Vertices:{vertexCount}. Tris:{triangleCount}. Submeshes:{submeshCount}. Format:{mesh.indexFormat}");
            
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexCount), Utilities.MeshValidationFlags);

            Bounds bounds = mesh.bounds;
            bounds.SetMinMax(finalBoundsMin, finalBoundsMax);
            mesh.bounds = bounds;

            //Collider needs to be kept readable, so a MeshCollider can post-process in a build
            mesh.UploadMeshData(false);

            return mesh;
        }
    }
}