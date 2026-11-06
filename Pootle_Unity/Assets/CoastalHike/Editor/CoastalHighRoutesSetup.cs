using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Pootle;
using Object = UnityEngine.Object;

// Additive, explicit scene authoring. Never rebuilds the existing hike or runs on load.
public static class CoastalHighRoutesSetup
{
    public const string RootName = "07 · Mountain skywalk and summit hut";
    const string Folder = "Assets/CoastalHike/High Route Assets";
    static Material timber, honey, darkWood, roof, rope, black, stone, coral, glow;
    static Transform root;
    static PootleZoneCameraDirector director;
    static PootleCaveTravel travel;
    static List<PootleCameraZone> zones;

    static Material Material(string name, string hex)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        ColorUtility.TryParseHtmlString("#" + hex, out var colour);
        mat.SetColor("_BaseColor", colour); mat.SetFloat("_Smoothness", .08f);
        return mat;
    }

    static Transform Group(string name, Transform parent, Vector3 point)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = point; return t;
    }
    static GameObject Box(string name, Transform parent, Vector3 p, Vector3 size, Material mat, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static void Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material mat, bool collision = false)
    {
        var go = Box(name, parent, (a + b) * .5f, new Vector3(width, width, Vector3.Distance(a, b)), mat, collision);
        go.transform.localRotation = Quaternion.LookRotation(b - a);
    }
    static Transform Marker(string name, Transform parent, Vector3 p, Vector3 direction)
    {
        var t = Group(name, parent, p); t.localRotation = Quaternion.LookRotation(direction); return t;
    }

    static void Bridge(string name, Vector3 a, Vector3 b, float sag)
    {
        var bridge = Group(name, root, Vector3.zero);
        var samples = Group("Walking route", bridge, Vector3.zero);
        float length = Vector3.Distance(a, b);
        float horizontalLength = Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        if (Mathf.Abs(b.y - a.y) / (horizontalLength - 6.4f) > .7f) sag = 0;
        int count = Mathf.CeilToInt(length / .75f);
        Vector3 right = Vector3.Cross(Vector3.up, b - a).normalized;
        Vector3 Point(float t)
        {
            // Meet the top of each summit platform before its edge, rather than hitting a raised lip.
            float edge = 3.2f / horizontalLength;
            float heightT = Mathf.Clamp01((t - edge) / (1 - 2 * edge));
            var point = Vector3.Lerp(a, b, t);
            point.y = Mathf.Lerp(a.y, b.y, heightT) - Mathf.Sin(heightT * Mathf.PI) * sag;
            return point;
        }
        for (int i = 0; i <= count; i++)
        {
            Vector3 p = Point(i / (float)count);
            if (i % 3 == 0 || i == count) Marker("Step " + i.ToString("D3"), samples, p + Vector3.up * .04f, b - a);
            if (i == count) break;
            Vector3 next = Point((i + 1f) / count), mid = (p + next) * .5f;
            var rotation = Quaternion.LookRotation(next - p);
            var carrier = Box("Continuous deck support", bridge, mid - rotation * Vector3.up * .14f,
                new Vector3(3.6f, .24f, Vector3.Distance(p, next) + .12f), darkWood);
            carrier.transform.localRotation = rotation;
            var plank = Box("Crosswise cedar plank", bridge, mid - rotation * Vector3.up * .045f,
                new Vector3(3.65f, .09f, Vector3.Distance(p, next) - .035f), i % 4 == 0 ? honey : timber, false);
            plank.transform.localRotation = rotation;
            // Leave the ends open so players can turn between bridges on the summit landings.
            if (i * length / count < 2.5f || (count - i) * length / count < 2.5f) continue;
            foreach (int side in new[] { -1, 1 })
            {
                var offset = right * (side * 1.82f);
                Beam("Upper rope handrail", bridge, p + offset + Vector3.up * 1.12f, next + offset + Vector3.up * 1.12f, .10f, rope, true);
                Beam("Lower rope", bridge, p + offset + Vector3.up * .52f, next + offset + Vector3.up * .52f, .065f, rope);
                if (i % 3 == 0)
                {
                    Box("Timber balustrade post", bridge, p + offset + Vector3.up * .59f, new Vector3(.17f, 1.35f, .17f), darkWood);
                    Box("Ivory post cap", bridge, p + offset + Vector3.up * 1.3f, new Vector3(.22f, .12f, .22f), honey, false);
                }
            }
        }
        var zone = Zone(name, root, (a + b) * .5f + Vector3.up * 3, new Vector3(9, Mathf.Abs(b.y - a.y) + 13, Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)) + 8), 65,
            new Vector2(52, 0), 43, a);
        zone.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(b - a, Vector3.up));
    }

    static PootleCameraZone Zone(string name, Transform parent, Vector3 centre, Vector3 size, int priority, Vector2 angles, float distance, Vector3 arrival)
    {
        var cam = Group("CAM · " + name, parent, Vector3.zero);
        var shot = cam.gameObject.AddComponent<PootleExplorationShot>();
        shot.focus = director.focus; shot.policy = AssetDatabase.LoadAssetAtPath<PootleCameraPolicy>("Assets/CoastalHike/Exploration Camera Policy.asset");
        shot.pitchYaw = angles; shot.distance = distance; shot.fieldOfView = 30; shot.screenPosition = new Vector2(0, .04f);
        shot.deadZone = new Vector2(.14f, .12f); shot.airborneWidening = 0; shot.ApplySettings(); shot.Rig.Priority = 0;
        cam.position = parent.TransformPoint(arrival) + Vector3.up * 1.4f - shot.AuthoredRotation * Vector3.forward * shot.EffectiveDistance;
        var go = Group("ZONE · " + name, parent, centre);
        var box = go.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = size;
        var zone = go.gameObject.AddComponent<PootleCameraZone>(); zone.cameraRig = shot.Rig; zone.precedence = priority;
        zone.playerArrival = Marker("Test arrival", parent, arrival + Vector3.up * .08f, Vector3.forward);
        zones.Add(zone); return zone;
    }

    static void Landing(string name, Vector3 p)
    {
        var t = Group(name, root, p);
        Box("Stone summit footing", t, new Vector3(0, -.38f, 0), new Vector3(4.6f, .65f, 4.6f), stone);
        Box("Timber summit landing", t, new Vector3(0, -.06f, 0), new Vector3(5.2f, .12f, 5.2f), timber);
        for (int i = -4; i <= 4; i++)
            Box("Landing board seam", t, new Vector3(i * .55f, .005f, 0), new Vector3(.025f, .012f, 5.18f), darkWood, false);
        Box("Summit waymarker pole", t, new Vector3(0, 1.35f, 2.3f), new Vector3(.12f, 2.7f, .12f), darkWood);
        Box("Coral trail flag", t, new Vector3(.5f, 2.45f, 2.3f), new Vector3(1, .5f, .06f), coral, false);
        Zone(name, root, p + Vector3.up * 3, new Vector3(10, 12, 10), 68, new Vector2(52, 0), 43, p);
    }

    static PootleCavePortal Portal(string name, Transform parent, Vector3 feet, Vector3 inward)
    {
        var t = Marker(name, parent, feet, inward);
        var box = t.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true;
        box.center = new Vector3(0, .8f, -.65f); box.size = new Vector3(2.35f, 2.8f, 1.3f);
        var portal = t.gameObject.AddComponent<PootleCavePortal>(); portal.travel = travel;
        portal.approachEnd = Marker("Doorway approach", parent, feet + inward * .75f, inward);
        return portal;
    }

    static void DoorFrame(Transform parent)
    {
        Box("Pitch-black doorway · no collision", parent, new Vector3(0, 1.55f, .08f), new Vector3(2.4f, 3.1f, .045f), black, false);
        foreach (int side in new[] { -1, 1 }) Box("Heavy timber door jamb", parent, new Vector3(side * 1.35f, 1.65f, -.08f), new Vector3(.25f, 3.3f, .36f), honey);
        Box("Timber door lintel", parent, new Vector3(0, 3.2f, -.08f), new Vector3(3, .27f, .38f), honey);
    }

    static void Cabin()
    {
        Vector3 feet = new Vector3(-3, 15.16f, 58);
        var hut = Group("Summit hut · exterior", root, feet);
        Box("Stone foundation", hut, new Vector3(0, -.5f, 2.9f), new Vector3(6.6f, .85f, 6.2f), stone);
        Box("Hut floor", hut, new Vector3(0, -.09f, 2.8f), new Vector3(6.4f, .18f, 6.4f), timber);
        Box("Front porch", hut, new Vector3(0, -.10f, -1), new Vector3(7, .2f, 2.4f), timber);
        Box("Doorway approach landing", hut, new Vector3(0, -.12f, -3), new Vector3(3.4f, .24f, 4), timber);
        Box("Summit path link", hut, new Vector3(1.6f, -.13f, -3), new Vector3(6.5f, .26f, 2.2f), timber);
        Box("West cabin wall", hut, new Vector3(-3.1f, 1.7f, 3), new Vector3(.24f, 3.4f, 6), timber);
        Box("East cabin wall", hut, new Vector3(3.1f, 1.7f, 3), new Vector3(.24f, 3.4f, 6), timber);
        Box("Rear cabin wall", hut, new Vector3(0, 1.7f, 5.95f), new Vector3(6.4f, 3.4f, .24f), timber);
        foreach (int side in new[] { -1, 1 })
        {
            Box("Front wall beside doorway", hut, new Vector3(side * 2.25f, 1.7f, .13f), new Vector3(1.9f, 3.4f, .24f), timber);
            for (int row = 0; row < 9; row++)
            {
                Box("Front horizontal cedar siding", hut, new Vector3(side * 2.25f, .19f + row * .36f, -.015f), new Vector3(1.85f, .31f, .065f), row % 3 == 0 ? honey : timber, false);
                Box("Side horizontal cedar siding", hut, new Vector3(side * 3.25f, .19f + row * .36f, 3), new Vector3(.06f, .31f, 6), row % 3 == 0 ? honey : timber, false);
            }
            foreach (float z in new[] { .05f, 5.95f }) Box("Corner timber", hut, new Vector3(side * 3.15f, 1.75f, z), new Vector3(.3f, 3.5f, .3f), darkWood);
            Box("Amber front window", hut, new Vector3(side * 2.25f, 2.15f, -.07f), new Vector3(1.05f, 1.05f, .09f), darkWood, false);
            Box("Warm window pane", hut, new Vector3(side * 2.25f, 2.15f, -.125f), new Vector3(.83f, .83f, .035f), glow, false);
            Box("Window mullion", hut, new Vector3(side * 2.25f, 2.15f, -.15f), new Vector3(.08f, 1, .05f), honey, false);
            Box("Window crossbar", hut, new Vector3(side * 2.25f, 2.15f, -.15f), new Vector3(1, .08f, .05f), honey, false);
            for (int i = 0; i < 13; i++)
            {
                float z = -.45f + i * .57f;
                Beam("Pitched green roof board", hut, new Vector3(0, 5.05f, z), new Vector3(side * 3.75f, 3.35f, z), .61f, roof, true);
            }
            Beam("Front gable fascia", hut, new Vector3(0, 5.08f, -.55f), new Vector3(side * 3.8f, 3.36f, -.55f), .18f, honey);
            // Gable boards follow the pitched roof silhouette.
            for (int i = 0; i < 8; i++)
            {
                float x = side * (.2f + i * .4f), height = 1.62f - Mathf.Abs(x) * .45f;
                Box("Cedar front gable", hut, new Vector3(x, 3.4f + height * .5f, .12f), new Vector3(.39f, height, .2f), timber, false);
            }
        }
        Beam("Roof ridge cap", hut, new Vector3(0, 5.15f, -.7f), new Vector3(0, 5.15f, 6.7f), .22f, darkWood);
        Box("Stone chimney", hut, new Vector3(2, 4.35f, 4.5f), new Vector3(.65f, 2.2f, .7f), stone);
        Box("Chimney cap", hut, new Vector3(2, 5.5f, 4.5f), new Vector3(.85f, .18f, .9f), darkWood);
        DoorFrame(hut);
        Box("Porch bench", hut, new Vector3(2.45f, .55f, -1), new Vector3(1.8f, .15f, .65f), honey);
        foreach (float x in new[] { 1.8f, 3.1f }) Box("Bench leg", hut, new Vector3(x, .25f, -1), new Vector3(.16f, .5f, .5f), darkWood);

        var network = Object.FindAnyObjectByType<PootleCaveNetwork>();
        var room = Group("Summit hut · interior", network.transform, feet);
        Undo.RegisterCreatedObjectUndo(room.gameObject, "Add summit hut interior");
        Box("Hut interior void", room, new Vector3(0, -.8f, 3), new Vector3(22, .1f, 22), black, false);
        Box("Hut interior floor", room, new Vector3(0, -.12f, 3.5f), new Vector3(8.4f, .24f, 8), timber);
        for (int i = -6; i <= 6; i++) Box("Interior floor seam", room, new Vector3(i * .6f, .004f, 3.5f), new Vector3(.018f, .008f, 7.9f), darkWood, false);
        foreach (int side in new[] { -1, 1 })
        {
            var wall = Box("Hut interior side wall", room, new Vector3(side * 4.2f, 1.5f, 3.5f), new Vector3(.24f, 3, 8.2f), timber);
            SealCutaway(wall);
            var front = Box("Hut interior front cutaway", room, new Vector3(side * 2.8f, .35f, -.45f), new Vector3(2.7f, .7f, .24f), timber);
            SealCutaway(front);
        }
        var rear = Box("Hut interior rear wall", room, new Vector3(0, 1.65f, 7.5f), new Vector3(8.6f, 3.3f, .24f), timber); SealCutaway(rear);
        var innerDoor = Group("Interior black exit", room, Vector3.zero); innerDoor.localRotation = Quaternion.Euler(0, 180, 0); DoorFrame(innerDoor);
        Box("Woven coral rug", room, new Vector3(0, .015f, 4), new Vector3(2.6f, .025f, 2.4f), coral, false);
        Box("Map table", room, new Vector3(-2.5f, .9f, 4.6f), new Vector3(2.2f, .15f, 1.3f), honey);
        foreach (float x in new[] { -3.3f, -1.7f }) foreach (float z in new[] { 4.15f, 5.05f }) Box("Table leg", room, new Vector3(x, .43f, z), new Vector3(.15f, .86f, .15f), darkWood);
        Box("Trail map parchment", room, new Vector3(-2.5f, .99f, 4.6f), new Vector3(1.45f, .015f, .85f), rope, false);
        Box("Bed frame", room, new Vector3(2.6f, .36f, 5), new Vector3(1.7f, .72f, 3.3f), darkWood);
        Box("Pine green blanket", room, new Vector3(2.6f, .77f, 4.7f), new Vector3(1.65f, .16f, 2.55f), roof, false);
        Box("Ivory pillow", room, new Vector3(2.6f, .82f, 6.1f), new Vector3(1.25f, .23f, .6f), rope, false);
        Box("Cabin stove", room, new Vector3(-2.5f, .6f, 6.6f), new Vector3(1.1f, 1.2f, .8f), darkWood);
        Box("Stove warm opening", room, new Vector3(-2.5f, .65f, 6.18f), new Vector3(.65f, .45f, .03f), glow, false);
        Box("Stove flue", room, new Vector3(-2.5f, 2.2f, 6.6f), new Vector3(.22f, 2, .22f), darkWood, false);
        var lamp = Group("Hut warm lamplight", room, new Vector3(0, 3.5f, 4)).gameObject.AddComponent<Light>();
        lamp.type = LightType.Point; lamp.color = new Color(1, .76f, .45f); lamp.intensity = 2; lamp.range = 12; lamp.shadows = LightShadows.None;
        foreach (var renderer in room.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;

        var enter = Portal("ENTER · Summit hut", root, feet, Vector3.forward);
        var exit = Portal("EXIT · Summit hut", network.transform, feet, Vector3.back);
        exit.transform.SetParent(room, true); exit.approachEnd.SetParent(room, true);
        enter.destination = Marker("Hut interior arrival", room, new Vector3(0, .08f, 1.8f), Vector3.forward);
        enter.arrivalWalkEnd = Marker("Hut interior walk clear", room, new Vector3(0, .08f, 4.1f), Vector3.forward);
        exit.destination = Marker("Hut exterior arrival", hut, new Vector3(0, .08f, -1.8f), Vector3.back);
        exit.arrivalWalkEnd = Marker("Hut exterior walk clear", hut, new Vector3(0, .08f, -4.1f), Vector3.back);
        Undo.RecordObject(travel, "Connect summit hut doors");
        travel.portals = travel.portals.Concat(new[] { enter, exit }).ToArray();
        Zone("Summit hut porch", root, feet + new Vector3(0, 2, -1), new Vector3(12, 10, 14), 75, new Vector2(48, 0), 32, feet + Vector3.back * 3);
        Zone("Cave summit hut interior", room, new Vector3(0, 3, 3.5f), new Vector3(10, 13, 10), 245, new Vector2(64, 0), 31, new Vector3(0, .08f, 4));
        var lighting = network.GetComponent<PootleCaveLighting>(); Undo.RecordObject(lighting, "Include hut interior lighting");
        var bounds = lighting.localInterior; bounds.Encapsulate(feet + new Vector3(-6, -1, -2)); bounds.Encapsulate(feet + new Vector3(6, 13, 10)); lighting.localInterior = bounds;
    }

    static void SealCutaway(GameObject wall)
    {
        var collider = wall.GetComponent<BoxCollider>(); var size = collider.size;
        size.y = 12 / wall.transform.localScale.y; collider.size = size;
        collider.center = new Vector3(0, (6 - wall.transform.localPosition.y) / wall.transform.localScale.y, 0);
    }

    static void PathInputExperiments()
    {
        foreach (string name in new[] { "02 · Meadow traverse", "04 · Lookout ascent" })
        {
            var path = GameObject.Find(name).transform;
            var pivot = Group("EXPERIMENT · Path input · " + name, root, path.position + Vector3.up * 1.2f);
            pivot.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(path.forward, Vector3.up));
            var zone = pivot.gameObject.AddComponent<PootlePathInputZone>();
            zone.Volume.isTrigger = true;
            zone.Volume.size = new Vector3(4.6f, Mathf.Abs(path.forward.y * path.localScale.z) + 4, path.localScale.z + 1);
            zone.Volume.center = Vector3.zero;
            zone.angleTolerance = 12; zone.digitalOnly = true;
        }
    }

    [MenuItem("Pootle/Coastal Hike/Add Mountain Bridges and Summit Hut")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != CoastalHikeBuilder.ScenePath)
            throw new InvalidOperationException("Open CoastalHike in Edit Mode first.");
        if (GameObject.Find(RootName)) throw new InvalidOperationException("The mountain route already exists. Edit its saved objects instead of duplicating it.");
        travel = Object.FindAnyObjectByType<PootleCaveTravel>(); director = Object.FindAnyObjectByType<PootleZoneCameraDirector>();
        if (!travel || !director || !Object.FindAnyObjectByType<PootleCaveNetwork>()) throw new InvalidOperationException("The existing cave network and camera director are required.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/CoastalHike", "High Route Assets");
        timber = Material("Cedar boards", "9D704B"); honey = Material("Honey timber trim", "C59A66"); darkWood = Material("Dark timber supports", "514335");
        roof = Material("Pine green roof", "3D6254"); rope = Material("Flax rope", "D9C79B");
        black = AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Cave Assets/Cave doorway darkness.mat");
        stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Materials/Pale stone.mat");
        coral = AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Materials/Trail coral.mat");
        glow = AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Cave Assets/Cave lantern amber.mat");
        root = Group(RootName, null, Vector3.zero); Undo.RegisterCreatedObjectUndo(root.gameObject, "Add mountain skywalk and hut");
        zones = new List<PootleCameraZone>(director.zones);
        Vector3[] peaks = { new Vector3(2, 15.16f, 66), new Vector3(-33, 21.16f, 80), new Vector3(-19, 30.16f, 87),
            new Vector3(-3, 37.16f, 89), new Vector3(16, 29.16f, 86), new Vector3(32, 20.16f, 81), new Vector3(5, 15.16f, 65) };
        string[] names = { "Skywalk 01 · western approach", "Skywalk 02 · west mountain crossing", "Skywalk 03 · highest ridge",
            "Skywalk 04 · eastern ridge", "Skywalk 05 · east mountain crossing", "Skywalk 06 · lookout return" };
        for (int i = 1; i < 6; i++) Landing("Mountain landing " + i, peaks[i]);
        for (int i = 0; i < 6; i++) Bridge(names[i], peaks[i], peaks[i + 1], i == 0 || i == 5 ? .65f : .22f);
        Cabin();
        PathInputExperiments();
        Undo.RecordObject(director, "Add high route camera regions"); director.zones = zones.ToArray();
        Physics.SyncTransforms(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(root.gameObject.scene); EditorSceneManager.SaveScene(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        SceneView.lastActiveSceneView?.LookAt(new Vector3(0, 20, 75), Quaternion.Euler(48, 0, 0), 65);
        Debug.Log("Mountain skywalk authored: six connected bridges, five summit landings, and a two-way furnished summit hut.");
    }
}
