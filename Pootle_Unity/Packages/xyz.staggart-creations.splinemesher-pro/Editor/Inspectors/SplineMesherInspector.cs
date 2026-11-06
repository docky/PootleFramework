using System;
using System.Collections.Generic;
using sc.splinemesher.pro.runtime;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.SceneManagement;
using UnityEditor.Splines;
using UnityEngine;
using UnityEngine.Splines;
using Object = UnityEngine.Object;

namespace sc.splinemesher.pro.editor
{
    public class SplineMesherInspector : Editor
    {
        protected SplineMesher component;
        
        protected SerializedProperty splineContainer;
        protected SerializedProperty splineChangeTrigger;
        protected SerializedProperty root;
        protected SerializedProperty rebuildTriggers;
        
        protected SerializedProperty settings;
        
        internal int vertexCount;
        internal int triangleCount;
        internal string memorySize;
        
        internal bool isAllowedToRebuild;
                
        internal bool isPrefab;
        internal bool isInspectingPrefab;
        internal bool missingMaterials;

        internal bool requiresRebuild;
        
        internal readonly List<UI.Section> sections = new List<UI.Section>();
        internal UI.Section eventsSection;

        public void OnEnable()
        {
            splineContainer = serializedObject.FindProperty("splineContainer");
            splineChangeTrigger = serializedObject.FindProperty("splineChangeTrigger");
            root = serializedObject.FindProperty("root");
            rebuildTriggers = serializedObject.FindProperty("rebuildTriggers");
            
            settings = serializedObject.FindProperty("settings");
            
            component = (SplineMesher)target;
        }

        public void Initialize()
        {
            eventsSection = UI.Section.Create<Events>(this, "Events",  new GUIContent("Events", UI.Icons.Event), null);
            sections.Add(eventsSection);
            
            isPrefab = PrefabUtility.IsPartOfPrefabInstance(target)|| PrefabStageUtility.GetCurrentPrefabStage();
            isInspectingPrefab = PrefabUtility.IsPartOfPrefabAsset(this.target) && this.component.gameObject.scene.name != string.Empty;

            Recalculate();
        }
        
        internal new void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.FlexibleSpace();

                UI.DrawHeader();

