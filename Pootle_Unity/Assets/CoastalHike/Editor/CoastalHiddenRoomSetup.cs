using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Pootle;
using Object = UnityEngine.Object;

public static class CoastalHiddenRoomSetup
{
    static Transform inside, outside;
    static Material wall, floor, foliage, tips, dark;
    static GameObject Box(string name, Transform parent, Vector3 point, Vector3 size, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
        go.transform.localPosition = point; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
    }
    static Transform Marker(string name, Transform parent, Vector3 point, Vector3 forward)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = point; t.localRotation = Quaternion.LookRotation(forward); return t;
    }
    static Material Material(string name, string hex)
    {
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); ColorUtility.TryParseHtmlString("#" + hex, out var c);
        material.SetColor("_BaseColor", c); material.SetFloat("_Smoothness", .05f);
        AssetDatabase.CreateAsset(material, "Assets/CoastalHike/Cave Assets/" + name + ".mat"); return material;
    }
    static void Bush(Transform parent, Vector3 point, float scale, string name)
    {
        var root = Marker(name, parent, point, Vector3.forward);
        for (int i = 0; i < 7; i++)
        {
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere); leaf.name = "Passable foliage"; leaf.transform.SetParent(root, false);
            float angle = i * Mathf.PI * 2 / 6;
            leaf.transform.localPosition = i == 6 ? new Vector3(0, 1.7f, 0) * scale : new Vector3(Mathf.Cos(angle) * .55f, .95f + (i % 2) * .45f, Mathf.Sin(angle) * 1.05f) * scale;
            leaf.transform.localScale = new Vector3(2.15f, 2.1f, 2.15f) * scale;
            leaf.GetComponent<Renderer>().sharedMaterial = i % 3 == 0 ? tips : foliage;
            Object.DestroyImmediate(leaf.GetComponent<Collider>());
        }
    }
    static PootleCavePortal Portal(Transform parent, string name, Vector3 inward, PootleCaveTravel travel)
    {
        var t = Marker(name, parent, new Vector3(26.4f, 3, 38), inward);
        var box = t.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true;
        box.center = new Vector3(0, .8f, -.65f); box.size = new Vector3(2.8f, 2.8f, 1.3f);
        var portal = t.gameObject.AddComponent<PootleCavePortal>(); portal.travel = travel;
        portal.approachEnd = Marker("Automatic bush approach", parent, t.localPosition + inward * .75f, inward); return portal;
    }
    [MenuItem("Pootle/Coastal Hike/Add Bush Room and Cave Lighting")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var network = Object.FindAnyObjectByType<PootleCaveNetwork>();
        if (!network || GameObject.Find("Hidden fern room")) throw new System.InvalidOperationException("Open the cave scene; this addition runs once.");
        inside = new GameObject("Hidden fern room").transform; inside.SetParent(network.transform, false);
        outside = new GameObject("Hidden fern entrance · beneath upper bridge").transform;
        outside.SetParent(GameObject.Find("06 · Secret cave entrances").transform, false);
        Undo.RegisterCreatedObjectUndo(inside.gameObject, "Add hidden fern room"); Undo.RegisterCreatedObjectUndo(outside.gameObject, "Add hidden bush entrance");
        wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Cave Assets/Cave cool stone.mat");
        floor = AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Cave Assets/Cave warm floor.mat");
        dark = AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Cave Assets/Cave doorway darkness.mat");
        foliage = Material("Secret bush deep green", "254B35"); tips = Material("Secret bush sage tips", "476C40");
        Box("Fern room floor", inside, new Vector3(22.6f, 2.8f, 38), new Vector3(8, .4f, 8), floor);
        Box("Fern room north wall", inside, new Vector3(22.6f, 4.8f, 42), new Vector3(8.6f, 3.6f, .65f), wall);
        Box("Fern room south cutaway", inside, new Vector3(22.6f, 3.5f, 34), new Vector3(8.6f, 1, .65f), wall);
        foreach (float z in new[] { 34.9f, 41.1f })
        {
            Box("Bush mouth stone", inside, new Vector3(26.6f, 4.6f, z), new Vector3(.65f, 3.2f, 1.8f), wall);
        }
        var cliff = GameObject.Find("03 · Sun terrace · stone").GetComponent<Renderer>().sharedMaterial;
        Box("Natural cliff buttress · concealed mouth", outside, new Vector3(24.5f, 3.2f, 38), new Vector3(3.8f, 6.4f, 7), cliff);
        var opening = Box("Unmarked black opening behind foliage", outside, new Vector3(26.41f, 4.4f, 38), new Vector3(.025f, 2.8f, 2.9f), dark);
        Object.DestroyImmediate(opening.GetComponent<Collider>());
        opening.GetComponent<Renderer>().enabled = false; // The bush conceals an unmarked trigger, with no exposed doorway silhouette.
        Box("Hidden entrance landing", outside, new Vector3(29, 2.8f, 38), new Vector3(6, .4f, 4), floor);
        Vector3 a = new Vector3(31, 3, 38), b = new Vector3(35, 2.08f, 38);
        var ramp = Box("Hidden entrance · lower route link", outside, (a + b) * .5f - Vector3.up * .15f, new Vector3(3.5f, .3f, Vector3.Distance(a, b) + .4f), floor);
        ramp.transform.localRotation = Quaternion.LookRotation(b - a);
        Bush(outside, new Vector3(27.05f, 3, 38), 1.15f, "Bush concealing exterior entrance");
        Bush(inside, new Vector3(26.45f, 3, 38), .85f, "Bush visible inside cave mouth");
        // A small mossy ledge gives the room a destination while keeping the walking line clear.
        Box("Fern room resting ledge", inside, new Vector3(22, 3.35f, 40.8f), new Vector3(2.8f, .7f, 1.2f), wall);
        var travel = Object.FindAnyObjectByType<PootleCaveTravel>();
        var enter = Portal(outside, "ENTER · Hidden fern gate", Vector3.left, travel);
        var exit = Portal(inside, "EXIT · Hidden fern gate", Vector3.right, travel);
        enter.destination = Marker("Fern interior arrival", inside, new Vector3(24.6f, 3.08f, 38), Vector3.left);
        enter.arrivalWalkEnd = Marker("Fern interior buffer end", inside, new Vector3(22.3f, 3.08f, 38), Vector3.left);
        exit.destination = Marker("Fern exterior arrival", outside, new Vector3(28.2f, 3.08f, 38), Vector3.right);
        exit.arrivalWalkEnd = Marker("Fern exterior buffer end", outside, new Vector3(30.5f, 3.08f, 38), Vector3.right);
        var portals = new List<PootleCavePortal>(travel.portals) { enter, exit }; travel.portals = portals.ToArray();
        var cam = new GameObject("CAM · Cave hidden fern room"); cam.transform.SetParent(inside, false);
        var shot = cam.AddComponent<PootleExplorationShot>(); shot.focus = travel.director.focus; shot.pitchYaw = new Vector2(64, 0);
        shot.distance = 25; shot.fieldOfView = 30; shot.airborneWidening = 0; shot.screenPosition = new Vector2(0, .05f); shot.ApplySettings(); shot.Rig.Priority = 0;
        var zone = new GameObject("ZONE · Cave hidden fern room"); zone.transform.SetParent(inside, false); zone.transform.localPosition = new Vector3(22.5f, 7, 38);
        var volume = zone.AddComponent<BoxCollider>(); volume.isTrigger = true; volume.size = new Vector3(11, 12, 10);
        var region = zone.AddComponent<PootleCameraZone>(); region.cameraRig = shot.Rig; region.precedence = 230;
        region.playerArrival = Marker("Test arrival · fern room", inside, new Vector3(22.5f, 3.1f, 38), Vector3.forward);
        var zones = new List<PootleCameraZone>(travel.director.zones) { region }; travel.director.zones = zones.ToArray();
        foreach (var renderer in inside.GetComponentsInChildren<Renderer>()) renderer.shadowCastingMode = ShadowCastingMode.Off;
        foreach (var light in network.GetComponentsInChildren<Light>()) { light.range = 4; light.intensity = .7f; }
        var lantern = new GameObject("Avatar · small cave point light").AddComponent<Light>();
        lantern.transform.SetParent(travel.character.transform, false); lantern.transform.localPosition = new Vector3(0, 2.8f, -.25f);
        lantern.type = LightType.Point; lantern.intensity = 3.5f; lantern.range = 7.5f; lantern.color = new Color(1, .8f, .55f); lantern.enabled = false;
        var lighting = network.gameObject.AddComponent<PootleCaveLighting>(); lighting.player = travel.character.transform; lighting.avatarLight = lantern;
        lighting.outdoorSun = GameObject.Find("Warm coastal afternoon").GetComponent<Light>();
        SeparateSecretRoom();
        AddApproachShot();
        Physics.SyncTransforms(); AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(network.gameObject.scene); EditorSceneManager.SaveScene(network.gameObject.scene);
        Selection.activeGameObject = inside.gameObject;
    }
    // Explicit authoring action; also upgrades the earlier connected-room scene.
    [MenuItem("Pootle/Coastal Hike/Separate Secret Room")]
    public static void SeparateSecretRoom()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
        var room = GameObject.Find("Hidden fern room").transform;
        var network = Object.FindAnyObjectByType<PootleCaveNetwork>().transform;
        var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/CoastalHike/Cave Assets/Cave cool stone.mat");
        foreach (var child in room.GetComponentsInChildren<Transform>())
            if (child.name.StartsWith("Side passage") || child.name == "Fern room west jamb")
                Undo.DestroyObjectImmediate(child.gameObject);
        var chamberWall = GameObject.Find("Chamber east north wall").transform;
        Undo.RecordObject(chamberWall, "Close chamber wall");
        chamberWall.localPosition = new Vector3(16.25f, 4.7f, 37.5f);
        chamberWall.localScale = new Vector3(.65f, 3.4f, 7.5f);
        var west = GameObject.Find("Fern room west sealed wall");
        if (!west)
        {
            west = Box("Fern room west sealed wall", room, new Vector3(18.5f, 4.7f, 38), new Vector3(.65f, 3.4f, 8.6f), stone);
            Undo.RegisterCreatedObjectUndo(west, "Seal secret room");
            west.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }
        // The roof and near walls are visually cut away; full-height collision keeps
        // jumping/gliding from bypassing the room's sole exterior entrance.
        foreach (var go in new[] { west, GameObject.Find("Fern room south cutaway"), GameObject.Find("Fern room north wall") })
        {
            var collider = go.GetComponent<BoxCollider>(); Undo.RecordObject(collider, "Full cave wall collision");
            var size = collider.size; size.y = 12f / go.transform.localScale.y; collider.size = size;
            collider.center = new Vector3(0, (9f - go.transform.localPosition.y) / go.transform.localScale.y, 0);
        }
        var region = GameObject.Find("ZONE · Cave hidden fern room");
        Undo.RecordObject(region.transform, "Keep secret camera inside room");
        region.transform.localPosition = new Vector3(22.7f, 7, 38);
        var bounds = region.GetComponent<BoxCollider>(); Undo.RecordObject(bounds, "Keep secret camera inside room");
        bounds.size = new Vector3(8.2f, 12, 8);
        AuthorLight(network, "Tunnel · dim ambient fill", new Vector3(12, 6.5f, 36), new Color(.65f, .76f, 1), 1.3f, 15);
        AuthorLight(room, "Secret room · warm tunnel beacon", new Vector3(21.5f, 5.6f, 38), new Color(1, .76f, .45f), 1.5f, 9);
        Physics.SyncTransforms(); EditorSceneManager.MarkSceneDirty(room.gameObject.scene);
        EditorSceneManager.SaveScene(room.gameObject.scene);
    }
    static void AuthorLight(Transform parent, string name, Vector3 point, Color colour, float intensity, float range)
    {
        var go = GameObject.Find(name);
        if (!go) { go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "Add cave fill light"); go.transform.SetParent(parent, false); }
        Undo.RecordObject(go.transform, "Position cave fill"); go.transform.localPosition = point;
        var light = go.GetComponent<Light>(); if (!light) light = Undo.AddComponent<Light>(go);
        Undo.RecordObject(light, "Tune cave fill"); light.type = LightType.Point; light.color = colour;
        light.intensity = intensity; light.range = range; light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }
    public static void AddApproachShot()
    {
        if (GameObject.Find("CAM · Hidden bush underpass")) return;
        var director = Object.FindAnyObjectByType<PootleZoneCameraDirector>();
        var parent = GameObject.Find("Hidden fern entrance · beneath upper bridge").transform;
        var cam = new GameObject("CAM · Hidden bush underpass"); cam.transform.SetParent(parent, false);
        var shot = cam.AddComponent<PootleExplorationShot>(); shot.focus = director.focus;
        shot.pitchYaw = new Vector2(15, -90); shot.distance = 22; shot.fieldOfView = 30;
        shot.airborneWidening = 0; shot.screenPosition = new Vector2(0, .05f); shot.ApplySettings(); shot.Rig.Priority = 0;
        var go = new GameObject("ZONE · Hidden bush underpass"); go.transform.SetParent(parent, false); go.transform.position = new Vector3(29.5f, 4, 38);
        var volume = go.AddComponent<BoxCollider>(); volume.isTrigger = true; volume.size = new Vector3(9, 8, 6);
        var region = go.AddComponent<PootleCameraZone>(); region.cameraRig = shot.Rig; region.precedence = 90;
        region.playerArrival = Marker("Test arrival · underpass", parent, new Vector3(31.2f, 3.1f, 38), Vector3.left);
        var zones = new List<PootleCameraZone>(director.zones) { region }; director.zones = zones.ToArray();
    }
}
