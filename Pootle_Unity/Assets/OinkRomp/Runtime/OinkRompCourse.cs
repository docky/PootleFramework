using UnityEngine;
using Pootle;
namespace OinkRomp
{
    public sealed class OinkRompCourse : MonoBehaviour
    {
        public OinkRompSmashFloor[] smashFloors = new OinkRompSmashFloor[0];
        public OinkRompImpact impact;
        public void Slam(OinkRompController pig,float height)
        {
            if(impact)impact.Show(pig.transform.position,height);
            foreach(var floor in smashFloors)if(floor)floor.TrySmash(pig.transform.position,height);
        }
        public OinkRompPickup[] pickups;
        public OinkRompGate[] gates;
        public OinkRompBird[] birds;
        public Transform[] springs;
        public BoxCollider mudVolume, bankVolume, hoverPlate;
        public Transform finish;
        public bool MudVisited {get;private set;}
        public bool HoverReached {get;private set;}
        public bool Completed {get;private set;}
        public OinkRompBird NearbyBird {get;private set;}
        float nextSpring;
        public bool IsInMud(Vector3 p)=>Contains(mudVolume,p+Vector3.up*.3f);
        public bool IsOnBank(Vector3 p)=>Contains(bankVolume,p+Vector3.up*.3f);
        public static bool Contains(BoxCollider volume,Vector3 p)
        {
            if(!volume)return false;var q=volume.transform.InverseTransformPoint(p)-volume.center;var half=volume.size*.5f;
            return Mathf.Abs(q.x)<=half.x && Mathf.Abs(q.y)<=half.y && Mathf.Abs(q.z)<=half.z;
        }
        public void BeforeStep(OinkRompController pig,PootleInputFrame input,float dt)
        { foreach(var gate in gates)if(gate)gate.Sample(pig,input,dt); }
        public void AfterStep(OinkRompController pig,float dt)
        {
            foreach(var pickup in pickups)if(pickup && pickup.isActiveAndEnabled)pickup.TryCollect(pig);
            if(IsInMud(pig.transform.position)){MudVisited=true;foreach(var g in gates)if(g.requirement==OinkRompGate.Requirement.Mud)g.Unlocked=true;}
            if(pig.Character.IsGrounded && Contains(hoverPlate,pig.transform.position+Vector3.up*.2f))
            {if(!HoverReached)pig.Notify("Lookout reached! The balloon gate is ready.");HoverReached=true;foreach(var g in gates)if(g.requirement==OinkRompGate.Requirement.HoverPlate)g.Unlocked=true;}
            if(Time.time>=nextSpring && pig.Character.Velocity.y<=.1f)
                foreach(var spring in springs)
                {
                    var d=pig.transform.position-spring.position;
                    if(new Vector2(d.x,d.z).sqrMagnitude<1.1f && d.y>=-.1f && d.y<.25f)
                    {pig.Spring(14);nextSpring=Time.time+.7f;break;}
                }
            NearbyBird=null;float nearest=float.PositiveInfinity;
            foreach(var bird in birds)
            {bool near=bird.InRange(pig.transform.position);bird.Face(pig,near);float d=(bird.transform.position-pig.transform.position).sqrMagnitude;if(near && d<nearest){nearest=d;NearbyBird=bird;}}
            if(!Completed && finish && Vector3.Distance(pig.transform.position,finish.position)<3)
            {Completed=true;pig.Notify("OINK-REDIBLE! The coastal orchard is yours to explore.",8);}
        }
    }
}
