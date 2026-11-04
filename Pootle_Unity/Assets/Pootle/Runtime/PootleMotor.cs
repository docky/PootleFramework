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
        public readonly PootleContact[] Contacts = new PootleContact[24];
        public int ContactCount { get; private set; }
        public int QueryCount { get; private set; }
        public PootleDebugSnapshot Capture { get; set; }
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly Collider[] overlaps = new Collider[32];
        private CapsuleCollider capsule;

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

        public void BeginTick() { ContactCount = 0; QueryCount = 0; }

        public void Endpoints(Vector3 position, out Vector3 a, out Vector3 b)
        {
            // World-up motor: avatar yaw/tilt belongs to a separate child transform.
            Vector3 c = position + center;
            Vector3 offset = Vector3.up * (Height * 0.5f - Radius);
            a = c - offset;
            b = c + offset;
        }

        private bool IsSelf(Collider other) => other == capsule || other.transform.IsChildOf(transform);

        private bool Nearest(int count, out RaycastHit hit)
        {
            hit = default;
            float nearest = float.PositiveInfinity;
            if (count == hits.Length && Capture != null && !Capture.frozen) Capture.overflow = true;
            for (int i = 0; i < count; i++)
            {
                if (!hits[i].collider || IsSelf(hits[i].collider) || hits[i].distance >= nearest) continue;
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
            bool found = Nearest(count, out hit);
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
            bool found = Nearest(count, out var hit);
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
            bool centralHit = Nearest(centralCount, out var central);
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
            support.walkable = found && support.angle <= slopeLimit + 0.01f && (!requireCenter || support.central);
            Record(PootleProbeKind.GroundSphere, origin, origin, Radius - Skin, Vector3.down, distance,
                hit, sphereFound, false, !sphereFound ? PootleProbeStatus.Miss : support.walkable ? PootleProbeStatus.Accepted : PootleProbeStatus.Rejected,
                !found || support.walkable ? PootleProbeRejection.None : support.angle > slopeLimit ? PootleProbeRejection.SlopeLimit : PootleProbeRejection.NoCentralSupport);
            Record(PootleProbeKind.CentralSupport, centralOrigin, centralOrigin, 0, Vector3.down, centralDistance,
                central, centralHit, false, !centralHit ? PootleProbeStatus.Miss : support.central ? PootleProbeStatus.Accepted : PootleProbeStatus.Rejected,
                centralHit && !support.central ? PootleProbeRejection.OutsideSupportReach : PootleProbeRejection.None);
            return support;
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
                    && Vector3.Angle(Vector3.up, wall.normal) > 0.1f)
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
            bool found = Nearest(count, out hit);
            Record(kind, origin, origin, 0, direction, distance, hit, found, false);
            return found;
        }

        public bool CanOccupy(Vector3 position)
        {
            Endpoints(position, out var a, out var b);
            QueryCount++;
            int count = Physics.OverlapCapsuleNonAlloc(a, b, Radius - Skin * 2, overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            for (int i = 0; i < count; i++) if (!IsSelf(overlaps[i])) { blocked = true; break; }
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
                    QueryCount++;
                    // position is the root, NOT the centre: ComputePenetration includes collider.center.
                    if (!Physics.ComputePenetration(capsule, position, Quaternion.identity, other,
                        other.transform.position, other.transform.rotation, out var direction, out float depth)) continue;
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
