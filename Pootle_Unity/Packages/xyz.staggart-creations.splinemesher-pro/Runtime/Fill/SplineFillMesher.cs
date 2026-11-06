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
    [ExecuteAlways]
    [SelectionBase]
    [AddComponentMenu("Splines/Spline Fill Mesher")]
    [Icon(SplineMesher.kPackageRoot + "/Editor/Resources/Components/spline-fill-mesher-icon-64px.psd")]
    public partial class SplineFillMesher : SplineMesher
    {
        public static readonly List<SplineFillMesher> Instances = new List<SplineFillMesher>();
        
        public FillMeshSettings settings = new FillMeshSettings();
        
        #if SM_DEV
        public bool gizmos;
        #endif
        
        //Vertex positions
        private NativeArray<float3> positions;
        //Keeps track of the state of each point (inside, edge or outside)
        private NativeArray<PointState> pointStates;
        //Distances from point to spline, for vertex colors
        private NativeArray<float> distances;
        //Points on spline nearest to each vertex
        private NativeArray<float3> splinePoints;
        //Holds the indices of the points
        private NativeArray<int> indexMapping;
        private NativeList<int> triangles;
        
        private NativeArray<RaycastHit> hits;
        
        internal const float MIN_TRIANGLE_SIZE = 0.02f;
        
        private void OnEnable()
        {
            Instances.Add(this);
            SubscribeCallbacks();
            
            //Rebuild();
        }
        
        public void Reset()
        {
            root = this.transform;
            splineContainer = GetComponentInParent<SplineContainer>();
            settings.renderer.SetDefaults();
        }

        public override void Rebuild(int splineIndex = -1)
        {
            if (!splineContainer) return;
            
            base.Rebuild();
            
            TriggerPreRebuildEvent(this);
            
            stopWatch.Restart();

            if (splineIndex >= 0)
            {
                RebuildSpline(splineIndex);
            }
            else
            {
                for (int i = 0; i < splineCount; i++)
                {
                    RebuildSpline(i);
                }
            }

            stopWatch.Stop();
            
            TriggerPostRebuildEvent(this);
        }

        //Note: Spline knots need to be transformed to world space before checking the winding order
        private bool IsSplineClockwise(ISpline spline)
        {
            //Right vector of the first knot
            float3 outwardDirection = math.mul(spline[0].Rotation, math.right());
            //Direction to the center of the spline
            float3 dirToCenter = math.normalize((float3)splineContainer.transform.position - spline[0].Position);

            bool clockwise = math.dot(outwardDirection, dirToCenter) < 0;

            return clockwise;
        }
        
        private void RebuildSpline(int splineIndex)
        {
            if (HasCachedSpline(splineIndex) == false) return;
            
            NativeSpline spline = nativeSplines[splineIndex];
            float splineLength = spline.GetLength();
            
            //Invalid
            if(spline.Count < 2 || splineLength < 0.5f) return;
            
            //If the spline winding order is counter-clockwise, the right vector should be flipped
            bool clockwise = IsSplineClockwise(spline);
            
            Bounds bounds = spline.GetBounds(float4x4.identity); //Already in world-space now

            Vector3 offset = new Vector3(settings.topology.margin, 0f, settings.topology.margin);
            
            bounds.min -= offset;
            bounds.max += offset;
            bounds.SetMinMax(bounds.min, bounds.max);
            
            SplineMeshContainer container = Containers[splineIndex];
            container.transform.localPosition = Vector3.zero;
            container.transform.localRotation = Quaternion.identity;
            
            container.PrepareSegments(1);

            SplineMeshSegment segment = container.GetOrCreateSegment(0);
            segment.transform.localPosition = Vector3.zero;
            segment.container = container;
            
            float m_triangleSize = settings.topology.GetTriangleSize(splineLength);
            
            float splineSampleInterval = 1f;
            splineSampleInterval = settings.topology.accuracy switch
            {
                Structs.Accuracy.BestPerformance => 6f,
                Structs.Accuracy.PreferPerformance => 4f,
                Structs.Accuracy.Balanced => 3f,
                Structs.Accuracy.PreferPrecision => 1f,
                Structs.Accuracy.HighestPrecision => 0.5f,
                _ => splineSampleInterval
            };
            SplineCache splinePointsJob = new SplineCache(spline, m_triangleSize * splineSampleInterval, false);

            var createMesh = !(settings.collision.enable && settings.collision.colliderOnly);
            
            if (createMesh)
            {
                segment.SetMaterial(settings.renderer.material);
                
                //Avoid conforming to the collider
                segment.SetColliderEnabled(false);
                
                Mesh mesh = segment.mesh;
                #if DEBUG
                mesh.name = this.name + " Spline Fill Mesh";
                #endif
                
                segment.mesh = GenerateMesh(spline, splinePointsJob.Points, clockwise, bounds, m_triangleSize, settings.output.keepReadable, ref mesh);
                
                segment.SetMaterial(settings.renderer.material);
                segment.SetRendererParameters(settings.renderer.shadowCastingMode, settings.renderer.lightProbeUsage, settings.renderer.reflectionProbeUsage, settings.renderer.renderingLayerMask, settings.output.forceMeshLod, settings.output.lodSelectionBias);
            }
            else
            {
                segment.mesh = null;
            }
            segment.SetMeshCollider(settings.collision.enable);
            
            if (settings.collision.enable)
            {
                Mesh collisionMesh = segment.collisionMesh;
                GenerateMesh(spline, splinePointsJob.Points, clockwise, bounds, Mathf.Max(m_triangleSize, 0.05f), true, ref collisionMesh);
                
                #if DEBUG
                collisionMesh.name = this.name + " Collider";
                #endif
                
                segment.SetColliderEnabled(true);
                segment.gameObject.layer = settings.collision.layer;
                
                segment.SetColliderSettings(settings.collision.layer, settings.collision.includeLayers, settings.collision.excludeLayers, false, false, false, settings.collision.provideContacts, settings.collision.physicsMaterial);
                segment.collisionMesh = collisionMesh;
            }
            
            segment.EnsureUniqueMeshes();
            
            splinePointsJob.Dispose();
        }

        public override void AssignMaterials(Material[] materials)
        {
            SetMaterial(materials[0]);
        }

        public void SetMaterial(Material target)
        {
            this.settings.renderer.material = target;
            foreach (var container in containers)
            {
                foreach (var segment in container.Segments)
                {
                    segment.SetMaterial(target);
                }
            }
        }

        private Mesh GenerateMesh(NativeSpline spline, NativeArray<Structs.SplinePoint> splineCachePoints, bool clockwise, Bounds bounds, float m_triangleSize, bool readable, ref Mesh mesh)
        {
            Profiler.BeginSample("Spline Fill Mesher: Vertices");
            
            float3 size = bounds.size;
            int2 gridSize = CreateVertices.GetGridSize(m_triangleSize, size.x, size.z);
            
            CreateVertices gridJob = new CreateVertices();
            gridJob.Setup(gridSize, m_triangleSize, bounds, spline, clockwise, splineCachePoints, this);

            JobHandle gridJobHandle = gridJob.Schedule(gridSize.x * gridSize.y, 128);
            gridJobHandle.Complete();
            
            //Grab resulting arrays
            positions = gridJob.GetPoints();
            pointStates = gridJob.GetPointStates();
            distances = gridJob.GetDistances();
            splinePoints = gridJob.GetNearestSplinePoints();
            
            int vertexCount = positions.Length;

            //Calculate maximum distance value, so a normalized value can be derived
            float averageHeight = 0;
            float maxDistance = float.MinValue;
            for (int j = 0; j < vertexCount; j++)
            {
                //Sum the height so that vertices can be positioned at the average (works better than the bounds center)
                averageHeight += splinePoints[j].y;
                maxDistance = math.max(maxDistance, distances[j]);
            }
            averageHeight /= vertexCount;
            
            Profiler.EndSample();
            
            Profiler.BeginSample("Spline Fill Mesher: Filtering");
            
            FilterPoints filterJob = new FilterPoints();
            filterJob.Setup(positions, pointStates, splinePoints, distances);

            JobHandle filterJobHandle = filterJob.Schedule(gridJobHandle);
            filterJobHandle.Complete();
            
            //Adopt filtered arrays
            positions = filterJob.newPoints;
            pointStates = filterJob.newStates;
            distances = filterJob.newDistances;
            splinePoints = filterJob.newSplinePoints;
            
            indexMapping = filterJob.indexMapping;
            
            filterJob.OnCompleted();
            
            Profiler.EndSample();

            vertexCount = positions.Length;

            if (settings.conforming.enable)
            {
                NativeArray<Structs.SplinePoint> splinePoints =
                    new NativeArray<Structs.SplinePoint>(positions.Length, Allocator.Temp);

                for (int i = 0; i < positions.Length; i++)
                {
                    splinePoints[i] = new Structs.SplinePoint(positions[i], default, math.up());
                }
                
                ConformRaycaster conformRaycaster = new ConformRaycaster();
                conformRaycaster.Raycast(splinePoints, settings.conforming.layerMask, settings.conforming.seekDistance, false);
                splinePoints.Dispose();
                
                hits = conformRaycaster.Hits;
            }
            else
            {
                hits = new NativeArray<RaycastHit>(0, Allocator.TempJob);
            }

            float4x4 worldToLocalMatrix = root ? root.transform.worldToLocalMatrix : Matrix4x4.identity;
            
            Profiler.BeginSample("Spline Fill Mesher: Displacement");
            
            Displacement displacementJob = new Displacement();
            displacementJob.Setup(this.settings.displacement, this.settings.conforming, worldToLocalMatrix, 
                positions, pointStates, distances, splinePoints, hits, maxDistance, averageHeight);

            JobHandle displacementJobHandle = displacementJob.Schedule(vertexCount, 64);
            displacementJobHandle.Complete();

            Bounds vertexBounds = displacementJob.GetBounds();
            
            Profiler.EndSample();
            
            Profiler.BeginSample("Spline Fill Mesher: Triangulation");
            
            //Triangles
            Triangulate triangulateJob = new Triangulate();
            triangulateJob.Setup(gridSize, m_triangleSize, settings.topology.reverseFaces, positions, pointStates, indexMapping);

            JobHandle triangulateJobHandle = triangulateJob.Schedule(gridJobHandle);
            triangulateJobHandle.Complete();
            
            triangles = triangulateJob.GetTriangles();
            
            Profiler.EndSample();

            Profiler.BeginSample("Spline Fill Mesher: UVs");

            //UV coordinates
            GenerateUV uvJob = new GenerateUV();
            uvJob.Setup(worldToLocalMatrix, positions, distances, vertexBounds.min, vertexBounds.max, settings.uv);

            JobHandle uvJobHandle = uvJob.Schedule();
            uvJobHandle.Complete();

            NativeArray<float4> uv0 = uvJob.GetUV0();
            NativeArray<float2> uv1 = uvJob.GetUV1();
            
            Profiler.EndSample();
            
            Profiler.BeginSample("Spline Fill Mesher: Calculate Normals & Tangents");
            
            //Normals & tangents
            CalculateNormals normalsJob = new CalculateNormals();
            normalsJob.Setup(positions, uv0, triangles, settings.topology.flipNormals);

            JobHandle normalsJobHandle = normalsJob.Schedule();
            normalsJobHandle.Complete();

            NativeArray<float3> normals = normalsJob.GetNormals();
            NativeArray<float4> tangents = normalsJob.GetTangents();
            
            Profiler.EndSample();
            
            Profiler.BeginSample("Spline Fill Mesher: Create Mesh");
            {
                //Populate vertex data
                NativeArray<Vertex> vertices = new NativeArray<Vertex>(vertexCount, Allocator.Temp);
                
                for (int i = 0; i < vertexCount; i++)
                {
                    vertices[i] = new Vertex(positions[i], normals[i], tangents[i], new float4(distances[i], 0, 0, 0), uv0[i], uv1[i]);
                }
                
                mesh = CreateMesh(vertices, displacementJob.GetBounds(), readable, ref mesh);
                
                vertices.Dispose();
            }
            Profiler.EndSample();
            
            triangulateJob.Dispose();
            displacementJob.Dispose();
            normalsJob.Dispose();
            uvJob.Dispose();
                        
            normals.Dispose();
            tangents.Dispose();

            hits.Dispose();
            
            //TODO: Currently needed still for debugging the points
            #if SM_DEV
            if (gizmos == false)
            {
            #endif
                filterJob.Dispose();
                gridJob.Dispose();
            #if SM_DEV
            }
            #endif

            return mesh;
        }
        
        public override void DrawComponentGizmos()
        {
            #if SM_DEV
            if (gizmos && positions.IsCreated && pointStates.IsCreated)
            {
#if UNITY_EDITOR
                UnityEditor.Handles.matrix = Gizmos.matrix;

                float r = settings.topology.triangleSize * 0.25f / this.transform.lossyScale.x;
                for (int i = 0; i < positions.Length; i++)
                {
                    if (pointStates[i] == PointState.Outside) Gizmos.color = Color.red;
                    if (pointStates[i] == PointState.Inside) Gizmos.color = Color.green;
                    if (pointStates[i] == PointState.Edge) Gizmos.color = Color.yellow;

                    Gizmos.DrawSphere(positions[i], r);
                }
                for (int i = 0; i < positions.Length; i++)
                {
                    UnityEditor.Handles.zTest = CompareFunction.Always;
                    UnityEditor.Handles.color = Color.black;
                    UnityEditor.Handles.Label(positions[i] + math.up() * r * 4f, i.ToString(), UnityEditor.EditorStyles.boldLabel);
                }
#endif
            }
            #endif
        }

        private void OnDisable()
        {
            Instances.Remove(this);
            UnsubscribeCallbacks();
            
            Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
        }
        
        public override int GetLODCount()
        {
            return settings.output.maxLodCount;
        }
    }
}