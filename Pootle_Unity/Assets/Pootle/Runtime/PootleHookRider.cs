using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent]
    public sealed class PootleHookRider : MonoBehaviour
    {
        public PootleDangleHook Hook { get; private set; }
        public Vector3 HookVelocity { get; private set; }
        private Vector3 lastGrip;
        private float cooldown, blockedTime;
        public void ResetRide() { Hook = null; HookVelocity = Vector3.zero; cooldown = blockedTime = 0; }
        public void Drop(PootleCharacter c) { if (Hook) Release(c); }
        public bool Tick(PootleCharacter c, float dt, PootleInputFrame frame, out PootleMotionResult result)
        {
            result = default; cooldown = Mathf.Max(0, cooldown - dt);
            if (!Hook && c.Mode == PootleMode.Dangling) { Release(c); return false; }
            if (Hook)
            {
                if (!Hook.isActiveAndEnabled) { Release(c); return false; }
                HookVelocity = Vector3.ClampMagnitude((Hook.GripPoint - lastGrip) / dt, 25); lastGrip = Hook.GripPoint;
                if (c.ConsumeJump())
                {
                    Vector3 direction = PootleMovement.CameraDirection(frame.move, c.outputCamera, out float magnitude);
                    if (magnitude < 0.1f) direction = Vector3.zero;
                    Vector3 momentum = HookVelocity;
                    Release(c); c.LaunchTraversal(direction * (c.profile.climbJumpSpeed * magnitude), frame.jumpHeld);
                    c.Velocity += Vector3.up * Mathf.Max(0, momentum.y); return false;
                }
                if (!c.profile.automaticGrip && !frame.grip) { Release(c); return false; }
                Vector3 desired = PootleMovement.CameraDirection(frame.move, c.outputCamera, out float inputMagnitude);
                if (inputMagnitude > 0.1f)
                    c.Facing = Vector3.RotateTowards(c.Facing, desired, c.profile.facingSpeed * Mathf.Deg2Rad * dt, 0).normalized;
                Hook.transform.rotation = Quaternion.LookRotation(c.Facing, Vector3.up);
                Vector3 target = Hook.HangingPosition(c.Facing);
                result = c.Motor.MoveTo(target);
                float lag = Vector3.Distance(transform.position, target);
                bool blocked = lag > Hook.maximumBlockedDistance || !c.Motor.CanOccupy(target);
                blockedTime = blocked ? blockedTime + dt : 0;
                c.Velocity = result.displacement / dt;
                if (lag > c.Motor.Radius * 2 || blocked && blockedTime >= Hook.blockedGraceTime) Release(c);
                return true;
            }
            if ((!c.profile.automaticGrip && !frame.grip) || !c.CanGrab || cooldown > 0 || c.Mode == PootleMode.LedgeHop || c.Mode == PootleMode.PoleStanding) return false;
            PootleDangleHook best = null; float nearest = float.PositiveInfinity;
            foreach (var hook in PootleDangleHook.Active)
            {
                if (!hook || !hook.isActiveAndEnabled) continue;
                float distance = Vector3.Distance(transform.position, hook.HangingPosition(c.Facing));
                if (distance > hook.grabRadius || distance >= nearest) continue;
                if (!c.Motor.CanOccupy(hook.HangingPosition(c.Facing))) continue;
                best = hook; nearest = distance;
            }
            if (!best) return false;
            Vector3 destination = best.HangingPosition(c.Facing);
            result = c.Motor.MoveTo(destination);
            if (Vector3.Distance(transform.position, destination) > 0.08f) return false;
            c.GetComponent<PootleRailRider>()?.ResetRide();
            c.GetComponent<PootleTraversal>()?.ResetTraversal();
            best.transform.rotation = Quaternion.LookRotation(c.Facing, Vector3.up);
            Hook = best; lastGrip = best.GripPoint; blockedTime = 0; HookVelocity = c.Velocity = Vector3.zero;
            c.SetMode(PootleMode.Dangling, PootleTransition.Grabbed); return true;
        }
        private void Release(PootleCharacter c)
        {
            Hook = null; cooldown = c.profile.regrabDelay; blockedTime = 0;
            c.Velocity = HookVelocity;
            c.SetMode(PootleMode.Airborne, PootleTransition.Released);
        }
    }
}
