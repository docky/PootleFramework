using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Docky.RiggleTools.Editor
{
    [CustomEditor(typeof(RiggleGeneric))]
    public sealed class RiggleGenericEditor : UnityEditor.Editor
    {
        int selected;
        HelpBox status;
        RiggleGeneric Rig => (RiggleGeneric)target;

        [MenuItem("Tools/Riggle/Add Generic Rig to Selected")]
        static void AddToSelected()
        {
            var go = Selection.activeGameObject;
            if (!go || EditorUtility.IsPersistent(go)) return;
            if (go.GetComponent<Riggle>()) { Debug.LogWarning("Use a separate character root for Riggle Generic.", go); return; }
            if (!go.GetComponent<RiggleGeneric>()) Undo.AddComponent<RiggleGeneric>(go);
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox("Generic skeletons and quadrupeds • +Y up, +Z forward. Fit the joints in Scene view, bake, then set up Legs Animator. No Humanoid Avatar or animation controller is required.", HelpBoxMessageType.Info));
            var authoring = new VisualElement(); root.Add(authoring);
            authoring.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode);
            AddButton(authoring, "Reset to quadruped preset fitted to mesh bounds", () =>
            {
                if (!Rig.TryGetBounds(out var bounds)) throw new InvalidOperationException("No source meshes to fit.");
                Undo.RecordObject(Rig, "Fit quadruped preset"); Rig.SetQuadrupedPreset(bounds);
                EditorUtility.SetDirty(Rig); selected = 0;
            });
            AddButton(authoring, "Mirror Left joints to Right", () =>
            {
                Undo.RecordObject(Rig, "Mirror generic joints");
                var hips = Rig.joints.Find(j => j.name == Rig.hips);
                float center = hips != null ? hips.position.x : 0;
                foreach (var left in Rig.joints)
                {
                    if (!left.name.Contains("Left")) continue;
                    var right = Rig.joints.Find(j => j.name == left.name.Replace("Left", "Right"));
                    if (right == null) continue;
                    right.position = left.position; right.position.x = 2 * center - right.position.x;
                    right.tip = left.tip; right.tip.x = 2 * center - right.tip.x; right.radius = left.radius;
                }
                EditorUtility.SetDirty(Rig);
            });
            authoring.Add(new PropertyField(serializedObject.FindProperty("legSegments")));
            AddButton(authoring,"Rebuild leg segments (keeps upper and foot positions)",()=>{Undo.RecordObject(Rig,"Rebuild leg segments");Rig.RebuildLegSegments();EditorUtility.SetDirty(Rig);});
            foreach (var name in new[] { "joints", "hips", "legs", "weighting", "falloff", "jointSoftness", "smoothSteps", "smoothAmount", "sourceMeshes", "assetFolder" })
                authoring.Add(new PropertyField(serializedObject.FindProperty(name)));
            authoring.Add(new HelpBox("Legs use Upper → Middle → Additional Joints → Foot. Choose 2–8 segments, rebuild, fit every joint, then rebake. Longer chains use the optional Riggle multi-segment Legs Animator integration. Rebuilding changes bone names/references; reconnect animation profiles afterwards.", HelpBoxMessageType.Info));
            AddButton(authoring, "Validate skeleton", () => { RiggleGenericBuilder.Validate(Rig); });
            AddButton(authoring, "Bake Generic • Bones + Skinning", () => RiggleGenericBuilder.Bake(Rig));
            AddButton(authoring, "Set up / reconnect Legs Animator", () => RiggleGenericLegsAnimator.Configure(Rig));
            AddButton(authoring, "Save new prefab", () =>
            {
                var path = RiggleGenericBuilder.Export(Rig);
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            });
            AddButton(authoring, "Clear generated rig and restore source meshes", () => RiggleGenericBuilder.Clear(Rig));
            status = new HelpBox("Click a joint dot in Scene view to move it. After editing the definition, rebake to update the generated skeleton.", HelpBoxMessageType.Info);
            root.Add(status);
            return root;
        }

        void AddButton(VisualElement root, string label, Action action)
        {
            root.Add(new Button(() =>
            {
                try
                {
                    serializedObject.ApplyModifiedProperties(); action(); serializedObject.Update();
                    status.text = label + " — done."; status.messageType = HelpBoxMessageType.Info;
                    SceneView.RepaintAll();
                }
                catch (Exception e) { status.text = e.Message; status.messageType = HelpBoxMessageType.Error; }
            }) { text = label });
        }

        void OnSceneGUI()
        {
            if (!Rig || Rig.joints == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
            for (int i = 0; i < Rig.joints.Count; i++)
            {
                var joint = Rig.joints[i]; if (joint == null) continue;
                var p = Rig.transform.TransformPoint(joint.position);
                Handles.color = i == selected ? Color.yellow : new Color(.2f,.85f,.9f);
                var parent = Rig.joints.Find(j => j != null && j.name == joint.parent);
                if (parent != null) Handles.DrawLine(Rig.transform.TransformPoint(parent.position), p);
                float size = HandleUtility.GetHandleSize(p) * .045f;
                if (Handles.Button(p, Quaternion.identity, size, size, Handles.SphereHandleCap)) { selected = i; Repaint(); }
                if (i != selected) continue;
                Handles.Label(p + Rig.transform.up * size * 2, joint.name);
                EditorGUI.BeginChangeCheck();
                var moved = Handles.PositionHandle(p, Rig.transform.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(Rig, "Move generic joint");
                    var next = Rig.transform.InverseTransformPoint(moved);
                    joint.tip += next - joint.position; joint.position = next;
                    EditorUtility.SetDirty(Rig);
                }
                if (string.IsNullOrEmpty(joint.endJoint))
                {
                    var tip = Rig.transform.TransformPoint(joint.tip); Handles.DrawDottedLine(p, tip, 4);
                    EditorGUI.BeginChangeCheck(); var next = Handles.PositionHandle(tip, Rig.transform.rotation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(Rig, "Move generic bone tip"); joint.tip = Rig.transform.InverseTransformPoint(next);
                        EditorUtility.SetDirty(Rig);
                    }
                }
            }
        }
    }
}
