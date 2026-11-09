using System;
using System.Collections.Generic;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Generic landmark skeleton authoring. Runtime data only; external animation owns the bones.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Riggle/Riggle Generic")]
    public sealed class RiggleGeneric : MonoBehaviour
    {
        [Serializable]
        public sealed class Joint
        {
            public string name = "Joint";
            [Tooltip("Parent joint name; empty only for the root.")] public string parent;
            public Vector3 position;
            [Tooltip("Envelope endpoint in character local space. Used for terminal bones.")] public Vector3 tip;
            [Tooltip("Child joint used as envelope endpoint; empty uses Tip.")] public string endJoint;
            [Min(.001f), Tooltip("Envelope radius in character local units.")] public float radius = .1f;
        }

        public enum Side { Undefined, Left, Right }
        [Serializable]
        public sealed class Leg
        {
            public string name = "Leg";
            public string upper, middle, foot;
            [Tooltip("Ordered joints between Middle and Foot. Empty retains the two-segment leg.")]
            public List<string> additionalJoints = new List<string>();
            public List<string> Chain()
            {
                var chain=new List<string>{upper,middle};
                if(additionalJoints!=null)chain.AddRange(additionalJoints);
                chain.Add(foot);return chain;
            }
            public Side side;
            [Tooltip("Paired leg name, or empty for unpaired limbs.")] public string opposite;
        }

        [Header("Skeleton (+Y up, +Z forward)")]
        public List<Joint> joints = new List<Joint>();
        public string hips = "Hips";
        public List<Leg> legs = new List<Leg>();
        [Range(2,8),Tooltip("Used by Rebuild leg segments. Fit the new joint handles, then rebake.")]
        public int legSegments = 2;
        [Header("Skinning")]
        public Riggle.WeightingMethod weighting = Riggle.WeightingMethod.ContiguousSurface;
        [Range(1.5f, 8)] public float falloff = 4.5f;
        [Range(.12f, 1.2f)] public float jointSoftness = .38f;
        [Range(0, 8)] public int smoothSteps = 2;
        [Range(0, .8f)] public float smoothAmount = .35f;
        [Tooltip("Empty collects ordinary MeshFilters under this character. Existing skinned rigs are not rebound.")]
        public MeshFilter[] sourceMeshes = Array.Empty<MeshFilter>();
        public string assetFolder = "Assets/RiggleGenerated/Generic";
        [HideInInspector] public Transform generatedBones;
        [HideInInspector] public List<Riggle.BakedSource> bakedSources = new List<Riggle.BakedSource>();
        [HideInInspector] public Component legsAnimator;

        public List<MeshFilter> CollectSources()
        {
            var result = new List<MeshFilter>();
            foreach (var filter in sourceMeshes != null && sourceMeshes.Length > 0
                         ? sourceMeshes : GetComponentsInChildren<MeshFilter>(true))
                if (filter && filter.sharedMesh && filter.GetComponent<MeshRenderer>() &&
                    !filter.GetComponent<SkinnedMeshRenderer>() &&
                    (filter.transform == transform || filter.transform.IsChildOf(transform)) &&
                    (!generatedBones || !filter.transform.IsChildOf(generatedBones)) &&
                    !filter.GetComponentInParent<RiggleImportedSource>() && !result.Contains(filter)) result.Add(filter);
            return result;
        }

        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = new Bounds();
            bool found = false;
            foreach (var source in CollectSources())
            {
                var b = source.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? b.min.x : b.max.x,
                        (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                    var p = transform.InverseTransformPoint(source.transform.TransformPoint(corner));
                    if (!found) { bounds = new Bounds(p, Vector3.zero); found = true; }
                    else bounds.Encapsulate(p);
                }
            }
            return found;
        }

        public void SetQuadrupedPreset(Bounds bounds)
        {
            joints.Clear(); legs.Clear(); hips = "Hips";
            Vector3 P(float x, float y, float z) => bounds.min + Vector3.Scale(bounds.size, new Vector3(x, y, z));
            float radius = Mathf.Max(.001f, Mathf.Min(bounds.size.x, bounds.size.y) * .15f);
            void Add(string name, string parent, Vector3 p, string end, Vector3 tip, float r)
                => joints.Add(new Joint { name = name, parent = parent, position = p, endJoint = end, tip = tip, radius = r });
            Add("Hips", "", P(.5f,.68f,.25f), "Spine", Vector3.zero, radius * 2);
            Add("Spine", "Hips", P(.5f,.7f,.45f), "Chest", Vector3.zero, radius * 2);
            Add("Chest", "Spine", P(.5f,.72f,.65f), "Neck", Vector3.zero, radius * 2);
            Add("Neck", "Chest", P(.5f,.8f,.78f), "Head", Vector3.zero, radius);
            Add("Head", "Neck", P(.5f,.88f,.85f), "", P(.5f,.88f,.98f), radius * 1.5f);
            Add("Tail", "Hips", P(.5f,.7f,.2f), "TailTip", Vector3.zero, radius * .6f);
            Add("TailTip", "Tail", P(.5f,.75f,.08f), "", P(.5f,.78f,0), radius * .4f);
            for (int pair = 0; pair < 2; pair++)
            for (int side = 0; side < 2; side++)
            {
                bool front = pair == 0;
                string prefix = (front ? "Front" : "Hind") + (side == 0 ? "Left" : "Right");
                float x = side == 0 ? .22f : .78f, z = front ? .65f : .25f;
                Add(prefix + "Upper", front ? "Chest" : "Hips", P(x,.65f,z), prefix + "Lower", Vector3.zero, radius);
                Add(prefix + "Lower", prefix + "Upper", P(x,.35f,z + (front ? -.06f : .08f)), prefix + "Foot", Vector3.zero, radius * .8f);
                Add(prefix + "Foot", prefix + "Lower", P(x,.06f,z), "", P(x,.04f,z + .1f), radius);
                legs.Add(new Leg { name = prefix, upper = prefix + "Upper", middle = prefix + "Lower", foot = prefix + "Foot",
                    side = side == 0 ? Side.Left : Side.Right,
                    opposite = (front ? "Front" : "Hind") + (side == 0 ? "Right" : "Left") });
            }
        }

        private void Reset()
        {
            if (!TryGetBounds(out var bounds)) bounds = new Bounds(new Vector3(0,.5f,0), new Vector3(.8f,1,1.8f));
            SetQuadrupedPreset(bounds);
        }

        public void RebuildLegSegments()
        {
            int count=Mathf.Clamp(legSegments,2,8);
            // Validate every leg before changing any definition, so a bad later leg cannot partially rebuild the rig.
            var planned=new HashSet<string>();
            foreach(var leg in legs)
            {
                if(leg==null)throw new InvalidOperationException("Remove empty leg definitions first.");
                var names=leg.Chain();var removed=leg.additionalJoints??new List<string>();
                if(names.Exists(n=>joints.Find(j=>j.name==n)==null))throw new InvalidOperationException(leg.name+": fix missing joint names first.");
                if(joints.Exists(j=>removed.Contains(j.parent)&&!names.Contains(j.name)))throw new InvalidOperationException("Move accessory branches off extra leg joints before rebuilding segments.");
                for(int i=2;i<count;i++)
                {
                    string name=leg.name+"Joint"+(i+1);
                    if(!planned.Add(name)||joints.Exists(j=>j.name==name&&!removed.Contains(name)))throw new InvalidOperationException("Joint name already exists: "+name);
                }
            }
            foreach(var leg in legs)
            {
                var chain=leg.Chain().ConvertAll(n=>joints.Find(j=>j.name==n));
                if(chain.Exists(j=>j==null))throw new InvalidOperationException(leg.name+": fix missing joint names first.");
                var points=chain.ConvertAll(j=>j.position);float total=0;
                for(int i=1;i<points.Count;i++)total+=Vector3.Distance(points[i-1],points[i]);
                Vector3 At(float fraction)
                {
                    float distance=total*fraction;
                    for(int i=1;i<points.Count;i++){float length=Vector3.Distance(points[i-1],points[i]);if(distance<=length)return Vector3.Lerp(points[i-1],points[i],length>0?distance/length:0);distance-=length;}
                    return points[points.Count-1];
                }
                var upper=chain[0];var middle=chain[1];var foot=chain[chain.Count-1];
                var removed=leg.additionalJoints??new List<string>();
                if(joints.Exists(j=>removed.Contains(j.parent)&&!chain.Contains(j)))throw new InvalidOperationException("Move accessory branches off extra leg joints before rebuilding segments.");
                joints.RemoveAll(j=>removed.Contains(j.name));leg.additionalJoints=new List<string>();
                middle.position=At(1f/count);middle.parent=upper.name;upper.endJoint=middle.name;
                var previous=middle;
                for(int i=2;i<count;i++)
                {
                    string name=leg.name+"Joint"+(i+1);
                    if(joints.Exists(j=>j.name==name))throw new InvalidOperationException("Joint name already exists: "+name);
                    var joint=new Joint{name=name,parent=previous.name,position=At(i/(float)count),radius=middle.radius};
                    previous.endJoint=name;joints.Add(joint);leg.additionalJoints.Add(name);previous=joint;
                }
                previous.endJoint=foot.name;foot.parent=previous.name;
            }
        }
    }
}
