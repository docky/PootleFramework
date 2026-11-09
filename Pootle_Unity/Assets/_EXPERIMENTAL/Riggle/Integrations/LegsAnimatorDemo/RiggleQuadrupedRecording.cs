#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Docky.RiggleTools.Demo
{
    [InitializeOnLoad,DefaultExecutionOrder(2000)]
    public sealed class RiggleQuadrupedRecording : MonoBehaviour
    {
        const string Key="RiggleQuadrupedRecord";
        const int Width=1920,Height=1080,Fps=30,Total=45*Fps;
        MediaEncoder encoder;RenderTexture target;Texture2D frame;Camera camera;
        RiggleQuadrupedWalk walk;TextMesh title,subtitle,footer;int frames;float oldCapture;
        string folder,path;bool complete;
        string Slug => walk.animal.ToLowerInvariant().Replace(' ', '-');
        static RiggleQuadrupedRecording(){EditorApplication.playModeStateChanged+=OnPlayMode;}
        public static void Arm(string animal)
        {if(EditorApplication.isPlaying)throw new InvalidOperationException("Arm before Play Mode.");SessionState.SetString(Key,animal);}
        static void OnPlayMode(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode)return;
            string animal=SessionState.GetString(Key,"");if(string.IsNullOrEmpty(animal))return;
            SessionState.EraseString(Key);
            var walk=Object.FindAnyObjectByType<RiggleQuadrupedWalk>();
            if(!walk){Debug.LogError("Walking trial is not open.");return;}
            walk.gameObject.AddComponent<RiggleQuadrupedRecording>().Begin(walk);
        }
        void Begin(RiggleQuadrupedWalk source)
        {
            walk=source;camera=walk.followCamera;
            folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Builds","Quadruped Trials","Videos"));Directory.CreateDirectory(folder);
            string slug=Slug;
            path=Path.Combine(folder,"riggle-"+slug+"-uneven-walk-2026-11-08.mp4");
            encoder=new MediaEncoder(path,new VideoTrackEncoderAttributes(new H264EncoderAttributes{gopSize=60,numConsecutiveBFrames=2,profile=VideoEncodingProfile.H264Main}){frameRate=new MediaRational(Fps),width=Width,height=Height,includeAlpha=false,targetBitRate=7000000,bitRateMode=VideoBitrateMode.High});
            target=new RenderTexture(Width,Height,24,RenderTextureFormat.ARGB32);target.Create();
            frame=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/Fps;
            title=Text("Recording title",new Vector3(-.51f,.275f,1),.0028f);
            subtitle=Text("Recording subtitle",new Vector3(-.51f,.235f,1),.0018f);
            footer=Text("Recording footer",new Vector3(-.51f,-.235f,1),.0018f);
            title.text="RIGGLE GENERIC / "+walk.animal.ToUpperInvariant();
            subtitle.text=walk.recordingSubtitle;
            File.WriteAllText(Path.Combine(folder,slug+"-status.txt"),"Recording");
        }
        TextMesh Text(string name,Vector3 position,float size)
        {
            var go=new GameObject(name);go.transform.SetParent(camera.transform,false);go.transform.localPosition=position;
            var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.UpperLeft;t.color=Color.white;
            go.GetComponent<Renderer>().sharedMaterial=t.font.material;return t;
        }
        void LateUpdate()
        {
            if(encoder==null)return;
            try
            {
                footer.text=RiggleUnevenCourse.Section(walk.distance)+"   |   "+walk.distance.ToString("F1")+" m   |   "+(frames/(float)Fps).ToString("F1")+" / 45 s\nFootfalls FL / FR / HL / HR: "+string.Join(" / ",walk.steps);
                camera.targetTexture=target;camera.Render();camera.targetTexture=null;
                var previous=RenderTexture.active;RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,Width,Height),0,0);frame.Apply(false);RenderTexture.active=previous;encoder.AddFrame(frame);
                string slug=Slug;
                if(frames==90||frames==300||frames==600||frames==900||frames==1200)
                    File.WriteAllBytes(Path.Combine(folder,slug+"-frame-"+(frames/Fps)+".png"),frame.EncodeToPNG());
                frames++;
                if(frames%30==0)File.WriteAllText(Path.Combine(folder,slug+"-status.txt"),frames+" / "+Total);
                if(frames>=Total)
                {complete=true;Finish();File.WriteAllText(Path.Combine(folder,slug+"-report.json"),walk.Report());EditorApplication.isPaused=true;}
            }
            catch(Exception e){Finish();Debug.LogException(e);}
        }
        void Finish()
        {
            encoder?.Dispose();encoder=null;Time.captureDeltaTime=oldCapture;
            if(camera)camera.targetTexture=null;
            if(target){target.Release();Destroy(target);}if(frame)Destroy(frame);
            if(title)Destroy(title.gameObject);if(subtitle)Destroy(subtitle.gameObject);if(footer)Destroy(footer.gameObject);
            if(walk)File.WriteAllText(Path.Combine(folder,Slug+"-status.txt"),(complete?"Complete: ":"Stopped: ")+frames+" frames, "+path);
        }
        void OnDisable(){if(encoder!=null)Finish();}
    }
}

#endif
