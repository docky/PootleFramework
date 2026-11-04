using UnityEngine;
namespace Pootle
{
    public enum PootleDropResult { None, InFront, Beneath, Blocked }
    [DisallowMultipleComponent,AddComponentMenu("Pootle/Object Carrying")]
    public sealed class PootleCarrying : MonoBehaviour
    {
        [Min(.1f)] public float pickupReach=2f;
        public PootleCarryable Held { get; private set; }
        public PootleDropResult LastDrop { get; private set; }
        private readonly Collider[] candidates=new Collider[48];
        private readonly Collider[] overlaps=new Collider[48];
        private readonly RaycastHit[] hits=new RaycastHit[48];
        private Vector3 heldCenter;
        private Quaternion heldRotation;

        public void HandleAction(PootleCharacter c)
        {
            if(Held){TryDrop(c);return;}
            if(c.IsRolling || c.Mode!=PootleMode.Grounded && c.Mode!=PootleMode.Airborne)return;
            int count=Physics.OverlapSphereNonAlloc(c.transform.position+c.Motor.center,pickupReach,candidates,c.Motor.collisionMask,QueryTriggerInteraction.Ignore);
            PootleCarryable best=null;float nearest=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                var candidate=candidates[i].GetComponent<PootleCarryable>();
                if(!candidate || !candidate.isActiveAndEnabled || candidate.IsHeld)continue;
                Vector3 delta=candidate.Shape.bounds.center-(c.transform.position+c.Motor.center);
                if(delta.magnitude>pickupReach || Vector3.Dot(delta,c.Facing)<-.1f || delta.sqrMagnitude>=nearest)continue;
                Quaternion rotation=Quaternion.LookRotation(c.Facing);
                Vector3 point=CarryCenter(c,candidate);
                if(!Clear(c,candidate,point,rotation) || !PathClear(c,candidate,candidate.Shape.bounds.center,point,rotation))continue;
                best=candidate;nearest=delta.sqrMagnitude;
            }
            if(!best)return;
            Held=best;heldCenter=CarryCenter(c,Held);heldRotation=Quaternion.LookRotation(c.Facing);
            Held.Take(heldCenter,heldRotation);LastDrop=PootleDropResult.None;
        }
        private Vector3 CarryCenter(PootleCharacter c,PootleCarryable item)=>c.transform.position+Vector3.up*(c.Motor.Height*.65f)+c.Facing*(c.Motor.Radius+item.HalfSize.z+.12f);
        private bool Solid(Collider collider,PootleCharacter c,PootleCarryable item,bool includeCharacter)
        {
            if(collider==item.Shape || collider.transform.IsChildOf(item.transform))return false;
            if(collider.transform.IsChildOf(c.transform))return includeCharacter && collider==c.GetComponent<CapsuleCollider>();
            return !collider.isTrigger || collider.GetComponent<PootleCharacter>();
        }
        private bool Clear(PootleCharacter c,PootleCarryable item,Vector3 center,Quaternion rotation)
        {
            int count=Physics.OverlapBoxNonAlloc(center,Vector3.Max(Vector3.one*.01f,item.HalfSize-Vector3.one*.008f),overlaps,rotation,c.Motor.collisionMask,QueryTriggerInteraction.Collide);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(Solid(overlaps[i],c,item,false))return false;
            // The character moved earlier in this same tick; its physics broadphase pose can lag.
            // Check the live capsule pose explicitly so an ascending jump doesn't reject a valid drop.
            var capsule=c.GetComponent<CapsuleCollider>();
            if(Physics.ComputePenetration(item.Shape,item.RootForCenter(center,rotation),rotation,
                capsule,c.transform.position,c.transform.rotation,out _,out float depth) && depth>.001f)return false;
            return true;
        }
        private bool PathClear(PootleCharacter c,PootleCarryable item,Vector3 from,Vector3 to,Quaternion rotation)
        {
            Vector3 delta=to-from;if(delta.sqrMagnitude<.000001f)return true;
            int count=Physics.BoxCastNonAlloc(from,Vector3.Max(Vector3.one*.01f,item.HalfSize-Vector3.one*.015f),delta.normalized,hits,rotation,delta.magnitude,c.Motor.collisionMask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(Solid(hits[i].collider,c,item,false))return false;
            return true;
        }
        public bool TryDrop(PootleCharacter c)
        {
            if(!Held)return false;
            var item=Held;Quaternion rotation=Quaternion.LookRotation(c.Facing);
            Vector3 beneath=c.transform.position-Vector3.up*(item.HalfSize.y+.04f);
            bool airborne=!c.IsGrounded;
            if(airborne && Clear(c,item,beneath,rotation) && PathClear(c,item,heldCenter,beneath,rotation))
            {Release(beneath,rotation,PootleDropResult.Beneath);return true;}
            Vector3 front=c.transform.position+c.Facing*(c.Motor.Radius+item.HalfSize.z+.15f)+Vector3.up*(item.HalfSize.y+.04f);
            // When grounded, place on the surface immediately ahead, including small height changes.
            if(c.IsGrounded && c.Motor.Ray(front+Vector3.up*c.Motor.stepHeight,Vector3.down,c.Motor.stepHeight+item.HalfSize.y+.15f,PootleProbeKind.Clearance,out var ground))
                front.y=ground.point.y+item.HalfSize.y+.04f;
            if(Clear(c,item,front,rotation) && PathClear(c,item,heldCenter,front,rotation))
            {Release(front,rotation,PootleDropResult.InFront);return true;}
            LastDrop=PootleDropResult.Blocked;return false;
        }
        private void Release(Vector3 center,Quaternion rotation,PootleDropResult result)
        {
            var item=Held;Held=null;item.Release(center,rotation);LastDrop=result;
        }
        public void Follow(PootleCharacter c)
        {
            if(!Held)return;
            Vector3 target=CarryCenter(c,Held);Quaternion rotation=Quaternion.LookRotation(c.Facing);
            if(Clear(c,Held,target,rotation) && PathClear(c,Held,heldCenter,target,rotation))
            {heldCenter=target;heldRotation=rotation;Held.Place(target,rotation);}
            else if(Vector3.Distance(heldCenter,c.transform.position+c.Motor.center)>pickupReach+.5f)
                ReleaseForReset();
        }
        public void ReleaseForReset()
        {if(Held)Release(heldCenter,heldRotation,PootleDropResult.InFront);}
        private void OnDisable()=>ReleaseForReset();
    }
}
