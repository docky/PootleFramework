using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Animations.Rigging;
using ControlRig;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleControlRigTests
    {
        Riggle[] rigs;
        [UnitySetUp] public IEnumerator Load()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_EXPERIMENTAL/Riggle/riggle test.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor scene checks"); yield break;
#endif
            yield return null;
            rigs = Object.FindObjectsByType<Riggle>(); Assert.That(rigs.Length, Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator BothControlGraphsStartWithoutMovingFeetOrHandsFromTheirTargets()
        {
            yield return null;
            foreach (var rig in rigs)
            {
                Assert.That(rig.GetComponent<RigBuilder>().graph.IsValid(), Is.True, rig.name);
                Assert.That(rig.GetComponent<RiggleHumanoidPreview>().enabled, Is.False, "The muscle preview must not override IK.");
                foreach (var ik in rig.GetComponentsInChildren<TwoBoneIKConstraint>())
                    Assert.That(Vector3.Distance(ik.data.target.position, ik.data.tip.position), Is.LessThan(.025f), rig.name + " " + ik.name);
            }
        }
        [UnityTest] public IEnumerator HandAndFootTargetsDriveBothCharactersThroughAnimationRigging()
        {
            foreach (var rig in rigs)
            foreach (var ik in rig.GetComponentsInChildren<TwoBoneIKConstraint>())
            {
                Vector3 desired = ik.data.target.position + (ik.data.root.position - ik.data.tip.position).normalized * .12f + rig.transform.up * .04f;
                ik.data.target.position = desired;
            }
            yield return null; yield return null;
            foreach (var rig in rigs)
            foreach (var ik in rig.GetComponentsInChildren<TwoBoneIKConstraint>())
                Assert.That(Vector3.Distance(ik.data.target.position, ik.data.tip.position), Is.LessThan(.025f), rig.name + " " + ik.name);
        }
        [UnityTest] public IEnumerator HipAndHeadControlsAffectMappedHumanoidBones()
        {
            var refs = rigs.Select(r => r.GetComponentInChildren<Rig_References>()).ToArray();
            var original = refs.Select(r => r.headRig.data.constrainedObject.rotation).ToArray();
            foreach (var r in refs)
            {
                r.hipRig.data.sourceObjects[0].transform.position += Vector3.up * .06f;
                r.headRig.data.sourceObjects[0].transform.position += r.GetComponentInParent<Animator>().transform.right * .6f;
            }
            yield return null; yield return null;
            for (int i = 0; i < refs.Length; i++)
            {
                var r = refs[i];
                Assert.That(Vector3.Distance(r.hipRig.data.sourceObjects[0].transform.position, r.hipRig.data.constrainedObject.position), Is.LessThan(.01f));
                Assert.That(Quaternion.Angle(original[i], r.headRig.data.constrainedObject.rotation), Is.GreaterThan(5));
            }
        }
    }
}
