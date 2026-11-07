using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations.Rigging;

namespace ControlRig
{
    public class ControlRigBuilder : EditorWindow
    {
        [SerializeField] Animator targetAnimator;
        [MenuItem("Tools/Control Rig Builder")]
        public static void ShowWindow() => GetWindow<ControlRigBuilder>("Control Rig Builder");
        void OnGUI()
        {
            targetAnimator = (Animator)EditorGUILayout.ObjectField("Target Animator", targetAnimator, typeof(Animator), true);
            EditorGUILayout.HelpBox("Fits hand/foot targets, elbow/knee hints, hips, spine and head aim to a valid Humanoid. Rebuilding updates this control rig and preserves other rig layers. Build in the rest pose.", MessageType.Info);
            using (new EditorGUI.DisabledScope(!targetAnimator || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Build / Refit Rig"))
                    try { Selection.activeGameObject = Build(targetAnimator).gameObject; }
                    catch (Exception e) { Debug.LogError(e.Message, targetAnimator); }
            var modes = targetAnimator ? targetAnimator.GetComponentInChildren<ControlRigModes>() : null;
            if (modes)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("IK / FK · keep current pose", EditorStyles.boldLabel);
                for (int i = 0; i < modes.limbs.Length; i++)
                {
                    var limb = modes.limbs[i];
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(limb.label + " · " + limb.mode, GUILayout.Width(115));
                        if (GUILayout.Button(limb.mode == ControlRigModes.Mode.IK ? "Switch to FK" : "Switch to IK"))
                            ControlRigModesSetup.Switch(modes,i,limb.mode == ControlRigModes.Mode.IK ? ControlRigModes.Mode.FK : ControlRigModes.Mode.IK);
                        if (GUILayout.Button("Target")) Selection.activeTransform = limb.ik.data.target;
                    }
                    if (limb.mode == ControlRigModes.Mode.FK)
                        using (new EditorGUILayout.HorizontalScope())
                            for (int b = 0; b < 3; b++)
                                if (GUILayout.Button(new[] {"FK root", "FK bend", "FK tip"}[b])) Selection.activeTransform = limb.fk[b];
                }
            }
        }

        public static Rig_References Build(Animator animator)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Select a valid scene Humanoid Animator in Edit Mode.");
            if (EditorUtility.IsPersistent(animator)) throw new InvalidOperationException("Open the prefab or place the character in a scene first.");
            Transform Bone(HumanBodyBones id) => animator.GetBoneTransform(id) ?? throw new InvalidOperationException("Missing Humanoid bone: " + id);
            var hips = Bone(HumanBodyBones.Hips); var spine = Bone(HumanBodyBones.Spine);
            var neck = Bone(HumanBodyBones.Neck); var head = Bone(HumanBodyBones.Head);
            var ids = new[] { HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand,
                HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand,
                HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot };
            var bones = ids.Select(Bone).ToArray();
            var existing = animator.GetComponentsInChildren<Rig_References>(true).Where(r => r.GetComponentInParent<Animator>() == animator).ToArray();
            if (existing.Length > 1) throw new InvalidOperationException("More than one Control Rig found; choose one before refitting.");
            var prefab = Resources.Load<GameObject>(animator.GetComponent("Riggle") ? "Riggle Control Rig" : "Control Rig");
            if (!prefab) throw new InvalidOperationException("Control Rig prefab is missing from Resources.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Fit humanoid control rig");
            try
            {
                var refs = existing.FirstOrDefault();
                if (!refs)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, animator.transform);
                    Undo.RegisterCreatedObjectUndo(instance, "Create control rig"); refs = instance.GetComponent<Rig_References>();
                }
                var rig = refs.GetComponent<Rig>();
                if (!rig || !refs.hipRig || !refs.spineRig || !refs.headRig || !refs.LeftHandRig || !refs.RightHandRig || !refs.LeftLegRig || !refs.RightLegRig)
                    throw new InvalidOperationException("Control Rig prefab has incomplete constraint references.");
                foreach (var component in refs.GetComponentsInChildren<Component>(true)) Undo.RecordObject(component, "Fit control rig");
                var builder = animator.GetComponent<RigBuilder>() ?? Undo.AddComponent<RigBuilder>(animator.gameObject);
                Undo.RecordObject(builder, "Register control rig layer");
                if (!builder.layers.Any(layer => layer.rig == rig)) builder.layers.Add(new RigLayer(rig, true));

