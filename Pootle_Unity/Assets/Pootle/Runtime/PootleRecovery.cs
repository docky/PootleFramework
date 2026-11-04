using UnityEngine;
namespace Pootle
{
    // A last resort for unresolved penetration or a capsule wedged between opposing walls.
    [DisallowMultipleComponent]
    public sealed class PootleRecovery : MonoBehaviour
    {
        public bool enabledRecovery = true;
        [Min(.1f)] public float blockedSeconds = 1.2f;
        public int RecoveryCount { get; private set; }
        private Vector3 lastPosition, safePosition;
        private bool sampled, hasSafe;
        private float blocked;
        public void Check(PootleCharacter c,float dt,PootleInputFrame input)
        {
            Vector3 now=transform.position;
            if(!sampled){sampled=true;lastPosition=now;}
            bool ordinary=c.Mode==PootleMode.Grounded || c.Mode==PootleMode.Airborne || c.Mode==PootleMode.GroundPounding;
            bool penetrating=!c.Motor.CanOccupy(now);
            bool opposed=Opposed(c,Vector3.right)||Opposed(c,Vector3.forward)
                || Opposed(c,new Vector3(1,0,1).normalized)||Opposed(c,new Vector3(1,0,-1).normalized);
            bool stalled=Vector3.ProjectOnPlane(now-lastPosition,Vector3.up).magnitude<.015f && input.move.sqrMagnitude>.1f;
            blocked=enabledRecovery && ordinary && (penetrating || opposed && stalled) ? blocked+dt : 0;
            if(c.Mode==PootleMode.Grounded && !penetrating && !opposed){safePosition=now;hasSafe=true;}
            lastPosition=now;
            if(blocked<blockedSeconds)return;
            if(hasSafe && Vector3.Distance(safePosition,now)<12 && c.Motor.CanOccupy(safePosition)) { Restore(c,safePosition); return; }
            for(int height=0;height<=6;height++)
                for(int radius=1;radius<=4;radius++)
                    for(int direction=0;direction<8;direction++)
                    {
                        Vector3 p=now+Vector3.up*(height*.5f)+Quaternion.Euler(0,direction*45,0)*Vector3.forward*(radius*.5f);
                        if(c.Motor.CanOccupy(p)){Restore(c,p);return;}
                    }
            blocked=0;
        }
        private bool Opposed(PootleCharacter c,Vector3 axis)
        {
            Vector3 center=transform.position+c.Motor.center;
            return c.Motor.Ray(center,axis,c.Motor.Radius+.08f,PootleProbeKind.Clearance,out var a)
                && c.Motor.Ray(center,-axis,c.Motor.Radius+.08f,PootleProbeKind.Clearance,out var b)
                && Mathf.Abs(a.normal.y)<.3f && Mathf.Abs(b.normal.y)<.3f;
        }
        private void Restore(PootleCharacter c,Vector3 p)
        {blocked=0;RecoveryCount++;c.Teleport(p,Quaternion.LookRotation(c.Facing));c.BlockGrabs();lastPosition=p;}
    }
}
