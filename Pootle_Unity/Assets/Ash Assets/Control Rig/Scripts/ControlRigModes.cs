using System;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace ControlRig
{
    public sealed class ControlRigModes : MonoBehaviour
    {
        public enum Mode { IK, FK }
        [Serializable] public sealed class Limb
        {
            public string label;
            public TwoBoneIKConstraint ik;
            public Transform[] fk = new Transform[3];
            public MultiRotationConstraint[] rotations = new MultiRotationConstraint[3];
            [HideInInspector] public Mode mode;
        }
        public Limb[] limbs = Array.Empty<Limb>();
        public Transform fkRoot;

        public void SwitchMode(int index, Mode mode)
        {
            var limb = limbs[index];
            if (limb.mode == mode) return;
            var data = limb.ik.data;
            if (mode == Mode.FK)
            {
                var bones = new[] { data.root, data.mid, data.tip };
                for (int i = 0; i < 3; i++) limb.fk[i].SetPositionAndRotation(bones[i].position, bones[i].rotation);
            }
            else
            {
                data.target.SetPositionAndRotation(data.tip.position, data.tip.rotation);
                Vector3 axis = (data.tip.position - data.root.position).normalized;
                Vector3 bend = Vector3.ProjectOnPlane(data.mid.position - data.root.position, axis);
                float length = Vector3.Distance(data.root.position, data.mid.position) + Vector3.Distance(data.mid.position, data.tip.position);
                if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(data.hint.position - data.mid.position, axis);
                if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(data.root.forward, axis);
                data.hint.position = data.mid.position + bend.normalized * Mathf.Max(.1f, length * .6f);
            }
            limb.mode = mode;
            ApplyWeights(limb);
        }
        public void ApplyWeights(Limb limb)
        {
            limb.ik.weight = limb.mode == Mode.IK ? 1 : 0;
            foreach (var rotation in limb.rotations) if (rotation) rotation.weight = 1; // Matched FK pose stays beneath IK, preserving axial roll on a snap.
        }
        void OnEnable() { foreach (var limb in limbs) if (limb.ik) ApplyWeights(limb); }
        void LateUpdate()
        {
            foreach (var limb in limbs)
                if (limb.ik && limb.fk.Length == 3 && limb.fk[0]) limb.fk[0].position = limb.ik.data.root.position;
        }
    }
}
