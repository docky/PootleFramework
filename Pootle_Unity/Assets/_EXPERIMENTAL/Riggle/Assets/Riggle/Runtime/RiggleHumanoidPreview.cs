using System;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>In-place joint exercise through Mecanim muscles, for checking the generated Humanoid.</summary>
    [RequireComponent(typeof(Riggle), typeof(Animator))]
    public sealed class RiggleHumanoidPreview : MonoBehaviour
    {
        [Min(1)] public float cycleSeconds = 4;
        [Range(0, 1)] public float strength = .65f;
        public bool animate = true;
        HumanPoseHandler handler;
        HumanPose pose;
        float[] restMuscles;
        Transform[] bones;
        Vector3[] positions;
        Quaternion[] rotations;
        Vector3[] scales;
        float elapsed;
        string[] muscles;

        void OnEnable()
        {
            muscles = HumanTrait.MuscleName;
            var rig = GetComponent<Riggle>();
            var animator = GetComponent<Animator>();
            if (!rig.GeneratedBones || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman) return;
            bones = rig.GeneratedBones.GetComponentsInChildren<Transform>();
            positions = new Vector3[bones.Length]; rotations = new Quaternion[bones.Length]; scales = new Vector3[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            { positions[i] = bones[i].localPosition; rotations[i] = bones[i].localRotation; scales[i] = bones[i].localScale; }
            handler = new HumanPoseHandler(animator.avatar, transform);
            handler.GetHumanPose(ref pose);
            restMuscles = (float[])pose.muscles.Clone();
            elapsed = 0;
        }
        void LateUpdate() { if (animate) { elapsed += Time.deltaTime; Sample(elapsed); } }
        public void Sample(float seconds)
        {
            if (handler == null || !float.IsFinite(seconds)) return;
            Array.Copy(restMuscles, pose.muscles, restMuscles.Length);
            float phase = seconds * Mathf.PI * 2 / Mathf.Max(1, cycleSeconds);
            float wave = Mathf.Sin(phase) * strength;
            Offset("Left Upper Leg Front-Back", wave * .45f);
            Offset("Right Upper Leg Front-Back", -wave * .45f);
            Offset("Left Lower Leg Stretch", -Mathf.Max(0, wave) * 1.3f);
            Offset("Right Lower Leg Stretch", -Mathf.Max(0, -wave) * 1.3f);
            Offset("Left Arm Down-Up", -.35f * strength);
            Offset("Right Arm Down-Up", -.35f * strength);
            Offset("Left Arm Front-Back", -wave * .4f);
            Offset("Right Arm Front-Back", wave * .4f);
            Offset("Left Forearm Stretch", -(1 + Mathf.Sin(phase)) * .45f * strength);
            Offset("Right Forearm Stretch", -(1 - Mathf.Sin(phase)) * .45f * strength);
            Offset("Head Turn Left-Right", Mathf.Sin(phase * .5f) * .35f * strength);
            handler.SetHumanPose(ref pose);
        }
        void Offset(string name, float amount)
        {
            int index = Array.IndexOf(muscles, name);
            if (index >= 0) pose.muscles[index] = Mathf.Clamp(restMuscles[index] + amount, -1, 1);
        }
        void OnDisable()
        {
            handler?.Dispose(); handler = null;
            if (bones == null) return;
            for (int i = 0; i < bones.Length; i++)
                if (bones[i]) { bones[i].localPosition = positions[i]; bones[i].localRotation = rotations[i]; bones[i].localScale = scales[i]; }
            bones = null;
        }
    }
}
