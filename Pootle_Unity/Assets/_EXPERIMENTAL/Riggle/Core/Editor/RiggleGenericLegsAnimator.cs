using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    /// <summary>Optional editor-only adapter. Core does not require or redistribute Legs Animator.</summary>
    internal static class RiggleGenericLegsAnimator
    {
        const string TypeName = "FIMSpace.FProceduralAnimation.LegsAnimator";
        internal static Type AnimatorType => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(TypeName, false)).FirstOrDefault(t => t != null);
        static Type RequiredType(RiggleGeneric rig) => rig.legs.Any(l=>l.additionalJoints!=null&&l.additionalJoints.Count>0)
            ? AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Docky.RiggleTools.RiggleMultiSegmentLegsAnimator",false)).FirstOrDefault(t=>t!=null) : AnimatorType;

        public static void Validate(RiggleGeneric rig)
        {
            RiggleGenericBuilder.Validate(rig, false);
            if (AnimatorType == null) throw new InvalidOperationException("Install Legs Animator to use automatic setup. Generic baking works without it.");
            var scale = rig.transform.lossyScale;
            if (scale.x <= 0 || Mathf.Abs(scale.x - scale.y) > .0001f || Mathf.Abs(scale.x - scale.z) > .0001f)
                throw new InvalidOperationException("Legs Animator setup requires a positive uniform character scale.");
            var map = rig.joints.ToDictionary(j => j.name);
            if (!map.ContainsKey(rig.hips ?? "")) throw new InvalidOperationException("Choose a Hips joint before setting up Legs Animator.");
            if (rig.legs == null || rig.legs.Count == 0) throw new InvalidOperationException("Define at least one leg chain.");
            if(RequiredType(rig)==null)throw new InvalidOperationException("Install Riggle's optional multi-segment Legs Animator integration for longer chains.");
            var names = new System.Collections.Generic.HashSet<string>();
            var used = new System.Collections.Generic.HashSet<string>();
            foreach (var leg in rig.legs)
            {
                if (leg == null || string.IsNullOrWhiteSpace(leg.name) || !names.Add(leg.name))
                    throw new InvalidOperationException("Leg names must be nonempty and unique.");
                var chain=leg.Chain();
                for(int c=0;c<chain.Count;c++)
                {
                    if(!map.ContainsKey(chain[c]??"")||(c>0&&map[chain[c]].parent!=chain[c-1]))throw new InvalidOperationException(leg.name+": chain must contain every joint in parent order.");
                    if(!used.Add(chain[c]))throw new InvalidOperationException("Leg chains cannot share or repeat joints.");
                }
                var ancestor = map[leg.upper];
                while (!string.IsNullOrEmpty(ancestor.parent) && ancestor.parent != rig.hips) ancestor = map[ancestor.parent];
                if (ancestor.parent != rig.hips) throw new InvalidOperationException(leg.name + ": Hips must be an ancestor of the leg.");
                Vector3 a = map[leg.middle].position - map[leg.upper].position, b = map[leg.foot].position - map[leg.middle].position;
                if (a.sqrMagnitude < 1e-10f || b.sqrMagnitude < 1e-10f || Vector3.Cross(a.normalized, b.normalized).sqrMagnitude < .0001f)
                    throw new InvalidOperationException(leg.name + ": add a slight knee bend to define the IK bend direction.");
                if (!string.IsNullOrEmpty(leg.opposite))
                {
                    var other = rig.legs.Find(l => l != null && l.name == leg.opposite);
                    if (other == null || other == leg || other.opposite != leg.name ||
                        leg.side == RiggleGeneric.Side.Undefined || other.side == RiggleGeneric.Side.Undefined || other.side == leg.side)
                        throw new InvalidOperationException(leg.name + ": opposite legs must reference each other and use opposite sides.");
                }
            }
            var existing = rig.GetComponent(AnimatorType);
            if (existing && existing != rig.legsAnimator)
                throw new InvalidOperationException("This root already has a manually configured Legs Animator. Keep its setup and assign the generated bones there, or remove it before automatic setup.");
        }

        public static void Configure(RiggleGeneric rig)
        {
            Validate(rig);
            if (!rig.generatedBones) throw new InvalidOperationException("Bake the generic rig first.");
            var bones = rig.generatedBones.GetComponentsInChildren<Transform>(true).Skip(1).ToDictionary(t => t.name);
            if (rig.joints.Any(j => !bones.ContainsKey(j.name))) throw new InvalidOperationException("The skeleton definition changed. Rebake before setting up Legs Animator.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up Legs Animator");
            try
            {
                Undo.RecordObject(rig, "Track generated Legs Animator");
                var type=RequiredType(rig);
                if(rig.legsAnimator&&rig.legsAnimator.GetType()!=type)
                {
                    string settings=JsonUtility.ToJson(rig.legsAnimator);
                    Undo.DestroyObjectImmediate(rig.legsAnimator);
                    rig.legsAnimator=Undo.AddComponent(rig.gameObject,type);
                    JsonUtility.FromJsonOverwrite(settings,rig.legsAnimator);
                }
                if (!rig.legsAnimator) rig.legsAnimator = Undo.AddComponent(rig.gameObject, type);
                var component = rig.legsAnimator;
                Undo.RegisterCompleteObjectUndo(component, "Configure leg chains");
                var so = new SerializedObject(component);
                Property(so, "baseTransform").objectReferenceValue = rig.transform;
                Property(so, "Hips").objectReferenceValue = bones[rig.hips];
                so.ApplyModifiedProperties();
                // Construct new legs through the vendor API so all per-leg defaults are retained.
                var field = AnimatorType.GetField("Legs");
                var add = AnimatorType.GetMethod("Legs_AddNewLeg");
                if (field == null || add == null) throw new InvalidOperationException("Unsupported Legs Animator API: missing Legs/Legs_AddNewLeg.");
                var list = (IList)field.GetValue(component);
                while (list.Count > rig.legs.Count) list.RemoveAt(list.Count - 1);
                while (list.Count < rig.legs.Count) add.Invoke(component, null);
                so.Update();
                var legs = Property(so, "Legs");
                for (int i = 0; i < rig.legs.Count; i++)
                {
                    var definition = rig.legs[i]; var leg = legs.GetArrayElementAtIndex(i);
                    leg.FindPropertyRelative("Owner").objectReferenceValue = component;
                    leg.FindPropertyRelative("BoneStart").objectReferenceValue = bones[definition.upper];
                    leg.FindPropertyRelative("BoneMid").objectReferenceValue = bones[definition.middle];
                    leg.FindPropertyRelative("BoneEnd").objectReferenceValue = bones[definition.foot];
                    leg.FindPropertyRelative("Side").enumValueIndex = (int)definition.side;
                    leg.FindPropertyRelative("OppositeLegIndex").intValue = rig.legs.FindIndex(l => l.name == definition.opposite);
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(component); EditorUtility.SetDirty(rig);
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                PrefabUtility.RecordPrefabInstancePropertyModifications(rig);
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            finally { Undo.CollapseUndoOperations(group); }
        }

        static SerializedProperty Property(SerializedObject so, string name) => so.FindProperty(name)
            ?? throw new InvalidOperationException("Unsupported Legs Animator API: missing " + name);
    }
}
