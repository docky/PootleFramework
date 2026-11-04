using UnityEngine;
using UnityEngine.Splines;
namespace Pootle
{
    [ExecuteAlways,RequireComponent(typeof(PootleRail)),AddComponentMenu("Pootle/Spline Rail")]
    public sealed class PootleSplineRail : MonoBehaviour, IPootleRailCurve
    {
        public SplineContainer path;
        [Min(.02f)] public float sampleSpacing=.15f;
        public float surfaceOffset=.12f;
        // Samples are only a search/arc-length table. The rider evaluates the actual spline at every step.
        private const int Subdivisions=4;
        private float[] distances;
        private int sections;
        private PootleRail rail;
        private void OnEnable(){Spline.Changed+=Changed;if(path)Rebuild();}
        private void OnDisable()
        {
            Spline.Changed-=Changed;
            if(rail && ReferenceEquals(rail.Curve,this))rail.Curve=null;
        }
        private void Changed(Spline spline,int index,SplineModification modification)
        {if(path && path.Spline==spline)Rebuild();}
        [ContextMenu("Rebuild rail walking surface")]
        public void Rebuild()
        {
            rail=GetComponent<PootleRail>();rail.Curve=null;
            if(!path||path.Spline.Count<2)return;
            sections=Mathf.Clamp(Mathf.CeilToInt(path.CalculateLength()/Mathf.Max(.02f,sampleSpacing)),2,4096);
            rail.points=new Vector3[sections+1];distances=new float[sections*Subdivisions+1];
            Vector3 previous=Evaluate(0);
            for(int i=0;i<distances.Length;i++)
            {
                Vector3 point=Evaluate(i/(float)(distances.Length-1));
                if(i>0)distances[i]=distances[i-1]+Vector3.Distance(previous,point);
                if(i%Subdivisions==0)rail.points[i/Subdivisions]=transform.InverseTransformPoint(point);
                previous=point;
            }
            rail.Curve=this;
        }
        private Vector3 Evaluate(float t)=>(Vector3)path.EvaluatePosition(Mathf.Clamp01(t))+Vector3.up*surfaceOffset;
        public float Length(int segment)=>distances[(segment+1)*Subdivisions]-distances[segment*Subdivisions];
        private float Parameter(int segment,float along)
        {
            int first=segment*Subdivisions,last=first+Subdivisions;
            float distance=distances[first]+Mathf.Clamp(along,0,Length(segment));
            int i=first;while(i<last-1 && distances[i+1]<distance)i++;
            return (i+Mathf.InverseLerp(distances[i],distances[i+1],distance))/(distances.Length-1);
        }
        public Vector3 Position(int segment,float along)
        {
            float t=Parameter(segment,along);
            // Keep end-of-rail departure continuous for the final free step.
            float excess=along-Mathf.Clamp(along,0,Length(segment));
            return Evaluate(t)+((Vector3)path.EvaluateTangent(t)).normalized*excess;
        }
        public Vector3 Tangent(int segment,float along)=>((Vector3)path.EvaluateTangent(Parameter(segment,along))).normalized;
        public Vector3 Closest(int segment,Vector3 point,out float along)
        {
            float lo=0,hi=Length(segment);
            for(int i=0;i<14;i++)
            {
                float a=Mathf.Lerp(lo,hi,1f/3),b=Mathf.Lerp(lo,hi,2f/3);
                if((Position(segment,a)-point).sqrMagnitude<(Position(segment,b)-point).sqrMagnitude)hi=b;else lo=a;
            }
            along=(lo+hi)*.5f;
            Vector3 result=Position(segment,along),start=Position(segment,0),end=Position(segment,Length(segment));
            if((start-point).sqrMagnitude<(result-point).sqrMagnitude){along=0;result=start;}
            if((end-point).sqrMagnitude<(result-point).sqrMagnitude){along=Length(segment);result=end;}
            return result;
        }
    }
}
