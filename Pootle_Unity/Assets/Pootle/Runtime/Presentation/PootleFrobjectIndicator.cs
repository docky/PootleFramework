using UnityEngine;
using UnityEngine.Rendering;

namespace Pootle
{
    /// <summary>Disposable placeholder presentation. Replace this with an outline using Sensor.GetState.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PootleFrobject)), AddComponentMenu("Pootle/Frobject/Dot and Circle")]
    [DefaultExecutionOrder(100)]
    public sealed class PootleFrobjectIndicator : MonoBehaviour
    {
        public PootleFrobjectSensor sensor;
        public Material markerMaterial;
        public Vector3 worldOffset = Vector3.up * .3f;
        [Min(.01f)] public float dotRadius = .055f;
        [Min(.01f)] public float circleRadius = .19f;
        [Min(0)] public float expandSpeed = 10;
        public Color awareColor = Color.white;
        public Color readyColor = new Color(.65f, 1, .75f);
        public PootleFrobjectState State { get; private set; }
        PootleFrobject target;
        GameObject visual;
        Mesh mesh;
        MeshRenderer marker;
        Material ownedMaterial;
        MaterialPropertyBlock properties;
        const int Segments = 40;
        readonly Vector3[] vertices = new Vector3[Segments * 2];
        float expansion;

        void Awake() => target = GetComponent<PootleFrobject>();
        void LateUpdate()
        {
            if (!sensor) sensor = FindAnyObjectByType<PootleFrobjectSensor>();
            State = sensor ? sensor.GetState(target) : PootleFrobjectState.Hidden;
            Transform camera = sensor && sensor.Character && sensor.Character.outputCamera ? sensor.Character.outputCamera : Camera.main ? Camera.main.transform : null;
            if (State == PootleFrobjectState.Hidden || !camera) { if (visual) visual.SetActive(false); expansion = 0; return; }
            if (!visual) CreateVisual();
            visual.SetActive(true);
            visual.transform.SetPositionAndRotation(target.Position + worldOffset, camera.rotation);
            expansion = Mathf.MoveTowards(expansion, State == PootleFrobjectState.Ready ? 1 : 0, expandSpeed * Time.deltaTime);
            float outer = Mathf.Lerp(dotRadius, circleRadius, expansion), inner = outer * .74f * expansion;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2 / Segments;
                Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0);
                vertices[i * 2] = direction * outer; vertices[i * 2 + 1] = direction * inner;
            }
            mesh.vertices = vertices; mesh.RecalculateBounds();
            properties.SetColor("_Color", Color.Lerp(awareColor, readyColor, expansion)); marker.SetPropertyBlock(properties);
        }

        void CreateVisual()
        {
            visual = new GameObject("Frobject marker (runtime)") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            // Keep unit scale even when the interactable is a scaled primitive.
            mesh = new Mesh { name = "Frobject dot-ring", hideFlags = HideFlags.DontSave }; mesh.MarkDynamic();
            var triangles = new int[Segments * 6];
            for (int i = 0; i < Segments; i++)
            {
                int a = i * 2, b = ((i + 1) % Segments) * 2, t = i * 6;
                triangles[t] = a; triangles[t+1] = b; triangles[t+2] = a+1;
                triangles[t+3] = b; triangles[t+4] = b+1; triangles[t+5] = a+1;
            }
            mesh.vertices = vertices; mesh.triangles = triangles;
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            marker = visual.AddComponent<MeshRenderer>();
            if (!markerMaterial) ownedMaterial = new Material(Shader.Find("Pootle/Frobject Marker")) { hideFlags = HideFlags.DontSave };
            marker.sharedMaterial = markerMaterial ? markerMaterial : ownedMaterial;
            marker.shadowCastingMode = ShadowCastingMode.Off; marker.receiveShadows = false;
            properties = new MaterialPropertyBlock();
        }
        void OnDisable() { State = PootleFrobjectState.Hidden; if (visual) visual.SetActive(false); }
        void OnDestroy() { if (visual) Destroy(visual); if (mesh) Destroy(mesh); if (ownedMaterial) Destroy(ownedMaterial); }
    }
}
