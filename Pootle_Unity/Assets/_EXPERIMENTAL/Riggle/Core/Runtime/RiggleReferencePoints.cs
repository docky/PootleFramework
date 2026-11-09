using UnityEngine;
namespace Docky.RiggleTools
{
    [DisallowMultipleComponent]
    public sealed class RiggleReferencePoints : MonoBehaviour
    {
        public bool useReferences = true;
        public bool inferMissingSide = true;
        public Transform searchRoot;
        public Transform[] points = new Transform[Riggle.PointCount];
        public static int Opposite(int point) => point >= 3 && point <= 7 ? point + 5 : point >= 8 && point <= 12 ? point - 5 : -1;
        public Transform Assigned(Riggle.Point point)
        {
            int i=(int)point;var t=points!=null && i<points.Length?points[i]:null;
            if(!t || t==transform || !t.IsChildOf(transform))return null;
            var rig=GetComponent<Riggle>();
            if(rig && rig.GeneratedBones && t.IsChildOf(rig.GeneratedBones))return null;
            var origin=t.GetComponent<RiggleOriginReference>();
            return origin && (!origin.enabled || origin.landmark!=point)?null:t;
        }
        static Vector3 Position(Transform t) => t.TryGetComponent<RiggleOriginReference>(out var origin)?origin.WorldPosition:t.position;
        public Transform Driven(Riggle.Point point)
        {
            if(!useReferences)return null;
            var direct=Assigned(point);if(direct)return direct;
            int other=Opposite((int)point);return inferMissingSide && other>=0?Assigned((Riggle.Point)other):null;
        }
        public bool TryGetLocal(Riggle rig,Riggle.Point point,out Vector3 local)
        {
            local=default;if(!useReferences)return false;
            var t=Assigned(point);
            if(t){local=rig.transform.InverseTransformPoint(Position(t));return true;}
            int other=Opposite((int)point);
            if(!inferMissingSide || other<0 || !Assigned((Riggle.Point)other))return false;
            local=rig.transform.InverseTransformPoint(Position(Assigned((Riggle.Point)other)));
            local.x=2*rig.GetLocal(Riggle.Point.Pelvis).x-local.x;return true;
        }
    }
}
