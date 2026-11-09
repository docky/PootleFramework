using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    internal static class RiggleTestSceneSetup
    {
        [MenuItem("Tools/Riggle/Set up test scene bipeds")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/riggle test.unity")
                throw new System.InvalidOperationException("Open riggle test in Edit Mode first.");
            var bean = Object.FindObjectsByType<Riggle>().First(r => r.name == "Riggle Demo - Bean Biped");
            foreach (var source in bean.CollectSourceMeshes())
            {
                var binding = source.GetComponent<RiggleSkinBinding>() ?? Undo.AddComponent<RiggleSkinBinding>(source.gameObject);
                Undo.RecordObject(binding, "Bind bean body part");
                string part = source.name.Replace(" ", "");
                binding.wholeMesh = source.name == "Bean Torso" ? RiggleSkinBinding.Part.Torso :
                    source.name == "Oversized Head" ? RiggleSkinBinding.Part.Head :
                    (RiggleSkinBinding.Part)System.Enum.Parse(typeof(RiggleSkinBinding.Part), part);
                EditorUtility.SetDirty(binding);
            }
            ConfigureAndBake(bean, "Bean");

            var dogObject = GameObject.Find("model_doggy_avatar_sketch");
            var dog = dogObject.GetComponent<Riggle>() ?? Undo.AddComponent<Riggle>(dogObject);
            Undo.RecordObject(dog, "Fit dog biped landmarks");
            dog.MirrorRight = true;
            dog.SetLocal(Riggle.Point.Pelvis, new Vector3(0, .56f, -.03f));
            dog.SetLocal(Riggle.Point.Neck, new Vector3(0, 1.01f, -.04f));
            dog.SetLocal(Riggle.Point.HeadTop, new Vector3(0, 1.748f, .005f));
            dog.SetLocal(Riggle.Point.LeftShoulder, new Vector3(-.25f, .85f, -.115f));
            dog.SetLocal(Riggle.Point.LeftElbow, new Vector3(-.47f, .83f, -.115f));
            dog.SetLocal(Riggle.Point.LeftWrist, new Vector3(-.69f, .833f, -.09f));
            dog.SetLocal(Riggle.Point.LeftKnee, new Vector3(-.199f, .32f, -.015f));
            dog.SetLocal(Riggle.Point.LeftAnkle, new Vector3(-.208f, .12f, 0));
            // The imported mesh contains 16 disconnected anatomical/accessory islands.
            var sourceMesh = dogObject.GetComponent<MeshFilter>().sharedMesh;
            var topology = new RiggleTopology(sourceMesh);
            var vertices = sourceMesh.vertices;
            var dogBinding = dogObject.GetComponent<RiggleSkinBinding>() ?? Undo.AddComponent<RiggleSkinBinding>(dogObject);
            Undo.RecordObject(dogBinding, "Bind dog mesh islands");
            dogBinding.islands = Enumerable.Range(0, vertices.Length).GroupBy(i => topology.islands[i]).Select(g =>
            {
                var bounds = new Bounds(vertices[g.First()], Vector3.zero);
                foreach (int i in g) bounds.Encapsulate(vertices[i]);
                var c = bounds.center;
                bool left = c.x < 0;
                RiggleSkinBinding.Part part;
                if (c.y > 1.2f) part = RiggleSkinBinding.Part.Head; // skull and both ears
                else if (Mathf.Abs(c.x) > .6f) part = left ? RiggleSkinBinding.Part.LeftHand : RiggleSkinBinding.Part.RightHand;
                else if (Mathf.Abs(c.x) > .4f) part = left ? RiggleSkinBinding.Part.LeftArm : RiggleSkinBinding.Part.RightArm;
                else if (c.y < .18f) part = left ? RiggleSkinBinding.Part.LeftFoot : RiggleSkinBinding.Part.RightFoot;
                else if (c.y < .4f) part = left ? RiggleSkinBinding.Part.LeftLeg : RiggleSkinBinding.Part.RightLeg;
                else if (c.z < -.55f) part = RiggleSkinBinding.Part.Hips; // tail follows pelvis
                else part = RiggleSkinBinding.Part.Torso; // shirt, collar and backpack
                return new RiggleSkinBinding.Island { seedVertex = g.First(), part = part };
            }).ToArray();
            EditorUtility.SetDirty(dogBinding);
            ConfigureAndBake(dog, "Dog");
            EditorSceneManager.MarkSceneDirty(dog.gameObject.scene);
            Selection.objects = new Object[] { bean.gameObject, dog.gameObject };
            SceneView.lastActiveSceneView?.FrameSelected();
        }

        static void ConfigureAndBake(Riggle rig, string folder)
        {
            var serialized = new SerializedObject(rig);
            serialized.FindProperty("tryHumanoid").boolValue = true;
            serialized.FindProperty("weighting").enumValueIndex = (int)Riggle.WeightingMethod.ContiguousSurface;
            serialized.FindProperty("assetFolder").stringValue = "Assets/_EXPERIMENTAL/Riggle/Demo/Generated/" + folder;
            serialized.ApplyModifiedProperties();
            RiggleBuilder.Bake(rig);
            RiggleDiagnostics.Validate(rig);
            if (!rig.BakedAvatar || !rig.BakedAvatar.isValid || !rig.BakedAvatar.isHuman)
                throw new System.InvalidOperationException("Humanoid setup failed: " + rig.name);
            if (!rig.GetComponent<RiggleHumanoidPreview>()) Undo.AddComponent<RiggleHumanoidPreview>(rig.gameObject);
            var preview = rig.GetComponent<RiggleHumanoidPreview>();
            Undo.RecordObject(preview, "Give pose ownership to control rig");
            preview.enabled = false;
            ControlRig.ControlRigBuilder.Build(rig.GetComponent<Animator>());
        }
    }
}
