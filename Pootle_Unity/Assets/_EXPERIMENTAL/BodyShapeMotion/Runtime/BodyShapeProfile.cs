using System;
using UnityEngine;
namespace Docky.BodyShapeMotion
{
    [CreateAssetMenu(menuName="Animation/Body Shape Profile")]
    public sealed class BodyShapeProfile : ScriptableObject
    {
        [Serializable] public struct Volume
        {
            public string label;
            public HumanBodyBones bone;
            public Vector3 center;
            public Vector3 radii;
        }
        [Serializable] public struct FootShape { public Vector3 center, halfExtents; }
        [Tooltip("Ellipsoids in the named bone's local space. Fit in the source rest pose.")]
        public Volume[] torso = Array.Empty<Volume>();
        public Vector3 leftHandCenter, rightHandCenter;
        [Min(.001f)] public float handRadius=.08f, forearmRadius=.045f;
        public FootShape leftFoot, rightFoot;
        [Min(0)] public float clearance=.015f;
        [Min(.001f)] public float maximumOffset=.3f;
        [Min(0)] public float smoothingSeconds=.065f;
        [Header("Knee direction")]
        [Tooltip("Carry the animated knee bend plane with the corrected hip-to-foot direction. Zero keeps the old fixed knee hint.")]
        [Range(0,1)] public float kneeFollow=1;
        [Tooltip("Preferred outward knee angle when separating overlapping shoes. Fades with lateral correction and strength; does not turn the feet.")]
        [Range(0,60)] public float kneeOutwardDegrees=25;
    }
}
