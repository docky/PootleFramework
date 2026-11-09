using UnityEditor;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    /// <summary>Fast no-import test character made entirely from Unity primitives.</summary>
    internal static class RiggleDemo
    {
        [MenuItem("GameObject/3D Object/Riggle Demo Biped", false, 25)]
        private static void CreateDemo(MenuCommand command)
        {
            GameObject root = new GameObject("Riggle Demo - Bean Biped");
            GameObjectUtility.SetParentAndAlign(root, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(root, "Create Riggle Demo");

            AddBall(root.transform, "Bean Torso", new Vector3(0, 0.99f, 0),
                new Vector3(0.55f, 0.65f, 0.36f));
            AddBall(root.transform, "Oversized Head", new Vector3(0, 1.58f, 0),
                new Vector3(0.70f, 0.71f, 0.63f));
            AddCapsule(root.transform, "Left Arm", new Vector3(-0.27f, 1.31f, 0),
                new Vector3(-0.81f, 1.29f, 0), 0.09f);
            AddCapsule(root.transform, "Right Arm", new Vector3(0.27f, 1.31f, 0),
                new Vector3(0.81f, 1.29f, 0), 0.09f);
            AddBall(root.transform, "Left Hand", new Vector3(-0.82f, 1.29f, 0),
                new Vector3(0.22f, 0.21f, 0.21f));
            AddBall(root.transform, "Right Hand", new Vector3(0.82f, 1.29f, 0),
                new Vector3(0.22f, 0.21f, 0.21f));
            AddCapsule(root.transform, "Left Leg", new Vector3(-0.14f, 0.79f, 0),
                new Vector3(-0.14f, 0.10f, 0), 0.145f);
            AddCapsule(root.transform, "Right Leg", new Vector3(0.14f, 0.79f, 0),
                new Vector3(0.14f, 0.10f, 0), 0.145f);
            AddBall(root.transform, "Left Foot", new Vector3(-0.14f, 0.085f, 0.115f),
                new Vector3(0.31f, 0.16f, 0.45f));
            AddBall(root.transform, "Right Foot", new Vector3(0.14f, 0.085f, 0.115f),
                new Vector3(0.31f, 0.16f, 0.45f));

            Riggle rig = Undo.AddComponent<Riggle>(root);
            Undo.RecordObject(rig, "Set up demo Riggle dots");
            rig.MirrorRight = true;
            rig.SetLocal(Riggle.Point.Pelvis, new Vector3(0, 0.87f, 0));
            rig.SetLocal(Riggle.Point.Neck, new Vector3(0, 1.34f, 0));
            rig.SetLocal(Riggle.Point.HeadTop, new Vector3(0, 1.93f, 0));
            rig.SetLocal(Riggle.Point.LeftShoulder, new Vector3(-0.27f, 1.31f, 0));
            rig.SetLocal(Riggle.Point.LeftElbow, new Vector3(-0.55f, 1.30f, 0));
            rig.SetLocal(Riggle.Point.LeftWrist, new Vector3(-0.80f, 1.29f, 0));
            rig.SetLocal(Riggle.Point.LeftKnee, new Vector3(-0.14f, 0.42f, 0));
            rig.SetLocal(Riggle.Point.LeftAnkle, new Vector3(-0.14f, 0.10f, 0));
            EditorUtility.SetDirty(rig);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        private static void AddBall(Transform parent, string name, Vector3 center, Vector3 scale)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;
            var collider = go.GetComponent<Collider>();
            if (collider) Undo.DestroyObjectImmediate(collider);
        }

        private static void AddCapsule(Transform parent, string name, Vector3 a, Vector3 b, float radius)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            float len = Vector3.Distance(a, b);
            go.transform.localScale = new Vector3(radius * 2, len * 0.5f, radius * 2);
            var collider = go.GetComponent<Collider>();
            if (collider) Undo.DestroyObjectImmediate(collider);
        }
    }
}
