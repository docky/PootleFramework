using UnityEngine;

namespace Pootle
{
    // Optional traversal authoring is carried by surface markers. All motion still goes through the capsule motor.
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Traversal")]
    public sealed class PootleTraversal : MonoBehaviour
    {
        private readonly Collider[] nearby = new Collider[24];
        private Collider wall;
        private Vector3 normal, ledgeDestination, hopStart, poleOffset;
        private PootlePole pole, hopPole;
        private float cooldown, stateTime;
        private bool automaticHop;
        public Collider AttachedWall => wall;
        public PootlePole AttachedPole => pole;
        public Vector3 SurfaceNormal => normal;
        // Presentation uses the full rock normal; locomotion keeps its existing horizontal grip basis.
        public Vector3 VisualSurfaceNormal
        {
            get
            {
                if (!wall) return normal;
                var motor = GetComponent<PootleMotor>();
                Vector3 center = transform.position + motor.center;
                var surface = wall.GetComponentInParent<PootleClimbSurface>();
                if (surface && surface.cylindrical)
                {
                    Vector3 radial = Vector3.ProjectOnPlane(center - wall.bounds.center, wall.transform.up);
                    return radial.sqrMagnitude > .00001f ? radial.normalized : normal;
                }
                // Raycast the actual face, including non-convex rock meshes, without moving the motor.
                return wall.Raycast(new Ray(center, -normal), out var hit, motor.Radius + 1)
                    ? hit.normal : normal;
            }
        }
        public void ResetTraversal() { wall = null; pole = hopPole = null; cooldown = stateTime = 0; }

