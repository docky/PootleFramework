using System.Collections.Generic;
using UnityEngine;
namespace Pootle
{
    [ExecuteAlways,RequireComponent(typeof(BoxCollider)),AddComponentMenu("Pootle/Wind Volume")]
    public sealed class PootleWind : MonoBehaviour
    {
        public static readonly HashSet<PootleWind> Active=new HashSet<PootleWind>();
        public Vector3 localVelocity=new Vector3(0,14,0);
        [Min(0)] public float acceleration=45;
        private void OnEnable(){Active.Add(this);GetComponent<BoxCollider>().isTrigger=true;}
        private void OnDisable()=>Active.Remove(this);
        public bool Contains(Vector3 point)
        {
            var box=GetComponent<BoxCollider>();var p=box.transform.InverseTransformPoint(point)-box.center;var half=box.size*.5f;
            return Mathf.Abs(p.x)<=half.x && Mathf.Abs(p.y)<=half.y && Mathf.Abs(p.z)<=half.z;
        }
        public Vector3 Velocity=>transform.TransformDirection(localVelocity);
    }
}
