using UnityEngine;
using UnityEngine.Splines;
namespace Pootle
{
    // PLAYER TWO MovingPlatform's speed/optional facing behavior, adapted to a spline and independent of its level manager.
    [DefaultExecutionOrder(-200), DisallowMultipleComponent, AddComponentMenu("Pootle/Spline Mover")]
    public sealed class PootleSplineMover : MonoBehaviour
    {
        public SplineContainer path;
        [Min(0)] public float speed = 3;
        public bool pingPong = true;
        [Min(0),Tooltip("Seconds to wait at the ends of an open ping-pong path.")]
        public float endpointPause;
        public bool rotateToPath;
        [Min(0)] public float turnSpeed = 360;
        public bool simulateAutomatically = true;
        public Vector3 Velocity { get; private set; }
        private readonly float[] distances = new float[257];
        private float phase, length, pauseRemaining;
        private void OnEnable() { phase = 0; pauseRemaining=endpointPause; Rebuild(); }
        public void Rebuild()
        {
            length = 0; distances[0] = 0;
            if (!path || path.Spline.Count < 2) return;
            Vector3 previous = path.EvaluatePosition(0);
            for (int i = 1; i < distances.Length; i++)
            {
                Vector3 p = path.EvaluatePosition(i / 256f);
                length += Vector3.Distance(previous, p); distances[i] = length; previous = p;
            }
        }
        private void Update() { if (simulateAutomatically) Tick(Time.deltaTime); }
        public void Tick(float dt)
        {
            if (dt <= 0 || !path || length <= 0.001f) return;
            bool reverseAtEnds = pingPong && !path.Spline.Closed;
            if(reverseAtEnds && pauseRemaining>0){pauseRemaining=Mathf.Max(0,pauseRemaining-dt);Velocity=Vector3.zero;return;}
            float next=phase+speed*dt;
            if(reverseAtEnds && endpointPause>0 && phase<length && next>=length){phase=length;pauseRemaining=endpointPause;}
            else if(reverseAtEnds && endpointPause>0 && next>=length*2){phase=0;pauseRemaining=endpointPause;}
            else phase = Mathf.Repeat(next, reverseAtEnds ? length * 2 : length);
            float distance = reverseAtEnds ? Mathf.PingPong(phase, length) : phase;
            int low = 0, high = 256;
            while (high - low > 1) { int mid = (low + high) / 2; if (distances[mid] < distance) low = mid; else high = mid; }
            float t = (low + Mathf.InverseLerp(distances[low], distances[high], distance)) / 256f;
            Vector3 position = path.EvaluatePosition(t);
            Velocity = (position - transform.position) / dt;
            transform.position = position;
            if (rotateToPath)
            {
                Vector3 tangent = path.EvaluateTangent(t);
                if (reverseAtEnds && phase > length) tangent = -tangent;
                tangent.y = 0;
                if (tangent.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(tangent), turnSpeed * dt);
            }
            // The character uses Update and explicit casts, so publish moved colliders before its tick.
            Physics.SyncTransforms();
        }
    }
}
