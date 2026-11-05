using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(CapsuleCollider), typeof(Rigidbody))]
    [AddComponentMenu("Pootle/Capsule Motor")]
    public sealed class PootleMotor : MonoBehaviour
    {
        [Min(0.05f)] public float radius = 0.5f;
        [Min(0.1f)] public float height = 2f;
        [Tooltip("Local capsule centre in metres. The default root is at the feet.")]
        public Vector3 center = Vector3.up;
        [Range(0.001f, 0.1f)] public float skin = 0.01f;
        [Range(0, 0.6f)] public float stepHeight = 0.6f;
        [Range(0, 80)] public float slopeLimit = 45f;
        public LayerMask collisionMask = Physics.DefaultRaycastLayers;
        [Header("Physics interaction")]
        public bool enablePhysicsInteraction=true;
        [Min(1)] public float characterMass=60;
        [Min(0)] public float pushForceScale=2;
        [Min(0)] public float maximumPushForce=1200;
        [Min(0)] public float maximumIncomingSpeed=10;
        [Range(0,1)] public float incomingReactionScale=.12f;
        public bool applyStandingDownwardForce=true;
        [Min(0)] public float standingDownwardForceScale=1;
        public readonly PootleContact[] Contacts = new PootleContact[24];
        public int ContactCount { get; private set; }
        public int QueryCount { get; private set; }
        public PootleDebugSnapshot Capture { get; set; }
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private CapsuleCollider capsule;
        private float tickDeltaTime=1f/60;
        private readonly System.Collections.Generic.HashSet<Rigidbody> pushedBodies = new System.Collections.Generic.HashSet<Rigidbody>();

        public float Radius => Mathf.Max(0.05f, radius);
        public float Height => Mathf.Max(height, 2 * Radius);
        public float Skin => Mathf.Clamp(skin, 0.001f, Radius * 0.25f);

        private void Awake() => Configure();
        private void Reset() => Configure();
        private void OnEnable() { Configure(); capsule.enabled = true; }
        private void OnDisable() { if (capsule) capsule.enabled = false; }

        public void Configure()
        {
            capsule = GetComponent<CapsuleCollider>();
            if (!capsule) return;
            capsule.isTrigger = true;
            capsule.direction = 1;
            capsule.radius = Radius - Skin;
            capsule.height = Height - 2 * Skin;
            capsule.center = center;
            var body = GetComponent<Rigidbody>();
            if (body) { body.isKinematic = true; body.useGravity = false; }
        }

        public void BeginTick(float dt=1f/60) { ContactCount = 0; QueryCount = 0; tickDeltaTime=Mathf.Max(.0001f,dt);pushedBodies.Clear(); }

        private void PushDynamicBody(RaycastHit hit,Vector3 attemptedVelocity)
        {
            var other=hit.rigidbody;
            if(!enablePhysicsInteraction || !other || other.isKinematic || other.transform.IsChildOf(transform)
                || Vector3.Angle(hit.normal,Vector3.up)<=slopeLimit || !pushedBodies.Add(other))return;
            // Relative normal speed and reduced mass: moving-away bodies receive no
            // extra push, heavier bodies respond less, and off-centre contact creates torque.
            float closing=Mathf.Max(0,-Vector3.Dot(attemptedVelocity-other.GetPointVelocity(hit.point),hit.normal));
            float mass=Mathf.Max(1,characterMass),reducedMass=mass*other.mass/(mass+other.mass);
            float impulse=Mathf.Min(closing*reducedMass*pushForceScale,maximumPushForce*tickDeltaTime);
            if(impulse>0)other.AddForceAtPosition(-hit.normal*impulse,hit.point,ForceMode.Impulse);
        }
        public void ReceiveBodyMotion(PootleCharacter character)
        {
            if(!enablePhysicsInteraction || !(character.IsGrounded || character.IsRolling || character.Mode==PootleMode.Airborne || character.Mode==PootleMode.Gliding))return;
            Endpoints(transform.position,out var a,out var b);
            // A fixed contact skin, never a velocity-scaled influence radius.
            // Fast props must reach the capsule before transferring momentum.
            float contactMargin=Mathf.Min(.015f,Skin*1.5f);
            int count=Physics.OverlapCapsuleNonAlloc(a,b,Radius+contactMargin,overlaps,collisionMask,QueryTriggerInteraction.Ignore);
            for(int i=0;i<count;i++)
            {
                var shape=overlaps[i];var other=shape.attachedRigidbody;
                if(IsSelf(shape) || !other || other.isKinematic || pushedBodies.Contains(other))continue;
                var oneWay=shape.GetComponentInParent<PootleOneWayPlatform>();if(oneWay && oneWay.isActiveAndEnabled)continue;
                Vector3 point=shape.ClosestPoint(transform.position+center);
                Vector3 normal=transform.position+center-point;
                ColliderPose(shape,out var shapePosition,out var shapeRotation);
                if(Physics.ComputePenetration(capsule,transform.position,Quaternion.identity,shape,shapePosition,shapeRotation,out var outward,out _))normal=outward;
                if(normal.sqrMagnitude<.00001f || Mathf.Abs(normal.normalized.y)>.55f)continue;
                normal=Vector3.ProjectOnPlane(normal,Vector3.up).normalized;
                float incoming=Vector3.Dot(other.GetPointVelocity(point),normal);
                if(incoming<.1f)continue;
                Vector3 axisPoint=a+Vector3.up*Mathf.Clamp(point.y-a.y,0,b.y-a.y);
                if(Vector3.Distance(point,axisPoint)>Radius+contactMargin)continue;
                float change=Mathf.Clamp(incoming-Vector3.Dot(character.Velocity,normal),0,maximumIncomingSpeed);
                if(change<=.01f)continue;
                pushedBodies.Add(other);
                character.ReceivePhysicsPush(normal*change);
                float impulse=Mathf.Min(change*characterMass*incomingReactionScale,maximumPushForce*tickDeltaTime);
                other.AddForceAtPosition(-normal*impulse,point,ForceMode.Impulse);
            }
        }

        public void ApplyStandingForce(PootleSupport support,bool standing)
        {
            if(!enablePhysicsInteraction || !applyStandingDownwardForce || !standing || !support.central
                || !support.collider || support.distance>Skin*3)return;
            var other=support.collider.attachedRigidbody;
            if(!other || other.isKinematic || other.transform.IsChildOf(transform))return;
            // Integrate in Update's dt, not once per render frame with ForceMode.Force.
            other.AddForceAtPosition(Physics.gravity*characterMass*standingDownwardForceScale*tickDeltaTime,
                support.point,ForceMode.Impulse);
        }

        public void Endpoints(Vector3 position, out Vector3 a, out Vector3 b)
        {
            // World-up motor: avatar yaw/tilt belongs to a separate child transform.
            Vector3 c = position + center;
            Vector3 offset = Vector3.up * (Height * 0.5f - Radius);
            a = c - offset;
            b = c + offset;
        }

        private bool IsSelf(Collider other) => other == capsule || other.transform.IsChildOf(transform);

        // Queries use the simulated collider pose. An interpolated body's Transform
        // is one physics step behind it: recovering against that rendered pose leaves
        // us inside the query collider and produces false zero-distance sweep hits.
        private static void ColliderPose(Collider shape,out Vector3 position,out Quaternion rotation)
        {
            var body=shape.attachedRigidbody;
            position=shape.transform.position;rotation=shape.transform.rotation;
            if(!body || body.isKinematic || body.interpolation==RigidbodyInterpolation.None)return;
            var change=body.rotation*Quaternion.Inverse(body.transform.rotation);
            position=body.position+change*(position-body.transform.position);
            rotation=change*rotation;
        }

        private bool Nearest(int count, out RaycastHit hit,Vector3 position,Vector3 direction)
        {
            hit = default;
            float nearest = float.PositiveInfinity;
            if (count == hits.Length && Capture != null && !Capture.frozen) Capture.overflow = true;
            for (int i = 0; i < count; i++)
            {
                if (!hits[i].collider || IsSelf(hits[i].collider) || hits[i].distance >= nearest) continue;
                var oneWay=hits[i].collider.GetComponentInParent<PootleOneWayPlatform>();
                if(oneWay && oneWay.isActiveAndEnabled && !oneWay.Accepts(this,position,direction,hits[i].normal))continue;
                nearest = hits[i].distance;
                hit = hits[i];
            }
            return hit.collider;
        }

        private void Record(PootleProbeKind kind, Vector3 a, Vector3 b, float r, Vector3 direction,
            float distance, RaycastHit hit, bool found, bool isCapsule, PootleProbeStatus? status = null,
            PootleProbeRejection rejection = PootleProbeRejection.None)
        {
            Capture?.Record(new PootleProbeRecord
            {
                kind = kind, rejection = rejection, a = a, b = b, radius = r, direction = direction, distance = distance,
                collider = hit.collider, hitPoint = hit.point, hitNormal = hit.normal,
                mask = collisionMask, capsule = isCapsule,
                status = status ?? (found ? PootleProbeStatus.Hit : PootleProbeStatus.Miss)
            });
        }

        private bool Sweep(Vector3 position, Vector3 direction, float distance, PootleProbeKind kind, out RaycastHit hit)
        {
            Endpoints(position, out var a, out var b);
            QueryCount++;
            // PhysX can classify an exactly touching capsule as an initial overlap and return
            // a synthetic normal opposite ANY sweep direction. Keep the query slightly inside
            // the recovery capsule; Skin still supplies the normal stopping clearance.
            float castRadius = Radius - Skin - Mathf.Max(0.005f, Skin * 0.5f);
            int count = Physics.CapsuleCastNonAlloc(a, b, castRadius, direction, hits, distance,
                collisionMask, QueryTriggerInteraction.Ignore);
            // A resting contact must not consume a tangential or separating sweep.
            for (int i = 0; i < count; i++)
                if (Vector3.Dot(direction, hits[i].normal) >= -0.0001f) hits[i] = default;
            bool found = Nearest(count, out hit,position,direction);
            Record(kind, a, b, castRadius, direction, distance, hit, found, true);
            return found;
        }

        public PootleSupport ProbeGround(Vector3 position, float reach, bool requireCenter)
        {
            Endpoints(position, out var bottom, out _);
            const float lift = 0.04f;
            Vector3 origin = bottom + Vector3.up * lift;
            float distance = reach + Skin + lift;
            QueryCount++;
            int count = Physics.SphereCastNonAlloc(origin, Radius - Skin, Vector3.down, hits,
                distance, collisionMask, QueryTriggerInteraction.Ignore);
            bool found = Nearest(count, out var hit,position,Vector3.down);
            bool sphereFound = found;
            var support = new PootleSupport
            {
                found = found, collider = hit.collider, normal = found ? hit.normal : Vector3.up,
                point = hit.point, distance = found ? hit.distance - lift - Skin : float.PositiveInfinity,
                angle = found ? Vector3.Angle(Vector3.up, hit.normal) : 0
            };
            Vector3 centralOrigin = position + center;
            // Capsule support is above the point directly under its centre on a slope.
            // Validate the capsule/plane separation, rather than an arbitrary short ray.
            float centralDistance = Height * 0.5f + reach + Radius * 6;
            QueryCount++;
            int centralCount = Physics.RaycastNonAlloc(centralOrigin, Vector3.down, hits, centralDistance,
                collisionMask, QueryTriggerInteraction.Ignore);
            bool centralHit = Nearest(centralCount, out var central,position,Vector3.down);
            float upDot = centralHit ? Vector3.Dot(central.normal, Vector3.up) : 0;
            float gap = upDot > 0.1f
                ? (Vector3.Dot(bottom - central.point, central.normal) - (Radius - Skin)) / upDot
                : float.PositiveInfinity;
            support.central = centralHit && gap <= reach + Skin * 2 && gap >= -Skin * 4;
            if (support.central)
            {
                support.found = found = true;
                support.collider = central.collider;
                support.point = central.point;
                support.normal = central.normal;
                support.distance = gap;
                support.angle = Vector3.Angle(Vector3.up, central.normal);
            }
            // A curved trough supports the capsule at its spherical contact, which can be
            // beside the centre ray on a bank. Do not alternate between those two planes.
            var slide= sphereFound && hit.collider ? hit.collider.GetComponentInParent<PootleSlideSurface>() : null;
            if(slide && slide.isActiveAndEnabled && hit.normal.y>.1f)
            {
                support.found=found=true;support.collider=hit.collider;support.point=hit.point;
                support.normal=slide.NormalAt(hit);support.distance=hit.distance-lift-Skin;
                support.angle=Vector3.Angle(Vector3.up,support.normal);
                support.central=support.distance<=reach+Skin*2 && support.distance>=-Skin*6;
            }
            support.walkable = found && support.angle <= slopeLimit + 0.01f && (!requireCenter || support.central);
            Record(PootleProbeKind.GroundSphere, origin, origin, Radius - Skin, Vector3.down, distance,
                hit, sphereFound, false, !sphereFound ? PootleProbeStatus.Miss : support.walkable ? PootleProbeStatus.Accepted : PootleProbeStatus.Rejected,
                !found || support.walkable ? PootleProbeRejection.None : support.angle > slopeLimit ? PootleProbeRejection.SlopeLimit : PootleProbeRejection.NoCentralSupport);
            Record(PootleProbeKind.CentralSupport, centralOrigin, centralOrigin, 0, Vector3.down, centralDistance,
                central, centralHit, false, !centralHit ? PootleProbeStatus.Miss : support.central ? PootleProbeStatus.Accepted : PootleProbeStatus.Rejected,
                centralHit && !support.central ? PootleProbeRejection.OutsideSupportReach : PootleProbeRejection.None);
            return support;
        }

        // Resolve existing overlap before calculating travel to a moving support/anchor.
        public PootleMotionResult MoveTo(Vector3 target)
        {
            Vector3 recovered = Recover(transform.position);
            return Move(target - recovered, false);
        }

        public bool CanMoveTo(Vector3 target)
        {
            Vector3 delta=target-transform.position;float distance=delta.magnitude;
            return distance<.000001f || !Sweep(transform.position,delta/distance,distance+Skin,PootleProbeKind.Clearance,out _);
        }

        public PootleMotionResult Move(Vector3 motion, bool allowSteps, float snapDistance = 0, bool steepSlide = false, float maximumSnap = float.PositiveInfinity)
        {
            Vector3 start = transform.position;
            Vector3 position = Recover(start);
            Vector3 horizontal = new Vector3(motion.x, 0, motion.z);
            bool stepped = false;
            if (allowSteps && horizontal.sqrMagnitude > 0.0000001f)
            {
                float distance = horizontal.magnitude;
                if (Sweep(position, horizontal / distance, distance + Skin, PootleProbeKind.Movement, out var wall)
                    && Vector3.Angle(Vector3.up, wall.normal) > slopeLimit + 0.1f)
                    stepped = TryStep(ref position, horizontal, wall.normal);
            }
            else Capture?.Record(new PootleProbeRecord { kind = PootleProbeKind.StepUp, status = PootleProbeStatus.NotEvaluated });
            // Resolve a surface-tangent velocity once. Splitting XZ and Y adds the slope rise twice.
            if (!stepped) position = Slide(position, motion, steepSlide);
            if (snapDistance > 0 && Sweep(position, Vector3.down, snapDistance + Skin, PootleProbeKind.StepDown, out var floor)
                && Vector3.Angle(floor.normal, Vector3.up) <= slopeLimit)
                position += Vector3.down * Mathf.Min(maximumSnap, Mathf.Max(0, floor.distance - Skin));
            position = Recover(position);
            transform.position = position;
            bool ceiling = false;
            for (int i = 0; i < ContactCount; i++) ceiling |= Contacts[i].ceiling;
            return new PootleMotionResult { requested = motion, displacement = position - start,
                stepped = stepped, hitCeiling = ceiling, contactCount = ContactCount };
        }

        private bool TryStep(ref Vector3 position, Vector3 horizontal, Vector3 normal)
        {
            if (stepHeight <= 0) return false;
            float lift = stepHeight + Skin * 3;
            if (Sweep(position, Vector3.up, lift, PootleProbeKind.StepUp, out _)) return false;
            Vector3 raised = position + Vector3.up * lift;
            float distance = horizontal.magnitude;
            if (Sweep(raised, horizontal / distance, distance + Skin, PootleProbeKind.StepForward, out _)) return false;
            // Sample over the riser in its normal direction: diagonal approach needs the same clearance.
            Vector3 inward = -Vector3.ProjectOnPlane(normal, Vector3.up).normalized;
            Vector3 toe = position + horizontal + inward * (Radius + Skin * 3) + Vector3.up * lift;
            if (!Ray(toe, Vector3.down, lift + Skin * 2, PootleProbeKind.StepDown, out var floor)) return false;
            if (Vector3.Angle(floor.normal, Vector3.up) > slopeLimit) return false;
            float rise = floor.point.y + Skin * 2 - position.y;
            if (rise < -Skin || rise > stepHeight + Skin * 3) return false;
            Vector3 destination = position + horizontal + Vector3.up * rise;
            if (!CanOccupy(destination)) return false;
            position = destination;
            return true;
        }

        private Vector3 Slide(Vector3 position, Vector3 motion, bool steepSlide)
        {
            for (int i = 0; i < 6 && motion.sqrMagnitude > 0.00000001f; i++)
            {
                float distance = motion.magnitude;
                Vector3 direction = motion / distance;
                if (!Sweep(position, direction, distance + Skin, PootleProbeKind.Movement, out var hit))
                    return position + motion;
                float safe = Mathf.Clamp(hit.distance - Skin, 0, distance);
                Vector3 advance = direction * safe;
                position += advance;
                AddContact(hit);
                PushDynamicBody(hit,motion/tickDeltaTime);
                Vector3 remainder = motion - advance;
                Vector3 projected = Vector3.ProjectOnPlane(remainder, hit.normal);
                float angle = Vector3.Angle(Vector3.up, hit.normal);
                // Only explicit slope sliding may convert descent to a surface-tangent slide.
                if (!steepSlide && angle > slopeLimit && hit.normal.y > 0 && projected.y > Mathf.Max(0, remainder.y))
                    projected = Vector3.ProjectOnPlane(remainder, Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized);
                if (!steepSlide && remainder.y < 0 && Mathf.Abs(remainder.x) + Mathf.Abs(remainder.z) < 0.00001f && angle <= slopeLimit)
                    return position;
                motion = projected;
                if (Vector3.Dot(motion, remainder) <= 0) break;
            }
            return position;
        }

        public bool Ray(Vector3 origin, Vector3 direction, float distance, PootleProbeKind kind, out RaycastHit hit)
        {
            QueryCount++;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
            bool found = Nearest(count, out hit,transform.position,direction);
            Record(kind, origin, origin, 0, direction, distance, hit, found, false);
            return found;
        }

        public bool CanOccupy(Vector3 position)
        {
            Endpoints(position, out var a, out var b);
            QueryCount++;
            int count = Physics.OverlapCapsuleNonAlloc(a, b, Radius - Skin * 2, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int i = 0; i < count; i++) if (!IsSelf(overlaps[i]))
            {
                var oneWay=overlaps[i].GetComponentInParent<PootleOneWayPlatform>();
                if(oneWay && oneWay.isActiveAndEnabled)continue;
                blocked = true; break;
            }
            Capture?.Record(new PootleProbeRecord { kind = PootleProbeKind.Clearance, a = a, b = b,
                radius = Radius - Skin * 2, capsule = true, mask = collisionMask,
                status = blocked ? PootleProbeStatus.Rejected : PootleProbeStatus.Accepted });
            return !blocked;
        }

        public int FindGripPoles(Collider[] results, float extraRadius)
        {
            Endpoints(transform.position, out var a, out var b);
            QueryCount++;
            int count = Physics.OverlapCapsuleNonAlloc(a, b, Radius + extraRadius, results, collisionMask, QueryTriggerInteraction.Collide);
            Capture?.Record(new PootleProbeRecord { kind = PootleProbeKind.Pole, a = a, b = b,
                radius = Radius + extraRadius, capsule = true, mask = collisionMask,
                status = count > 0 ? PootleProbeStatus.Hit : PootleProbeStatus.Miss });
            return count;
        }

        private void AddContact(RaycastHit hit)
        {
            if (ContactCount >= Contacts.Length) return;
            float up = Vector3.Dot(hit.normal, Vector3.up);
            Contacts[ContactCount++] = new PootleContact { collider = hit.collider, point = hit.point,
                normal = hit.normal, ceiling = up < -0.5f, wall = up >= -0.5f && Vector3.Angle(Vector3.up, hit.normal) > slopeLimit };
        }

        public Vector3 Recover(Vector3 position)
        {
            if (!capsule) Configure();
            for (int pass = 0; pass < 4; pass++)
            {
                Endpoints(position, out var a, out var b);
                QueryCount++;
                int count = Physics.OverlapCapsuleNonAlloc(a, b, Radius - Skin, overlaps,
                    collisionMask, QueryTriggerInteraction.Ignore);
                Capture?.Record(new PootleProbeRecord { kind = PootleProbeKind.Overlap, a = a, b = b,
                    radius = Radius - Skin, capsule = true, mask = collisionMask,
                    status = count > 0 ? PootleProbeStatus.Hit : PootleProbeStatus.Miss });
                bool corrected = false;
                if (count == overlaps.Length && Capture != null && !Capture.frozen) Capture.overflow = true;
                for (int i = 0; i < count; i++)
                {
                    var other = overlaps[i];
                    if (IsSelf(other)) continue;
                    var oneWay=other.GetComponentInParent<PootleOneWayPlatform>();
                    if(oneWay && oneWay.isActiveAndEnabled && !oneWay.IsAbove(this,position))continue;
                    QueryCount++;
                    // position is the root, NOT the centre: ComputePenetration includes collider.center.
                    ColliderPose(other,out var otherPosition,out var otherRotation);
                    if (!Physics.ComputePenetration(capsule, position, Quaternion.identity, other,
                        otherPosition, otherRotation, out var direction, out float depth)) continue;
                    Vector3 correction = direction * (depth + Skin);
                    Capture?.Record(new PootleProbeRecord { kind = PootleProbeKind.Recovery, a = position,
                        b = position + correction, direction = direction, distance = correction.magnitude,
                        collider = other, mask = collisionMask, status = PootleProbeStatus.Accepted });
                    position += correction;
                    corrected = true;
                }
                if (!corrected) break;
            }
            return position;
        }
    }
}
