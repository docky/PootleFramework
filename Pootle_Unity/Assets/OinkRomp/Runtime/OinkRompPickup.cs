using UnityEngine;
namespace OinkRomp
{
    public sealed class OinkRompPickup : MonoBehaviour
    {
        public enum Kind { Apple, Balloons }
        public Kind kind;
        public int amount=1;
        public float radius=1.05f;
        [Tooltip("Zero means a permanent collectible; tutorial supplies replenish.")]
        public float refillSeconds;
        public Transform visual;
        public bool Collected { get; private set; }
        float readyAt; Vector3 rest;
        void Awake() { if(!visual)visual=transform.GetChild(0);rest=visual.localPosition; }
        public bool TryCollect(OinkRompController pig)
        {
            if(Collected || (pig.transform.position+Vector3.up*.6f-transform.position).sqrMagnitude>radius*radius)return false;
            if(kind==Kind.Balloons && pig.AddBalloons(amount)==0)return false;
            if(kind==Kind.Apple)pig.EatApple();
            Collected=true;readyAt=Time.time+refillSeconds;visual.gameObject.SetActive(false);return true;
        }
        void Update()
        {
            if(Collected) { if(kind==Kind.Apple && refillSeconds>0 && Time.time>=readyAt){Collected=false;visual.gameObject.SetActive(true);}return; }
            visual.localPosition=rest+Vector3.up*(Mathf.Sin(Time.time*2+transform.position.x)*.12f);
            visual.Rotate(0,24*Time.deltaTime,0,Space.Self);
        }
    }
}