                SplineMesher component = (SplineMesher)target;
                component.drawWireFrame = GUILayout.Toggle(component.drawWireFrame, new GUIContent("", EditorGUIUtility.IconContent("d_MainStageView").image, "Toggle wire frame display"), "MiniButtonLeft", GUILayout.Width(30f), GUILayout.Height(22f));

                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent(UI.Icons.prefix + "Settings").image, "Utility functions"), EditorStyles.miniButtonMid, GUILayout.Width(30f), GUILayout.Height(22f)))
                {
                    GenericMenu menu = new GenericMenu();
                    
                    menu.AddItem(new GUIContent("Open preferences"), false, SplineMesherSettings.OpenPreferences);
                    menu.AddItem(new GUIContent("Open project settings"), false, SplineMesherSettings.OpenProjectSettings);
                    
                    menu.AddSeparator(string.Empty);

                    if (component.GetType() == typeof(SplineCurveMesher))
                    {
                        SplineCurveMesher curveMesher = (SplineCurveMesher)component;
                        
                        menu.AddItem(new GUIContent("Clear Scale data"), false, () =>
                        {
                            curveMesher.ResetScaleData();
                            curveMesher.Rebuild();
                            EditorUtility.SetDirty(component);
                        });
                        menu.AddItem(new GUIContent("Clear Roll data"), false, () =>
                        {
                            curveMesher.ResetRollData();
                            curveMesher.Rebuild();
                            EditorUtility.SetDirty(component);
                        });
                        menu.AddItem(new GUIContent("Clear Vertex Color data"), false, () =>
                        {
                            curveMesher.ResetVertexColorData();
                            curveMesher.Rebuild();
                            EditorUtility.SetDirty(component);
                        });
                        menu.AddItem(new GUIContent("Clear Conforming data"), false, () =>
                        {
                            curveMesher.ResetConformingData();
                            curveMesher.Rebuild();
                            EditorUtility.SetDirty(component);
                        });

                        menu.AddSeparator(string.Empty);

                        menu.AddItem(new GUIContent("Detach caps", "Detach the created start/end cap objects"), false, () =>
                        {
                            curveMesher.DetachCaps();
                            EditorUtility.SetDirty(component);
                        });
                        
                        menu.AddItem(new GUIContent("Force lightmap generation"), false, () => SplineMesherEditor.GenerateLightmapUV(curveMesher));
                    }
                    //if(SplineMesherEditor.meshLodSupport) 
                        menu.AddItem(new GUIContent("Generate MeshLOD"), false, () => SplineMesherEditor.GenerateLODS(component));
                    
                    menu.ShowAsContext();
                }
                
                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent(UI.Icons.prefix + "Help").image, "Help window"), EditorStyles.miniButtonMid, GUILayout.Width(30f)))
                {
                    HelpWindow.ShowWindow();
                }
            }
            
            EditorGUILayout.Space(AssetInfo.VersionChecking.UPDATE_AVAILABLE && !SplineMesherSettings.HideUpdateNotice ? 12f : 0f);
            
            if (Unity.Burst.BurstCompiler.IsEnabled == false)
            {
                EditorGUILayout.HelpBox("Burst compilation is disabled, expect performance degradation", MessageType.Warning);
                EditorGUILayout.Separator();
            }
            
            if (isAllowedToRebuild == false)
            {
                EditorGUILayout.HelpBox("\nRebuilding disabled. This object is not a scene instance." +
                                        "\n\nOpen the prefab to edit it.\n", MessageType.Info);
                
                GUILayout.Space(-32);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(
                            new GUIContent("Open", EditorGUIUtility.IconContent("d_tab_next").image), GUILayout.Width(60f), GUILayout.Height(22f)))
                    {
                        SplineMesherEditor.OpenPrefab(component);
                    }

                    GUILayout.Space(8);
                }

                GUILayout.Space(11);
            }
            
            EditorGUILayout.Space();
        }

        internal void DrawSplineContainerField()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(splineContainer);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorApplication.delayCall += () =>
                    {
                        foreach (var m_target in targets)
                        {
                            ((SplineMesher)m_target).RebuildSplineCache();
                            ((SplineMesher)m_target).Rebuild();
                        }
                    };

                    requiresRebuild = true;
                }
                
                if (splineContainer.objectReferenceValue)
                {
                    var splineToolActive = ToolManager.activeContextType != null && ToolManager.activeContextType == typeof(SplineToolContext);
                    
                    EditorGUI.BeginChangeCheck();
                    GUILayout.Toggle(splineToolActive, new GUIContent("Edit", "Toggle Spline Editor"), "Button", GUILayout.MaxWidth(60f));

                    if (EditorGUI.EndChangeCheck())
                    {
                        if (!splineToolActive)
                        {
                            Selection.activeGameObject = ((SplineMesher)target).SplineContainer.gameObject;
                            EditorApplication.delayCall += ToolManager.SetActiveContext<SplineToolContext>;
                        }
                        else ToolManager.SetActiveContext<GameObjectToolContext>();
                    }
                }
            }

            if (!splineContainer.objectReferenceValue)
            {
                EditorGUILayout.Separator();
                EditorGUILayout.HelpBox("Assign a Spline Container, or create one here", MessageType.Info);
                
                GUILayout.Space(-32);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button(
                            new GUIContent("Create", EditorGUIUtility.IconContent("d_tab_next").image), GUILayout.Width(120f), GUILayout.Height(22f)))
                    {
                        splineContainer.objectReferenceValue = CreateDefaultSpline();
                        
                        foreach (var m_target in targets)
                        {
                            ((SplineMesher)m_target).RebuildSplineCache();
                        }
                        requiresRebuild = true;
                    }

                    GUILayout.Space(8);
                }

                GUILayout.Space(11);
            }
        }

        internal void DrawRebuildTriggers()
        {
            int rebuildTrigger = rebuildTriggers.intValue;
            bool IsTriggerEnabled(SplineMesher.RebuildTriggers trigger) => (rebuildTrigger & (int)trigger) == (int)trigger;
                        
            using (new EditorGUI.DisabledGroupScope(IsTriggerEnabled(SplineMesher.RebuildTriggers.OnSplineChanged) == false))
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(splineChangeTrigger, new GUIContent("Change Trigger", splineChangeTrigger.tooltip), GUILayout.MaxWidth(UI.DropDownWidth));
                EditorGUI.indentLevel--;
            }
                
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(rebuildTriggers, new GUIContent("Rebuild triggers", rebuildTriggers.tooltip), GUILayout.Width(UI.DropDownWidth));
                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }
                
                if (GUILayout.Button(new GUIContent(" Rebuild", EditorGUIUtility.IconContent("d_Refresh").image), GUILayout.MaxHeight(19f)))
                {
                    Rebuild();
                    return;
                }
                GUILayout.FlexibleSpace();
            }
                
            rebuildTrigger = rebuildTriggers.intValue;
            
            if (IsTriggerEnabled(SplineMesher.RebuildTriggers.OnTransformChange))
            {
                //Check if Gizmos are disabled in the scene-view
                if (SceneView.lastActiveSceneView && SceneView.lastActiveSceneView.drawGizmos == false)
                {
                    EditorApplication.delayCall += () =>
                    {
                        foreach (var m_target in targets)
                        {
                            ((SplineMesher)m_target).ListenForTransformChanges();
                        }
                    };
                } 
            }
            
            if (IsTriggerEnabled(SplineMesher.RebuildTriggers.OnLoad) == false && isPrefab && !SplineMesherSettings.HidePrefabWarning)
            {
                EditorGUILayout.HelpBox("\nProcedurally created geometry cannot be used with prefabs." +
                                        "\n\nMesh data will be lost when the prefab is used outside of the scene it was created in." +
                                        "\n\nExport the created meshes to an FBX file, and use those instead. Or enable the \"On Load\" option under Rebuild Triggers.\n", MessageType.Warning);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Don't show again", EditorStyles.miniButton, GUILayout.Width(120f), GUILayout.Height(22f)))
                    {
                        SplineMesherSettings.HidePrefabWarning = true;
                        
                        if(EditorUtility.DisplayDialog("Spline Mesher", "The warning can be restored in your Preferences.", "Show me","OK"))
                        {
                            SplineMesherSettings.OpenPreferences();
                        }
                    }
                }            
            }

            if (!IsTriggerEnabled(SplineMesher.RebuildTriggers.OnSplineAdded) ||
                !IsTriggerEnabled(SplineMesher.RebuildTriggers.OnSplineChanged) ||
                !IsTriggerEnabled(SplineMesher.RebuildTriggers.OnSplineRemoved))
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField(
                        "One or more spline rebuild triggers are disabled.\n\n" +
                        "Use the <b>AddCachedSpline</b>, <b>UpdateCachedSpline</b> and <b>RemoveCacheSpline</b> functions to manage spline changes manually through scripting.",
                        UI.Styles.WordWrappedRich);
                }

                if (GUILayout.Button("Force Spline Cache Rebuild", EditorStyles.miniButton, GUILayout.Width(220f),
                        GUILayout.Height(22f)))
                {
                    component.RebuildSplineCache();
                }
            }
        }

        public virtual void Rebuild()
        {
            
        }
        
        public virtual SplineContainer CreateDefaultSpline()
        {
            return null;
        }
        
        public void PerformMaterialDragAndDrop(ref bool changed)
        {
            Event currentEvent = Event.current;
            
            if(DragAndDrop.objectReferences.Length == 0) return;
            
            List<Material> materials = new List<Material>();
            foreach (Object draggedObject in DragAndDrop.objectReferences)
            {
                if (draggedObject is Material material)
                {
                    materials.Add(material);
                }
            }

            //Only accept dragging materials for this action, otherwise dragging other object types is hindered
            if (materials.Count == 0) return;
            
            Rect activeArea = GUILayoutUtility.GetRect(0, 50f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            
            GUIContent content = new GUIContent("+ Drag & drop material(s) to assign", EditorGUIUtility.IconContent("Material Icon").image);
            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!activeArea.Contains(currentEvent.mousePosition))
                    {
                        return;
                    }

                    DragAndDrop.visualMode = DragAndDropVisualMode.Link;

                    if (currentEvent.type == EventType.DragPerform)
                    {
                        if(DragAndDrop.objectReferences.Length > 0) Undo.RecordObjects(targets, "Drop & Drop material(s) into mesher");
                        
                        foreach (var m_target in targets)
                        {
                            SplineMesher mesher = (SplineMesher)m_target;
                            mesher.AssignMaterials(materials.ToArray());
                            
                            EditorUtility.SetDirty(mesher);
                        }
                        
                        changed = true;
                        
                        if(changed) DragAndDrop.AcceptDrag();
                        
                        DragAndDrop.activeControlID = 0;
                        Event.current.Use();
                    }

                    break;
            }
            
            GUI.Box(activeArea, GUIContent.none, EditorStyles.textArea);
            activeArea.y += 10f;
            activeArea.height = 30f;
            GUI.Label(activeArea, content, EditorStyles.centeredGreyMiniLabel);
            
            EditorGUILayout.Separator();
        }

        private static bool _expandMaterialEditor;
        private static bool expandMaterialEditor
        {
            get => SessionState.GetBool("SplineMesherInspector_expandMaterialEditor", true);
            set => SessionState.SetBool("SplineMesherInspector_expandMaterialEditor", value);
        }
        
        // Cache material inspectors so we don't recreate them every repaint
        private static readonly Dictionary<int, MaterialEditor> s_MaterialEditors = new Dictionary<int, MaterialEditor>();
        
        public static void DrawMaterialQuickEditor(SerializedProperty materials)
        {
            var isArray = materials.isArray;

            if (isArray && materials.arraySize == 0)
            {
                EditorGUILayout.PropertyField(materials);
                return;
            }
            
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PropertyField(materials);
                
                expandMaterialEditor = GUILayout.Toggle(expandMaterialEditor, new GUIContent(expandMaterialEditor ? "▲" : "▼", "Toggle material editor"), "Button", GUILayout.MaxWidth(40f));
            }
            
            if (expandMaterialEditor)
            {
                if (isArray)
                {
                    //Draw a material editor for every material in the property's array
                    int count = materials.arraySize;
                    if (count == 0)
                    {
                        EditorGUILayout.HelpBox("Assign at least one material to edit it here.", MessageType.Info);
                        return;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        var element = materials.GetArrayElementAtIndex(i);
                        var mat = element.objectReferenceValue as Material;

                        if (!mat)
                        {
                            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                            {
                                EditorGUILayout.LabelField($"Element {i}: Missing Material", EditorStyles.miniLabel);
                            }
                            continue;
                        }

                        DrawInlineMaterialEditor(mat);
                        if (i < count - 1) EditorGUILayout.Space();
                    }
                }
                else
                {
                    //Draw a material editor for the single material reference
                    var mat = materials.objectReferenceValue as Material;
                    if (!mat)
                    {
                        EditorGUILayout.HelpBox("Assign a material to edit it here.", MessageType.Info);
                        return;
                    }

                    DrawInlineMaterialEditor(mat);
                }
            }
        }
        
        private static void DrawInlineMaterialEditor(Material mat)
        {
            if (!mat) return;

            void CleanupNullMaterialEditors()
            {
                if (s_MaterialEditors.Count == 0) return;

                // Collect first to avoid modifying dictionary while iterating
                List<int> toRemove = null;
                foreach (var kvp in s_MaterialEditors)
                {
                    if (kvp.Value == null)
                    {
                        toRemove ??= new List<int>(2);
                        toRemove.Add(kvp.Key);
                    }
                }

                if (toRemove == null) return;

                for (int i = 0; i < toRemove.Count; i++)
                {
                    s_MaterialEditors.Remove(toRemove[i]);
                }
            }
            CleanupNullMaterialEditors();

#if UNITY_6000_4_OR_NEWER
            int id = mat.GetHashCode();
#else
            int id = mat.GetInstanceID();
#endif
            s_MaterialEditors.TryGetValue(id, out var materialEditor);

            // Create (or refresh) the cached editor for this material
            Editor cached = materialEditor;
            MaterialEditor.CreateCachedEditor(mat, null, ref cached);
            materialEditor = cached as MaterialEditor;
            s_MaterialEditors[id] = materialEditor;

            if (materialEditor == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUI.indentLevel++;
                materialEditor.DrawHeader();
                if (materialEditor.isVisible)
                {
                    if (mat.shader.isSupported)
                    {
                        EditorGUI.BeginChangeCheck();
                        materialEditor.PropertiesGUI();
                        if (EditorGUI.EndChangeCheck())
                        {
                            materialEditor.PropertiesChanged();
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Material is missing a shader or uses an unsupported one.", MessageType.Error);
                    }
                }
                EditorGUI.indentLevel--;
            }
        }

        public static void CleanupMaterialEditors()
        {
            foreach (var kvp in s_MaterialEditors)
            {
                if (kvp.Value == null)
                {
                    kvp.Value.OnDisable();
                    s_MaterialEditors.Remove(kvp.Key);
                }
            }
            s_MaterialEditors.Clear();
        }
        
        internal void Recalculate()
        {
            isAllowedToRebuild = component.IsAllowedToRebuild();
            //Debug.Log("Recalulate");
        }

        internal void DrawStats()
        {
            memorySize = Utilities.FormatMemorySize(component.CalculateMemorySize());
            component.CountMeshVertTris(out vertexCount, out triangleCount);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(new GUIContent($" Generation: {component.LastProcessingTime:F}ms", EditorGUIUtility.IconContent("Profiler.GPU").image), EditorStyles.miniLabel);
                EditorGUILayout.LabelField(new GUIContent($"  Vertices: {vertexCount:N0}", EditorGUIUtility.IconContent("d_EditCollider").image), EditorStyles.miniLabel);
                EditorGUILayout.LabelField(new GUIContent($" Triangles: {triangleCount:N0}", EditorGUIUtility.IconContent("d_ProfilerColumn.WarningCount").image), EditorStyles.miniLabel);
                EditorGUILayout.LabelField(new GUIContent($" Size: {memorySize}", EditorGUIUtility.IconContent("Profiler.Memory").image), EditorStyles.miniLabel);
            }
        }

        public void OnDisable()
        {
            //Dispose of spline cache when leaving the inspector
            component.Dispose();
        }

        public class Events : UI.Section.SectionEditor
        {
            private SerializedProperty rebuildWith;
            private SerializedProperty onPreRebuild, onPostRebuild;
            private SerializedProperty onCollisionEnter, onCollisionExit;
            private SerializedProperty onTriggerEnter, onTriggerStay, onTriggerExit;

            private bool isFillMesher;
            
            public override void OnEnable()
            {
                SplineMesher component = (SplineMesher)target;
                isFillMesher = component.GetType() == typeof(SplineFillMesher);
                
                rebuildWith = serializedObject.FindProperty("rebuildWith");
                onPreRebuild = serializedObject.FindProperty("onPreRebuild");
                onPostRebuild = serializedObject.FindProperty("onPostRebuild");
                
                onCollisionEnter = serializedObject.FindProperty("onCollisionEnter");
                onCollisionExit = serializedObject.FindProperty("onCollisionExit");
                
                onTriggerEnter = serializedObject.FindProperty("onTriggerEnter");
                onTriggerStay = serializedObject.FindProperty("onTriggerStay");
                onTriggerExit = serializedObject.FindProperty("onTriggerExit");
            }

            public override void OnInspectorGUI(ref bool changed)
            {
                EditorGUI.BeginChangeCheck();
                
                EditorGUILayout.PropertyField(rebuildWith);
                if (rebuildWith.isExpanded)
                {
                    EditorGUILayout.HelpBox("Rebuild this instance whenever any of the assigned other Spline Meshers rebuild", MessageType.Info);
                }
                
                EditorGUILayout.Separator();
                
                EditorGUILayout.LabelField("Mesh rebuilding", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.textArea))
                {
                    EditorGUILayout.PropertyField(onPreRebuild);
                    EditorGUILayout.PropertyField(onPostRebuild);
                }

                EditorGUILayout.Separator();
                
                EditorGUILayout.LabelField("Collision", EditorStyles.boldLabel);
                using (new EditorGUILayout.VerticalScope(EditorStyles.textArea))
                {
                    EditorGUILayout.PropertyField(onCollisionEnter);
                    EditorGUILayout.PropertyField(onCollisionExit);
                }

                //Mesh colliders do not support triggers
                if (isFillMesher == false)
                {
                    EditorGUILayout.Separator();

                    EditorGUILayout.LabelField("Physics Triggers", EditorStyles.boldLabel);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.textArea))
                    {
                        EditorGUILayout.PropertyField(onTriggerEnter);
                        EditorGUILayout.PropertyField(onTriggerStay);
                        EditorGUILayout.PropertyField(onTriggerExit);
                    }
                }

                changed |= EditorGUI.EndChangeCheck();
            }
        }
    }
    
    [CustomEditor(typeof(SplineMesher), false)]
    public class SplineMesherBaseInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("This is a base class for Spline Mesher components. It is not intended to be used directly.", MessageType.Error);
        }
    }
    
    [CustomEditor(typeof(SplineMeshContainer))]
    public class SplineMeshContainerInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.FlexibleSpace();
                UI.DrawHeader();
            }
            
            EditorGUILayout.Space();
            
            SplineMeshContainer component = (SplineMeshContainer)target;

            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.ObjectField("Owner", component.Owner, typeof(GameObject), true);
            }
            
            EditorGUILayout.Space();
            
            EditorGUILayout.HelpBox($"This component is created to contain the {component.SegmentCount} mesh segments for spline #{component.SplineIndex}", MessageType.Info);
            
            UI.DrawFooter();
        }
    }
}