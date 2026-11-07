using System;
using System.Collections.Generic;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Authoring data on an original source. Vertex IDs and bone names survive a skeleton rebake.</summary>
    [DisallowMultipleComponent]
    public sealed class RiggleMeshEdits : MonoBehaviour
    {
        [Serializable] public sealed class WeightEdit
        {
            public int vertex;
            public string[] bones;
            public float[] weights;
        }
        [Serializable] public sealed class VertexDelta
        {
            public int vertex;
            public Vector3 delta;
        }
        [Serializable] public sealed class Corrective
        {
            public string name;
            public string bone;
            public Vector3 axis;
            public Vector3 restDirection;
            public Quaternion restRotation = Quaternion.identity;
            public float startAngle = 20;
            public float fullAngle = 90;
            [Range(0,2)] public float strength = 1;
            public List<VertexDelta> deltas = new List<VertexDelta>();
        }
        [HideInInspector] public Mesh sourceMesh;
        [HideInInspector] public string topology;
        [HideInInspector] public List<WeightEdit> weights = new List<WeightEdit>();
        [HideInInspector] public List<Corrective> correctives = new List<Corrective>();
    }
}
