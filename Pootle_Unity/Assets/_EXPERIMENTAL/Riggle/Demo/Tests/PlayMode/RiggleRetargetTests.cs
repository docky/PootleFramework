using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.Animations.Rigging;
using ControlRig;
namespace Docky.RiggleTools.Tests
{
 public class RiggleRetargetTests
 {
  [UnityTest] public IEnumerator KawaiiRowsRetargetAndApplyScaledRootMotion()
  {
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Kawaii Retargeting.unity",new LoadSceneParameters(LoadSceneMode.Single));
   var samples=Object.FindObjectsByType<RiggleRetargetSample>();foreach(var s in samples)s.animate=false;
   yield return null;
   Assert.That(samples.Length,Is.EqualTo(56));
   float min=float.MaxValue,max=0;
   foreach(var s in samples)
   {
    s.Begin();var a=s.GetComponent<Animator>();Assert.That(a.avatar.isHuman,Is.True,s.name);
    Assert.That(s.GetComponent<RigBuilder>().enabled,Is.False,"Manual controls must not pin a retargeted pose");
    var root=s.transform.position;var rotation=s.transform.rotation;
    var hand=a.GetBoneTransform(HumanBodyBones.LeftHand);var initial=hand.position-a.GetBoneTransform(HumanBodyBones.Hips).position;
    for(int i=0;i<20;i++)s.Step(.025f);
    Assert.That(a.GetBoneTransform(HumanBodyBones.Hips).position.y,Is.InRange(-1f,5f));
    foreach(var skin in s.GetComponentsInChildren<SkinnedMeshRenderer>())
    {var mesh=new Mesh();skin.BakeMesh(mesh);Assert.That(mesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)),Is.True,s.name);Object.Destroy(mesh);}
    if(s.clip.name=="KA_Walk01")
    {
     var displacement=s.transform.position-root;Assert.That(Vector3.Dot(displacement,rotation*Vector3.forward),Is.GreaterThan(.2f));
     float scaled=displacement.magnitude/a.humanScale;min=Mathf.Min(min,scaled);max=Mathf.Max(max,scaled);
    }
    if(s.clip.name=="KA_Run01_TurnL")Assert.That(Quaternion.Angle(rotation,s.transform.rotation),Is.GreaterThan(3f),s.transform.parent.parent.name);
   }
   Assert.That(max-min,Is.LessThan(.035f),"Root displacement should scale consistently with the Humanoid");
  }
  [UnityTest] public IEnumerator AnimatorControllersAutomaticallyReleaseRigAndMoveRoot()
  {
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Kawaii Retargeting.unity",new LoadSceneParameters(LoadSceneMode.Single));
   var samples=Object.FindObjectsByType<RiggleRetargetSample>();foreach(var s in samples)s.animate=false;yield return null;
   var walkers=samples.Where(s=>s.clip.name=="KA_Walk01").ToArray();
   var starts=walkers.Select(s=>s.transform.position).ToArray();
   foreach(var s in walkers)
   {
    s.Stop();s.enabled=false;
    var a=s.GetComponent<Animator>();a.runtimeAnimatorController=UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Retarget Lab/Kawaii Walk.controller");
    var control=s.GetComponent<ControlRigAnimation>();control.poseSource=ControlRigAnimation.PoseSource.Automatic;control.Refresh();a.Rebind();
   }
   yield return new WaitForSeconds(.5f);
   for(int i=0;i<walkers.Length;i++)
   {
    Assert.That(Vector3.Distance(starts[i],walkers[i].transform.position),Is.GreaterThan(.12f),walkers[i].transform.parent.parent.name);
    Assert.That(walkers[i].GetComponent<RigBuilder>().enabled,Is.False);
   }
  }
  [UnityTest] public IEnumerator RenderedLimbsChangeAcrossPlaybackFrames()
  {
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Kawaii Retargeting.unity",new LoadSceneParameters(LoadSceneMode.Single));
   var samples=Object.FindObjectsByType<RiggleRetargetSample>();foreach(var s in samples)s.animate=false;yield return null;
   var sample=samples.First(s=>s.clip.name=="KA_Walk01" && s.transform.parent.parent.name.StartsWith("Fox Girl"));
   foreach(var other in samples.Where(s=>s!=sample))other.gameObject.SetActive(false);
   foreach(var label in Object.FindObjectsByType<TextMesh>())label.gameObject.SetActive(false);
   sample.moveRoot=false;sample.Begin();sample.Step(.1f);
   var animator=sample.GetComponent<Animator>();
   var limbs=new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg}.Select(animator.GetBoneTransform).ToArray();
   var before=limbs.Select(b=>b.localRotation).ToArray();
   var camera=Camera.main;camera.orthographicSize=1.3f;
   var center=sample.transform.position+Vector3.up*.9f;camera.transform.position=center+new Vector3(-3,1,-5);camera.transform.LookAt(center);
   var target=new RenderTexture(320,320,24);target.Create();camera.targetTexture=target;
   var image=new Texture2D(320,320,TextureFormat.RGB24,false);
   try
   {
    // A real player frame must run between poses: Camera.Render alone does not refresh GPU skinning.
    yield return null;
    camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,320,320),0,0);image.Apply();RenderTexture.active=previous;var pixels=image.GetPixels32();
    sample.Step(.4f);yield return null;
    camera.Render();previous=RenderTexture.active;RenderTexture.active=target;image.ReadPixels(new Rect(0,0,320,320),0,0);image.Apply();RenderTexture.active=previous;var after=image.GetPixels32();
    int changed=0;for(int i=0;i<pixels.Length;i++)if(Mathf.Abs(pixels[i].r-after[i].r)+Mathf.Abs(pixels[i].g-after[i].g)+Mathf.Abs(pixels[i].b-after[i].b)>45)changed++;
    Assert.That(Quaternion.Angle(before[0],limbs[0].localRotation)+Quaternion.Angle(before[1],limbs[1].localRotation),Is.GreaterThan(5),"Arms must animate independently of the root");
    Assert.That(Quaternion.Angle(before[2],limbs[2].localRotation)+Quaternion.Angle(before[3],limbs[3].localRotation),Is.GreaterThan(15),"Legs must animate independently of the root");
    Assert.That(changed,Is.GreaterThan(pixels.Length*.01f),"The visible skinned silhouette must change, not just the root transform");
   }
   finally { camera.targetTexture=null;target.Release();Object.Destroy(target);Object.Destroy(image); }
  }
  [UnityTest] public IEnumerator RootMotionCanBeDisabledAndManualControlsRestored()
  {
   yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/Riggle Kawaii Retargeting.unity",new LoadSceneParameters(LoadSceneMode.Single));
   var samples=Object.FindObjectsByType<RiggleRetargetSample>();foreach(var s in samples)s.animate=false;yield return null;
   var sample=samples.First(s=>s.clip.name=="KA_Walk01");sample.moveRoot=false;sample.Begin();var p=sample.transform.position;
   for(int i=0;i<20;i++)sample.Step(.025f);
   Assert.That(Vector3.Distance(p,sample.transform.position),Is.LessThan(.001f));sample.Stop();
   var controls=sample.GetComponent<ControlRigAnimation>();controls.poseSource=ControlRigAnimation.PoseSource.Controls;controls.Refresh();
   Assert.That(sample.GetComponent<RigBuilder>().enabled,Is.True);Assert.That(sample.GetComponentInChildren<Rig>().weight,Is.EqualTo(1));
  }
 }
}
