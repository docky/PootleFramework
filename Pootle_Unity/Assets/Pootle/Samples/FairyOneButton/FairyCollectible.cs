using UnityEngine;
namespace Pootle.FairyDemo
{
    public sealed class FairyCollectible : MonoBehaviour
    {
        public Transform visual;
        public float radius=.38f;
        public bool Collected { get; private set; }
        public void Collect(){Collected=true;if(visual)visual.gameObject.SetActive(false);}
        public void ResetCollectible(){Collected=false;if(visual)visual.gameObject.SetActive(true);}
        void Update()
        {
            if(Collected||!visual)return;
            visual.localPosition=Vector3.up*(Mathf.Sin(Time.time*2.5f+transform.position.x)*.045f);
            visual.localRotation=Quaternion.Euler(0,Time.time*65,12);
        }
    }
}
