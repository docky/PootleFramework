using Docky.RiggleTools.Editor;
using UnityEditor;
using UnityEngine;

namespace Docky.RiggleTools.Demo
{
    public static class RiggleGenericDemo
    {
        [MenuItem("GameObject/3D Object/Riggle Demo Quadruped", false, 11)]
        public static void CreateMenu()
        {
            var rig = Create();
            Selection.activeGameObject = rig.gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        public static RiggleGeneric Create()
        {
            var root = new GameObject("Riggle Demo Quadruped");
            Undo.RegisterCreatedObjectUndo(root, "Create generic quadruped demo");
            var rig = root.AddComponent<RiggleGeneric>();
            rig.SetQuadrupedPreset(new Bounds(new Vector3(0,.65f,0), new Vector3(.9f,1.3f,2.2f)));
            rig.assetFolder = "Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Generic";
            foreach (var j in rig.joints)
            {
                var end = string.IsNullOrEmpty(j.endJoint) ? j.tip : rig.joints.Find(n => n.name == j.endJoint).position;
                var middle = (j.position + end) * .5f;
                var part = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                part.name = j.name + " Mesh"; part.transform.SetParent(root.transform, false);
                part.transform.localPosition = middle;
                part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - j.position);
                part.transform.localScale = new Vector3(j.radius * 1.5f, (end - j.position).magnitude * .5f + j.radius * .45f, j.radius * 1.5f);
                Object.DestroyImmediate(part.GetComponent<Collider>());
            }
            RiggleGenericBuilder.Bake(rig);
            if (RiggleGenericLegsAnimator.AnimatorType != null) RiggleGenericLegsAnimator.Configure(rig);
            return rig;
        }
    }
}
