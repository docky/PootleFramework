using System;
using System.Linq;
using Docky.RiggleTools.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleGenericTests
    {
        GameObject root;
        RiggleGeneric rig;
        MeshFilter source;
        Scene scene;
        string folder;

        [SetUp] public void SetUp()
        {
            scene = EditorSceneManager.NewPreviewScene();
            root = new GameObject("Generic Test"); SceneManager.MoveGameObjectToScene(root, scene);
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.transform.SetParent(root.transform, false);
            cube.transform.localPosition = new Vector3(0,.5f,0); cube.transform.localScale = new Vector3(.8f,1,1.8f);
            source = cube.GetComponent<MeshFilter>();
            rig = root.AddComponent<RiggleGeneric>();
            rig.TryGetBounds(out var bounds); rig.SetQuadrupedPreset(bounds);
            folder = "Assets/__RiggleGenericTests_" + Guid.NewGuid().ToString("N"); rig.assetFolder = folder;
        }

        [TearDown] public void TearDown()
        {
            if (root) Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
            if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
        }

        [Test] public void PresetHasFourIndependentLegsAndSupportsAdditionalLegPairs()
        {
            Assert.That(RiggleGenericBuilder.Validate(rig).Count, Is.EqualTo(19));
            Assert.That(rig.legs.Count, Is.EqualTo(4));
            foreach (var leg in rig.legs.Take(2).ToArray())
            {
                string Remap(string n) => n.Replace("Front", "Middle");
                foreach (var name in new[] { leg.upper, leg.middle, leg.foot })
                {
                    var j = rig.joints.Find(b => b.name == name);
                    rig.joints.Add(new RiggleGeneric.Joint { name = Remap(name), parent = Remap(j.parent),
                        position = j.position + Vector3.back * .3f, tip = j.tip + Vector3.back * .3f,
                        endJoint = Remap(j.endJoint), radius = j.radius });
                }
                rig.legs.Add(new RiggleGeneric.Leg { name = Remap(leg.name), upper = Remap(leg.upper),
                    middle = Remap(leg.middle), foot = Remap(leg.foot), side = leg.side, opposite = Remap(leg.opposite) });
            }
            RiggleGenericBuilder.Bake(rig);
            Assert.That(rig.generatedBones.GetComponentsInChildren<Transform>().Length, Is.EqualTo(26));
            if (RiggleGenericLegsAnimator.AnimatorType != null)
            {
                RiggleGenericLegsAnimator.Configure(rig);
                Assert.That(new SerializedObject(rig.legsAnimator).FindProperty("Legs").arraySize, Is.EqualTo(6));
            }
        }

        [TestCase(Riggle.WeightingMethod.Envelopes)]
        [TestCase(Riggle.WeightingMethod.ContiguousSurface)]
        public void BakePreservesSourceAndTransformedRestGeometry(Riggle.WeightingMethod weighting)
        {
            rig.weighting = weighting;
            root.transform.SetPositionAndRotation(new Vector3(3,2,-4), Quaternion.Euler(8,37,12));
            root.transform.localScale = new Vector3(1.2f,.8f,1.4f);
            source.transform.localRotation = Quaternion.Euler(13,11,9);
            var original = source.sharedMesh;
            RiggleGenericBuilder.Bake(rig);
            Assert.That(source.sharedMesh, Is.SameAs(original));
            Assert.That(source.GetComponent<MeshRenderer>().enabled, Is.False);
            var skin = rig.bakedSources.Single().baked;
            Assert.That(skin.sharedMesh, Is.Not.SameAs(original));
            foreach (var w in skin.sharedMesh.boneWeights)
                Assert.That(w.weight0 + w.weight1 + w.weight2 + w.weight3, Is.EqualTo(1).Within(.00001f));
            var baked = new Mesh();
            try
            {
                skin.BakeMesh(baked, true);
                for (int i = 0; i < baked.vertexCount; i++)
                    Assert.That(Vector3.Distance(skin.transform.TransformPoint(baked.vertices[i]), source.transform.TransformPoint(original.vertices[i])), Is.LessThan(.0001f));
                var rest = baked.vertices;
                skin.bones[0].localRotation *= Quaternion.Euler(0,0,35);
                skin.BakeMesh(baked, true);
                Assert.That(rest.Zip(baked.vertices, Vector3.Distance).Max(), Is.GreaterThan(.05f));
            }
            finally { Object.DestroyImmediate(baked); }
            RiggleGenericBuilder.Clear(rig);
            Assert.That(source.GetComponent<MeshRenderer>().enabled, Is.True);
            Assert.That(rig.generatedBones, Is.Null);
        }

        [Test] public void InvalidDefinitionDoesNotDestroyPreviousBake()
        {
            RiggleGenericBuilder.Bake(rig); var previous = rig.generatedBones;
            rig.joints[1].parent = "missing";
            Assert.Throws<InvalidOperationException>(() => RiggleGenericBuilder.Bake(rig));
            Assert.That(rig.generatedBones, Is.SameAs(previous));
            Assert.That(rig.bakedSources.Single().baked, Is.Not.Null);
        }

        [Test] public void ValidationRejectsCyclesDuplicateNamesAndPathTraversal()
        {
            rig.joints[1].parent = "Chest";
            Assert.Throws<InvalidOperationException>(() => RiggleGenericBuilder.Validate(rig));
            rig.joints[1].parent = "Hips"; rig.joints[1].name = "Hips";
            Assert.Throws<InvalidOperationException>(() => RiggleGenericBuilder.Validate(rig));
            rig.joints[1].name = "Spine"; rig.assetFolder = "Assets/../Outside";
            Assert.Throws<InvalidOperationException>(() => RiggleGenericBuilder.Validate(rig));
        }

        [Test] public void LegValidationRejectsInvalidPairingAndStraightKnees()
        {
            if (RiggleGenericLegsAnimator.AnimatorType == null) Assert.Ignore("Legs Animator not installed.");
            rig.legs[0].opposite = rig.legs[0].name;
            Assert.Throws<InvalidOperationException>(() => RiggleGenericLegsAnimator.Validate(rig));
            rig.legs[0].opposite = rig.legs[1].name;
            var leg = rig.legs[0];
            rig.joints.Find(j => j.name == leg.middle).position = Vector3.Lerp(
                rig.joints.Find(j => j.name == leg.upper).position, rig.joints.Find(j => j.name == leg.foot).position, .5f);
            Assert.Throws<InvalidOperationException>(() => RiggleGenericLegsAnimator.Validate(rig));
        }

        [Test] public void LegsAnimatorRebakePreservesSettingsAndPrefabReferences()
        {
            if (RiggleGenericLegsAnimator.AnimatorType == null) Assert.Ignore("Legs Animator not installed.");
            RiggleGenericBuilder.Bake(rig); RiggleGenericLegsAnimator.Configure(rig);
            var component = rig.legsAnimator;
            var so = new SerializedObject(component);
            so.FindProperty("Legs").GetArrayElementAtIndex(0).FindPropertyRelative("LegBlendWeight").floatValue = .42f;
            so.ApplyModifiedPropertiesWithoutUndo();
            RiggleGenericBuilder.Bake(rig);
            Assert.That(rig.legsAnimator, Is.SameAs(component));
            AssertMappings(rig);
            so.Update();
            Assert.That(so.FindProperty("Legs").GetArrayElementAtIndex(0).FindPropertyRelative("LegBlendWeight").floatValue, Is.EqualTo(.42f));
            var path = RiggleGenericBuilder.Export(rig);
            var prefab = PrefabUtility.LoadPrefabContents(path);
            try { AssertMappings(prefab.GetComponent<RiggleGeneric>()); }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            RiggleGenericBuilder.Clear(rig);
            Assert.That(rig.GetComponent(RiggleGenericLegsAnimator.AnimatorType), Is.Null);
        }

        [Test] public void ClearAndRebakeAreUndoableAndRestoreDisabledSourceState()
        {
            source.GetComponent<MeshRenderer>().enabled = false;
            RiggleGenericBuilder.Bake(rig);
            RiggleGenericBuilder.Bake(rig);
            Assert.That(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length, Is.EqualTo(1));
            Undo.IncrementCurrentGroup();
            RiggleGenericBuilder.Clear(rig);
            Assert.That(source.GetComponent<MeshRenderer>().enabled, Is.False);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.That(rig.generatedBones, Is.Not.Null);
            Assert.That(rig.bakedSources.Single().baked, Is.Not.Null);
        }

        static void AssertMappings(RiggleGeneric rig)
        {
            var so = new SerializedObject(rig.legsAnimator);
            Assert.That(so.FindProperty("baseTransform").objectReferenceValue, Is.SameAs(rig.transform));
            Assert.That(((Transform)so.FindProperty("Hips").objectReferenceValue).IsChildOf(rig.generatedBones), Is.True);
            var legs = so.FindProperty("Legs");
            Assert.That(legs.arraySize, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                var leg = legs.GetArrayElementAtIndex(i);
                foreach (var field in new[] { "BoneStart", "BoneMid", "BoneEnd" })
                    Assert.That(((Transform)leg.FindPropertyRelative(field).objectReferenceValue).IsChildOf(rig.generatedBones), Is.True);
                Assert.That(leg.FindPropertyRelative("OppositeLegIndex").intValue, Is.EqualTo(i ^ 1));
            }
        }
    }
}
