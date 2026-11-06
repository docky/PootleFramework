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

using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace sc.splinemesher.pro.runtime
{
    public static class Utilities
    {
        //Clamp spline sampling so that a tangent can always be derived at the start/end of the curve.
        public const float MIN_T_VALUE = 0.00005f;
        public const float MAX_T_VALUE = 0.99995f;
        
        //Ensures that t-values always wrap around for closed curves.
        //A value of 1.01 becomes 0.01. And -0.01 becomes 0.99.
        public static float WrapT(float t)
        {
            //Wrap using modulo
            return t - math.floor(t);
        }
        
        //Default flags to use, where all validation is disabled. Best performance, but requires data to be absolutely correct.
        public const MeshUpdateFlags MeshValidationFlags = MeshUpdateFlags.DontNotifyMeshUsers | MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds;
        
        public static quaternion LockRotationAngle(quaternion neutralRotation, quaternion targetRotation, bool3 angles)
        {
            math.RotationOrder rotationOrder = math.RotationOrder.ZXY;
            
            float3 prevEuler = math.Euler(neutralRotation, rotationOrder);
            float3 newEuler = math.Euler(targetRotation, rotationOrder);
                
            //Note: Angles are in radians
            
            if (angles.x)
            {
                newEuler.x = prevEuler.x;
            }
            if (angles.y)
            {
                newEuler.y = prevEuler.y;
            }
            if (angles.z)
            {
                newEuler.z = prevEuler.z;
            }
                
            quaternion newRotation = quaternion.Euler(newEuler, rotationOrder);

            return newRotation;
        }
        
        [BurstCompile]
        public static float EaseInOut(float t)
        {
            float eased = 2f * t * t;
            if (t > 0.5f) eased = 4f * t - eased - 1f;
                            
            return eased;
        }
        
        [BurstCompile]
        public static float CalculateDistanceWeight(float position, float surfaceLength, float startDistance, float startFalloff, float endDistance, float endFalloff, bool invert, bool easeInOut = false)
        {
            float start = math.saturate(((startDistance) - (position - (startDistance + startFalloff))) / (math.max(startFalloff, 0.00001f)));
            float end = math.saturate(((surfaceLength - endDistance) - (position + endDistance)) / (math.max(endFalloff, 0.00001f)));

            //Patch when falloff is 0
            if(endFalloff == 0f && (position - surfaceLength) <= 0f) end = 1f;
            
            float gradient = math.max(start, 1f- end);

            if (easeInOut)
            {
                gradient = EaseInOut(gradient);
            }
            
            if(invert) gradient = 1f-gradient;
            
            return gradient;
        }
        
        [BurstCompile]
        public static float EdgeDistanceMask(float position, float maxWidth, float distance, float falloff, bool invert = false)
        {
            falloff = math.max(falloff, 0.00001f);
            
            float start = math.saturate(((distance + falloff) - (position - distance)) / falloff);
            float end = math.saturate(((maxWidth - distance) - (position + distance)) / falloff);

            float gradient = math.max(start, 1f- end);
            
            if(invert) gradient = 1f-gradient;
            
            return gradient;
        }
        
        /// <summary>
        /// Returns a material that is compatible with the current render pipeline. Only in the editor will it have a nice checker texture assigned.
        /// </summary>
        /// <returns></returns>
        public static Material CreateDefaultMaterial()
        {
            RenderPipelineAsset pipelineAsset = GraphicsSettings.defaultRenderPipeline ?? QualitySettings.renderPipeline;
            var usingSRP = pipelineAsset;
            
            //Material compatible with current render pipeline
            Material template = usingSRP ? pipelineAsset.defaultMaterial : new Material(Shader.Find("Standard"));
            Material material = new Material(template.shader)
            {
                color = Color.white, //Built-in RP
                hideFlags = HideFlags.NotEditable, //Encourage user to use their own material
                name = "Spline Mesh Prototype"
            };
            
            if(usingSRP)
            {
                material.SetColor("_BaseColor", Color.white); //SRP
                material.SetFloat("_Cull", 0); //SRP
            }
            
            #if UNITY_EDITOR
            string albedoPath = UnityEditor.AssetDatabase.GUIDToAssetPath("16d55b237fb84ff4aafa64d31e0e24d5");

            if (albedoPath != string.Empty)
            {
                Texture2D albedo = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);

                string propertyName = "_MainTex";
                
                //URP
                if (template.shader.name.Contains("Universal"))
                {
                    propertyName = "_BaseMap";
                    material.SetFloat("_SmoothnessTextureChannel", 1); //Embedded in albedo alpha channel
                    material.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
                    material.SetFloat("_Smoothness", 1);
                }
                //HDRP
                if(template.shader.name.Contains("HDRP")) propertyName = "_BaseColorMap";

                material.SetTexture(propertyName, albedo);
            }
            
            string normalPath = UnityEditor.AssetDatabase.GUIDToAssetPath("b1e8b000f48129a4084fd7a606f1c7f2");
            if (normalPath != string.Empty)
            {
                Texture2D normals = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);

                string propertyName = "_BumpMap";
                string keywordName = "_NORMALMAP";

                if (usingSRP)
                {
                    //URP
                    if (template.shader.name.Contains("Universal"))
                    {
                        propertyName = "_BumpMap";
                        keywordName = "_NORMALMAP";
                    }
                    //HDRP
                    if (template.shader.name.Contains("HDRP"))
                    {
                        propertyName = "_NormalMap";
                        keywordName = "_NORMALMAP";
                    }
                }
                else
                {
                    propertyName = "_BumpMap";
                    keywordName = "_NORMALMAP";
                }

                material.EnableKeyword(keywordName);
                material.SetTexture(propertyName, normals);
            }
            #endif

            return material;
        }

        public static void DrawBoundsGizmo(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;

            Vector3[] corners =
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y,  extents.z),
                center + new Vector3(-extents.x,  extents.y, -extents.z),
                center + new Vector3(-extents.x,  extents.y,  extents.z),
                center + new Vector3( extents.x, -extents.y, -extents.z),
                center + new Vector3( extents.x, -extents.y,  extents.z),
                center + new Vector3( extents.x,  extents.y, -extents.z),
                center + new Vector3( extents.x,  extents.y,  extents.z),
            };

            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 corner = corners[i];
                Vector3 sign = new Vector3(
                    Mathf.Sign(corner.x - center.x),
                    Mathf.Sign(corner.y - center.y),
                    Mathf.Sign(corner.z - center.z));

                Vector3 x = corner + new Vector3(-sign.x * extents.x * 0.25f, 0f, 0f);
                Vector3 y = corner + new Vector3(0f, -sign.y * extents.y * 0.25f, 0f);
                Vector3 z = corner + new Vector3(0f, 0f, -sign.z * extents.z * 0.25f);

                Gizmos.DrawLine(corner, x);
                Gizmos.DrawLine(corner, y);
                Gizmos.DrawLine(corner, z);
            }
        }
        
        //In megabytes
        public static float GetMemorySize(Mesh mesh)
        {
            float size = 0;
            
            if (mesh)
            {
                int vertexCount = mesh.vertexCount;
                
                //Get actual byte size per vertex
                for (int stream = 0; stream < mesh.vertexBufferCount; stream++)
                {
                    int stride = mesh.GetVertexBufferStride(stream);
                    size += vertexCount * stride;
                }
                
                //Index data
                long indexCount = 0;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    indexCount += mesh.GetIndexCount(submesh);

                bool use32Bit = mesh.indexFormat == IndexFormat.UInt32;
                size += indexCount * (use32Bit ? sizeof(uint) : sizeof(ushort));
                
                return size / (1024f * 1024f);
            }

            return size;
        }
        
        public static string FormatMemorySize(float size)
        {
            //Convert to bytes
            float sizeInBytes = size * 1024 * 1024;

            if (sizeInBytes < 1024f)
            {
                return sizeInBytes.ToString("F0") + " bytes";
            }
            else if (sizeInBytes < 1024f * 1024f)
            {
                float sizeInKB = sizeInBytes / 1024f;
                return sizeInKB.ToString("F2") + " KB";
            }
            else
            {
                return size.ToString("F2") + " MB";
            }
        }

        //Set proper index format based on number of vertices added.
        //Meshes with a higher vertex count that this will need to suffer a performance hit, as the conversion to an int format is needed.
        public static void SetMeshIndices(Mesh mesh, NativeArray<ushort> indices, int vertexCount, MeshUpdateFlags updateFlags = MeshValidationFlags)
        {
            //TODO investigate if using direct pointers avoids the overhead of accessing native array items in managed code
            
            int indexCount = indices.Length;
            if (vertexCount > ushort.MaxValue)
            {
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);

                NativeArray<int> indices32 = new NativeArray<int>(indexCount, Allocator.Temp);
                for (int i = 0; i < indexCount; i++)
                {
                    indices32[i] = (int)indices[i];
                }
                mesh.SetIndexBufferData(indices32, 0, 0, indexCount, updateFlags);
                indices32.Dispose();
            }
            else
            {
                mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt16);
                mesh.SetIndexBufferData(indices, 0, 0, indexCount, updateFlags);
            }
        }

        public static void Destroy(Object obj)
        {
            if (obj != null)
            {
#if UNITY_EDITOR
                if (Application.isPlaying && !UnityEditor.EditorApplication.isPaused)
                    Object.Destroy(obj);
                else
                    Object.DestroyImmediate(obj);
#else
                Object.Destroy(obj);
#endif
            }
        }
    }
}