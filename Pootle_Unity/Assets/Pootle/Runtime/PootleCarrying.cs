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
        [Tooltip("Maximum backward room-making step when a standing front drop is blocked. Zero disables it.")]
        [Min(0)] public float maximumDropBackstep=.6f;
        public PootleCarryable Held { get; private set; }
        public PootleDropResult LastDrop { get; private set; }
        private readonly Collider[] candidates=new Collider[48];
        private readonly Collider[] overlaps=new Collider[48];
        private readonly RaycastHit[] hits=new RaycastHit[48];
        private Vector3 heldCenter;
        private Quaternion heldRotation;
        private PootleCharacter owner;
        private Vector3 pickupCenter,safeCenter;
        private Quaternion pickupRotation,safeRotation;

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
                if(!Reachable(c,candidate,c.transform.position,candidate.Shape.bounds.center))continue;
                best=candidate;nearest=delta.sqrMagnitude;
            }
            if(!best)return;
            owner=c;Held=best;pickupCenter=safeCenter=Held.Shape.bounds.center;pickupRotation=safeRotation=Held.transform.rotation;
            heldCenter=CarryCenter(c,Held);heldRotation=Quaternion.LookRotation(c.Facing);
            Held.Take(heldCenter,heldRotation);LastDrop=PootleDropResult.None;
            if(c.Support.collider && c.Support.collider.transform.IsChildOf(Held.transform))
                c.GetComponent<PootlePlatformRider>()?.ClearRiding();
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
        private bool Reachable(PootleCharacter c,PootleCarryable item,Vector3 root,Vector3 destination)
        {
            Vector3 from=root+c.Motor.center,delta=destination-from;if(delta.sqrMagnitude<.000001f)return true;
            int count=Physics.RaycastNonAlloc(from,delta.normalized,hits,delta.magnitude,c.Motor.collisionMask,QueryTriggerInteraction.Ignore);
            if(count==hits.Length)return false;
            for(int i=0;i<count;i++)
            {
                var shape=hits[i].collider;if(!Solid(shape,c,item,false))continue;
                var oneWay=shape.GetComponentInParent<PootleOneWayPlatform>();
                if(oneWay && oneWay.isActiveAndEnabled && (Vector3.Dot(delta,oneWay.Normal)>0 ||
                    Vector3.Dot(from-oneWay.Top,oneWay.Normal)<-oneWay.surfaceTolerance || Vector3.Dot(hits[i].normal,oneWay.Normal)<.5f))continue;
                return false;
            }
            return true;
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
                if(!c.Motor.CanMoveTo(root) || !c.Motor.CanOccupy(root))return false;
                c.Motor.MoveTo(root);
                c.Facing=facing;heldCenter=center;heldRotation=rotation;Held.Place(center,rotation);
            }
            return true;
        }
        public bool TryDrop(PootleCharacter c)=>TryDrop(c,true);
        private bool TryDrop(PootleCharacter c,bool makeRoom)
        {
            if(!Held)return false;
            var item=Held;Quaternion rotation=Quaternion.LookRotation(c.Facing);
            Vector3 beneath=c.transform.position-Vector3.up*(item.HalfSize.y+.04f);
            if(!c.IsGrounded && CanReleaseAt(c,item,c.transform.position,beneath,rotation))
            {Release(beneath,rotation,PootleDropResult.Beneath);return true;}
            Vector3 front=FrontDrop(c,item,c.transform.position);
            if(CanReleaseAt(c,item,c.transform.position,front,rotation))
            {Release(front,rotation,PootleDropResult.InFront);return true;}
            if(c.IsGrounded && makeRoom)
            {
                int steps=Mathf.CeilToInt(maximumDropBackstep/.1f);
                for(int i=1;i<=steps;i++)
                {
                    Vector3 root=c.transform.position-c.Facing*Mathf.Min(i*.1f,maximumDropBackstep);
                    if(!c.Motor.CanMoveTo(root) || !c.Motor.CanOccupy(root))continue;
                    var support=c.Motor.ProbeGround(root,.06f,true);
                    if(!support.walkable || !support.central || support.distance>.06f)continue;
                    front=FrontDrop(c,item,root);
                    if(!CanReleaseAt(c,item,root,front,rotation))continue;
                    c.Motor.MoveTo(root);c.Velocity=Vector3.zero;
                    Release(front,rotation,PootleDropResult.InFront);return true;
                }
            }
            if(CanReleaseAt(c,item,c.transform.position,beneath,rotation))
            {Release(beneath,rotation,PootleDropResult.Beneath);return true;}
            LastDrop=PootleDropResult.Blocked;return false;
        }
        private bool CanReleaseAt(PootleCharacter c,PootleCarryable item,Vector3 root,Vector3 center,Quaternion rotation)
            =>Clear(c,item,center,rotation,root) && Reachable(c,item,root,center);
        private Vector3 FrontDrop(PootleCharacter c,PootleCarryable item,Vector3 root)
        {
            Vector3 front=root+c.Facing*(c.Motor.Radius+item.HalfSize.z+.15f)+Vector3.up*(item.HalfSize.y+.04f);
            if(c.IsGrounded && c.Motor.Ray(front+Vector3.up*c.Motor.stepHeight,Vector3.down,c.Motor.stepHeight+item.HalfSize.y+.15f,PootleProbeKind.Clearance,out var ground))
            {
                var normal=ground.normal;var half=item.HalfSize;
                Vector3 right=Vector3.Cross(Vector3.up,c.Facing);
                float extent=Mathf.Abs(Vector3.Dot(normal,right))*half.x+Mathf.Abs(normal.y)*half.y+Mathf.Abs(Vector3.Dot(normal,c.Facing))*half.z;
                if(normal.y>.1f)front.y=ground.point.y+(extent+.04f)/normal.y;
            }
            return front;
        }
        private void Release(Vector3 center,Quaternion rotation,PootleDropResult result)
        {
            var item=Held;Held=null;item.Release(center,rotation);LastDrop=result;
        }
        public void Follow(PootleCharacter c)
        {
            if(!Held)return;
            Vector3 target=CarryCenter(c,Held);Quaternion rotation=Quaternion.LookRotation(c.Facing);
            heldCenter=target;heldRotation=rotation;Held.Place(target,rotation);
            if(CanReleaseAt(c,Held,c.transform.position,target,rotation)){safeCenter=target;safeRotation=rotation;}
        }
        public void ReleaseForReset()
        {
            if(!Held)return;if(!owner)owner=GetComponent<PootleCharacter>();
            if(TryDrop(owner,false))return;
            if(Clear(owner,Held,safeCenter,safeRotation)){Release(safeCenter,safeRotation,PootleDropResult.InFront);return;}
            if(Clear(owner,Held,pickupCenter,pickupRotation))Release(pickupCenter,pickupRotation,PootleDropResult.InFront);
        }
        private void OnDisable()=>ReleaseForReset();
    }
}
