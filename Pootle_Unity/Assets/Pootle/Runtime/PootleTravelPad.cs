using UnityEngine;
namespace Pootle
{
    /// <summary>Optional playground access: step onto a marked pad to travel.</summary>
    public sealed class PootleTravelPad : MonoBehaviour
    {
        public PootleCharacter character;
        public Vector3 destination;
        public float facingYaw;
        [Min(.2f)] public float radius=1.3f;
        private void Update()
        {
            if(!character || !character.simulateAutomatically || !character.IsGrounded)return;
            Vector3 delta=character.transform.position-transform.position;
            if(Mathf.Abs(delta.y)>.4f || Vector3.ProjectOnPlane(delta,Vector3.up).sqrMagnitude>radius*radius)return;
            character.Teleport(destination,Quaternion.Euler(0,facingYaw,0));
        }
    }
}
