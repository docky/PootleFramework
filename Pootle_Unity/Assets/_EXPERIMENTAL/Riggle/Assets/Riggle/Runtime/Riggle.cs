using System;
using System.Collections.Generic;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>
    /// Editor-authored landmark rig for simple upright, two-legged characters.
    /// The component is only data at runtime; baking is performed by its custom editor.
    /// Local axes: +Y up, +Z facing forward, -X character's left.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Riggle : MonoBehaviour
    {
        public enum Point
        {
            Pelvis = 0,
            Neck = 1,
            HeadTop = 2,
            LeftShoulder = 3,
            LeftElbow = 4,
            LeftWrist = 5,
            LeftKnee = 6,
            LeftAnkle = 7,
            RightShoulder = 8,
            RightElbow = 9,
            RightWrist = 10,
            RightKnee = 11,
            RightAnkle = 12
        }

        public const int PointCount = 13;

        [Serializable]
        public sealed class BakedSource
        {
            public MeshRenderer source;
            public bool wasEnabled;
            public SkinnedMeshRenderer baked;
        }

        [Header("Landmarks")]
        [Tooltip("Right-hand landmarks are reflected across the pelvis X position. Turn this off for asymmetrical characters.")]
        [SerializeField] private bool mirrorRight = true;
        [SerializeField, HideInInspector] private Vector3[] landmarks = new Vector3[0];

        [Header("Skeleton proportions")]
        [SerializeField, Tooltip("Adds three weighted twist helpers to each upper/lower arm and leg. Rebake after changing.")]
        private bool distributeTwist = true;
        [Tooltip("Approximate hip width relative to shoulder width. This is only an initial guess for the hip joints.")]
        [SerializeField, Range(0.2f, 1.2f)] private float hipToShoulderWidth = 0.62f;
        [Tooltip("Fraction of neck-to-crown distance to place the head joint (not the tip of the head).")]
        [SerializeField, Range(0.1f, 0.8f)] private float headJointHeight = 0.3f;
        [Tooltip("Feet point along the local +Z axis. This scales the estimated toe reach.")]
        [SerializeField, Range(0.03f, 0.25f)] private float footLengthHeightRatio = 0.095f;

        public enum WeightingMethod { Envelopes, ContiguousSurface }
        [Header("Auto skinning")]
        [Tooltip("Contiguous Surface propagates bone influence along welded triangle edges instead of across empty space. It is a surface geodesic method, not a voxel/volume solver.")]
        [SerializeField] private WeightingMethod weighting = WeightingMethod.ContiguousSurface;
        [Tooltip("Falloff exponent of distance-to-bone envelopes. Higher values produce sharper separations.")]
        [SerializeField, Range(1.5f, 8f)] private float weightFalloff = 4.5f;
        [Tooltip("A small offset that controls blending at bone joints. Increase for more flexible rubbery joints.")]
        [SerializeField, Range(0.12f, 1.2f)] private float jointSoftness = 0.38f;
        [Tooltip("Number of triangle-neighbour smoothing steps. Does not propagate between disconnected mesh islands.")]
        [SerializeField, Range(0, 8)] private int surfaceSmoothSteps = 2;
        [SerializeField, Range(0f, 0.8f)] private float surfaceSmoothAmount = 0.35f;
        [Tooltip("Envelopes are scaled by these fractions of overall character height.")]
        [SerializeField, Range(0.05f, 0.4f)] private float trunkRadius = 0.14f;
        [SerializeField, Range(0.02f, 0.24f)] private float armRadius = 0.072f;
        [SerializeField, Range(0.03f, 0.3f)] private float legRadius = 0.095f;
        [SerializeField, Range(0.05f, 0.4f)] private float headRadius = 0.15f;

        public enum AssemblyMode { SmoothSkin, NamedRigidParts, NamedBodyParts }
        [Header("Assembly")]
        [SerializeField, Tooltip("Named Rigid Parts uses one bone per part. Named Body Parts lets chunks bend within their own anatomical chain only. Marked accessories are parented instead.")]
        private AssemblyMode assemblyMode;
        [SerializeField, Tooltip("In Named Body Parts, keep each arm/leg/ear/tail/wing chunk as its own skinned renderer for later replacement. Other chunks still merge unless their Riggle Body Part component requests separation.")]
        private bool separateLimbMeshes;
        [SerializeField, Tooltip("Marker for an accessory that stays separate, e.g. Head^ Hat. May be changed; leave empty to disable attachments.")]
        private string attachmentSymbol = "^";
        [Serializable] public sealed class AttachedObject
        {
            public Transform source, originalParent;
            public int siblingIndex;
            public Vector3 localPosition, localScale;
            public Quaternion localRotation;
            public string boneName;
        }
        [SerializeField, HideInInspector] private List<AttachedObject> attachedObjects = new List<AttachedObject>();
        public AssemblyMode Assembly => assemblyMode;
        public bool SeparateLimbMeshes => separateLimbMeshes;
        public string AttachmentSymbol => attachmentSymbol;
        public List<AttachedObject> AttachedObjects => attachedObjects;

        [Header("Bake")]
        [Tooltip("Optional MeshFilters to bind. Empty = collect all ordinary MeshFilters in this character's children.")]
        [SerializeField] private MeshFilter[] sourceMeshes = new MeshFilter[0];
        [Tooltip("Folder for saved copies of skinned meshes and any generated Avatar.")]
        [SerializeField] private string assetFolder = "Assets/RiggleGenerated";
        [Tooltip("Try to create a Unity Mecanim Humanoid Avatar. A proper T-pose and anatomical layout are needed. Generic bones still work if it fails.")]
        [SerializeField] private bool tryHumanoid = false;

        [SerializeField, HideInInspector] private Transform generatedBones;
        [SerializeField, HideInInspector] private List<BakedSource> bakedSources = new List<BakedSource>();
        [SerializeField, HideInInspector] private Avatar bakedAvatar;

        public bool MirrorRight { get { return mirrorRight; } set { mirrorRight = value; } }
        public float HipToShoulderWidth { get { return hipToShoulderWidth; } }
        public float HeadJointHeight { get { return headJointHeight; } }
        public float FootLengthHeightRatio { get { return footLengthHeightRatio; } }
        public float WeightFalloff { get { return weightFalloff; } }
        public WeightingMethod Weighting { get { return weighting; } }
        public float JointSoftness { get { return jointSoftness; } }
        public int SurfaceSmoothSteps { get { return surfaceSmoothSteps; } }
        public float SurfaceSmoothAmount { get { return surfaceSmoothAmount; } }
        public float TrunkRadius { get { return trunkRadius; } }
        public float ArmRadius { get { return armRadius; } }
        public float LegRadius { get { return legRadius; } }
        public float HeadRadius { get { return headRadius; } }
        public MeshFilter[] SourceMeshes { get { return sourceMeshes; } }
        public string AssetFolder { get { return assetFolder; } }
        public bool TryHumanoid { get { return tryHumanoid; } }
        public bool DistributeTwist => distributeTwist;
        public Transform GeneratedBones { get { return generatedBones; } set { generatedBones = value; } }
        public List<BakedSource> BakedSources { get { return bakedSources; } }
        public Avatar BakedAvatar { get { return bakedAvatar; } set { bakedAvatar = value; } }

        public static bool IsRightPoint(Point point) { return (int)point >= (int)Point.RightShoulder; }
        public static Point ToLeftPoint(Point rightPoint)
        {
            return (Point)((int)rightPoint - (int)Point.RightShoulder + (int)Point.LeftShoulder);
        }

        public Vector3 GetLocal(Point point)
        {
            EnsurePoints();
            var refs = GetComponent<RiggleReferencePoints>();
            if (refs && refs.TryGetLocal(this, point, out var referenced)) return referenced;
            int opposite = RiggleReferencePoints.Opposite((int)point);
            if (refs && refs.useReferences && !refs.inferMissingSide && opposite >= 0 && refs.Assigned((Point)opposite)) return landmarks[(int)point];
            if (mirrorRight && IsRightPoint(point))
            {
                var left = landmarks[(int)ToLeftPoint(point)];
                var pelvis = landmarks[(int)Point.Pelvis];
                return new Vector3(2f * pelvis.x - left.x, left.y, left.z);
            }
            return landmarks[(int)point];
        }

        public Vector3 GetWorld(Point point) { return transform.TransformPoint(GetLocal(point)); }

        /// <summary>Store the current mirrored positions so unlinking does not jump back to old values.</summary>
        public void CaptureMirroredRightSide()
        {
            EnsurePoints();
            Vector3 pelvis = landmarks[(int)Point.Pelvis];
            for (int i = (int)Point.RightShoulder; i < PointCount; i++)
            {
                Vector3 left = landmarks[i - (int)Point.RightShoulder + (int)Point.LeftShoulder];
                landmarks[i] = new Vector3(2f * pelvis.x - left.x, left.y, left.z);
            }
        }

        /// <summary>In mirror mode, editing the right-side marker edits the opposite left-side marker.</summary>
        public void SetLocal(Point point, Vector3 localPosition)
        {
            EnsurePoints();
            var refs = GetComponent<RiggleReferencePoints>();
            var driven = refs ? refs.Driven(point) : null;
            if (driven)
            {
                if (!refs.Assigned(point)) localPosition.x = 2 * GetLocal(Point.Pelvis).x - localPosition.x;
                if (driven.TryGetComponent<RiggleOriginReference>(out var origin))
                    origin.localOffset = driven.InverseTransformPoint(transform.TransformPoint(localPosition));
                else driven.position = transform.TransformPoint(localPosition);
                return;
            }
            int opposite = RiggleReferencePoints.Opposite((int)point);
            bool manualMissing = refs && refs.useReferences && !refs.inferMissingSide && opposite >= 0 && refs.Assigned((Point)opposite);
            if (mirrorRight && IsRightPoint(point) && !manualMissing)
            {
                var pelvisX = GetLocal(Point.Pelvis).x;
                landmarks[(int)ToLeftPoint(point)] = new Vector3(2f * pelvisX - localPosition.x, localPosition.y, localPosition.z);
            }
            else
            {
                landmarks[(int)point] = localPosition;
            }
        }

        public void EnsurePoints()
        {
            if (landmarks != null && landmarks.Length == PointCount) return;
            landmarks = new Vector3[PointCount];
            SetFallbackPoints();
        }

        public void SetFallbackPoints()
        {
            if (landmarks == null || landmarks.Length != PointCount) landmarks = new Vector3[PointCount];
            landmarks[(int)Point.Pelvis] = new Vector3(0, 0.85f, 0);
            landmarks[(int)Point.Neck] = new Vector3(0, 1.45f, 0);
            landmarks[(int)Point.HeadTop] = new Vector3(0, 1.85f, 0);
            landmarks[(int)Point.LeftShoulder] = new Vector3(-0.25f, 1.34f, 0);
            landmarks[(int)Point.LeftElbow] = new Vector3(-0.55f, 1.32f, 0);
            landmarks[(int)Point.LeftWrist] = new Vector3(-0.88f, 1.3f, 0);
            landmarks[(int)Point.LeftKnee] = new Vector3(-0.14f, 0.45f, 0);
            landmarks[(int)Point.LeftAnkle] = new Vector3(-0.14f, 0.07f, 0);
            for (int i = (int)Point.RightShoulder; i < PointCount; i++)
            {
                var left = landmarks[i - (int)Point.RightShoulder + (int)Point.LeftShoulder];
                landmarks[i] = new Vector3(-left.x, left.y, left.z);
            }
        }

        /// <summary>One-click initial guess. Requires adjusting actual joints before baking.</summary>
        public bool FitPointsToMeshBounds()
        {
            Bounds b;
            if (!TryGetSourceBounds(out b)) return false;
            if (landmarks == null || landmarks.Length != PointCount) landmarks = new Vector3[PointCount];

            float h = Mathf.Max(0.001f, b.size.y);
            float w = Mathf.Max(0.001f, b.size.x);
            float cx = b.center.x;
            float cz = b.center.z;
            float bottom = b.min.y;
            landmarks[(int)Point.Pelvis] = new Vector3(cx, bottom + h * 0.51f, cz);
            landmarks[(int)Point.Neck] = new Vector3(cx, bottom + h * 0.79f, cz);
            landmarks[(int)Point.HeadTop] = new Vector3(cx, bottom + h * 0.985f, cz);
            landmarks[(int)Point.LeftShoulder] = new Vector3(cx - w * 0.17f, bottom + h * 0.74f, cz);
            landmarks[(int)Point.LeftElbow] = new Vector3(cx - w * 0.34f, bottom + h * 0.72f, cz);
            landmarks[(int)Point.LeftWrist] = new Vector3(b.min.x + w * 0.025f, bottom + h * 0.70f, cz);
            landmarks[(int)Point.LeftKnee] = new Vector3(cx - w * 0.105f, bottom + h * 0.29f, cz);
            landmarks[(int)Point.LeftAnkle] = new Vector3(cx - w * 0.105f, bottom + h * 0.055f, cz);
            for (int i = (int)Point.RightShoulder; i < PointCount; i++)
            {
                var left = landmarks[i - (int)Point.RightShoulder + (int)Point.LeftShoulder];
                landmarks[i] = new Vector3(2f * cx - left.x, left.y, left.z);
            }
            var refs = GetComponent<RiggleReferencePoints>();
            if (refs && refs.useReferences)
                for (int i = 0; i < PointCount; i++)
                {
                    var marker = refs.Assigned((Point)i);
                    if (marker)
                    {
                        if (marker.TryGetComponent<RiggleOriginReference>(out var origin))
                            origin.localOffset = marker.InverseTransformPoint(transform.TransformPoint(landmarks[i]));
                        else marker.position = transform.TransformPoint(landmarks[i]);
                    }
                }
            return true;
        }

        /// <summary>Collect visible character meshes, excluding any generated meshes and skinned-only objects.</summary>
        public List<MeshFilter> CollectSourceMeshes()
        {
            var result = new List<MeshFilter>();
            var added = new HashSet<MeshFilter>();
            if (sourceMeshes != null && sourceMeshes.Length > 0)
            {
                foreach (var filter in sourceMeshes)
                {
                    if (IsUsableSource(filter) && added.Add(filter)) result.Add(filter);
                }
                return result;
            }

            foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
            {
                if (IsUsableSource(filter) && added.Add(filter)) result.Add(filter);
            }
            return result;
        }

        private bool IsUsableSource(MeshFilter filter)
        {
            return filter != null && filter.sharedMesh != null &&
                   (filter.transform == transform || filter.transform.IsChildOf(transform)) &&
                   filter.GetComponent<MeshRenderer>() != null &&
                   filter.GetComponentInParent<RiggleImportedSource>() == null &&
                   (generatedBones == null || !filter.transform.IsChildOf(generatedBones)) &&
                   filter.GetComponent<SkinnedMeshRenderer>() == null;
        }

        public bool TryGetSourceBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            foreach (var filter in CollectSourceMeshes())
            {
                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int xi = 0; xi < 2; xi++)
                for (int yi = 0; yi < 2; yi++)
                for (int zi = 0; zi < 2; zi++)
                {
                    Vector3 corner = new Vector3(
                        xi == 0 ? meshBounds.min.x : meshBounds.max.x,
                        yi == 0 ? meshBounds.min.y : meshBounds.max.y,
                        zi == 0 ? meshBounds.min.z : meshBounds.max.z);
                    Vector3 local = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                    if (!found) { bounds = new Bounds(local, Vector3.zero); found = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return found;
        }

        private void Reset()
        {
            if (!FitPointsToMeshBounds()) SetFallbackPoints();
        }
    }
}
