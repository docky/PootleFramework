using UnityEngine;
namespace Pootle.FairyDemo
{
    public sealed class FairyHoop : MonoBehaviour
    {
        public float radius=.9f;
        public GameObject clearedPetals;
        public bool Cleared { get; private set; }
        // Swept crossing of the hoop's plane; proximity alone is never a pass.
        public bool TryCross(Vector3 from,Vector3 to,float bodyClearance)
        {
            if(Cleared)return false;
            var a=transform.InverseTransformPoint(from);var b=transform.InverseTransformPoint(to);
            if(a.z*b.z>0||Mathf.Abs(a.z-b.z)<.00001f)return false;
            var point=Vector3.Lerp(a,b,a.z/(a.z-b.z));
            float opening=Mathf.Max(0,radius-bodyClearance);
            if(new Vector2(point.x,point.y).sqrMagnitude>opening*opening)return false;
            Cleared=true;if(clearedPetals)clearedPetals.SetActive(true);return true;
        }
        public void ResetHoop(){Cleared=false;if(clearedPetals)clearedPetals.SetActive(false);}
    }
}