                var anatomy = animator.GetComponent<ControlRigAnatomy>() ?? Undo.AddComponent<ControlRigAnatomy>(animator.gameObject);
                Undo.RecordObject(anatomy, "Detect anatomical facing");
                anatomy.detectedForward = DetectFootForward(animator);
                Vector3 forward = anatomy.Forward;
                EditorUtility.SetDirty(anatomy);
                var hip = refs.hipRig.data; hip.constrainedObject = hips;
                var hipTarget = hip.sourceObjects[0].transform; hipTarget.SetPositionAndRotation(hips.position, hips.rotation);
                hip.maintainPositionOffset = false; hip.maintainRotationOffset = false; refs.hipRig.data = hip;
                var twist = refs.spineRig.data; twist.root = spine; twist.tip = neck;
                twist.rootTarget.SetPositionAndRotation(spine.position, spine.rotation);
                twist.tipTarget.SetPositionAndRotation(neck.position, neck.rotation); refs.spineRig.data = twist;
                FitLimb(refs.LeftHandRig, bones[0], bones[1], bones[2], -forward);
                FitLimb(refs.RightHandRig, bones[3], bones[4], bones[5], -forward);
                FitLimb(refs.LeftLegRig, bones[6], bones[7], bones[8], forward);
                FitLimb(refs.RightLegRig, bones[9], bones[10], bones[11], forward);
                var aim = refs.headRig.data; aim.constrainedObject = head;
                float height = Mathf.Max(.2f, Vector3.Distance(head.position, bones[8].position));
                aim.sourceObjects[0].transform.position = head.position + forward * height;
                aim.aimAxis = MultiAimConstraintData.Axis.Z; aim.upAxis = MultiAimConstraintData.Axis.Y;
                aim.worldUpType = MultiAimConstraintData.WorldUpType.ObjectRotationUp;
                aim.worldUpObject = animator.transform; aim.worldUpAxis = MultiAimConstraintData.Axis.Y;
                aim.maintainOffset = true; aim.offset = Vector3.zero; refs.headRig.data = aim;
                foreach (var component in refs.GetComponentsInChildren<Component>(true))
                { EditorUtility.SetDirty(component); PrefabUtility.RecordPrefabInstancePropertyModifications(component); }
                ControlRigModesSetup.Build(animator, refs);
                EditorUtility.SetDirty(builder);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
                return refs;
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
            finally { Undo.CollapseUndoOperations(group); }
        }

        public static float DetectFootForward(Animator animator)
        {
            var ankleIds = new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
            float positive = 0, negative = 0; int count = 0;
            float height = Mathf.Max(.1f, Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.Head).position,
                animator.GetBoneTransform(HumanBodyBones.LeftFoot).position));
            foreach (var id in ankleIds)
            {
                var ankle = animator.GetBoneTransform(id).position;
                float front = 0, back = 0; int samples = 0;
                foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var mesh = new Mesh(); skin.BakeMesh(mesh);
                    foreach (var v in mesh.vertices)
                    {
                        var delta = skin.transform.TransformPoint(v) - ankle;
                        float y = Vector3.Dot(delta, animator.transform.up);
                        float x = Vector3.Dot(delta, animator.transform.right);
                        float z = Vector3.Dot(delta, animator.transform.forward);
                        if (y < -height * .12f || y > height * .045f || Mathf.Abs(x) > height * .065f || Mathf.Abs(z) > height * .23f) continue;
                        front = Mathf.Max(front, z); back = Mathf.Max(back, -z); samples++;
                    }
                    DestroyImmediate(mesh);
                }
                if (samples > 8) { positive += front; negative += back; count++; }
            }
            // Do not guess from nearly symmetric feet.
            return count > 0 && negative > positive * 1.2f && negative - positive > height * .015f ? -1 : 1;
        }

        static void FitLimb(TwoBoneIKConstraint constraint, Transform root, Transform mid, Transform tip, Vector3 fallback)
        {
            var data = constraint.data;
            data.root = root; data.mid = mid; data.tip = tip;
            if (!data.target || !data.hint) throw new InvalidOperationException("Missing target or hint on " + constraint.name);
            data.target.SetPositionAndRotation(tip.position, tip.rotation);
            Vector3 axis = (tip.position - root.position).normalized;
            Vector3 bend = Vector3.ProjectOnPlane(mid.position - root.position, axis);
            float length = Vector3.Distance(root.position, mid.position) + Vector3.Distance(mid.position, tip.position);
            if (bend.magnitude < length * .05f) bend = Vector3.ProjectOnPlane(fallback, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(root.up, axis);
            // A pole near knee height can cross the root-target line as the foot lifts.
            // Keep it outside the reachable limb sphere, relative to the limb root.
            data.hint.position = root.position + bend.normalized * length * 2f;
            data.targetPositionWeight = 1; data.targetRotationWeight = 1; data.hintWeight = 1;
            data.maintainTargetPositionOffset = false; data.maintainTargetRotationOffset = false;
            constraint.data = data;
            // Refit the optional foot pivot controls too; the first source (foot target) remains the driver.
            var pivots = constraint.GetComponent<MultiReferentialConstraint>();
            if (pivots)
            {
                var pivotData = pivots.data;
                foreach (var pivot in pivotData.sourceObjects)
                {
                    if (!pivot || pivot == data.target) continue;
                    float offset = pivot.name.Contains("Heel") ? -.12f : pivot.name.Contains("Toe") ? .25f : .1f;
                    pivot.SetPositionAndRotation(tip.position + fallback * length * offset, tip.rotation);
                }
                pivotData.driver = pivotData.sourceObjects.IndexOf(data.target);
                if (pivotData.driver < 0) throw new InvalidOperationException("Foot target is missing from pivot sources.");
                pivots.data = pivotData;
            }
        }
    }
}
