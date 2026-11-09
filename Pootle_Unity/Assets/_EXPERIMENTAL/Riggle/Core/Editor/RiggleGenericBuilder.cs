using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    internal static class RiggleGenericBuilder
    {
        public static List<RiggleGeneric.Joint> Validate(RiggleGeneric rig, bool requireSources = true)
        {
            if (!rig || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Generic rig authoring is available in Edit Mode only.");
            if (rig.GetComponent<Riggle>()) throw new InvalidOperationException("Use a separate character root for Riggle Generic and humanoid Riggle.");
            if (rig.joints == null || rig.joints.Count == 0) throw new InvalidOperationException("Add at least one joint.");
            var map = new Dictionary<string, RiggleGeneric.Joint>();
            foreach (var j in rig.joints)
            {
                if (j == null || string.IsNullOrWhiteSpace(j.name) || j.name.IndexOfAny(new[] {'/', '\\'}) >= 0 || map.ContainsKey(j.name))
                    throw new InvalidOperationException("Joint names must be unique, nonempty, and contain no slashes.");
                if (!Finite(j.position) || !Finite(j.tip) || !float.IsFinite(j.radius) || j.radius <= 0)
                    throw new InvalidOperationException(j.name + ": positions and positive radius must be finite.");
                map.Add(j.name, j);
            }
            if (rig.joints.Count(j => string.IsNullOrEmpty(j.parent)) != 1)
                throw new InvalidOperationException("The skeleton needs exactly one root with an empty parent.");
            var ordered = new List<RiggleGeneric.Joint>();
            var visiting = new HashSet<string>();
            void Visit(RiggleGeneric.Joint j)
            {
                if (ordered.Contains(j)) return;
                if (!visiting.Add(j.name)) throw new InvalidOperationException("Joint parenting contains a cycle at " + j.name);
                if (!string.IsNullOrEmpty(j.parent))
                {
                    if (!map.TryGetValue(j.parent, out var parent)) throw new InvalidOperationException(j.name + ": missing parent " + j.parent);
                    Visit(parent);
                }
                if (!string.IsNullOrEmpty(j.endJoint) && (!map.TryGetValue(j.endJoint, out var end) || end.parent != j.name))
                    throw new InvalidOperationException(j.name + ": End Joint must name a direct child.");
                var tip = string.IsNullOrEmpty(j.endJoint) ? j.tip : map[j.endJoint].position;
                if ((tip - j.position).sqrMagnitude < 1e-10f) throw new InvalidOperationException(j.name + ": bone has zero length.");
                visiting.Remove(j.name); ordered.Add(j);
            }
            foreach (var j in rig.joints) Visit(j);
            var legBones=new HashSet<string>();
            foreach(var leg in rig.legs??new List<RiggleGeneric.Leg>())
            {
                if(leg==null)throw new InvalidOperationException("Remove empty leg definitions.");
                var chain=leg.Chain();
                for(int i=0;i<chain.Count;i++)
                {
                    if(!map.TryGetValue(chain[i]??"",out var joint)||(i>0&&joint.parent!=chain[i-1]))throw new InvalidOperationException(leg.name+": leg chain must list every joint in parent order.");
                    if(!legBones.Add(chain[i]))throw new InvalidOperationException("Leg chains cannot share or repeat joints.");
                }
            }
            if (!Finite(rig.transform.lossyScale) || Mathf.Abs(rig.transform.localToWorldMatrix.determinant) < 1e-10f)
                throw new InvalidOperationException("Character transform must have a finite, nonzero scale.");
            if (!float.IsFinite(rig.falloff) || !float.IsFinite(rig.jointSoftness) || !float.IsFinite(rig.smoothAmount) ||
                rig.falloff < 1.5f || rig.falloff > 8 || rig.jointSoftness < .12f || rig.jointSoftness > 1.2f ||
                rig.smoothSteps < 0 || rig.smoothSteps > 8 || rig.smoothAmount < 0 || rig.smoothAmount > .8f)
                throw new InvalidOperationException("Skinning settings are outside their supported ranges.");
            ValidateFolder(rig.assetFolder);
            if (requireSources)
            {
                var sources = rig.CollectSources();
                if (sources.Count == 0) throw new InvalidOperationException("No ordinary MeshFilters found. Add mesh parts or assign Source Meshes.");
                foreach (var source in sources)
                    if (!source.sharedMesh.isReadable || source.sharedMesh.vertexCount == 0)
                        throw new InvalidOperationException(source.name + ": enable Read/Write in model import settings; mesh must contain vertices.");
            }
            return ordered;
        }

        static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);

        internal static void ValidateFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !(folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal)) ||
                folder.Split('/').Any(p => string.IsNullOrWhiteSpace(p) || p == "." || p == ".." || p.IndexOfAny(new[] {'\\', ':'}) >= 0))
                throw new InvalidOperationException("Output folder must be a project path under Assets, with no empty or relative segments.");
        }

        static string Folder(string path)
        {
            ValidateFolder(path);
            var parts = path.Split('/'); string current = "Assets";
            foreach (var part in parts.Skip(1))
            {
                if (!AssetDatabase.IsValidFolder(current + "/" + part)) AssetDatabase.CreateFolder(current, part);
                current += "/" + part;
            }
            return current;
        }

        static string SafeName(string name) => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_'));

        public static void Bake(RiggleGeneric rig)
        {
            var joints = Validate(rig);
            if (rig.legsAnimator) RiggleGenericLegsAnimator.Validate(rig);
            var sources = rig.CollectSources();
            var createdAssets = new List<string>();
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bake Riggle Generic");
            try
            {
                Clear(rig, true);
                Undo.RegisterCompleteObjectUndo(rig, "Generic rig output");
                rig.generatedBones = Child("RiggleGeneric_Bones", rig.transform);
                var lookup = new Dictionary<string, Transform>();
                var specs = new List<RiggleBuilder.BoneSpec>();
                var scale = rig.transform.lossyScale;
                float radiusScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                foreach (var j in joints)
                {
                    var bone = Child(j.name, string.IsNullOrEmpty(j.parent) ? rig.generatedBones : lookup[j.parent]);
                    bone.SetPositionAndRotation(rig.transform.TransformPoint(j.position), rig.transform.rotation);
                    lookup.Add(j.name, bone);
                    var end = string.IsNullOrEmpty(j.endJoint) ? j.tip : joints.Find(n => n.name == j.endJoint).position;
                    specs.Add(new RiggleBuilder.BoneSpec(j.name, j.parent, bone.position, rig.transform.TransformPoint(end),
                        RiggleBuilder.Region.Appendage, HumanBodyBones.LastBone)
                        { transform = bone, skinIndex = specs.Count, radiusFraction = j.radius * radiusScale });
                }
                string folder = Folder(rig.assetFolder);
                var settings = new RiggleSkinner.Settings { scale = 1, falloff = rig.falloff, softness = rig.jointSoftness,
                    weighting = rig.weighting, smoothSteps = rig.smoothSteps, smoothAmount = rig.smoothAmount };
                foreach (var source in sources)
                {
                    var mesh = RiggleSkinner.BuildSkinnedMesh(settings, source, specs);
                    string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + SafeName(rig.name) + "_" + SafeName(source.name) + "_Generic.asset");
                    try { AssetDatabase.CreateAsset(mesh, path); createdAssets.Add(path); }
                    catch { UnityEngine.Object.DestroyImmediate(mesh); throw; }
                    var original = source.GetComponent<MeshRenderer>();
                    var skin = Undo.AddComponent<SkinnedMeshRenderer>(Child("RiggleGeneric_Skin_" + source.name, source.transform).gameObject);
                    skin.gameObject.layer = source.gameObject.layer;
                    skin.sharedMesh = mesh; skin.sharedMaterials = original.sharedMaterials;
                    skin.bones = specs.Select(b => b.transform).ToArray(); skin.rootBone = lookup[joints[0].name];
                    skin.quality = SkinQuality.Bone4; skin.updateWhenOffscreen = true;
                    skin.localBounds = mesh.bounds;
                    skin.shadowCastingMode = original.shadowCastingMode; skin.receiveShadows = original.receiveShadows;
                    skin.lightProbeUsage = original.lightProbeUsage; skin.reflectionProbeUsage = original.reflectionProbeUsage;
                    rig.bakedSources.Add(new Riggle.BakedSource { source = original, wasEnabled = original.enabled, baked = skin });
                    Undo.RegisterCompleteObjectUndo(original, "Hide original mesh"); original.enabled = false;
                }
                if (rig.legsAnimator) RiggleGenericLegsAnimator.Configure(rig);
                EditorUtility.SetDirty(rig); PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
                AssetDatabase.SaveAssets();
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                foreach (var path in createdAssets) AssetDatabase.DeleteAsset(path);
                throw;
            }
            finally { Undo.CollapseUndoOperations(group); }
        }

        public static void Clear(RiggleGeneric rig, bool preserveIntegration = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Clear is only available in Edit Mode.");
            Undo.RegisterCompleteObjectUndo(rig, "Clear generic rig");
            if (!preserveIntegration && rig.legsAnimator)
            {
                Undo.DestroyObjectImmediate(rig.legsAnimator); rig.legsAnimator = null;
            }
            foreach (var source in rig.bakedSources)
            {
                if (source == null) continue;
                if (source.source) { Undo.RegisterCompleteObjectUndo(source.source, "Restore source visibility"); source.source.enabled = source.wasEnabled; }
                if (source.baked) Undo.DestroyObjectImmediate(source.baked.gameObject);
            }
            rig.bakedSources.Clear();
            if (rig.generatedBones) Undo.DestroyObjectImmediate(rig.generatedBones.gameObject);
            rig.generatedBones = null;
            EditorUtility.SetDirty(rig);
        }

        public static string Export(RiggleGeneric rig)
        {
            if (!rig.generatedBones) throw new InvalidOperationException("Bake the rig before exporting.");
            string path = AssetDatabase.GenerateUniqueAssetPath(Folder(rig.assetFolder) + "/" + SafeName(rig.name) + "_Generic.prefab");
            PrefabUtility.SaveAsPrefabAsset(rig.gameObject, path, out bool success);
            if (!success) throw new InvalidOperationException("Could not save generic rig prefab.");
            return path;
        }

        static Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Create generic rig bone/skin");
            go.transform.SetParent(parent, false); return go.transform;
        }
    }
}
