using System.Collections.Generic;
using UnityEngine;
namespace Pootle
{
    [ExecuteAlways, AddComponentMenu("Pootle/Climbable Pole or Rope")]
    public sealed class PootlePole : MonoBehaviour
    {
        public static readonly HashSet<PootlePole> Active = new HashSet<PootlePole>();
        [Min(0.1f)] public float length = 6;
        [Min(0.01f)] public float radius = 0.12f;
        public bool climbable = true, standOnTop = true, allowExitTop = true, allowExitBottom = true;
        public bool limitFacing;
        [Range(-180,180)] public float minimumYaw = -180;
        [Range(-180,180)] public float maximumYaw = 180;
        public Transform topBlockMarker, bottomBlockMarker;
        public Vector3 Bottom => transform.position - transform.up * length * 0.5f;
        public Vector3 Top => Bottom + transform.up * length;
        public Vector3 LimitFacing(Vector3 facing)
        {
            if (!limitFacing) return facing;
            var forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            float yaw=Vector3.SignedAngle(forward,facing,Vector3.up);
            return Quaternion.AngleAxis(Mathf.Clamp(yaw,Mathf.Min(minimumYaw,maximumYaw),Mathf.Max(minimumYaw,maximumYaw)),Vector3.up)*forward;
        }
        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);
        private void Update()
        {
            if(topBlockMarker) topBlockMarker.gameObject.SetActive(!allowExitTop);
            if(bottomBlockMarker) bottomBlockMarker.gameObject.SetActive(!allowExitBottom);
        }
        private void Reset() { var c=GetComponent<Collider>(); if(c)c.isTrigger=true; }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color=Color.yellow;
            if(!allowExitTop)Gizmos.DrawSphere(Top,radius*1.8f);
            if(!allowExitBottom)Gizmos.DrawSphere(Bottom,radius*1.8f);
        }
    }
}
