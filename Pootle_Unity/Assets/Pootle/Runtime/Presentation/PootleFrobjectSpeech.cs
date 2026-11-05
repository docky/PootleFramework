using System.Text;
using UnityEngine;

namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(PootleFrobject)), AddComponentMenu("Pootle/Frobject/World Speech")]
    [DefaultExecutionOrder(110)]
    public sealed class PootleFrobjectSpeech : MonoBehaviour
    {
        [TextArea(2, 5)] public string[] lines = { "Hello! Nice to meet you." };
        [Min(.1f)] public float secondsVisible = 5;
        public Vector3 worldOffset = new Vector3(0, 1.1f, 0);
        public Material bubbleMaterial;
        public bool IsShowing => bubble && bubble.activeSelf;
        PootleFrobject target;
        PootleFrobjectSensor speaker;
        GameObject bubble;
        TextMesh text;
        Material ownedMaterial;
        int nextLine;
        float hideAt;
        void Awake() => target = GetComponent<PootleFrobject>();
        void OnEnable() { if (!target) target = GetComponent<PootleFrobject>(); target.Interacted += Speak; }
        void OnDisable() { target.Interacted -= Speak; Hide(); }

        void Speak(PootleFrobjectSensor sensor)
        {
            if (lines == null || lines.Length == 0) return;
            if (!bubble) CreateBubble();
            speaker = sensor;
            text.text = Wrap(lines[nextLine % lines.Length] ?? ""); nextLine = (nextLine + 1) % lines.Length;
            Vector3 size = text.GetComponent<Renderer>().localBounds.size;
            text.transform.localScale = Vector3.one * Mathf.Min(1, 3.2f / Mathf.Max(.001f, size.x), 1.05f / Mathf.Max(.001f, size.y));
            hideAt = Time.time + Mathf.Max(.1f, secondsVisible);
            bubble.SetActive(true);
        }
        public void Hide() { if (bubble) bubble.SetActive(false); }
        void LateUpdate()
        {
            if (!IsShowing) return;
            if (Time.time >= hideAt || !target.isActiveAndEnabled || !speaker || speaker.GetState(target) == PootleFrobjectState.Hidden) { Hide(); return; }
            Transform camera = speaker.Character.outputCamera ? speaker.Character.outputCamera : Camera.main ? Camera.main.transform : null;
            if (!camera) { Hide(); return; }
            bubble.transform.SetPositionAndRotation(target.Position + worldOffset, camera.rotation);
        }
        void CreateBubble()
        {
            bubble = new GameObject("Speech bubble (runtime)") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
            background.name = "Bubble background"; background.transform.SetParent(bubble.transform, false);
            background.layer = gameObject.layer; background.transform.localScale = new Vector3(3.6f, 1.35f, 1);
            Destroy(background.GetComponent<Collider>());
            var renderer = background.GetComponent<MeshRenderer>();
            if (!bubbleMaterial) ownedMaterial = new Material(Shader.Find("Pootle/Frobject Marker")) { hideFlags = HideFlags.DontSave, color = new Color(.04f, .065f, .09f, .95f) };
            renderer.sharedMaterial = bubbleMaterial ? bubbleMaterial : ownedMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var label = new GameObject("Speech"); label.layer = gameObject.layer; label.transform.SetParent(bubble.transform, false);
            label.transform.localPosition = new Vector3(0, 0, -.015f);
            text = label.AddComponent<TextMesh>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.fontSize = 64; text.characterSize = .035f; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center;
            text.color = Color.white; text.richText = false;
        }
        static string Wrap(string value)
        {
            // Small prototype bubbles: four lines of up to 32 characters. Author longer dialogue as multiple entries.
            var result = new StringBuilder(); int column = 0, row = 0;
            foreach (char c in value)
            {
                if (c == '\r') continue;
                if (c == '\n' || column == 32)
                {
                    if (++row == 4) { result.Append("…"); break; }
                    result.Append('\n'); column = 0;
                    if (c == '\n' || c == ' ') continue;
                }
                result.Append(c); column++;
            }
            return result.ToString();
        }
        void OnDestroy() { if (bubble) Destroy(bubble); if (ownedMaterial) Destroy(ownedMaterial); }
    }
}