        public void Drop(PootleCharacter c) { if(wall || pole || c.Mode==PootleMode.LedgeHop) Release(c); }
        public bool Tick(PootleCharacter c, float dt, PootleInputFrame frame, out PootleMotionResult result)
        {
            result = default;
            cooldown = Mathf.Max(0, cooldown - dt);
            var p = c.profile;
            if (c.Mode == PootleMode.LedgeHop)
            {
                stateTime += dt;
                float hopHeight=automaticHop?.12f:p.topOutHopHeight;
                // Higher catches start at hand height, so a fixed short duration would
                // launch the visual upward. Bound the peak lift speed of SmoothStep.
                float duration=Mathf.Max(.15f,automaticHop?p.smallHopDuration:p.topOutDuration);
                duration=Mathf.Max(duration,(Mathf.Max(0,ledgeDestination.y-hopStart.y)+hopHeight)*1.5f/(.45f*Mathf.Max(.1f,p.topOutLiftSpeed)));
                float t = Mathf.Clamp01(stateTime / duration);
                // Smooth lift and transfer phases meet with zero velocity at the lip.
                float liftPhase = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / 0.45f));
                float across = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.45f, 1, t));
                Vector3 target = Vector3.Lerp(hopStart, ledgeDestination, across);
                target.y = t < 0.45f ? Mathf.Lerp(hopStart.y, ledgeDestination.y + (automaticHop ? .12f : p.topOutHopHeight), liftPhase)
                    : Mathf.Lerp(ledgeDestination.y + (automaticHop ? .12f : p.topOutHopHeight), ledgeDestination.y, across);
                result = c.Motor.Move(target - transform.position, false);
                c.Velocity = result.displacement / dt;
                if (t >= 1)
                {
                    c.Velocity=Vector3.zero;
                    if(hopPole && Vector3.Distance(transform.position,hopPole.Top)<.08f)
                    {pole=hopPole;hopPole=null;c.SetMode(PootleMode.PoleStanding,PootleTransition.TopOut);}
                    else
                    {
                        ClearAttachment(c);
                        if(!c.GetComponent<PootleRailRider>().TryMount(c,.2f,.45f) && !c.FinishTraversalTopOut())
                            c.SetMode(PootleMode.Airborne,PootleTransition.Released);
                    }
                }
                return true;
            }
            if(c.Mode==PootleMode.PoleStanding)
            {
                if(!pole || !pole.isActiveAndEnabled){Release(c);return false;}
                Vector3 desired=PootleMovement.CameraDirection(frame.move,c.outputCamera,out float amount);
                if(amount>.1f)c.Facing=pole.LimitFacing(desired);
                if(c.ConsumeJump())
                {
                    // Standing on a post is a stationary support, not a climbing launch.
                    Release(c);c.LaunchTraversal(Vector3.zero,frame.jumpHeld);return false;
                }
                result=c.Motor.MoveTo(pole.Top);c.Velocity=Vector3.zero;c.RailSupported();
                return true;
            }
            if (c.Mode == PootleMode.PoleClimbing)
            {
                if (!pole || !pole.isActiveAndEnabled || (!p.automaticGrip && !frame.grip)) { Release(c); return false; }
                if (c.ConsumeJump()) { Jump(c, frame.jumpHeld); return false; }
                c.Facing = pole.LimitFacing(Quaternion.AngleAxis(frame.move.x * p.poleTurnSpeed * dt, Vector3.up) * c.Facing);
                float h = Vector3.Dot(transform.position + c.Motor.center - pole.Bottom, pole.transform.up) + frame.move.y*p.climbSpeed*dt;
                if(h<0 && pole.allowExitBottom){Release(c);c.BlockGrabs();return false;}
                if(h>=pole.length && frame.move.y>0 && pole.allowExitTop)
                {
                    if(pole.standOnTop){hopPole=pole;ledgeDestination=pole.Top;StartHop(c);return true;}
                    Release(c);c.BlockGrabs();return false;
                }
                h=Mathf.Clamp(h,0,pole.length);
                Vector3 desiredOffset = -Vector3.ProjectOnPlane(c.Facing, pole.transform.up).normalized * (pole.radius + p.poleBodyDistance);
                poleOffset = Vector3.MoveTowards(poleOffset, desiredOffset, p.poleAttachmentSpeed * dt);
                Vector3 target = pole.Bottom + pole.transform.up * h + poleOffset - c.Motor.center;
                result = c.Motor.Move(target - transform.position, false);
                c.Velocity = result.displacement / dt;
                if(frame.move.y<0 && c.Support.walkable && c.Support.distance<.05f && pole.allowExitBottom){Release(c);c.BlockGrabs();}
                return true;
            }
            if (c.Mode == PootleMode.LedgeHanging)
            {
                if (!wall || !wall.enabled || !wall.gameObject.activeInHierarchy || frame.move.y < -0.5f) { Release(c); return false; }
                if (c.ConsumeJump()) { Jump(c, frame.jumpHeld); return false; }
                c.Velocity = Vector3.zero;
                stateTime += dt;
                if (stateTime > 0.12f && c.Motor.CanOccupy(ledgeDestination)) StartHop(c);
                return true;
            }
            if (c.Mode == PootleMode.Climbing)
            {
                if (!wall || !wall.enabled || !wall.gameObject.activeInHierarchy || (!p.automaticGrip && !frame.grip)) { Release(c); return false; }
                if (c.ConsumeJump()) { Jump(c, frame.jumpHeld); return false; }
                var surface=wall.GetComponentInParent<PootleClimbSurface>();
                if(!surface || !surface.isActiveAndEnabled){Release(c);return false;}
                if (ResolveWallTop(c))return true;
                Vector3 oldNormal=normal;
                Vector3 side=Vector3.Cross(normal,Vector3.up);
                Vector3 target=transform.position+Vector3.up*frame.move.y*p.climbSpeed*dt+side*frame.move.x*p.climbSidewaysSpeed*dt;
                if(surface.TryFollow(wall,target+c.Motor.center,normal,c.Motor.Radius+c.Motor.Skin*2,out var attached,out var nextNormal))
                {
                    result=c.Motor.MoveTo(attached-c.Motor.center);
                    // A blocked sweep must not rotate the grip around an unreachable corner.
                    if(Vector3.Distance(transform.position,attached-c.Motor.center)<.08f)
                    {
                        normal=nextNormal;
                        if(surface.rotateCamera)c.RotateClimbingView(Vector3.SignedAngle(oldNormal,normal,Vector3.up));
                    }
                }
                else { c.Velocity=Vector3.zero; return true; }
                c.Facing=-normal;c.Velocity=result.displacement/dt;
                if(ResolveWallTop(c))return true;
                if (frame.move.y < 0 && c.Support.walkable) Release(c);
                return true;
            }
            if (cooldown > 0 || !c.CanGrab || c.Mode == PootleMode.Sliding || c.Mode==PootleMode.RailWalking || c.Mode==PootleMode.RailSliding) return false;
            if (TrySmallHop(c,frame)) return true;
            if (frame.grip || p.automaticGrip)
            {
                int count = c.Motor.FindGripPoles(nearby, p.climbReach);
                PootlePole closest = null; float best = float.PositiveInfinity;
                Vector3 center = transform.position + c.Motor.center;
                for (int i = 0; i < count; i++)
                {
                    var candidate = nearby[i].GetComponentInParent<PootlePole>();
                    if (!candidate || !candidate.climbable || !candidate.isActiveAndEnabled || candidate.length < c.Motor.Height*.75f) continue;
                    float h = Mathf.Clamp(Vector3.Dot(center - candidate.Bottom, candidate.transform.up), 0, candidate.length);
                    float distance = (center - (candidate.Bottom + candidate.transform.up * h)).sqrMagnitude;
                    if (distance < best) { closest = candidate; best = distance; }
                }
                if (closest)
                {
                    pole = closest; c.Facing=pole.LimitFacing(c.Facing);
                    float h = Mathf.Clamp(Vector3.Dot(center - pole.Bottom, pole.transform.up), 0, pole.length);
                    poleOffset = Vector3.ProjectOnPlane(center - (pole.Bottom + pole.transform.up * h), pole.transform.up);
                    if (poleOffset.sqrMagnitude < 0.001f) poleOffset = -c.Facing * (pole.radius + c.Motor.Radius);
                    c.Velocity = Vector3.zero; c.SetMode(PootleMode.PoleClimbing, PootleTransition.Grabbed);
                    return true;
                }
            }
            Vector3 approach = PootleMovement.CameraDirection(frame.move, c.outputCamera, out float magnitude);
            if (magnitude < 0.1f) approach = c.Facing;
            if (WallRay(c, approach, out var front))
            {
                normal = Vector3.ProjectOnPlane(front.normal, Vector3.up).normalized;
                var climbSurface=front.collider.GetComponentInParent<PootleClimbSurface>();
                if ((frame.grip || p.automaticGrip) && climbSurface && climbSurface.isActiveAndEnabled)
                {
                    wall = front.collider; c.Velocity = Vector3.zero; c.Facing = -normal;
                    c.SetMode(PootleMode.Climbing, PootleTransition.Grabbed);
                    ResolveWallTop(c);return true;
                }
                // An unmarked physics prop is a push contact, not a stable ledge.
                // Otherwise an approaching paddle can interrupt a nudge with a top-out.
                if(front.rigidbody && !front.rigidbody.isKinematic)return false;
                if (!c.IsGrounded && c.Velocity.y <= 0 && FindTop(c, normal, p.ledgeHandHeight + 0.25f, out var top,front.collider,p.ledgeMaximumTopAngle))
                {
                    wall=front.collider;ledgeDestination=top;
                    // A high edge landing already overlaps the lip: resolve upward,
                    // rather than leaving a torso-high wall grip or snapping down first.
                    if(top.y-transform.position.y<p.ledgeHandHeight && c.Motor.CanOccupy(top))
                    {StartHop(c);automaticHop=true;return true;}
                    Vector3 hang = transform.position; hang.y = top.y - p.ledgeHandHeight;
                    if (c.Motor.CanOccupy(hang))
                    {
                        wall = front.collider; ledgeDestination = top; stateTime = 0;
                        result = c.Motor.Move(hang - transform.position, false);
                        c.Velocity = Vector3.zero; c.Facing = -normal;
                        c.SetMode(PootleMode.LedgeHanging, PootleTransition.Grabbed); return true;
                    }
                }
            }
            return false;
        }

        private bool ResolveWallTop(PootleCharacter c)
        {
            if(!FindTop(c,normal,c.profile.ledgeHandHeight,out var top,wall,c.Motor.slopeLimit))return false;
            ledgeDestination=top;
            if(!c.Motor.CanOccupy(top))
            {
                // A blocked destination keeps the head at the lip, never the torso above it.
                Vector3 hang=transform.position;hang.y=Mathf.Min(hang.y,top.y-c.profile.ledgeHandHeight);
                c.Motor.MoveTo(hang);c.Velocity=Vector3.zero;stateTime=0;
                c.SetMode(PootleMode.LedgeHanging,PootleTransition.Grabbed);return true;
            }
            StartHop(c);automaticHop=true;return true;
        }

        private bool TrySmallHop(PootleCharacter c,PootleInputFrame frame)
        {
            if(!c.IsGrounded || !c.profile.smallObstacleHop || frame.move.sqrMagnitude<.1f)return false;
            var direction=PootleMovement.CameraDirection(frame.move,c.outputCamera,out _);
            if(!c.Motor.Ray(transform.position+Vector3.up*.25f,direction,c.Motor.Radius+.14f,PootleProbeKind.Wall,out var obstacle)
                || Mathf.Abs(obstacle.normal.y)>.1f)return false;
            if(obstacle.rigidbody && !obstacle.rigidbody.isKinematic)return false;
            Vector3 outward=Vector3.ProjectOnPlane(obstacle.normal,Vector3.up).normalized;
            if(!FindTop(c,outward,c.profile.smallHopHeight,out var top))return false;
            float rise=top.y-transform.position.y;
            if(rise<=c.Motor.stepHeight+c.Motor.Skin*3 || !c.Motor.CanOccupy(top))return false;
            ledgeDestination=top;wall=obstacle.collider;normal=outward;StartHop(c);automaticHop=true;return true;
        }

        private bool WallRay(PootleCharacter c, Vector3 direction, out RaycastHit hit)
        {
            float reach = c.Motor.Radius + c.profile.climbReach;
            if (c.Motor.Ray(transform.position + Vector3.up * 1.1f, direction, reach, PootleProbeKind.Wall, out hit)
                && Mathf.Abs(hit.normal.y) < 0.35f) return true;
            return c.Motor.Ray(transform.position + Vector3.up * 0.05f, direction, reach, PootleProbeKind.Wall, out hit)
                && Mathf.Abs(hit.normal.y) < 0.35f;
        }

        private bool FindTop(PootleCharacter c, Vector3 outward, float maxRise, out Vector3 destination,Collider ledge=null,float maxAngle=-1)
        {
            destination = default;
            // Near, middle and deep samples support narrow wall tops as well as wide platforms.
            for(int sample=2;sample>=0;sample--)
            {
                float inset=c.Motor.Radius*(1.1f+sample*.5f)+c.Motor.Skin*3;
                Vector3 above=transform.position-outward*inset+Vector3.up*(maxRise+.15f);
                if(!c.Motor.Ray(above,Vector3.down,maxRise+.3f,PootleProbeKind.Ledge,out var top)
                    || Vector3.Angle(top.normal,Vector3.up)>(maxAngle<0?c.Motor.slopeLimit:maxAngle))continue;
                if(ledge)
                {
                    // A distant platform behind a wall is not its lip. Validate the
                    // actual face just below this top and a hand-width of support.
                    if(top.collider!=ledge)continue;
                    Vector3 lipOrigin=transform.position;lipOrigin.y=top.point.y-.12f;
                    if(!c.Motor.Ray(lipOrigin,-outward,inset+.1f,PootleProbeKind.Ledge,out var lip)
                        || lip.collider!=ledge || Mathf.Abs(lip.normal.y)>.35f)continue;
                    Vector3 side=Vector3.Cross(outward,Vector3.up)*.18f;
                    bool supported=true;
                    for(int sign=-1;sign<=1;sign+=2)
                        if(!c.Motor.Ray(top.point+side*sign+Vector3.up*.2f,Vector3.down,.45f,PootleProbeKind.Ledge,out var hand)
                            || hand.collider!=ledge || Vector3.Angle(hand.normal,Vector3.up)>maxAngle
                            || Mathf.Abs(hand.point.y-top.point.y)>.12f){supported=false;break;}
                    if(!supported)continue;
                }
                float rise=top.point.y-transform.position.y;
                if(rise<.05f||rise>maxRise)continue;
                destination=top.point+Vector3.up*c.Motor.Skin*3;
                return true;
            }
            return false;
        }
        public bool CatchPoleTop(PootleCharacter c,Vector3 from,Vector3 to)
        {
            if(c.Velocity.y>0 || cooldown>0 || pole)return false;
            PootlePole best=null;float nearest=float.PositiveInfinity;
            foreach(var candidate in PootlePole.Active)
            {
                if(!candidate || !candidate.isActiveAndEnabled || !candidate.standOnTop || !candidate.allowExitTop)continue;
                var top=candidate.Top;
                if(from.y<top.y-.03f || to.y>top.y+.03f || to.y>=from.y)continue;
                float t=Mathf.Clamp01((from.y-top.y)/(from.y-to.y));
                Vector3 crossing=Vector3.Lerp(from,to,t);
                float d=Vector3.ProjectOnPlane(crossing-top,Vector3.up).magnitude;
                if(d>candidate.radius+c.Motor.Radius*.75f || d>=nearest || !c.Motor.CanOccupy(top))continue;
                best=candidate;nearest=d;
            }
            if(!best)return false;
            c.Motor.MoveTo(best.Top);if(Vector3.Distance(transform.position,best.Top)>.05f)return false;
            c.ReportLanding(-c.Velocity.y);
            pole=best;c.Facing=pole.LimitFacing(c.Facing);c.Velocity=Vector3.zero;c.RailSupported();
            c.SetMode(PootleMode.PoleStanding,PootleTransition.Landed);return true;
        }

        private void StartHop(PootleCharacter c)
        {
            automaticHop=false;
            if (!c.Motor.CanOccupy(ledgeDestination))
            {
                stateTime = 0; c.SetMode(PootleMode.LedgeHanging, PootleTransition.Grabbed); return;
            }
            hopStart = transform.position; stateTime = 0; c.Velocity = Vector3.zero;
            c.SetMode(PootleMode.LedgeHop, PootleTransition.TopOut);
        }
        private void Jump(PootleCharacter c, bool held)
        {
            Vector3 direction = c.Facing;
            Release(c);
            c.LaunchTraversal(direction * c.profile.climbJumpSpeed, held);
        }
        private void ClearAttachment(PootleCharacter c)
        {
            wall = null; pole = hopPole = null; stateTime = 0; cooldown = c.profile.regrabDelay;
            c.BlockGrabs();
        }
        private void Release(PootleCharacter c)
        {
            ClearAttachment(c);
            c.SetMode(PootleMode.Airborne, PootleTransition.Released);
        }
    }
}
