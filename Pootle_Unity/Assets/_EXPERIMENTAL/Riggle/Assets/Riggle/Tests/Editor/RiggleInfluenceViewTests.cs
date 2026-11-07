using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Docky.RiggleTools.Editor;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    public class RiggleInfluenceViewTests
    {
        [Test] public void SurveyUsesBoneIdentityAcrossMeshesAndLeavesAssetsAndPoseUntouched()
        {
            var root = new GameObject("Influence survey test");
            var meshA = new Mesh(); var meshB = new Mesh(); bool hidden = Tools.hidden;
            try
            {
                var rig = root.AddComponent<Riggle>();
                var arm = new GameObject("Arm").transform; arm.SetParent(root.transform); arm.localRotation = Quaternion.Euler(10, 20, 30);
                var leg = new GameObject("Leg").transform; leg.SetParent(root.transform);
                meshA.vertices = meshB.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                meshA.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0=0, weight0=.75f, boneIndex1=1, weight1=.25f },3).ToArray();
                meshB.boneWeights = Enumerable.Repeat(new BoneWeight { boneIndex0=0, weight0=1 },3).ToArray();
                var skinA = new GameObject("A").AddComponent<SkinnedMeshRenderer>(); skinA.transform.SetParent(root.transform);
                var skinB = new GameObject("B").AddComponent<SkinnedMeshRenderer>(); skinB.transform.SetParent(root.transform);
                skinA.sharedMesh = meshA; skinB.sharedMesh = meshB;
                skinA.bones = new[] { arm, leg }; skinB.bones = new[] { leg, arm };
                rig.BakedSources.Add(new Riggle.BakedSource { baked=skinA });
                rig.BakedSources.Add(new Riggle.BakedSource { baked=skinA }); // Merged sources share a renderer.
                rig.BakedSources.Add(new Riggle.BakedSource { baked=skinB });
                var weightsA = meshA.boneWeights; var weightsB = meshB.boneWeights; var rotation = arm.localRotation;
                using (var view = new RiggleInfluenceView(rig, null))
                {
                    view.Active = true; view.Selected = arm;
                    Assert.That(view.Skins.Length, Is.EqualTo(2), "Merged output must not count twice");
                    Assert.That(RiggleInfluenceView.Weights(skinA,arm), Is.All.EqualTo(.75f));
                    Assert.That(RiggleInfluenceView.Weights(skinB,arm), Is.All.Zero, "The same bone can have a different index in another mesh");
                    view.Selected = leg;
                    Assert.That(RiggleInfluenceView.Weights(skinB,leg), Is.All.EqualTo(1));
                    Assert.That(Tools.hidden, Is.True);
                }
                Assert.That(Tools.hidden, Is.EqualTo(hidden), "Closing the viewer restores editor handles");
                Assert.That(arm.localRotation, Is.EqualTo(rotation));
                Assert.That(meshA.boneWeights, Is.EqualTo(weightsA)); Assert.That(meshB.boneWeights, Is.EqualTo(weightsB));
                Assert.That(skinA.sharedMesh, Is.SameAs(meshA)); Assert.That(skinB.sharedMesh, Is.SameAs(meshB));
                Assert.That(root.GetComponentsInChildren<RiggleMeshEdits>(), Is.Empty);
            }
            finally { Tools.hidden=hidden; Object.DestroyImmediate(root); Object.DestroyImmediate(meshA); Object.DestroyImmediate(meshB); }
        }
    }
}
