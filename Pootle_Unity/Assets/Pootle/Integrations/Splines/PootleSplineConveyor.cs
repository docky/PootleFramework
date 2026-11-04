using UnityEngine;
using UnityEngine.Splines;
namespace Pootle
{
    [RequireComponent(typeof(PootleConveyor)),AddComponentMenu("Pootle/Spline Conveyor")]
    public sealed class PootleSplineConveyor : MonoBehaviour,IPootleConveyorPath
    {
        public SplineContainer path;
        public Vector3 DirectionAt(Vector3 point)
        {
            if(!path || path.Spline.Count<2)return transform.forward;
            // Coarse global search, then refine on the actual curve. Never use collider chunks as directions.
            const int samples=128;float best=0,distance=float.PositiveInfinity;
            for(int i=0;i<=samples;i++)
            {
                float t=i/(float)samples;float d=((Vector3)path.EvaluatePosition(t)-point).sqrMagnitude;
                if(d<distance){distance=d;best=t;}
            }
            float lo=Mathf.Max(0,best-1f/samples),hi=Mathf.Min(1,best+1f/samples);
            for(int i=0;i<12;i++)
            {
                float a=Mathf.Lerp(lo,hi,1f/3),b=Mathf.Lerp(lo,hi,2f/3);
                if(((Vector3)path.EvaluatePosition(a)-point).sqrMagnitude<((Vector3)path.EvaluatePosition(b)-point).sqrMagnitude)hi=b;else lo=a;
            }
            return ((Vector3)path.EvaluateTangent((lo+hi)*.5f)).normalized;
        }
    }
}
