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
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if !UNITY_6000_0_OR_NEWER
using PhysicsMaterial = UnityEngine.PhysicMaterial;
#endif

namespace sc.splinemesher.pro.runtime
{
    [AddComponentMenu("")] //Hide
    [DisallowMultipleComponent]
    public class SplineMeshSegment : MonoBehaviour
    {
        [HideInInspector] [SerializeField]
        internal SplineMeshContainer container;
        /// <summary>
        /// The container this segment belongs to, which in turn belongs to a Spline Mesher <see cref="SplineMeshContainer.Owner"/>
        /// </summary>
        public SplineMeshContainer Container
        {
            get
            {
                if (!container && Application.isPlaying)
                {
                    throw new Exception("Unable to get container. This segment does not belong to a one. It has been orphaned. To prevent this, ensure that the component isn't moved or detached");
                }
                
                return container;
            }
        }
        
        [SerializeField] [HideInInspector]
        private MeshFilter meshFilter;
        [SerializeField] [HideInInspector]
        private MeshRenderer meshRenderer;
        [SerializeField] [HideInInspector]
        private MeshCollider meshCollider;

        [SerializeField] [HideInInspector]
        internal Structs.CurveSegmentInfo curveInfo;
        /// <summary>
        /// If the segment belongs to a Curve Mesher, this contains information about the section of the spline it represents.
        /// </summary>
        public Structs.CurveSegmentInfo CurveInfo => curveInfo;
        
        /// <summary>
        /// Shorthand for <see cref="Container.Owner.SplineContainer"/>
        /// </summary>
        /// <returns></returns>
        public SplineContainer GetSplineContainer()
        {
            return Container.Owner.SplineContainer;
        }

        /// <summary>
        /// Shorthand for accessing the spline this segment belongs to.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public Spline GetSpline()
        {
            SplineContainer splineContainer = GetSplineContainer();
            int splineIndex = Container.SplineIndex;

            if (splineIndex < 0 || splineIndex >= splineContainer.Splines.Count)
            {
                throw new Exception($"Spline index {splineIndex} is out of bounds for spline container {splineContainer.name}");
            }
            
            return splineContainer.Splines[splineIndex];
        }
        
