using System;
using System.Collections.Generic;
using sc.splinemesher.pro.runtime;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace sc.splinemesher.pro.editor
{
    public static partial class SplineMesherEditor
    {
        private static void OnBakeryStart(object sender, EventArgs e)
        {
            OnLightBakeStart();
        }
        
        private static void OnLightBakeStart()
        {
            if (SplineMesherSettings.GenerateLightmapUV == SplineMesherSettings.BakingMode.Automatic)
            {
                #if SM_DEV
                Debug.Log("[Spline Mesher] Lightbake started. Automatically generating lightmap UV's if needed");
                #endif
                
                GenerateLightmapUVs();
            }
        }
        
        /// <summary>
        /// Generates lightmap UV's for any spline mesh that requires it
        /// </summary>
        public static void GenerateLightmapUVs()
        {
            #if UNITY_6000_4_OR_NEWER
            SplineCurveMesher[] splineMeshers = Object.FindObjectsByType<SplineCurveMesher>();
            #else
            SplineCurveMesher[] splineMeshers = Object.FindObjectsByType<SplineCurveMesher>(FindObjectsSortMode.None);
            #endif
            
            int count = 0;
            System.Diagnostics.Stopwatch lightmapUVUnwrapTimer = new System.Diagnostics.Stopwatch();
            
            lightmapUVUnwrapTimer.Start();
            //Find the spline meshes that still require lightmap UV's
            foreach (SplineCurveMesher splineMesher in splineMeshers)
            {
                if (GenerateLightmapUV(splineMesher))
                {
                    count++;
                }
            }
            lightmapUVUnwrapTimer.Stop();
            
            if (count > 0)
            {
                Debug.Log($"[Spline Mesher] Lightmap UV created for {count} spline meshes (Duration: {lightmapUVUnwrapTimer.ElapsedMilliseconds}ms)");
            }

            //Meshes are also 8-16 bit format, which the lightmapper can't accept. Convert them to 32-bit
            ConvertFillMeshersTo32Bit();
        }

        public static void ConvertFillMeshersTo32Bit()
        {
#if UNITY_6000_4_OR_NEWER
            SplineFillMesher[] fillMeshers = Object.FindObjectsByType<SplineFillMesher>();
#else
            SplineFillMesher[] fillMeshers = Object.FindObjectsByType<SplineFillMesher>(FindObjectsSortMode.None);
#endif

            foreach (SplineFillMesher fillMesher in fillMeshers)
            {
                ConvertFillMesherTo32Bit(fillMesher);
            }
        }

        public static bool GenerateLightmapUV(SplineCurveMesher splineMesher)
        {
            if(splineMesher.settings.collision.colliderOnly) return false;
            
            bool generated = false;
            for (int i = 0; i < splineMesher.Containers.Count; i++)
            {
                SplineMeshContainer container = splineMesher.Containers[i];

                for (int j = 0; j < container.SegmentCount; j++)
                {
                    SplineMeshSegment segment = container.Segments[j];

                    if (RequiresLightmapUV(segment))
                    {
                        #if SM_DEV
                        //Debug.Log($"{splineMesher.name} segment #{j} requires new lightmap UV's");
                        #endif

                        GenerateLightmapUV(segment, splineMesher.settings.output.lightmapUVAngleThreshold, splineMesher.settings.output.lightmapUVMarginMultiplier);

                        generated = true;
                    }
                }
            }

            return generated;
        }

        public static bool RequiresLightmapUV(SplineCurveMesher splineMesher)
        {
            if(splineMesher.settings.collision.colliderOnly) return false;
            
            for (int i = 0; i < splineMesher.Containers.Count; i++)
            {
                SplineMeshContainer container = splineMesher.Containers[i];

                for (int j = 0; j < container.SegmentCount; j++)
                {
                    SplineMeshSegment segment = container.Segments[j];

                    if (RequiresLightmapUV(segment))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
        
        private static bool RequiresLightmapUV(SplineMeshSegment segment)
        {
            Mesh mesh = segment.mesh;

            if (mesh == null) return false;
            
            StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(segment.gameObject);

            if (staticFlags.HasFlag(StaticEditorFlags.ContributeGI) == false) return false;
            
            if (mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
            {
                return false;
            }

            /*
            //Conditions that indicate the mesh has no lightmap UV's
            //Note that rebuilding the spline mesh clears the UV2 channel, automatically marking it as 'dirty' again.
            if (mesh.uv2 == null || mesh.uv2.Length == 0)
            {
                return true;
            }

            */
            
            return true;
        }
        
        public static void GenerateLightmapUV(SplineMeshSegment segment, float angleThreshold, float marginMultiplier)
        {
            MeshFilter mf = segment.GetComponent<MeshFilter>();

            if (!mf) return;
            
            Mesh originalMesh = mf.sharedMesh;
            if(originalMesh == null) return;
            
            //Unity's lightmap UV generation does not accept meshes with 8-16bit vertex data, causing the editor to crash
            //A workaround is to create a copy with vertex data converted back to 32bit
            Mesh mesh32Bit = Create32BitCopy<CurveToMesh.Vertex, CurveToMesh.Vertex32Bit>(
                originalMesh,
                CurveToMesh.VertexAttributes32Bit,
                vertex => new CurveToMesh.Vertex32Bit(vertex));;

            UnwrapParam.SetDefaults(out var unwrapSettings);
            unwrapSettings.hardAngle = angleThreshold;
            unwrapSettings.packMargin *= Mathf.Max(0.01f, marginMultiplier);

            //Creates a lightmap UV, through largely unknown methods
            //Will split vertices along UV seams, resulting in a higher vertex count
            if (Unwrapping.GenerateSecondaryUVSet(mesh32Bit, unwrapSettings) == false)
            {
                throw new Exception($"Lightmap UV generation failed for {originalMesh.name}");
            }
            
            mf.sharedMesh = mesh32Bit;
        }

        public static bool Requires32BitConversion(Mesh mesh)
        {
            return mesh.GetVertexAttributeFormat(VertexAttribute.Normal) != VertexAttributeFormat.Float32;
        }

        [MenuItem("CONTEXT/SplineFillMesher/Convert to 32-bit")]
        private static void ConvertFillMesherTo32BitContext(MenuCommand command)
        {
            SplineFillMesher fillMesher = (SplineFillMesher)command.context;
            
            ConvertFillMesherTo32Bit(fillMesher);
        }
        
        public static void ConvertFillMesherTo32Bit(SplineFillMesher fillMesher)
        {
            foreach (var container in fillMesher.Containers)
            {
                if(!container) continue;
                
                foreach (var segment in container.Segments)
                {
                    if(!segment) continue;
                    
                    if(segment.mesh && Requires32BitConversion(segment.mesh))
                    {
                        //Debug.Log($"[Spline Mesher] Converting {segment.mesh.name} to 32-bit");
                        
                        segment.mesh = Create32BitCopy<SplineFillMesher.Vertex, SplineFillMesher.Vertex32Bit>(
                            segment.mesh,
                            SplineFillMesher.VertexAttributes32Bit,
                            vertex => new SplineFillMesher.Vertex32Bit(vertex));
                    }
                }
            }
        }
        
        private static Mesh Create32BitCopy<TSourceVertex, TTargetVertex>(
                Mesh originalMesh,
                VertexAttributeDescriptor[] vertexAttributes32Bit,
                Func<TSourceVertex, TTargetVertex> convertVertex)
                where TSourceVertex : struct
                where TTargetVertex : struct
        {
            Mesh.MeshDataArray meshDataArray = Mesh.AcquireReadOnlyMeshData(originalMesh);
            Mesh.MeshData sourceData = meshDataArray[0];

            NativeArray<TSourceVertex> sourceVertices = sourceData.GetVertexData<TSourceVertex>();
            int vertexCount = sourceData.vertexCount;
            var sourceIndexData = sourceData.GetIndexData<ushort>();
            int indexCount = sourceIndexData.Length;
        
            NativeArray<TTargetVertex> vertices = new NativeArray<TTargetVertex>(vertexCount, Allocator.Temp);
            for (int i = 0; i < vertexCount; i++)
            {
                vertices[i] = convertVertex(sourceVertices[i]);
            }
        
            MeshUpdateFlags noValidation = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds;
        
            Mesh newMesh = new Mesh();
            newMesh.name = originalMesh.name;
        
            newMesh.SetVertexBufferParams(vertexCount, vertexAttributes32Bit);

            newMesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, noValidation);
            sourceVertices.Dispose();
            vertices.Dispose();
        
            newMesh.SetIndexBufferParams(indexCount, originalMesh.indexFormat);
            newMesh.SetIndexBufferData(sourceIndexData, 0, 0, indexCount, noValidation);
            sourceIndexData.Dispose();
        
            newMesh.subMeshCount = sourceData.subMeshCount;
            for (int i = 0; i < newMesh.subMeshCount; i++)
            {
                newMesh.SetSubMesh(i, sourceData.GetSubMesh(i), noValidation);
            }
        
            newMesh.bounds = originalMesh.bounds;
        
            Utilities.Destroy(originalMesh);
        
            meshDataArray.Dispose();

            return newMesh;
        }
    }
}