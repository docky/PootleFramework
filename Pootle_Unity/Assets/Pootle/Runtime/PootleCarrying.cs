using UnityEngine;
namespace Pootle
{
    public enum PootleDropResult { None, InFront, Beneath, Blocked }
    [DisallowMultipleComponent,AddComponentMenu("Pootle/Object Carrying")]
    public sealed class PootleCarrying : MonoBehaviour
    {
        [Min(.1f)] public float pickupReach=2f;
        [Tooltip("Forward offset from Lily's feet used as the turning pivot while carrying, in world units. Zero turns around Lily; 0.4 pivots nearer the middle of Lily and the box.")]
        public float rotationPivotForward=.4f;
        public PootleCarryable Held { get; private set; }
        public PootleDropResult LastDrop { get; private set; }
        private readonly Collider[] candidates=new Collider[48];
        private readonly Collider[] overlaps=new Collider[48];
        private readonly RaycastHit[] hits=new RaycastHit[48];
        private Vector3 heldCenter;
        private Quaternion heldRotation;
        private PootleCharacter owner;
        private bool turning;

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
        private Vector3 CarryCenter(PootleCharacter c,PootleCarryable item)=>CarryCenter(c,item,c.transform.position,c.Facing);
        private Vector3 CarryCenter(PootleCharacter c,PootleCarryable item,Vector3 root,Vector3 facing)=>root+Vector3.up*(c.Motor.Height*.65f)+facing*(c.Motor.Radius+item.HalfSize.z+.12f);
        private bool Solid(Collider collider,PootleCharacter c,PootleCarryable item,bool includeCharacter)
        {
            if(collider==item.Shape || collider.transform.IsChildOf(item.transform))return false;
            if(collider.transform.IsChildOf(c.transform))return includeCharacter && collider==c.GetComponent<CapsuleCollider>();
            return !collider.isTrigger || collider.GetComponent<PootleCharacter>();
        }
        private bool Clear(PootleCharacter c,PootleCarryable item,Vector3 center,Quaternion rotation,Vector3? characterRoot=null)
        {
            int count=Physics.OverlapBoxNonAlloc(center,Vector3.Max(Vector3.one*.01f,item.HalfSize-Vector3.one*.008f),overlaps,rotation,c.Motor.collisionMask,QueryTriggerInteraction.Collide);
            if(count==overlaps.Length)return false;
            for(int i=0;i<count;i++)if(Solid(overlaps[i],c,item,false))return false;
            // The character moved earlier in this same tick; its physics broadphase pose can lag.
            // Check the live capsule pose explicitly so an ascending jump doesn't reject a valid drop.
            var capsule=c.GetComponent<CapsuleCollider>();
            if(Physics.ComputePenetration(item.Shape,item.RootForCenter(center,rotation),rotation,
                capsule,characterRoot??c.transform.position,c.transform.rotation,out _,out float depth) && depth>.001f)return false;
            return true;
        }
        private bool PathClear(PootleCharacter c,PootleCarryable item,Vector3 from,Vector3 to,Quaternion rotation)
        {
            Vector3 delta=to-from;if(delta.sqrMagnitude<.000001f)return true;
            int count=Physics.BoxCastNonAlloc(from,Vector3.Max(Vector3.one*.01f,item.HalfSize-Vector3.one*.015f),delta.normalized,hits,rotation,delta.magnitude,c.Motor.collisionMask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)if(Solid(hits[i].collider,c,item,false) && Vector3.Dot(delta,hits[i].normal)<-.0001f)return false;
            return true;
        }
        internal Vector3 ConstrainMotion(PootleMotor motor,Vector3 motion)
        {
            if(!Held || turning)return motion;
            if(!owner)owner=GetComponent<PootleCharacter>();
            Vector3 center=heldCenter,remaining=motion,resolved=Vector3.zero;
            var half=Vector3.Max(Vector3.one*.01f,Held.HalfSize-Vector3.one*.008f);
            for(int pass=0;pass<3 && remaining.sqrMagnitude>.00000001f;pass++)
            {
                float distance=remaining.magnitude;Vector3 direction=remaining/distance;
                int count=Physics.BoxCastNonAlloc(center,half,direction,hits,heldRotation,distance+motor.Skin,motor.collisionMask,QueryTriggerInteraction.Ignore);
                if(count==hits.Length)return resolved;
                float nearest=float.PositiveInfinity;RaycastHit blocker=default;
                for(int i=0;i<count;i++)
                    if(Solid(hits[i].collider,owner,Held,false) && Vector3.Dot(direction,hits[i].normal)<-.0001f && hits[i].distance<nearest)
                    {nearest=hits[i].distance;blocker=hits[i];}
                if(!blocker.collider)return resolved+remaining;
                Vector3 advance=direction*Mathf.Clamp(nearest-motor.Skin,0,distance);
                center+=advance;resolved+=advance;
                remaining=Vector3.ProjectOnPlane(remaining-advance,blocker.normal);
            }
            return resolved;
        }
        public bool Turn(PootleCharacter c,Vector3 desiredFacing)
        {
            desiredFacing=Vector3.ProjectOnPlane(desiredFacing,Vector3.up).normalized;
            if(desiredFacing.sqrMagnitude<.5f)return false;
            if(!Held){c.Facing=desiredFacing;return true;}
            Vector3 initialFacing=c.Facing,pivot=c.transform.position+initialFacing*rotationPivotForward;
            float angle=Vector3.SignedAngle(initialFacing,desiredFacing,Vector3.up);
            if(Mathf.Abs(angle)<.001f)return true;
            int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(angle)/5));
            for(int i=1;i<=steps;i++)
            {
                Vector3 facing=Quaternion.AngleAxis(angle*i/steps,Vector3.up)*initialFacing;
                Vector3 root=pivot-facing*rotationPivotForward;
                Quaternion rotation=Quaternion.LookRotation(facing);
                Vector3 center=CarryCenter(c,Held,root,facing);
                if(!c.Motor.CanMoveTo(root) || !c.Motor.CanOccupy(root) || !Clear(c,Held,center,rotation,root)
                    || !PathClear(c,Held,heldCenter,center,Quaternion.Slerp(heldRotation,rotation,.5f)))return false;
                turning=true;
                try{c.Motor.MoveTo(root);}finally{turning=false;}
                c.Facing=facing;heldCenter=center;heldRotation=rotation;Held.Place(center,rotation);
            }
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
            // Blocked movement/turns keep ownership. Only an explicit drop or reset releases it.
        }
        public void ReleaseForReset()
        {if(Held)Release(heldCenter,heldRotation,PootleDropResult.InFront);}
        private void OnDisable()=>ReleaseForReset();
    }
}
