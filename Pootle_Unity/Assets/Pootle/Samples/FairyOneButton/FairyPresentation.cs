using UnityEngine;
using TMPro;
namespace Pootle.FairyDemo
{
    public enum FairyCue { Windup, Launch, Flap, Empty, Fast, Use, Land }
    public sealed class FairyPresentation : MonoBehaviour
    {
        public FairyController fairy;
        public Transform pose, leftWing, rightWing, shadow;
        public Transform[] sparkles;
        public RectTransform promptPanel, speechPanel, pipPanel;
        public TMP_Text prompt, speech;
        public UnityEngine.UI.Image[] pips;
        public Camera view;
        public Canvas canvas;
        public AudioSource sound;
        public float effectsVolume=.23f;
        float cueTime, cueDuration, phase;
        FairyCue lastCue;
        AudioClip[] clips;
        bool wasGrounded;
        void Awake()
        {
            clips=new AudioClip[7];
            for(int i=0;i<clips.Length;i++)clips[i]=Tone(i);
        }
        AudioClip Tone(int kind)
        {
            const int rate=22050;float length=kind==1?.36f:.16f;int count=(int)(length*rate);
            var data=new float[count];float angle=0;
            for(int i=0;i<count;i++)
            {
                float t=(float)i/rate, u=t/length;
                float frequency=kind==3?180:440+kind*70+u*(kind==1?660:180);
                angle+=frequency*2*Mathf.PI/rate;
                data[i]=(Mathf.Sin(angle)+.2f*Mathf.Sin(angle*2))*Mathf.Sin(Mathf.PI*u)*Mathf.Exp(-u*3)*.5f;
            }
            var clip=AudioClip.Create("Fairy chime "+kind,count,1,rate,false);clip.SetData(data,0);return clip;
        }
        public void Cue(FairyCue cue)
        {
            lastCue=cue;cueDuration=cue==FairyCue.Windup?.3f:.4f;cueTime=cueDuration;
            if(sound&&clips!=null)sound.PlayOneShot(clips[(int)cue],effectsVolume);
        }
        public void ResetPose(){cueTime=0;if(pose)pose.localScale=Vector3.one;}
        void LateUpdate()
        {
            if(!fairy||!fairy.Character)return;
            float dt=Time.deltaTime;phase+=dt;cueTime=Mathf.Max(0,cueTime-dt);
            bool ground=fairy.ConfirmedGround;
            if(ground&&!wasGrounded)Cue(FairyCue.Land);wasGrounded=ground;
            float pulse=cueDuration>0?Mathf.Sin(cueTime/cueDuration*Mathf.PI):0;
            bool wind=fairy.Windup>0;
            float squash=wind?-.18f:cueTime>0?(lastCue==FairyCue.Land?-.2f:lastCue==FairyCue.Launch||lastCue==FairyCue.Flap?.22f:0)*pulse:0;
            if(pose)
            {
                pose.localScale=new Vector3(1-squash*.45f,1+squash,1-squash*.45f);
                var local=transform.InverseTransformDirection(fairy.Character.Velocity);
                pose.localRotation=Quaternion.Euler(ground?0:local.z*2,0,ground?0:-local.x*3);
            }
            float beat=ground?(wind?52:12+Mathf.Sin(phase*5)*7):30+Mathf.Sin(phase*22)*24+pulse*35;
            if(leftWing)leftWing.localRotation=Quaternion.Euler(0,-beat,-15);
            if(rightWing)rightWing.localRotation=Quaternion.Euler(0,beat,15);
            if(shadow)
            {
                bool hit=Physics.Raycast(transform.position+Vector3.up*.1f,Vector3.down,out var groundHit,20,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                shadow.gameObject.SetActive(hit);
                if(hit){shadow.position=groundHit.point+groundHit.normal*.014f;shadow.rotation=Quaternion.FromToRotation(Vector3.up,groundHit.normal);float size=Mathf.Lerp(.48f,.23f,Mathf.Clamp01(groundHit.distance/5));shadow.localScale=new Vector3(size,.003f,size);}
            }
            if(sparkles!=null)for(int i=0;i<sparkles.Length;i++)
            {
                var s=sparkles[i];float a=i*Mathf.PI*2/sparkles.Length+phase*2;
                bool active=cueTime>0&&(lastCue==FairyCue.Launch||lastCue==FairyCue.Flap||lastCue==FairyCue.Use);
                s.gameObject.SetActive(active);
                if(active){float spread=1-cueTime/cueDuration;s.localPosition=new Vector3(Mathf.Cos(a)*(.25f+spread*.55f),.3f+spread*.5f,Mathf.Sin(a)*(.25f+spread*.55f));s.localScale=Vector3.one*(.07f*cueTime/cueDuration);}
            }
            if(!view||!canvas)return;
            string label=fairy.CurrentAction switch {FairyAction.Launch=>"Tap twice to fly",FairyAction.Windup=>"Tap again to fly",FairyAction.Talk=>"Talk",FairyAction.Press=>fairy.Focus&&fairy.Focus.kind==FairyTargetKind.Reset?"Reset":"Press",_=>""};
            if(prompt){prompt.text=label;promptPanel.gameObject.SetActive(label.Length>0);Place(promptPanel,fairy.Focus?fairy.Focus.focusPoint.position+Vector3.up*1.3f:transform.position+Vector3.up*1.3f);}
            if(speech){speech.text=fairy.Speech;speechPanel.gameObject.SetActive(fairy.Speech.Length>0);if(fairy.Speaker)Place(speechPanel,fairy.Speaker.transform.position+Vector3.up*1.9f);}
            if(pipPanel){pipPanel.gameObject.SetActive(!ground);Place(pipPanel,transform.position+Vector3.up*1.13f);}
            if(pips!=null)for(int i=0;i<pips.Length;i++)pips[i].color=i<fairy.Flaps?new Color(1,.91f,.4f):new Color(.28f,.35f,.42f,.8f);
        }
        void Place(RectTransform rect,Vector3 point)
        {
            var viewport=view.WorldToViewportPoint(point);
            rect.anchorMin=rect.anchorMax=new Vector2(Mathf.Clamp(viewport.x,.12f,.88f),Mathf.Clamp(viewport.y,.10f,.86f));rect.anchoredPosition=Vector2.zero;
        }
        void OnDestroy(){if(clips!=null)foreach(var clip in clips)if(clip)Destroy(clip);}
    }
}
