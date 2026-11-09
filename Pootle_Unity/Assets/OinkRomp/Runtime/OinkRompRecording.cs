#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Media;
using Unity.Collections;
using Pootle;
namespace OinkRomp
{
    [InitializeOnLoad,DefaultExecutionOrder(2000)]
    public sealed class OinkRompRecording : MonoBehaviour
    {
        const string Key="OinkRompRecord";
        const int Width=1920,Height=1080,Fps=30,Frames=90*Fps;
        public static string Folder=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/oinkromp"));
        MediaEncoder encoder;RenderTexture target;Texture2D frame;float oldCapture;
        OinkRompController pig;Camera camera;int count,section=-1;bool audioStarted;
        Vector2 smoothMove,previousTarget;
        readonly List<string> evidence=new List<string>();
        static OinkRompRecording(){EditorApplication.playModeStateChanged+=OnPlay;}
        public static void Arm(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Arm in Edit Mode.");SessionState.SetBool(Key,true);}
        static void OnPlay(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            SessionState.EraseBool(Key);var pig=FindAnyObjectByType<OinkRompController>();
            if(!pig){Debug.LogError("Open OinkRomp first.");return;}
            pig.gameObject.AddComponent<OinkRompRecording>().Begin(pig);
        }
        void Begin(OinkRompController source)
        {
            pig=source;pig.simulateAutomatically=false;camera=Camera.main;Directory.CreateDirectory(Folder);
            encoder=new MediaEncoder(Path.Combine(Folder,"oinkromp-coastal-revision-2026-11-08.mp4"),new VideoTrackEncoderAttributes(new H264EncoderAttributes{gopSize=60,numConsecutiveBFrames=2,profile=VideoEncodingProfile.H264Main}){frameRate=new MediaRational(Fps),width=Width,height=Height,targetBitRate=8000000,bitRateMode=VideoBitrateMode.High},new AudioTrackAttributes{sampleRate=new MediaRational(AudioSettings.outputSampleRate),channelCount=2,language="en"});
            target=new RenderTexture(Width,Height,24);target.Create();frame=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/Fps;audioStarted=AudioRenderer.Start();
        }
        void Drive(Vector3 world,bool press=false,bool held=false,float speed=1)
        {
            var forward=PootleMovement.CameraDirection(Vector2.up,pig.Character.outputCamera,out _);var right=Vector3.Cross(Vector3.up,forward);
            Vector2 wanted=new Vector2(Vector3.Dot(world,right),Vector3.Dot(world,forward))*speed;wanted=Vector2.ClampMagnitude(wanted,1);
            var tune=pig.Character.input.digitalMovement;
            bool turning=wanted.sqrMagnitude>0&&smoothMove.sqrMagnitude>0&&Vector2.Dot(wanted.normalized,smoothMove.normalized)<.999f;
            float duration=turning?tune.turnTime:wanted.magnitude<smoothMove.magnitude?tune.releaseTime:tune.accelerationTime;
            smoothMove=Vector2.MoveTowards(smoothMove,wanted,(turning?2:1)/Mathf.Max(.01f,duration)/Fps);
            pig.Step(1f/Fps,new PootleInputFrame{move=smoothMove,jumpPressed=press,jumpHeld=held});
        }
        void MoveTo(Vector3 destination,bool press=false,bool held=false,float speed=1)
        {Vector3 d=Vector3.ProjectOnPlane(destination-pig.transform.position,Vector3.up);Drive(Vector3.ClampMagnitude(d*.9f,1),press,held,speed);}
        void Cut(int next,Vector3 position)
        {
            var travel=FindAnyObjectByType<PootleCaveTravel>();if(travel){if(travel.Busy)travel.Cancel();foreach(var portal in travel.portals)portal.Disarm(Time.unscaledTime);}
            section=next;smoothMove=Vector2.zero;pig.Character.Teleport(position,Quaternion.identity);pig.Step(1f/Fps,default);
            evidence.Add("Shot "+next+" starts at "+count+" frames, "+position);
        }
        OinkRompPickup Upgrade(int index)=>pig.course.pickups.Where(p=>p.kind==OinkRompPickup.Kind.Balloons).ElementAt(index);
        void FindUpgrade(int shot,int index)
        {
            var p=Upgrade(index);if(section!=shot)Cut(shot,p.transform.position-Vector3.up*.6f-Vector3.forward*1.6f);
            MoveTo(p.transform.position,speed:.42f);
        }
        void Update()
        {
            if(encoder==null)return;float t=count/(float)Fps;
            if(t<10)
            {
                if(section!=0)Cut(0,new Vector3(-6.6f,3.04f,10.21f));
                if(t<3.5f)MoveTo(Upgrade(0).transform.position,speed:.45f);
                else if(t<7.5f)MoveTo(pig.course.gates[0].button.position+Vector3.forward*.2f,speed:.5f);
                else MoveTo(new Vector3(-9.3f,3,18),speed:.5f);
            }
            else if(t<14)FindUpgrade(1,1);
            else if(t<18)FindUpgrade(2,2);
            else if(t<22)FindUpgrade(3,3);
            else if(t<30)FindUpgrade(4,4);
            else if(t<34)FindUpgrade(5,5);
            else if(t<38)FindUpgrade(6,6);
            else if(t<42)
            {if(section!=7)Cut(7,new Vector3(9,103.03f,24));MoveTo(new Vector3(9,103,29),speed:.45f);}
            else if(t<56)
            {
                var floor=pig.course.smashFloors[0];
                if(section!=8){var b=floor.surface.bounds;Cut(8,new Vector3(b.center.x,b.max.y+.05f,b.center.z));}
                float local=t-42;int tick=count-42*Fps;bool press=tick==30 || tick>=36 && tick<=144 && (tick-36)%18==0;
                Drive(Vector3.zero,press,press||local>6.8f&&local<10);
            }
            else if(t<66)
            {
                var rail=pig.course.GetComponentInChildren<PootleRail>();
                if(section!=9)Cut(9,rail.Point(rail.points.Length-2)+Vector3.up*.15f);
                // Hold to drop onto the helix, then let its native slide mode carry the pig.
                if(t<58)Drive(Vector3.zero,count==1690,t>=56.33f);else Drive(Vector3.zero);
            }
            else if(t<73)
            {
                var hook=pig.course.GetComponentsInChildren<PootleDangleHook>().First();
                if(section!=10)Cut(10,hook.HangingPosition(Vector3.forward));
                bool press=count==72*Fps;Drive(Vector3.forward*.3f,press,press);
            }
            else if(t<80)
            {if(section!=11)Cut(11,new Vector3(9,12.1f,30.5f));Drive(Vector3.zero);}
            else if(t<86)
            {
                var mud=pig.course.mudVolume.transform.position;
                if(section!=12)Cut(12,mud+new Vector3(0,-.35f,-1.4f));MoveTo(mud+new Vector3(0,0,6),speed:.5f);
            }
            else
            {if(section!=13)Cut(13,new Vector3(-6.6f,3.04f,10.21f));MoveTo(new Vector3(-4,3,12),speed:.25f);}
        }
        void LateUpdate()
        {
            if(encoder==null)return;
            try
            {
                camera.targetTexture=target;camera.Render();camera.targetTexture=null;
                var previous=RenderTexture.active;RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,Width,Height),0,0);frame.Apply(false);RenderTexture.active=previous;encoder.AddFrame(frame);
                if(audioStarted){int samples=AudioRenderer.GetSampleCountForCaptureFrame();using(var data=new NativeArray<float>(samples*2,Allocator.Temp)){AudioRenderer.Render(data);encoder.AddSamples(data);}}
                if(count%90==0)
                {
                    File.WriteAllBytes(Path.Combine(Folder,"coastal-review-"+count.ToString("D4")+".png"),frame.EncodeToPNG());
                    var director=FindAnyObjectByType<PootleZoneCameraDirector>();
                    evidence.Add(count+" frames: position="+pig.transform.position+", hop="+pig.Hop+", active="+pig.InflatedCount+", capacity="+pig.BalloonCapacity+", mode="+pig.Character.Mode+", slam="+pig.LastSlamHeight+", zone="+(director.ActiveZone?director.ActiveZone.name:"fallback"));
                }
                count++;if(count%30==0)File.WriteAllText(Path.Combine(Folder,"status.txt"),count+" / "+Frames);
                if(count>=Frames){Finish();File.WriteAllText(Path.Combine(Folder,"status.txt"),"Complete: "+count+" frames");File.WriteAllLines(Path.Combine(Folder,"coastal-recording-events.txt"),evidence);EditorApplication.isPaused=true;}
            }
            catch(Exception e){Finish();File.WriteAllText(Path.Combine(Folder,"status.txt"),"Failed: "+e);Debug.LogException(e);}
        }
        void Finish()
        {
            encoder?.Dispose();encoder=null;Time.captureDeltaTime=oldCapture;if(audioStarted){AudioRenderer.Stop();audioStarted=false;}
            if(camera)camera.targetTexture=null;if(target){target.Release();Destroy(target);}if(frame)Destroy(frame);if(pig)pig.simulateAutomatically=true;
        }
        void OnDisable(){if(encoder!=null)Finish();}
    }
}
#endif
