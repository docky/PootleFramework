using System.Collections.Generic;
using DavidJalbert.TinyCarController;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pootle.Editor
{
    public static class PootleCarSetup
    {
        public const string PrefabPath = "Assets/Pootle/Samples/Vehicles/Pootle Sedan.prefab";
        const string Folder = "Assets/Pootle/Samples/Vehicles";
        const string Source = "Assets/DavidJalbert/TinyCarController/Examples/Prefabs/Vehicles/Car Sedan.prefab";

        [MenuItem("Pootle/Add Driveable Sedan")]
        public static void ApplyMenu() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Pootle/Samples/PootlePlayground.unity")
                throw new System.InvalidOperationException("Open PootlePlayground before adding the sample Sedan.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Pootle/Samples", "Vehicles");
            var materials = new Dictionary<Material, Material>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (!source) throw new System.InvalidOperationException("Import Tiny Car Controller's Sedan prefab first.");
            var car = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(car, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            try
            {
                car.name = "Pootle Sedan";
                car.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                car.transform.localScale = Vector3.one * 1.6f;
                RemapMaterials(car, materials);
                var controller = car.GetComponent<TCCPlayer>();
                controller.motorMaxSpeed = 12;
                var body = car.GetComponentInChildren<TCCBody>();
                var endpoint = body.gameObject.AddComponent<PootleFrobject>(); endpoint.localPoint = new Vector3(0, .7f, 0);
                var indicator = body.gameObject.AddComponent<PootleFrobjectIndicator>();
                indicator.markerMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Frobject marker.mat");
                var rig = new GameObject("Sedan Chase Camera"); rig.transform.SetParent(car.transform, false);
                var camera = rig.AddComponent<CinemachineCamera>(); camera.Priority = 100; camera.Lens.FieldOfView = 60;
                rig.SetActive(false);
                var driving = car.AddComponent<PootleCar>(); driving.interaction = endpoint; driving.chaseCamera = camera;
                var effects = car.GetComponentInChildren<TCCBasicEffects>();
                if (effects)
                {
                    effects.skidMarkPrefab = ConvertEffect(effects.skidMarkPrefab, materials);
                    effects.smokePrefab = ConvertEffect(effects.smokePrefab, materials);
                    effects.hitPrefab = ConvertEffect(effects.hitPrefab, materials);
                    effects.boostPrefab = ConvertEffect(effects.boostPrefab, materials);
                    if (effects.audioEngine) effects.audioEngine.mute = true;
                }
                PrefabUtility.SaveAsPrefabAsset(car, PrefabPath);
            }
            finally { Object.DestroyImmediate(car); }

            var existing = Object.FindAnyObjectByType<PootleCar>();
            if (!existing)
            {
                var pickup = GameObject.Find("Car Pickup");
                // The old Pickup spot faces a carry cube; leave room for the longer, scaled Sedan.
                Vector3 position = new Vector3(8, 0, -5);
                Quaternion rotation = pickup ? pickup.transform.rotation : Quaternion.identity;
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                Undo.RegisterCreatedObjectUndo(instance, "Add driveable Sedan");
                instance.transform.SetPositionAndRotation(position, rotation);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                if (pickup) Undo.DestroyObjectImmediate(pickup);
                Selection.activeGameObject = instance;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "Sedan added at 1.6 scale with URP materials, Frobject entry, B exit and a chase camera.";
        }

        static T ConvertEffect<T>(T source, Dictionary<Material, Material> materials) where T : Component
        {
            if (!source) return null;
            var copy = Object.Instantiate(source.gameObject);
            try
            {
                copy.name = source.gameObject.name;
                RemapMaterials(copy, materials);
                return PrefabUtility.SaveAsPrefabAsset(copy, Folder + "/" + copy.name + ".prefab").GetComponent<T>();
            }
            finally { Object.DestroyImmediate(copy); }
        }

        static void RemapMaterials(GameObject root, Dictionary<Material, Material> materials)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    var source = slots[i]; if (!source) continue;
                    if (!materials.TryGetValue(source, out var material))
                    {
                        bool particle = renderer is ParticleSystemRenderer || renderer is TrailRenderer;
                        string path = Folder + "/" + source.name + " URP.mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!material)
                        {
                            material = new Material(Shader.Find(particle ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit"));
                            var texture = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                            Color color = source.HasProperty("_Color") ? source.GetColor("_Color") : source.HasProperty("_TintColor") ? source.GetColor("_TintColor") : Color.white;
                            material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", color);
                            if (!particle)
                            {
                                material.SetFloat("_Smoothness", source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : .25f);
                                if (source.HasProperty("_EmissionMap") && source.GetTexture("_EmissionMap"))
                                {
                                    material.SetTexture("_EmissionMap", source.GetTexture("_EmissionMap"));
                                    material.SetColor("_EmissionColor", source.GetColor("_EmissionColor")); material.EnableKeyword("_EMISSION");
                                }
                            }
                            else
                            {
                                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                                material.SetFloat("_Cull", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                                material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
                            }
                            AssetDatabase.CreateAsset(material, path);
                        }
                        materials.Add(source, material);
                    }
                    slots[i] = material;
                }
                renderer.sharedMaterials = slots;
            }
        }
    }
}
