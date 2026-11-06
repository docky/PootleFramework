using System.Collections.Generic;
using UnityEngine;

namespace Pootle
{
    // Owns visible passengers without deactivating their avatar or Animator.
    [DefaultExecutionOrder(10100), AddComponentMenu("Pootle/Machine Passenger")]
    public sealed class PootleMachinePassenger : MonoBehaviour
    {
        public Transform seat;
        public PootleCharacter Rider { get; private set; }
        public bool Occupied => Rider;
        public float SeatWeight { get; private set; }
        readonly List<MonoBehaviour> suspended = new List<MonoBehaviour>();
        readonly List<Collider> colliders = new List<Collider>();
        Animator animator;
        Vector3 entry, visualPosition;
        Quaternion entryRotation, visualRotation;
        public bool Begin(PootleCharacter character)
        {
            if (Occupied || !character || !seat) return false;
            Rider = character; entry = character.transform.position; entryRotation = Quaternion.LookRotation(character.Facing);
            animator = character.GetComponentInChildren<Animator>();
            foreach (var behaviour in character.GetComponentsInChildren<MonoBehaviour>())
                if (behaviour.enabled) { suspended.Add(behaviour); behaviour.enabled = false; }
            foreach (var collider in character.GetComponentsInChildren<Collider>())
                if (collider.enabled) { colliders.Add(collider); collider.enabled = false; }
            if (character.visualRoot) { visualPosition = character.visualRoot.localPosition; visualRotation = character.visualRoot.localRotation; }
            SeatWeight = 0;
            if (animator && animator.runtimeAnimatorController && animator.HasState(0, Animator.StringToHash("Locomotion"))) animator.CrossFade("Locomotion", .1f);
            return true;
        }
        public void Place(Vector3 feet, Quaternion rotation, float seated)
        {
            if (!Rider) return;
            SeatWeight = Mathf.Clamp01(seated);
            Rider.transform.SetPositionAndRotation(feet, rotation);
            if (animator && animator.runtimeAnimatorController)
            {
                foreach (var p in animator.parameters)
                {
                    if (p.name == "Speed") animator.SetFloat(p.nameHash, SeatWeight < .8f ? 1.5f : 0);
                    if (p.name == "Grounded") animator.SetBool(p.nameHash, true);
                }
            }
        }
        public bool CanRelease(Vector3 feet) => Rider && Rider.Motor.CanOccupy(feet);
        public void Release(Vector3 feet, Quaternion rotation)
        {
            var rider = Rider; if (!rider) { Clear(); return; }
            if (rider.visualRoot) { rider.visualRoot.localPosition = visualPosition; rider.visualRoot.localRotation = visualRotation; }
            rider.Teleport(feet, rotation);
            foreach (var c in colliders) if (c) c.enabled = true;
            foreach (var b in suspended) if (b) b.enabled = true;
            Rider = null; SeatWeight = 0; Clear(); Physics.SyncTransforms();
        }
        void Clear() { suspended.Clear(); colliders.Clear(); animator = null; }
        public void Abort() { if (Rider) Release(entry, entryRotation); else Clear(); }
        void OnDisable() => Abort();
        void LateUpdate()
        {
            if (!Rider || !Rider.visualRoot) return;
            var visual = Rider.visualRoot; visual.localPosition = visualPosition; visual.localRotation = Quaternion.identity;
            if (!animator || !animator.isHuman || SeatWeight <= 0) return;
            Vector3 f = seat.forward, up = seat.up;
            Leg(HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, f, up);
            Leg(HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, f, up);
            Aim(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, f * .5f - up);
            Aim(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, f * .5f - up);
            Aim(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, f);
            Aim(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, f);
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (hips) visual.position += (seat.position - hips.position) * SeatWeight;
        }
        void Leg(HumanBodyBones thigh, HumanBodyBones knee, HumanBodyBones foot, Vector3 forward, Vector3 up)
        { Aim(thigh, knee, forward - up * .15f); Aim(knee, foot, -up + forward * .1f); }
        void Aim(HumanBodyBones parent, HumanBodyBones child, Vector3 direction)
        {
            var a = animator.GetBoneTransform(parent); var b = animator.GetBoneTransform(child);
            if (a && b) a.rotation = Quaternion.Slerp(a.rotation, Quaternion.FromToRotation(b.position - a.position, direction) * a.rotation, SeatWeight);
        }
    }
}
