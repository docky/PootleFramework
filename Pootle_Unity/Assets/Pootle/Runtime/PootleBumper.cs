using System.Collections.Generic;
using UnityEngine;
namespace Pootle
{
    [ExecuteAlways,RequireComponent(typeof(SphereCollider)),AddComponentMenu("Pootle/Pinball Bumper")]
    public sealed class PootleBumper : MonoBehaviour
    {
        public static readonly HashSet<PootleBumper> Active=new HashSet<PootleBumper>();
        public float launchSpeed=17,upwardSpeed=5,cooldown=.35f;
        private void OnEnable(){Active.Add(this);GetComponent<SphereCollider>().isTrigger=true;}
        private void OnDisable()=>Active.Remove(this);
        public bool Touches(PootleCharacter c)
        {
            var shape=GetComponent<SphereCollider>();Vector3 center=transform.TransformPoint(shape.center);
            c.Motor.Endpoints(c.transform.position,out var a,out var b);
            Vector3 point=a+Vector3.Project(center-a,b-a);point.y=Mathf.Clamp(point.y,a.y,b.y);
            float radius=shape.radius*Mathf.Max(transform.lossyScale.x,Mathf.Max(transform.lossyScale.y,transform.lossyScale.z));
            return (center-point).sqrMagnitude<=Mathf.Pow(radius+c.Motor.Radius,2);
        }
        public Vector3 Launch(PootleCharacter c)
        {
            Vector3 direction=Vector3.ProjectOnPlane(c.transform.position-transform.position,Vector3.up).normalized;
            if(direction.sqrMagnitude<.01f)direction=-c.Facing;
            return direction*launchSpeed+Vector3.up*upwardSpeed;
        }
    }
}
