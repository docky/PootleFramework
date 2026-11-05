using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.DualShock.LowLevel;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
 public sealed partial class PootlePlayModeTests
 {
  [UnityTest]
  public IEnumerator ShadowRun_DualSenseL1HeldOnEnableRunsAndReleaseWalks()
  {
   var pad=InputSystem.AddDevice<DualSenseGamepadHID>();
   try
   {
    character.simulateAutomatically=false;character.outputCamera=null;
    character.input.enabled=false;
    var state=new DualSenseHIDInputReport{leftStickX=128,leftStickY=0,rightStickX=128,rightStickY=128,buttons0=8,buttons1=1};
    InputSystem.QueueStateEvent(pad,state);yield return null;
    character.input.enabled=true;yield return null;yield return null;
    Assert.That(character.input.Sample().run,Is.True,"Held L1 must be detected after enable");
    character.Teleport(new Vector3(150,.03f,280),Quaternion.identity);
    for(int i=0;i<90;i++){Physics.SyncTransforms();character.Tick(1f/60,character.input.Sample());}
    Assert.That(character.IsRunning);Assert.That(character.Velocity.z,Is.EqualTo(12).Within(.15f));
    state.buttons1=0;InputSystem.QueueStateEvent(pad,state);yield return null;yield return null;
    for(int i=0;i<90;i++){Physics.SyncTransforms();character.Tick(1f/60,character.input.Sample());}
    Assert.That(character.IsRunning,Is.False);Assert.That(character.Velocity.z,Is.EqualTo(7.5f).Within(.15f));
   }
   finally{InputSystem.RemoveDevice(pad);}
  }
  [UnityTest]
  public IEnumerator ShadowRun_BodyMaskProjectsAndDisappearsBeyondRange()
  {
   var blob=character.GetComponent<PootleBlobShadow>();Assert.That(blob,Is.Not.Null);
   character.simulateAutomatically=false;character.Teleport(new Vector3(150,.03f,280),Quaternion.identity);
   yield return null;yield return null;
   Assert.That(blob.CircleCount,Is.GreaterThanOrEqualTo(13));Assert.That(blob.Mask.IsCreated());
   Assert.That(blob.ProjectionActive);
   var pixels=new Texture2D(blob.Mask.width,blob.Mask.height,TextureFormat.RGBA32,false);var previous=RenderTexture.active;
   try
   {
    RenderTexture.active=blob.Mask;pixels.ReadPixels(new Rect(0,0,blob.Mask.width,blob.Mask.height),0,0);pixels.Apply();
    int dark=0,clear=0;foreach(var p in pixels.GetPixels32()){if(p.a>100)dark++;if(p.a==0)clear++;}
    Assert.That(dark,Is.GreaterThan(100));Assert.That(clear,Is.GreaterThan(1000));
   }
   finally{RenderTexture.active=previous;Object.Destroy(pixels);}
   character.Teleport(new Vector3(150,50,280),Quaternion.identity);yield return null;
   Assert.That(blob.ProjectionActive); // Range clipping is verified by rendered-pixel tests.
   var renderer=character.visualRoot.GetComponentInChildren<SkinnedMeshRenderer>();
   Assert.That(renderer.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
   blob.enabled=false;yield return null;
   Assert.That(renderer.shadowCastingMode,Is.Not.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
  }
 }
}
