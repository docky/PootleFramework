using System.Collections.Generic;
using UnityEngine;
namespace Pootle
{
    [ExecuteAlways, DisallowMultipleComponent, AddComponentMenu("Pootle/Dangle Hook")]
    public sealed class PootleDangleHook : MonoBehaviour
    {
        public static readonly HashSet<PootleDangleHook> Active = new HashSet<PootleDangleHook>();
        public Vector3 localGripPoint;
        [Min(0.1f)] public float grabRadius = 0.65f;
        [Min(0.5f)] public float hangDistance = 1.7f;
        [Min(0)] public float bodyDistance = 0.7f;
        public Vector3 HangingPosition(Vector3 facing) => GripPoint - Vector3.up * hangDistance - facing * bodyDistance;
        [Min(0.05f)] public float maximumBlockedDistance = 0.3f;
        [Min(0)] public float blockedGraceTime = 0.1f;
        public Vector3 GripPoint => transform.TransformPoint(localGripPoint);
        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);
        private void OnDrawGizmosSelected() { Gizmos.color = Color.magenta; Gizmos.DrawWireSphere(GripPoint, grabRadius); Gizmos.DrawLine(GripPoint, GripPoint - Vector3.up * hangDistance); }
    }
}
