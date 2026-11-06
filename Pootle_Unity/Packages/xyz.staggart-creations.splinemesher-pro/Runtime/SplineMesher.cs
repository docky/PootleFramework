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
using UnityEngine.Events;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if !UNITY_2022_3_OR_NEWER
#error This asset requires Unity 2022.3 or newer. Any errors you see are attributed to legitimate incompatibility. To resolve them delete this asset or upgrade the project to Unity 2022.3 or newer.
#endif

namespace sc.splinemesher.pro.runtime
{
    [AddComponentMenu("")] //Hide
    public partial class SplineMesher : MonoBehaviour, IDisposable
    {
        public const string kPackageRoot = "Packages/xyz.staggart-creations.splinemesher-pro";

        [Flags]
        public enum RebuildTriggers
        {
            [InspectorName("Via scripting")]
            None = 0,
            [InspectorName("On Spline Change")]
            OnSplineChanged = 1,
            OnSplineAdded = 2,
            OnSplineRemoved = 4,
            [InspectorName("On Load")]
            OnLoad = 8,
            OnUIChange = 16,
            OnTransformChange = 32,
            [InspectorName("On Mesh File Change (Editor)")]
            OnMeshImported = 64
        }

        [Tooltip("Control which sort of events cause the mesh to be regenerated." +
                 "\n\n" +
                 "For instance when the spline changes (default), or on the component's Start() function." +
                 "\n\n" +
                 "If none are selected you need to call the Rebuild() function through script.")]
        public RebuildTriggers rebuildTriggers = RebuildTriggers.OnSplineAdded | RebuildTriggers.OnSplineRemoved | RebuildTriggers.OnSplineChanged | RebuildTriggers.OnUIChange | RebuildTriggers.OnTransformChange | RebuildTriggers.OnMeshImported;

        public bool RebuildTriggersEnabled(RebuildTriggers trigger)
        {
            return (rebuildTriggers & trigger) != 0;
        }

        [Tooltip("The object under which spline meshes are created. If left empty, meshes are created in the scene's root.")]
        public Transform root;
        [SerializeField]
        internal List<SplineMeshContainer> containers = new List<SplineMeshContainer>();
        /// <summary>
        /// The list of containers that contain the generated meshes. One for each spline.
        /// </summary>
        public List<SplineMeshContainer> Containers => containers;
        
        internal readonly System.Diagnostics.Stopwatch stopWatch = new System.Diagnostics.Stopwatch();
        public float LastProcessingTime => stopWatch.IsRunning ? 0 : (float)stopWatch.Elapsed.TotalMilliseconds;

        [NonSerialized]
        public bool drawWireFrame = true;
        [NonSerialized]
        public bool drawUV = false;
        
        /// <summary>
        /// A list of Spline Meshers that should trigger this instance to rebuild after they do
        /// </summary>
        public List<SplineMesher> rebuildWith = new List<SplineMesher>();
        
        #pragma warning disable CS0067 //Event is never used
        public delegate void Action(SplineMesher instance);
        /// <summary>
        /// Pre- and post-build callbacks. The instance being passed is the Spline Mesher being rebuilt.
        /// </summary>
        public static event Action onPreRebuildMesher, onPostRebuildMesher;
        
        /// <summary>
        /// UnityEvent, fires whenever the spline is rebuild (eg. editing nodes) or parameters are changed
        /// </summary>
        public UnityEvent onPreRebuild, onPostRebuild;
        public UnityEvent<Collision> onCollisionEnter, onCollisionExit;
        public UnityEvent<Collider> onTriggerEnter, onTriggerStay, onTriggerExit;
        #pragma warning restore CS0067
        
        protected void TriggerPreRebuildEvent(SplineMesher instance)
        {
            onPreRebuildMesher?.Invoke(this);
            onPreRebuild?.Invoke();
        }
        
        public virtual void Rebuild(int splineIndex = -1)
        {
            //Debug.Log($"[Spline Mesher] Rebuilding {name}", this);

            if (!IsAllowedToRebuild(false)) return;
            
            Validate();
        }

