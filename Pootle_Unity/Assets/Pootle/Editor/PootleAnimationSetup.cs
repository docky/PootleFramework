using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pootle.Editor
{
    public static class PootleAnimationSetup
    {
        private const string Path = "Assets/Pootle/Samples/Art/Lily/PootleLily.controller";
        public static void Configure(PootleCharacter character)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath("Assets/Pootle/Samples/Art/Lily/Lily.fbx").OfType<AnimationClip>().ToArray();
            AnimationClip Clip(string name) => clips.FirstOrDefault(c => c.name == "Lily|" + name);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);
            if (!controller)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(Path);
                controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
                controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
                controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
                controller.AddParameter("Mode", AnimatorControllerParameterType.Int);
                var machine = controller.layers[0].stateMachine;
                var ground = new BlendTree { name = "Idle Walk Run", blendParameter = "Speed", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(ground, controller);
                ground.AddChild(Clip("Idle"), 0); ground.AddChild(Clip("Walk"), 3.5f); ground.AddChild(Clip("Run"), 9);
                machine.defaultState = machine.AddState("Locomotion"); machine.defaultState.motion = ground;
                var climb = new BlendTree { name = "Climb Down Idle Up", blendParameter = "VerticalSpeed", useAutomaticThresholds = false };
                AssetDatabase.AddObjectToAsset(climb, controller);
                climb.AddChild(Clip("ClimbDown"), -3); climb.AddChild(Clip("ClimbIdle"), 0); climb.AddChild(Clip("ClimbUp"), 3);
                machine.AddState("Climb").motion = climb;
                machine.AddState("Jump").motion = Clip("Jump"); machine.AddState("Fall").motion = Clip("Fall");
                machine.AddState("SeatedSlide").motion = Clip("Crouch");
                machine.AddState("LedgeHang").motion = Clip("LedgeHangingIdle");
                machine.AddState("LedgeClimb").motion = Clip("LedgeClimbing");
                EditorUtility.SetDirty(controller);
            }
            foreach (var a in character.GetComponentsInChildren<Animator>(true))
            {
                a.runtimeAnimatorController = controller; a.applyRootMotion = false; a.enabled = true;
                EditorUtility.SetDirty(a); PrefabUtility.RecordPrefabInstancePropertyModifications(a);
            }
            // Older scene instances may have added overrides as well as the upgraded prefab components.
            foreach (var duplicate in character.GetComponents<PootleAvatar>().Skip(1)) Object.DestroyImmediate(duplicate);
            foreach (var duplicate in character.GetComponents<PootleSeatedPose>().Skip(1)) Object.DestroyImmediate(duplicate);
            var adapter = character.GetComponent<PootleAvatar>() ?? character.gameObject.AddComponent<PootleAvatar>();
            adapter.character = character; adapter.animator = character.GetComponentInChildren<Animator>();
            var seated = character.GetComponent<PootleSeatedPose>() ?? character.gameObject.AddComponent<PootleSeatedPose>();
            seated.character = character; seated.animator = adapter.animator;
            EditorUtility.SetDirty(adapter); EditorUtility.SetDirty(seated);
        }
    }
}
