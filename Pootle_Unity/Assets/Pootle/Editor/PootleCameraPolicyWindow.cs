using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pootle.Editor
{
    // Direct controls avoid opening the Cinemachine component's native Inspector header.
    public sealed class PootleCameraPolicyWindow : EditorWindow
    {
        ObjectField shotField;
        VisualElement settings;
        [MenuItem("Pootle/Exploration/Camera Policy and Shot Settings")]
        public static void Open() => GetWindow<PootleCameraPolicyWindow>("Camera Policy").Show();
        public void CreateGUI()
        {
            rootVisualElement.Add(new HelpBox("Shared policy: viewed size 1.3 = approximately 30% closer framing. FOV and View Tilt apply to all assigned shots and supported visuals. Individual shots keep their authored base distances.", HelpBoxMessageType.Info));
            var scroll = new ScrollView(); rootVisualElement.Add(scroll);
            var policy = AssetDatabase.LoadAssetAtPath<PootleCameraPolicy>("Assets/CoastalHike/Exploration Camera Policy.asset");
            var policyField = new ObjectField("General camera policy") { objectType = typeof(PootleCameraPolicy), value = policy };
            scroll.Add(policyField);
            var policySettings = new VisualElement(); scroll.Add(policySettings);
            void BindPolicy(PootleCameraPolicy value)
            {
                policySettings.Unbind(); policySettings.Clear(); if (!value) return;
                var data = new SerializedObject(value);
                foreach (string name in new[] { "viewedSizeMultiplier", "fieldOfView", "enableViewTilt", "viewTiltDegrees", "limitTiltToCameraElevation" })
                    policySettings.Add(new PropertyField(data.FindProperty(name)));
                policySettings.Bind(data);
            }
            BindPolicy(policy); policyField.RegisterValueChangedCallback(e => BindPolicy(e.newValue as PootleCameraPolicy));
            rootVisualElement.Add(new Button(PootlePlacementWindow.Open) { text = "Open surface placement preview" });
            shotField = new ObjectField("Individual shot") { objectType = typeof(PootleExplorationShot), allowSceneObjects = true };
            scroll.Add(shotField); settings = new VisualElement(); scroll.Add(settings);
            shotField.RegisterValueChangedCallback(e => BindShot(e.newValue as PootleExplorationShot));
            var selected = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<PootleExplorationShot>() : null;
            shotField.value = selected ? selected : Object.FindAnyObjectByType<PootleExplorationShot>();
            settings.RegisterCallback<SerializedPropertyChangeEvent>(_ =>
            {
                var shot = shotField.value as PootleExplorationShot;
                if (shot) { shot.ApplySettings(); EditorUtility.SetDirty(shot); }
            });
        }
        void BindShot(PootleExplorationShot shot)
        {
            settings.Unbind(); settings.Clear(); if (!shot) return;
            var data = new SerializedObject(shot);
            foreach (string name in new[] { "policy", "mode", "pitchYaw", "distance", "screenPosition", "deadZone", "softZone", "damping", "spline", "lineStart", "lineEnd", "railOffset", "railOrientation", "railDamping", "anchor", "focusOrigin", "bounded", "positionBounds" })
                settings.Add(new PropertyField(data.FindProperty(name)));
            settings.Bind(data);
        }
    }
}
