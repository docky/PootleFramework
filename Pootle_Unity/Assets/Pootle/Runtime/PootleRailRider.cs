using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Rail Rider")]
    public sealed class PootleRailRider : MonoBehaviour
    {
        public PootleRail Rail { get; private set; }
        public float Speed { get; private set; }
        private int segment;
        private float along, cooldown, facingAngle;
        private bool sliding;
        public void ResetRide() { Rail=null;Speed=cooldown=0;sliding=false; }
        private static Vector3 Flat(Vector3 v)=>Vector3.ProjectOnPlane(v,Vector3.up).normalized;
        private void SnapFacing(PootleCharacter c,Vector3 tangent,Vector3 desired)
        {
            facingAngle=Mathf.Round(Vector3.SignedAngle(Flat(tangent),desired,Vector3.up)/90)*90;
            c.Facing=Quaternion.AngleAxis(facingAngle,Vector3.up)*Flat(tangent);
        }
        private bool CanCatch(PootleCharacter c)=>!Rail && cooldown<=0 && c.Velocity.y<=0 &&
            (c.Mode==PootleMode.Airborne || c.Mode==PootleMode.Gliding || c.Mode==PootleMode.GroundPounding || c.Mode==PootleMode.Sliding);
        public void AssistLanding(PootleCharacter c,float dt)
        {
            if(!CanCatch(c)||c.profile.railMagnetRadius<=0)return;
            Vector3 best=default;float nearest=c.profile.railMagnetRadius;bool found=false;
            foreach(var rail in PootleRail.Active)
            {
                if(!rail||!rail.isActiveAndEnabled)continue;
                for(int i=0;i<rail.SegmentCount;i++)
                {
                    Vector3 a=rail.Point(i),delta=rail.Point(i+1)-a,flat=Vector3.ProjectOnPlane(delta,Vector3.up);
                    if(flat.sqrMagnitude<.001f)continue;
                    float t=Mathf.Clamp01(Vector3.Dot(transform.position-a,flat)/flat.sqrMagnitude);
                    Vector3 point=a+delta*t;float height=transform.position.y-point.y;
                    if(height<-.03f || height>c.profile.railMagnetHeight)continue;
                    float distance=Vector3.ProjectOnPlane(point-transform.position,Vector3.up).magnitude;
                    if(distance>=nearest)continue;
                    nearest=distance;best=point;found=true;
                }
            }
            if(found)c.Motor.Move(Vector3.ClampMagnitude(Vector3.ProjectOnPlane(best-transform.position,Vector3.up),c.profile.railMagnetSpeed*dt),false);
        }
        public bool Catch(PootleCharacter c,Vector3 from,Vector3 to)
        {
            if(!CanCatch(c))return false;
            float best=float.PositiveInfinity,bestAlong=0;int bestSegment=0;PootleRail bestRail=null;
            foreach(var rail in PootleRail.Active)
                if(rail && rail.isActiveAndEnabled && rail.Landing(from,to,out int s,out float d,out float t,c.Motor.Radius*c.profile.railLandingRadiusFraction) && t<best)
                {best=t;bestRail=rail;bestSegment=s;bestAlong=d;}
            return bestRail && Attach(c,bestRail,bestSegment,bestAlong);
        }
        public bool TryMount(PootleCharacter c,float verticalReach,float radius)
        {
            PootleRail best=null;int chosen=0;float location=0,nearest=radius;
            foreach(var rail in PootleRail.Active)
                if(rail && rail.isActiveAndEnabled)
                    for(int i=0;i<rail.SegmentCount;i++)
                    {
                        var point=rail.Closest(i,transform.position,out float at);
                        float d=Vector3.ProjectOnPlane(point-transform.position,Vector3.up).magnitude;
                        if(Mathf.Abs(point.y-transform.position.y)>verticalReach || d>=nearest)continue;
                        best=rail;chosen=i;location=at;nearest=d;
                    }
            return best && Attach(c,best,chosen,location);
        }
        private bool Attach(PootleCharacter c,PootleRail rail,int section,float location)
        {
            Vector3 tangent=rail.Tangent(section,location),target=rail.Position(section,location);
            if(!c.Motor.CanOccupy(target))return false;
            c.Motor.MoveTo(target);if(Vector3.Distance(transform.position,target)>.05f)return false;
            if(c.Mode==PootleMode.Airborne || c.Mode==PootleMode.Gliding || c.Mode==PootleMode.GroundPounding)
                c.ReportLanding(-c.Velocity.y);
            bool pound=c.Mode==PootleMode.GroundPounding;
            Rail=rail;segment=section;along=location;
            // Only genuine horizontal momentum is inherited; a falling impact never kicks along an incline.
            Speed=Vector3.Dot(Vector3.ProjectOnPlane(c.Velocity,Vector3.up),Flat(tangent));
            sliding=pound || c.Mode==PootleMode.Sliding || Mathf.Abs(tangent.y)>Mathf.Sin(c.Motor.slopeLimit*Mathf.Deg2Rad);
            if(pound && Mathf.Abs(tangent.y)>.01f)Speed=-Mathf.Sign(tangent.y)*c.profile.poundSlideSpeed;
            SnapFacing(c,tangent,c.Facing);c.RailSupported();c.Velocity=tangent*Speed;
            c.SetMode(sliding?PootleMode.RailSliding:PootleMode.RailWalking,PootleTransition.Grabbed);return true;
        }
        public bool Tick(PootleCharacter c,float dt,PootleInputFrame frame,out PootleMotionResult result)
        {
            result=default;cooldown=Mathf.Max(0,cooldown-dt);
            bool wasRiding=c.Mode==PootleMode.RailWalking || c.Mode==PootleMode.RailSliding;
            if(!Rail || !Rail.isActiveAndEnabled)
            {
                if(wasRiding)Detach(c);
                if(!wasRiding && cooldown<=0 && c.Mode==PootleMode.Grounded && !frame.jumpPressed && TryWalkOn(c,frame))return true;
                return false;
            }
            if(segment>=Rail.SegmentCount || Rail.Length(segment)<.001f){Detach(c);return false;}
            c.RailSupported();var p=c.profile;
            Vector3 input=PootleMovement.CameraDirection(frame.move,c.outputCamera,out float magnitude);
            if(c.ConsumeJump())
            {
                Vector3 launch=magnitude>.1f?input*p.walkSpeed*magnitude:Vector3.ProjectOnPlane(c.Velocity,Vector3.up);
                Detach(c);c.LaunchTraversal(launch,frame.jumpHeld);return false;
            }
            float junctionAlong=along,approach=0;
            bool approaching=!sliding && magnitude>.15f && TryBranch(c,input,out approach,out junctionAlong);
            Vector3 tangent=Rail.Tangent(segment,along);
            bool steep=Mathf.Abs(tangent.y)>Mathf.Sin(c.Motor.slopeLimit*Mathf.Deg2Rad);
            if(steep && !sliding){sliding=true;SnapFacing(c,tangent,c.Facing);}
            float oldSpeed=Speed;
            if(sliding)
            {
                Speed+=-tangent.y*p.slideGravity*dt;
                bool slope=Mathf.Abs(tangent.y)>.05f;
                Speed=Mathf.MoveTowards(Speed,0,(slope?p.slideSlopeFriction:p.slideFlatDeceleration)*dt);
                Speed=Mathf.Clamp(Speed,-p.slideMaximumSpeed,p.slideMaximumSpeed);
                if(!slope && Mathf.Abs(Speed)<p.slideStopSpeed){Speed=0;sliding=false;}
            }
            else
            {
                Vector3 steering=approaching?Flat(tangent)*approach:input;
                if(magnitude>.15f)SnapFacing(c,tangent,steering);
                float intent=Vector3.Dot(steering,Flat(tangent))*magnitude;
                bool sideways=Mathf.Abs(Mathf.DeltaAngle(facingAngle,90))<1 || Mathf.Abs(Mathf.DeltaAngle(facingAngle,-90))<1;
                float target=!sideways && Mathf.Abs(intent)>.15f?intent*(frame.run&&p.allowRun?p.runSpeed:p.walkSpeed):0;
                Speed=Mathf.MoveTowards(Speed,target,(target==0?p.releaseDeceleration:p.groundAcceleration)*dt);
            }
            float next=along+(oldSpeed+Speed)*.5f*dt;
            // Approach on the current rail; never teleport diagonally across a junction.
            if(approaching && (next-junctionAlong)*approach>=0){next=junctionAlong;Speed=0;}
            int nextSegment=segment;bool stoppedCorner=false;
            while(next>Rail.Length(nextSegment) && nextSegment<Rail.SegmentCount-1)
            {
                if(!Rail.IsContinuous && Vector3.Angle(Flat(Rail.Tangent(nextSegment)),Flat(Rail.Tangent(nextSegment+1)))>35)
                {next=Rail.Length(nextSegment);Speed=0;stoppedCorner=true;break;}
                next-=Rail.Length(nextSegment);nextSegment++;
            }
            while(next<0 && nextSegment>0)
            {
                if(!Rail.IsContinuous && Vector3.Angle(Flat(Rail.Tangent(nextSegment)),Flat(Rail.Tangent(nextSegment-1)))>35)
                {next=0;Speed=0;stoppedCorner=true;break;}
                nextSegment--;next+=Rail.Length(nextSegment);
            }
            bool offEnd=!stoppedCorner && (next<0 || next>Rail.Length(nextSegment));
            if(offEnd && HasConnection(Rail.Point(next<0?0:Rail.SegmentCount)))
            {next=Mathf.Clamp(next,0,Rail.Length(nextSegment));Speed=0;offEnd=false;}
            Vector3 newTangent=Rail.Tangent(nextSegment,next),destination=Rail.Position(nextSegment,next);
            result=c.Motor.MoveTo(destination);
            if(Vector3.Distance(transform.position,destination)>.05f){Speed=0;Detach(c);return true;}
            segment=nextSegment;along=next;
            c.Facing=Quaternion.AngleAxis(facingAngle,Vector3.up)*Flat(newTangent);
            c.SeatedNormal=Vector3.ProjectOnPlane(Vector3.up,newTangent).normalized;c.Velocity=newTangent*Speed;
            c.SetMode(sliding?PootleMode.RailSliding:PootleMode.RailWalking,PootleTransition.Supported);
            if(offEnd)Detach(c);return true;
        }
        private bool HasConnection(Vector3 point)
        {
            foreach(var rail in PootleRail.Active)
                if(rail && rail!=Rail && rail.isActiveAndEnabled)
                    for(int i=0;i<rail.SegmentCount;i++)if(Vector3.Distance(rail.Closest(i,point,out _),point)<.2f)return true;
            return false;
        }
        // Find a real connection, then guide along the current support before turning onto it.
        // A continuous spline must never select another sample of itself as a branch.
        private bool TryBranch(PootleCharacter c,Vector3 input,out float approach,out float junctionAlong)
        {
            approach=0;junctionAlong=along;
            Vector3 tangent=Rail.Tangent(segment,along);float currentScore=Mathf.Abs(Vector3.Dot(input,Flat(tangent)));
            bool exiting=along<.2f && Vector3.Dot(input,tangent)<0 || along>Rail.Length(segment)-.2f && Vector3.Dot(input,tangent)>0;
            PootleRail best=null;int section=0;float location=0,score=.65f,connection=0;
            foreach(var rail in PootleRail.Active)
            {
                if(!rail||!rail.isActiveAndEnabled || rail==Rail && Rail.IsContinuous)continue;
                for(int i=0;i<rail.SegmentCount;i++)
                {
                    if(rail==Rail && (Mathf.Abs(i-segment)!=1 || Vector3.Angle(Flat(tangent),Flat(rail.Tangent(i)))<35))continue;
                    ClosestPair(Rail.Point(segment),Rail.Point(segment+1),rail.Point(i),rail.Point(i+1),out float a,out float b);
                    Vector3 chordCurrent=Vector3.Lerp(Rail.Point(segment),Rail.Point(segment+1),a);
                    Vector3 chordBranch=Vector3.Lerp(rail.Point(i),rail.Point(i+1),b);
                    if(Vector3.Distance(chordCurrent,chordBranch)>.15f || Vector3.Distance(chordCurrent,transform.position)>c.profile.railJunctionApproach+.05f)continue;
                    float here=a*Rail.Length(segment),at=b*rail.Length(i);
                    Vector3 onCurrent=Rail.Position(segment,here),point=rail.Position(i,at);
                    if(Vector3.Distance(point,onCurrent)>.12f || Mathf.Abs(here-along)>c.profile.railJunctionApproach)continue;
                    Vector3 candidate=rail.Tangent(i,at);float dot=Vector3.Dot(input,Flat(candidate));
                    if(at<.05f && dot<0 || at>rail.Length(i)-.05f && dot>0)continue;
                    float value=Mathf.Abs(dot);
                    if(value<.65f || !exiting && value<currentScore+.15f)continue;
                    value-=.1f*Mathf.Abs(here-along)/Mathf.Max(.01f,c.profile.railJunctionApproach);
                    if(value<=score)continue;
                    best=rail;section=i;location=at;connection=here;score=value;
                }
            }
            if(!best)return false;
            if(Mathf.Abs(connection-along)<=.015f)
            {Attach(c,best,section,location);return false;}
            junctionAlong=connection;approach=Mathf.Sign(connection-along);return true;
        }
        private static void ClosestPair(Vector3 p,Vector3 q,Vector3 r,Vector3 s,out float u,out float v)
        {
            Vector3 d=q-p,e=s-r,w=p-r;
            float a=Vector3.Dot(d,d),b=Vector3.Dot(d,e),c=Vector3.Dot(e,e),x=Vector3.Dot(d,w),y=Vector3.Dot(e,w);
            if(a<.000001f || c<.000001f){u=v=0;return;}
            float denominator=a*c-b*b;
            u=denominator>.000001f?Mathf.Clamp01((b*y-c*x)/denominator):0;
            v=(b*u+y)/c;
            if(v<0){v=0;u=Mathf.Clamp01(-x/a);}else if(v>1){v=1;u=Mathf.Clamp01((b-x)/a);}
        }
        private bool TryWalkOn(PootleCharacter c,PootleInputFrame frame)
        {
            Vector3 input=PootleMovement.CameraDirection(frame.move,c.outputCamera,out float magnitude);
            if(magnitude<.1f)return false;
            PootleRail nearest=null;int chosen=0;float distance=float.PositiveInfinity,chosenAlong=0;
            foreach(var rail in PootleRail.Active)
            {
                if(!rail||!rail.isActiveAndEnabled||rail.SegmentCount==0)continue;
                for(int end=0;end<2;end++)
                {
                    int s=end==0?0:rail.SegmentCount-1;Vector3 point=rail.Point(end==0?0:rail.SegmentCount);
                    Vector3 inward=rail.Tangent(s)*(end==0?1:-1),delta=point-transform.position;
                    float planar=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude;
                    if(planar>c.Motor.Radius+.15f || planar>=distance || Mathf.Abs(delta.y)>c.Motor.stepHeight+.06f
                        || Vector3.Dot(input,Flat(inward))<.35f || Vector3.Dot(delta,input)<-.15f)continue;
                    nearest=rail;chosen=s;chosenAlong=end==0?0:rail.Length(s);distance=planar;
                }
            }
            return nearest && Attach(c,nearest,chosen,chosenAlong);
        }
        private void Detach(PootleCharacter c)
        {Rail=null;cooldown=.2f;c.SetMode(PootleMode.Airborne,PootleTransition.Released);sliding=false;}
    }
}
