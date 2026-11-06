using UnityEditor;
using UnityEngine;
namespace Pootle.FairyDemo.Editor
{
    [CustomEditor(typeof(FairyController))]
    public sealed class FairyInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var f=(FairyController)target;
            EditorGUILayout.Space();EditorGUILayout.LabelField("Live one-button state",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Action",f.CurrentAction.ToString());
            EditorGUILayout.ObjectField("Focused target",f.Focus,typeof(FairyTarget),true);
            EditorGUILayout.LabelField("Confirmed ground",f.ConfirmedGround.ToString());
            EditorGUILayout.LabelField("Settle / wind-up",$"{f.Settle:F2} / {f.Windup:F2} s");
            EditorGUILayout.LabelField("Actual speed / vertical",$"{f.ActualSpeed:F2} / {(f.Character?f.Character.Velocity.y:0):F2} m/s");
            EditorGUILayout.LabelField("Flaps",f.Flaps.ToString());
            if(Application.isPlaying&&GUILayout.Button("Rebind joystick fire button"))f.RebindJoystickButton(2);
            if(Application.isPlaying)Repaint();
        }
    }
}
