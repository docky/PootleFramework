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
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_EXPERIMENTAL/Riggle/Demo/Generated/"+name+"/"+name+"_Skinned.prefab");
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
  [TestCase(0f)] [TestCase(180f)]
  public void APoseReferenceCalibrationPreservesBindPoseAndFlexesForearmsForward(float yaw)
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Base Mesh/Base Mesh_Skinned.prefab");
   var go=Object.Instantiate(prefab);Avatar avatar=null;HumanPoseHandler handler=null;
   try
   {
    var a=go.GetComponent<Animator>();var bones=go.transform.Find("Riggle_Bones");
    var transforms=bones.GetComponentsInChildren<Transform>();var rotations=System.Array.ConvertAll(transforms,t=>t.localRotation);
    var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;var bind=mesh.bindposes;
    go.transform.SetPositionAndRotation(new Vector3(3,0,2),Quaternion.Euler(0,yaw,0));
    avatar=Docky.RiggleTools.Editor.RiggleBuilder.BuildHumanoidAvatar(go,bones,a.avatar.humanDescription.human);
    Assert.That(avatar.isHuman && avatar.isValid,Is.True);
    for(int i=0;i<transforms.Length;i++)Assert.That(Quaternion.Angle(rotations[i],transforms[i].localRotation),Is.LessThan(.001f),"Calibration must preserve the authored bind pose");
    Assert.That(go.transform.position,Is.EqualTo(new Vector3(3,0,2)));Assert.That(mesh.bindposes,Is.EqualTo(bind));
    a.avatar=avatar;go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);handler=new HumanPoseHandler(avatar,go.transform);var pose=new HumanPose();handler.GetHumanPose(ref pose);pose.muscles=new float[HumanTrait.MuscleCount];go.transform.SetPositionAndRotation(new Vector3(3,0,2),Quaternion.Euler(0,yaw,0));
    foreach(string side in new[]{"Left","Right"})pose.muscles[System.Array.IndexOf(HumanTrait.MuscleName,side+" Forearm Stretch")]=-.7f;
    RiggleHumanoidPreview.ApplyLocalPose(handler,ref pose,go.transform);
    foreach(var ids in new[]{new[]{HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftHand},new[]{HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand}})
    {var forearm=a.GetBoneTransform(ids[1]).position-a.GetBoneTransform(ids[0]).position;Assert.That(Vector3.Dot(forearm.normalized,go.transform.forward),Is.GreaterThan(.15f),"A-pose elbows must not fold the hands backwards through the torso");}
   }
   finally{handler?.Dispose();Object.DestroyImmediate(go);if(avatar)Object.DestroyImmediate(avatar);}
  }
  [Test] public void RiggleControlTemplateIsVariantWithoutUnboundEffectors()
  {
   var prefab=Resources.Load<GameObject>("Riggle Control Rig");Assert.That(PrefabUtility.GetPrefabAssetType(prefab),Is.EqualTo(PrefabAssetType.Variant));
   foreach(var e in prefab.GetComponent<UnityEngine.Animations.Rigging.Rig>().effectors)Assert.That(e.transform,Is.Not.Null);
  }
 }
}