        internal static SplineMeshSegment Create(SplineMeshContainer container, int segmentIndex)
        {
            GameObject go = new GameObject($"Spline Mesh #{segmentIndex}");
            go.transform.SetParent(container.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.hideFlags = HideFlags.NotEditable;
            
            SplineMeshSegment segment = go.AddComponent<SplineMeshSegment>();
            segment.container = container;
            segment.meshFilter = go.AddComponent<MeshFilter>();
            segment.meshFilter.mesh = new Mesh();
            segment.meshRenderer = go.AddComponent<MeshRenderer>();
            
            segment.StoreID();

            return segment;
        }

        //Save a unique ID to verify against
        //Avoids having to recreate a new mesh objects every rebuild operation
        //Whilst ensuring that does happen when duplicating a mesh object
        [SerializeField] [HideInInspector]
        [UnityEngine.Serialization.FormerlySerializedAs("meshID")] //Not needed, but gracefully retains the old value
#if UNITY_6000_3_OR_NEWER
        private EntityId segmentID;
#else
        private int segmentID;
#endif

        [ContextMenu("Force Unique Meshes")]
        private void ForceUniqueMesh()
        {
#if UNITY_6000_3_OR_NEWER
            segmentID = new EntityId();
#else
            segmentID = 0;
#endif

            EnsureUniqueMeshes();
        }

        private void StoreID()
        {
#if UNITY_6000_3_OR_NEWER
            segmentID = this.GetEntityId();
#else
            segmentID = this.GetInstanceID();
#endif
        }
        
        //Render and collision meshes are passed along by reference
        //When duplicating a Spline Mesher its created meshes will be linked to the original
        //This function safeguards against this and makes new - unique - copies of the meshes for this instance.
        internal void EnsureUniqueMeshes()
        {
#if UNITY_6000_3_OR_NEWER
            EntityId currentSegmentID = this.GetEntityId();
#else
            int currentSegmentID = this.GetInstanceID();
#endif
            
            //Note: Instance/Entity ID is not persistent between sessions and domain reloads
            //But this is acceptable as it merely incurs the duplication process once
            var changed = currentSegmentID != segmentID;
            //if(changed) Debug.Log($"Mesh segment {gameObject.name} does not belong to original owner ({currentSegmentID}!={segmentID}). Duplicating mesh objects.", container.Owner.gameObject);
            
            if (changed == false) return;
            
            segmentID = currentSegmentID;
            
            if (meshFilter.sharedMesh)
            {
                //Duplicate
                var newMesh = Instantiate(mesh);
#if UNITY_EDITOR
                newMesh.name = meshFilter.sharedMesh.name;
#endif
                //Note: Don't destroy the original mesh as it MAY still be in use a Spline Mesher this instance was duplicated/instantiated from!
                //Editor behavior: Unused meshes are automatically destroyed when the domain is reloaded
                //Runtime behavior: Unused meshes need to be cleared eventually using `Resources.UnloadUnusedAssets()`
                
                meshFilter.mesh = newMesh;
            }

            if (meshCollider && meshCollider.sharedMesh)
            {
                var newCollider = Instantiate(collisionMesh);
#if UNITY_EDITOR
                newCollider.name = meshCollider.sharedMesh.name;
#endif
 
                meshCollider.sharedMesh = newCollider;
            }
        }

        #region Render Mesh
        /// <summary>
        /// The generated mesh
        /// </summary>
        public Mesh mesh
        {
            get
            {
                #if DEBUG
                if (!meshFilter)
                {
                    Debug.LogError("Mesh filter not found on " + gameObject.name, this);
                }
                #endif
                if(!meshFilter.sharedMesh) 
                {
                    meshFilter.sharedMesh = new Mesh();
                    
                    StoreID();
                }
                return meshFilter.sharedMesh;
            }
            set => meshFilter.sharedMesh = value;
        }
        
        /// <summary>
        /// Materials assigned to the mesh renderer
        /// </summary>
        public Material[] materials => meshRenderer.sharedMaterials;
        
        public void SetMaterials(Material[] materials)
        {
            meshRenderer.sharedMaterials = materials;
        }
        
        public void SetMaterial(Material material)
        {
            meshRenderer.sharedMaterial = material;
        }
        
        public void SetRendererParameters(ShadowCastingMode shadowCastingMode, LightProbeUsage lightProbeUsage, ReflectionProbeUsage reflectionProbeUsage, uint renderingLayerMask, int forceMeshLod, float lodSelectionBias)
        {
            meshRenderer.shadowCastingMode = shadowCastingMode;
            meshRenderer.lightProbeUsage = lightProbeUsage;
            meshRenderer.reflectionProbeUsage = reflectionProbeUsage;
            meshRenderer.renderingLayerMask = renderingLayerMask;
#if UNITY_6000_2_OR_NEWER
            meshRenderer.forceMeshLod = (short)forceMeshLod;
            meshRenderer.meshLodSelectionBias = lodSelectionBias;
#endif
        }

        public void SetRendererState(bool state)
        {
            meshRenderer.enabled = state;
        }
        
        public (int, int) GetMeshStats()
        {
            if (mesh)
            {
                int triCount = 0;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    triCount += (int)mesh.GetIndexCount(i) / 3;
                }
                return (mesh.vertexCount, triCount);
            }

            return (0, 0);
        }
        
        //In megabytes
        public float GetMemorySize()
        {
            return mesh ? Utilities.GetMemorySize(mesh) : 0;
        }
        #endregion
        
        #region Collision Mesh
        /// <summary>
        /// The collision mesh assigned to the mesh collider
        /// </summary>
        public Mesh collisionMesh
        {
            get
            {
                #if DEBUG
                if (!meshCollider)
                {
                    Debug.LogError(
                        "[Accessing collision mesh failed] Mesh Collider not found on mesh segment belonging to " +
                        Container.Owner.name, this);
                    SetMeshCollider(true);
                }
                #endif
                
                if (!meshCollider.sharedMesh)
                {
                    return new Mesh();
                }
            
                return meshCollider.sharedMesh;
            }
            set
            {
                #if DEBUG
                if (!meshCollider)
                {
                    Debug.LogError(
                        "[Setting collision mesh failed] Mesh Collider not found on mesh segment belonging to " +
                        Container.Owner.name, this);
                    SetMeshCollider(true);
                }
                #endif
                
                meshCollider.sharedMesh = value;
            }
        }
        
