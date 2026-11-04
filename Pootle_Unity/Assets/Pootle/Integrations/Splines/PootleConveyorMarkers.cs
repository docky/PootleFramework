using UnityEngine;
using UnityEngine.Splines;
namespace Pootle
{
    public sealed class PootleConveyorMarkers : MonoBehaviour
    {
        public SplineContainer path;
        public PootleConveyor belt;
        public Transform[] markers;
        private float phase,length;
        private void Start(){if(path)length=path.CalculateLength();}
        private void Update()
        {
            if(!path || !belt || markers==null || markers.Length==0 || length<.01f)return;
            phase=Mathf.Repeat(phase+belt.speed*Time.deltaTime/length,1);
            for(int i=0;i<markers.Length;i++)
            {
                float t=Mathf.Repeat(phase+i/(float)markers.Length,1);
                markers[i].SetPositionAndRotation((Vector3)path.EvaluatePosition(t)+Vector3.up*.025f,
                    Quaternion.LookRotation(((Vector3)path.EvaluateTangent(t)).normalized,Vector3.up));
            }
        }
    }
}
