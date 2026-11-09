using System;
using System.Collections.Generic;
using UnityEngine;

namespace Docky.RiggleTools
{
    [CreateAssetMenu(menuName = "Riggle/Quadruped Animation Profile")]
    public sealed class RiggleQuadrupedRetargetProfile : ScriptableObject
    {
        public GameObject sourceModel;
        public GameObject targetPrefab;
        [Tooltip("Path relative to the source model. This transform carries locomotion.")]
        public string sourceMotionRoot;
        public string sourceHips;
        public string targetHips = "Hips";
        [Tooltip("Optional neutral reference clip. Use this when imported bind axes differ from the animation pack's authored neutral pose.")]
        public AnimationClip sourceReferencePose;
        [Min(0)] public float referenceTime;
        [Tooltip("Rotation aligning source character coordinates to target +Y up / +Z forward.")]
        public Vector3 sourceToTargetEuler;
        [Tooltip("Keep horizontal translation and turning. Vertical root motion is always retained.")]
        public bool includeRootMotion;
        [Range(15, 120)] public int sampleRate = 60;
        [Range(.1f, 2)] public float strideScale = 1;
        [Tooltip("For clips authored on flat ground, keep each sole above its reference floor height.")]
        public bool clampFeetToReferenceFloor;
        [Range(0,.3f),Tooltip("Maximum downward hip correction to keep the mapped hoof trajectories reachable.")]
        public float maxBodyLowering;
        public string outputFolder = "Assets/RiggleGenerated/Quadruped Animations";
        public List<BodyBone> body = new List<BodyBone>();
        public List<Leg> legs = new List<Leg>();
        public AnimationClip[] clips = Array.Empty<AnimationClip>();

        [Serializable] public sealed class BodyBone
        {
            public string sourcePath;
            public string targetJoint;
            [Range(0, 1)] public float rotationWeight = 1;
        }
        [Serializable] public sealed class Leg
        {
            public string name;
            public string sourceUpper, sourceFoot;
            public string targetUpper, targetMiddle, targetFoot;
            public string[] additionalTargetJoints = Array.Empty<string>();
            [Tooltip("Optional full source chain with the same joint count as the target, for transferring hock/ankle shape as well as hoof motion.")]
            public string[] sourceShapeChain = Array.Empty<string>();
            [Tooltip("Local offset from source hoof pivot to its contact point.")]
            public Vector3 sourceSoleOffset;
            [Tooltip("Local offset from target foot pivot to its contact point.")]
            public Vector3 targetSoleOffset;
            [Tooltip("Target character-space knee direction; use the authored bend when zero.")]
            public Vector3 bendDirection;
        }
    }
}
