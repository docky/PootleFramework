using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100), AddComponentMenu("Pootle/Seated Slide Pose")]
    public sealed class PootleSeatedPose : MonoBehaviour
    {
        public PootleCharacter character;
        public Animator animator;
        private Vector3 rest;
        private float weight;
        private void Awake()
        {
            if (!character) character = GetComponentInParent<PootleCharacter>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (character && character.visualRoot) rest = character.visualRoot.localPosition;
        }
        private void LateUpdate()
        {
            if (!character || !character.profile || !animator || !animator.isHuman || !character.visualRoot) return;
            var visual = character.visualRoot;
            visual.localPosition = rest;
            weight = Mathf.MoveTowards(weight, character.Mode == PootleMode.Sliding || character.Mode == PootleMode.RailSliding || character.SlideRideAirborne ? 1 : 0,
                character.profile.seatedPoseSpeed * Time.deltaTime);
            if (weight <= 0) return;
            Vector3 up = visual.up, forward = visual.forward;
            PoseLeg(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, forward, up);
            PoseLeg(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, forward, up);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (!hips) return;
            character.Motor.Endpoints(character.transform.position, out var bottom, out _);
            Vector3 contact = character.Mode == PootleMode.RailSliding ? character.transform.position
                : bottom - character.SeatedNormal * character.Motor.Radius;
            Vector3 seat = contact + up * 0.22f;
            visual.position += (seat - hips.position) * weight;
        }
        private void PoseLeg(HumanBodyBones thighBone, HumanBodyBones kneeBone, HumanBodyBones footBone, Vector3 forward, Vector3 up)
        {
            var thigh = animator.GetBoneTransform(thighBone);
            var knee = animator.GetBoneTransform(kneeBone);
            var foot = animator.GetBoneTransform(footBone);
            if (!thigh || !knee || !foot) return;
            thigh.rotation = Quaternion.Slerp(thigh.rotation,
                Quaternion.FromToRotation(knee.position - thigh.position, forward - up * 0.12f) * thigh.rotation, weight);
            knee.rotation = Quaternion.Slerp(knee.rotation,
                Quaternion.FromToRotation(foot.position - knee.position, forward - up * 0.3f) * knee.rotation, weight);
        }
        private void OnDisable()
        {
            if (character && character.visualRoot) character.visualRoot.localPosition = rest;
            weight = 0;
        }
    }
}
