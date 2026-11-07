using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Editor
{
    // View state and baked display geometry belong to the inspector, never the rig or its assets.
    internal sealed class RiggleInfluenceView : IDisposable
    {
        readonly Riggle rig;
        readonly Action repaint;
        readonly Mesh display = new Mesh { name = "Riggle influence preview", hideFlags = HideFlags.HideAndDontSave };
        readonly List<Vector3> vertices = new List<Vector3>();
        string search = "";
        bool xray, showZero = true;
        bool active, previousToolsHidden;
        internal bool Active
        {
            get => active;
            set
            {
                if (active == value) return;
                active = value;
                if (active) { previousToolsHidden = Tools.hidden; Tools.hidden = true; }
                else Tools.hidden = previousToolsHidden;
            }
        }
        internal Transform Selected { get; set; }
        internal RiggleInfluenceView(Riggle rig, Action repaint) { this.rig = rig; this.repaint = repaint; }
        public void Dispose() { Active = false; Object.DestroyImmediate(display); }
        internal SkinnedMeshRenderer[] Skins => rig.BakedSources.Where(s => s != null && s.baked && s.baked.sharedMesh).Select(s => s.baked).Distinct().ToArray();
        internal Transform[] Bones => Skins.SelectMany(s => s.bones).Where(b => b).Distinct().OrderBy(b => b.name, StringComparer.OrdinalIgnoreCase).ToArray();
        internal static float Influence(BoneWeight weight, int index)
        {
            if (index < 0) return 0;
            return (weight.boneIndex0 == index ? weight.weight0 : 0) + (weight.boneIndex1 == index ? weight.weight1 : 0) +
                (weight.boneIndex2 == index ? weight.weight2 : 0) + (weight.boneIndex3 == index ? weight.weight3 : 0);
        }
        internal static Color WeightColor(float weight) => Color.Lerp(new Color(.08f, .3f, 1, .8f), new Color(1, .12f, .03f, 1), Mathf.Clamp01(weight));
        internal static float[] Weights(SkinnedMeshRenderer skin, Transform bone)
        {
            int index = Array.IndexOf(skin.bones, bone);
            return skin.sharedMesh.boneWeights.Select(w => Influence(w, index)).ToArray();
        }
        void Choose(Transform bone) { Selected = bone; SceneView.RepaintAll(); repaint?.Invoke(); }
        public bool Draw()
        {
            bool next = EditorGUILayout.ToggleLeft("View bone influences (read only)", Active, EditorStyles.boldLabel);
            if (next != Active) { Active = next; SceneView.RepaintAll(); }
            if (!Active) return false;
            EditorGUILayout.HelpBox("Survey the generated bones without painting or changing the pose. Choose a bone below or click a bone dot in the Scene view. Collapse this view to return to authoring.", MessageType.Info);
            var skins = Skins; var bones = Bones;
            if (bones.Length == 0) { EditorGUILayout.HelpBox("Bake the character first to inspect its bone influences.", MessageType.Info); return true; }
            if (!Selected || !bones.Contains(Selected)) Selected = bones[0];
            EditorGUI.BeginChangeCheck();
            search = EditorGUILayout.TextField("Find bone", search);
            var visible = bones.Where(b => b.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            if (visible.Length > 0)
            {
                if (!visible.Contains(Selected)) Selected = visible[0];
                int index = Array.IndexOf(visible, Selected);
                int chosen = EditorGUILayout.Popup("Bone", index, visible.Select(b => b.name.Replace("Riggle_", "")).ToArray());
                Selected = visible[chosen];
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Previous bone")) Choose(visible[(chosen + visible.Length - 1) % visible.Length]);
                    if (GUILayout.Button("Next bone")) Choose(visible[(chosen + 1) % visible.Length]);
                    if (GUILayout.Button("Frame bone") && SceneView.lastActiveSceneView)
                        SceneView.lastActiveSceneView.Frame(new Bounds(Selected.position, Vector3.one * .4f), false);
                }
            }
            else EditorGUILayout.HelpBox("No matching bones. Clear the search to see the full skeleton.", MessageType.None);
            showZero = EditorGUILayout.Toggle("Show zero influence", showZero);
            xray = EditorGUILayout.Toggle("See through mesh", xray);
            if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
            EditorGUILayout.LabelField("Selected", Selected.name.Replace("Riggle_", ""));
            int affected = 0, total = 0, meshCount = 0; float maximum = 0;
            foreach (var skin in skins)
            {
                var weights = Weights(skin, Selected); int count = weights.Count(w => w > .0001f);
                affected += count; total += weights.Length; if (count > 0) meshCount++;
                foreach (float w in weights) maximum = Mathf.Max(maximum, w);
            }
            EditorGUILayout.LabelField("Influence", $"{affected:N0} / {total:N0} vertices · {meshCount} meshes");
            EditorGUILayout.LabelField("Highest weight", maximum.ToString("P0"));
            EditorGUILayout.HelpBox("Vertex dots: blue = 0%, red = 100%. Yellow marks the selected bone. Dots are sampled on dense meshes; counts include every vertex. Hats and other bone-parented attachments have no skin weights.", MessageType.None);
            return true;
        }
        public bool SceneGUI()
        {
            if (!Active) return false;
            var bones = Bones;
            if (!Selected && bones.Length > 0) Selected = bones[0];
            var evt = Event.current;
            if (evt.type == EventType.KeyDown && evt.keyCode == KeyCode.Escape)
            { Active = false; evt.Use(); SceneView.RepaintAll(); repaint?.Invoke(); return true; }
            Color previousColor = Handles.color; var previousTest = Handles.zTest;
            try
            {
                Handles.zTest = xray ? CompareFunction.Always : CompareFunction.LessEqual;
                if (evt.type == EventType.Repaint && Selected)
                {
                    var skins = Skins; int total = skins.Sum(s => s.sharedMesh.vertexCount);
                    int stride = Mathf.Max(1, Mathf.CeilToInt(total / 6000f));
                    foreach (var skin in skins)
                    {
                        skin.BakeMesh(display, true); display.GetVertices(vertices); var weights = Weights(skin, Selected);
                        for (int i = 0; i < Mathf.Min(vertices.Count, weights.Length); i += stride)
                        {
                            if (!showZero && weights[i] <= .0001f) continue;
                            var point = skin.transform.TransformPoint(vertices[i]);
                            Handles.color = WeightColor(weights[i]);
                            Handles.DotHandleCap(0, point, Quaternion.identity, HandleUtility.GetHandleSize(point) * .018f, EventType.Repaint);
                        }
                    }
                }
                // Skeleton picks change only this viewer's selection, never Unity's selected Transform.
                Handles.zTest = CompareFunction.Always;
                var set = new HashSet<Transform>(bones);
                foreach (var bone in bones)
                {
                    Handles.color = bone == Selected ? Color.yellow : new Color(.25f, .9f, .9f, .65f);
                    if (bone.parent && set.Contains(bone.parent)) Handles.DrawLine(bone.parent.position, bone.position);
                    float size = HandleUtility.GetHandleSize(bone.position) * (bone == Selected ? .055f : .025f);
                    if (Handles.Button(bone.position, Quaternion.identity, size, size, Handles.SphereHandleCap)) { search = ""; Choose(bone); }
                }
                if (Selected) Handles.Label(Selected.position, Selected.name.Replace("Riggle_", ""));
            }
            finally { Handles.color = previousColor; Handles.zTest = previousTest; }
            return true;
        }
    }
}
