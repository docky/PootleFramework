using UnityEngine;
using UnityEditor;

namespace ProceduralPrimitivesUtil.PPEditor
{
    [CustomEditor(typeof(Lathe))]
    [CanEditMultipleObjects]
    public class LatheEditor : PPCircularBaseEditor
    {
        SerializedProperty profile;
        SerializedProperty profileSubdivisions;
        SerializedProperty capBottom;
        SerializedProperty capTop;
        SerializedProperty angleBasedSmoothing;
        SerializedProperty hardEdgeAngle;

        protected override void Init()
        {
            base.Init();

            // PPCircularBaseEditor already wires: sides, sliceOn, sliceFrom, sliceTo
            profile             = serializedObject.FindProperty("profile");
            profileSubdivisions = serializedObject.FindProperty("profileSubdivisions");
            capBottom           = serializedObject.FindProperty("capBottom");
            capTop              = serializedObject.FindProperty("capTop");
            angleBasedSmoothing = serializedObject.FindProperty("angleBasedSmoothing");
            hardEdgeAngle       = serializedObject.FindProperty("hardEdgeAngle");
        }

        
        
        
        protected override void DrawBasicParameters()
        {
            // basic fields in the foldout
            EditorGUILayout.PropertyField(capBottom, new GUIContent("Cap Bottom"));
            EditorGUILayout.PropertyField(capTop,    new GUIContent("Cap Top"));
            EditorGUILayout.PropertyField(angleBasedSmoothing, new GUIContent("Angle-based Smoothing"));
            if (angleBasedSmoothing.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(hardEdgeAngle, new GUIContent("Hard Edge Angle"));
                EditorGUI.indentLevel--;
            }

            // close the Basic Parameters foldout *here*
            EditorGUILayout.EndFoldoutHeaderGroup();

            // draw profile as a plain list below the foldout, same as Polygon.points
            EditorGUILayout.PropertyField(profile, new GUIContent("Profile (X = radius, Y = height)"), true);
        }

        

        protected override void DrawSegments()
        {
            // "sides" from PPCircularBase
            base.DrawSegments();
            EditorGUILayout.PropertyField(profileSubdivisions, new GUIContent("Profile Subdivisions"));
        }
    }
}
