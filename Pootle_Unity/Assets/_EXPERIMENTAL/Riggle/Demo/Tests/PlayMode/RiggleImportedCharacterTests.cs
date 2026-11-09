using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.Animations.Rigging;
using UnityEditor.SceneManagement;
using ControlRig;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleImportedCharacterTests
    {
        [UnityTest] public IEnumerator ImportedCharactersSolveAndRetainPoseWhenSwitchingModes()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Imported Characters.unity",new LoadSceneParameters(LoadSceneMode.Single));
            yield return null; yield return null;
            var rigs=Object.FindObjectsByType<Riggle>();Assert.That(rigs.Length,Is.EqualTo(2));
            foreach(var rig in rigs)
            {
                Assert.That(rig.GetComponent<RigBuilder>().graph.IsValid(),Is.True,rig.name);
                var modes=rig.GetComponentInChildren<ControlRigModes>();
                foreach(var limb in modes.limbs)limb.ik.data.target.position+=(limb.ik.data.root.position-limb.ik.data.tip.position).normalized*.07f+Vector3.up*.025f;
            }
            yield return null;yield return null;
            foreach(var rig in rigs)
            {
                var modes=rig.GetComponentInChildren<ControlRigModes>();
                for(int i=0;i<modes.limbs.Length;i++)
                {
                    var limb=modes.limbs[i];Assert.That(Vector3.Distance(limb.ik.data.tip.position,limb.ik.data.target.position),Is.LessThan(.025f),rig.name+limb.label);
                    var joints=new[]{limb.ik.data.root,limb.ik.data.mid,limb.ik.data.tip};var before=joints.Select(j=>j.position).ToArray();
                    modes.SwitchMode(i,ControlRigModes.Mode.FK);yield return null;yield return null;
                    for(int j=0;j<3;j++)Assert.That(Vector3.Distance(before[j],joints[j].position),Is.LessThan(.002f),rig.name+" FK "+limb.label);
                    modes.SwitchMode(i,ControlRigModes.Mode.IK);yield return null;yield return null;
                    for(int j=0;j<3;j++)Assert.That(Vector3.Distance(before[j],joints[j].position),Is.LessThan(.002f),rig.name+" IK "+limb.label);
                }
                foreach(var source in rig.BakedSources)
                {
                    var mesh=new Mesh();source.baked.BakeMesh(mesh);
                    Assert.That(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),Is.True);
                    Object.Destroy(mesh);
                }
            }
        }
    }
}
