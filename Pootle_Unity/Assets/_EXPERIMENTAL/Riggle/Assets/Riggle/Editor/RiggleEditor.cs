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
        private RiggleDeformationInspector deformation;
        private RiggleInfluenceView influences;
        private void OnEnable() { deformation = new RiggleDeformationInspector((Riggle)target); influences = new RiggleInfluenceView((Riggle)target, Repaint); }
        private void OnDisable() { influences?.Dispose(); influences = null; deformation?.Dispose(); deformation = null; }

        public override void OnInspectorGUI()
        {
            var rig = (Riggle)target;
            bool wasViewing = influences != null && influences.Active;
            if (influences != null && influences.Draw())
            {
                if (!wasViewing) { placing = false; deformation?.EnterInspection(); }
                return;
            }
            serializedObject.Update();
            if (GUILayout.Button("Open guided Rig Helper", GUILayout.Height(28))) RiggleHelperWindow.Open(rig);
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
                "generatedBones", "bakedSources", "bakedAvatar", "attachedObjects");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Landmark editor", EditorStyles.boldLabel);
            selected = (Riggle.Point)EditorGUILayout.EnumPopup("Selected dot", selected);
            EditorGUI.BeginChangeCheck();
            Vector3 value = EditorGUILayout.Vector3Field("Local position", rig.GetLocal(selected));
            if (EditorGUI.EndChangeCheck())
            {
                RecordPointUndo(rig, selected, "Move Riggle dot");
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
                    for (int i = 0; i < Riggle.PointCount; i++) RecordPointUndo(rig, (Riggle.Point)i, "Fit Riggle dots to mesh");
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
                        RecordPointUndo(rig, point, "Edit Riggle landmark");
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
                deformation?.StopPreview();
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
                    { deformation?.StopPreview(); RiggleBuilder.Clear(rig); GUIUtility.ExitGUI(); }
                }
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField("Generated character", EditorStyles.boldLabel);
                if (GUILayout.Button("Save skinned character prefab"))
                { deformation?.StopPreview(); EditorGUIUtility.PingObject(RigglePrefabExporter.Save(rig)); }
                var poseAnimator = rig.GetComponent<Animator>();
                bool canShowPoses = poseAnimator && poseAnimator.avatar && poseAnimator.avatar.isHuman;
                using (new EditorGUI.DisabledScope(!canShowPoses || EditorApplication.isPlaying))
                    if (GUILayout.Button("Build / refresh visible pose row to the right"))
                    { deformation?.StopPreview(); RigglePoseGalleryBuilder.Rebuild(rig); }
                if (!canShowPoses) EditorGUILayout.HelpBox("Bake with Try Humanoid Avatar to enable the pose tests.", MessageType.Info);
                var gallery = rig.GetComponent<RigglePoseGallery>();
                if (gallery && gallery.generatedRow)
                {
                    bool visible = EditorGUILayout.Toggle("Show pose tests", gallery.visible);
                    bool compare = gallery.correctiveRow && EditorGUILayout.Toggle("Compare corrections", gallery.compareCorrectives);
                    if (visible != gallery.visible || compare != gallery.compareCorrectives)
                    {
                        Undo.RecordObject(gallery, "Toggle pose tests"); Undo.RecordObject(gallery.generatedRow.gameObject, "Toggle pose tests");
                        if (gallery.correctiveRow) Undo.RecordObject(gallery.correctiveRow.gameObject, "Toggle corrective tests");
                        gallery.visible = visible; gallery.compareCorrectives = compare; gallery.ApplyVisibility();
                        EditorUtility.SetDirty(gallery);
                    }
                }
                var modes = rig.GetComponentInChildren<ControlRig.ControlRigModes>();
                if (modes)
                {
                    EditorGUILayout.LabelField("IK / FK — preserve current pose", EditorStyles.boldLabel);
                    for (int i = 0; i < modes.limbs.Length; i++)
                    {
                        var limb = modes.limbs[i];
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(limb.label + " · " + limb.mode);
                            if (GUILayout.Button(limb.mode == ControlRig.ControlRigModes.Mode.IK ? "Switch to FK" : "Switch to IK"))
                                ControlRig.ControlRigModesSetup.Switch(modes, i, limb.mode == ControlRig.ControlRigModes.Mode.IK ? ControlRig.ControlRigModes.Mode.FK : ControlRig.ControlRigModes.Mode.IK);
                            if (GUILayout.Button("Select control", GUILayout.Width(100)))
                                Selection.activeTransform = limb.mode == ControlRig.ControlRigModes.Mode.IK ? limb.ik.data.target : limb.fk[0];
                        }
                    }
                }
                if (rig.Assembly == Riggle.AssemblyMode.SmoothSkin) deformation?.Draw();
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

        internal static void RecordPointUndo(Riggle rig, Riggle.Point point, string label)
        {
            Undo.RecordObject(rig, label);
            var refs = rig.GetComponent<RiggleReferencePoints>();var driven = refs ? refs.Driven(point) : null;
            if (driven) Undo.RecordObject(driven.TryGetComponent<RiggleOriginReference>(out var origin) ? (UnityEngine.Object)origin : driven, label);
        }

        private void OnSceneGUI()
        {
            var rig = (Riggle)target;
            if (!rig) return;
            if (influences != null && influences.SceneGUI()) return;
            if (rig.Assembly == Riggle.AssemblyMode.SmoothSkin && deformation != null && deformation.SceneGUI()) return;
            rig.EnsurePoints();
            RiggleAppendageBuilder.DrawGuides(rig);
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
                        RecordPointUndo(rig, selected, "Place Riggle landmark");
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
                RecordPointUndo(rig, selected, "Move Riggle landmark");
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
