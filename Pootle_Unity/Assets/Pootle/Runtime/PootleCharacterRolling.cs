using UnityEngine;

namespace Pootle
{
    public sealed partial class PootleCharacter
    {
        public bool IsRolling { get; private set; }
        public bool LastUnrollBlocked { get; private set; }
        private bool rollingGrounded;
        private float standingHeight, standingStep;
        private Vector3 standingCenter;

        private void ToggleRolling()
        {
            if(!Motor)Motor=GetComponent<PootleMotor>();
            if(IsRolling)
            {
                float ballHeight=Motor.height;Vector3 ballCenter=Motor.center;
                Motor.height=standingHeight;Motor.center=standingCenter;Motor.Configure();
                if(!Motor.CanOccupy(transform.position))
                {
                    Motor.height=ballHeight;Motor.center=ballCenter;Motor.Configure();
                    LastUnrollBlocked=true;return;
                }
                Motor.stepHeight=standingStep;IsRolling=rollingGrounded=false;LastUnrollBlocked=false;
                SetMode(PootleMode.Airborne,PootleTransition.Released);BlockGrabs();return;
            }
            if(carrying && carrying.Held)return;
            if(Mode!=PootleMode.Grounded && Mode!=PootleMode.Airborne && Mode!=PootleMode.Sliding)return;
            standingHeight=Motor.height;standingCenter=Motor.center;standingStep=Motor.stepHeight;
            rollingGrounded=IsGrounded;IsRolling=true;LastUnrollBlocked=false;
            Motor.height=Motor.Radius*2;Motor.center=new Vector3(standingCenter.x,Motor.Radius,standingCenter.z);
            Motor.stepHeight=Mathf.Min(.2f,standingStep);Motor.Configure();
            traversal?.ResetTraversal();railRider?.ResetRide();hookRider?.ResetRide();
            forcedSlopeSlide=false;SetMode(PootleMode.Rolling,PootleTransition.Supported);
        }
        private void ResetRolling()
        {
            if(IsRolling && Motor)
            {Motor.height=standingHeight;Motor.center=standingCenter;Motor.stepHeight=standingStep;Motor.Configure();}
            IsRolling=rollingGrounded=LastUnrollBlocked=false;
        }
        private void TickRolling(float dt,PootleInputFrame frame,out PootleMotionResult result)
        {
            IsRunning=IsBraking=false;
            Support=Motor.ProbeGround(transform.position,rollingGrounded?Motor.stepHeight+.06f:.07f,profile.requireCentralSupport);
            bool contact=Support.central && Support.distance<=.07f && Support.angle<85;
            bool wasGrounded=rollingGrounded;
            rollingGrounded=contact && Vector3.Dot(Velocity,Support.normal)<=.1f;
            if(rollingGrounded && !wasGrounded)Landed?.Invoke(Mathf.Max(0,-Velocity.y));
            if(rollingGrounded){lastGrounded=clock;groundJumpUsed=false;AirJumpsUsed=0;}
            bool spring=TrySpring();
            if(spring)rollingGrounded=false;
            Vector3 direction=PootleMovement.CameraDirection(frame.move,outputCamera,out float magnitude);
            float vertical=Velocity.y;
            bool jumped=!spring && TryJump(ref vertical,frame.jumpHeld);
            if(jumped)
            {
                rollingGrounded=false;
                if(platformRider){Velocity+=Vector3.ProjectOnPlane(platformRider.CarryVelocity,Vector3.up);vertical+=platformRider.CarryVelocity.y;}
            }
            Vector3 previous=Velocity;
            if(rollingGrounded)
            {
                Vector3 velocity=Vector3.ProjectOnPlane(Velocity,Support.normal);
                if(magnitude>.001f)
                {
                    Vector3 target=Vector3.ProjectOnPlane(direction,Support.normal).normalized*profile.rollingSpeed*magnitude;
                    velocity=Vector3.MoveTowards(velocity,target,profile.rollingControl*dt);
                }
                else velocity=Vector3.MoveTowards(velocity,Vector3.zero,profile.rollingFriction*dt);
                velocity+=Vector3.ProjectOnPlane(Vector3.down*profile.rollingGravity,Support.normal)*dt;
                Velocity=Vector3.ClampMagnitude(velocity,profile.rollingMaximumSpeed);
                previous=Vector3.ProjectOnPlane(previous,Support.normal);
            }
            else
            {
                Vector3 planar=Vector3.ProjectOnPlane(Velocity,Vector3.up);
                if(magnitude>.001f)planar=Vector3.MoveTowards(planar,direction*profile.rollingSpeed*magnitude,profile.rollingControl*dt);
                if(variableJump && !frame.jumpHeld)vertical=Mathf.Min(vertical,profile.jumpReleaseSpeed);
                if(jumped)previous=new Vector3(previous.x,vertical,previous.z);
                vertical=Mathf.Max(-profile.maximumFallSpeed,vertical-(vertical>0?profile.risingGravity:profile.fallingGravity)*dt);
                Velocity=planar+Vector3.up*vertical;
            }
            result=Motor.Move((previous+Velocity)*.5f*dt,rollingGrounded,rollingGrounded?Motor.stepHeight+.06f:0,true,profile.groundSnapSpeed*dt);
            for(int i=0;i<Motor.ContactCount;i++)
                if(Vector3.Dot(Velocity,Motor.Contacts[i].normal)<0)Velocity=Vector3.ProjectOnPlane(Velocity,Motor.Contacts[i].normal);
            if(direction.sqrMagnitude>.001f)Facing=direction;
            // The toggle remains active through jumps, falls, landings and spring launches.
            SetMode(PootleMode.Rolling,PootleTransition.Supported);
        }
    }
}
