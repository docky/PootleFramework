using System.Collections.Generic;
using DavidJalbert.TinyCarController;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pootle.Editor
{
    public static class PootlePickupSetup
    {
        public const string PickupPath = "Assets/Pootle/Samples/Vehicles/Pootle Pickup.prefab";
        const string Examples = "Assets/DavidJalbert/TinyCarController/Examples/";
        const string DemoRoot = "Tiny Car demo driving area";
        public const float DemoFloorOffset = .01f;

        [MenuItem("Pootle/Add Pickup Truck and Demo Ramps")]
        public static void ApplyMenu() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Pootle/Samples/PootlePlayground.unity")
                throw new System.InvalidOperationException("Open PootlePlayground first.");
            var materials = new Dictionary<Material, Material>();
            CreatePickup(materials);
            if (!GameObject.Find("Pootle Pickup"))
            {
                var truck = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PickupPath));
                Undo.RegisterCreatedObjectUndo(truck, "Add Pickup truck");
                truck.transform.SetPositionAndRotation(new Vector3(11, 0, -12), Quaternion.Euler(0, 180, 0));
                PrefabUtility.RecordPrefabInstancePropertyModifications(truck.transform);
                Selection.activeGameObject = truck;
            }
            var demo = GameObject.Find(DemoRoot);
            if (!demo) CreateDemoArea(materials);
            else
            {
                Undo.RecordObject(demo.transform, "Raise demo floor above playground");
                var position = demo.transform.position; position.y = DemoFloorOffset; demo.transform.position = position;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "Added 1.7x Pickup at (11, 0, -12), asphalt driving area, demo ramp, quarter pipe and mound.";
        }

        static void CreatePickup(Dictionary<Material, Material> materials)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Examples + "Prefabs/Vehicles/Car Pickup.prefab");
            var truck = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(truck, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            try
            {
                truck.name = "Pootle Pickup";
                truck.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                truck.transform.localScale = Vector3.one * 1.7f;
                PootleCarSetup.RemapMaterials(truck, materials);
                truck.GetComponent<TCCPlayer>().motorMaxSpeed = 12;
                var body = truck.GetComponentInChildren<TCCBody>();
                var endpoint = body.gameObject.AddComponent<PootleFrobject>();
                endpoint.localPoint = new Vector3(0, .75f, 0);
                var marker = body.gameObject.AddComponent<PootleFrobjectIndicator>();
                marker.markerMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Frobject marker.mat");
                var rig = new GameObject("Pickup Chase Camera"); rig.transform.SetParent(truck.transform, false);
                var camera = rig.AddComponent<CinemachineCamera>(); camera.Priority = 100; camera.Lens.FieldOfView = 60;
                rig.SetActive(false);
                var driving = truck.AddComponent<PootleCar>(); driving.interaction = endpoint; driving.chaseCamera = camera;
                var effects = truck.GetComponentInChildren<TCCBasicEffects>();
                if (effects)
                {
                    effects.skidMarkPrefab = PootleCarSetup.ConvertEffect(effects.skidMarkPrefab, materials);
                    effects.smokePrefab = PootleCarSetup.ConvertEffect(effects.smokePrefab, materials);
                    effects.hitPrefab = PootleCarSetup.ConvertEffect(effects.hitPrefab, materials);
                    effects.boostPrefab = PootleCarSetup.ConvertEffect(effects.boostPrefab, materials);
                    if (effects.audioEngine) effects.audioEngine.mute = true;
                }
                PrefabUtility.SaveAsPrefabAsset(truck, PickupPath);
            }
            finally { Object.DestroyImmediate(truck); }
        }

        static void CreateDemoArea(Dictionary<Material, Material> materials)
        {
            var root = new GameObject(DemoRoot); Undo.RegisterCreatedObjectUndo(root, "Add Tiny Car demo area");
            var asphalt = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Examples + "Prefabs/Environment/Ground Asphalt.prefab"), root.transform);
            PrefabUtility.UnpackPrefabInstance(asphalt, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            asphalt.name = "Demo asphalt · south driving apron";
            asphalt.transform.SetPositionAndRotation(new Vector3(0, -.6f, -55), Quaternion.identity);
            asphalt.transform.localScale = new Vector3(1.48f, .6f, 1.72f);
            PootleCarSetup.RemapMaterials(asphalt, materials);
            AddModel(root.transform, "Demo race ramp", "race track ramp", new Vector3(-12, 0, -37), 90, 1, materials);
            AddModel(root.transform, "Demo quarter pipe", "Quarter Pipe", new Vector3(15, 0, -72), 90, 5, materials);
            AddModel(root.transform, "Demo mound", "Mound", new Vector3(-12, 0, -72), 0, 6, materials);
            root.transform.position = Vector3.up * DemoFloorOffset;
        }

        static void AddModel(Transform parent, string name, string file, Vector3 at, float yaw, float modelScale, Dictionary<Material, Material> materials)
        {
            // Preserve the demo model's useful dimensions beneath the requested 1.7x station scale.
            var station = new GameObject(name); station.transform.SetParent(parent, false); station.transform.localScale = Vector3.one * 1.7f;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Examples + "Models/Meshes/Environment/" + file + ".fbx"), station.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.localScale = Vector3.one * modelScale;
            station.transform.rotation = Quaternion.Euler(0, yaw, 0);
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
                if (!filter.GetComponent<Collider>()) filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
            // Use the demo's platform material consistently, including its untextured mound.
            var platforms = AssetDatabase.LoadAssetAtPath<Material>(Examples + "Materials/Platforms.mat");
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++) slots[i] = platforms;
                renderer.sharedMaterials = slots;
            }
            PootleCarSetup.RemapMaterials(model, materials);
            var renderers = model.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            station.transform.position += new Vector3(at.x - bounds.center.x, at.y - bounds.min.y, at.z - bounds.center.z);
        }
    }
}
