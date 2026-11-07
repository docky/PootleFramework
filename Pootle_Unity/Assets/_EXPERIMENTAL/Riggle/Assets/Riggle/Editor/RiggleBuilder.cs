using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Docky.RiggleTools.Editor
{
    /// <summary>Pure editor-side generation. No edit-mode Update/OnValidate/ExecuteAlways.</summary>
    internal static class RiggleBuilder
    {
        internal enum Region { Torso, Head, Arm, Leg, Appendage }

        internal sealed class BoneSpec
        {
            public string name;
            public string parent;
            public Vector3 position;
            public Vector3 end;
            public Region region;
            public HumanBodyBones human;
            public bool skinned;
            public Transform transform;
            public int skinIndex = -1;
            public string twistSource;
            public string appendage;
            public float radiusFraction;

            public BoneSpec(string name, string parent, Vector3 position, Vector3 end,
                Region region, HumanBodyBones human, bool skinned = true)
            {
                this.name = name;
                this.parent = parent;
                this.position = position;
                this.end = end;
                this.region = region;
                this.human = human;
                this.skinned = skinned;
            }
        }

        public static void Bake(Riggle rig)
        {
            if (!CheckInput(rig)) return;
            var rigid = rig.Assembly != Riggle.AssemblyMode.SmoothSkin ? RiggleRigidParts.Plan(rig) : null;
            var sources = RiggleRigidParts.Sources(rig);
            List<BoneSpec> bones = MakeBoneSpecs(rig);
            // Reject topology/bone changes before touching the previous generated hierarchy.
            foreach (var source in rigid == null ? sources : new List<MeshFilter>())
                RiggleDeformationTools.Validate(source.sharedMesh, source.GetComponent<RiggleMeshEdits>(), bones.ConvertAll(b => b.name));
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Riggle: generate skeleton and skinning");
            try
            {
                Clear(rig, rig.TryHumanoid);
                Undo.RecordObject(rig, "Riggle Output");
                var output = CreateChild("Riggle_Bones", rig.transform);
                rig.GeneratedBones = output;

                var boneLookup = new Dictionary<string, Transform>();
                foreach (var spec in bones)
                {
                    Transform parent = spec.parent == null ? output : boneLookup[spec.parent];
                    Transform bone = CreateChild(spec.name, parent);
                    bone.SetPositionAndRotation(spec.position, rig.transform.rotation);
                    spec.transform = bone;
                    boneLookup.Add(spec.name, bone);
                }
                ConfigureTwists(rig, bones, boneLookup);

                List<BoneSpec> deformBones = bones.FindAll(x => x.skinned);
                for (int i = 0; i < deformBones.Count; i++) deformBones[i].skinIndex = i;
                string folder = PrepareFolder(rig.AssetFolder);
                string baseName = SafeFileName(rig.gameObject.name);

                if (rigid != null) RiggleRigidParts.Build(rig, rigid, deformBones, boneLookup, folder, baseName);
                foreach (var filter in rigid == null ? sources : new List<MeshFilter>())
                {
                    MeshRenderer originalRenderer = filter.GetComponent<MeshRenderer>();
                    Mesh skinnedCopy = RiggleSkinner.BuildSkinnedMesh(rig, filter, deformBones);
                    var edits = filter.GetComponent<RiggleMeshEdits>();
                    RiggleDeformationTools.ApplyWeights(filter.sharedMesh, skinnedCopy, deformBones.ConvertAll(b => b.name).ToArray(), edits);
                    RiggleDeformationTools.ApplyShapes(skinnedCopy, edits);
                    string newAssetPath = AssetDatabase.GenerateUniqueAssetPath(
                        folder + "/" + baseName + "_" + SafeFileName(filter.gameObject.name) + "_Riggle.asset");
                    AssetDatabase.CreateAsset(skinnedCopy, newAssetPath);

                    // Child-of-source at identity preserves the full local-to-world matrix,
                    // even with rotated parents and non-uniform scale.
                    Transform skinGo = CreateChild("Riggle_Skin_" + filter.gameObject.name, filter.transform);
                    var skin = Undo.AddComponent<SkinnedMeshRenderer>(skinGo.gameObject);
                    skinGo.gameObject.layer = filter.gameObject.layer;
                    skin.sharedMesh = skinnedCopy;
                    skin.sharedMaterials = originalRenderer.sharedMaterials;
                    skin.bones = deformBones.ConvertAll(x => x.transform).ToArray();
                    skin.rootBone = boneLookup["Riggle_Hips"];
                    skin.quality = SkinQuality.Bone4;
                    skin.localBounds = RiggleDeformationTools.ExpandedBounds(skinnedCopy, edits);
                    skin.updateWhenOffscreen = true; // Safe defaults for exaggerated motion and prototyping.
                    skin.shadowCastingMode = originalRenderer.shadowCastingMode;
                    skin.receiveShadows = originalRenderer.receiveShadows;
                    skin.lightProbeUsage = originalRenderer.lightProbeUsage;
                    skin.reflectionProbeUsage = originalRenderer.reflectionProbeUsage;

                    rig.BakedSources.Add(new Riggle.BakedSource
                    {
                        source = originalRenderer,
                        wasEnabled = originalRenderer.enabled,
                        baked = skin
                    });
                    Undo.RecordObject(originalRenderer, "Hide original, show Riggle skin");
                    originalRenderer.enabled = false;
                }

                if (rig.TryHumanoid) TryMakeHumanoid(rig, bones, folder, baseName);
                RiggleAppendageBuilder.BuildControls(rig, bones, boneLookup);
                if (rigid != null) RiggleRigidParts.Attach(rig, rigid, boneLookup);
                if (rig.GetComponentInChildren<ControlRig.Rig_References>())
                {
                    if (!rig.BakedAvatar) throw new InvalidOperationException("Cannot reconnect existing controls without a valid Humanoid Avatar.");
                    ControlRig.ControlRigBuilder.Build(rig.GetComponent<Animator>());
                }
                RiggleDeformationTools.Bind(rig);
                AssetDatabase.SaveAssets();
                RigglePrefabExporter.Save(rig);
                EditorUtility.SetDirty(rig);
                Debug.Log($"Riggle: created {deformBones.Count} deform bones and {sources.Count} skinned mesh(es) on '{rig.name}'.", rig);
            }
            catch (Exception e)
            {
                Debug.LogException(e, rig);
                Undo.RevertAllDownToGroup(group);
                Debug.LogError("Riggle bake failed. The previous scene hierarchy was restored by Undo; newly created asset files may still be in the project.", rig);
            }
            finally
            {
                Undo.CollapseUndoOperations(group);
            }
        }

        public static void Clear(Riggle rig) => Clear(rig, false);

        private static void Clear(Riggle rig, bool preserveControls)
        {
            Undo.RecordObject(rig, "Clear Riggle bake");
            RiggleRigidParts.RestoreAttachments(rig);
            var appendages = rig.GetComponent<RiggleAppendageRig>();
            if (appendages) { Undo.RecordObject(appendages, "Clear appendage controls"); appendages.chains = Array.Empty<RiggleAppendageRig.Chain>(); }
            if (!preserveControls)
            {
                var builder = rig.GetComponent<UnityEngine.Animations.Rigging.RigBuilder>();
                foreach (var controls in rig.GetComponentsInChildren<ControlRig.Rig_References>())
                {
                    if (!controls.hipRig || !controls.hipRig.data.constrainedObject || !rig.GeneratedBones ||
                        !controls.hipRig.data.constrainedObject.IsChildOf(rig.GeneratedBones)) continue;
                    if (builder)
                    {
                        Undo.RecordObject(builder, "Remove generated skeleton control layer");
                        var layerRig = controls.GetComponent<UnityEngine.Animations.Rigging.Rig>();
                        builder.layers.RemoveAll(layer => layer.rig == layerRig);
                    }
                    Undo.DestroyObjectImmediate(controls.gameObject);
                }
            }
            var animator = rig.GetComponent<Animator>();
            if (animator && rig.BakedAvatar && animator.avatar == rig.BakedAvatar)
            {
                Undo.RecordObject(animator, "Unassign Riggle avatar");
                animator.avatar = null;
            }

            foreach (var binding in rig.BakedSources)
            {
                if (binding == null) continue;
                if (binding.source)
                {
                    Undo.RecordObject(binding.source, "Restore original mesh visibility");
                    binding.source.enabled = binding.wasEnabled;
                }
                if (binding.baked) Undo.DestroyObjectImmediate(binding.baked.gameObject);
            }
            rig.BakedSources.Clear();
            if (rig.GeneratedBones) Undo.DestroyObjectImmediate(rig.GeneratedBones.gameObject);
            rig.GeneratedBones = null;
            rig.BakedAvatar = null;
            var correctives = rig.GetComponent<RiggleCorrectives>();
            if (correctives) { Undo.RecordObject(correctives, "Clear corrective bindings"); correctives.bindings = Array.Empty<RiggleCorrectives.Binding>(); }
            var twists = rig.GetComponent<RiggleTwistDistribution>();
            if (twists) { Undo.RecordObject(twists, "Clear twist bindings"); twists.chains = Array.Empty<RiggleTwistDistribution.Chain>(); }
            EditorUtility.SetDirty(rig);
        }

        private static bool CheckInput(Riggle rig)
        {
            if (!rig || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Riggle is an editor-only bake. Stop Play Mode first.", rig);
                return false;
            }
            if (PrefabUtility.IsPartOfPrefabAsset(rig.gameObject))
            {
                Debug.LogWarning("Open a prefab in Prefab Mode, or place it in a scene, before baking Riggle.", rig);
                return false;
            }
            if (string.IsNullOrEmpty(rig.AssetFolder) ||
                (rig.AssetFolder != "Assets" && !rig.AssetFolder.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                Debug.LogError("Riggle asset folder must be under Assets/.", rig);
                return false;
            }
            var filters = RiggleRigidParts.Sources(rig);
            if (filters.Count == 0 && !(rig.Assembly != Riggle.AssemblyMode.SmoothSkin && rig.AttachedObjects.Count > 0))
            {
                Debug.LogError("Riggle needs at least one MeshFilter + MeshRenderer. You may explicitly assign source meshes in the component.", rig);
                return false;
            }
            foreach (var filter in filters)
            {
                if (filter.transform == rig.transform && filter.GetComponent<MeshRenderer>() == null)
                {
                    Debug.LogError("Riggle source mesh has no MeshRenderer.", rig);
                    return false;
                }
                try { var ignored = filter.sharedMesh.vertices.Length; }
                catch (Exception e)
                {
                    Debug.LogError($"Riggle cannot read mesh '{filter.sharedMesh.name}'. Enable Read/Write in its import settings. {e.Message}", filter);
                    return false;
                }
            }

            var pelvis = rig.GetLocal(Riggle.Point.Pelvis);
            var neck = rig.GetLocal(Riggle.Point.Neck);
            var crown = rig.GetLocal(Riggle.Point.HeadTop);
            float h = Vector3.Distance(crown, pelvis);
            if (h < 0.1f || neck.y <= pelvis.y || crown.y <= neck.y)
            {
                Debug.LogError("Riggle: landmarks must have pelvis < neck < top of head, with meaningful height.", rig);
                return false;
            }
            for (int side = 0; side < 2; side++)
            {
                bool left = side == 0;
                var shoulder = rig.GetLocal(left ? Riggle.Point.LeftShoulder : Riggle.Point.RightShoulder);
                var elbow = rig.GetLocal(left ? Riggle.Point.LeftElbow : Riggle.Point.RightElbow);
                var wrist = rig.GetLocal(left ? Riggle.Point.LeftWrist : Riggle.Point.RightWrist);
                var knee = rig.GetLocal(left ? Riggle.Point.LeftKnee : Riggle.Point.RightKnee);
                var ankle = rig.GetLocal(left ? Riggle.Point.LeftAnkle : Riggle.Point.RightAnkle);
                if (Vector3.Distance(shoulder, elbow) < h * 0.02f ||
                    Vector3.Distance(elbow, wrist) < h * 0.02f ||
                    Vector3.Distance(knee, ankle) < h * 0.02f)
                {
                    Debug.LogError("Riggle: one or more shoulder/elbow/wrist or knee/ankle segments are too short.", rig);
                    return false;
                }
            }
            return true;
        }

        private static Transform CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        internal static List<BoneSpec> MakeBoneSpecs(Riggle rig)
        {
            Vector3 pelvis = rig.GetWorld(Riggle.Point.Pelvis);
            Vector3 neck = rig.GetWorld(Riggle.Point.Neck);
            Vector3 top = rig.GetWorld(Riggle.Point.HeadTop);
            Vector3 spine = Vector3.Lerp(pelvis, neck, 0.34f);
            Vector3 chest = Vector3.Lerp(pelvis, neck, 0.63f);
            Vector3 upperChest = Vector3.Lerp(pelvis, neck, 0.83f);
            Vector3 head = Vector3.Lerp(neck, top, rig.HeadJointHeight);

            var b = new List<BoneSpec>();
            b.Add(new BoneSpec("Riggle_Hips", null, pelvis, spine, Region.Torso, HumanBodyBones.Hips));
            b.Add(new BoneSpec("Riggle_Spine", "Riggle_Hips", spine, chest, Region.Torso, HumanBodyBones.Spine));
            b.Add(new BoneSpec("Riggle_Chest", "Riggle_Spine", chest, upperChest, Region.Torso, HumanBodyBones.Chest));
            b.Add(new BoneSpec("Riggle_UpperChest", "Riggle_Chest", upperChest, neck, Region.Torso, HumanBodyBones.UpperChest));
            b.Add(new BoneSpec("Riggle_Neck", "Riggle_UpperChest", neck, head, Region.Torso, HumanBodyBones.Neck));
            b.Add(new BoneSpec("Riggle_Head", "Riggle_Neck", head, top, Region.Head, HumanBodyBones.Head));
            b.Add(new BoneSpec("Riggle_HeadEnd", "Riggle_Head", top, top, Region.Head, HumanBodyBones.LastBone, false));

            float shoulderHalfWidth = Mathf.Abs(rig.GetLocal(Riggle.Point.RightShoulder).x -
                                                  rig.GetLocal(Riggle.Point.LeftShoulder).x) * 0.5f;
            float hipHalfWidth = Mathf.Clamp(shoulderHalfWidth * rig.HipToShoulderWidth,
                0.03f * Vector3.Distance(rig.GetLocal(Riggle.Point.Neck), rig.GetLocal(Riggle.Point.Pelvis)),
                0.46f * Vector3.Distance(rig.GetLocal(Riggle.Point.Neck), rig.GetLocal(Riggle.Point.Pelvis)));

            AddSide(b, rig, true, upperChest, pelvis, hipHalfWidth);
            AddSide(b, rig, false, upperChest, pelvis, hipHalfWidth);
            RiggleAppendageBuilder.AddSpecs(rig, b);
            if (rig.DistributeTwist)
            {
                foreach (var segment in b.FindAll(x => x.name.EndsWith("UpperArm") || x.name.EndsWith("LowerArm") || x.name.EndsWith("UpperLeg") || x.name.EndsWith("LowerLeg")))
                    for (int i = 1; i <= 3; i++)
                        b.Add(new BoneSpec(segment.name + "_Twist" + i, segment.name,
                            Vector3.Lerp(segment.position, segment.end, i / 3f), segment.end,
                            segment.region, HumanBodyBones.LastBone) { twistSource = segment.name });
            }
            return b;
        }

        private static void ConfigureTwists(Riggle rig, List<BoneSpec> specs, Dictionary<string, Transform> bones)
        {
            var distribution = rig.GetComponent<RiggleTwistDistribution>() ?? Undo.AddComponent<RiggleTwistDistribution>(rig.gameObject);
            Undo.RecordObject(distribution, "Bind twist distribution");
            var chains = new List<RiggleTwistDistribution.Chain>();
            foreach (var segment in specs.FindAll(s => s.twistSource == null && bones.ContainsKey(s.name + "_Twist1")))
            {
                var end = specs.Find(s => s.parent == segment.name && s.twistSource == null);
                if (end == null) continue;
                chains.Add(new RiggleTwistDistribution.Chain { start = segment.transform, end = end.transform,
                    axis = segment.transform.InverseTransformDirection(segment.end - segment.position).normalized,
                    restRelative = Quaternion.Inverse(segment.transform.rotation) * end.transform.rotation,
                    twists = new[] { bones[segment.name + "_Twist1"], bones[segment.name + "_Twist2"], bones[segment.name + "_Twist3"] } });
            }
            distribution.chains = chains.ToArray();
        }

        private static void AddSide(List<BoneSpec> b, Riggle rig, bool left,
            Vector3 upperChest, Vector3 pelvis, float hipHalfWidth)
        {
            string prefix = left ? "Riggle_Left" : "Riggle_Right";
            string armParent = "Riggle_UpperChest";
            string hipsParent = "Riggle_Hips";
            Vector3 shoulder = rig.GetWorld(left ? Riggle.Point.LeftShoulder : Riggle.Point.RightShoulder);
            Vector3 elbow = rig.GetWorld(left ? Riggle.Point.LeftElbow : Riggle.Point.RightElbow);
            Vector3 wrist = rig.GetWorld(left ? Riggle.Point.LeftWrist : Riggle.Point.RightWrist);
            Vector3 knee = rig.GetWorld(left ? Riggle.Point.LeftKnee : Riggle.Point.RightKnee);
            Vector3 ankle = rig.GetWorld(left ? Riggle.Point.LeftAnkle : Riggle.Point.RightAnkle);
            Vector3 clavicle = Vector3.Lerp(upperChest, shoulder, 0.44f);
            Vector3 wristEnd = wrist + (wrist - elbow).normalized * Vector3.Distance(elbow, wrist) * 0.35f;
            var pelvisLocal = rig.GetLocal(Riggle.Point.Pelvis);
            var kneeLocal = rig.GetLocal(left ? Riggle.Point.LeftKnee : Riggle.Point.RightKnee);
            Vector3 hipLocal = new Vector3(pelvisLocal.x + (left ? -hipHalfWidth : hipHalfWidth),
                pelvisLocal.y - Vector3.Distance(pelvisLocal, rig.GetLocal(Riggle.Point.Neck)) * 0.045f,
                Mathf.Lerp(pelvisLocal.z, kneeLocal.z, 0.18f));
            Vector3 hip = rig.transform.TransformPoint(hipLocal);
            Vector3 footEnd = ankle + rig.transform.TransformDirection(Vector3.forward) *
                              (Vector3.Distance(rig.GetWorld(Riggle.Point.HeadTop), ankle) * rig.FootLengthHeightRatio);

            b.Add(new BoneSpec(prefix + "Shoulder", armParent, clavicle, shoulder, Region.Arm,
                left ? HumanBodyBones.LeftShoulder : HumanBodyBones.RightShoulder));
            b.Add(new BoneSpec(prefix + "UpperArm", prefix + "Shoulder", shoulder, elbow, Region.Arm,
                left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm));
            b.Add(new BoneSpec(prefix + "LowerArm", prefix + "UpperArm", elbow, wrist, Region.Arm,
                left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm));
            b.Add(new BoneSpec(prefix + "Hand", prefix + "LowerArm", wrist, wristEnd, Region.Arm,
                left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand));
            b.Add(new BoneSpec(prefix + "HandEnd", prefix + "Hand", wristEnd, wristEnd, Region.Arm,
                HumanBodyBones.LastBone, false));

            b.Add(new BoneSpec(prefix + "UpperLeg", hipsParent, hip, knee, Region.Leg,
                left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg));
            b.Add(new BoneSpec(prefix + "LowerLeg", prefix + "UpperLeg", knee, ankle, Region.Leg,
                left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg));
            b.Add(new BoneSpec(prefix + "Foot", prefix + "LowerLeg", ankle, footEnd, Region.Leg,
                left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot));
            b.Add(new BoneSpec(prefix + "FootEnd", prefix + "Foot", footEnd, footEnd, Region.Leg,
                HumanBodyBones.LastBone, false));
        }

        private static void TryMakeHumanoid(Riggle rig, List<BoneSpec> bones, string folder, string baseName)
        {
            // Unity's humanoid builder imposes skeletal proportions and T-pose requirements.
            // The generic bone chain remains useful even when avatar validation fails.
            try
            {
                var human = new List<HumanBone>();
                foreach (var spec in bones)
                {
                    if (spec.human == HumanBodyBones.LastBone) continue;
                    human.Add(new HumanBone
                    {
                        boneName = spec.name,
                        humanName = HumanTrait.BoneName[(int)spec.human],
                        limit = new HumanLimit { useDefaultValues = true }
                    });
                }

                Avatar avatar = BuildHumanoidAvatar(rig.gameObject, rig.GeneratedBones, human.ToArray());
                if (!avatar || !avatar.isValid || !avatar.isHuman)
                {
                    if (avatar) UnityEngine.Object.DestroyImmediate(avatar);
                    Debug.LogWarning("Riggle: Unity rejected the Humanoid Avatar. The skeleton and skinning are still valid as a Generic rig. Check T-pose and proportions.", rig);
                    return;
                }

                avatar.name = baseName + "_RiggleHumanoid";
                string path = AssetDatabase.GenerateUniqueAssetPath(folder + "/" + avatar.name + ".asset");
                AssetDatabase.CreateAsset(avatar, path);
                Animator animator = rig.GetComponent<Animator>();
                if (!animator) animator = Undo.AddComponent<Animator>(rig.gameObject);
                Undo.RecordObject(animator, "Assign Riggle Humanoid Avatar");
                animator.avatar = avatar;
                rig.BakedAvatar = avatar;
                Debug.Log("Riggle: valid Humanoid Avatar built and assigned.", rig);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Riggle: Humanoid Avatar could not be built: " + e.Message, rig);
            }
        }

        // Avatar calibration needs a T-pose even when the source geometry/bind pose is an A-pose.
        // Only the temporary reference changes; meshes, bind poses and authored landmarks stay intact.
        internal static Avatar BuildHumanoidAvatar(GameObject root, Transform skeletonRoot, HumanBone[] human)
        {
            var transforms = skeletonRoot.GetComponentsInChildren<Transform>(true);
            var rotations = new Quaternion[transforms.Length];
            for (int i = 0; i < transforms.Length; i++) rotations[i] = transforms[i].localRotation;
            Vector3 rootPosition = root.transform.localPosition;
            Quaternion rootRotation = root.transform.localRotation;
            try
            {
                root.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                foreach (bool left in new[] { true, false })
                {
                    Transform upper = FindHumanBone(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm, human, transforms);
                    Transform lower = FindHumanBone(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm, human, transforms);
                    Transform hand = FindHumanBone(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand, human, transforms);
                    if (!upper || !lower || !hand) continue;
                    Vector3 direction = root.transform.TransformDirection(left ? Vector3.left : Vector3.right);
                    if ((lower.position - upper.position).sqrMagnitude > .000001f)
                        upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, direction) * upper.rotation;
                    if ((hand.position - lower.position).sqrMagnitude > .000001f)
                        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, direction) * lower.rotation;
                }
                var skeleton = new List<SkeletonBone>();
                AddSingleSkeletonBone(root.transform, skeleton);
                var referenceRoot = skeleton[0]; referenceRoot.position = Vector3.zero; referenceRoot.rotation = Quaternion.identity; skeleton[0] = referenceRoot;
                AddSkeleton(skeletonRoot, skeleton);
                return AvatarBuilder.BuildHumanAvatar(root, new HumanDescription
                {
                    human = human, skeleton = skeleton.ToArray(),
                    upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                    armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false
                });
            }
            finally
            {
                for (int i = 0; i < transforms.Length; i++) transforms[i].localRotation = rotations[i];
                root.transform.SetLocalPositionAndRotation(rootPosition, rootRotation);
            }
        }

        private static Transform FindHumanBone(HumanBodyBones bone, HumanBone[] human, Transform[] transforms)
        {
            string humanName = HumanTrait.BoneName[(int)bone];
            foreach (var entry in human)
                if (entry.humanName == humanName)
                    foreach (var transform in transforms) if (transform.name == entry.boneName) return transform;
            return null;
        }

        private static void AddSkeleton(Transform current, List<SkeletonBone> skeleton)
        {
            AddSingleSkeletonBone(current, skeleton);
            foreach (Transform child in current) AddSkeleton(child, skeleton);
        }

        private static void AddSingleSkeletonBone(Transform current, List<SkeletonBone> skeleton)
        {
            skeleton.Add(new SkeletonBone
            {
                name = current.name,
                position = current.localPosition,
                rotation = current.localRotation,
                scale = current.localScale
            });
        }

        private static string PrepareFolder(string folder)
        {
            folder = folder.TrimEnd('/').Replace('\\', '/');
            if (folder == "Assets") return folder;
            var pieces = folder.Split('/');
            string path = "Assets";
            for (int i = 1; i < pieces.Length; i++)
            {
                if (string.IsNullOrEmpty(pieces[i]) || pieces[i] == "." || pieces[i] == "..")
                    throw new ArgumentException("Riggle asset folder contains an invalid path segment.");
                string next = path + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, pieces[i]);
                path = next;
            }
            return path;
        }

        private static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        }
    }
}
