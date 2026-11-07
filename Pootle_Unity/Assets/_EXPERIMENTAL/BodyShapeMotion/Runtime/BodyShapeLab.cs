using UnityEngine;
namespace Docky.BodyShapeMotion
{
    public sealed class BodyShapeLab : MonoBehaviour
    {
        public BodyShapeSample[] samples;
        public bool playing=true;
        [Range(.1f,2)] public float speed=1;
        [Range(0,1)] public float phase=.35f;
        public int focusRow;
        public bool focusOnly;
        double seconds;
        void Start(){foreach(var s in samples)if(s && s.isActiveAndEnabled)s.Initialize();}
        void Update()
        {
            if(!playing)return;seconds+=Time.deltaTime*speed;
            foreach(var s in samples)if(s.row==focusRow){phase=Mathf.Repeat((float)seconds/s.clip.length,1);break;}
            foreach(var s in samples)if(s && s.isActiveAndEnabled)s.Sample((float)seconds,Time.deltaTime);
        }
        public void Scrub(float normalized)
        {
            playing=false;phase=Mathf.Clamp01(normalized);seconds=0;
            foreach(var s in samples)if(s.row==focusRow){seconds=phase*s.clip.length;break;}
            foreach(var s in samples)if(s && s.isActiveAndEnabled)s.Sample(Mathf.Min(phase,.999999f)*s.clip.length,0,true);
        }
        public void Restart(){seconds=0;foreach(var s in samples)if(s && s.isActiveAndEnabled){s.Initialize();s.Sample(0,0,true);}}
        public void Focus(int row,bool only)
        {
            focusRow=Mathf.Clamp(row,0,15);focusOnly=only;
            foreach(var s in samples)if(s)s.transform.parent.gameObject.SetActive(!only || s.row==focusRow);
            var camera=Camera.main;if(!camera)return;
            camera.orthographic=true;camera.orthographicSize=only?3.7f:22;
            var center=only?new Vector3(4.8f,.7f,focusRow*3.4f):new Vector3(4.8f,0,25.5f);
            camera.transform.position=center+(only?new Vector3(-.6f,2,-9):new Vector3(4,46,-30));camera.transform.LookAt(center);
        }
    }
}
