using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Pootle;
using Object = UnityEngine.Object;

// Explicit scene authoring, never an edit-mode callback. Adds to the existing hike.
public static class CoastalCaveSetup
{
    const string RootName = "Cave Network · hidden +100Y";
    const string Folder = "Assets/CoastalHike/Cave Assets";
    static Transform exterior, interior;
    static Material wall, floor, trim, black, amber, mint;
    static PootleCaveTravel travel;
    static PootleZoneCameraDirector director;
    static readonly List<PootleCavePortal> portals = new List<PootleCavePortal>();
    static readonly List<PootleCameraZone> zones = new List<PootleCameraZone>();
    static Material Mat(string name, string hex, bool unlit = false)
    {
        var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
        m.name = name; ColorUtility.TryParseHtmlString("#" + hex, out var c); m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", .05f);
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0);
        AssetDatabase.CreateAsset(m, Folder + "/" + name + ".mat"); return m;
    }
    static GameObject Box(string name, Transform parent, Vector3 p, Vector3 size, Material mat, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static Transform Marker(string name, Transform parent, Vector3 p, Vector3 direction)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = p;
        t.localRotation = Quaternion.LookRotation(direction); return t;
    }
    static void Path(string name, Transform parent, Vector3 a, Vector3 b, float width, Material mat)
    {
        var go = Box(name, parent, (a + b) * .5f - Vector3.up * .17f, new Vector3(width, .34f, Vector3.Distance(a, b) + .25f), mat);
        go.transform.localRotation = Quaternion.LookRotation(b - a);
    }
    static void Corridor(string name, Vector3 a, Vector3 b)
    {
        Path(name + " · floor", interior, a, b, 5.5f, floor);
        Vector3 direction = (b - a).normalized, right = Vector3.Cross(Vector3.up, direction);
        foreach (int side in new[] { -1, 1 })
        {
            // The camera-facing edge is a cutaway lip; the opposite edge reads as a cave wall.
            Vector3 offset = right * side * 3;
            float height = offset.z < -.1f ? 1.2f : 3.4f;
            var go = Box(name + " · rock wall", interior, (a + b) * .5f + offset + Vector3.up * height * .5f,
                new Vector3(.65f, height, Vector3.Distance(a, b) + .3f), wall);
            go.transform.localRotation = Quaternion.LookRotation(direction);
        }
        for (int i = 1; i <= 2; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / 3f) + right * 2.55f;
            Lantern(name + " · amber guide", p + Vector3.up * 1.4f);
        }
    }
    static void Lantern(string name, Vector3 p)
    {
        Box(name + " · bracket", interior, p, new Vector3(.45f, .65f, .45f), trim, false);
        Box(name + " · glow", interior, p + Vector3.up * .15f, new Vector3(.24f, .36f, .24f), amber, false);
        var light = new GameObject(name + " · light").AddComponent<Light>();
        light.transform.SetParent(interior, false); light.transform.localPosition = p + Vector3.up * .3f;
        light.type = LightType.Point; light.color = new Color(1, .68f, .32f); light.range = 9; light.intensity = 2.5f; light.shadows = LightShadows.None;
    }
    static void Door(string name, Transform parent, Vector3 feet, Vector3 inward)
    {
        var frame = Marker(name + " · black stone doorway", parent, feet, inward);
        var vertices = new List<Vector3> { new Vector3(-1.55f, 0, -.03f), new Vector3(1.55f, 0, -.03f) };
        for (int i = 0; i <= 12; i++)
        {
            float angle = i * Mathf.PI / 12;
            vertices.Add(new Vector3(Mathf.Cos(angle) * 1.55f, 2.15f + Mathf.Sin(angle) * 1.55f, -.03f));
        }
        var tris = new List<int>();
        for (int i = 1; i < vertices.Count - 1; i++) { tris.Add(0); tris.Add(i); tris.Add(i + 1); }
        Mesh mesh = new Mesh { name = name + " silhouette" }; mesh.SetVertices(vertices); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, Folder + "/" + name + " doorway.asset");
        var aperture = new GameObject("Pitch-black opening", typeof(MeshFilter), typeof(MeshRenderer)); aperture.transform.SetParent(frame, false);
        aperture.GetComponent<MeshFilter>().sharedMesh = mesh; aperture.GetComponent<MeshRenderer>().sharedMaterial = black;
        foreach (int side in new[] { -1, 1 }) Box("Stone jamb", frame, new Vector3(side * 1.95f, 1.25f, .1f), new Vector3(.75f, 2.5f, 1.1f), trim);
        for (int i = 0; i <= 8; i++)
        {
            float angle = i * Mathf.PI / 8;
            var stone = Box("Arch stone", frame, new Vector3(Mathf.Cos(angle) * 1.95f, 2.15f + Mathf.Sin(angle) * 1.95f, .1f), new Vector3(.78f, .7f, 1.1f), trim);
            stone.transform.localRotation = Quaternion.Euler(0, 0, angle * Mathf.Rad2Deg - 90);
        }
    }
    static PootleCavePortal Portal(string name, Transform parent, Vector3 feet, Vector3 inward)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = feet;
        go.transform.localRotation = Quaternion.LookRotation(inward);
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0, .8f, -.65f); box.size = new Vector3(2.8f, 2.8f, 1.3f);
        var portal = go.AddComponent<PootleCavePortal>(); portal.travel = travel;
        portal.approachEnd = Marker("Automatic approach", parent, feet + inward * .75f, inward);
        portals.Add(portal); return portal;
    }
    static void LinkDoor(string name, Vector3 feet, Vector3 inward)
    {
        Door(name + " outside", exterior, feet, inward);
        // Both thresholds occupy exactly the same layout coordinates when the network root is lowered.
        Door(name + " inside", interior, feet, -inward);
        var enter = Portal("ENTER · " + name, exterior, feet, inward);
        var leave = Portal("EXIT · " + name, interior, feet, -inward);
        enter.destination = Marker(name + " · interior arrival", interior, feet + inward * 1.8f + Vector3.up * .08f, inward);
        enter.arrivalWalkEnd = Marker(name + " · interior buffer end", interior, feet + inward * 4.1f + Vector3.up * .08f, inward);
        leave.destination = Marker(name + " · exterior arrival", exterior, feet - inward * 1.8f + Vector3.up * .08f, -inward);
        leave.arrivalWalkEnd = Marker(name + " · exterior buffer end", exterior, feet - inward * 4.1f + Vector3.up * .08f, -inward);
        Path(name + " · exterior landing", exterior, feet + inward * .7f, feet - inward * 4.8f, 5, trim);
    }
    static void CameraZone(string name, Vector3 center, Vector3 size, int priority, Vector2 angles, float distance, Vector3 arrival)
    {
        var cam = new GameObject("CAM · Cave " + name); cam.transform.SetParent(interior, false);
        var shot = cam.AddComponent<PootleExplorationShot>(); shot.focus = director.focus; shot.pitchYaw = angles;
        shot.distance = distance; shot.fieldOfView = 30; shot.airborneWidening = 0; shot.screenPosition = new Vector2(0, .06f);
        shot.deadZone = new Vector2(.18f, .14f); shot.ApplySettings(); shot.Rig.Priority = 0;
        cam.transform.localPosition = arrival + Vector3.up * 1.5f - shot.AuthoredRotation * Vector3.forward * distance;
        var go = new GameObject("ZONE · Cave " + name); go.transform.SetParent(interior, false); go.transform.localPosition = center;
        var box = go.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = size;
        var zone = go.AddComponent<PootleCameraZone>(); zone.cameraRig = shot.Rig; zone.precedence = priority;
        zone.playerArrival = Marker("Test arrival", go.transform, arrival - center + Vector3.up * .1f, Vector3.forward);
        zones.Add(zone);
    }
    struct Vertex
    {
        public Vector3 p, n; public Vector2 uv;
        public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex { p = Vector3.Lerp(a.p, b.p, t), n = Vector3.Lerp(a.n, b.n, t).normalized, uv = Vector2.Lerp(a.uv, b.uv, t) };
    }
    // Subtract a convex vertical polygon from every triangle: preserves the rest of the cone.
    static void CutHole(MeshFilter filter, Vector3 center, float radius)
    {
        var mesh = filter.sharedMesh; var pos = mesh.vertices; var normals = mesh.normals; var uv = mesh.uv;
        var output = new List<Vertex>(); var triangles = new List<int>();
        void Emit(List<Vertex> polygon)
        {
            if (polygon.Count < 3) return;
            int first = output.Count; output.AddRange(polygon);
            for (int i = 1; i < polygon.Count - 1; i++) { triangles.Add(first); triangles.Add(first + i); triangles.Add(first + i + 1); }
        }
        var source = mesh.triangles;
        for (int t = 0; t < source.Length; t += 3)
        {
            var inside = new List<Vertex>();
            for (int j = 0; j < 3; j++) { int id = source[t + j]; inside.Add(new Vertex { p = pos[id], n = normals.Length > id ? normals[id] : Vector3.up, uv = uv.Length > id ? uv[id] : Vector2.zero }); }
            for (int edge = 0; edge < 32 && inside.Count > 0; edge++)
            {
                float angle = (edge + .5f) * Mathf.PI * 2 / 32; Vector3 normal = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                float plane = radius * Mathf.Cos(Mathf.PI / 32);
                var next = new List<Vertex>(); var outside = new List<Vertex>();
                for (int j = 0; j < inside.Count; j++)
                {
                    Vertex a = inside[j], b = inside[(j + 1) % inside.Count];
                    float da = Vector3.Dot(filter.transform.TransformPoint(a.p) - center, normal) - plane;
                    float db = Vector3.Dot(filter.transform.TransformPoint(b.p) - center, normal) - plane;
                    if (da <= 0) next.Add(a); else outside.Add(a);
                    if ((da > 0) != (db > 0)) { var cross = Vertex.Lerp(a, b, da / (da - db)); next.Add(cross); outside.Add(cross); }
                }
                Emit(outside); inside = next;
            }
        }
        var vertices = new List<Vector3>(); var ns = new List<Vector3>(); var uvs = new List<Vector2>();
        foreach (var v in output) { vertices.Add(v.p); ns.Add(v.n); uvs.Add(v.uv); }
        var result = new Mesh { name = mesh.name + " · cave aperture", indexFormat = IndexFormat.UInt32 };
        result.SetVertices(vertices); result.SetNormals(ns); result.SetUVs(0, uvs); result.SetTriangles(triangles, 0); result.RecalculateBounds();
        AssetDatabase.CreateAsset(result, Folder + "/" + filter.name + " with aperture.asset");
        Undo.RecordObject(filter, "Cut cave aperture"); filter.sharedMesh = result;
        var collider = filter.GetComponent<MeshCollider>(); Undo.RecordObject(collider, "Cut aperture collision"); collider.sharedMesh = result;
        // PPBase regenerates in Awake even on scene reload. These two modified meshes are now baked.
        var primitive = filter.GetComponent<ProceduralPrimitivesUtil.Cone>();
        if (primitive) Undo.DestroyObjectImmediate(primitive);
    }
    static void Ring(string name, Transform parent, Vector3 center, float inner, float outer, float depth, Material mat)
    {
        var vertices = new List<Vector3>(); var triangles = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2 / 32, b = (i + 1) * Mathf.PI * 2 / 32;
            Vector3 x = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)), y = new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b));
            int v = vertices.Count;
            vertices.AddRange(new[] { x * inner, y * inner, y * outer, x * outer, x * inner - Vector3.up * depth, y * inner - Vector3.up * depth });
            triangles.AddRange(new[] { v, v + 2, v + 1, v, v + 3, v + 2, v, v + 1, v + 5, v, v + 5, v + 4 });
        }
        var mesh = new Mesh { name = name }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals();
        AssetDatabase.CreateAsset(mesh, Folder + "/" + name + ".asset");
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(parent, false); go.transform.localPosition = center;
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<Renderer>().sharedMaterial = mat;
    }
    [MenuItem("Pootle/Coastal Hike/Add Secret Cave Network")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != CoastalHikeBuilder.ScenePath) throw new InvalidOperationException("Open CoastalHike in Edit Mode first.");
        if (GameObject.Find(RootName)) throw new InvalidOperationException("Cave already exists. Edit the scene objects; this additive authoring pass runs once.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/CoastalHike", "Cave Assets");
        portals.Clear(); zones.Clear(); director = Object.FindAnyObjectByType<PootleZoneCameraDirector>();
        zones.AddRange(director.zones);
        exterior = new GameObject("06 · Secret cave entrances").transform;
        interior = new GameObject(RootName).transform;
        Undo.RegisterCreatedObjectUndo(exterior.gameObject, "Add cave entrances"); Undo.RegisterCreatedObjectUndo(interior.gameObject, "Add cave network");
        var network = interior.gameObject.AddComponent<PootleCaveNetwork>();
        wall = Mat("Cave cool stone", "3E5557"); floor = Mat("Cave warm floor", "79766B"); trim = Mat("Cave weathered arch", "777A6A");
        black = Mat("Cave doorway darkness", "000000", true); amber = Mat("Cave lantern amber", "FFC47E", true); mint = Mat("Cave mineral teal", "78D8CB", true);
        travel = new GameObject("Cave travel · automatic entry and circle wipe").AddComponent<PootleCaveTravel>(); travel.transform.SetParent(exterior);
        travel.character = Object.FindAnyObjectByType<PootleCharacter>(); travel.director = director;
        // Non-interactive screen overlay; no event system or raycaster is needed.
        var canvas = new GameObject("Circle transition overlay", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(travel.transform, false); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32760;
        var image = new GameObject("Circle iris", typeof(RectTransform), typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
        image.transform.SetParent(canvas.transform, false); image.raycastTarget = false; image.color = Color.white;
        image.rectTransform.anchorMin = Vector2.zero; image.rectTransform.anchorMax = Vector2.one; image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        var irisMaterial = new Material(Shader.Find("Pootle/Circle Iris")); AssetDatabase.CreateAsset(irisMaterial, Folder + "/Circle Iris.mat"); image.material = irisMaterial; image.enabled = false;
        var iris = canvas.gameObject.AddComponent<PootleCircleIris>(); iris.image = image; iris.outputCamera = Camera.main; iris.subject = travel.character.transform; travel.iris = iris;
        Box("Cave void backdrop", interior, new Vector3(9, 1.5f, 34), new Vector3(130, .2f, 130), black, false);
        Box("Central chamber floor", interior, new Vector3(9, 2.8f, 34), new Vector3(14, .4f, 14), floor);
        Box("Chamber north rock face", interior, new Vector3(9, 5, 41.25f), new Vector3(14.5f, 4, .65f), wall);
        Box("Chamber west north wall", interior, new Vector3(1.75f, 4.7f, 39), new Vector3(.65f, 3.4f, 4.5f), wall);
        Box("Chamber west south wall", interior, new Vector3(1.75f, 3.6f, 29), new Vector3(.65f, 1.2f, 4), wall);
        Box("Chamber east north wall", interior, new Vector3(16.25f, 4.7f, 37.5f), new Vector3(.65f, 3.4f, 7.5f), wall);
        foreach (float x in new[] { 4.2f, 13.8f }) Box("Chamber south cutaway", interior, new Vector3(x, 3.6f, 26.75f), new Vector3(4.4f, 1.2f, .65f), wall);
        Corridor("South entrance tunnel", new Vector3(9, 3, 19), new Vector3(9, 3, 27.5f));
        Corridor("West echo tunnel", new Vector3(-8.2f, 3, 34), new Vector3(2, 3, 34));
        Vector3 eastDoor = new Vector3(24, 3, 26), eastInner = new Vector3(14.5f, 3, 31);
        Corridor("East mineral tunnel", eastInner, eastDoor);
        LinkDoor("South lantern gate", new Vector3(9, 3, 19), Vector3.forward);
        LinkDoor("West echo gate", new Vector3(-8.2f, 3, 34), Vector3.right);
        LinkDoor("East mineral gate", eastDoor, (eastInner - eastDoor).normalized);
        Path("South gate · meadow approach", exterior, new Vector3(0, 3.08f, 18), new Vector3(9, 3.08f, 15.5f), 3.6f, trim);
        Path("West gate · meadow approach", exterior, new Vector3(-14, 3.08f, 27), new Vector3(-11.8f, 3.08f, 34), 3.6f, trim);
        Path("East gate · coastal approach", exterior, new Vector3(28.5f, 3.08f, 23.6f), new Vector3(29, 2.08f, 20), 4, trim);
        // Room landmarks keep the exits recognisable without adding UI instructions.
        for (int i = 0; i < 5; i++)
        {
            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder); crystal.name = "Mint mineral column"; crystal.transform.SetParent(interior, false);
            crystal.transform.localPosition = new Vector3(4 + i * .7f, 3.3f + i % 3 * .2f, 39.2f + i % 2 * .5f);
            crystal.transform.localScale = new Vector3(.35f, .4f + i % 3 * .2f, .35f); crystal.transform.localRotation = Quaternion.Euler(0, 0, i * 7 - 14);
            crystal.GetComponent<Renderer>().sharedMaterial = mint;
        }
        Lantern("Chamber north lantern", new Vector3(10, 5, 40.6f));
        CameraZone("south tunnel", new Vector3(9, 7, 22), new Vector3(9, 12, 14), 200, new Vector2(60, 0), 23, new Vector3(9, 3, 23));
        CameraZone("three-way chamber", new Vector3(9, 7, 34.5f), new Vector3(15, 12, 14), 220, new Vector2(63, 0), 29, new Vector3(9, 3, 33));
        CameraZone("west tunnel", new Vector3(-4, 7, 34), new Vector3(14, 12, 10), 205, new Vector2(63, 0), 24, new Vector3(-3, 3, 34));
        CameraZone("east tunnel", new Vector3(22, 7, 28), new Vector3(12, 12, 13), 205, new Vector2(63, 0), 24, new Vector3(21, 3, 27.5f));
        Vector3 hole = new Vector3(12, 7, 36);
        foreach (string name in new[] { "03 · Sun terrace · stone", "03 · Sun terrace · flat crown" }) CutHole(GameObject.Find(name).GetComponent<MeshFilter>(), hole, 1.6f);
        Ring("Plateau open drop rim", exterior, hole + Vector3.up * .04f, 1.6f, 2.05f, 4.2f, trim);
        // A black bottom conceals the separate location; the actual walkable cone has a real aperture.
        var darkness = GameObject.CreatePrimitive(PrimitiveType.Cylinder); darkness.name = "Drop shaft darkness"; darkness.transform.SetParent(exterior, false);
        darkness.transform.localPosition = hole - Vector3.up * 2; darkness.transform.localScale = new Vector3(3.2f, .01f, 3.2f);
        darkness.GetComponent<Renderer>().sharedMaterial = black; Object.DestroyImmediate(darkness.GetComponent<Collider>());
        Ring("Chamber ceiling shaft rim", interior, hole, 1.6f, 1.95f, .5f, wall);
        var drop = new GameObject("ENTER · Open plateau drop"); drop.transform.SetParent(exterior, false); drop.transform.localPosition = hole;
        var dropBox = drop.AddComponent<BoxCollider>(); dropBox.isTrigger = true; dropBox.center = Vector3.up * .05f; dropBox.size = new Vector3(2.9f, 1.3f, 2.9f);
        var portalDrop = drop.AddComponent<PootleCavePortal>(); portalDrop.travel = travel; portalDrop.dropEntrance = portalDrop.dropArrival = true;
        portalDrop.approachEnd = Marker("Fall toward shaft centre", exterior, hole - Vector3.up, Vector3.forward);
        portalDrop.destination = Marker("Drop into chamber · feet", interior, hole - Vector3.up * .2f, Vector3.forward);
        network.chamberDrop = portalDrop.destination; portals.Add(portalDrop);
        travel.portals = portals.ToArray(); director.zones = zones.ToArray();
        // The hidden network must never cast a giant shadow down onto the outdoor island.
        foreach (var renderer in interior.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
        interior.position = network.hiddenOffset;
        Physics.SyncTransforms(); AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        Selection.activeGameObject = interior.gameObject;
        Debug.Log("Secret cave added: three paired doors, real plateau aperture, +100Y interior, automatic circle transitions and re-entry buffers.");
    }
}
