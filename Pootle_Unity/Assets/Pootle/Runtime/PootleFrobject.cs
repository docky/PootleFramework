using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Pootle
{
    /// <summary>A discoverable interaction endpoint; no collider or presentation is required.</summary>
    [DisallowMultipleComponent, AddComponentMenu("Pootle/Frobject/Interactable")]
    public sealed class PootleFrobject : MonoBehaviour
    {
        internal static readonly List<PootleFrobject> Active = new List<PootleFrobject>();
        public Transform interactionPoint;
        public Vector3 localPoint = Vector3.up;
        public bool interactable = true;
        [Min(0)] public float cooldown = .25f;
        public UnityEvent onInteract = new UnityEvent();
        public event Action<PootleFrobjectSensor> Interacted;
        public Vector3 Position => interactionPoint ? interactionPoint.position : transform.TransformPoint(localPoint);
        public bool Available => isActiveAndEnabled && interactable && Time.time >= nextInteraction;
        float nextInteraction = float.NegativeInfinity;

        void OnEnable() { if (!Active.Contains(this)) Active.Add(this); nextInteraction = float.NegativeInfinity; }
        void OnDisable() => Active.Remove(this);

        internal bool InvokeInteraction(PootleFrobjectSensor sensor)
        {
            if (!Available) return false;
            // Latch before callbacks, which may disable/destroy this object or try to re-enter.
            nextInteraction = Time.time + Mathf.Max(0, cooldown);
            Interacted?.Invoke(sensor);
            onInteract.Invoke();
            return true;
        }
    }
}
