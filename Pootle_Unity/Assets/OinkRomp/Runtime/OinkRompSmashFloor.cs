using UnityEngine;
namespace OinkRomp
{
    public sealed class OinkRompSmashFloor : MonoBehaviour
    {
        public float requiredHeight=2;
        public Collider surface;
        public Transform[] planks;
        public bool Broken {get;private set;}
        float age;
        public bool TrySmash(Vector3 feet,float height)
        {
            if(Broken || !surface || height<requiredHeight)return false;
            Bounds b=surface.bounds;b.Expand(new Vector3(.6f,.6f,.6f));
            if(!b.Contains(feet))return false;
            Broken=true;surface.enabled=false;return true;
        }
        void Update()
        {
            if(!Broken)return;age+=Time.deltaTime;
            for(int i=0;i<planks.Length;i++)if(planks[i])
            {
                planks[i].position+=new Vector3((i%2==0?-1:1)*.8f,-age*8,0)*Time.deltaTime;
                planks[i].Rotate((i%2==0?1:-1)*95*Time.deltaTime,0,30*Time.deltaTime);
                if(age>1.2f)planks[i].gameObject.SetActive(false);
            }
        }
    }
}
