using UnityEngine;
namespace OinkRomp
{
    public sealed class OinkRompBalloons : MonoBehaviour
    {
        public OinkRompController pig;
        public Transform[] envelopes;
        public LineRenderer[] strings;
        public Transform anchor;
        void LateUpdate()
        {
            if(!pig || !anchor)return;
            for(int i=0;i<envelopes.Length;i++)
            {
                float life=pig.BalloonLives[i]; bool visible=life>0;
                envelopes[i].gameObject.SetActive(visible);strings[i].enabled=visible;
                if(!visible)continue;
                float size=Mathf.Lerp(.16f,.85f,Mathf.Clamp01(life/pig.balloonLifetime));
                float angle=i*2.39996f;
                float spread=i==0?0:.65f+Mathf.Sqrt(i)*.19f;
                Vector3 offset=new Vector3(Mathf.Cos(angle)*spread,2.1f+(i%3)*.48f+.12f*Mathf.Sin(Time.time*2+i),Mathf.Sin(angle)*spread);
                envelopes[i].position=anchor.position+offset;
                envelopes[i].localScale=new Vector3(size,size*1.22f,size);
                strings[i].SetPosition(0,anchor.position);strings[i].SetPosition(1,envelopes[i].position-Vector3.up*size*.5f);
            }
        }
    }
}
