using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;
using Unity.Mathematics;
using Unity.Cinemachine;
using TMPro;
using sc.splinemesher.pro.runtime;
using Pootle;
using Object = UnityEngine.Object;

// Explicit, additive authoring. The installed Spline Mesher owns the track geometry.
public static class CoastalCoasterSetup
{
    public const string RootName = "08 · Island rollercoaster";
    const string Folder = "Assets/CoastalHike/Coaster Assets";
    static Transform root;
    static Material cream, coral, teal, wood, dark, amber;
    static Transform Group(string name, Transform parent, Vector3 position)
    { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }
    static Material Mat(string name, string hex)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, Folder + "/" + name + ".mat"); }
        ColorUtility.TryParseHtmlString("#" + hex, out var c); m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .16f); return m;
    }
    static GameObject Box(string name, Transform parent, Vector3 p, Vector3 scale, Material m, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = p; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m; if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>()); return go;
    }
    static void Beam(string name, Transform parent, Vector3 a, Vector3 b, float width, Material m, bool collision = false)
    { var go = Box(name, parent, (a + b) * .5f, new Vector3(width, width, Vector3.Distance(a, b)), m, collision); go.transform.localRotation = Quaternion.LookRotation(b - a); }
    static TextMeshPro Sign(string text, Transform parent, Vector3 p, float size = .12f)
    {
        var t = Group(text, parent, p); var tm = t.gameObject.AddComponent<TextMeshPro>(); tm.text = text;
        tm.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_ThirdParty/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        tm.fontSize = size * 35; tm.rectTransform.sizeDelta = new Vector2(7, .8f); tm.textWrappingMode = TextWrappingModes.NoWrap;
        tm.alignment = TextAlignmentOptions.Center; tm.color = new Color(1, .95f, .78f);
        var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Station lettering.mat");
        if (!material) { material = new Material(tm.font.material); material.SetFloat("_CullMode", 2); AssetDatabase.CreateAsset(material, Folder + "/Station lettering.mat"); }
        tm.fontSharedMaterial = material; return tm;
    }
    static void MeshTrack(string name, SplineContainer path, float x, float y, float radius, bool ties = false)
    {
        var t = Group(name, root, Vector3.zero); var mesher = t.gameObject.AddComponent<SplineCurveMesher>(); mesher.root = t;
        mesher.autoSplineSampleDistance = false; mesher.splineSampleDistance = .2f;
        mesher.settings.input.shape = ties ? CurveMeshSettings.Shape.Cube : CurveMeshSettings.Shape.Cylinder;
        if (ties) { mesher.settings.input.cube.scale = new Vector3(2.2f, .16f, .18f); mesher.settings.distribution.spacing = 1.05f; }
        else { mesher.settings.input.cylinder.outerRadius = radius; mesher.settings.input.cylinder.radialSegments = 8; mesher.settings.input.cylinder.edgeLoopDistance = .4f; }
        mesher.settings.distribution.curveOffset = new float2(x, y);
        mesher.settings.renderer.materials = new[] { ties ? coral : radius > .15f ? teal : cream };
        mesher.settings.collision.enable = false;
        mesher.SetSplineContainer(path); mesher.Rebuild();
    }
    static PootleIntentGate Gate(string name, Transform parent, Vector3 position, Quaternion rotation)
    {
        var t = Group(name, parent, position); t.localRotation = rotation;
        var volume = t.gameObject.AddComponent<BoxCollider>(); volume.isTrigger = true; volume.center = new Vector3(0, .8f, -.5f); volume.size = new Vector3(.95f, 1.6f, 1.2f);
        var gate = t.gameObject.AddComponent<PootleIntentGate>(); gate.holdSeconds = .35f;
        foreach (int side in new[] { -1, 1 })
        {
            Box("Intent gap post", t, new Vector3(side * .67f, .65f, 0), new Vector3(.3f, 1.3f, .5f), teal);
            Box("Ivory post cap", t, new Vector3(side * .67f, 1.34f, 0), new Vector3(.36f, .12f, .56f), cream, false);
        }
        var cap = Box("Push plate", t, new Vector3(0, .58f, .12f), new Vector3(1.03f, 1.05f, .16f), coral);
        gate.plunger = cap.transform; gate.barrier = cap.GetComponent<Collider>();
        gate.acknowledgementLamp = Box("Push acknowledgement lamp", t, new Vector3(.67f, 1.47f, -.04f), new Vector3(.22f, .15f, .28f), amber, false).GetComponent<Renderer>();
        Sign("PUSH", t, new Vector3(0, .73f, .025f), .07f);
        return gate;
    }
    static PootleCoasterStation Station(string name, Vector3 track, Vector3 forward, Vector3 gateForward)
    {
        var t = Group(name, root, track - Vector3.up * .9f); t.rotation = Quaternion.LookRotation(gateForward);
        var station = t.gameObject.AddComponent<PootleCoasterStation>();
        // The platform is on the approach side of the rail; the narrow gate is its last metre.
        Box("Station timber platform", t, new Vector3(0, -.18f, -3), new Vector3(9, .36f, 6), wood);
        for (int i = 0; i < 15; i++) Box("Deck board seam", t, new Vector3(i * .58f - 4.1f, .005f, -3), new Vector3(.025f, .012f, 5.9f), dark, false);
        foreach (int side in new[] { -1, 1 })
        {
            Box("Platform canopy post", t, new Vector3(-2.65f + side * 1.15f, 1.6f, -4), new Vector3(.22f, 3.2f, .22f), teal);
            for (int j = 0; j < 4; j++) Box("Platform safety post", t, new Vector3(side * 4.3f, .55f, -j * 1.65f), new Vector3(.16f, 1.1f, .16f), cream);
            Beam("Platform handrail", t, new Vector3(side * 4.3f, 1.05f, 0), new Vector3(side * 4.3f, 1.05f, -5), .12f, cream, true);
            // Rail-side fence leaves only the intentional boarding gap.
            Box("Boarding safety fence", t, new Vector3(side * 2.5f, .6f, -.9f), new Vector3(3.5f, .12f, .16f), cream);
        }
        Box("Small station canopy", t, new Vector3(-2.65f, 3.25f, -4), new Vector3(3.2f, .2f, 2.6f), coral);
        Box("Station nameboard", t, new Vector3(-2.65f, 2.65f, -4.65f), new Vector3(3.2f, .85f, .12f), teal, false);
        Sign(name.ToUpperInvariant(), t, new Vector3(-2.65f, 2.65f, -4.72f), .08f);
        station.gate = Gate("Push through to board", t, new Vector3(0, 0, -1.1f), Quaternion.identity); station.gate.receiver = station;
        station.trackAnchor = Group("Track stop", t, new Vector3(0, .9f, 0)); station.trackAnchor.rotation = Quaternion.LookRotation(forward);
        station.boardingPoint = Group("Boarding step", t, new Vector3(0, .05f, -.75f));
        station.exitPoint = Group("Safe exit", t, new Vector3(2.2f, .06f, -3.4f)); station.exitPoint.localRotation = Quaternion.Euler(0, 180, 0);
        Box("Status signboard", t, new Vector3(0, 1.9f, -1.03f), new Vector3(4.4f,.5f,.1f), teal, false);
        station.statusSign = Sign("PUSH TO BOARD", t, new Vector3(0, 1.9f, -1.12f), .085f);
        for (int i = 0; i < 3; i++) station.countdownLights[i] = Box("Countdown light " + (i + 1), t, new Vector3((i - 1) * .45f, 2.35f, -1), new Vector3(.3f, .3f, .2f), amber, false).GetComponent<Renderer>();
        return station;
    }
    static void CameraZone(PootleCoasterStation station, PootleZoneCameraDirector director, List<PootleCameraZone> zones)
    {
        var cam = Group("CAM · " + station.name, root, Vector3.zero); var shot = cam.gameObject.AddComponent<PootleExplorationShot>();
        shot.focus = director.focus; shot.policy = AssetDatabase.LoadAssetAtPath<PootleCameraPolicy>("Assets/CoastalHike/Exploration Camera Policy.asset");
        shot.pitchYaw = new Vector2(48, Mathf.Atan2(station.transform.forward.x, station.transform.forward.z) * Mathf.Rad2Deg); shot.distance = 30; shot.fieldOfView = 30; shot.airborneWidening = 0; shot.ApplySettings(); shot.Rig.Priority = 0;
        cam.position = station.exitPoint.position + Vector3.up * 1.4f - shot.AuthoredRotation * Vector3.forward * shot.EffectiveDistance;
        var v = Group("ZONE · " + station.name, root, station.transform.position + Vector3.up * 3); var box = v.gameObject.AddComponent<BoxCollider>(); box.isTrigger = true; box.size = new Vector3(17, 12, 17);
        var z = v.gameObject.AddComponent<PootleCameraZone>(); z.cameraRig = shot.Rig; z.precedence = 80; z.playerArrival = station.exitPoint; zones.Add(z);
    }
    [MenuItem("Pootle/Coastal Hike/Add Island Rollercoaster")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != CoastalHikeBuilder.ScenePath) throw new InvalidOperationException("Open CoastalHike in Edit Mode first.");
        if (GameObject.Find(RootName)) throw new InvalidOperationException("The coaster already exists; edit its saved objects rather than adding a duplicate.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/CoastalHike", "Coaster Assets");
        cream = Mat("Ivory rails", "F0D9A2"); coral = Mat("Coral cart and ties", "CB6E50"); teal = Mat("Deep teal steel", "315B56"); wood = Mat("Cedar platform", "AD8056"); dark = Mat("Dark wheels", "343C37"); amber = Mat("Signal lens", "F4BB56");
        root = Group(RootName, null, Vector3.zero); Undo.RegisterCreatedObjectUndo(root.gameObject, "Add island rollercoaster");
        var track = Group("Island loop · editable spline", root, Vector3.zero).gameObject.AddComponent<SplineContainer>();
        Vector3[] points = { new Vector3(30,.9f,-15), new Vector3(30,.9f,-7), new Vector3(42,8,10), new Vector3(48,18,42), new Vector3(46,30,72), new Vector3(27,43,103), new Vector3(12,38.06f,100), new Vector3(1,38.06f,100), new Vector3(-10,38.06f,100), new Vector3(-38,30,105), new Vector3(-48,18,80), new Vector3(-43,13,50), new Vector3(-45,8,21), new Vector3(-36,3,-15), new Vector3(-10,7,-31), new Vector3(18,4,-32), new Vector3(30,.9f,-23) };
        var spline = new Spline(); foreach (var p in points) spline.Add(new BezierKnot(p), TangentMode.AutoSmooth); spline.Closed = true; track.Spline = spline;
        MeshTrack("Left running rail · Spline Mesher", track, -.8f, 0, .12f); MeshTrack("Right running rail · Spline Mesher", track, .8f, 0, .12f);
        MeshTrack("Track spine · Spline Mesher", track, 0, -.38f, .19f); MeshTrack("Cross ties · Spline Mesher", track, 0, -.18f, 0, true);
        var supports = Group("Track trestles", root, Vector3.zero); Physics.SyncTransforms(); int supportCount = Mathf.CeilToInt(track.CalculateLength() / 9);
        for (int i = 0; i < supportCount; i++)
        {
            float t = i / (float)supportCount; Vector3 p = track.EvaluatePosition(t), f = track.EvaluateTangent(t); Vector3 right = Vector3.Cross(Vector3.up, f).normalized;
            float ground = -1.8f; foreach (var h in Physics.RaycastAll(p - Vector3.up, Vector3.down, 80, ~0, QueryTriggerInteraction.Ignore)) if (!h.collider.transform.IsChildOf(root)) ground = Mathf.Max(ground, h.point.y);
            if (p.y - ground < 1.1f) continue;
            foreach (int side in new[] { -1, 1 })
            { Vector3 supportTop = p + right * side * 1.3f - Vector3.up * .4f, foot = new Vector3(supportTop.x, ground, supportTop.z); Beam("Steel trestle", supports, foot, supportTop, .24f, teal); Box("Concrete footing", supports, foot, new Vector3(.8f, .3f, .8f), cream); }
            Beam("Trestle crossbar", supports, p - right * 1.5f - Vector3.up * .42f, p + right * 1.5f - Vector3.up * .42f, .22f, teal);
        }
        var bottom = Station("Cove station", points[0], Vector3.forward, Vector3.right);
        var top = Station("Summit station", points[7], Vector3.left, Vector3.forward);
        // Link the existing highest mountain landing to the summit platform.
        var a = new Vector3(-3, 37.16f, 89); var b = new Vector3(1, 37.16f, 95);
        Beam("Summit access walkway", root, a - Vector3.up * .16f, b - Vector3.up * .16f, 2.6f, wood, true);
        // Beam is deliberately thin vertically while keeping its authored width.
        var walkway = root.Find("Summit access walkway"); walkway.localScale = new Vector3(2.6f, .24f, Vector3.Distance(a,b));
        Vector3 sideway = Vector3.Cross(Vector3.up, b-a).normalized;
        foreach (int side in new[] {-1,1}) Beam("Summit access rail", root, Vector3.Lerp(a,b,.45f)+sideway*side*1.25f+Vector3.up, b+sideway*side*1.25f+Vector3.up, .12f, cream, true);
        // Broad boardwalk joins the lower station to the beach.
        Box("Cove approach boardwalk", root, new Vector3(21, -.02f, -15), new Vector3(6,.25f,3), wood);
        var demo = Gate("Spring-back intent button", root, new Vector3(19,.105f,-14.8f), Quaternion.identity); demo.pushBackSpeed = 3.2f;
        Sign("PUSH TO ACKNOWLEDGE", demo.transform, new Vector3(0, 1.9f, -.08f), .075f);
        var cart = Group("Coaster cart", root, points[0]);
        Box("Coral tub", cart, new Vector3(0,.38f,0), new Vector3(1.45f,.5f,2.1f), coral, false);
        Box("Seat cushion", cart, new Vector3(0,.72f,-.25f), new Vector3(.95f,.16f,.75f), teal, false);
        Box("Seat back", cart, new Vector3(0,1.1f,-.68f), new Vector3(1.05f,.85f,.15f), teal, false);
        Box("Lap safety bar", cart, new Vector3(0,1.12f,.32f), new Vector3(1.25f,.09f,.09f), cream, false);
        foreach (int side in new[] {-1,1})
        {
            Box("Tub side",cart,new Vector3(side*.68f,.8f,0),new Vector3(.15f,.45f,2.1f),coral,false);
            foreach(int end in new[]{-1,1}) { var wheel=GameObject.CreatePrimitive(PrimitiveType.Cylinder);wheel.name="Rail wheel";wheel.transform.SetParent(cart,false);wheel.transform.localPosition=new Vector3(side*.8f,.12f,end*.7f);wheel.transform.localRotation=Quaternion.Euler(0,0,90);wheel.transform.localScale=new Vector3(.4f,.1f,.4f);wheel.GetComponent<Renderer>().sharedMaterial=dark;Object.DestroyImmediate(wheel.GetComponent<Collider>()); }
        }
        var passenger = cart.gameObject.AddComponent<PootleMachinePassenger>(); passenger.seat = Group("Passenger hip anchor", cart, new Vector3(0,.88f,-.25f));
        var coaster = root.gameObject.AddComponent<PootleRollercoaster>(); coaster.path=track;coaster.cart=cart;coaster.passenger=passenger;coaster.stations=new[]{bottom,top};bottom.coaster=top.coaster=coaster;
        coaster.stopRequestLamp=Box("Stop requested lamp",cart,new Vector3(0,1.05f,.9f),new Vector3(.22f,.16f,.2f),amber,false).GetComponent<Renderer>();
        var rideCam=Group("CAM · coaster ride",root,Vector3.zero);var rig=rideCam.gameObject.AddComponent<CinemachineCamera>();rig.Priority=-100;rig.Lens.FieldOfView=48;
        var follow=rideCam.gameObject.AddComponent<PootleMachineCamera>();follow.passenger=passenger;follow.vehicle=cart;follow.fixedThirdPerson=true;
        follow.explorationPolicy=AssetDatabase.LoadAssetAtPath<PootleCameraPolicy>("Assets/CoastalHike/Exploration Camera Policy.asset");
        var director=Object.FindAnyObjectByType<PootleZoneCameraDirector>();var zones=new List<PootleCameraZone>(director.zones);CameraZone(bottom,director,zones);CameraZone(top,director,zones);Undo.RecordObject(director,"Add station cameras");director.zones=zones.ToArray();
        Physics.SyncTransforms();AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);Selection.activeGameObject=root.gameObject;
        Debug.Log("Island rollercoaster added: spline-meshed loop, cove/summit stations, push gates, visible boarding and three-light departure.");
    }
}
