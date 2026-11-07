using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ControlRig;
namespace Docky.RiggleTools.Tests
{
 public class RiggleAnatomyTests
 {
  [TestCase("Fox Girl",0f)] [TestCase("Fox Girl",180f)]
  [TestCase("Winged Bunny",70f)] [TestCase("Base Mesh",180f)]
  public void ExportedHumanoidFlexesKneesForwardIndependentOfPlacement(string name,float yaw)
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RiggleGenerated/"+name+"/"+name+"_Skinned.prefab");
   var go=Object.Instantiate(prefab);HumanPoseHandler handler=null;
   try
   {
    var a=go.GetComponent<Animator>();
    handler=new HumanPoseHandler(a.avatar,go.transform);var pose=new HumanPose();handler.GetHumanPose(ref pose);
    foreach(var side in new[]{"Left","Right"})pose.muscles[System.Array.IndexOf(HumanTrait.MuscleName,side+" Lower Leg Stretch")]=-.7f;
    go.transform.rotation=Quaternion.Euler(0,yaw,0);RiggleHumanoidPreview.ApplyLocalPose(handler,ref pose,go.transform);
    foreach(var ids in new[]{new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot},new[]{HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot}})
    {var r=a.GetBoneTransform(ids[0]);var m=a.GetBoneTransform(ids[1]);var t=a.GetBoneTransform(ids[2]);var bend=Vector3.ProjectOnPlane(m.position-r.position,(t.position-r.position).normalized);Assert.That(Vector3.Dot(bend,go.transform.forward),Is.GreaterThan(.03f),name+" "+ids[1]);}
   }
   finally{handler?.Dispose();Object.DestroyImmediate(go);}
  }
  [Test] public void RiggleControlTemplateIsVariantWithoutUnboundEffectors()
  {
   var prefab=Resources.Load<GameObject>("Riggle Control Rig");Assert.That(PrefabUtility.GetPrefabAssetType(prefab),Is.EqualTo(PrefabAssetType.Variant));
   foreach(var e in prefab.GetComponent<UnityEngine.Animations.Rigging.Rig>().effectors)Assert.That(e.transform,Is.Not.Null);
  }
 }
}
