#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;

namespace Docky.RiggleTools.Demo
{
    [InitializeOnLoad,DefaultExecutionOrder(2000)]
    public sealed class RiggleQuadrupedRetargetRecording : MonoBehaviour
    {
        const string Key="RiggleHorseRetargetRecord";
        const int Width=1920,Height=1080,Fps=30,Frames=48*Fps;
        MediaEncoder encoder;RenderTexture target;Texture2D frame;float oldCapture;
        RiggleQuadrupedRetargetShowcase demo;TextMesh title,subtitle,footer;string folder;int count;
        static RiggleQuadrupedRetargetRecording(){EditorApplication.playModeStateChanged+=OnPlay;}
        public static void Arm(){if(EditorApplication.isPlaying)throw new InvalidOperationException("Arm in Edit Mode.");SessionState.SetBool(Key,true);}
        static void OnPlay(PlayModeStateChange state)
        {
            if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
            SessionState.EraseBool(Key);var demo=FindAnyObjectByType<RiggleQuadrupedRetargetShowcase>();
            if(!demo){Debug.LogError("Open the horse retarget showcase first.");return;}
            demo.gameObject.AddComponent<RiggleQuadrupedRetargetRecording>().Begin(demo);
        }
        void Begin(RiggleQuadrupedRetargetShowcase source)
        {
            demo=source;folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Builds/Horse Retarget",demo.recordingSubfolder));Directory.CreateDirectory(folder);
            encoder=new MediaEncoder(Path.Combine(folder,demo.recordingSlug+".mp4"),new VideoTrackEncoderAttributes(new H264EncoderAttributes{gopSize=60,numConsecutiveBFrames=2,profile=VideoEncodingProfile.H264Main}){frameRate=new MediaRational(Fps),width=Width,height=Height,targetBitRate=8000000,bitRateMode=VideoBitrateMode.High});
            target=new RenderTexture(Width,Height,24);target.Create();frame=new Texture2D(Width,Height,TextureFormat.RGBA32,false);
            oldCapture=Time.captureDeltaTime;Time.captureDeltaTime=1f/Fps;
            title=Text(new Vector3(-.54f,.285f,1),.0026f);
            subtitle=Text(new Vector3(-.54f,.245f,1),.0018f);
            footer=Text(new Vector3(-.54f,-.245f,1),.0018f);
            title.text="HORSE ANIMATIONS  >  "+demo.targetLabel;
            subtitle.text="Generic animation conversion  |  Four segments per leg\nSource horse (left)  /  Riggle pony (right)";
        }
        TextMesh Text(Vector3 position,float size)
        {
            var go=new GameObject("Recording label");go.transform.SetParent(demo.camera.transform,false);go.transform.localPosition=position;
            var t=go.AddComponent<TextMesh>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=64;t.characterSize=size;t.anchor=TextAnchor.UpperLeft;t.color=Color.white;go.GetComponent<Renderer>().sharedMaterial=t.font.material;return t;
        }
        void LateUpdate()
        {
            if(encoder==null)return;
            try
            {
                footer.text=demo.Label+"  |  Same clip timing, different proportions\n"+(count/(float)Fps).ToString("F1")+" / 48 s  |  Baked clips; ground IK disabled for comparison";
                demo.camera.targetTexture=target;demo.camera.Render();demo.camera.targetTexture=null;
                var previous=RenderTexture.active;RenderTexture.active=target;frame.ReadPixels(new Rect(0,0,Width,Height),0,0);frame.Apply(false);RenderTexture.active=previous;encoder.AddFrame(frame);
                if(count%240==120)File.WriteAllBytes(Path.Combine(folder,"frame-"+demo.Current+"-"+demo.Label.Replace(' ','-')+".png"),frame.EncodeToPNG());
                count++;if(count%30==0)File.WriteAllText(Path.Combine(folder,"status.txt"),count+" / "+Frames);
                if(count>=Frames){Finish();File.WriteAllText(Path.Combine(folder,"status.txt"),"Complete: "+count+" frames");EditorApplication.isPaused=true;}
            }
            catch(Exception e){Finish();Debug.LogException(e);}
        }
        void Finish()
        {
            encoder?.Dispose();encoder=null;Time.captureDeltaTime=oldCapture;
            if(demo&&demo.camera)demo.camera.targetTexture=null;
            if(target){target.Release();Destroy(target);}if(frame)Destroy(frame);
            if(title)Destroy(title.gameObject);if(subtitle)Destroy(subtitle.gameObject);if(footer)Destroy(footer.gameObject);
        }
        void OnDisable(){if(encoder!=null)Finish();}
    }
}
#endif
