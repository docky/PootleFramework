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
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace sc.splinemesher.pro.runtime
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance)]
    public partial struct CurveToMesh : IJob
    {
        //4 iterations yield a sample at 4 corners
        private const int boundsSampleIterations = 4;
        
        [ReadOnly] private NativeSpline spline;
        [ReadOnly] private float splineLength;
        [ReadOnly] private float4x4 splineLocalToWorld;
        [ReadOnly] private float3 splineTransformForward;
        [ReadOnly] private float3 splineTransformUp;
        [ReadOnly] private NativeArray<Structs.SplinePoint> splinePoints;
        
        private float2 curveRange;
        private float curveLength;
        private float minT, maxT;
        [ReadOnly] private SplineCurveMeshData splineMeshData;
        [ReadOnly] private NativeArray<RaycastHit> raycastHits;
        private float3 positionOffset; //Value used to center the pivot

        private int tileCount;
        private float tileLength;
        private float meshLength;
        
        //Input mesh
        [ReadOnly] private Structs.BoundsData inputBounds;
    
        [ReadOnly] private NativeArray<float3> sourcePositions;
        private ushort sourceVertexCount;
        [ReadOnly] private NativeArray<float3> sourceNormals;
        [ReadOnly] private NativeArray<float4> sourceTangents;
        [ReadOnly] private NativeArray<float2> sourceUV0;
        [ReadOnly] private NativeArray<float4> sourceColors;
    
        [ReadOnly] private NativeArray<ushort> sourceTriangles;
        private int submeshCount;
        [ReadOnly] private NativeArray<int2> sourceSubmeshRanges;
        
        //Output mesh
        private bool createMesh;
        private int vertexCount;
        //[WriteOnly]
        private NativeArray<Vertex> vertices;
        private int triangleCount;
        [WriteOnly]
        private NativeArray<ushort> indices;

        //Input collider
        [ReadOnly] private NativeArray<float3> sourceColliderPositions;
        private int sourceColliderVertexCount;
        [ReadOnly] private NativeArray<float3> sourceColliderNormals;
        [ReadOnly] private NativeArray<ushort> sourceColliderTriangles;

        //Output collider
        private bool createCollider;
        private int colliderVertexCount;
        [WriteOnly]
        private NativeArray<Vertex> colliderVertices;
        private int colliderTriangleCount;
        [WriteOnly]
        private NativeArray<ushort> colliderIndices;

        private float4x4 rendererWorldToLocal;
        private float3x3 normalTransform;
        
        //Output bounds
        private float3 currentBoundsMin;
        private float3 currentBoundsMax;
        [WriteOnly]
        private NativeArray<float3> metaData;

        public float3 finalBoundsMin => metaData[0];
        public float3 finalBoundsMax => metaData[1];
        public float3 pivotOffset => metaData[2];
        public bool zeroScaleMesh => metaData[3].x > 0;
        public bool zeroScaleCollider => metaData[3].y > 0;
        
        //Settings
        private CurveMeshSettings.Distribution distributionSettings;
        private CurveMeshSettings.Scale scaleSettings;
        private NativeCurve scaleCurve;
        private CurveMeshSettings.Rotation rotationSettings;
        private CurveMeshSettings.Conforming conformingSettings;
        private CurveMeshSettings.Color colorSettings;
        private CurveMeshSettings.UV uvSettings;
        private CurveMeshSettings.OutputMesh outputSettings;

        //Track the average scale to determine if the mesh is extremely thin
        private float3 totalMeshScale; 
        private float3 totalColliderScale;
        
        private void PrepareOutput()
        {
            //Array sizes
            vertexCount = tileCount * sourceVertexCount;
            triangleCount = tileCount * sourceTriangles.Length;
            
            //Output mesh
            vertices = new NativeArray<Vertex>(vertexCount, Allocator.TempJob);
            indices = new NativeArray<ushort>(triangleCount, Allocator.TempJob);

            //Collider
            if (createCollider)
            {
                colliderVertexCount = tileCount * sourceColliderVertexCount;
                colliderTriangleCount = tileCount * sourceColliderTriangles.Length;

                colliderVertices = new NativeArray<Vertex>(colliderVertexCount, Allocator.TempJob);
                colliderIndices = new NativeArray<ushort>(colliderTriangleCount, Allocator.TempJob);
            }
            else
            {
                colliderVertices = new NativeArray<Vertex>(0, Allocator.TempJob);
                colliderIndices = new NativeArray<ushort>(0, Allocator.TempJob);
            }

            currentBoundsMin = new float3(float.MaxValue);
            currentBoundsMax = new float3(float.MinValue);

            metaData = new NativeArray<float3>(4, Allocator.TempJob);
            //0: Bounds min
            //1: Bounds max
            //2: Position offset pivot
            //3: X: Valid mesh generated (bool)/ Y: Valid collider generated (bool)
        }

        private void PrepareSettings(CurveMeshSettings settings)
        {
            this.createCollider = settings.collision.enable;
            
            distributionSettings = settings.distribution;
            scaleSettings = settings.scale;
            rotationSettings = settings.rotation;
            conformingSettings = settings.conforming;
            outputSettings = settings.output;
            colorSettings = settings.color;
            uvSettings = settings.uv;
            
            scaleCurve = new NativeCurve(settings.scaleOverCurve, Allocator.TempJob);
        }

        public void Setup(Structs.InputMeshData inputMeshData, bool createMesh, Structs.InputMeshData inputColliderData, bool createCollider,
            NativeSpline sourceSpline, Matrix4x4 splineLocalToWorld, NativeArray<Structs.SplinePoint> splinePoints, Structs.CurveSegmentInfo segmentInfo,
            float trimmedSplineLength, Matrix4x4 outputWorldToLocal, CurveMeshSettings settings,
            SplineCurveMeshData splineData, NativeArray<RaycastHit> hits)
        {
            this.spline = sourceSpline;
            splineLength = this.spline.GetLength();
            this.splineLocalToWorld = splineLocalToWorld;
            splineTransformForward = math.normalize(splineLocalToWorld.GetColumn(2)).xyz;
            splineTransformUp = math.normalize(splineLocalToWorld.GetColumn(1)).xyz;
            this.splinePoints = splinePoints;

            //These represent the section of the total spline curve this mesh is generated on
            //So includes trimming and the range for the current segment
            this.curveRange = segmentInfo.curveRange;
            this.curveLength = trimmedSplineLength;
            //No need to clamp by MIN_T_VALUE/MAX_T_VALUE since the SplineData was already sampled using clamped t-values.
            minT = Mathf.Max(0f, curveRange.x / splineLength);
            maxT = Mathf.Min(1f, curveRange.y / splineLength);
            
            this.splineMeshData = splineData;
            this.raycastHits = hits;

            //Note these values are of the visual mesh, as such it needs to be identical for a collider
            this.tileCount = segmentInfo.tileCount;
            this.tileLength = segmentInfo.tileLength;
            //Length of tile including spacing
            this.meshLength = segmentInfo.tileOccupiedLength;

            this.createMesh = createMesh;
            
            this.inputBounds = inputMeshData.bounds;
            this.sourcePositions = inputMeshData.positions;
            this.sourceVertexCount = inputMeshData.vertexCount;
            this.sourceNormals = inputMeshData.normals;
            this.sourceTangents = inputMeshData.tangents;
            this.sourceUV0 = inputMeshData.uv;
            this.sourceColors = inputMeshData.colors;

            this.sourceTriangles = inputMeshData.sourceTriangles;
            this.submeshCount = inputMeshData.submeshCount;
            this.sourceSubmeshRanges = inputMeshData.sourceSubmeshRanges;
            
            this.createCollider = createCollider;
            
            //Debug.Log($"CurveToMesh.Setup: createMesh:{createMesh}. createCollider:{createCollider}");
            
            this.sourceColliderPositions = inputColliderData.positions;
            this.sourceColliderVertexCount = inputColliderData.vertexCount;
            this.sourceColliderNormals = inputColliderData.normals;
            this.sourceColliderTriangles = inputColliderData.sourceTriangles;
            
            //If no mesh is created, use the bounds from the collider data. This is important for the collider mesh to be generated correctly
            if(!this.createMesh) inputBounds = inputColliderData.bounds;
            
            PrepareSettings(settings);
            PrepareOutput();
            
            //Matrix to convert the world-space vertices into local-space again
            rendererWorldToLocal = outputWorldToLocal;
            
            //Normals need the inverse transpose for correct transformation under rotation/scale
            //Transpose the matrix. Does not support scaling/skewing, but the output renderers are only every translated.
            //Note: Using `math.transpose((float3x3)rendererWorldToLocal);` also works, but doesn't work if the parent of the renderer has a rotation. Instead, extract purely the rotation
            normalTransform = new float3x3(math.normalize(rendererWorldToLocal.c0.xyz), math.normalize(rendererWorldToLocal.c1.xyz), math.normalize(rendererWorldToLocal.c2.xyz));
            
            //Supports skewing/scaling but is much slower
            //normalTransform = math.transpose((float3x3)math.inverse(rendererWorldToLocal));
            
            #if DEBUG
            if (conformingSettings.enable && raycastHits.Length == 0)
            {
                Debug.LogError("Conforming enabled, but not no raycast hits provided...");
            }
            #endif
        }
        
        float prevZ;
        float3 origin, tangent, forward, up, right, scale;
        private quaternion rotation, normalRotation;
        private float4 vertexColor;
        private float3 splineScale;
        //Cache rotation matrices to avoid repeated quaternion-to-matrix conversions
        private float3x3 rotationMatrix, normalRotationMatrix;

        private Structs.BoundsData CalculateCurveBounds()
        {
            //Calculate a fast approximate bounds of the curve segment.
            
            float3 curveBoundsMin = new float3(float.MaxValue);
            float3 curveBoundsMax = new float3(float.MinValue);
            for (int i = 0; i < boundsSampleIterations; i++)
            {
                float t = (float)i / (boundsSampleIterations-1);
                
                t = math.lerp(minT, maxT, t);
                
                SplineCache.Sample(ref splinePoints, t, out float3 curvePos, out var _, out var _);
                
                curveBoundsMin = math.min(curveBoundsMin, curvePos);
                curveBoundsMax = math.max(curveBoundsMax, curvePos);
            }
            
            return new Structs.BoundsData(curveBoundsMin, curveBoundsMax);
        }
        
        public void Execute()
        {
            splineScale = new float3(1f);
            vertexColor = colorSettings.NativeBaseColor;

            scale = new float3(scaleSettings.scale.x, scaleSettings.scale.y, 0f);

            Structs.BoundsData curveBounds = CalculateCurveBounds();
            //Offset required to center the pivot point (in object-space), must be set afterwards as the GameObject's local position
            positionOffset = math.mul(rendererWorldToLocal, new float4(curveBounds.CalculatedCenter, 1f)).xyz;
            
            int meshVertIndex = 0;
            int colliderVertIndex = 0;
            for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
            {
                //Space between each tile, including empty spacing
                float tileOffset = (float)tileIndex * meshLength;
                
                //Reset every tile
                prevZ = float.MinValue;
                
                if (createMesh)
                {
                    ProcessTile(ref meshVertIndex, ref vertices, tileOffset, inputBounds,
                        sourcePositions, sourceNormals, sourceTangents, sourceColors, sourceUV0, false);
                }
                
                if (createCollider)
                {
                    prevZ = float.MinValue;
                    ProcessTile(ref colliderVertIndex, ref colliderVertices, tileOffset, inputBounds,
                        sourceColliderPositions, sourceColliderNormals, sourceTangents, sourceColors, sourceUV0, true);
                }
            }
            
            metaData[0] = currentBoundsMin;
            metaData[1] = currentBoundsMax;
            metaData[2] = positionOffset;
            
            float averageMeshScale = math.length(totalMeshScale / vertices.Length);
            float averageColliderScale = math.length(totalColliderScale / (colliderVertices.Length));
            metaData[3] = new float3(averageMeshScale < MIN_AVERAGE_SCALE ? 1 : 0, averageColliderScale < MIN_AVERAGE_SCALE ? 1 : 0, 0);
            
            //Debug.Log($"Average mesh scale: {averageMeshScale}. Average collider scale: {averageColliderScale}. Considered invalid: {averageMeshScale < MIN_AVERAGE_SCALE} & {averageColliderScale < MIN_AVERAGE_SCALE}");
            
            int triangleIndex = 0;
            if (createMesh)
            {
                //Iterate submeshes first, then tiles, matches the layout expected by CreateMesh()
                for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    //Start and end indices for this submesh
                    int startIndex = sourceSubmeshRanges[submeshIndex].x;
                    int endIndex = startIndex + sourceSubmeshRanges[submeshIndex].y;

                    for (ushort tileIndex = 0; tileIndex < tileCount; tileIndex++)
                    {
                        ushort vertexOffset = (ushort)(sourceVertexCount * tileIndex);

                        for (int j = startIndex; j < endIndex; j++)
                        {
                            ushort sourceIndex = sourceTriangles[j];
                            ushort newIndex = (ushort)(sourceIndex + vertexOffset);

                            indices[triangleIndex++] = newIndex;
                        }
                    }
                }
            }

            if (createCollider)
            {
                triangleIndex = 0;
                for (int tileIndex = 0; tileIndex < tileCount; tileIndex++)
                {
                    int vertexOffset = sourceColliderVertexCount * tileIndex;

                    for (int j = 0; j < sourceColliderTriangles.Length; j++)
                    {
                        ushort sourceIndex = sourceColliderTriangles[j];
                        ushort newIndex = (ushort)(sourceIndex + vertexOffset);

                        colliderIndices[triangleIndex++] = newIndex;
                    }
                }
            }
        }
        
        private void ProcessTile(ref int i, ref NativeArray<Vertex> outputVertices, float tileOffset, Structs.BoundsData bounds,
            NativeArray<float3> positions,
            NativeArray<float3> normals,
            NativeArray<float4> tangents,
            NativeArray<float4> colors,
            NativeArray<float2> uvs,
            bool isCollider)
        {
            float3 boundsSize = bounds.size;
            //Faster than constantly using a division. Value represents the length of the mesh.
            float boundsRCPz = 1f / boundsSize.z;
            
            int numVertices = positions.Length;

            float tipGradient;
            
            for (int v = 0; v < numVertices; v++)
            {
                //t-value of vertex over the length of the mesh. Normalized value 0-1
                float localVertPos = ((positions[v].z) - bounds.min.z) * boundsRCPz;
                localVertPos = math.clamp(localVertPos, 0f, 1f);
                
                //Distance (in units) of current vertex along the spline
                float distance = (localVertPos * tileLength) + tileOffset;
                float center = (0.5f * tileLength) + tileOffset;
                
                //Check if Z-value of vertex is changing, meaning sampling moves forward
                var resample = (math.abs(distance - prevZ) > 0f);
                //resample = true; //Test
                if (resample) prevZ = distance;
                
                float t = distance / splineLength;
                t += minT;
                
                //Clamp t to respect both start (curveRange.x) and end (curveRange.y) trimming
                t = math.clamp(t, minT, maxT);

                //Important this sits outside of the resample loop, since it needs to apply to vertices along the width as well
                vertexColor = colorSettings.retainVertexColors ? colors[v] : colorSettings.NativeBaseColor;
                
                //Important optimization. If a mesh has edge loops (vertices sharing the same Z-value) the spline gets unnecessarily re-sampled
                //In this case, all the spline-related information is identical, so doesn't need to be recalculated.
                if (resample)
                {
                    SplineCache.Sample(ref splinePoints, t, out origin, out tangent, out up);
                    //spline.Evaluate(t, out origin, out tangent, out up); //Slow
               
                    if (distributionSettings.knotSnapDistance > 0 && !conformingSettings.enable)
                    {
                        //Nearest knot
                        float knotT = spline.ConvertIndexUnit(t, PathIndexUnit.Normalized, PathIndexUnit.Knot);
                        int knotIndex = (int)math.round(knotT);

                        if (knotIndex > 0 && knotIndex < spline.Knots.Length-1) //Skip first and last
                        {
                            BezierKnot knot = spline.Knots[knotIndex];

                            //var linearKnot = math.lengthsq(knot.TangentOut + knot.TangentIn) < (0.02f * 0.02f);
                            
                            //Linear knot only
                            //if (linearKnot)
                            {
                                float knotDist = math.distance(knot.Position, origin);

                                //Snap to knot
                                if (knotDist < distributionSettings.knotSnapDistance)
                                {
                                    tangent = math.mul(knot.Rotation, math.forward());
                                    up = math.mul(knot.Rotation, math.up());
                                    origin = knot.Position;
                                }
                            }
                        }
                    }

                    forward = math.normalize(tangent);
                    right = math.cross(forward, up);
                    
                    rotation = quaternion.LookRotation(forward, up);
                    
                    if (math.any(rotationSettings.align) && rotationSettings.rollAngle == 0)
                    {
                        rotation = Utilities.LockRotationAngle(quaternion.LookRotation(splineTransformForward, splineTransformUp), rotation, !rotationSettings.align);
                        right = math.rotate(rotation, math.right());
                    }
                    
                    if (rotationSettings.rollAngle != 0f || splineMeshData.Has(SplineCurveMeshData.DataType.Roll))
                    {
                        //Aligned conforming will completely override this rotation, so skip the calculations
                        if ((conformingSettings.enable && math.any(conformingSettings.align)) == false)
                        {
                            float rollInterpolator = rotationSettings.rollMode == CurveMeshSettings.Rotation.RollMode.PerTile ? (center / splineLength) : t;

                            float rollFrequency = rotationSettings.rollFrequency > 0 ? rotationSettings.rollFrequency * (rollInterpolator * splineLength) : 1f;
                            float rollAngle = rotationSettings.rollAngle;

                            float rollValue = rollAngle * rollFrequency;

                            if (splineMeshData.Has(SplineCurveMeshData.DataType.Roll))
                            {
                                rollValue += splineMeshData.roll.Evaluate(spline, t, false);
                            }

                            //Not needed, only a tiny bit of skewing
                            //forward = math.normalize(spline.EvaluateTangent(rollInterpolator));

                            //rotation = Quaternion.AngleAxis(-rollValue, forward) * rotation;
                            rotation = math.mul(quaternion.AxisAngle(forward, -rollValue * Mathf.Deg2Rad), rotation);
                            
                            //Recalculate vectors, particularly for the curve offset functionality later on
                            right = math.mul(rotation, math.right());
                            up = math.mul(rotation, math.up());
                        }
                    }

                    normalRotation = rotation;
                    
                    splineScale = new float3(1f);

                    //t-value is remapped so its normalized again. Represents the 0-1 value over the trimmed curve
                    splineScale *= scaleCurve.Sample(scaleSettings.scaleCurveTrimmed ? (t - minT) / (maxT - minT) : t);
                    
                    if (splineMeshData.Has(SplineCurveMeshData.DataType.Scale))
                    {
                        splineScale *= splineMeshData.scale.Evaluate(spline, t, scaleSettings.interpolation == CurveMeshSettings.InterpolationType.EaseInEaseOut);
                    }
                    
                    if (conformingSettings.enable)
                    {
                        // Map distance to raycastHits array index
                        int raycastCount = raycastHits.Length;
                        float arrayIndexFloat = t * (raycastCount - 1);
                        int currentIndex = (int)math.floor(arrayIndexFloat);
                        currentIndex = math.clamp(currentIndex, 0, raycastCount - 1);

                        int skip = math.max(conformingSettings.skipping, 1);

                        // Calculate skipped index (quantized to multiples of skip)
                        int skippedIndex = (currentIndex / skip) * skip;
                        skippedIndex = math.clamp(skippedIndex, 0, raycastCount - 1);

                        // Calculate blend factor between skip intervals
                        float blendFactor = skip > 1 ? (float)(currentIndex - skippedIndex) / skip : 0f;

                        float strength = 1.0f;
                        if (splineMeshData.Has(SplineCurveMeshData.DataType.Conforming))
                        {
                            strength *= splineMeshData.conforming.Evaluate(spline, t, true);
                        }
                        
                        float conformingFalloff = 1f-Utilities.CalculateDistanceWeight(t * curveLength, curveLength, conformingSettings.startOffset, conformingSettings.startFalloff, 
                            conformingSettings.endOffset, conformingSettings.endFalloff, false);
                        strength *= conformingFalloff;
                        
                        // Get both hits
                        RaycastHit currentHit = raycastHits[currentIndex];
                        RaycastHit skippedHit = raycastHits[skippedIndex];

                        if (currentHit.distance > 0 && skippedHit.distance > 0)
                        {
                            // Blend between skipped and current
                            float3 blendedPoint = math.lerp(skippedHit.point, currentHit.point, blendFactor);
                            float3 blendedNormal = math.normalize(math.lerp(skippedHit.normal, currentHit.normal, blendFactor));

                            float3 direction = conformingSettings.direction == CurveMeshSettings.Conforming.Direction.SplineNormal ? blendedNormal : math.up();
                            blendedPoint += direction * conformingSettings.heightOffset;

                            blendedPoint.x = origin.x;
                            blendedPoint.z = origin.z;
                            //blendedPoint.y += conformingSettings.heightOffset;
                            
                            //Debug.Log($"Index: {currentIndex}, Skipped: {skippedIndex}, Blend: {blendFactor}");

                            //Blend by strength
                            blendedNormal = math.lerp(up, blendedNormal, strength);
                            origin = math.lerp(origin, blendedPoint, strength);
                            
                            quaternion hitRotation = quaternion.LookRotation(tangent, blendedNormal);
                            
                            //Copy normal of surface, to be used for deforming
                            rotation = Utilities.LockRotationAngle(rotation, hitRotation, !conformingSettings.align);
                            
                            if (conformingSettings.blendNormal)
                            {
                                normalRotation = hitRotation;
                            }
                        }
                    }
                    
                    //Pre-calculate rotation matrices once per spline sample point
                    rotationMatrix = new float3x3(rotation);
                    normalRotationMatrix = new float3x3(normalRotation);

                    origin += right * distributionSettings.curveOffset.x;
                    origin += up * distributionSettings.curveOffset.y;
                }
                
                //Track scale to determine average. In turn used to identify extremely thin meshes
                if(isCollider) totalColliderScale += splineScale * scale;
                else totalMeshScale += splineScale * scale;
                
                //Outside of resample loop, since x-position changes every iteration
                if (isCollider == false)
                {
                    float ApplyChannel(NativeSplineData<SplineCurveMesher.VertexColorChannel> channel, NativeSpline targetSpline, float t, float original)
                    {
                        SplineCurveMesher.VertexColorChannel data = channel.Evaluate(targetSpline, t);

                        float value = data.value;
                        if (data.blend) value += original;

                        return value;
                    }
                        
                    //Used to be inside re-sample loop, but needs to work with the procedural gradients.
                    //Point of improvement would be to find a way to move it back as the values are identical across the width/height of the mesh
                    if (splineMeshData.Has(SplineCurveMeshData.DataType.VertexColorRed)) vertexColor.x = ApplyChannel(splineMeshData.red, spline, t, vertexColor.x);
                    if (splineMeshData.Has(SplineCurveMeshData.DataType.VertexColorGreen)) vertexColor.y = ApplyChannel(splineMeshData.green, spline, t, vertexColor.y);
                    if (splineMeshData.Has(SplineCurveMeshData.DataType.VertexColorBlue)) vertexColor.z = ApplyChannel(splineMeshData.blue, spline, t, vertexColor.z);
                    if (splineMeshData.Has(SplineCurveMeshData.DataType.VertexColorAlpha)) vertexColor.w = ApplyChannel(splineMeshData.alpha, spline, t, vertexColor.w);
                    
                    if (colorSettings.tipGradients)
                    {
                        tipGradient = 1f - Utilities.CalculateDistanceWeight(t * curveLength, curveLength + (1f/curveLength),
                            colorSettings.startGradientOffset, colorSettings.startGradientFalloff,
                            colorSettings.endGradientOffset, colorSettings.endGradientFalloff,
                            colorSettings.invertTipGradient);

                        int channel = (int)colorSettings.tipGradientChannel;

                        vertexColor[channel] =
                            BlendVertexColor(vertexColor, channel, tipGradient, colorSettings.tipBlendMode);
                    }

                    if (colorSettings.widthGradients)
                    {
                        float widthScale = colorSettings.widthGradientScaled ? splineScale.x : 1f;
                        float width = boundsSize.x * widthScale;
                        //Width over mesh
                        float x = (positions[v].x - bounds.min.x) * widthScale;

                        float widthGradient = 0;
                        //Run from edge inwards
                        if (colorSettings.widthGradientMirrored)
                        {
                            widthGradient = Utilities.EdgeDistanceMask(x, width, colorSettings.widthGradientOffset, colorSettings.widthGradientFalloff, colorSettings.invertWidthGradient);
                        }
                        //Run from left to right
                        else
                        {
                            widthGradient = Utilities.CalculateDistanceWeight(x, width + (1f/width),
                                colorSettings.widthGradientOffset, colorSettings.widthGradientFalloff,
                                0, 0, colorSettings.invertWidthGradient);
                        }
                        
                        int channel = (int)colorSettings.widthGradientChannel;
                    
                        vertexColor[channel] = BlendVertexColor(vertexColor, channel, widthGradient, colorSettings.widthBlendMode);
                    }
                }
                
                //Transform vertex to spline point and rotation (world-space)
                float3 vertexPosition = origin + math.mul(rotationMatrix, (positions[v] + new float3(0f, 0f, distributionSettings.spacing)) * scale * splineScale);

                float3 vertexWorldPosition = vertexPosition;
                
                //Make that the local-space position of the mesh filter
                vertexPosition = math.mul(rendererWorldToLocal, new float4(vertexPosition, 1.0f)).xyz;
                vertexPosition -= positionOffset;
                
                //Extend bounds as it expands in local-space
                currentBoundsMin = math.min(vertexPosition, currentBoundsMin);
                currentBoundsMax = math.max(vertexPosition, currentBoundsMax);
                
                float3 vertexNormal = splineTransformUp;
                float4 vertexTangent = new float4(1, 0, 0, 1f);
                
                float4 uv = 0;
                if (isCollider == false)
                {
                    uv.xy = uvs[v].xy;

                    if (uvSettings.source != CurveMeshSettings.UV.Source.InputMesh)
                    {
                        switch (uvSettings.source)
                        {
                            case CurveMeshSettings.UV.Source.WorldSpaceXY: uv.xy = vertexWorldPosition.xy; break;
                            case CurveMeshSettings.UV.Source.WorldSpaceXZ: uv.xy = vertexWorldPosition.xz; break;
                            case CurveMeshSettings.UV.Source.WorldSpaceYZ: uv.xy = vertexWorldPosition.zy; break;
                        }
                    }

                    if (uvSettings.rotate) (uv.x, uv.y) = (uv.y, uv.x);

                    if (uvSettings.source == CurveMeshSettings.UV.Source.InputMesh)
                    {
                        uv.x = math.select(uv.x, -t * curveLength, uvSettings.StretchX);
                        uv.y = math.select(uv.y, -t * curveLength, uvSettings.StretchY);
                    }

                    uv.xy = (uv.xy * uvSettings.scale) + uvSettings.offset.xy;
                    
                    uv.x = math.select(uv.x, 1-uv.x, uvSettings.FlipX);
                    uv.y = math.select(uv.y, 1-uv.y, uvSettings.FlipY);
                        
                    if (outputSettings.storeGradientsInUV)
                    {
                        //Normalized Distance
                        uv.z = t;
                        //Normalized Height
                        float verticalT = (positions[v].y - bounds.min.y) / boundsSize.y;
                        uv.w = (float)math.abs(1f-verticalT * splineScale.y);
                    }
                }
                
                if (isCollider == false)
                {
                    //Also transform the normal
                    vertexNormal = math.mul(normalRotationMatrix, normals[v]); //To spline world-space
                    vertexNormal = math.normalize(math.mul(normalTransform, vertexNormal)); //Mesh filter local space
                    
                    //Correct tangent orientation requires for normal mapping
                    float4 sourceTangent = new float4(tangents[v]);
                    if (uvSettings.FlipX) sourceTangent.w *= -1;
                    
                    float3 transformedTangent = math.mul(normalRotationMatrix, sourceTangent.xyz);
                    transformedTangent = math.normalize(math.mul(normalTransform, transformedTangent));
                    vertexTangent = new float4(transformedTangent, sourceTangent.w);
                }
                
                outputVertices[i++] = new Vertex
                (
                    vertexPosition,
                    vertexNormal,
                    vertexTangent,
                    vertexColor,
                    uv
                );
            }
        }

        private float BlendVertexColor(float4 color, int channel, float value,
            CurveMeshSettings.Color.BlendMode blendMode)
        {
            float channelValue = color[channel];
            
            if(blendMode == CurveMeshSettings.Color.BlendMode.Min) channelValue = math.min(channelValue, value);
            else if (blendMode == CurveMeshSettings.Color.BlendMode.Max) channelValue = math.max(channelValue, value);
            else if (blendMode == CurveMeshSettings.Color.BlendMode.Add) channelValue = math.saturate(channelValue + value);

            return channelValue;
        }

        public void Dispose()
        {
            //Input is disposed with ProcessInput job(s). Arrays are used across multiple jobs, so aren't disposed in this one.
            
            //Output mesh
            vertices.Dispose();
            indices.Dispose();

            //Output collider
            colliderVertices.Dispose();
            colliderIndices.Dispose();
            
            metaData.Dispose();
            
            scaleCurve.Dispose();
        }
    }
}