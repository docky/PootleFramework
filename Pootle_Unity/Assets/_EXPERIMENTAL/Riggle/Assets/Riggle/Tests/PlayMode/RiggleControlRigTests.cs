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
        [UnityTest] public IEnumerator CorrectiveShapesFollowLiveFkBendsAndReleaseAtRest()
        {
            foreach (var rig in rigs)
            {
                var driver = rig.GetComponent<RiggleCorrectives>();
                Assert.That(driver.bindings.Length, Is.EqualTo(4));
                foreach (var binding in driver.bindings) Assert.That(binding.skin.GetBlendShapeWeight(binding.Index), Is.EqualTo(0).Within(.001));
                var modes = rig.GetComponentInChildren<ControlRigModes>();
                var bindingLeft = driver.bindings.First(b => b.bone == modes.limbs[0].ik.data.root);
                modes.SwitchMode(0, ControlRigModes.Mode.FK);
                modes.limbs[0].fk[0].rotation = bindingLeft.bone.parent.rotation * bindingLeft.restRotation * Quaternion.AngleAxis(65, bindingLeft.axis);
            }
            yield return null; yield return null;
            foreach (var rig in rigs)
            {
                var driver = rig.GetComponent<RiggleCorrectives>(); var modes = rig.GetComponentInChildren<ControlRigModes>();
                var binding = driver.bindings.First(b => b.bone == modes.limbs[0].ik.data.root);
                Assert.That(binding.skin.GetBlendShapeWeight(binding.Index), Is.GreaterThan(50));
                var on = new Mesh(); var off = new Mesh();
                try
                {
                    binding.skin.BakeMesh(on, true); driver.enabled = false; binding.skin.BakeMesh(off, true);
                    Assert.That(on.vertices.Zip(off.vertices,(a,b)=>Vector3.Distance(a,b)).Max(), Is.GreaterThan(.0001f));
                    foreach (var b in driver.bindings) Assert.That(b.skin.GetBlendShapeWeight(b.Index), Is.Zero);
                }
                finally { Object.Destroy(on); Object.Destroy(off); }
                driver.enabled = true;
                modes.limbs[0].fk[0].rotation = binding.bone.parent.rotation * binding.restRotation;
            }
            yield return null; yield return null;
            foreach (var rig in rigs)
                foreach (var b in rig.GetComponent<RiggleCorrectives>().bindings)
                    Assert.That(b.skin.GetBlendShapeWeight(b.Index), Is.EqualTo(0).Within(.001));
        }
        [UnityTest] public IEnumerator IkFkRoundTripPreservesEveryJointAndFkControlsDriveTheBones()
        {
            foreach(var rig in rigs)
            {
                var modes=rig.GetComponentInChildren<ControlRigModes>();
                foreach(var limb in modes.limbs)
                    limb.ik.data.target.position+=(limb.ik.data.root.position-limb.ik.data.tip.position).normalized*.15f+rig.transform.up*.05f;
            }
            yield return null; yield return null;
            foreach(var rig in rigs)
            {
                var modes=rig.GetComponentInChildren<ControlRigModes>();
                for(int i=0;i<modes.limbs.Length;i++)
                {
                    var limb=modes.limbs[i]; var bones=new[]{limb.ik.data.root,limb.ik.data.mid,limb.ik.data.tip};
                    var positions=bones.Select(t=>t.position).ToArray();var rotations=bones.Select(t=>t.rotation).ToArray();
                    modes.SwitchMode(i,ControlRigModes.Mode.FK);
                    yield return null;yield return null;
                    for(int b=0;b<3;b++)
                    {Assert.That(Vector3.Distance(positions[b],bones[b].position),Is.LessThan(.002f),limb.label+" IK to FK position");Assert.That(Quaternion.Angle(rotations[b],bones[b].rotation),Is.LessThan(.2f),rig.name+" "+limb.label+" bone "+b+" IK to FK rotation before="+rotations[b].eulerAngles+" after="+bones[b].rotation.eulerAngles+" control="+limb.fk[b].rotation.eulerAngles);}
                    limb.fk[1].localRotation*=Quaternion.Euler(12,7,5);
                    yield return null;yield return null;
                    Assert.That(Quaternion.Angle(rotations[1],bones[1].rotation),Is.GreaterThan(5));
                    positions=bones.Select(t=>t.position).ToArray();rotations=bones.Select(t=>t.rotation).ToArray();
                    modes.SwitchMode(i,ControlRigModes.Mode.IK);
                    yield return null;yield return null;
                    for(int b=0;b<3;b++)
                    {Assert.That(Vector3.Distance(positions[b],bones[b].position),Is.LessThan(.002f),limb.label+" FK to IK position");Assert.That(Quaternion.Angle(rotations[b],bones[b].rotation),Is.LessThan(.2f),limb.label+" FK to IK rotation");}
                }
            }
        }
    }
}
