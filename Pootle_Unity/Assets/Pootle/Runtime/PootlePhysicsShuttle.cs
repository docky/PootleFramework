using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent,RequireComponent(typeof(Rigidbody))]
    public sealed class PootlePhysicsShuttle : MonoBehaviour
    {
        public Vector3 localAxis=Vector3.right;
        public float amplitude=3,period=5;
        [Min(0)] public float spring=500,damper=180,maximumForce=1800;
        Rigidbody body;Vector3 origin,axis;float time;
        void Awake(){body=GetComponent<Rigidbody>();origin=body.position;axis=transform.TransformDirection(localAxis).normalized;}
        void FixedUpdate()
        {
            time+=Time.fixedDeltaTime;float rate=2*Mathf.PI/Mathf.Max(.5f,period);
            Vector3 target=origin+axis*(amplitude*Mathf.Sin(time*rate));
            Vector3 velocity=axis*(amplitude*rate*Mathf.Cos(time*rate));
            body.AddForce(Vector3.ClampMagnitude((target-body.position)*spring+(velocity-body.linearVelocity)*damper,maximumForce));
        }
    }
}