        protected void TriggerPostRebuildEvent(SplineMesher instance)
        {
            onPostRebuildMesher?.Invoke(this);
            onPostRebuild?.Invoke();
        }

        /// <summary>
        /// Rebuilds the spline data cache if needed (spline count change, transform changed). Verifies if the spline data (roll, scale, vertex colors, conforming) is present for each spline.
        /// Checks for any null containers and removes them. Adopts any orphaned containers.
        /// Ensures the layer/static flags of this object is propagated to all child objects.
        /// </summary>
        public void Validate()
        {
            //If the container transform was altered, the cached native splines will be required to update
            if (splineContainer && (splineCount != nativeSplines.Count || splineContainer.transform.hasChanged))
            {
                splineContainer.transform.hasChanged = false;
                
                RebuildSplineCache();
            }

            ValidateData();
            ValidateContainers();
        }
        
        public virtual void ValidateData() { }

        internal bool HasCachedSpline(int splineIndex)
        {
            //If calling this function directly, yet the component is configured for only manual rebuilding
            //Then no native spline will have been created automatically
            if (RebuildTriggersEnabled(RebuildTriggers.OnSplineChanged) == false || RebuildTriggersEnabled(RebuildTriggers.OnSplineAdded) == false)
            {
                if (splineIndex >= nativeSplines.Count)
                {
                    Debug.LogError($"[Spline Curve Mesher] Manual rebuilding in use. Spline #{splineIndex} in {splineContainer.name} does not exist in cache. Ensure you have added the spline through the AddCacheSpline function.", this);
                    return false;
                }
            }

            return true;
        }
        
        /// <summary>
        /// Editor only. Checks if the instance is a prefab in the project window. Rebuilding may involve deleting objects or changing transform hierarchies.
        /// Which is only possible for scene instances or inside a prefab editing context.
        /// </summary>
        /// <returns></returns>
        public bool IsAllowedToRebuild(bool suppressError = true)
        {
#if UNITY_EDITOR
            //Prefab selected in the Project window, with a Spline Mesher component on its root
            bool inspectingPrefab = PrefabUtility.IsPartOfPrefabAsset(this.gameObject) && this.gameObject.scene.name != string.Empty;
            
            if (inspectingPrefab && !suppressError)
            {
                Debug.LogError("[Spline Mesher] Unable to rebuild, since the instance is a prefab in the project, not a scene instance. Unity disallows modifying prefab files directly.", this);
                return false;
            }
            
            return inspectingPrefab == false;
#else
            return true;
#endif
        }
        
        private static bool isExitingPlayMode;
        
        protected void SubscribeCallbacks()
        {
            SubscribeSplineCallbacks();
            onPostRebuildMesher += AfterMesherRebuilds;
            
            #if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            #endif
        }

        protected void UnsubscribeCallbacks()
        {
            UnsubscribeSplineCallbacks();
            onPostRebuildMesher -= AfterMesherRebuilds;
            
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
        }
        
#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            isExitingPlayMode = state == UnityEditor.PlayModeStateChange.ExitingPlayMode;
        }
#endif

        private void AfterMesherRebuilds(SplineMesher instance)
        {
            if (rebuildWith.Contains(instance) && instance != this)
            {
                //Debug.Log($"{this.name} rebuilds because it depends on {instance.name}", this);
                Rebuild();
            }
        }

        private void Start()
        {
            if (RebuildTriggersEnabled(RebuildTriggers.OnLoad) == false) return;
            
            #if UNITY_EDITOR
            //Do not execute when exiting play mode (due to the [ExecuteAlways] attribute)
            if (isExitingPlayMode) return;
            #endif

            Rebuild();
        }
        
        public static void ConvertIndexUnit<T>(ISpline spline, ref List<SplineData<T>> data, int index, PathIndexUnit targetUnit)
        {
            //Data points
            for (int i = 0; i < data[index].Count; i++)
            {
                data[index].ConvertPathUnit(spline, targetUnit);
            }

            //Set to new index unit
            data[index].PathIndexUnit = targetUnit;
        }
        
