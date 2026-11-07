using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Editor
{
    internal static class RiggleDeformationTools
    {
        public const string Prefix = "RiggleCorrective_";
        public static string Fingerprint(Mesh mesh)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (var v in mesh.vertices)
                { h = (h ^ (uint)v.x.GetHashCode()) * 16777619; h = (h ^ (uint)v.y.GetHashCode()) * 16777619; h = (h ^ (uint)v.z.GetHashCode()) * 16777619; }
                foreach (var i in mesh.triangles) h = (h ^ (uint)i) * 16777619;
                return mesh.vertexCount + ":" + h.ToString("x8");
            }
        }
        public static void Validate(Mesh source, RiggleMeshEdits edits, IEnumerable<string> bones = null)
        {
            if (!edits || (edits.weights.Count == 0 && edits.correctives.Count == 0)) return;
            if (edits.sourceMesh != source || edits.topology != Fingerprint(source))
                throw new InvalidOperationException("Source topology changed on " + edits.name + ". Clear or restore its deformation edits before rebaking; vertex IDs must still match.");
            var names = bones == null ? null : new HashSet<string>(bones);
            foreach (var w in edits.weights)
            {
                if (w.vertex < 0 || w.vertex >= source.vertexCount || w.bones == null || w.weights == null || w.bones.Length != w.weights.Length || w.bones.Length > 4)
                    throw new InvalidOperationException("Invalid saved weight correction on " + edits.name);
                if (names != null && w.bones.Any(n => !names.Contains(n)))
                    throw new InvalidOperationException("A painted bone is missing on " + edits.name + ". Keep twist helpers enabled or clear those weight edits first.");
            }
            foreach (var c in edits.correctives)
                if ((names != null && !names.Contains(c.bone)) || c.deltas.Any(d => d.vertex < 0 || d.vertex >= source.vertexCount))
                    throw new InvalidOperationException("Corrective references missing bones or vertices on " + edits.name);
        }
        public static RiggleMeshEdits GetEdits(Riggle.BakedSource source)
        {
            var filter = source.source.GetComponent<MeshFilter>();
            var edits = filter.GetComponent<RiggleMeshEdits>() ?? Undo.AddComponent<RiggleMeshEdits>(filter.gameObject);
            Validate(filter.sharedMesh, edits);
            Undo.RecordObject(edits, "Edit Riggle deformation");
            edits.sourceMesh = filter.sharedMesh; edits.topology = Fingerprint(filter.sharedMesh);
            return edits;
        }
        public static void ApplyWeights(Mesh source, Mesh output, string[] bones, RiggleMeshEdits edits)
        {
            Validate(source, edits, bones);
            if (!edits || edits.weights.Count == 0) return;
            var weights = output.boneWeights;
            foreach (var edit in edits.weights)
            {
                var values = new Dictionary<int,float>();
                for (int i=0;i<edit.bones.Length;i++) values[Array.IndexOf(bones,edit.bones[i])] = edit.weights[i];
                weights[edit.vertex] = Pack(values);
            }
            output.boneWeights = weights;
        }
        static Dictionary<int,float> Unpack(BoneWeight w)
        {
            var result = new Dictionary<int,float>();
            void Add(int b,float value) { if(value>0) result[b] = result.TryGetValue(b,out var old)?old+value:value; }
            Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
            return result;
        }
        static BoneWeight Pack(Dictionary<int,float> values)
        {
            var top=values.Where(v=>v.Key>=0 && v.Value>0 && !float.IsNaN(v.Value) && !float.IsInfinity(v.Value)).OrderByDescending(v=>v.Value).ThenBy(v=>v.Key).Take(4).ToArray();
            float sum=top.Sum(v=>v.Value); if(sum<=0) throw new InvalidOperationException("A vertex must retain at least one bone influence.");
            var b=new int[4];var w=new float[4];for(int i=0;i<top.Length;i++){b[i]=top[i].Key;w[i]=top[i].Value/sum;}
            return new BoneWeight{boneIndex0=b[0],boneIndex1=b[1],boneIndex2=b[2],boneIndex3=b[3],weight0=w[0],weight1=w[1],weight2=w[2],weight3=w[3]};
        }
        public static float Weight(BoneWeight w,int bone) => (w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)+(w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
        public static int PaintWeights(Riggle.BakedSource source, int bone, Vector3[] worldVertices, Vector3 center, float radius, float strength, float target, int seed)
        {
            var edits=GetEdits(source); var mesh=source.baked.sharedMesh; var topology=new RiggleTopology(edits.sourceMesh);
            var weights=mesh.boneWeights; var saved=edits.weights.ToDictionary(e=>e.vertex);
            Undo.RegisterCompleteObjectUndo(mesh,"Paint skin weights"); int count=0;
            for(int v=0;v<weights.Length;v++)
            {
                if(topology.welded[v]!=v || (seed>=0 && topology.islands[v]!=topology.islands[seed]))continue;
                float d=Vector3.Distance(worldVertices[v],center); if(d>=radius)continue;
                float falloff=Mathf.Pow(1-d/radius,2); var values=Unpack(weights[v]);
                float desired=Mathf.Lerp(Weight(weights[v],bone),Mathf.Clamp01(target),Mathf.Clamp01(strength)*falloff);
                values.Remove(bone); var others=values.OrderByDescending(p=>p.Value).Take(3).ToDictionary(p=>p.Key,p=>p.Value);
                if(others.Count==0 && desired<1)
                { int parent=Array.IndexOf(source.baked.bones,source.baked.bones[bone].parent);if(parent<0 || parent==bone)parent=bone==0?1:0;others[parent]=1; }
                float sum=others.Values.Sum();var normalized=others.ToDictionary(p=>p.Key,p=>sum>0?p.Value/sum*(1-desired):0);
                normalized[bone]=desired;var value=Pack(normalized);
                for(int alias=0;alias<weights.Length;alias++) if(topology.welded[alias]==v)
                {
                    weights[alias]=value;var packed=Unpack(value);
                    saved[alias]=new RiggleMeshEdits.WeightEdit{vertex=alias,bones=packed.Keys.Select(i=>source.baked.bones[i].name).ToArray(),weights=packed.Values.ToArray()}; count++;
                }
            }
            edits.weights=saved.Values.OrderBy(e=>e.vertex).ToList();mesh.boneWeights=weights;
            EditorUtility.SetDirty(mesh);EditorUtility.SetDirty(edits);return count;
        }
        public static void AddStarters(Riggle rig)
        {
            var animator=rig.GetComponent<Animator>();
            if(!animator || !animator.avatar || !animator.avatar.isHuman)throw new InvalidOperationException("Bake a valid Humanoid first.");
            var joints=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.RightUpperLeg};
            string[] labels={"LeftShoulder","RightShoulder","LeftHip","RightHip"};
            float height=Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.Head).position,animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
            foreach(var source in rig.BakedSources)
            {
                var edits=GetEdits(source);var vertices=edits.sourceMesh.vertices;var skin=source.baked;
                for(int j=0;j<4;j++)
                {
                    string name=Prefix+labels[j];if(edits.correctives.Any(c=>c.name==name))continue;
                    var bone=animator.GetBoneTransform(joints[j]); bool arm=j<2;
                    var direction=arm?(j==0?Vector3.left:Vector3.right):Vector3.down;
                    var c=new RiggleMeshEdits.Corrective{name=name,bone=bone.name,axis=arm?(j==0?Vector3.back:Vector3.forward):Vector3.left,restDirection=direction,restRotation=bone.localRotation};
                    Vector3 limbAxis=bone.TransformDirection(direction);float radius=height*(arm?.17f:.20f);
                    var topology=new RiggleTopology(edits.sourceMesh);var deltas=new Dictionary<int,Vector3>();
                    for(int v=0;v<vertices.Length;v++)
                    {
                        if(topology.welded[v]!=v)continue;
                        Vector3 world=skin.transform.TransformPoint(vertices[v]);Vector3 relative=world-bone.position;
                        float distance=relative.magnitude;if(distance>=radius)continue;
                        // Anatomical masks exclude nearby head, opposite limbs and accessories.
                        var w=skin.sharedMesh.boneWeights[v];float influence=0;
                        foreach(var pair in Unpack(w))
                            if(skin.bones[pair.Key]==bone || skin.bones[pair.Key].name.StartsWith(bone.name+"_Twist",StringComparison.Ordinal))influence+=pair.Value;
                        if(influence<.05f)continue;
                        Vector3 radial=Vector3.ProjectOnPlane(relative,limbAxis);
                        if(radial.sqrMagnitude<.000001f)continue;
                        Vector3 delta=skin.transform.InverseTransformVector(radial.normalized*height*.025f*Mathf.Pow(1-distance/radius,2)*influence);
                        deltas[v]=delta;
                    }
                    for(int v=0;v<vertices.Length;v++)if(deltas.TryGetValue(topology.welded[v],out var delta))c.deltas.Add(new RiggleMeshEdits.VertexDelta{vertex=v,delta=delta});
                    if(c.deltas.Count>0)edits.correctives.Add(c);
                }
                EditorUtility.SetDirty(edits);
            }
            UpdateMeshes(rig);
        }
        public static void ApplyShapes(Mesh mesh,RiggleMeshEdits edits)
        {
            // Preserve every frame of shapes supplied by the source or another tool.
            var preserved=new List<(string name,float weight,Vector3[] v,Vector3[] n,Vector3[] t)>();
            for(int i=0;i<mesh.blendShapeCount;i++)
            {
                string name=mesh.GetBlendShapeName(i);if(name.StartsWith(Prefix,StringComparison.Ordinal))continue;
                for(int f=0;f<mesh.GetBlendShapeFrameCount(i);f++)
                {
                    var v=new Vector3[mesh.vertexCount];var n=new Vector3[v.Length];var t=new Vector3[v.Length];mesh.GetBlendShapeFrameVertices(i,f,v,n,t);
                    preserved.Add((name,mesh.GetBlendShapeFrameWeight(i,f),v,n,t));
                }
            }
            mesh.ClearBlendShapes();foreach(var p in preserved)mesh.AddBlendShapeFrame(p.name,p.weight,p.v,p.n,p.t);
            if(!edits || edits.correctives.Count==0)return;
            var scratch=Object.Instantiate(mesh);
            try
            {
                var vertices=mesh.vertices;scratch.RecalculateNormals();var restNormals=scratch.normals;
                foreach(var c in edits.correctives)
                {
                    var delta=new Vector3[vertices.Length];foreach(var d in c.deltas)delta[d.vertex]=d.delta;
                    scratch.vertices=vertices.Select((v,i)=>v+delta[i]).ToArray();scratch.RecalculateNormals();var normals=scratch.normals;
                    for(int v=0;v<normals.Length;v++)normals[v]-=restNormals[v];
                    mesh.AddBlendShapeFrame(c.name,100,delta,normals,null);
                }
            }
            finally{Object.DestroyImmediate(scratch);}
        }
        public static Bounds ExpandedBounds(Mesh mesh, RiggleMeshEdits edits)
        {
            var bounds = mesh.bounds;
            float padding = edits ? edits.correctives.Sum(c => c.deltas.Count == 0 ? 0 : c.deltas.Max(d => d.delta.magnitude) * Mathf.Max(1,c.strength)) : 0;
            bounds.Expand(padding * 2); return bounds;
        }
        public static void RefreshShapes(SkinnedMeshRenderer skin, RiggleMeshEdits edits)
        {
            Undo.RecordObject(skin, "Update corrective weights and bounds");
            var mesh = skin.sharedMesh; var weights = new Dictionary<string,float>();
            for (int i=0; i<mesh.blendShapeCount; i++) weights[mesh.GetBlendShapeName(i)] = skin.GetBlendShapeWeight(i);
            ApplyShapes(mesh, edits);
            for (int i=0; i<mesh.blendShapeCount; i++) skin.SetBlendShapeWeight(i, weights.TryGetValue(mesh.GetBlendShapeName(i),out var weight) ? weight : 0);
            skin.localBounds = ExpandedBounds(mesh, edits);
        }
        public static void Bind(Riggle rig)
        {
            var driver=rig.GetComponent<RiggleCorrectives>()??Undo.AddComponent<RiggleCorrectives>(rig.gameObject);
            Undo.RecordObject(driver,"Bind corrective shapes");var bindings=new List<RiggleCorrectives.Binding>();
            foreach(var source in rig.BakedSources)
            {
                var edits=source.source.GetComponent<RiggleMeshEdits>();if(!edits)continue;
                foreach(var c in edits.correctives)
                {
                    var bone=source.baked.bones.FirstOrDefault(b=>b.name==c.bone);if(!bone)continue;
                    bindings.Add(new RiggleCorrectives.Binding{skin=source.baked,bone=bone,shape=c.name,axis=c.axis,restDirection=c.restDirection,restRotation=c.restRotation,startAngle=c.startAngle,fullAngle=c.fullAngle,strength=c.strength});
                }
            }
            driver.bindings=bindings.ToArray();driver.Evaluate();EditorUtility.SetDirty(driver);
        }
        public static void UpdateMeshes(Riggle rig)
        {
            foreach(var source in rig.BakedSources)
            {
                var edits=source.source.GetComponent<RiggleMeshEdits>();var mesh=source.baked.sharedMesh;
                Validate(source.source.GetComponent<MeshFilter>().sharedMesh,edits,source.baked.bones.Select(b=>b.name));
                Undo.RegisterCompleteObjectUndo(mesh,"Update corrective mesh");RefreshShapes(source.baked,edits);EditorUtility.SetDirty(mesh);
            }
            Bind(rig);AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        }
        public static Matrix4x4 SkinMatrix(SkinnedMeshRenderer skin,int vertex)
        {
            var w=Unpack(skin.sharedMesh.boneWeights[vertex]);var binds=skin.sharedMesh.bindposes;Matrix4x4 result=Matrix4x4.zero;
            foreach(var pair in w)
            {
                var m=skin.bones[pair.Key].localToWorldMatrix*binds[pair.Key];
                for(int r=0;r<4;r++)for(int c=0;c<4;c++)result[r,c]+=m[r,c]*pair.Value;
            }
            return result;
        }
        public static int Sculpt(Riggle.BakedSource source,int shape,Vector3[] worldVertices,Vector3 center,Vector3 worldDirection,float radius,float amount,int seed)
        {
            var edits=GetEdits(source);var c=edits.correctives[shape];var topology=new RiggleTopology(edits.sourceMesh);
            var deltas=c.deltas.ToDictionary(d=>d.vertex,d=>d.delta);int count=0;
            for(int v=0;v<worldVertices.Length;v++)
            {
                if(topology.welded[v]!=v || (seed>=0 && topology.islands[v]!=topology.islands[seed]))continue;
                float distance=Vector3.Distance(worldVertices[v],center);if(distance>=radius)continue;
                var matrix=SkinMatrix(source.baked,v);if(Mathf.Abs(matrix.determinant)<.0000001f)continue;
                Vector3 step=matrix.inverse.MultiplyVector(worldDirection.normalized*amount*Mathf.Pow(1-distance/radius,2));
                var value=(deltas.TryGetValue(v,out var old)?old:Vector3.zero)+step;
                for(int alias=0;alias<worldVertices.Length;alias++)if(topology.welded[alias]==v){deltas[alias]=value;count++;}
            }
            c.deltas=deltas.OrderBy(p=>p.Key).Select(p=>new RiggleMeshEdits.VertexDelta{vertex=p.Key,delta=p.Value}).ToList();EditorUtility.SetDirty(edits);
            Undo.RegisterCompleteObjectUndo(source.baked.sharedMesh,"Sculpt corrective");RefreshShapes(source.baked,edits);EditorUtility.SetDirty(source.baked.sharedMesh);return count;
        }
    }
}
