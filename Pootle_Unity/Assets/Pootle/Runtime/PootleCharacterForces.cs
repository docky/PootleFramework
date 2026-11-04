using UnityEngine;
namespace Pootle
{
    public sealed partial class PootleCharacter
    {
        private Vector3 externalPlanar,windVelocity;
        private float facingHoldUntil,bumperUntil,windAcceleration;
        public bool HoldingExternalFacing=>clock<facingHoldUntil;
        public void PreserveExternalFacing()=>facingHoldUntil=clock+profile.externalFacingHold;
        public void ApplyImpulse(Vector3 velocity)
        {
            ResetSlideRide();
            traversal?.ResetTraversal();railRider?.ResetRide();hookRider?.ResetRide();platformRider?.ClearRiding();
            BlockGrabs();variableJump=false;forcedSlopeSlide=false;
            Velocity=velocity;externalPlanar=Vector3.ProjectOnPlane(velocity,Vector3.up);
            jumpBuffer.Clear();groundJumpUsed=true;lastGrounded=float.NegativeInfinity;
            SetMode(PootleMode.Airborne,PootleTransition.Released);PreserveExternalFacing();
        }
        private void TickEnvironment(float dt)
        {
            windVelocity=Vector3.zero;windAcceleration=0;
            foreach(var wind in PootleWind.Active)
                if(wind && wind.isActiveAndEnabled && wind.Contains(transform.position+Motor.center))
                {windVelocity+=wind.Velocity;windAcceleration=Mathf.Max(windAcceleration,wind.acceleration);}
            bool free=IsRolling || Mode==PootleMode.Grounded || Mode==PootleMode.Airborne || Mode==PootleMode.Gliding || Mode==PootleMode.GroundPounding;
            if(!free){externalPlanar=Vector3.zero;windVelocity=Vector3.zero;windAcceleration=0;}
            Vector3 previous=externalPlanar;
            externalPlanar=Vector3.MoveTowards(externalPlanar,Vector3.ProjectOnPlane(windVelocity,Vector3.up),
                (windAcceleration>0?windAcceleration:IsGrounded?8:1.5f)*dt);
            Velocity+=externalPlanar-previous;
            if(windAcceleration>0)PreserveExternalFacing();
            if(clock>=bumperUntil)
                foreach(var bumper in PootleBumper.Active)
                    if(bumper && bumper.isActiveAndEnabled && bumper.Touches(this))
                    {bumperUntil=clock+bumper.cooldown;ApplyImpulse(bumper.Launch(this));break;}
        }
        private float WindVertical(float initial,float normal,float dt)
        {
            if(windAcceleration<=0 || Mathf.Abs(windVelocity.y)<.01f)return normal;
            float result=Mathf.MoveTowards(initial,windVelocity.y,windAcceleration*dt);
            if(result>0 && IsGrounded)SetMode(PootleMode.Airborne,PootleTransition.Released);
            return result;
        }
        private void ResetForces(){externalPlanar=windVelocity=Vector3.zero;facingHoldUntil=bumperUntil=windAcceleration=0;}
    }
}