        private void ValidateContainers()
        {
            //Remove all null containers and ones that aren't parented under this object
            for (int i = containers.Count - 1; i >= 0; i--)
            {
                if (!containers[i] || containers[i].transform.parent != root)
                {
                    containers.RemoveAt(i);
                }
            }

            int adopted = 0;
            //If the root is empty, containers will not be children of the mesher.
            //Duplicating a mesher would mean the containers aren't duplicated
            for (int i = 0; i < containers.Count; i++)
            {
                //Belongs to the original, manually duplicate and adopt it
                if (containers[i].owner != this)
                {
                    SplineMeshContainer newContainer = GameObject.Instantiate(containers[i], root);
                    newContainer.owner = this;
                 
                    containers[i] = newContainer;

                    adopted++;
                }
            }
            
            //SplineCurveMesher or SplineFillMesher
            Type mesherType = this.GetType();
            
            //Check for orphaned containers under this object
            Transform rootTransform = root ?? transform;
            int childObjectCount = rootTransform.transform.childCount;
            for (int i = 0; i < childObjectCount; i++)
            {
                Transform child = rootTransform.GetChild(i);
                SplineMeshContainer container = child.GetComponent<SplineMeshContainer>();
                
                if(!container) continue;
                
                //Owner went missing, or the owner is actually a different spline mesher
                //This behaviour does prevent multiple mesher using the same Root object!
                if(container.owner == null || (container.owner != this && container.owner.GetType() == mesherType))
                {
                    container.owner = this;
                    adopted++;
                }
            }

            if (adopted > 0)
            {
                Debug.Log($"[Spline Mesher] {adopted} orphaned instances containers were found under {this.name}. So they have been adopted. This may happen when duplicating a Spline Mesher");
            }
            
            if (splineCount != containers.Count)
            {
                //Debug.LogWarning($"Mismatching number of object containers ({containers.Count}) relative to the number of splines ({splineCount}). Synchronizing them now. This may happen if containers are manually deleted, or the spline container was changed.");

                if (containers.Count > 0)
                {
                    //Remove excess containers
                    for (int i = containers.Count - 1; i >= splineCount; i--)
                    {
                        containers[i].Destroy();
                        containers.RemoveAt(i);
                    }
                }

                //Add missing containers
                for (int i = containers.Count; i < splineCount; i++)
                {
                    containers.Add(SplineMeshContainer.Create(this, i));
                }
            }

            //Copy static flags for occlusion culling and global illumination
            #if UNITY_EDITOR
            if (this.gameObject.isStatic)
            {
                UnityEditor.StaticEditorFlags staticFlags = UnityEditor.GameObjectUtility.GetStaticEditorFlags(this.gameObject);
                
                foreach (var container in containers)
                {
                    UnityEditor.GameObjectUtility.SetStaticEditorFlags(container.gameObject, staticFlags);

                    foreach (var segment in container.Segments)
                    {
                        UnityEditor.GameObjectUtility.SetStaticEditorFlags(segment.gameObject, staticFlags);
                    }
                }
            }
            #endif
        }
        
        public virtual void Dispose()
        {
            DisposeNativeSplines();
        }

        private static Material _defaultMaterial;
        /// <summary>
        /// A default material that is compatible with the current render pipeline. Only in the editor will it have a nice checker texture assigned.
        /// </summary>
        public static Material defaultMaterial
        {
            get
            {
                if (!_defaultMaterial)
                {
                    _defaultMaterial = Utilities.CreateDefaultMaterial();
                    _defaultMaterial.hideFlags = HideFlags.NotEditable | HideFlags.DontSave;
                }
                return _defaultMaterial;
            }
        }
        
        public virtual void AssignMaterials(Material[] materials)
        {
            
        }

