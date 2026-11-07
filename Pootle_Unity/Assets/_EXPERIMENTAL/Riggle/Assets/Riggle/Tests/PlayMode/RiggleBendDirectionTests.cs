using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using UnityEngine.Animations.Rigging;
using ControlRig;
namespace Docky.RiggleTools.Tests
{
 public class RiggleBendDirectionTests
 {
  [UnityTest] public IEnumerator LimbsKeepAnatomicalBendThroughDeepAndExtendedTargets()
  {
   foreach(var path in new[]{"Assets/_EXPERIMENTAL/Riggle/Riggle Imported Characters.unity","Assets/_EXPERIMENTAL/Riggle/Riggle Appendages.unity"})
   {
    yield return EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));yield return null;yield return null;
    foreach(var rig in Object.FindObjectsByType<Riggle>())
    {
     var anatomy=rig.GetComponent<ControlRigAnatomy>();
     var refs=rig.GetComponentInChildren<Rig_References>();
     foreach(var ik in new[]{refs.LeftLegRig,refs.RightLegRig,refs.LeftHandRig,refs.RightHandRig})
     {
      var d=ik.data;var start=d.target.position;var rest=(d.tip.position-d.root.position).normalized;
      var length=Vector3.Distance(d.root.position,d.mid.position)+Vector3.Distance(d.mid.position,d.tip.position);
      bool leg=ik==refs.LeftLegRig || ik==refs.RightLegRig;
      var forward=anatomy.Forward*(leg?1:-1);
      foreach(float reach in new[]{1.1f,.99f,.8f,.4f,.08f,.4f,.8f,.99f,1.1f,.8f})
      {
       d.target.position=d.root.position+rest*length*reach+anatomy.Forward*length*.12f;
       yield return null;yield return null;
       var axis=(d.tip.position-d.root.position).normalized;
       var bend=Vector3.ProjectOnPlane(d.mid.position-d.root.position,axis);
       var expected=Vector3.ProjectOnPlane(forward,axis).normalized;
       if(bend.magnitude>length*.04f)Assert.That(Vector3.Dot(bend.normalized,expected),Is.GreaterThan(.1f),rig.name+" "+ik.name+" reach="+reach);
       Assert.That(Vector3.Distance(d.root.position,d.mid.position),Is.LessThan(length),"finite joint length");
      }
      d.target.position=start;yield return null;yield return null;
     }
    }
   }
  }
 }
}
