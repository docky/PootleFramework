using UnityEngine;
using TMPro;
using Sirenix.OdinInspector;

namespace Pootle
{
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Proximity Sign Label"), DefaultExecutionOrder(10100)]
    public sealed class PootleSignLabel : MonoBehaviour
    {
        public Transform player;
        public Camera viewCamera;
        [TextArea(1, 3)] public string message = "Trail ahead";
        [Min(0), Tooltip("Show the label when the player comes within this radius.")]
        public float showDistance = 4;
        [Min(0), Tooltip("Once shown, keep it visible until the player leaves this larger radius.")]
        public float exitDistance = 6;
        public Vector3 worldOffset = new Vector3(0, 3.2f, 0);
        public RectTransform labelRoot;
        public TMP_Text label;
        [ShowInInspector, ReadOnly] public bool IsVisible { get; private set; }
        public bool EvaluateVisibility(Vector3 playerPosition)
        {
            float radius = IsVisible ? Mathf.Max(showDistance, exitDistance) : Mathf.Max(0, showDistance);
            IsVisible = (playerPosition - transform.position).sqrMagnitude <= radius * radius;
            return IsVisible;
        }
        void LateUpdate()
        {
            if (!player || !labelRoot) { Hide(); return; }
            EvaluateVisibility(player.position);
            var camera = viewCamera ? viewCamera : Camera.main;
            bool visible = IsVisible && camera && Vector3.Dot(camera.transform.forward, transform.position + worldOffset - camera.transform.position) > 0;
            if (labelRoot.gameObject.activeSelf != visible) labelRoot.gameObject.SetActive(visible);
            if (!visible) return;
            labelRoot.SetPositionAndRotation(transform.position + worldOffset, camera.transform.rotation);
            if (label) label.text = message;
        }
        void OnDisable() => Hide();
        void Hide() { IsVisible = false; if (labelRoot) labelRoot.gameObject.SetActive(false); }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position, showDistance);
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, Mathf.Max(showDistance, exitDistance));
        }
    }
}
