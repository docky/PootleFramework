using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.Animations.Rigging;
using UnityEditor.SceneManagement;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleAppendagePlayModeTests
    {
        [UnityTest] public IEnumerator FiveAppendageChainsFollowFkWhileHumanoidBodyKeepsSolving()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Appendages.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;yield return null;
            var rig=Object.FindObjectsByType<Riggle>().Single();var driver=rig.GetComponent<RiggleAppendageRig>();
            Assert.That(driver.chains.Length,Is.EqualTo(5));Assert.That(rig.GetComponent<RigBuilder>().graph.IsValid(),Is.True);
            var torso=rig.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Chest);var restTorso=torso.localRotation;
            var ends=driver.chains.Select(c=>c.bones.Last().position).ToArray();
            foreach(var chain in driver.chains)chain.controls[0].localRotation*=Quaternion.Euler(22,18,25);
            yield return null;yield return null;
            for(int c=0;c<driver.chains.Length;c++)
            {
                var chain=driver.chains[c];Assert.That(Vector3.Distance(ends[c],chain.bones.Last().position),Is.GreaterThan(.015),chain.name);
                for(int i=0;i<chain.bones.Length;i++)
                {
                    Assert.That(Quaternion.Angle(chain.bones[i].localRotation,chain.controls[i].localRotation),Is.LessThan(.05),chain.name);
                    Assert.That(Vector3.Distance(chain.bones[i].position,chain.controls[i].position),Is.LessThan(.001),chain.name);
                }
            }
            Assert.That(Quaternion.Angle(torso.localRotation,restTorso),Is.LessThan(.05));
            foreach(var skin in rig.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh=new Mesh();skin.BakeMesh(mesh);Assert.That(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),Is.True);Object.Destroy(mesh);
            }
        }
    }
}
