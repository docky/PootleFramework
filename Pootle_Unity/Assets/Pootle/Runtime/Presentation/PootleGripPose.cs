using UnityEngine;
namespace Pootle
{
    [DisallowMultipleComponent, DefaultExecutionOrder(110)]
    public sealed class PootleGripPose : MonoBehaviour
    {
        public PootleCharacter character;
        public Animator animator;
        private float weight;
        private void Awake()
        {
            if (!character) character = GetComponentInParent<PootleCharacter>();
            if (!animator) animator = GetComponentInChildren<Animator>();
        }
        private void LateUpdate()
        {
            if (!character || !animator || !animator.isActiveAndEnabled || !animator.isHuman) return;
            var pole = character.Mode == PootleMode.PoleClimbing ? character.GetComponent<PootleTraversal>().AttachedPole : null;
            var hook = character.Mode == PootleMode.Dangling ? character.GetComponent<PootleHookRider>().Hook : null;
            var item=character.GetComponent<PootleCarrying>()?.Held;
            if (!pole && !hook && !item) { weight = 0; return; }
            weight = Mathf.MoveTowards(weight, 1, Time.deltaTime * 10);
            Arm(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, -1, pole, hook, item);
            Arm(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 1, pole, hook, item);
        }
        private void Arm(HumanBodyBones upperId, HumanBodyBones lowerId, HumanBodyBones handId, float side, PootlePole pole, PootleDangleHook hook, PootleCarryable item)
        {
            Transform upper = animator.GetBoneTransform(upperId), lower = animator.GetBoneTransform(lowerId), hand = animator.GetBoneTransform(handId);
            if (!upper || !lower || !hand) return;
            Vector3 right = Vector3.Cross(Vector3.up, character.Facing);
            Vector3 target;
            if(item)target=item.Shape.bounds.center+right*side*item.HalfSize.x-character.Facing*item.HalfSize.z*.65f;
            else if (hook) target = hook.GripPoint + right * side * 0.07f;
            else
            {
                float h = Mathf.Clamp(Vector3.Dot(hand.position - pole.Bottom, pole.transform.up), 0, pole.length);
                Vector3 axis = pole.Bottom + pole.transform.up * h;
                Vector3 outward = Vector3.ProjectOnPlane(character.transform.position - axis, pole.transform.up).normalized;
                target = axis + outward * (pole.radius + 0.025f) + right * side * pole.radius * 0.45f;
            }
            target = Vector3.Lerp(hand.position, target, weight);
            Vector3 shoulder = upper.position;
            float a = Vector3.Distance(shoulder, lower.position), b = Vector3.Distance(lower.position, hand.position);
            Vector3 direction = (target - shoulder).normalized;
            float distance = Mathf.Clamp(Vector3.Distance(target, shoulder), Mathf.Abs(a-b)+0.001f, a+b-0.001f);
            if (distance < 0.001f || direction.sqrMagnitude < 0.1f) return;
            float along = (a*a - b*b + distance*distance)/(2*distance);
            Vector3 bend = Vector3.ProjectOnPlane(lower.position - shoulder, direction).normalized;
            if (bend.sqrMagnitude < 0.001f) bend = Vector3.ProjectOnPlane(right * side, direction).normalized;
            Vector3 elbow = shoulder + direction * along + bend * Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation = Quaternion.FromToRotation(lower.position-shoulder, elbow-shoulder) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position-lower.position, target-lower.position) * lower.rotation;
        }
    }
}
