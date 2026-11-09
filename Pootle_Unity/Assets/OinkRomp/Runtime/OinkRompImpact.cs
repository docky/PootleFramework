using UnityEngine;
namespace OinkRomp
{
    public sealed class OinkRompImpact : MonoBehaviour
    {
        public LineRenderer ring;
        public float Radius {get;private set;}
        float age=1;
        Vector3 center;
        public void Show(Vector3 position,float height){center=position+Vector3.up*.07f;Radius=Mathf.Lerp(.7f,3.5f,Mathf.Clamp01(height/12));age=0;ring.enabled=true;}
        void Update()
        {
            if(!ring || !ring.enabled)return;age+=Time.deltaTime;
            float t=age/.55f;ring.startWidth=ring.endWidth=.12f*(1-t);
            for(int i=0;i<ring.positionCount;i++){float a=i/(float)ring.positionCount*Mathf.PI*2;ring.SetPosition(i,center+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*Radius*Mathf.Clamp01(t*1.6f));}
            if(t>=1)ring.enabled=false;
        }
    }
}
