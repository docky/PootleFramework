using UnityEditor;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    [CustomEditor(typeof(Riggle))]
    public sealed class RiggleEditor : UnityEditor.Editor
    {
        private Riggle.Point selected = Riggle.Point.LeftElbow;
        private bool placing;
        private bool showAllDotsInInspector;

        public override void OnInspectorGUI()
        {
            var rig = (Riggle)target;
            serializedObject.Update();
            EditorGUILayout.HelpBox(
                "RIGGLE • Biped first pass\nCharacter faces +Z, up is +Y, left is -X. " +
                "Fit dots, refine them in the Scene view, then Bake. The source meshes stay untouched.",
                MessageType.Info);

            bool newMirror = EditorGUILayout.ToggleLeft("Mirror right-side landmarks", rig.MirrorRight);
            if (newMirror != rig.MirrorRight)
            {
                Undo.RecordObject(rig, "Riggle mirror landmarks");
                if (!newMirror) rig.CaptureMirroredRightSide();
                rig.MirrorRight = newMirror;
                EditorUtility.SetDirty(rig);
                SceneView.RepaintAll();
            }
            DrawPropertiesExcluding(serializedObject, "m_Script", "mirrorRight", "landmarks",
                "generatedBones", "bakedSources", "bakedAvatar");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Landmark editor", EditorStyles.boldLabel);
            selected = (Riggle.Point)EditorGUILayout.EnumPopup("Selected dot", selected);
            EditorGUI.BeginChangeCheck();
            Vector3 value = EditorGUILayout.Vector3Field("Local position", rig.GetLocal(selected));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rig, "Move Riggle dot");
                rig.SetLocal(selected, value);
                EditorUtility.SetDirty(rig);
                SceneView.RepaintAll();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(placing ? "Cancel placing" : "Place dot by clicking Scene"))
                {
                    placing = !placing;
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Fit from mesh bounds"))
                {
                    Undo.RecordObject(rig, "Fit Riggle dots to mesh");
                    if (!rig.FitPointsToMeshBounds())
                        Debug.LogWarning("Riggle: no MeshFilter+MeshRenderer sources to fit to.", rig);
                    EditorUtility.SetDirty(rig);
                    SceneView.RepaintAll();
                }
            }

            showAllDotsInInspector = EditorGUILayout.Foldout(showAllDotsInInspector, "All dot positions (local)", true);
            if (showAllDotsInInspector)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < Riggle.PointCount; i++)
                {
                    var point = (Riggle.Point)i;
                    if (rig.MirrorRight && Riggle.IsRightPoint(point)) continue;
                    EditorGUI.BeginChangeCheck();
                    Vector3 edited = EditorGUILayout.Vector3Field(point.ToString(), rig.GetLocal(point));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(rig, "Edit Riggle landmark");
                        rig.SetLocal(point, edited);
                        EditorUtility.SetDirty(rig);
                        SceneView.RepaintAll();
                    }
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(9);
            if (GUILayout.Button("BAKE RIGGLE  •  Bones + Skinning", GUILayout.Height(32)))
            {
                placing = false;
                RiggleBuilder.Bake(rig);
                SceneView.RepaintAll();
            }
            if (rig.GeneratedBones)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Select generated skeleton"))
                        Selection.activeGameObject = rig.GeneratedBones.gameObject;
                    if (GUILayout.Button("Restore original meshes / Clear"))
                        RiggleBuilder.Clear(rig);
                }
                if (GUILayout.Button("Validate baked mesh weights and bind poses"))
                    RiggleDiagnostics.Validate(rig);
                EditorGUILayout.HelpBox(
                    "Rebake replaces Riggle-generated scene objects. Previously saved .asset meshes " +
                    "are deliberately retained so prefab or animation references cannot be deleted unexpectedly.",
                    MessageType.None);
            }
            EditorGUILayout.Space(3);
            EditorGUILayout.HelpBox(
                "Weights are segment-based with triangle-neighbour smoothing, NOT volumetric bone heat. " +
                "Inspect armpits, hips, intersecting limbs, and very large heads after baking. " +
                "Generic animation works without a valid Humanoid Avatar.",
                MessageType.None);
        }

        private void OnSceneGUI()
        {
            var rig = (Riggle)target;
            if (!rig) return;
            rig.EnsurePoints();
            DrawReferenceSkeleton(rig);

            Event evt = Event.current;
            if (placing)
            {
                DrawPlaceOverlay();
                int id = GUIUtility.GetControlID(FocusType.Passive);
                HandleUtility.AddDefaultControl(id);
                if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
                {
                    placing = false;
                    evt.Use();
                    Repaint();
                }
                else if (evt.type == EventType.MouseDown && evt.button == 0 && !evt.alt && !Tools.viewToolActive)
                {
                    // Project on the current dot's local-Z plane. This intentionally works
                    // on models without any colliders and avoids snapping to the front surface.
                    Vector3 old = rig.GetLocal(selected);
                    var plane = new Plane(rig.transform.forward,
                        rig.transform.TransformPoint(new Vector3(0, 0, old.z)));
                    Ray ray = HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                    float distance;
                    if (plane.Raycast(ray, out distance))
                    {
                        Vector3 newLocal = rig.transform.InverseTransformPoint(ray.GetPoint(distance));
                        newLocal.z = old.z;
                        Undo.RecordObject(rig, "Place Riggle landmark");
                        rig.SetLocal(selected, newLocal);
                        EditorUtility.SetDirty(rig);
                        placing = false;
                        evt.Use();
                        Repaint();
                    }
                }
                DrawDots(rig, false);
                return;
            }

            DrawDots(rig, true);
            Vector3 world = rig.GetWorld(selected);
            EditorGUI.BeginChangeCheck();
            Quaternion orientation = Tools.pivotRotation == PivotRotation.Local
                ? rig.transform.rotation : Quaternion.identity;
            Vector3 changedWorld = Handles.PositionHandle(world, orientation);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rig, "Move Riggle landmark");
                rig.SetLocal(selected, rig.transform.InverseTransformPoint(changedWorld));
                EditorUtility.SetDirty(rig);
            }
        }

        private void DrawDots(Riggle rig, bool clickable)
        {
            for (int i = 0; i < Riggle.PointCount; i++)
            {
                var point = (Riggle.Point)i;
                Vector3 pos = rig.GetWorld(point);
                bool right = Riggle.IsRightPoint(point);
                bool isSelected = point == selected;
                Handles.color = isSelected ? new Color(1f, 0.94f, 0.24f, 1f)
                    : right ? new Color(1f, 0.43f, 0.72f, rig.MirrorRight ? 0.55f : 1f)
                    : new Color(0.32f, 0.93f, 0.94f, 1f);
                float size = Mathf.Max(0.009f, HandleUtility.GetHandleSize(pos) * (isSelected ? 0.092f : 0.063f));
                if (clickable)
                {
                    if (Handles.Button(pos, Quaternion.identity, size, size * 1.4f, Handles.SphereHandleCap))
                    {
                        selected = point;
                        Repaint();
                    }
                }
                else
                {
                    Handles.SphereHandleCap(0, pos, Quaternion.identity, size * 1.5f, EventType.Repaint);
                }
                Handles.Label(pos + rig.transform.up * (size * 1.8f), point.ToString(),
                    EditorStyles.miniLabel);
            }
            Handles.color = Color.white;
        }

        private void DrawReferenceSkeleton(Riggle rig)
        {
            Handles.color = new Color(0.99f, 0.85f, 0.47f, 0.8f);
            DrawPath(rig, Riggle.Point.Pelvis, Riggle.Point.Neck, Riggle.Point.HeadTop);
            Handles.color = new Color(0.25f, 0.88f, 0.95f, 0.9f);
            DrawPath(rig, Riggle.Point.Neck, Riggle.Point.LeftShoulder,
                Riggle.Point.LeftElbow, Riggle.Point.LeftWrist);
            DrawPath(rig, Riggle.Point.Pelvis, Riggle.Point.LeftKnee, Riggle.Point.LeftAnkle);
            Handles.color = new Color(1f, 0.43f, 0.75f, rig.MirrorRight ? 0.55f : 0.9f);
            DrawPath(rig, Riggle.Point.Neck, Riggle.Point.RightShoulder,
                Riggle.Point.RightElbow, Riggle.Point.RightWrist);
            DrawPath(rig, Riggle.Point.Pelvis, Riggle.Point.RightKnee, Riggle.Point.RightAnkle);
            Handles.color = Color.white;
        }

        private static void DrawPath(Riggle rig, params Riggle.Point[] points)
        {
            var positions = new Vector3[points.Length];
            for (int i = 0; i < points.Length; i++) positions[i] = rig.GetWorld(points[i]);
            Handles.DrawAAPolyLine(3f, positions);
        }

        private static void DrawPlaceOverlay()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(14, 14, 370, 62), EditorStyles.helpBox);
            GUILayout.Label("RIGGLE • Click in Scene view to place selected dot", EditorStyles.boldLabel);
            GUILayout.Label("Uses the dot's depth plane. Esc cancels; drag handle later for depth.");
            GUILayout.EndArea();
            Handles.EndGUI();
        }
    }
}
