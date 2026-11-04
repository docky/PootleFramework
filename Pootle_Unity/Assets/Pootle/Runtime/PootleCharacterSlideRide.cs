using UnityEngine;
namespace Pootle
{
    public sealed partial class PootleCharacter
    {
        public bool SlideRideActive => rideSurface;
        public bool SlideRideAirborne => rideSurface && Mode==PootleMode.Airborne;
        private PootleSlideSurface rideSurface;
        private float rideTime,rideFacingOffset,rideReattachDelay;
        private bool rideAscendingExit;
        private void ResetSlideRide(){rideSurface=null;rideTime=rideReattachDelay=0;rideAscendingExit=false;}
        private bool TickSlideRide(float dt,PootleInputFrame frame,out PootleMotionResult result)
        {
            result=default;
            if(Mode!=PootleMode.Grounded && Mode!=PootleMode.Airborne && Mode!=PootleMode.Sliding && Mode!=PootleMode.Gliding && Mode!=PootleMode.GroundPounding)
            {ResetSlideRide();return false;}
            var surface=Support.collider?Support.collider.GetComponentInParent<PootleSlideSurface>():null;
            bool contact=Support.found && Support.central && Support.distance<=.055f && Support.normal.y>.1f;
            if(!rideSurface)
            {
                if(!surface || !surface.isActiveAndEnabled || !contact || Vector3.Dot(Velocity,Support.normal)>.8f)return false;
                if(Support.angle<3 && Velocity.magnitude<1)return false;
                rideSurface=surface;rideTime=0;
                Vector3 heading=Vector3.ProjectOnPlane(Velocity,Vector3.up);
                if(heading.sqrMagnitude<1)heading=Vector3.ProjectOnPlane(Support.normal,Vector3.up);
                rideFacingOffset=Mathf.Round(Vector3.SignedAngle(heading,Facing,Vector3.up)/90)*90;
            }
            if(!rideSurface.isActiveAndEnabled){ResetSlideRide();return false;}
            if(frame.glide && !contact && Velocity.y<=0){ResetSlideRide();return false;}
            Vector3 start=transform.position;
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt*120));float h=dt/steps;
            for(int step=0;step<steps;step++)
            {
                Motor.BeginTick();
                rideReattachDelay=Mathf.Max(0,rideReattachDelay-h);
                if(Velocity.y<=0)rideAscendingExit=false;
                Support=Motor.ProbeGround(transform.position,.08f,profile.requireCentralSupport);
                contact=Support.found && Support.central && Support.distance<=.055f && Support.normal.y>.1f;
                bool attached=contact && !rideAscendingExit && !(rideReattachDelay>0 && Velocity.y>0);
                if(attached)
                {
                    if(Mode!=PootleMode.Sliding && !IsGrounded)ReportLanding(-Vector3.Dot(Velocity,Support.normal));
                    SetMode(PootleMode.Sliding,PootleTransition.SlideStarted);
                    lastGrounded=clock;groundJumpUsed=false;AirJumpsUsed=0;
                }
                float launch=Velocity.y;
                if(TryJump(ref launch,frame.jumpHeld))
                {Velocity=new Vector3(Velocity.x,launch,Velocity.z);ResetSlideRide();return false;}
                Vector3 before=Velocity;
                Vector3 input=PootleMovement.CameraDirection(frame.move,outputCamera,out float magnitude);
                if(attached)
                {
                    Vector3 normal=Support.normal;
                    // Integrate full gravity, then remove only velocity INTO the surface.
                    // Outward velocity survives: a convex bank cannot pull us down to it.
                    Velocity+=Vector3.down*rideSurface.gravity*h;
                    Velocity-=normal*Mathf.Min(0,Vector3.Dot(Velocity,normal));
                    Vector3 forward=Velocity.normalized;
                    Vector3 steering=Vector3.ProjectOnPlane(input,normal);
                    float alignment=Vector3.Dot(steering,forward);
                    Velocity+=Vector3.ProjectOnPlane(steering,forward)*rideSurface.steeringAcceleration*magnitude*h;
                    var marker=Support.collider.GetComponentInParent<PootleSlideSurface>();
                    float friction=marker?rideSurface.friction:rideSurface.runoutDeceleration;
                    if(alignment<-.35f)friction+=rideSurface.brakingDeceleration*magnitude*(-alignment);
                    Velocity=Vector3.MoveTowards(Velocity,Vector3.zero,friction*h);
                    // Only driving/gravity is capped; large incoming impulses can still overshoot a bank.
                    Velocity=Vector3.ClampMagnitude(Velocity,Mathf.Max(before.magnitude,rideSurface.maximumSpeed));
                    SeatedNormal=Vector3.Slerp(SeatedNormal,normal,1-Mathf.Exp(-14*h));
                    if(rideTime>.4f && Velocity.magnitude<profile.slideStopSpeed && Support.angle<3)
                    {Velocity=Vector3.zero;ResetSlideRide();SetMode(PootleMode.Grounded,PootleTransition.SlideStopped);return false;}
                }
                else
                {
                    // The capsule may brush the lip once more while rising past it.
                    // Keep that a ballistic collision, not another mount/dismount cycle.
                    if(Mode==PootleMode.Sliding)rideReattachDelay=.1f;
                    if(!contact && Velocity.y>1)rideAscendingExit=true;
                    SetMode(PootleMode.Airborne,PootleTransition.WalkedOffEdge);
                    Velocity+=Vector3.down*rideSurface.gravity*h;
                    Velocity+=Vector3.ProjectOnPlane(input,Velocity.normalized)*magnitude*2*h;
                    SeatedNormal=Vector3.Slerp(SeatedNormal,Vector3.up,1-Mathf.Exp(-5*h));
                }
                Vector3 motion=(before+Velocity)*(.5f*h);
                var moved=Motor.Move(motion,false,0,true);
                result.requested+=motion;result.hitCeiling|=moved.hitCeiling;
                result.contactCount+=Motor.ContactCount;
                for(int i=0;i<Motor.ContactCount;i++)
                {
                    var hit=Motor.Contacts[i];
                    Velocity-=hit.normal*Mathf.Min(0,Vector3.Dot(Velocity,hit.normal));
                }
                Vector3 flat=Vector3.ProjectOnPlane(Velocity,Vector3.up);
                if(flat.sqrMagnitude>.25f)
                {
                    Vector3 target=Quaternion.AngleAxis(rideFacingOffset,Vector3.up)*flat.normalized;
                    Facing=Vector3.RotateTowards(Facing,target,360*Mathf.Deg2Rad*h,0).normalized;
                }
                rideTime+=h;
            }
            IsBraking=IsRunning=false;
            result.displacement=transform.position-start;
            return true;
        }
    }
}