        [NonSerialized]
        private bool firstTransformChange = true;
        /// <summary>
        /// Checks for changes to the Spline Container or Root's transform. If so, a rebuild is triggered.
        /// </summary>
        public void ListenForTransformChanges()
        {
            if (!RebuildTriggersEnabled(RebuildTriggers.OnTransformChange)) return;
            
            var hasSplineChange = false;
            if (splineContainer)
            {
                hasSplineChange = splineContainer.transform.hasChanged;
                splineContainer.transform.hasChanged = false;
            }

            var hasRootChange = false;
            if (root)
            {
                hasRootChange = root.hasChanged;
                root.hasChanged = false;
            }

            if (!firstTransformChange && (hasSplineChange || hasRootChange))
            {
                if(hasSplineChange) RebuildSplineCache();
                Rebuild();
                
                //Debug.Log($"[Spline Mesher] {name} rebuilt due to transform change. Root:{hasRootChange}. Spline:{hasSplineChange}", this);
            }

            if (firstTransformChange) firstTransformChange = false;
        }

        private static Material uvMaterial;
        private void OnDrawGizmosSelected()
        {
            DrawComponentGizmos();

            #if UNITY_EDITOR
            if (drawUV)
            {
                if (!uvMaterial)
                {
                    string shaderPath = $"{SplineMesher.kPackageRoot}/Editor/Resources/VisualizeVertexAttributes.shader";
                    Shader shader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
                    uvMaterial = new Material(shader);
                    uvMaterial.EnableKeyword("_DISPLAY_UV0");
                    uvMaterial.DisableKeyword("_DISPLAY_COLOR");
                    uvMaterial.SetFloat("_Display", 1);
                    uvMaterial.SetFloat("_UVChecker", 1);
                    uvMaterial.SetFloat("_Shaded", 1f);
                    uvMaterial.name = "UV Checkers";
                }
                
                uvMaterial.SetPass(0);
                foreach (var container in containers)
                {
                    foreach (var segment in container.Segments)
                    {
                        segment.DrawMeshNow();
                    }
                }
            }
            #endif
            
            if (drawWireFrame)
            {
                Gizmos.color = new Color(0, 0, 0, 0.25f);
                
                foreach (var container in containers)
                {
                    if(!container) continue; //Deleted by user
                    
                    foreach (var segment in container.Segments)
                    {
                        if(segment) segment.DrawWireframeGizmo();
                    }
                }
            }

            ListenForTransformChanges();
        }

        public virtual void DrawComponentGizmos() { }
        
        public virtual int GetLODCount()
        {
            return 0;
        }
        
        public void CountMeshVertTris(out int vertexCount, out int triangleCount)
        {
            vertexCount = 0;
            triangleCount = 0;
            
            foreach (var container in containers)
            {
                if(!container) continue;
                
                foreach (var segment in container.Segments)
                {
                    if(!segment) continue;
                    
                    var stats = segment.GetMeshStats();

                    vertexCount += stats.Item1;
                    triangleCount += stats.Item2;
                }
            }
        }

        //In megabytes
        public float CalculateMemorySize()
        {
            float size = 0f;
                
            foreach (var container in containers)
            {
                if(!container) continue;
                
                foreach (var segment in container.Segments)
                {
                    if(segment) size += segment.GetMemorySize();
                }
            }

            return size;
        }

        private void OnDestroy()
        {
            Dispose();
        }

        /// <summary>
        /// Explicitly destroy all the meshes (and collision meshes) created and contained in the mesh segments (child objects).
        /// This is required for proper memory management, as meshes can linger in memory until the garbage collector runs.
        /// Calling <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> on this instance alone will not guarantee this happens otherwise.
        /// </summary>
        public void DestroyMeshes()
        {
            foreach (var container in containers)
            {
                if (!container) continue;
                
                foreach (var segment in container.Segments)
                {
                    if (!segment) continue;
                        
                    //Force immediate cleanup of meshes before child destruction
                    segment.DestroyMeshes();
                }
            }
        }
    }
}