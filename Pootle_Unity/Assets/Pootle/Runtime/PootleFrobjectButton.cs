using UnityEngine;
using UnityEngine.Events;

namespace Pootle
{
    [DisallowMultipleComponent, RequireComponent(typeof(PootleFrobject)), AddComponentMenu("Pootle/Frobject/Push Button")]
    public sealed class PootleFrobjectButton : MonoBehaviour
    {
        public Transform cap;
        public Renderer lamp;
        public Vector3 pressedOffset = Vector3.down * .1f;
        public Color offColor = new Color(.3f, .4f, .45f), onColor = new Color(.3f, 1, .55f);
        public UnityEvent<bool> onValueChanged = new UnityEvent<bool>();
        public bool Value { get; private set; }
        PootleFrobject target;
        Vector3 rest;
        float releaseAt;
        MaterialPropertyBlock properties;
        void Awake() { target = GetComponent<PootleFrobject>(); if (cap) rest = cap.localPosition; properties = new MaterialPropertyBlock(); }
        void OnEnable() { target.Interacted += Press; Paint(); }
        void OnDisable() { target.Interacted -= Press; if (cap) cap.localPosition = rest; }
        void Press(PootleFrobjectSensor sensor)
        {
            Value = !Value; releaseAt = Time.time + .18f;
            if (cap) cap.localPosition = rest + pressedOffset;
            Paint(); onValueChanged.Invoke(Value);
        }
        void Update() { if (cap && Time.time >= releaseAt) cap.localPosition = rest; }
        void Paint()
        {
            if (!lamp) return;
            lamp.GetPropertyBlock(properties); properties.SetColor("_BaseColor", Value ? onColor : offColor);
            properties.SetColor("_Color", Value ? onColor : offColor); lamp.SetPropertyBlock(properties);
        }
    }
}
