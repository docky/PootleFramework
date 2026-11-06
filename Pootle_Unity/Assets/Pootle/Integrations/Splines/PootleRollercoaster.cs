using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace Pootle
{
    public enum PootleCoasterState { Parked, Boarding, Countdown, Riding, Calling, Disembarking, WaitingForExit }

    [AddComponentMenu("Pootle/Rollercoaster")]
    public sealed class PootleRollercoaster : MonoBehaviour
    {
        public SplineContainer path;
        public Transform cart;
        public PootleMachinePassenger passenger;
        public PootleCoasterStation[] stations;
        public Renderer stopRequestLamp;
        [Min(.1f)] public float cruiseSpeed = 12, acceleration = 4, braking = 6;
        [Min(.1f)] public float boardingSeconds = 1.6f, countdownSeconds = 3, exitSeconds = 1.2f;
        public bool simulateAutomatically = true;
        public PootleCoasterState State { get; private set; }
        public PootleCoasterStation CurrentStation { get; private set; }
        public PootleCoasterStation RequestedStation { get; private set; }
        public bool StopRequested => RequestedStation;
        public float Speed { get; private set; }
        public float Distance { get; private set; }
        public float Length { get; private set; }
        public int CountdownLightCount { get; private set; }
        // Rebuild transient spline data after Editor script reloads as well as scene loads.
        [System.NonSerialized] float[] lengths;
        float timer, stopDistance;
        Vector3 boardingStart;
        Quaternion boardingRotation;
        InputActionAsset controls;
        InputAction jump, reset;
        MaterialPropertyBlock lampBlock;
        void OnEnable() { lengths = null; }
        void Start() => Initialize();
        public void Initialize()
        {
            if (!path || !cart || !passenger || stations == null || stations.Length == 0 || !path.Spline.Closed) return;
            lengths = new float[4097]; Vector3 previous = path.EvaluatePosition(0);
            for (int i = 1; i < lengths.Length; i++)
            { Vector3 p = path.EvaluatePosition(i / 4096f); lengths[i] = lengths[i - 1] + Vector3.Distance(previous, p); previous = p; }
            Length = lengths[4096]; if (Length < 1) { lengths = null; return; }
            foreach (var station in stations)
            {
                station.coaster = this; station.Distance = NearestDistance(station.trackAnchor.position);
                if (station.gate) station.gate.receiver = station;
                station.Signal("PUSH TO CALL");
            }
            CurrentStation = stations[0]; Distance = CurrentStation.Distance; State = PootleCoasterState.Parked;
            CurrentStation.Signal("PUSH TO BOARD"); PositionCart();
        }
        public float NearestDistance(Vector3 point)
        {
            int best = 0; float bestSquared = float.PositiveInfinity;
            for (int i = 0; i < lengths.Length - 1; i++)
            { float sq = ((Vector3)path.EvaluatePosition(i / 4096f) - point).sqrMagnitude; if (sq < bestSquared) { best = i; bestSquared = sq; } }
            float lo = Mathf.Max(0, (best - 1) / 4096f), hi = Mathf.Min(1, (best + 1) / 4096f);
            for (int i = 0; i < 18; i++)
            {
                float a = Mathf.Lerp(lo, hi, 1f / 3), b = Mathf.Lerp(lo, hi, 2f / 3);
                if (((Vector3)path.EvaluatePosition(a) - point).sqrMagnitude < ((Vector3)path.EvaluatePosition(b) - point).sqrMagnitude) hi = b; else lo = a;
            }
            float index = (lo + hi) * .5f * 4096; int first = Mathf.Min(4095, (int)index);
            return Mathf.Lerp(lengths[first], lengths[first + 1], index - first);
        }
        float Parameter(float distance)
        {
            float d = Mathf.Repeat(distance, Length); int lo = 0, hi = 4096;
            while (hi - lo > 1) { int mid = (lo + hi) / 2; if (lengths[mid] < d) lo = mid; else hi = mid; }
            return (lo + Mathf.InverseLerp(lengths[lo], lengths[hi], d)) / 4096f;
        }
        void PositionCart()
        {
            float t = Parameter(Distance); Vector3 tangent = path.EvaluateTangent(t);
            cart.SetPositionAndRotation(path.EvaluatePosition(t), Quaternion.LookRotation(tangent.normalized, Vector3.up));
        }
        public bool RequestBoard(PootleCoasterStation station, PootleCharacter character)
        {
            if (lengths == null || Length <= 0) Initialize();
            if (lengths == null || State != PootleCoasterState.Parked || passenger.Occupied || !station || !character) return false;
            if (station != CurrentStation)
            { SetDestination(station); State = PootleCoasterState.Calling; CurrentStation.Signal("CART CALLED"); station.Signal("CART ON ITS WAY"); return false; }
            boardingStart = character.transform.position; boardingRotation = character.transform.rotation;
            if (character.input && character.input.actions)
            {
                controls = Instantiate(character.input.actions);
                jump = controls.FindAction("Character/Jump", false); reset = controls.FindAction("Character/Reset", false);
                jump?.Enable(); reset?.Enable();
            }
            if (!passenger.Begin(character)) { DisposeControls(); return false; }
            timer = 0; State = PootleCoasterState.Boarding; station.gate?.SetOpen(true); station.Signal("TAKE YOUR SEAT"); return true;
        }
        void Update()
        {
            if (lengths == null || Length <= 0) Initialize();
            if (simulateAutomatically) Step(Time.deltaTime, jump != null && jump.WasPressedThisFrame(), reset != null && reset.WasPressedThisFrame());
        }
        public void RequestStop()
        {
            if (!passenger.Occupied || RequestedStation || lengths == null || State == PootleCoasterState.Disembarking || State == PootleCoasterState.WaitingForExit) return;
            PootleCoasterStation next = null; float nearest = float.PositiveInfinity;
            foreach (var s in stations)
            {
                float ahead = Mathf.Repeat(s.Distance - Distance, Length);
                if (ahead < .5f) ahead += Length;
                if (ahead < nearest) { nearest = ahead; next = s; }
            }
            SetDestination(next); next.Signal("STOP REQUESTED"); SetStopLamp(true);
        }
        void SetDestination(PootleCoasterStation station)
        {
            RequestedStation = station;
            float ahead = Mathf.Repeat(station.Distance - Distance, Length); if (ahead < .01f) ahead += Length;
            stopDistance = Distance + ahead;
        }
        public void Step(float dt, bool jumpPressed = false, bool resetPressed = false)
        {
            if (lengths == null || dt <= 0 || !float.IsFinite(dt)) return;
            dt = Mathf.Min(dt, .1f);
            if (resetPressed) { Abort(); return; }
            if (jumpPressed) RequestStop();
            if (State != PootleCoasterState.Parked && State != PootleCoasterState.Calling && !passenger.Occupied) { Abort(); return; }
            timer += dt;
            if (State == PootleCoasterState.Boarding)
            {
                float t = Mathf.Clamp01(timer / boardingSeconds), smooth = t * t * (3 - 2 * t);
                Vector3 feet = Vector3.Lerp(boardingStart, passenger.seat.position - cart.up * .65f, smooth);
                feet += Vector3.up * Mathf.Sin(t * Mathf.PI) * .3f;
                passenger.Place(feet, Quaternion.Slerp(boardingRotation, cart.rotation, smooth), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, t)));
                if (t >= 1) { timer = 0; State = PootleCoasterState.Countdown; CurrentStation.gate?.SetOpen(false); }
            }
            else if (State == PootleCoasterState.Countdown)
            {
                CountdownLightCount = Mathf.Min(3, 1 + Mathf.FloorToInt(timer / (countdownSeconds / 3)));
                CurrentStation.Signal("JUMP: NEXT STATION", CountdownLightCount); Seat();
                if (timer >= countdownSeconds) { timer = 0; State = PootleCoasterState.Riding; }
            }
            else if (State == PootleCoasterState.Riding || State == PootleCoasterState.Calling)
            {
                float slope = ((Vector3)path.EvaluateTangent(Parameter(Distance))).normalized.y;
                float desired = cruiseSpeed * Mathf.Clamp(1 - slope * 1.3f, .55f, 1.6f);
                float remaining = RequestedStation ? Mathf.Max(0, stopDistance - Distance) : float.PositiveInfinity;
                if (RequestedStation) desired = Mathf.Min(desired, Mathf.Sqrt(2 * braking * remaining));
                Speed = Mathf.MoveTowards(Speed, desired, (desired < Speed ? braking : acceleration) * dt);
                float step = Mathf.Min(Speed * dt, remaining); Distance += step; PositionCart(); Seat();
                if (RequestedStation && remaining - step < .025f) Arrive();
            }
            else if (State == PootleCoasterState.WaitingForExit)
            {
                Seat();
                if (passenger.CanRelease(CurrentStation.exitPoint.position)) { timer = 0; State = PootleCoasterState.Disembarking; CurrentStation.gate?.SetOpen(true); }
            }
            else if (State == PootleCoasterState.Disembarking)
            {
                if (!passenger.CanRelease(CurrentStation.exitPoint.position)) { State = PootleCoasterState.WaitingForExit; Seat(); return; }
                float t = Mathf.Clamp01(timer / exitSeconds), s = t * t * (3 - 2 * t);
                passenger.Place(Vector3.Lerp(passenger.seat.position - cart.up * .65f, CurrentStation.exitPoint.position, s),
                    Quaternion.Slerp(cart.rotation, CurrentStation.exitPoint.rotation, s), 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 2)));
                if (t >= 1)
                { passenger.Release(CurrentStation.exitPoint.position, CurrentStation.exitPoint.rotation); DisposeControls(); State = PootleCoasterState.Parked; CurrentStation.Signal("PUSH TO BOARD"); CurrentStation.gate?.SetOpen(false); }
            }
        }
        void Seat() { if (passenger.Occupied) passenger.Place(passenger.seat.position - cart.up * .65f, cart.rotation, 1); }
        void Arrive()
        {
            Distance = stopDistance; Speed = 0; PositionCart(); CurrentStation.Signal("PUSH TO CALL"); CurrentStation = RequestedStation; RequestedStation = null; timer = 0; SetStopLamp(false);
            if (passenger.Occupied) { State = PootleCoasterState.WaitingForExit; CurrentStation.Signal("ARRIVED — MIND THE GAP", 3); }
            else { State = PootleCoasterState.Parked; CurrentStation.Signal("PUSH TO BOARD"); }
        }
        void SetStopLamp(bool on)
        {
            if (!stopRequestLamp) return; lampBlock ??= new MaterialPropertyBlock();
            lampBlock.SetColor("_BaseColor", on ? new Color(1, .65f, .04f) : new Color(.14f, .2f, .19f)); stopRequestLamp.SetPropertyBlock(lampBlock);
        }
        public void Abort()
        {
            if (passenger) passenger.Abort(); DisposeControls(); RequestedStation = null; Speed = 0; State = PootleCoasterState.Parked; SetStopLamp(false);
            if (stations != null) foreach (var s in stations) if (s) s.gate?.SetOpen(false);
            if (CurrentStation && lengths != null) { Distance = CurrentStation.Distance; PositionCart(); CurrentStation.Signal("PUSH TO BOARD"); }
        }
        void DisposeControls() { jump = reset = null; if (controls) { controls.Disable(); Destroy(controls); controls = null; } }
        void OnDisable() => Abort();
    }
}
