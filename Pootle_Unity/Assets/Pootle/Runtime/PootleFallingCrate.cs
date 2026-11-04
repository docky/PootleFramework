using UnityEngine;
namespace Pootle
{
    // Update-order motion keeps the support and player in the same simulation clock.
    [DefaultExecutionOrder(-180),RequireComponent(typeof(PootleCarryable),typeof(PootleMovingSurface))]
    public sealed class PootleFallingCrate : MonoBehaviour
    {
        [Min(0)] public float hangTime=.32f;
        [Min(0)] public float initialGravity=3;
        [Min(0)] public float fallingGravity=65;
        [Min(1)] public float maximumFallSpeed=35;
        public bool simulateAutomatically=true;
        public float FallSpeed {get;private set;}
        public bool Grounded {get;private set;}
        private float elapsed;
        private PootleCarryable item;
        private readonly RaycastHit[] hits=new RaycastHit[32];
        private readonly Collider[] overlaps=new Collider[32];
        private void Awake(){item=GetComponent<PootleCarryable>();Configure();}
        private void Configure(){if(!item)item=GetComponent<PootleCarryable>();item.Body.collisionDetectionMode=CollisionDetectionMode.Discrete;item.Body.isKinematic=true;item.Body.useGravity=false;item.Body.interpolation=RigidbodyInterpolation.None;}
        public void RestartFall(){elapsed=FallSpeed=0;Grounded=false;Configure();}
        private void Update(){if(simulateAutomatically)Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if(dt<=0)return;Configure();
            if(item.IsHeld){elapsed=FallSpeed=0;Grounded=false;return;}
            float acceleration=elapsed<hangTime?initialGravity:fallingGravity;
            float next=Mathf.Min(maximumFallSpeed,FallSpeed+acceleration*dt);
            float distance=(FallSpeed+next)*.5f*dt;
            var shape=item.Shape;
            Vector3 center=shape.transform.TransformPoint(shape.center);
            int count=Physics.BoxCastNonAlloc(center,item.HalfSize-Vector3.one*.005f,Vector3.down,hits,transform.rotation,distance+.012f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float travel=distance;Grounded=false;Collider support=null;float closest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];if(!hit.collider || hit.collider==shape || hit.collider.transform.IsChildOf(transform) || hit.normal.y<.4f)continue;
                if(hit.distance<=distance+.01f)
                {
                    Grounded=true;travel=Mathf.Min(travel,Mathf.Max(0,hit.distance-.005f));
                    if(hit.distance<closest){closest=hit.distance;support=hit.collider;}
                }
            }
            transform.position+=Vector3.down*travel;
            if(Grounded){elapsed=FallSpeed=0;}else{elapsed+=dt;FallSpeed=next;}
            if(support)
            {
                var belt=support.GetComponentInParent<PootleConveyor>();
                if(belt && belt.isActiveAndEnabled)CarryOnBelt(belt,support,dt);
            }
            Physics.SyncTransforms();
        }
        private void CarryOnBelt(PootleConveyor belt,Collider support,float dt)
        {
            Vector3 from=transform.position,delta=belt.VelocityAt(from)*dt;
            float distance=delta.magnitude;if(distance<.000001f)return;
            Vector3 center=item.Shape.transform.TransformPoint(item.Shape.center);
            var half=Vector3.Max(Vector3.one*.01f,item.HalfSize-Vector3.one*.01f);
            int count=Physics.BoxCastNonAlloc(center,half,delta.normalized,hits,transform.rotation,distance+.005f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            float travel=distance;
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(!hit.collider || hit.collider==support || hit.collider==item.Shape || hit.collider.transform.IsChildOf(transform))continue;
                if(Vector3.Dot(delta,hit.normal)>=0)continue;
                travel=Mathf.Min(travel,Mathf.Max(0,hit.distance-.005f));
            }
            delta=delta.normalized*travel;
            Quaternion rotation=belt.RotationBetween(from,from+delta)*transform.rotation;
            count=Physics.OverlapBoxNonAlloc(center+delta,half,overlaps,rotation,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            bool clear=count<overlaps.Length;
            for(int i=0;i<count;i++)
                if(overlaps[i]!=support && overlaps[i]!=item.Shape && !overlaps[i].transform.IsChildOf(transform))clear=false;
            item.Place(center+delta,clear?rotation:transform.rotation);
        }
    }
}
