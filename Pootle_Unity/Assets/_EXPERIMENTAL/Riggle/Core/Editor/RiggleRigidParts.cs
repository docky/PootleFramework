using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Editor
{
    internal static class RiggleRigidParts
    {
        internal sealed class Part { public MeshFilter mesh; public string bone; public RiggleBodyParts.Scope scope; }
        internal sealed class Attachment { public Transform root; public string bone; public bool hidden; }
        internal sealed class BuildPlan
        {
            public readonly List<Part> parts = new List<Part>();
            public readonly List<Attachment> attachments = new List<Attachment>();
            public bool bodyParts, separateLimbs;
            public Transform OutputGroup(Part p) => p.scope != null && (p.scope.separate || separateLimbs && p.scope.limb) ? p.scope.root : null;
            public string Summary => $"{parts.Count} mesh parts → {parts.GroupBy(OutputGroup).Count()} skinned renderer(s); {attachments.Count} separate accessories." +
                (bodyParts ? "\n" + string.Join("\n", parts.Select(p => p.scope == null ? p.mesh.name + " → " + p.bone.Replace("Riggle_", "") :
                    p.scope.label + " → " + string.Join(", ", p.scope.bones.Select(b => b.Replace("Riggle_", ""))) +
                    (OutputGroup(p) ? " · separate renderer" : " · merged")).Distinct()) : "");
        }
        static readonly string[] BoneNames = {
            "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand", "RightUpperLeg", "RightLowerLeg", "RightFoot"
        };
        static string Key(string name) => Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]", "");
        public static bool TryBone(string name, out string bone)
        {
            bone = null;
            name = Regex.Replace(name.Trim(), @"(?:\s*\(\d+\)|\.\d{3})$", "");
            if (name.StartsWith("Riggle_", StringComparison.OrdinalIgnoreCase)) name = name.Substring(7);
            string key = Key(name);
            // Generic optional chains use numbered bones; bare names address their first segment.
            var extra = Regex.Match(key, @"^(left|right|middle)?(ear|wing|tail|ponytail)(\d*)(left|right|middle|l|r|m)?$");
            if (extra.Success)
            {
                string first = extra.Groups[1].Value, last = extra.Groups[4].Value;
                if (last == "m") last = "middle";
                if (last == "l") last = "left"; if (last == "r") last = "right";
                if (first.Length > 0 && last.Length > 0 && first != last) return false;
                string sideName = first.Length > 0 ? first : last, type = extra.Groups[2].Value;
                if(type=="ponytail" && sideName.Length==0)sideName="middle";
                if(type!="ponytail" && ((type == "tail") != (sideName.Length == 0) || sideName=="middle")) return false;
                if (!int.TryParse(extra.Groups[3].Value.Length > 0 ? extra.Groups[3].Value : "1", out int segment) || segment < 1 || segment > (type=="ponytail"?6:16)) return false;
                string chain = type=="ponytail" ? (sideName=="middle"?"Middle":sideName=="left"?"Left":"Right")+"Ponytail" : type == "tail" ? "Tail" : (sideName == "left" ? "Left" : "Right") + (type == "ear" ? "Ear" : "Wing");
                bone = RiggleAppendageBuilder.BoneName(chain, segment - 1); return true;
            }
            foreach (string b in BoneNames) if (Key(b) == key) { bone = "Riggle_" + b; return true; }
            if (key == "pelvis") { bone = "Riggle_Hips"; return true; }
            string spaced = Regex.Replace(name, @"([a-z0-9])([A-Z])", "$1 $2");
            var tokens = Regex.Split(spaced.ToLowerInvariant(), "[^a-z0-9]+").Where(t => t.Length > 0).ToList();
            string side = null;
            if (tokens.Count > 1 && new[] { "l", "r", "left", "right" }.Contains(tokens[0]))
            { side = tokens[0] == "l" || tokens[0] == "left" ? "Left" : "Right"; tokens.RemoveAt(0); }
            if (tokens.Count > 1 && new[] { "l", "r", "left", "right" }.Contains(tokens.Last()))
            {
                string end = tokens.Last() == "l" || tokens.Last() == "left" ? "Left" : "Right";
                if (side != null && side != end) return false;
                side = end; tokens.RemoveAt(tokens.Count - 1);
            }
            if (side == null) return false;
            string part = string.Concat(tokens);
            if (part == "forearm" || part == "elbow") part = "lowerarm";
            if (part == "wrist") part = "hand";
            if (part == "thigh") part = "upperleg";
            if (part == "shin" || part == "knee") part = "lowerleg";
            if (part == "ankle") part = "foot";
            foreach (string b in BoneNames) if (Key(b) == side.ToLowerInvariant() + part) { bone = "Riggle_" + b; return true; }
            return false;
        }
        public static List<MeshFilter> Sources(Riggle rig)
        {
            var sources = rig.CollectSourceMeshes();
            foreach (var attachment in rig.AttachedObjects.Where(a => a.source))
                foreach (var mesh in attachment.source.GetComponentsInChildren<MeshFilter>(true))
                    if (mesh.sharedMesh && mesh.GetComponent<MeshRenderer>() && !sources.Contains(mesh) &&
                        (rig.SourceMeshes.Length == 0 || rig.SourceMeshes.Contains(mesh))) sources.Add(mesh);
            return sources;
        }
        public static BuildPlan Plan(Riggle rig)
        {
            var plan = new BuildPlan { bodyParts = rig.Assembly == Riggle.AssemblyMode.NamedBodyParts, separateLimbs = rig.SeparateLimbMeshes }; var sources = new HashSet<MeshFilter>(Sources(rig));
            var errors = new List<string>(); var visited = new HashSet<Transform>();
            void Walk(Transform t, string inherited, RiggleBodyParts.Scope inheritedScope)
            {
                if (!visited.Add(t) || t == rig.GeneratedBones || t.GetComponent<RiggleImportedSource>() || t.GetComponent<ControlRig.Rig_References>()) return;
                bool marked = !string.IsNullOrEmpty(rig.AttachmentSymbol) && t.name.Contains(rig.AttachmentSymbol);
                string candidate = t.name;
                if (marked)
                {
                    var split = candidate.Split(new[] { rig.AttachmentSymbol }, StringSplitOptions.None);
                    candidate = split.FirstOrDefault(s=>!string.IsNullOrWhiteSpace(s))?.Trim() ?? "";
                }
                bool recognized = TryBone(candidate, out var own);
                var ownScope = plan.bodyParts && !marked ? RiggleBodyParts.Resolve(t) : null;
                var scope = ownScope ?? (recognized ? null : inheritedScope);
                string bone = ownScope != null ? (ownScope.bones.Count == 1 ? ownScope.bones.First() : null) : recognized ? own : inherited;
                if (marked)
                {
                    if (bone == null) errors.Add(t.name + ": accessory needs a bone name (e.g. Head^ Hat), or a named parent.");
                    else plan.attachments.Add(new Attachment { root = t, bone = bone, hidden=t.name.Contains(rig.AttachmentSymbol+rig.AttachmentSymbol) });
                    return;
                }
                var filter = t.GetComponent<MeshFilter>();
                if (filter && sources.Contains(filter))
                {
                    if (bone == null && scope == null) errors.Add(t.name + ": no recognized bone/body-part name on this mesh or its parents. Name it or add Riggle Body Part.");
                    else
                    {
                        var old = rig.BakedSources.FirstOrDefault(b => b.source == filter.GetComponent<MeshRenderer>());
                        bool enabled = old != null ? old.wasEnabled : filter.GetComponent<MeshRenderer>().enabled;
                        bool active = true; for (var p = t; p && p != rig.transform; p = p.parent) active &= p.gameObject.activeSelf;
                        if (!enabled || !active) errors.Add(t.name + ": hidden parts cannot be merged; remove them from the source selection or enable them.");
                        else if (filter.sharedMesh.blendShapeCount > 0 || (filter.TryGetComponent<RiggleMeshEdits>(out var edits) && (edits.weights.Count > 0 || edits.correctives.Count > 0))) errors.Add(t.name + ": named part modes require a source without deformation edits or blend shapes. Preserve a separate smooth version.");
                        else plan.parts.Add(new Part { mesh = filter, bone = bone, scope = scope });
                    }
                }
                foreach (Transform child in t) Walk(child, bone, scope);
            }
            foreach (Transform child in rig.transform) Walk(child, null, null);
            foreach (var attachment in rig.AttachedObjects.Where(a => a.source))
            {
                string inherited = null;
                for (var parent = attachment.originalParent; parent && parent != rig.transform; parent = parent.parent)
                    if (TryBone(parent.name, out inherited)) break;
                Walk(attachment.source, inherited, null);
            }
            if (rig.GetComponent<MeshFilter>() && sources.Contains(rig.GetComponent<MeshFilter>())) errors.Add("Named parts must be child objects, not a mesh on the Riggle root.");
            var extraTargets = plan.parts.Where(p => p.scope == null).Select(p => p.bone).Concat(plan.attachments.Select(a => a.bone)).Where(b => b != null && RiggleAppendageBuilder.IsAppendageBone(b)).ToArray();
            if (extraTargets.Length > 0)
            {
                var available = new HashSet<string>(RiggleAppendageBuilder.Definitions(rig).SelectMany(d => RiggleAppendageBuilder.ScopeBones(rig, d.name)));
                foreach (var target in extraTargets) if (!available.Contains(target)) errors.Add(target + ": enable its appendage and enough segments before baking.");
            }
            if (errors.Count > 0) throw new InvalidOperationException("Named part scan:\n" + string.Join("\n", errors));
            if (plan.parts.Count == 0 && plan.attachments.Count == 0) throw new InvalidOperationException("No named parts or marked accessories found.");
            return plan;
        }
        public static void Build(Riggle rig, BuildPlan plan, List<RiggleBuilder.BoneSpec> bones,
            Dictionary<string, Transform> lookup, string folder, string baseName)
        {
            foreach (var group in plan.parts.GroupBy(plan.OutputGroup))
            {
                var parts = group.ToList();
                var outputBones = plan.bodyParts ? bones.Where(b => parts.Any(p => p.scope != null
                    ? RiggleBodyParts.Allows(p.scope, b) : p.bone == b.name)).ToList() : bones;
                string suffix = group.Key ? "Part_" + Regex.Replace(group.Key.name, @"[^a-zA-Z0-9_.-]", "_") : "MergedParts";
                BuildOutput(rig, parts, outputBones, lookup, folder, baseName, suffix, plan.bodyParts);
            }
        }
        static void BuildOutput(Riggle rig, List<Part> parts, List<RiggleBuilder.BoneSpec> bones,
            Dictionary<string, Transform> lookup, string folder, string baseName, string suffix, bool bodyParts)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>();
            var colors = new List<Color>(); var weights = new List<BoneWeight>();
            var uvs = Enumerable.Range(0, 8).Select(_ => new List<Vector4>()).ToArray();
            var materials = new List<Material>(); var indices = new List<List<int>>(); bool recalcNormals = false;
            foreach (var part in parts)
            {
                var mesh = part.mesh.sharedMesh; int offset = vertices.Count; int count = mesh.vertexCount;
                var matrix = rig.transform.worldToLocalMatrix * part.mesh.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose; bool mirrored = matrix.determinant < 0;
                int bone = bones.FindIndex(b => b.name == part.bone);
                if (part.scope == null && bone < 0) throw new InvalidOperationException("Missing target bone: " + part.bone);
                vertices.AddRange(mesh.vertices.Select(matrix.MultiplyPoint3x4));
                var n = mesh.normals; recalcNormals |= n.Length != count;
                normals.AddRange(n.Length == count ? n.Select(v => normalMatrix.MultiplyVector(v).normalized) : Enumerable.Repeat(Vector3.up, count));
                var tang = mesh.tangents;
                tangents.AddRange(Enumerable.Range(0, count).Select(i => {
                    var v = i < tang.Length ? tang[i] : new Vector4(1, 0, 0, 1);
                    var d = matrix.MultiplyVector(new Vector3(v.x, v.y, v.z)).normalized;
                    return new Vector4(d.x, d.y, d.z, v.w * (mirrored ? -1 : 1)); }));
                var c = mesh.colors; colors.AddRange(c.Length == count ? c : Enumerable.Repeat(Color.white, count));
                if (part.scope == null)
                    weights.AddRange(Enumerable.Repeat(new BoneWeight { boneIndex0 = bone, weight0 = 1 }, count));
                else
                {
                    var allowed = bones.Where(b => RiggleBodyParts.Allows(part.scope, b)).ToList();
                    // The solver never even receives bones outside this chunk's chain.
                    // Compact per-part indices are remapped only when assembling the output renderer.
                    Mesh weighted = RiggleSkinner.BuildSkinnedMesh(rig, part.mesh, allowed, false);
                    try
                    {
                        var remap = allowed.Select(b => bones.IndexOf(b)).ToArray();
                        foreach (var value in weighted.boneWeights)
                        {
                            var w = value; w.boneIndex0 = remap[w.boneIndex0]; w.boneIndex1 = remap[w.boneIndex1];
                            w.boneIndex2 = remap[w.boneIndex2]; w.boneIndex3 = remap[w.boneIndex3]; weights.Add(w);
                        }
                    }
                    finally { Object.DestroyImmediate(weighted); }
                }
                for (int channel = 0; channel < 8; channel++)
                { var values = new List<Vector4>(); mesh.GetUVs(channel, values); uvs[channel].AddRange(values.Count == count ? values : Enumerable.Repeat(Vector4.zero, count)); }
                var mats = part.mesh.GetComponent<MeshRenderer>().sharedMaterials;
                if (mats.Length != mesh.subMeshCount) throw new InvalidOperationException(part.mesh.name + ": material count must match submesh count.");
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (mesh.GetTopology(s) != MeshTopology.Triangles) throw new InvalidOperationException(part.mesh.name + ": only triangle meshes can be merged.");
                    int material = materials.IndexOf(mats[s]);
                    if (material < 0) { material = materials.Count; materials.Add(mats[s]); indices.Add(new List<int>()); }
                    var tri = mesh.GetTriangles(s);
                    for (int i = 0; i < tri.Length; i += 3)
                    { indices[material].Add(offset + tri[i]); indices[material].Add(offset + tri[i + (mirrored ? 2 : 1)]); indices[material].Add(offset + tri[i + (mirrored ? 1 : 2)]); }
                }
            }
            var merged = new Mesh { name = baseName + "_" + suffix, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            merged.SetVertices(vertices); merged.SetNormals(normals); merged.SetTangents(tangents); merged.SetColors(colors);
            for (int channel = 0; channel < 8; channel++) merged.SetUVs(channel, uvs[channel]);
            merged.subMeshCount = materials.Count;
            for (int i = 0; i < materials.Count; i++) merged.SetTriangles(indices[i], i);
            if (recalcNormals) merged.RecalculateNormals();
            merged.bindposes = bones.Select(b => b.transform.worldToLocalMatrix * rig.transform.localToWorldMatrix).ToArray();
            merged.boneWeights = weights.ToArray(); merged.RecalculateBounds();
            AssetDatabase.CreateAsset(merged, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + baseName + "_" + suffix + ".asset"));
            var go = new GameObject("Riggle_" + suffix); go.layer = rig.gameObject.layer; Undo.RegisterCreatedObjectUndo(go, "Merge rigid parts"); go.transform.SetParent(rig.GeneratedBones, false);
            var skin = Undo.AddComponent<SkinnedMeshRenderer>(go); skin.sharedMesh = merged; skin.sharedMaterials = materials.ToArray();
            skin.bones = bones.Select(b => b.transform).ToArray(); skin.rootBone = lookup["Riggle_Hips"]; skin.quality = bodyParts ? SkinQuality.Bone4 : SkinQuality.Bone1;
            skin.updateWhenOffscreen = true; skin.localBounds = merged.bounds;
            foreach (var part in parts)
            {
                var renderer = part.mesh.GetComponent<MeshRenderer>();
                rig.BakedSources.Add(new Riggle.BakedSource { source = renderer, wasEnabled = renderer.enabled, baked = skin });
                Undo.RecordObject(renderer, "Hide merged source"); renderer.enabled = false;
            }
        }
        public static void Attach(Riggle rig, BuildPlan plan, Dictionary<string, Transform> bones)
        {
            foreach (var item in plan.attachments)
            {
                var t = item.root;
                rig.AttachedObjects.Add(new Riggle.AttachedObject { source = t, originalParent = t.parent, siblingIndex = t.GetSiblingIndex(),
                    localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale, boneName = item.bone, wasActive=t.gameObject.activeSelf });
                var before = t.localToWorldMatrix;
                Undo.SetTransformParent(t, bones[item.bone], "Attach accessory to bone");
                if(item.hidden){Undo.RecordObject(t.gameObject,"Hide alternate attachment");t.gameObject.SetActive(false);}
                var after = t.localToWorldMatrix;
                for (int i = 0; i < 16; i++)
                    if (Mathf.Abs(before[i] - after[i]) > .0001f)
                        throw new InvalidOperationException(t.name + ": reparenting would change its shape. Apply the non-uniform parent scale before attaching this accessory.");
            }
        }
        public static void RestoreAttachments(Riggle rig)
        {
            foreach (var item in rig.AttachedObjects.OrderBy(a => a.siblingIndex))
            {
                if (!item.source) continue;
                Undo.SetTransformParent(item.source, item.originalParent ? item.originalParent : rig.transform, "Restore accessory parent");
                Undo.RecordObject(item.source, "Restore accessory rest pose");
                item.source.SetLocalPositionAndRotation(item.localPosition, item.localRotation); item.source.localScale = item.localScale;
                item.source.SetSiblingIndex(item.siblingIndex);
                Undo.RecordObject(item.source.gameObject,"Restore accessory visibility");item.source.gameObject.SetActive(item.wasActive);
            }
            rig.AttachedObjects.Clear();
        }
    }
}
