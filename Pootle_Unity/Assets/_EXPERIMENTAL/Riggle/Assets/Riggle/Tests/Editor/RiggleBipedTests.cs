using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Docky.RiggleTools.Editor;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleBipedTests
    {
        Riggle[] rigs;
        [SetUp] public void OpenScene()
        {
            EditorSceneManager.OpenScene("Assets/_EXPERIMENTAL/Riggle/riggle test.unity");
            rigs = Object.FindObjectsByType<Riggle>();
            Assert.That(rigs.Length, Is.EqualTo(2));
        }

        [Test] public void BothCharactersHaveValidMappedHumanoidsAndPreservedSources()
        {
            foreach (var rig in rigs)
            {
                var animator = rig.GetComponent<Animator>();
                Assert.That(animator && animator.avatar && animator.avatar.isValid && animator.avatar.isHuman, Is.True, rig.name);
                Assert.That(rig.GetComponent<RiggleHumanoidPreview>(), Is.Not.Null);
                foreach (var bone in new[] {HumanBodyBones.Hips, HumanBodyBones.Head, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm,
                    HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg,
                    HumanBodyBones.LeftFoot, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot})
                    Assert.That(animator.GetBoneTransform(bone), Is.Not.Null, rig.name + " " + bone);
                foreach (var item in rig.BakedSources)
                {
                    Assert.That(item.source.enabled, Is.False);
                    var original = item.source.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(item.baked.sharedMesh, Is.Not.SameAs(original));
                    Assert.That(original.boneWeights.Length, Is.Zero);
                    Assert.That(item.baked.sharedMesh.vertices, Is.EqualTo(original.vertices));
                    Assert.That(item.baked.sharedMesh.uv, Is.EqualTo(original.uv));
                    Assert.That(item.baked.sharedMesh.triangles, Is.EqualTo(original.triangles));
                    Assert.That(item.baked.sharedMesh.normals, Is.EqualTo(original.normals));
                }
            }
        }

        [Test] public void RestPoseMatchesOriginalGeometryIncludingScaledPrimitiveMeshes()
        {
            foreach (var rig in rigs)
            foreach (var item in rig.BakedSources)
            {
                var mesh = new Mesh();
                try
                {
                    item.baked.BakeMesh(mesh, true);
                    var source = item.source.GetComponent<MeshFilter>();
                    var expected = source.sharedMesh.vertices;
                    var actual = mesh.vertices;
                    for (int i = 0; i < actual.Length; i++)
                        Assert.That(Vector3.Distance(item.baked.transform.TransformPoint(actual[i]), source.transform.TransformPoint(expected[i])),
                            Is.LessThan(.0001f), item.source.name + " vertex " + i);
                }
                finally { Object.DestroyImmediate(mesh); }
            }
        }

        [Test] public void BentKneesElbowsAndHipsKeepAllUvAndNormalSeamsClosed()
        {
            foreach (var rig in rigs)
            {
                var bones = rig.GeneratedBones.GetComponentsInChildren<Transform>();
                var rotations = bones.Select(t => t.localRotation).ToArray();
                try
                {
                    foreach (var bone in bones)
                        if (bone.name.EndsWith("UpperLeg")) bone.localRotation = Quaternion.Euler(-65, 0, 0);
                        else if (bone.name.EndsWith("LowerLeg")) bone.localRotation = Quaternion.Euler(75, 0, 0);
                        else if (bone.name.EndsWith("LowerArm")) bone.localRotation = Quaternion.Euler(0, 65, 0);
                    foreach (var item in rig.BakedSources) AssertClosedSeams(item.baked);
                }
                finally { for (int i = 0; i < bones.Length; i++) bones[i].localRotation = rotations[i]; }
            }
        }

        [Test] public void RebuildingWeightsHonoursPartsAndSharesSeams()
        {
            foreach (var rig in rigs)
            {
                var bones = RiggleBuilder.MakeBoneSpecs(rig).Where(b => b.skinned).ToList();
                var lookup = rig.GeneratedBones.GetComponentsInChildren<Transform>().ToDictionary(t => t.name);
                foreach (var bone in bones) bone.transform = lookup[bone.name];
                foreach (var source in rig.CollectSourceMeshes())
                {
                    var mesh = RiggleSkinner.BuildSkinnedMesh(rig, source, bones);
                    try
                    {
                        var topology = new RiggleTopology(mesh);
                        var parts = topology.GetParts(source.GetComponent<RiggleSkinBinding>());
                        var weights = mesh.boneWeights;
                        for (int i = 0; i < weights.Length; i++)
                        {
                            Assert.That(weights[i], Is.EqualTo(weights[topology.welded[i]]), source.name + " seam");
                            var w = weights[i];
                            Assert.That(w.weight0 + w.weight1 + w.weight2 + w.weight3, Is.EqualTo(1).Within(.00001f));
                            int[] indices = {w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3};
                            float[] values = {w.weight0, w.weight1, w.weight2, w.weight3};
                            for (int k = 0; k < 4; k++)
                            {
                                if (values[k] <= 0) continue;
                                string boneName = bones[indices[k]].name;
                                if (parts[i].ToString().StartsWith("Left")) Assert.That(boneName, Does.StartWith("Riggle_Left"));
                                if (parts[i].ToString().StartsWith("Right")) Assert.That(boneName, Does.StartWith("Riggle_Right"));
                                if (parts[i] == RiggleSkinBinding.Part.Head) Assert.That(boneName, Is.EqualTo("Riggle_Head"));
                                if (parts[i] == RiggleSkinBinding.Part.Hips) Assert.That(boneName, Is.EqualTo("Riggle_Hips"));
                            }
                        }
                    }
                    finally { Object.DestroyImmediate(mesh); }
                }
            }
        }

        [Test] public void HumanoidMusclesMoveTheRigWithoutOpeningSeams()
        {
            foreach (var rig in rigs)
            {
                var transforms = rig.GetComponentsInChildren<Transform>();
                var positions = transforms.Select(t => t.localPosition).ToArray();
                var rotations = transforms.Select(t => t.localRotation).ToArray();
                var scales = transforms.Select(t => t.localScale).ToArray();
                try
                {
                    var animator = rig.GetComponent<Animator>();
                    var arm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                    var before = arm.rotation;
                    using (var handler = new HumanPoseHandler(animator.avatar, rig.transform))
                    {
                        var pose = new HumanPose(); handler.GetHumanPose(ref pose);
                        pose.muscles[Array.IndexOf(HumanTrait.MuscleName, "Left Forearm Stretch")] = -.4f;
                        pose.muscles[Array.IndexOf(HumanTrait.MuscleName, "Left Lower Leg Stretch")] = -.2f;
                        pose.muscles[Array.IndexOf(HumanTrait.MuscleName, "Head Turn Left-Right")] = .4f;
                        handler.SetHumanPose(ref pose);
                        Assert.That(Quaternion.Angle(before, arm.rotation), Is.GreaterThan(20), rig.name);
                        foreach (var item in rig.BakedSources) AssertClosedSeams(item.baked);
                    }
                }
                finally
                {
                    for (int i = 0; i < transforms.Length; i++)
                    { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; transforms[i].localScale = scales[i]; }
                }
            }
        }

        [Test] public void SurfaceDistancesFollowTheConnectedStripAndDoNotJumpToNearbyIslands()
        {
            var mesh = new Mesh();
            try
            {
                mesh.vertices = new[] { new Vector3(0,0,0), new Vector3(0,0,.1f), new Vector3(0,3,0), new Vector3(0,3,.1f),
                    new Vector3(.2f,3,0), new Vector3(.2f,3,.1f), new Vector3(.2f,0,0), new Vector3(.2f,0,.1f),
                    new Vector3(.01f,0,0), new Vector3(.01f,.05f,0), new Vector3(.01f,0,.05f) };
                mesh.triangles = new[] {0,1,2,1,3,2,2,3,4,3,5,4,4,5,6,5,7,6,8,9,10};
                var seeds = Enumerable.Repeat(float.PositiveInfinity, 11).ToArray(); seeds[0] = 0;
                var graph = new RiggleSurfaceGraph(mesh, new RiggleTopology(mesh), Matrix4x4.identity);
                var distances = graph.Distances(seeds);
                Assert.That(distances[6], Is.GreaterThan(6), "A spatially close endpoint must be reached around the U.");
                Assert.That(distances[8], Is.EqualTo(float.PositiveInfinity), "A nearby disconnected triangle must stay unreachable.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test] public void ControlRigRefitPreservesOtherLayersAndDoesNotDuplicateControls()
        {
            foreach (var rig in rigs)
            {
                var animator = rig.GetComponent<Animator>();
                var builder = rig.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
                var extra = new GameObject("Unrelated animation layer"); extra.transform.SetParent(rig.transform, false);
                var other = extra.AddComponent<UnityEngine.Animations.Rigging.Rig>();
                builder.layers.Add(new UnityEngine.Animations.Rigging.RigLayer(other));
                try
                {
                    var first = ControlRig.ControlRigBuilder.Build(animator);
                    Assert.That(ControlRig.ControlRigBuilder.Build(animator), Is.SameAs(first));
                    Assert.That(rig.GetComponentsInChildren<ControlRig.Rig_References>().Length, Is.EqualTo(1));
                    Assert.That(builder.layers.Count(l => l.rig == other), Is.EqualTo(1));
                    foreach (var ik in first.GetComponentsInChildren<UnityEngine.Animations.Rigging.TwoBoneIKConstraint>())
                    {
                        Assert.That(Vector3.Distance(ik.data.tip.position, ik.data.target.position), Is.LessThan(.0001f));
                        Assert.That(Quaternion.Angle(ik.data.tip.rotation, ik.data.target.rotation), Is.LessThan(.01f));
                        Assert.That(Vector3.Distance(ik.data.mid.position, ik.data.hint.position), Is.GreaterThan(.05f));
                    }
                }
                finally { builder.layers.RemoveAll(l => l.rig == other); Object.DestroyImmediate(extra); }
            }
        }

        [Test] public void ClearRestoresOriginalsAndRemovesOnlyTheControlLayerBoundToDeletedBones()
        {
            foreach (var rig in rigs)
            {
                var originals = rig.BakedSources.Select(b => b.source).ToArray();
                var builder = rig.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
                var extra = new GameObject("Unrelated animation layer"); extra.transform.SetParent(rig.transform, false);
                var other = extra.AddComponent<UnityEngine.Animations.Rigging.Rig>();
                builder.layers.Add(new UnityEngine.Animations.Rigging.RigLayer(other));
                RiggleBuilder.Clear(rig);
                Assert.That(rig.GeneratedBones, Is.Null);
                Assert.That(rig.BakedSources, Is.Empty);
                Assert.That(rig.GetComponentInChildren<ControlRig.Rig_References>(), Is.Null);
                Assert.That(builder.layers.Count, Is.EqualTo(1));
                Assert.That(builder.layers[0].rig, Is.SameAs(other));
                foreach (var original in originals) Assert.That(original.enabled, Is.True);
            }
        }

        static void AssertClosedSeams(SkinnedMeshRenderer skin)
        {
            var topology = new RiggleTopology(skin.sharedMesh);
            var mesh = new Mesh();
            try
            {
                skin.BakeMesh(mesh, true); var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++)
                    Assert.That(Vector3.Distance(skin.transform.TransformPoint(vertices[i]), skin.transform.TransformPoint(vertices[topology.welded[i]])),
                        Is.LessThan(.0001f), skin.name + " seam vertex " + i);
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