        /// <summary>
        /// Adds or removes a Mesh Collider to the segment.
        /// </summary>
        /// <param name="state"></param>
        public void SetMeshCollider(bool state)
        {
            if (state && !meshCollider)
            {
                #if UNITY_EDITOR
                meshCollider = Undo.AddComponent<MeshCollider>(this.gameObject);
                #else
                meshCollider = this.gameObject.AddComponent<MeshCollider>();
                #endif
                
                meshCollider.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.UseFastMidphase | MeshColliderCookingOptions.EnableMeshCleaning;
                
                //Ensure a unique mesh is created for the collision mesh
                meshCollider.sharedMesh = ProceduralMesh.Cube.Create(1f,1f,1f, 1f, false, false, float2.zero, float3.zero, Structs.Alignment.PivotPoint);
            }
            if (!state && meshCollider)
            {
                if (Application.isPlaying)
                    Destroy(meshCollider);
                else
                    DestroyImmediate(meshCollider);
            }
        }
        
        //TODO: Temporarily switch the object's layer to "Ignore Raycast". As enabling a MeshCollider triggers its cooking process
        public void SetColliderEnabled(bool state)
        {
            if (meshCollider) meshCollider.enabled = state;
        }

        public void SetColliderSettings(int layer, LayerMask includeLayers, LayerMask excludeLayers, bool isKinematic, bool convex, bool isTrigger, bool provideContacts, PhysicsMaterial physicsMaterial)
        {
            if (!meshCollider) return;
            
            meshCollider.gameObject.layer = layer;
            meshCollider.sharedMaterial = physicsMaterial;
#if UNITY_2022_3_OR_NEWER
            meshCollider.includeLayers = includeLayers;
            meshCollider.excludeLayers = excludeLayers;
#endif
            
            Rigidbody rb = gameObject.GetComponent<Rigidbody>();
            if (isKinematic == false && rb)
            {
                if (Application.isPlaying)
                    Destroy(rb);
                else
                    DestroyImmediate(rb);
            }
            if(isKinematic && !rb) rb = gameObject.AddComponent<Rigidbody>();
            if (rb)
            {
#if UNITY_2022_3_OR_NEWER
                rb.includeLayers = 0;
                rb.excludeLayers = 0;
#endif
                rb.isKinematic = isKinematic;
            }
            
#if UNITY_2022_3_OR_NEWER
            meshCollider.providesContacts = provideContacts;
#endif
            
            //Set convex first, as isTrigger depends on it
            if(meshCollider.convex != convex) meshCollider.convex = convex;
    
            //Only allow isTrigger if the mesh is convex
            bool shouldBeTrigger = convex && isTrigger;
            if(meshCollider.isTrigger != shouldBeTrigger) meshCollider.isTrigger = shouldBeTrigger;
        }
        #endregion

        /// <summary>
        /// Find the nearest point on the section this segment occupies on its spline to the given world position.
        /// </summary>
        /// <param name="worldPosition"></param>
        /// <param name="nearestT">The normalize 't' distance value of the nearest point. May be used to re-sample the spline at that point to get other data.</param>
        /// <param name="sampleDistance">Sampling interval distance, keep as high as possible for best performance</param>
        /// <returns>Nearest position on the segment's spline (in world-space)</returns>
        public Vector3 GetNearestPointOnSpline(Vector3 worldPosition, out float nearestT, float sampleDistance = 0.2f)
        {
            float curveLength = curveInfo.curveRange.y - curveInfo.curveRange.x;
            Vector3 nearestPoint = Vector3.zero;
            nearestT = 0;

            if (curveLength < 0.02f)
            {
                return nearestPoint;
            }
            
            SplineContainer splineContainer = GetSplineContainer();
            Vector3 localPosition = splineContainer.transform.InverseTransformPoint(worldPosition);

            Spline spline = splineContainer.Splines[container.SplineIndex];
            float splineLength = spline.GetLength();
            
            float minDistance = float.MaxValue;

            float minT = curveInfo.curveRange.x / splineLength;
            float maxT = curveInfo.curveRange.y / splineLength;
            
            int sampleCount = Mathf.CeilToInt(curveLength / Mathf.Max(0.02f, sampleDistance));
            sampleCount = Mathf.Max(2, sampleCount);
            
            //Sample the spline between minT and maxT
            //Store the nearest point and return it
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (i / (float)sampleCount);
                float normalizedT = Mathf.Lerp(minT, maxT, t);

                Vector3 samplePoint = spline.EvaluatePosition(normalizedT);

                float distance = Vector3.Distance(localPosition, samplePoint);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestT = normalizedT;
                    nearestPoint = splineContainer.transform.TransformPoint(samplePoint);
                }
            }

