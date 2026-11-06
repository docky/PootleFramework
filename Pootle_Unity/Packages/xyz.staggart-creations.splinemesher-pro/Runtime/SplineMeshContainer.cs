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

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace sc.splinemesher.pro.runtime
{
    [AddComponentMenu("")] //Hide
    [DisallowMultipleComponent]
    public class SplineMeshContainer : MonoBehaviour
    {
        //Important to track the container's owner, as duplicating a spawner (and thus its containers) would not update references (the copy will reference the original's containers)
        [SerializeReference]
        internal SplineMesher owner;
        /// <summary>
        /// The Spline Mesher that this container belongs to.
        /// </summary>
        public SplineMesher Owner
        {
            get
            {
                if (!owner)
                {
                    throw new System.Exception("Unable to get owner. This container does not belong to a Spline Mesher. It has been orphaned. To prevent this, ensure that the component isn't moved or detached");
                }
                return owner;
            }
        }

        [SerializeField] [HideInInspector]
        internal int splineIndex;
        /// <summary>
        /// The index of the spline this container is created for. 
        /// </summary>
        public int SplineIndex => splineIndex;
        
        [SerializeField] [HideInInspector]
        private List<SplineMeshSegment> segments = new List<SplineMeshSegment>();
        /// <summary>
        /// The list of segments that contain the generated meshes.
        /// </summary>
        public List<SplineMeshSegment> Segments => segments;
        public int SegmentCount => segments.Count;
        
        internal static SplineMeshContainer Create(SplineMesher mesher, int splineIndex)
        {
            var objName = string.Empty;
            
            #if DEBUG
            objName = $"Spline #{splineIndex} Meshes";
            #endif
            
            GameObject go = new GameObject(objName);

            #if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(go, $"Create Spline Meshes container new spline #{splineIndex}");
            #endif
            
            go.transform.SetParent(mesher.root);
            go.transform.localPosition = Vector3.zero;
            go.transform.SetSiblingIndex(splineIndex);
            go.transform.hideFlags = HideFlags.NotEditable;

            SplineMeshContainer container = go.AddComponent<SplineMeshContainer>();
            container.owner = mesher;
            container.splineIndex = splineIndex;
            
            return container;
        }

        internal void PrepareSegments(int count)
        {
            //Remove any null references or ones that were manually removes from the transform
            for (int i = segments.Count - 1; i >= 0; i--)
            {
                if (!segments[i] || segments[i].transform.parent != this.transform)
                {
                    segments.RemoveAt(i);
                }
            }
            
            int currentCount = segments.Count;
            
            //Debug.Log($"Current count: {currentCount}. target count: {count}");
            
            int delta = count - currentCount;
            
            //Trim excess segments
            if (delta < 0)
            {
                //Debug.Log($"Trimming {currentCount - count} segments", owner);
                
                for (int i = currentCount-1; i >= count; i--)
                {
                    Utilities.Destroy(segments[i].gameObject);
                    segments.RemoveAt(i);
                }
            }
            //Append newly needed segments
            else
            {
                //Debug.Log($"Adding {delta} segments", owner);

                for (int i = 0; i < delta; i++)
                {
                    SplineMeshSegment segment = SplineMeshSegment.Create(this, i);
                    segments.Add(segment);
                }
            }
        }
        
        public void Destroy()
        {
            if (this == null) return;
            
            if (Application.isPlaying)
                Destroy(this.gameObject);
            else
                DestroyImmediate(this.gameObject);
        }
        
        internal SplineMeshSegment GetOrCreateSegment(int segmentIndex)
        {
            if (segmentIndex > segments.Count - 1)
            {
                SplineMeshSegment segment = SplineMeshSegment.Create(this, segmentIndex);
                segments.Add(segment);
            }
            
            return segments[segmentIndex];
        }

        [ContextMenu("Delete All Segments")]
        public void DeleteAllSegments()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                Utilities.Destroy(segments[i].gameObject);
            }
            segments.Clear();
        }
    }
}