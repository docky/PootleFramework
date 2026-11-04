using System.IO;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Pootle.Editor
{
    public static class PootleSceneSetup
    {
        public const string Root = "Assets/Pootle";
        public const string SampleScene = Root + "/Samples/PootlePlayground.unity";
        public const string SamplePrefab = Root + "/Samples/PootleCharacter.prefab";

        public static PootleProfile DefaultProfile()
        {
            string path = Root + "/Data/PootleDefault.asset";
            var p = AssetDatabase.LoadAssetAtPath<PootleProfile>(path);
            if (p) return p;
            p = ScriptableObject.CreateInstance<PootleProfile>();
            AssetDatabase.CreateAsset(p, path);
            var fast = ScriptableObject.CreateInstance<PootleProfile>();
            fast.walkSpeed = 16; fast.groundAcceleration = 12; fast.releaseDeceleration = 9;
            fast.airAcceleration = 18; fast.brakeDeceleration = 40; fast.jumpLaunchSpeed = 20;
            fast.runSpeed = 16; fast.applySlopeSpeed = true; fast.downhillForce = 35;
            fast.allowRun = false; fast.decelerateExcessSpeed = false;
            AssetDatabase.CreateAsset(fast, Root + "/Data/PootleFastReference.asset");
            AssetDatabase.SaveAssets();
            return p;
        }

        public static PootleCharacter CreateCharacter(GameObject visualAsset = null)
        {
            var root = new GameObject("Pootle · Lily");
            Undo.RegisterCreatedObjectUndo(root, "Create Pootle character");
            var motor = Undo.AddComponent<PootleMotor>(root);
            motor.Configure();
            var input = Undo.AddComponent<PootleInput>(root);
            input.actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/Data/PootleControls.inputactions");
            var character = Undo.AddComponent<PootleCharacter>(root);
            character.input = input;
            character.profile = DefaultProfile();
            var visual = new GameObject("Visual");
            Undo.RegisterCreatedObjectUndo(visual, "Create Pootle visual");
            visual.transform.SetParent(root.transform, false);
            character.visualRoot = visual.transform;
            if (!visualAsset) visualAsset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Samples/Art/Lily/Lily.fbx");
            if (visualAsset)
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(visualAsset);
                Undo.RegisterCreatedObjectUndo(model, "Add Lily visual");
                model.transform.SetParent(visual.transform, false);
                foreach (var animator in model.GetComponentsInChildren<Animator>())
                {
                    animator.applyRootMotion = false;
                    animator.runtimeAnimatorController = null;
                    animator.enabled = false;
                }
                AlignModel(model.transform, 2f);
            }
            var target = new GameObject("Camera Target");
            Undo.RegisterCreatedObjectUndo(target, "Create Pootle camera target");
            target.transform.SetParent(root.transform, false);
            target.transform.localPosition = Vector3.up * 1.25f;
            character.cameraTarget = target.transform;
            PootleAnimationSetup.Configure(character);
            PootleNextFeaturesSetup.ConfigureCharacter(character);
            EditorUtility.SetDirty(character);
            return character;
        }

        public static void AlignModel(Transform model, float height)
        {
            if (!TryBounds(model, out var bounds)) return;
            Undo.RecordObject(model, "Align Pootle model");
            if (bounds.size.y > 0.001f) model.localScale *= height / bounds.size.y;
            if (TryBounds(model, out bounds))
                model.position += new Vector3(model.parent.position.x - bounds.center.x,
                    model.parent.position.y - bounds.min.y, model.parent.position.z - bounds.center.z);
            PrefabUtility.RecordPrefabInstancePropertyModifications(model);
        }

        public static bool TryBounds(Transform visual, out Bounds bounds)
        {
            bounds = default;
            if (!visual) return false;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            bool any = false;
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        public static void FitCapsule(PootleCharacter character)
        {
            if (!character || !TryBounds(character.visualRoot, out var bounds)) return;
            var motor = character.GetComponent<PootleMotor>();
            Undo.RecordObjects(new Object[] { motor, motor.GetComponent<CapsuleCollider>() }, "Fit Pootle capsule");
            Vector3 min = bounds.min - character.transform.position;
            Vector3 max = bounds.max - character.transform.position;
            motor.height = max.y - min.y;
            motor.radius = Mathf.Max(0.05f, Mathf.Min(motor.height * 0.5f, Mathf.Max(bounds.extents.x, bounds.extents.z)));
            motor.center = (min + max) * 0.5f;
            motor.Configure();
            EditorUtility.SetDirty(motor);
            PrefabUtility.RecordPrefabInstancePropertyModifications(motor);
            EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
        }

        public static Camera ConnectCamera(PootleCharacter character)
        {
            if (!character) return null;
            var output = character.outputCamera ? character.outputCamera.GetComponent<Camera>() : Camera.main;
            if (output && output.gameObject.scene != character.gameObject.scene) output = null;
            if (!output)
            {
                var go = new GameObject("Main Camera");
                Undo.RegisterCreatedObjectUndo(go, "Create Pootle output camera");
                output = Undo.AddComponent<Camera>(go);
                go.tag = "MainCamera";
                Undo.AddComponent<AudioListener>(go);
            }
            if (!output.GetComponent<CinemachineBrain>()) Undo.AddComponent<CinemachineBrain>(output.gameObject);
            Undo.RecordObject(character, "Connect Pootle camera");
            character.outputCamera = output.transform;
            if (!character.cameraTarget)
            {
                var target = new GameObject("Camera Target");
                Undo.RegisterCreatedObjectUndo(target, "Create Pootle target");
                target.transform.SetParent(character.transform, false);
                target.transform.localPosition = Vector3.up * 1.25f;
                character.cameraTarget = target.transform;
            }
            PootleOrbitInput adapter = null;
            foreach (var a in Object.FindObjectsByType<PootleOrbitInput>())
                if (a.character == character) { adapter = a; break; }
            if (!adapter)
            {
                var rig = new GameObject("Pootle Orbit Camera");
                Undo.RegisterCreatedObjectUndo(rig, "Create Pootle Cinemachine rig");
                var cm = Undo.AddComponent<CinemachineCamera>(rig);
                cm.Target = new CameraTarget { TrackingTarget = character.cameraTarget, LookAtTarget = character.cameraTarget, CustomLookAtTarget = true };
                cm.Lens.FieldOfView = 55;
                cm.Lens.NearClipPlane = 0.1f;
                var orbit = Undo.AddComponent<CinemachineOrbitalFollow>(rig);
                orbit.Radius = 8;
                orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
                orbit.TrackerSettings.PositionDamping = new Vector3(0.15f, 0.3f, 0.15f);
                orbit.HorizontalAxis.Value = 0;
                orbit.HorizontalAxis.Recentering.Enabled = false;
                orbit.VerticalAxis.Range = new Vector2(-15, 70);
                orbit.VerticalAxis.Value = orbit.VerticalAxis.Center = 20;
                orbit.VerticalAxis.Recentering.Enabled = false;
                orbit.RadialAxis.Range = Vector2.one;
                orbit.RadialAxis.Value = 1;
                var aim = Undo.AddComponent<CinemachineRotationComposer>(rig);
                aim.Damping = new Vector2(0.15f, 0.25f);
                var obstruction = Undo.AddComponent<CinemachineDeoccluder>(rig);
                obstruction.CollideAgainst = character.GetComponent<PootleMotor>().collisionMask;
                obstruction.AvoidObstacles = new CinemachineDeoccluder.ObstacleAvoidance
                { MaximumEffort = 4, Damping = 0.3f, DampingWhenOccluded = 0.1f, SmoothingTime = 0.1f };
                obstruction.AvoidObstacles.Enabled = true;
                obstruction.AvoidObstacles.CameraRadius = 0.25f;
                var driver = Undo.AddComponent<CinemachineInputAxisController>(rig);
                driver.AutoEnableInputs = false;
                adapter = Undo.AddComponent<PootleOrbitInput>(rig);
                adapter.character = character;
                adapter.actions = character.input ? character.input.actions : null;
            }
            PootleNextFeaturesSetup.ConfigureCamera(adapter);
            EditorUtility.SetDirty(character);
            PrefabUtility.RecordPrefabInstancePropertyModifications(character);
            EditorSceneManager.MarkSceneDirty(character.gameObject.scene);
            return output;
        }

        [MenuItem("Pootle/Open Character Playground")]
        public static void OpenSample()
        {
            if (File.Exists(SampleScene))
            {
                var existing = SceneManager.GetSceneByPath(SampleScene);
                if (existing.isLoaded) SceneManager.SetActiveScene(existing);
                else SceneManager.SetActiveScene(EditorSceneManager.OpenScene(SampleScene, OpenSceneMode.Additive));
                return;
            }
            CreateSample();
        }

        public static void CreateSample()
        {
            if (File.Exists(SampleScene)) { OpenSample(); return; }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var floor = Material("Floor", "52656D");
            var trim = Material("Grid", "617880");
            var steps = Material("Steps", "C5D3C8");
            var slope = Material("Walkable Slopes", "8DAF9D");
            var steep = Material("Steep Slopes", "C08367");
            var accent = Material("Accent", "EFCB78");
            var wall = Material("Walls", "718792");
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.58f, 0.66f, 0.73f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(0.46f, 0.57f, 0.63f);
            RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.008f;
            var sun = new GameObject("Soft afternoon key");
            var light = sun.AddComponent<Light>(); light.type = LightType.Directional;
            light.intensity = 2.2f; light.color = new Color(1, 0.94f, 0.83f); light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            Box("Court", new Vector3(0, -0.3f, 9), new Vector3(42, 0.6f, 44), floor);
            for (int x = -20; x <= 20; x += 2) Box("2m grid", new Vector3(x, 0.004f, 9), new Vector3(0.018f, 0.008f, 44), trim, false);
            for (int z = -12; z <= 30; z += 2) Box("2m grid", new Vector3(0, 0.004f, z), new Vector3(42, 0.008f, 0.018f), trim, false);
            for (int i = 0; i < 7; i++)
            {
                float h = (i + 1) * 0.15f;
                Box("Steps · 0.15 m riser", new Vector3(-10, h * 0.5f, 5 + i * 0.9f), new Vector3(4, h, 0.9f), steps);
            }
            for (int i = 0; i < 3; i++)
            {
                float angle = i == 0 ? 25 : i == 1 ? 44 : 60;
                float length = 7;
                var ramp = Box("Slope · " + angle + " degrees", new Vector3(-4 + i * 5, Mathf.Sin(angle * Mathf.Deg2Rad) * length * 0.5f - 0.15f, 15), new Vector3(3.5f, 0.3f, length), i == 2 ? steep : slope);
                ramp.transform.rotation = Quaternion.Euler(-angle, 0, 0);
                Label(angle + "°", new Vector3(-4 + i * 5, 0.04f, 10), 0.45f, Quaternion.Euler(90, 0, 0));
            }
            Box("Jump deck · 1m", new Vector3(10, 0.5f, 4), new Vector3(4, 1, 4), steps);
            Box("Jump deck · 2m", new Vector3(15, 1, 9), new Vector3(4, 2, 4), steps);
            Box("Jump deck · 3m", new Vector3(12, 1.5f, 15), new Vector3(4, 3, 4), accent);
            Box("Ceiling test", new Vector3(-13, 2.7f, 18), new Vector3(7, 0.4f, 4), wall);
            Box("Ceiling support", new Vector3(-16, 1.3f, 18), new Vector3(0.4f, 2.6f, 4), wall);
            Box("Wall glide", new Vector3(18, 2, -2), new Vector3(0.5f, 4, 12), wall);
            Box("Corner A", new Vector3(-14, 1.5f, -5), new Vector3(8, 3, 0.5f), wall);
            Box("Corner B", new Vector3(-18, 1.5f, -2), new Vector3(0.5f, 3, 6), wall);
            Box("Edge island", new Vector3(0, 1, 26), new Vector3(6, 2, 4), steps);
            Label("POOTLE", new Vector3(-5, 4, 23), 1.3f, Quaternion.identity);
            Label("CHARACTER PLAYGROUND  /  v0.1", new Vector3(-5, 2.9f, 23), 0.25f, Quaternion.identity);
            Label("LEFT STICK  Move    RIGHT STICK  Orbit\nA / CROSS  Jump    LB / L1  Run    R3  Recenter\nWASD  Move    RMB + MOUSE / ARROWS  Orbit\nSPACE  Jump    SHIFT  Run    LT / E  Grip    C  Recenter    R  Reset", new Vector3(-5.5f, 0.05f, -2), 0.24f, Quaternion.Euler(90, 0, 0));
            var character = CreateCharacter();
            character.transform.position = new Vector3(0, 0.03f, -4);
            // Scene-only camera references are assigned after saving the reusable character prefab.
            PrefabUtility.SaveAsPrefabAssetAndConnect(character.gameObject, SamplePrefab, InteractionMode.AutomatedAction);
            var camera = ConnectCamera(character);
            camera.backgroundColor = RenderSettings.fogColor;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.SetPositionAndRotation(new Vector3(0, 4, -12), Quaternion.Euler(20, 0, 0));
            EditorSceneManager.SaveScene(scene, SampleScene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = character.gameObject;
            if (SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(0, 1, 3), Quaternion.Euler(35, 0, 0), 22);
        }

        [MenuItem("Pootle/Upgrade Playground Traversal")]
        public static void UpgradePlayground()
        {
            // Apply presentation and traversal to the reusable prefab without scene camera references.
            var prefab = PrefabUtility.LoadPrefabContents(SamplePrefab);
            var prefabCharacter = prefab.GetComponent<PootleCharacter>();
            if (!prefab.GetComponent<PootleTraversal>()) prefab.AddComponent<PootleTraversal>();
            PootleAnimationSetup.Configure(prefabCharacter);
            PrefabUtility.SaveAsPrefabAsset(prefab, SamplePrefab); PrefabUtility.UnloadPrefabContents(prefab);
            var scene = EditorSceneManager.OpenScene(SampleScene, OpenSceneMode.Single);
            var character = Object.FindAnyObjectByType<PootleCharacter>();
            if (!character.GetComponent<PootleTraversal>()) character.gameObject.AddComponent<PootleTraversal>();
            PootleAnimationSetup.Configure(character);
            foreach (var cameraInput in Object.FindObjectsByType<PootleOrbitInput>())
            { cameraInput.invertY = true; EditorUtility.SetDirty(cameraInput); }
            foreach (var label in Object.FindObjectsByType<TextMesh>())
                if (label.text.Contains("LEFT STICK"))
                    label.text = "LEFT STICK  Move    RIGHT STICK  Orbit\nA / CROSS  Jump    LB / L1  Run    LT / L2  Grip\nWASD  Move    RMB + MOUSE / ARROWS  Orbit\nSPACE  Jump    SHIFT  Run    E  Grip    C  Recenter    R  Reset";
            if (!GameObject.Find("Traversal course"))
            {
                var group = new GameObject("Traversal course");
                var wallMat = Material("Climbable", "73A8BD");
                var poleMat = Material("Grip Poles", "EFCB78");
                var wall = Box("Climb wall · hold LT or E", new Vector3(-11, 2.5f, 26), new Vector3(5, 5, 2), wallMat);
                wall.AddComponent<PootleClimbSurface>(); wall.transform.SetParent(group.transform);
                var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cylinder.name = "Climb cylinder · move sideways around surface";
                cylinder.transform.position = new Vector3(-17, 2.5f, 25); cylinder.transform.localScale = new Vector3(3, 2.5f, 3);
                cylinder.GetComponent<Renderer>().sharedMaterial = wallMat;
                cylinder.AddComponent<PootleClimbSurface>(); cylinder.transform.SetParent(group.transform);
                // Mesh collision preserves the curved side and a flat top instead of the default capsule cap.
                Object.DestroyImmediate(cylinder.GetComponent<Collider>());
                var mesh = cylinder.AddComponent<MeshCollider>(); mesh.sharedMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
                var ledge = Box("Ledge catch + auto hop", new Vector3(-10, 1.25f, -9), new Vector3(6, 2.5f, 3), wallMat);
                ledge.transform.SetParent(group.transform);
                for (int i = 0; i < 2; i++)
                {
                    var pole = new GameObject(i == 0 ? "Climb pole · facing-directed jump" : "Climb rope · facing-directed jump");
                    pole.transform.position = new Vector3(6 + i * 4, 3, -7);
                    var grip = pole.AddComponent<PootlePole>(); grip.length = 6; grip.radius = i == 0 ? 0.15f : 0.07f;
                    var trigger = pole.AddComponent<CapsuleCollider>(); trigger.radius = grip.radius; trigger.height = grip.length; trigger.isTrigger = true;
                    var art = GameObject.CreatePrimitive(PrimitiveType.Cylinder); art.name = "Visual";
                    art.transform.SetParent(pole.transform, false); art.transform.localScale = new Vector3(grip.radius * 2, 3, grip.radius * 2);
                    art.GetComponent<Renderer>().sharedMaterial = poleMat; Object.DestroyImmediate(art.GetComponent<Collider>());
                    pole.transform.SetParent(group.transform);
                }
                Label("BLUE  Hold LT / E to climb\nMove up/down and around cylinders\nYELLOW  Grip poles / ropes; turn with left stick\nJump follows facing; release grip to drop", new Vector3(1, 0.05f, -10), 0.22f, Quaternion.Euler(90, 0, 0));
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(character);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static Material Material(string name, string hex)
        {
            string path = Root + "/Samples/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name, color = color };
            material.SetFloat("_Smoothness", 0.16f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
        private static GameObject Box(string name, Vector3 position, Vector3 scale, Material material, bool solid = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        private static void Label(string text, Vector3 position, float size, Quaternion rotation)
        {
            var go = new GameObject(text.Split('\n')[0]);
            go.transform.SetPositionAndRotation(position, rotation);
            var label = go.AddComponent<TextMesh>();
            label.text = text; label.fontSize = 64; label.characterSize = size * 0.1f;
            label.color = new Color(0.95f, 0.91f, 0.78f);
            label.anchor = TextAnchor.MiddleLeft;
        }
    }
}