            return nearestPoint;
        }
        
        /// <summary>
        /// Returns true if the mesh was previously lightmapped, but was rebuild at some point after. The result would be something that appears broken, as the lightmap is now incorrectly mapped to the surface.
        /// </summary>
        /// <returns></returns>
        public bool HasBrokenLightmap()
        {
            if(!mesh) return false;
            
            //Debug.Log($"{mesh.name} lightmap index: {meshRenderer.lightmapIndex}. UV2? :{mesh.uv2.Length != 0}", this);
            
            //Is assigned to a lightmap, but has no lightmap UVs
            return meshRenderer.lightmapIndex >= 0 && mesh.uv2.Length == 0;
        }
        
        private static readonly Color boundsGizmoColor = new Color(1f, 1f, 0.0f, 0.5f);
        private void OnDrawGizmosSelected()
        {
            #if UNITY_EDITOR
            if(UnityEditor.Selection.activeGameObject != this.gameObject) return;
            
            if (mesh)
            {
                Gizmos.color = boundsGizmoColor;
                Gizmos.matrix = this.transform.localToWorldMatrix;
                Utilities.DrawBoundsGizmo(mesh.bounds);
            }
            #endif
        }

        internal void DrawWireframeGizmo()
        {
            if (!meshFilter.sharedMesh) return;
            
            Gizmos.matrix = this.transform.localToWorldMatrix;
            if(meshFilter.sharedMesh.vertexCount > 0) Gizmos.DrawWireMesh(meshFilter.sharedMesh);
        }
        
        internal void DrawMeshGizmo()
        {
            if (!meshFilter.sharedMesh) return;
            
            Gizmos.matrix = this.transform.localToWorldMatrix;
            if(meshFilter.sharedMesh.vertexCount > 0) Gizmos.DrawMesh(meshFilter.sharedMesh);
            else if (meshCollider) Gizmos.DrawMesh(meshCollider.sharedMesh);
        }

        internal void DrawMeshNow()
        {
            if (!meshFilter.sharedMesh) return;
            
            Graphics.DrawMeshNow(meshFilter.sharedMesh, this.transform.localToWorldMatrix);
        }

        /// <summary>
        /// Destroys the mesh objects created by this segment.
        /// </summary>
        public void DestroyMeshes()
        {
            if (meshFilter && meshFilter.sharedMesh)
            {
                Utilities.Destroy(meshFilter.sharedMesh);
                meshFilter.sharedMesh = null; 
                
               // Debug.Log($"Destroying mesh segment {gameObject.name}", this);
            }

            if (meshCollider && meshCollider.sharedMesh)
            {
                Utilities.Destroy(meshCollider.sharedMesh);
                meshCollider.sharedMesh = null;
            }
            
            //if(meshFilter.sharedMesh) Debug.LogError("Mesh filter still has a mesh assigned to it. This should not happen", this);
        }
        
        //Ensure that when this instance is destroyed, that the created mesh objects are also disposed of.
        private void OnDestroy()
        {
            DestroyMeshes();
        }
        
        #region Events
        #pragma warning disable CS0067 //Event is never used
        public delegate void CollisionAction(SplineMeshSegment splineMeshSegment, Collision other);
        /// <summary>
        /// Collision callbacks.
        /// </summary>
        public static event CollisionAction onCollisionEnter, onCollisionExit;
        public delegate void TriggerAction(SplineMeshSegment splineMeshSegment, Collider other);
        /// <summary>
        /// Trigger callbacks.
        /// </summary>
        public static event TriggerAction onTriggerEnter, onTriggerStay, onTriggerExit;
        #pragma warning restore CS0067
        
        public void OnCollisionEnter(Collision other)
        {
            onCollisionEnter?.Invoke(this, other);
            Container.Owner.onCollisionEnter?.Invoke(other);
        }
        
        public void OnCollisionExit(Collision other)
        {
            onCollisionExit?.Invoke(this, other);
            Container.Owner.onCollisionExit?.Invoke(other);
        }

        public void OnTriggerEnter(Collider other)
        {
            onTriggerEnter?.Invoke(this, other);
            Container.Owner.onTriggerEnter?.Invoke(other);
        }

        public void OnTriggerStay(Collider other)
        {
            onTriggerStay?.Invoke(this, other);
            Container.Owner.onTriggerStay?.Invoke(other);
        }

        public void OnTriggerExit(Collider other)
        {
            onTriggerExit?.Invoke(this, other);
            Container.Owner.onTriggerExit?.Invoke(other);
        }
        #endregion
    }
}