using UnityEngine;
using UnityEngine.Splines;
namespace Pootle
{
    [DefaultExecutionOrder(-40), AddComponentMenu("Pootle/Looping Chair Lift")]
    public sealed class PootleChairLift : MonoBehaviour
    {
        public SplineContainer path;
        public Transform platform;
        public PootleRideSeat ride;
        [Min(.1f)] public float speed = 3.5f;
        [Range(0, 1)] public float startFraction;
        public bool simulateAutomatically = true;
        public float Distance { get; private set; }
        public float Length { get; private set; }
        [System.NonSerialized] float[] lengths;
        void OnEnable() { lengths = null; }
        void Start() => Initialize();
        void OnDisable() { if (ride) ride.Abort(); }
        public void Initialize()
        {
            if (!path || !platform || !ride || path.Spline.Count < 2 || !path.Spline.Closed) return;
            lengths = new float[2049]; Vector3 previous = path.EvaluatePosition(0);
            for (int i = 1; i < lengths.Length; i++)
            { Vector3 p = path.EvaluatePosition(i / 2048f); lengths[i] = lengths[i - 1] + Vector3.Distance(previous, p); previous = p; }
            Length = lengths[2048]; if (Length < .1f) { lengths = null; return; }
            Distance = Mathf.Repeat(startFraction, 1) * Length; Place(); ride.RideVelocity = Vector3.zero;
        }
        float Parameter(float distance)
        {
            float d = Mathf.Repeat(distance, Length); int lo = 0, hi = 2048;
            while (hi - lo > 1) { int mid = (hi + lo) / 2; if (lengths[mid] < d) lo = mid; else hi = mid; }
            return (lo + Mathf.InverseLerp(lengths[lo], lengths[hi], d)) / 2048f;
        }
        void Place()
        {
            float t = Parameter(Distance); Vector3 tangent = Vector3.ProjectOnPlane(path.EvaluateTangent(t), Vector3.up);
            Quaternion facing = tangent.sqrMagnitude > .0001f ? Quaternion.LookRotation(tangent.normalized) : platform.rotation;
            platform.SetPositionAndRotation(path.EvaluatePosition(t), facing);
        }
        void Update() { if (simulateAutomatically) Step(Time.deltaTime); }
        public void Step(float dt)
        {
            if (lengths == null || Length <= 0) Initialize();
            if (lengths == null || dt <= 0 || !float.IsFinite(dt)) return;
            dt = Mathf.Min(dt, .1f); Vector3 before = platform.position;
            Distance = Mathf.Repeat(Distance + Mathf.Max(0, speed) * dt, Length); Place();
            ride.RideVelocity = (platform.position - before) / dt;
        }
    }
}
