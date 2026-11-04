using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace ProceduralPrimitivesUtil.PPEditor
{
    [CustomEditor(typeof(PPBase), true)]
    [CanEditMultipleObjects]
    public class PPBaseEditor : Editor
    {
        private static bool transformFold = true;
        private static bool colliderFold = true;
        private static bool mappingFold = true;
        private static bool basicFold = true;
        private static bool otherFold = true;
        private static bool segmentFold = true;
        private static bool sliceFold = true;
        private static bool editorFold = true;
        private static bool noiseFold = false; // Final Stage Noise foldout
        private static bool taperFold = false; // Final Taper foldout

        protected bool hasSegment = true;
        protected bool hasSmooth = false;

        protected SerializedProperty pivotOffset;
        protected SerializedProperty rotation;
        protected SerializedProperty generateMappingCoords;
        protected SerializedProperty realWorldMapSize;
        protected SerializedProperty UVOffset;
        protected SerializedProperty UVTiling;
        protected SerializedProperty smooth;
        protected SerializedProperty flipNormals;
        protected SerializedProperty autoUpdateCollider;

        private SerializedProperty vertexColor;

        // Taper properties
        private SerializedProperty finalTaperScale;
        private SerializedProperty finalTaperOffset;

        // Noise properties
        private SerializedProperty noiseEnabled;
        private SerializedProperty noiseSeed;
        private SerializedProperty noiseOctaves;
        private SerializedProperty noiseAmplitude;
        private SerializedProperty noiseFrequency;
        private SerializedProperty noisePersistence;
        private SerializedProperty noiseLacunarity;
        private SerializedProperty noiseHeightLacunarity;
        private SerializedProperty noiseScale;
        private SerializedProperty noiseDirectionMode;

        private void OnEnable()
        {
            Init();
        }

        protected virtual void Init()
        {
            vertexColor = serializedObject.FindProperty("vertexColor");

            pivotOffset = serializedObject.FindProperty("pivotOffset");
            rotation = serializedObject.FindProperty("rotation");

            generateMappingCoords = serializedObject.FindProperty("generateMappingCoords");
            realWorldMapSize = serializedObject.FindProperty("realWorldMapSize");
            UVOffset = serializedObject.FindProperty("UVOffset");
            UVTiling = serializedObject.FindProperty("UVTiling");

            smooth = serializedObject.FindProperty("smooth");
            flipNormals = serializedObject.FindProperty("flipNormals");
            autoUpdateCollider = serializedObject.FindProperty("autoUpdateCollider");

            // taper
            finalTaperScale  = serializedObject.FindProperty("finalTaperScale");
            finalTaperOffset = serializedObject.FindProperty("finalTaperOffset");

            // noise
            noiseEnabled           = serializedObject.FindProperty("noiseEnabled");
            noiseSeed              = serializedObject.FindProperty("noiseSeed");
            noiseOctaves           = serializedObject.FindProperty("noiseOctaves");
            noiseAmplitude         = serializedObject.FindProperty("noiseAmplitude");
            noiseFrequency         = serializedObject.FindProperty("noiseFrequency");
            noisePersistence       = serializedObject.FindProperty("noisePersistence");
            noiseLacunarity        = serializedObject.FindProperty("noiseLacunarity");
            noiseHeightLacunarity  = serializedObject.FindProperty("noiseHeightLacunarity");
            noiseScale             = serializedObject.FindProperty("noiseScale");
            noiseDirectionMode     = serializedObject.FindProperty("noiseDirectionMode");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // Transform
            transformFold = EditorGUILayout.BeginFoldoutHeaderGroup(transformFold, "Transform");
            if (transformFold) DrawTransform();
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Basic parameters
            basicFold = EditorGUILayout.BeginFoldoutHeaderGroup(basicFold, "Basic Parameters");
            if (basicFold)
            {
                // Utility button to set pivot to bottom using "height" if present
                if (GUILayout.Button("Set Pivot Offset Bottom"))
                {
                    var pp = (PPBase)target;
                    var newOffset = pp.pivotOffset;
                    // Guarded usage: only if the shape defines a "height" field
                    var heightProp = serializedObject.FindProperty("height");
                    if (heightProp != null)
                    {
                        newOffset.y = 0.5f * heightProp.floatValue;
                        pp.pivotOffset = newOffset;
                        pp.RefreshMesh();
                    }
                }
                DrawBasicParameters();
            }
            EditorGUILayout.PropertyField(vertexColor, new GUIContent("Vertex Color"));
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Segments
            if (hasSegment)
            {
                segmentFold = EditorGUILayout.BeginFoldoutHeaderGroup(segmentFold, "Segments");
                if (segmentFold) DrawSegments();
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            // Slice (circular shapes)
            if (target is PPCircularBase)
            {
                sliceFold = EditorGUILayout.BeginFoldoutHeaderGroup(sliceFold, "Slice");
                if (sliceFold) DrawSlice();
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            // Mapping / UVs
            mappingFold = EditorGUILayout.BeginFoldoutHeaderGroup(mappingFold, "Mapping Coordinates");
            if (mappingFold) DrawUVMapping();
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---------- Final Taper UI ----------
            taperFold = EditorGUILayout.BeginFoldoutHeaderGroup(taperFold, "Final Taper");
            if (taperFold)
            {
                EditorGUILayout.PropertyField(finalTaperScale,  new GUIContent("Taper Scale"));
                EditorGUILayout.PropertyField(finalTaperOffset, new GUIContent("Taper Offset"));

                EditorGUILayout.HelpBox(
                    "XZ scale factor = 1 + (y + Offset) * Scale.\n" +
                    "Use Scale = 0 to disable. Positive values flare out with height; negative values pinch.",
                    MessageType.None);

                if (GUILayout.Button("Refresh Mesh (Taper)"))
                {
                    foreach (PPBase pp in targets) pp.RefreshMesh();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            // -----------------------------------

            // ---------- Final Stage Noise UI ----------
            noiseFold = EditorGUILayout.BeginFoldoutHeaderGroup(noiseFold, "Final Stage Noise");
            if (noiseFold)
            {
                EditorGUILayout.PropertyField(noiseEnabled, new GUIContent("Enabled"));
                using (new EditorGUI.DisabledScope(!noiseEnabled.boolValue))
                {
                    EditorGUILayout.PropertyField(noiseDirectionMode, new GUIContent("Direction Mode"));
                    EditorGUILayout.PropertyField(noiseSeed, new GUIContent("Seed"));
                    EditorGUILayout.PropertyField(noiseOctaves, new GUIContent("Octaves"));
                    EditorGUILayout.PropertyField(noiseAmplitude, new GUIContent("Amplitude"));
                    EditorGUILayout.PropertyField(noiseFrequency, new GUIContent("Frequency"));
                    EditorGUILayout.PropertyField(noisePersistence, new GUIContent("Persistence"));
                    EditorGUILayout.PropertyField(noiseLacunarity, new GUIContent("Lacunarity"));
                    EditorGUILayout.PropertyField(noiseHeightLacunarity, new GUIContent("Height Lacunarity"));
                    EditorGUILayout.PropertyField(noiseScale, new GUIContent("Scale"));
                    EditorGUILayout.Space(4);
                    if (GUILayout.Button("Randomize Seed"))
                    {
                        noiseSeed.intValue = Random.Range(0, 100000);
                    }
                    if (GUILayout.Button("Refresh Mesh"))
                    {
                        foreach (PPBase pp in targets) pp.RefreshMesh();
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            // -----------------------------------------

            // Others
            otherFold = EditorGUILayout.BeginFoldoutHeaderGroup(otherFold, "Others");
            if (otherFold) DrawOthers();
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Collider
            colliderFold = EditorGUILayout.BeginFoldoutHeaderGroup(colliderFold, "Collider");
            if (colliderFold) DrawCollider();
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();

            // Editor Functions
            editorFold = EditorGUILayout.BeginFoldoutHeaderGroup(editorFold, "Editor Functions");
            if (editorFold) DrawEditorFunctions();
            EditorGUILayout.EndFoldoutHeaderGroup();

            // Apply live updates
            foreach (PPBase pp in targets)
            {
                if (pp.gameObject.scene.name == null) continue;
                if (pp.isDirty && pp.GetType() != typeof(PPCombiner)) pp.Apply();
            }
        }

        private void DrawTransform()
        {
            EditorGUILayout.PropertyField(pivotOffset);
            EditorGUILayout.PropertyField(rotation);
        }

        protected virtual void DrawBasicParameters() { }
        protected virtual void DrawSegments() { }
        protected virtual void DrawSlice() { }

        private void DrawUVMapping()
        {
            EditorGUILayout.PropertyField(generateMappingCoords);
            EditorGUILayout.PropertyField(realWorldMapSize);
            EditorGUILayout.PropertyField(UVOffset);
            EditorGUILayout.PropertyField(UVTiling);
        }

        protected virtual void DrawOthers()
        {
            EditorGUILayout.PropertyField(flipNormals);
            if (hasSmooth) EditorGUILayout.PropertyField(smooth);
        }

        private void DrawCollider()
        {
            if (targets.Length == 1)
            {
                Collider col = (target as PPBase).GetCollider();
                if (col == null)
                {
                    EditorGUILayout.HelpBox("No Collider found", MessageType.None);
                    PPBase pp = (PPBase)target;
                    if (GUILayout.Button("Add Box Collider"))
                    {
                        Collider collider = pp.AddBoxCollider();
                        Undo.RegisterCreatedObjectUndo(collider, "Add Box Collider");
                    }
                    if (GUILayout.Button("Add Sphere Collider"))
                    {
                        Collider collider = pp.AddSphereCollider();
                        Undo.RegisterCreatedObjectUndo(collider, "Add Sphere Collider");
                    }
                    if (GUILayout.Button("Add Capsule Collider"))
                    {
                        Collider collider = pp.AddCapsuleCollider();
                        Undo.RegisterCreatedObjectUndo(collider, "Add Capsule Collider");
                    }
                    if (GUILayout.Button("Add Mesh Collider"))
                    {
                        Collider collider = pp.AddMeshCollider();
                        Undo.RegisterCreatedObjectUndo(collider, "Add Mesh Collider");
                    }
                }
                else if (col is BoxCollider)
                {
                    EditorGUILayout.HelpBox("Found Box Collider", MessageType.None);
                }
                else if (col is SphereCollider)
                {
                    EditorGUILayout.HelpBox("Found Sphere Collider", MessageType.None);
                }
                else if (col is CapsuleCollider)
                {
                    EditorGUILayout.HelpBox("Found Capsule Collider", MessageType.None);
                }
                else if (col is MeshCollider)
                {
                    EditorGUILayout.HelpBox("Found Mesh Collider", MessageType.None);
                }
                else
                {
                    EditorGUILayout.HelpBox("Collider not supported", MessageType.None);
                }
            }
            EditorGUILayout.PropertyField(autoUpdateCollider);
        }

        private void DrawEditorFunctions()
        {
            if (targets.Length == 1)
            {
                if (target is PPCombiner)
                {
                    if (GUILayout.Button("Refresh Mesh")) (target as PPCombiner).Apply();
                }

                PPBase pp = (PPBase)target;
                EditorGUILayout.HelpBox("Mesh asset will be saved under \"Assets/Procedural Primitives\" folder", MessageType.None);
                if (GUILayout.Button("Save Mesh")) SaveMesh(pp);
            }
        }

        private void SaveMesh(PPBase pp)
        {
            string folderPath = "Assets/Procedural Primitives";
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string guid = AssetDatabase.CreateFolder("Assets", "Procedural Primitives");
                folderPath = AssetDatabase.GUIDToAssetPath(guid);
            }
            string assetName = pp.mesh.name.Trim();
            string assetPath = folderPath + "/" + assetName + ".asset";
            int counter = 1;
            while (File.Exists(assetPath))
            {
                counter++;
                assetPath = folderPath + "/" + assetName + "_" + counter.ToString() + ".asset";
            }
            AssetDatabase.CreateAsset(pp.mesh, assetPath);
            pp.RefreshMesh();
        }
    }
}
