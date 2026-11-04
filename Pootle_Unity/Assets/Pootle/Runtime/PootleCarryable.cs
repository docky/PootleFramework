using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent,RequireComponent(typeof(BoxCollider),typeof(Rigidbody))]
    public sealed class PootleCarryable : MonoBehaviour
    {
        public bool IsHeld { get; private set; }
        public BoxCollider Shape => GetComponent<BoxCollider>();
        public Rigidbody Body => GetComponent<Rigidbody>();
        public Vector3 HalfSize => Vector3.Scale(Shape.size,Abs(transform.lossyScale))*.5f;
        private bool wasKinematic,usedGravity,wasTrigger;
        private static Vector3 Abs(Vector3 v)=>new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z));
        public Vector3 RootForCenter(Vector3 center,Quaternion rotation)=>center-rotation*Vector3.Scale(Shape.center,transform.lossyScale);
        public void Take(Vector3 center,Quaternion rotation)
        {
            wasKinematic=Body.isKinematic;usedGravity=Body.useGravity;wasTrigger=Shape.isTrigger;
            if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
            Body.isKinematic=true;Body.useGravity=false;Shape.isTrigger=true;IsHeld=true;
            Place(center,rotation);
        }
        public void Place(Vector3 center,Quaternion rotation)=>transform.SetPositionAndRotation(RootForCenter(center,rotation),rotation);
        public void Release(Vector3 center,Quaternion rotation)
        {
            Place(center,rotation);Shape.isTrigger=wasTrigger;Body.isKinematic=wasKinematic;Body.useGravity=usedGravity;
            if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;Body.WakeUp();}
            IsHeld=false;Physics.SyncTransforms();
        }
    }
}
