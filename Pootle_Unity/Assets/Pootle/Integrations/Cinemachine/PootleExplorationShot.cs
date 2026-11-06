using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Splines;
using Sirenix.OdinInspector;

namespace Pootle
{
    public enum ExplorationCameraMode { FixedAngleFollow, RailFollow, StaticOrBounded }
    public enum RailOrientation { Authored, LookAtFocus }

    [DefaultExecutionOrder(-60), DisallowMultipleComponent]
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachinePositionComposer))]
    public sealed class PootleExplorationShot : MonoBehaviour
    {
        public ExplorationCameraMode mode;
        public PootleCameraFocus focus;
        [FoldoutGroup("Composition")] public Vector2 pitchYaw = new Vector2(55, 0);
        [FoldoutGroup("Composition"), Range(15, 90)] public float fieldOfView = 30;
        [FoldoutGroup("Composition")] public bool orthographic;
        [FoldoutGroup("Composition"), Min(1)] public float orthographicSize = 14;
        [FoldoutGroup("Composition"), Min(1)] public float distance = 28;
        [FoldoutGroup("Composition")] public Vector3 damping = new Vector3(.35f, .8f, .35f);
        [FoldoutGroup("Composition")] public Vector2 screenPosition = new Vector2(0, .12f);
        [FoldoutGroup("Composition")] public Vector2 deadZone = new Vector2(.12f, .1f);
        [FoldoutGroup("Composition"), Tooltip("Outer screen limits; motion between dead and outer limits is damped.")]
        public Vector2 softZone = new Vector2(.75f, .7f);
        [FoldoutGroup("Composition"), Range(0, 12)] public float airborneWidening = 0;
        [FoldoutGroup("Rail")] public SplineContainer spline;
        [FoldoutGroup("Rail"), Tooltip("World-space guide endpoints used when no spline is assigned.")]
        public Vector3 lineStart, lineEnd = Vector3.forward * 20;
        [FoldoutGroup("Rail"), Tooltip("World-space offset from the ground guide to the camera rail.")]
        public Vector3 railOffset = new Vector3(0, 24, -17);
        [FoldoutGroup("Rail")] public bool projectHorizontally = true;
        [FoldoutGroup("Rail")] public RailOrientation railOrientation;
        [FoldoutGroup("Rail"), Min(.01f)] public float railDamping = .4f;
        [FoldoutGroup("Rail"), Range(16, 128)] public int projectionSamples = 64;
        [FoldoutGroup("Static and Bounded")] public Vector3 anchor;
        [FoldoutGroup("Static and Bounded")] public bool bounded;
        [FoldoutGroup("Static and Bounded")] public Vector3 focusOrigin;
        [FoldoutGroup("Static and Bounded")] public Vector3 positionBounds = new Vector3(6, 0, 4);
        [ShowInInspector, ReadOnly] public float RailT { get; private set; }
        [ShowInInspector, ReadOnly] public Vector3 ClosestRailPoint { get; private set; }
        public float AuthoredYaw => pitchYaw.y;
        CinemachineCamera rig;
        CinemachinePositionComposer composer;
        Vector3 positionVelocity;
        float railVelocity, lensValue;
        bool initialized;
        ExplorationCameraMode previousMode;
        public CinemachineCamera Rig => rig ? rig : GetComponent<CinemachineCamera>();
        public Quaternion AuthoredRotation => Quaternion.Euler(pitchYaw.x, pitchYaw.y, 0);
        void OnEnable() { initialized = false; ApplySettings(); }
        void LateUpdate() => TickShot(Time.deltaTime);
        [Button("Apply shot settings")]
        public void ApplySettings()
        {
            rig = GetComponent<CinemachineCamera>(); composer = GetComponent<CinemachinePositionComposer>();
            if (!rig || !composer) return;
            if (focus) rig.Follow = focus.transform;
            composer.enabled = mode == ExplorationCameraMode.FixedAngleFollow;
            composer.CameraDistance = distance; composer.Damping = damping;
            var composition = ScreenComposerSettings.Default;
            composition.ScreenPosition = screenPosition;
            composition.DeadZone.Enabled = true; composition.DeadZone.Size = deadZone;
            composition.HardLimits.Enabled = true; composition.HardLimits.Size = softZone;
            composition.Validate(); composer.Composition = composition;
            composer.Lookahead.Enabled = false; // Shared focus owns lookahead, avoiding double prediction.
            rig.Lens.ModeOverride = orthographic ? LensSettings.OverrideModes.Orthographic : LensSettings.OverrideModes.Perspective;
            rig.Lens.OrthographicSize = orthographicSize;
            if (!Application.isPlaying) rig.Lens.FieldOfView = fieldOfView;
            if (mode != ExplorationCameraMode.RailFollow || railOrientation == RailOrientation.Authored) transform.rotation = AuthoredRotation;
        }
        public void TickShot(float dt)
        {
            ApplySettings();
            if (!rig || !focus) return;
            bool reset = !initialized || previousMode != mode;
            if (reset) { initialized = true; previousMode = mode; railVelocity = 0; positionVelocity = Vector3.zero; lensValue = fieldOfView; rig.PreviousStateIsValid = false; }
            float wantedFov = fieldOfView + (focus.IsAirborne ? airborneWidening : 0);
            lensValue = Mathf.Lerp(lensValue, wantedFov, 1 - Mathf.Exp(-Mathf.Max(0, dt) / .6f));
            rig.Lens.FieldOfView = lensValue;
            if (mode == ExplorationCameraMode.RailFollow)
            {
                float desiredT = Project(focus.transform.position);
                ClosestRailPoint = EvaluateRail(desiredT);
                float t = reset ? desiredT : Mathf.SmoothDamp(RailT, desiredT, ref railVelocity, railDamping, Mathf.Infinity, dt);
                RailT = Mathf.Clamp01(t); transform.position = EvaluateRail(RailT);
                if (railOrientation == RailOrientation.LookAtFocus)
                {
                    Vector3 aim = focus.transform.position - transform.position;
                    if (aim.sqrMagnitude > .001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aim), reset ? 1 : 1 - Mathf.Exp(-dt / .4f));
                }
            }
            else if (mode == ExplorationCameraMode.StaticOrBounded)
            {
                Vector3 desired = bounded ? ClampPosition(anchor + focus.transform.position - focusOrigin) : anchor;
                transform.position = reset || !bounded ? desired : ClampPosition(Vector3.SmoothDamp(transform.position, desired, ref positionVelocity, railDamping, Mathf.Infinity, dt));
            }
        }
        public void ResetTracking() { initialized = false; if (Rig) Rig.PreviousStateIsValid = false; }
        public Vector3 ClampPosition(Vector3 value)
        {
            var half = new Vector3(Mathf.Abs(positionBounds.x), Mathf.Abs(positionBounds.y), Mathf.Abs(positionBounds.z)) * .5f;
            return new Vector3(Mathf.Clamp(value.x, anchor.x-half.x, anchor.x+half.x), Mathf.Clamp(value.y, anchor.y-half.y, anchor.y+half.y), Mathf.Clamp(value.z, anchor.z-half.z, anchor.z+half.z));
        }
        public Vector3 EvaluateGuide(float t) => spline && spline.Spline.Count > 1 ? (Vector3)spline.EvaluatePosition(Mathf.Clamp01(t)) : Vector3.Lerp(lineStart, lineEnd, Mathf.Clamp01(t));
        public Vector3 EvaluateRail(float t) => EvaluateGuide(t) + railOffset;
        public float Project(Vector3 player)
        {
            float best = float.PositiveInfinity, bestT = 0;
            int count = spline ? Mathf.Max(16, projectionSamples) : 1;
            Vector3 a = EvaluateGuide(0);
            for (int i = 1; i <= count; i++)
            {
                Vector3 b = EvaluateGuide(i / (float)count), start = a, end = b, point = player;
                if (projectHorizontally) start.y = end.y = point.y = 0;
                var ab = end-start; float u = ab.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector3.Dot(point-start, ab)/ab.sqrMagnitude) : 0;
                float d = (point-(start+ab*u)).sqrMagnitude;
                if (d < best) { best=d; bestT=(i-1+u)/count; }
                a=b;
            }
            return Mathf.Clamp01(bestT);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1,.7f,.3f); Gizmos.DrawRay(transform.position, AuthoredRotation * Vector3.forward * 5);
            if (mode == ExplorationCameraMode.RailFollow)
            {
                for (int i=1;i<=64;i++) { Gizmos.DrawLine(EvaluateRail((i-1)/64f), EvaluateRail(i/64f)); }
                if (focus) { var p=EvaluateRail(Project(focus.transform.position)); Gizmos.DrawSphere(p,.35f); Gizmos.DrawLine(focus.transform.position,p); }
            }
            if (mode == ExplorationCameraMode.StaticOrBounded) { Gizmos.DrawWireCube(anchor, bounded ? positionBounds : Vector3.one); }
        }
    }
}
