using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Docky.RiggleTools.Editor;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleBodyPartsTests
    {
        Riggle rig; string folder; Material material;
        [SetUp] public void Setup()
        {
            rig = new GameObject("Chunk test").AddComponent<Riggle>(); rig.SetFallbackPoints();
            folder = "Assets/RiggleGenerated/ChunkTest_" + Guid.NewGuid().ToString("N"); RiggleModelPreparation.Folder(folder);
            var so = new SerializedObject(rig); so.FindProperty("assemblyMode").enumValueIndex = 2;
            so.FindProperty("assetFolder").stringValue = folder; so.ApplyModifiedPropertiesWithoutUndo();
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, folder + "/part.mat");
        }
        [TearDown] public void Cleanup()
        {
            if (rig)
            {
                foreach (var t in rig.GetComponentsInChildren<Transform>(true))
                { Undo.ClearUndo(t.gameObject); foreach (var c in t.GetComponents<Component>()) Undo.ClearUndo(c); }
                Object.DestroyImmediate(rig.gameObject);
            }
            AssetDatabase.DeleteAsset(folder);
        }
        Transform Group(string name)
        { var t = new GameObject(name).transform; t.SetParent(rig.transform, false); return t; }
        MeshFilter Cube(string name, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent ? parent : rig.transform, false);
            // Deliberately overlap all chunks near the shoulder: proximity must never defeat ownership.
            go.transform.localPosition = new Vector3(.15f, 1.3f, 0); go.transform.localScale = Vector3.one * .3f;
            go.GetComponent<MeshRenderer>().sharedMaterial = material; return go.GetComponent<MeshFilter>();
        }
        [TestCase("Arm.L", "LeftUpperArm,LeftLowerArm")]
        [TestCase("arm_with_hand_R", "RightUpperArm,RightLowerArm,RightHand")]
        [TestCase("LeftLegWithoutFoot", "LeftUpperLeg,LeftLowerLeg")]
        [TestCase("LegWithFoot.R.001", "RightUpperLeg,RightLowerLeg,RightFoot")]
        [TestCase("Torso", "Hips,Spine,Chest,UpperChest")]
        [TestCase("TorsoWithNeck", "Hips,Spine,Chest,UpperChest,Neck")]
        [TestCase("Head", "Head")]
        [TestCase("HeadWithNeck", "Head,Neck")]
        public void NamesResolveExactChains(string name, string expected)
        {
            var scope = RiggleBodyParts.Resolve(Group(name));
            Assert.That(scope, Is.Not.Null); Assert.That(scope.bones, Is.EquivalentTo(expected.Split(',').Select(n => "Riggle_" + n)));
        }
        [Test] public void AmbiguousOrPartialNamesDoNotResolve()
        {
            foreach (var name in new[] { "Arm", "Leg", "LeftArm.R", "Armour.L", "TorsoCable", "Headlight" })
                Assert.That(RiggleBodyParts.Resolve(Group(name)), Is.Null, name);
        }
        [TestCase(0, false)] [TestCase(1, true)]
        public void MergedWeightsNeverLeaveTheirChainsEvenAfterSmoothingAndTwist(int method, bool twists)
        {
            var so = new SerializedObject(rig); so.FindProperty("weighting").enumValueIndex = method;
            so.FindProperty("distributeTwist").boolValue = twists; so.FindProperty("surfaceSmoothSteps").intValue = 8;
            so.ApplyModifiedPropertiesWithoutUndo();
            var names = new[] { "Torso", "ArmWithHand.L", "Arm.R", "Leg.L", "LegWithFoot.R", "HeadWithNeck" };
            var meshes = names.Select(n => Cube(n)).ToArray();
            var scopes = meshes.Select(m => RiggleBodyParts.Resolve(m.transform)).ToArray();
            RiggleBuilder.Bake(rig); var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
            int start = 0;
            for (int p = 0; p < meshes.Length; p++)
            {
                var allowed = scopes[p].bones;
                foreach (var w in skin.sharedMesh.boneWeights.Skip(start).Take(meshes[p].sharedMesh.vertexCount))
                {
                    Assert.That(w.weight0 + w.weight1 + w.weight2 + w.weight3, Is.EqualTo(1).Within(.00001));
                    Check(w.boneIndex0, w.weight0); Check(w.boneIndex1, w.weight1); Check(w.boneIndex2, w.weight2); Check(w.boneIndex3, w.weight3);
                    void Check(int i, float weight)
                    { if (weight > 0) Assert.That(allowed.Contains(skin.bones[i].name.Split(new[] { "_Twist" }, StringSplitOptions.None)[0]), Is.True, names[p] + " leaked to " + skin.bones[i].name); }
                }
                start += meshes[p].sharedMesh.vertexCount;
            }
            // Moving an arm cannot move any torso vertex, even though their source geometry overlaps.
            var before = new Mesh(); var after = new Mesh();
            try
            {
                skin.BakeMesh(before, true);
                skin.bones.First(b => b.name == "Riggle_LeftUpperArm").localRotation = Quaternion.Euler(0, 0, 70);
                skin.BakeMesh(after, true);
                for (int v = 0; v < meshes[0].sharedMesh.vertexCount; v++)
                    Assert.That(Vector3.Distance(before.vertices[v], after.vertices[v]), Is.LessThan(.00001));
                Assert.That(before.vertices.Zip(after.vertices, Vector3.Distance).Max(), Is.GreaterThan(.01));
            }
            finally { Object.DestroyImmediate(before); Object.DestroyImmediate(after); }
        }
        [Test] public void SeparateChunksShareSkeletonUseCompactBoneListsAndSurviveExportRebakeAndClear()
        {
            var so = new SerializedObject(rig); so.FindProperty("separateLimbMeshes").boolValue = true; so.ApplyModifiedPropertiesWithoutUndo();
            Cube("Torso"); var arm = Group("ArmWithHand.L"); Cube("Sleeve", arm); Cube("Glove surface", arm);
            var head = Cube("My helmet"); var settings = head.gameObject.AddComponent<RiggleBodyPart>();
            settings.kind = RiggleBodyPart.Kind.Head; settings.includeNeck = true; settings.keepSeparateRenderer = true;
            var hat = Cube("Head^ Hat").transform; var parent = hat.parent;
            RiggleBuilder.Bake(rig); Verify(); RiggleBuilder.Bake(rig); Verify();
            var prefab = RigglePrefabExporter.Save(rig);
            Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>().Length, Is.EqualTo(3));
            Assert.That(prefab.GetComponentsInChildren<RiggleBodyPart>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(1));
            RiggleBuilder.Clear(rig); Assert.That(hat.parent, Is.EqualTo(parent));
            Assert.That(rig.GetComponentsInChildren<SkinnedMeshRenderer>(), Is.Empty);
            Assert.That(rig.GetComponentsInChildren<MeshRenderer>().All(r => r.enabled), Is.True);
            void Verify()
            {
                var skins = rig.GetComponentsInChildren<SkinnedMeshRenderer>(); Assert.That(skins.Length, Is.EqualTo(3));
                var armSkin = skins.Single(s => s.name.Contains("ArmWithHand.L"));
                Assert.That(armSkin.sharedMesh.vertexCount, Is.EqualTo(48));
                Assert.That(armSkin.bones.All(b => b.name.StartsWith("Riggle_LeftUpperArm") || b.name.StartsWith("Riggle_LeftLowerArm") || b.name == "Riggle_LeftHand"), Is.True);
                Assert.That(skins.Select(s => s.rootBone).Distinct().Count(), Is.EqualTo(1));
                Assert.That(hat.parent.name, Is.EqualTo("Riggle_Head"));
            }
        }
        [Test] public void ComponentOverridesNameAndPreflightRetainsExistingRigOnUnknownSource()
        {
            var source = Cube("Torso"); var part = source.gameObject.AddComponent<RiggleBodyPart>();
            part.kind = RiggleBodyPart.Kind.Leg; part.side = RiggleBodyPart.Side.Right; part.includeHandOrFoot = true;
            RiggleBuilder.Bake(rig); var skin = rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single();
            Assert.That(skin.bones.All(b => b.name.StartsWith("Riggle_RightUpperLeg") || b.name.StartsWith("Riggle_RightLowerLeg") || b.name == "Riggle_RightFoot"), Is.True);
            var old = rig.GeneratedBones; Cube("Unknown"); Assert.Throws<InvalidOperationException>(() => RiggleBuilder.Bake(rig));
            Assert.That(rig.GeneratedBones, Is.EqualTo(old)); Assert.That(source.GetComponent<MeshRenderer>().enabled, Is.False);
        }
    }
}
