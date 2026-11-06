using UnityEngine;

namespace Pootle
{
    [DefaultExecutionOrder(1000)]
    public sealed class PootleCircleIris : MonoBehaviour
    {
        public UnityEngine.UI.RawImage image;
        public Camera outputCamera;
        public Transform subject;
        public float focusHeight = 1;
        public float Openness { get; private set; } = 1;
        public Vector2 Centre { get; private set; } = new Vector2(.5f, .5f);
        Material instance;
        void Awake()
        {
            if (image && image.material) { instance = new Material(image.material); image.material = instance; }
            SetOpen(1);
        }
        public void SetOpen(float value)
        {
            Openness = Mathf.Clamp01(value);
            if (image) image.enabled = Openness < .9999f;
            Refresh();
        }
        void LateUpdate() => Refresh();
        void Refresh()
        {
            if (!instance || !image) return;
            if (subject && outputCamera)
            {
                var p = outputCamera.WorldToViewportPoint(subject.position + Vector3.up * focusHeight);
                Centre = p.z > 0 ? new Vector2(Mathf.Clamp01(p.x), Mathf.Clamp01(p.y)) : new Vector2(.5f, .5f);
            }
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            float x = Mathf.Max(Centre.x, 1 - Centre.x) * aspect;
            float y = Mathf.Max(Centre.y, 1 - Centre.y);
            instance.SetVector("_Centre", new Vector4(Centre.x, Centre.y, 0, 0));
            instance.SetFloat("_Aspect", aspect);
            instance.SetFloat("_Radius", Mathf.Lerp(-.01f, Mathf.Sqrt(x * x + y * y) + .02f, Openness));
        }
        void OnDestroy() { if (instance) Destroy(instance); }
    }
}
