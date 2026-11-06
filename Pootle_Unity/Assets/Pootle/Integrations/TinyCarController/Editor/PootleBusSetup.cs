using System;
using System.Collections.Generic;
using DavidJalbert.TinyCarController;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pootle.Editor
{
    public static class PootleBusSetup
    {
        public const string Folder = "Assets/Pootle/Samples/Vehicles/Buses";
        public const string PrefabPath = Folder + "/Pootle Double Decker.prefab";
        const float RoofHeight = 5.45f, UpperHeight = 3.1f;
        static readonly Color Red = new Color(.82f, .085f, .11f), Cream = new Color(1, .88f, .64f);
        static readonly Color Glass = new Color(.075f, .19f, .27f), Dark = new Color(.07f, .09f, .12f);
        static readonly Color Metal = new Color(.55f, .64f, .68f), Gold = new Color(1, .67f, .12f);

        [MenuItem("Pootle/Add Four Double Decker Platform Buses")]
        public static void ApplyMenu() => Apply();

        public static string Apply()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Pootle/Samples/PootlePlayground.unity") throw new InvalidOperationException("Open PootlePlayground first.");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Pootle/Samples/Vehicles", "Buses");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Bus vertex colors.mat");
            if (!material)
            {
                var shader = Shader.Find("Pootle/Vertex Color Lit");
                if (!shader) throw new InvalidOperationException("Import the vertex-color shader first.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, Folder + "/Bus vertex colors.mat");
            }
            BuildBus(material);
            if (!GameObject.Find("Double decker bus stops"))
            {
                var fleet = new GameObject("Double decker bus stops"); Undo.RegisterCreatedObjectUndo(fleet, "Add four platform buses");
                var positions = new[] { new Vector3(-27, 0, -23), new Vector3(-31, 0, 30), new Vector3(31, 0, 31), new Vector3(88, 0, 16) };
                var yaw = new[] { 90f, 90f, -90f, 0f };
                var names = new[] { "South Stunts", "West Workshop", "East Meadow", "Spiral Lookout" };
                for (int i = 0; i < positions.Length; i++)
                {
                    var bus = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), fleet.transform);
                    bus.name = "Pootle Bus " + (i + 1) + " · " + names[i];
                    bus.transform.SetPositionAndRotation(positions[i], Quaternion.Euler(0, yaw[i], 0));
                    var platform = bus.GetComponent<PootleBusPlatform>(); platform.fleetNumber = i + 1;
                    var stop = new GameObject("High landing " + (i + 1) + " · " + names[i]);
                    stop.transform.SetParent(fleet.transform, false); stop.transform.SetPositionAndRotation(positions[i], bus.transform.rotation);
                    using (var shapes = new Shapes(stop.transform, material))
                    {
                        shapes.Box("High landing", new Vector3(0, 6.35f, 7.2f), new Vector3(3.6f, .2f, 3), Cream);
                        foreach (float x in new[] { -1.5f, 1.5f })
                            foreach (float z in new[] { 6f, 8.4f }) shapes.Box("Landing support", new Vector3(x, 3.125f, z), new Vector3(.16f, 6.25f, .16f), Metal);
                        shapes.Box("Landing back rail", new Vector3(0, 6.85f, 8.65f), new Vector3(3.6f, .65f, .08f), Red);
                        shapes.Finish("Bus stop mesh");
                    }
                    var landing = new GameObject("Landing surface waypoint"); landing.transform.SetParent(stop.transform, false); landing.transform.localPosition = new Vector3(0, 6.45f, 6.3f);
                    platform.destinationLanding = landing.transform;
                    EnsureGround(stop.transform, positions[i], material, i);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(bus.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(platform);
                    EditorUtility.SetDirty(bus); EditorUtility.SetDirty(platform);
                }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            return "Four procedural double-deckers with vertex colors, parked roof access stairs, and 6.45m destination landings.";
        }

        static void BuildBus(Material material)
        {
            var root = new GameObject("Pootle Double Decker");
            try
            {
                var bodyObject = new GameObject("Bus body"); bodyObject.transform.SetParent(root.transform, false);
                var body = bodyObject.AddComponent<TCCBody>(); body.bodyMass = 45; body.centerOfMass = new Vector3(0, .65f, 0);
                var visual = new GameObject("Primitive body visuals"); visual.transform.SetParent(bodyObject.transform, false); body.carModel = visual.transform;
                using (var shapes = new Shapes(bodyObject.transform, material, visual))
                {
                    shapes.Box("Lower deck", new Vector3(0, .66f, 0), new Vector3(3.2f, .16f, 9), Cream);
                    shapes.Box("Upper deck", new Vector3(0, UpperHeight - .08f, 0), new Vector3(3.2f, .16f, 9), Cream);
                    shapes.Box("Walkable roof", new Vector3(0, RoofHeight - .08f, 0), new Vector3(3.2f, .16f, 9), Red);
                    shapes.Box("Chassis", new Vector3(0, .5f, 0), new Vector3(2.65f, .35f, 8.4f), Dark);
                    foreach (float x in new[] { -1.55f, 1.55f })
                    {
                        shapes.Box("Lower red skirt", new Vector3(x, 1.04f, x < 0 ? -.75f : 0), new Vector3(.1f, .6f, x < 0 ? 7.3f : 8.8f), Red);
                        shapes.Box("Cream waist stripe", new Vector3(x, UpperHeight - .2f, 0), new Vector3(.12f, .18f, 8.8f), Cream);
                        if (x < 0) shapes.Box("Upper red waist", new Vector3(x, UpperHeight + .25f, 0), new Vector3(.1f, .5f, 8.8f), Red);
                        else
                        {
                            shapes.Box("Upper red waist rear", new Vector3(x, UpperHeight + .25f, -2.5f), new Vector3(.1f, .5f, 3.8f), Red);
                            shapes.Box("Upper red waist front", new Vector3(x, UpperHeight + .25f, 3), new Vector3(.1f, .5f, 2.8f), Red);
                        }
                        for (int i = 0; i < 6; i++)
                        {
                            float z = -3.65f + i * 1.45f;
                            bool lowerDoor = x < 0 && z > 2.5f;
                            bool upperDoor = x > 0 && z > -.1f && z < 1.5f;
                            if (!lowerDoor) shapes.Box("Lower window", new Vector3(x, 1.93f, z), new Vector3(.11f, 1.1f, 1.3f), Glass);
                            if (!upperDoor) shapes.Box("Upper window", new Vector3(x, 4.25f, z), new Vector3(.11f, 1.25f, 1.3f), Glass);
                            if (!lowerDoor) shapes.Box("Lower window pillar", new Vector3(x, 2.02f, z + .7f), new Vector3(.15f, 1.4f, .1f), Red);
                            if (!upperDoor) shapes.Box("Upper window pillar", new Vector3(x, 4.28f, z + .7f), new Vector3(.15f, 1.7f, .1f), Red);
                        }
                        shapes.Box("Roof side rail", new Vector3(x, RoofHeight + .36f, .65f), new Vector3(.08f, .65f, 7.2f), Cream);
                    }
                    foreach (float z in new[] { -4.46f, 4.46f })
                    {
                        shapes.Box("End red panel", new Vector3(0, 1.05f, z), new Vector3(3.2f, .6f, .1f), Red);
                        shapes.Box("Lower windscreen", new Vector3(0, 1.98f, z), new Vector3(2.9f, 1.15f, .1f), Glass);
                        shapes.Box("Upper windscreen", new Vector3(0, 4.22f, z), new Vector3(2.9f, 1.15f, .1f), Glass);
                        shapes.Box("End waist stripe", new Vector3(0, UpperHeight - .1f, z), new Vector3(3.2f, .2f, .12f), Cream);
                        shapes.Box("End roof rail", new Vector3(0, RoofHeight + .36f, z), new Vector3(3.2f, .65f, .08f), Cream);
                        shapes.Box("Bumper", new Vector3(0, .58f, z * 1.025f), new Vector3(3.35f, .2f, .18f), Metal);
                        shapes.Box("Destination board", new Vector3(0, 5.04f, z * 1.012f), new Vector3(2.5f, .38f, .08f), Dark);
                        for (int i = 0; i < 5; i++) shapes.Box("Route display segment", new Vector3(-.8f + i * .4f, 5.04f, z * 1.025f), new Vector3(.22f, .16f, .035f), Gold, false);
                        foreach (float x in new[] { -1.18f, 1.18f }) shapes.Box("Lights", new Vector3(x, .99f, z * 1.025f), new Vector3(.35f, .25f, .06f), z > 0 ? Cream : Red, false);
                    }
                    for (int row = 0; row < 2; row++)
                        foreach (float z in new[] { -3.1f, -1.6f, 1.55f })
                            foreach (float x in new[] { -.97f, .97f })
                            {
                                float floor = row == 0 ? .74f : UpperHeight;
                                shapes.Box("Passenger seat", new Vector3(x, floor + .32f, z), new Vector3(.75f, .28f, .7f), Dark);
                                shapes.Box("Seat back", new Vector3(x, floor + .7f, z - .3f), new Vector3(.75f, .65f, .12f), Red);
                            }
                    shapes.Finish("Bus body mesh");
                }
                var access = new GameObject("Fold-out platform stairs"); access.transform.SetParent(bodyObject.transform, false);
                using (var shapes = new Shapes(access.transform, material))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float top = (i + 1) * (UpperHeight / 10);
                        shapes.Box("Lower stair " + i, new Vector3(2.65f, top - .08f, -4 + i * .42f), new Vector3(1.1f, .16f, .44f), i % 2 == 0 ? Cream : Metal);
                        top = UpperHeight + (i + 1) * ((RoofHeight - UpperHeight) / 10);
                        shapes.Box("Roof stair " + i, new Vector3(3.85f, top - .08f, .22f - i * .42f), new Vector3(1.1f, .16f, .44f), i % 2 == 0 ? Cream : Metal);
                    }
                    shapes.Box("Upper deck access landing", new Vector3(3.05f, UpperHeight - .08f, .5f), new Vector3(3, .16f, 1.2f), Gold);
                    shapes.Box("Roof access bridge", new Vector3(3, RoofHeight - .08f, -3.9f), new Vector3(2.8f, .16f, 1), Gold);
                    foreach (float edge in new[] { -.5f, .5f })
                    {
                        shapes.Primitive(PrimitiveType.Cube, new Vector3(2.65f + edge, 1.5f, -1.9f), new Vector3(.08f, .1f, 5.25f), Quaternion.Euler(-36.4f, 0, 0), Metal);
                        shapes.Primitive(PrimitiveType.Cube, new Vector3(3.85f + edge, 4.2f, -1.7f), new Vector3(.08f, .1f, 4.8f), Quaternion.Euler(29.2f, 0, 0), Metal);
                    }
                    for (int i = 0; i < 3; i++) shapes.Box("Boarding step", new Vector3(-2.65f + .35f * i, (i + 1) * .246f - .07f, 3.3f), new Vector3(.4f, .14f, 1.3f), Cream);
                    shapes.Finish("Bus access stairs mesh");
                }
                var wheels = new List<TCCWheel>();
                Mesh wheelMesh;
                using (var shapes = new Shapes(null, material))
                {
                    shapes.Primitive(PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.04f, .14f, 1.04f), Quaternion.Euler(0, 0, 90), Dark);
                    foreach (float x in new[] { -.155f, .155f }) shapes.Primitive(PrimitiveType.Cylinder, new Vector3(x, 0, 0), new Vector3(.54f, .025f, .54f), Quaternion.Euler(0, 0, 90), Metal);
                    wheelMesh = shapes.Save("Bus wheel mesh");
                }
                foreach (float z in new[] { 3.1f, -2.35f, -3.2f })
                    foreach (float x in new[] { -1.5f, 1.5f })
                    {
                        var go = new GameObject("Bus wheel " + x + " " + z); go.transform.SetParent(root.transform, false); go.transform.localPosition = new Vector3(x, .52f, z);
                        var wheel = go.AddComponent<TCCWheel>(); wheel.wheelRadius = .52f; wheel.wheelMass = 2;
                        wheel.suspensionSpring = 6000; wheel.suspensionDamper = 130; wheel.steeringMaxRotation = z > 0 ? 30 : 0;
                        var model = new GameObject("Primitive wheel model"); model.transform.SetParent(go.transform, false);
                        model.AddComponent<MeshFilter>().sharedMesh = wheelMesh; model.AddComponent<MeshRenderer>().sharedMaterial = material;
                        wheel.wheelModel = model.transform; wheels.Add(wheel);
                    }
                var controller = root.AddComponent<TCCPlayer>(); controller.carBody = body; controller.wheels = wheels.ToArray();
                controller.motorMaxSpeed = 9; controller.motorAcceleration = 420; controller.brakeForce = 150; controller.steeringSpeed = 65; controller.steeringMinSpeed = 3;
                var sound = new GameObject("Bus engine sound"); sound.transform.SetParent(bodyObject.transform, false);
                var engine = sound.AddComponent<AudioSource>();
                engine.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/DavidJalbert/TinyCarController/Examples/Sounds/Car Engine.wav");
                engine.loop = true; engine.playOnAwake = true; engine.mute = true; engine.volume = .13f; engine.spatialBlend = 1; engine.minDistance = 4; engine.maxDistance = 45;
                var effects = root.AddComponent<TCCBasicEffects>(); effects.playerObject = controller; effects.audioEngine = engine; effects.maxEnginePitch = .8f;
                var endpoint = bodyObject.AddComponent<PootleFrobject>(); endpoint.localPoint = new Vector3(-1.9f, 1.1f, 3.7f);
                bodyObject.AddComponent<PootleFrobjectIndicator>().markerMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Pootle/Samples/Materials/Frobject marker.mat");
                var rig = new GameObject("Bus Chase Camera"); rig.transform.SetParent(root.transform, false);
                var camera = rig.AddComponent<CinemachineCamera>(); camera.Priority = 100; camera.Lens.FieldOfView = 60; rig.SetActive(false);
                var car = root.AddComponent<PootleCar>(); car.interaction = endpoint; car.chaseCamera = camera;
                car.cameraOffset = new Vector3(0, 6.5f, -12); car.lookOffset = new Vector3(0, 2.6f, 0);
                car.exitOffsets = new[] { new Vector3(-3.5f, 0, 3.8f), new Vector3(-2.7f, 0, -2), new Vector3(0, 0, 5.5f), new Vector3(0, 0, -5.5f) };
                var platform = root.AddComponent<PootleBusPlatform>(); platform.accessStairs = access;
                platform.roofPoint = Point(bodyObject.transform, "Roof standing point", new Vector3(0, RoofHeight, 0));
                platform.stairWaypoints = new[]
                {
                    Point(access.transform,"Stair start",new Vector3(2.65f,0,-4.8f)),
                    Point(access.transform,"First landing",new Vector3(2.65f,UpperHeight,.5f)),
                    Point(access.transform,"Second flight start",new Vector3(3.85f,UpperHeight,.5f)),
                    Point(access.transform,"Roof bridge",new Vector3(3.85f,RoofHeight,-3.9f)),
                    Point(access.transform,"Roof entry",new Vector3(0,RoofHeight,-3.9f)),
                    platform.roofPoint
                };
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static Transform Point(Transform parent, string name, Vector3 at)
        { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at; return go.transform; }

        static void EnsureGround(Transform parent, Vector3 center, Material material, int index)
        {
            foreach (var offset in new[] { new Vector3(-5, 0, -5), new Vector3(5, 0, 8) })
                if (!Physics.Raycast(center + parent.rotation * offset + Vector3.up * .1f, Vector3.down, .3f))
                {
                    using (var shapes = new Shapes(parent, material))
                    { shapes.Box("Bus stop ground", new Vector3(0, -.25f, 1.5f), new Vector3(13, .5f, 19), new Color(.3f, .43f, .42f)); shapes.Finish("Bus stop ground " + index); }
                    return;
                }
        }

        sealed class Shapes : IDisposable
        {
            readonly Transform parent; readonly Material material; readonly GameObject visual;
            readonly List<CombineInstance> parts = new List<CombineInstance>(); readonly List<Mesh> temporary = new List<Mesh>();
            public Shapes(Transform parent, Material material, GameObject visual = null) { this.parent = parent; this.material = material; this.visual = visual; }
            public void Box(string name, Vector3 at, Vector3 size, Color color, bool solid = true)
            {
                Primitive(PrimitiveType.Cube, at, size, Quaternion.identity, color);
                if (solid && parent)
                { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = at; go.AddComponent<BoxCollider>().size = size; }
            }
            public void Primitive(PrimitiveType type, Vector3 at, Vector3 size, Quaternion rotation, Color color)
            {
                var go = GameObject.CreatePrimitive(type);
                var mesh = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh); Object.DestroyImmediate(go);
                var colors = new Color[mesh.vertexCount]; for (int i = 0; i < colors.Length; i++) colors[i] = color.linear;
                mesh.colors = colors; temporary.Add(mesh);
                parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(at, rotation, size) });
            }
            public Mesh Save(string name)
            {
                var mesh = new Mesh { name = name }; mesh.CombineMeshes(parts.ToArray(), true, true); mesh.RecalculateBounds();
                string path = Folder + "/" + name + ".asset"; var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (saved) { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved); return saved; }
                AssetDatabase.CreateAsset(mesh, path); return mesh;
            }
            public void Finish(string name)
            {
                var go = visual ? visual : new GameObject(name); if (!visual) go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = Save(name); go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            public void Dispose() { foreach (var mesh in temporary) Object.DestroyImmediate(mesh); }
        }
    }
}
