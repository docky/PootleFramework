// Run with Unity Pipeline script run, entry RiggleRetargetVideo.Main.
// Kept outside Assets: this recording helper does not ship in players.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using Docky.RiggleTools;
using Object=UnityEngine.Object;
public static class RiggleRetargetVideo
{
 const int W=1920,H=1080,Fps=24,Total=90*Fps;
 static readonly string OutputFolder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Builds","Riggle Showcase"));
 static readonly string Output=Path.Combine(OutputFolder,"riggle-kawaii-retargeting-showcase-90s.mp4");
 static readonly string Checks=Path.Combine(OutputFolder,"frames");
 static readonly string Status=Path.Combine(OutputFolder,"recording-status.txt");
 static RiggleRetargetSample[] samples;static AnimationClip[] originals;static Dictionary<AnimationClip,AnimationClip> clips;
 sealed class SkinFrame { public SkinnedMeshRenderer source; public Mesh mesh; public MeshRenderer renderer; }
 static Dictionary<RiggleRetargetSample,SkinFrame[]> skins;
 static Camera camera;static GameObject stage;static RenderTexture target;static Texture2D frame;static MediaEncoder encoder;
 static TextMesh title,subtitle,footer;static TextMesh[] labels;static int frames,phase=-1;static float[] elapsed;static string scene;static bool active;
 public static string Main()
 {
  if(EditorApplication.isPlaying)throw new Exception("Record from the saved comparison scene in Edit Mode.");
  var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();scene=current.path;if(current.isDirty)throw new Exception("Save the comparison scene before recording.");
  samples=Object.FindObjectsByType<RiggleRetargetSample>();if(samples.Length!=56)throw new Exception("Open the Kawaii retarget scene first.");
  originals=samples.Select(s=>s.clip).ToArray();clips=new Dictionary<AnimationClip,AnimationClip>();elapsed=new float[samples.Length];
  for(int i=0;i<samples.Length;i++)
  {
   if(!clips.TryGetValue(originals[i],out var clip)){clip=Object.Instantiate(originals[i]);clip.name=originals[i].name;var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);clips[originals[i]]=clip;}
   samples[i].clip=clip;samples[i].animate=false;samples[i].Begin();
  }
  // Manual Editor graph evaluation advances bones but does not run the player
  // skinning phase. Bake the evaluated pose for every recorded frame.
  skins=samples.ToDictionary(s=>s,s=>s.GetComponentsInChildren<SkinnedMeshRenderer>().Select(source=>{
   var go=new GameObject("Offline skinned frame"){hideFlags=HideFlags.HideAndDontSave};go.transform.SetParent(source.transform,false);
   var mesh=new Mesh();go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterials=source.sharedMaterials;
   renderer.shadowCastingMode=source.shadowCastingMode;renderer.receiveShadows=source.receiveShadows;source.enabled=false;
   return new SkinFrame{source=source,mesh=mesh,renderer=renderer};
  }).ToArray());
  labels=Object.FindObjectsByType<TextMesh>();
  stage=new GameObject("Showcase camera · temporary"){hideFlags=HideFlags.HideAndDontSave};camera=stage.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;
  title=Text("title");subtitle=Text("subtitle");footer=Text("footer");
  target=new RenderTexture(W,H,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;frame=new Texture2D(W,H,TextureFormat.RGBA32,false);
  Directory.CreateDirectory(OutputFolder);
  encoder=new MediaEncoder(Output,new VideoTrackEncoderAttributes(new H264EncoderAttributes{gopSize=48,numConsecutiveBFrames=2,profile=VideoEncodingProfile.H264Main}){frameRate=new MediaRational(Fps),width=W,height=H,includeAlpha=false,targetBitRate=7000000,bitRateMode=VideoBitrateMode.High});
  Directory.CreateDirectory(Checks);frames=0;phase=-1;active=true;EditorApplication.update+=Tick;return Output;
 }
 static TextMesh Text(string name){var go=new GameObject(name);go.transform.SetParent(camera.transform,false);go.hideFlags=HideFlags.HideAndDontSave;var text=go.AddComponent<TextMesh>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=64;text.anchor=TextAnchor.UpperLeft;text.color=Color.white;go.GetComponent<Renderer>().sharedMaterial=text.font.material;return text;}
 static string Name(RiggleRetargetSample s)=>s.transform.parent.parent.name.Replace(" · Kawaii row","").Replace("model_doggy_avatar_sketch","Dog").Replace("Riggle Demo - Bean Biped","Bean biped");
 static RiggleRetargetSample Find(string character,string clip)=>samples.First(s=>Name(s)==character && s.clip.name==clip);
 static void Tick()
 {
  if(!active)return;
  try
  {
   float t=frames/(float)Fps;int next=t<8?0:t<29?1:t<44?2:t<56?3:t<74?4:t<82?5:6;
   int row=next==1?Mathf.Min(6,(int)((t-8)/3)):0;
   string[] foxClips={"KA_Idle18_Shy","KA_Idle20_TriplePose","KA_Idle23_Dance03"};string[] bunnyClips={"KA_Idle25_Cheers","KA_Idle23_Dance03","KA_Idle30_Stretch2"};string[] motionClips={"KA_Walk01","KA_Run01","KA_Run01_TurnL"};
   var focus=next==2?Find("Fox Girl",foxClips[Mathf.Min(2,(int)((t-29)/5))]):next==3?Find("Winged Bunny",bunnyClips[Mathf.Min(2,(int)((t-44)/4))]):next==4?Find("Fox Girl",motionClips[Mathf.Min(2,(int)((t-56)/6))]):next==5?Find("Dog","KA_Walk01"):null;
   int shot=next*100+(next==1?row:focus?Array.IndexOf(samples,focus)+10:0);
   if(phase!=shot){phase=shot;for(int i=0;i<samples.Length;i++){samples[i].Begin();elapsed[i]=0;
    if(next<4 && !samples[i].clip.name.StartsWith("KA_Walk") && !samples[i].clip.name.StartsWith("KA_Run"))
    {float warm=samples[i].clip.length*.22f;while(warm>0){float dt=Mathf.Min(1f/Fps,warm);samples[i].Step(dt);elapsed[i]+=dt;warm-=dt;}}
   }}
   var rowGroup=next==1?samples.First(s=>Mathf.Abs(s.transform.parent.position.z-row*5)<.1f).transform.parent.parent:null;
   foreach(var label in labels)label.GetComponent<Renderer>().enabled=next==0 || next==6 || next==1 && label.transform.IsChildOf(rowGroup) && label.transform.parent!=rowGroup;
   for(int i=0;i<samples.Length;i++)
   {
    var s=samples[i];bool continuous=(next==4 || next==5)&&s==focus;
    if(!continuous && elapsed[i]>=s.clip.length){s.Begin();elapsed[i]=0;}
    s.Step(1f/Fps);elapsed[i]+=1f/Fps;
    var twist=s.GetComponent<RiggleTwistDistribution>();if(twist)twist.Evaluate();var corrective=s.GetComponent<RiggleCorrectives>();if(corrective)corrective.Evaluate();
    bool visible=focus?s==focus:next!=1 || Mathf.Abs(s.transform.parent.position.z-row*5)<.1f;
    foreach(var renderer in s.GetComponentsInChildren<Renderer>())renderer.enabled=visible && !(renderer is SkinnedMeshRenderer);
    foreach(var skin in skins[s]){skin.renderer.enabled=visible;if(visible)skin.source.BakeMesh(skin.mesh,true);}
   }
   if(next==0 || next==6){camera.orthographicSize=16.5f;camera.transform.position=new Vector3(10,34,-24);camera.transform.LookAt(new Vector3(10,0,14));title.text="Riggle / Kawaii retargeting";subtitle.text="7 characters  ·  8 animations  ·  shared Humanoid clips";}
   else if(next==1){camera.orthographicSize=8.1f;camera.transform.position=new Vector3(11.7f,4.5f,row*5-15);camera.transform.LookAt(new Vector3(11.7f,.7f,row*5));title.text=samples.Where(s=>Mathf.Abs(s.transform.parent.position.z-row*5)<.1f).Select(Name).First();subtitle.text="Shy  /  Triple pose  /  Dance  /  Cheers  /  Stretch  /  Walk  /  Run  /  Turn";}
   else
   {
    float size=next==3?1.85f:next==5?1.3f:1.35f;camera.orthographicSize=size;
    var center=focus.transform.position+Vector3.up*(next==3?1.25f:.9f);
    camera.transform.position=center+new Vector3(-2.8f+Mathf.Sin(t*.3f)*.3f,1.1f,-4.5f);camera.transform.LookAt(center);
    title.text=Name(focus)+" / "+focus.clip.name.Replace("KA_Idle","").Replace("KA_","").Replace('_',' ');
    subtitle.text=next==4 || next==5?"Root motion drives travel and turning":"The same animation, retargeted to different proportions";
   }
   float h=camera.orthographicSize,w=h*W/H;
   title.transform.localPosition=new Vector3(-w*.94f,h*.93f,2);title.characterSize=.017f*h;
   subtitle.transform.localPosition=new Vector3(-w*.94f,h*.80f,2);subtitle.characterSize=.010f*h;
   footer.transform.localPosition=new Vector3(-w*.94f,-h*.85f,2);footer.characterSize=.010f*h;
   footer.text="RIGGLE SHOWCASE   ·   "+Mathf.FloorToInt(t).ToString("00")+" / 90 s"+(next==4 || next==5?"   ·   clip-driven root motion":"");
   camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,W,H),0,0);frame.Apply(false);RenderTexture.active=previous;encoder.AddFrame(frame);
   if(new[]{4,11,25,32,33,48,49,57,58,59,60,68,78,86}.Contains(frames/Fps)&&frames%Fps==0)File.WriteAllBytes(Path.Combine(Checks,"frame-"+(frames/Fps)+".png"),frame.EncodeToPNG());
   frames++;if(frames%24==0)File.WriteAllText(Status,frames+" / "+Total);if(frames>=Total)Finish(null);
  }catch(Exception e){Finish(e.ToString());}
 }
 static void Finish(string error)
 {
  active=false;EditorApplication.update-=Tick;encoder?.Dispose();encoder=null;
  for(int i=0;i<samples.Length;i++){samples[i].Stop();samples[i].clip=originals[i];}
  foreach(var skin in skins.Values.SelectMany(v=>v)){Object.DestroyImmediate(skin.renderer.gameObject);Object.DestroyImmediate(skin.mesh);}
  camera.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(frame);Object.DestroyImmediate(stage);foreach(var c in clips.Values)Object.DestroyImmediate(c);
  EditorSceneManager.OpenScene(scene);File.WriteAllText(Status,error??"Complete: "+frames+" frames / 90 seconds / "+Output);if(error!=null)Debug.LogError(error);
 }
}
