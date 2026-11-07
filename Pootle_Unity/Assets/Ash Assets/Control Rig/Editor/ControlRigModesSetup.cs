using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;

namespace ControlRig
{
    public static class ControlRigModesSetup
    {
        public static ControlRigModes Build(Animator animator, Rig_References refs)
        {
            var modes = refs.GetComponent<ControlRigModes>() ?? Undo.AddComponent<ControlRigModes>(refs.gameObject);
            Undo.RecordObject(modes, "Set up IK/FK controls");
            var builder = animator.GetComponent<RigBuilder>();
            if (!modes.fkRoot) modes.fkRoot = animator.transform.Find("FK rotation layer");
            if (!modes.fkRoot)
            {
                var go = new GameObject("FK controls"); Undo.RegisterCreatedObjectUndo(go, "Create FK controls");
                modes.fkRoot = go.transform;
            }
            // One ordered graph: hips/spine, matched FK base, then limb IK.
            // A separate FK layer before the body constraints double-applies parent rotations.
            var obsoleteLayer = modes.fkRoot.GetComponent<Rig>();
            if (obsoleteLayer)
            {
                Undo.RecordObject(builder, "Migrate FK layer");
                builder.layers.RemoveAll(l => l.rig == obsoleteLayer);
                Undo.DestroyObjectImmediate(obsoleteLayer);
            }
            Undo.SetTransformParent(modes.fkRoot, refs.transform, "Order FK controls");
            modes.fkRoot.name = "FK controls";
            modes.fkRoot.SetSiblingIndex(refs.spineRig.transform.parent.GetSiblingIndex() + 1);
            var iks = new[] { refs.LeftHandRig, refs.RightHandRig, refs.LeftLegRig, refs.RightLegRig };
            string[] names = { "Left arm", "Right arm", "Left leg", "Right leg" };
            var old = modes.limbs;
            modes.limbs = new ControlRigModes.Limb[4];
            for (int i = 0; i < 4; i++)
            {
                var limb = old.Length > i && old[i] != null ? old[i] : new ControlRigModes.Limb();
                limb.label = names[i]; limb.ik = iks[i]; limb.mode = ControlRigModes.Mode.IK;
                var bones = new[] { iks[i].data.root, iks[i].data.mid, iks[i].data.tip };
                Transform parent = modes.fkRoot;
                for (int b = 0; b < 3; b++)
                {
                    if (!limb.fk[b])
                    {
                        var control = new GameObject(names[i] + " FK " + new[] { "root", "bend", "tip" }[b]);
                        Undo.RegisterCreatedObjectUndo(control, "Create FK control"); control.transform.SetParent(parent, false); limb.fk[b] = control.transform;
                    }
                    Undo.RecordObject(limb.fk[b], "Align FK control");
                    limb.fk[b].SetPositionAndRotation(bones[b].position, bones[b].rotation); parent = limb.fk[b];
                    if (!limb.rotations[b])
                    {
                        var constraint = new GameObject(names[i] + " FK constraint " + b); Undo.RegisterCreatedObjectUndo(constraint, "Create FK constraint");
                        constraint.transform.SetParent(modes.fkRoot, false); limb.rotations[b] = Undo.AddComponent<MultiRotationConstraint>(constraint);
                    }
                    Undo.RecordObject(limb.rotations[b], "Bind FK rotation");
                    var data = limb.rotations[b].data; data.constrainedObject = bones[b];
                    data.sourceObjects = new WeightedTransformArray { new WeightedTransform(limb.fk[b], 1) };
                    data.maintainOffset = false; limb.rotations[b].data = data;
                }
                modes.ApplyWeights(limb); modes.limbs[i] = limb;
            }
            return modes;
        }
        public static void Switch(ControlRigModes modes, int index, ControlRigModes.Mode next)
        {
            Undo.RecordObjects(modes.GetComponentInParent<Animator>().GetComponentsInChildren<Component>(true).Cast<Object>().ToArray(), "Switch IK/FK preserving pose");
            modes.SwitchMode(index, next);
            foreach (var item in modes.GetComponentInParent<Animator>().GetComponentsInChildren<Component>(true)) { EditorUtility.SetDirty(item); PrefabUtility.RecordPrefabInstancePropertyModifications(item); }
            SceneView.RepaintAll();
        }
    }
}
