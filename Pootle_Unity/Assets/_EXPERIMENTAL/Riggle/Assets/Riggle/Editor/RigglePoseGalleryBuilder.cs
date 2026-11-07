using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;
using ControlRig;

namespace Docky.RiggleTools.Editor
{
    internal static class RigglePoseGalleryBuilder
    {
        public static readonly string[] Titles = { "01  REST", "02  REACH", "03  CROUCH", "04  KNEE LIFT", "05  WRIST ROLL", "06  MATCHED FK" };
        static readonly string[] Details = { "Neutral", "Arms & elbows", "Hips & knees", "Deep leg bend", "100° twist", "Pose preserved" };
        public static void Rebuild(Riggle rig)
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Build the pose row in Edit Mode.");
            var sourceAnimator = rig.GetComponent<Animator>();
            if (!sourceAnimator || !sourceAnimator.avatar || !sourceAnimator.avatar.isHuman)
                throw new System.InvalidOperationException("Enable Try Humanoid Avatar and bake a valid Humanoid before building poses.");
            if (!rig.GetComponentInChildren<ControlRigModes>()) ControlRigBuilder.Build(sourceAnimator);
            var prefab = RigglePrefabExporter.Save(rig);
            var gallery = rig.GetComponent<RigglePoseGallery>() ?? Undo.AddComponent<RigglePoseGallery>(rig.gameObject);
            Undo.RecordObject(gallery, "Build visible pose tests"); gallery.source = rig;
            if (gallery.generatedRow) Undo.DestroyObjectImmediate(gallery.generatedRow.gameObject);
            var row = new GameObject(rig.name + " · pose tests"); Undo.RegisterCreatedObjectUndo(row, "Create pose row");
            gallery.generatedRow = row.transform;
            bool extras = rig.GetComponent<RiggleAppendageRig>() && rig.GetComponent<RiggleAppendageRig>().chains.Length > 0;
            var titles = extras ? Titles.Concat(new[] { "07  CURL & SPREAD", "08  SWEEP & FOLD" }).ToArray() : Titles;
            for (int p = 0; p < titles.Length; p++)
            {
                var model = Object.Instantiate(prefab, row.transform); model.name = titles[p];
                model.transform.SetPositionAndRotation(rig.transform.position + Vector3.right * gallery.spacing * (p + 1), rig.transform.rotation);
                model.transform.localScale = rig.transform.lossyScale;
                model.GetComponent<RigBuilder>().enabled = false;
                model.GetComponent<Animator>().enabled = false;
                model.GetComponentInChildren<ControlRigModes>().enabled = false;
                ApplyPose(model, p);
                var extraRig = model.GetComponent<RiggleAppendageRig>();
                if (extraRig) { RiggleAppendageBuilder.Pose(extraRig, p < 6 ? 0 : p - 5, false); extraRig.enabled = false; }
                var twists = model.GetComponent<RiggleTwistDistribution>(); if (twists) { twists.Evaluate(); twists.enabled = false; }
                var corrective = model.GetComponent<RiggleCorrectives>(); if (corrective) { corrective.enabled = false; corrective.Evaluate(); }
                Label(row.transform, model.transform.position + new Vector3(0,.035f,-.7f), titles[p] + "\n" + (p < Details.Length ? Details[p] : "Ears · tail · wings"));
            }
            BuildCorrectiveComparison(rig, gallery, prefab);
            gallery.ApplyVisibility();
            EditorUtility.SetDirty(gallery);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        }
        static void BuildCorrectiveComparison(Riggle rig, RigglePoseGallery gallery, GameObject prefab)
        {
            if (gallery.correctiveRow) Undo.DestroyObjectImmediate(gallery.correctiveRow.gameObject);
            var sourceDriver = rig.GetComponent<RiggleCorrectives>();
            if (!sourceDriver || sourceDriver.bindings.Length == 0) return;
            var row = new GameObject(rig.name + " · corrective comparison"); Undo.RegisterCreatedObjectUndo(row, "Create corrective comparison");
            gallery.correctiveRow = row.transform;
            for (int p = 0; p < 4; p++)
            {
                bool shoulder = p < 2, enabled = p % 2 == 1;
                string label = (shoulder ? "SHOULDER" : "HIP") + (enabled ? " · ON" : " · OFF");
                var model = Object.Instantiate(prefab, row.transform); model.name = label;
                model.transform.SetPositionAndRotation(rig.transform.position + Vector3.right * gallery.spacing * (p + 1), rig.transform.rotation);
                model.transform.localScale = rig.transform.lossyScale;
                model.GetComponent<RigBuilder>().enabled = false; model.GetComponent<Animator>().enabled = false;
                model.GetComponentInChildren<ControlRigModes>().enabled = false;
                var animator = model.GetComponent<Animator>();
                float facing = model.GetComponent<ControlRigAnatomy>()?.ForwardSign ?? 1;
                if (shoulder) animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).localRotation *= Quaternion.Euler(0,0,-65);
                else
                {
                    animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg).localRotation *= Quaternion.Euler(-90 * facing,0,0);
                    animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation *= Quaternion.Euler(100 * facing,0,0);
                }
                var twists = model.GetComponent<RiggleTwistDistribution>(); if (twists) { twists.Evaluate(); twists.enabled = false; }
                var driver = model.GetComponent<RiggleCorrectives>(); driver.enabled = false; driver.weight = enabled ? 1 : 0; driver.Evaluate();
                Label(row.transform, model.transform.position + new Vector3(0,.035f,-.7f), label + "\n" + (enabled ? "Corrective shape" : "Base skinning"));
            }
            row.SetActive(gallery.visible);
        }
        public static void ApplyPose(GameObject model, int pose)
        {
            var animator = model.GetComponent<Animator>();
                float facing = model.GetComponent<ControlRigAnatomy>()?.ForwardSign ?? 1;
            Transform Bone(HumanBodyBones b) => animator.GetBoneTransform(b);
            void Rotate(HumanBodyBones b, Vector3 angles) { angles.x *= facing; angles.y *= facing; Bone(b).localRotation *= Quaternion.Euler(angles); }
            if (pose == 1 || pose == 5)
            {
                Rotate(HumanBodyBones.LeftUpperArm, new Vector3(0,0,-55));
                Rotate(HumanBodyBones.LeftLowerArm, new Vector3(0,65,0));
                Rotate(HumanBodyBones.RightUpperArm, new Vector3(0,0,25));
                Rotate(HumanBodyBones.RightLowerArm, new Vector3(0,-45,0));
            }
            if (pose == 2)
            {
                foreach (var b in new[] {HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg}) Rotate(b,new Vector3(-50,0,0));
                foreach (var b in new[] {HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg}) Rotate(b,new Vector3(95,0,0));
                foreach (var b in new[] {HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot}) Rotate(b,new Vector3(-45,0,0));
                Bone(HumanBodyBones.Hips).position -= model.transform.up * animator.humanScale * .17f;
                Rotate(HumanBodyBones.Spine,new Vector3(15,0,0));
            }
            if (pose == 3)
            {
                Rotate(HumanBodyBones.LeftUpperLeg,new Vector3(-65,0,0));
                Rotate(HumanBodyBones.LeftLowerLeg,new Vector3(95,0,0));
                Rotate(HumanBodyBones.LeftFoot,new Vector3(-30,0,0));
            }
            if (pose == 4)
            {
                var forearm = Bone(HumanBodyBones.LeftLowerArm);
                var axis = forearm.InverseTransformDirection(Bone(HumanBodyBones.LeftHand).position - forearm.position).normalized;
                Bone(HumanBodyBones.LeftHand).localRotation *= Quaternion.AngleAxis(100, axis);
                Rotate(HumanBodyBones.RightUpperLeg,new Vector3(0,20,0));
            }
            if (pose == 5)
            {
                var modes = model.GetComponentInChildren<ControlRigModes>();
                for (int i=0;i<modes.limbs.Length;i++) modes.SwitchMode(i,ControlRigModes.Mode.FK);
            }
        }
        static void Label(Transform parent, Vector3 position, string text)
        {
            var label = new GameObject(text.Split('\n')[0] + " · label"); label.transform.SetParent(parent,false);label.transform.position=position; label.transform.rotation=Quaternion.Euler(90,0,0);
            var mesh = label.AddComponent<TextMesh>(); mesh.text=text;mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            mesh.fontSize=48;mesh.characterSize=.055f;mesh.anchor=TextAnchor.UpperCenter;mesh.alignment=TextAlignment.Center;mesh.color=new Color(.88f,.95f,1);
            label.GetComponent<MeshRenderer>().sharedMaterial=mesh.font.material;
        }
    }
}
