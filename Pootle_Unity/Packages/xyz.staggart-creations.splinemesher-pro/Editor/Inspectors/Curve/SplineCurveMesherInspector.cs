using System;
using System.Collections.Generic;
using UnityEditor;
using sc.splinemesher.pro.runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Splines;

namespace sc.splinemesher.pro.editor
{
    [CustomEditor(typeof(SplineCurveMesher))]
    public class SplineCurveMesherInspector : SplineMesherInspector
    {
        private SerializedProperty splineSampleDistance;
        private SerializedProperty autoSplineSampleDistance;
        
        private UI.Section rendererSection;
        private bool hasLightmapUV;
        private bool hasBrokenLightmap;
        
        new void OnEnable()
        {
            base.OnEnable();
            
            splineSampleDistance = serializedObject.FindProperty("splineSampleDistance");
            autoSplineSampleDistance = serializedObject.FindProperty("autoSplineSampleDistance");
            
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.InputMesh>(this, "InputMesh",  new GUIContent("Input Mesh", UI.Icons.Mesh), settings.FindPropertyRelative("input")));
            rendererSection = UI.Section.Create<SplineCurveSettingsEditor.Renderer>(this, "renderer", new GUIContent("Renderer", UI.Icons.Renderer), settings.FindPropertyRelative("renderer"));
            sections.Add(rendererSection);
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.Distribution>(this, "Distribution",  new GUIContent("Distribution", UI.Icons.Distribution), settings.FindPropertyRelative("distribution")));
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.Scale>(this, "Scale",  new GUIContent("Scale", UI.Icons.Scale), settings.FindPropertyRelative("scale")));
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.Rotation>(this, "Rotation",  new GUIContent("Rotation", UI.Icons.Roll), settings.FindPropertyRelative("rotation")));
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.UV>(this, "UV",  new GUIContent("UV", UI.Icons.UV), settings.FindPropertyRelative("uv")));
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.Color>(this, "Color",  new GUIContent("Vertex Colors", UI.Icons.VertexColors), settings.FindPropertyRelative("color")));
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.Conforming>(this, "Conforming",  new GUIContent("Conforming", UI.Icons.Conforming), settings.FindPropertyRelative("conforming")));
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.Collision>(this, "Collision",  new GUIContent("Collision", UI.Icons.Collision), settings.FindPropertyRelative("collision")));
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.OutputMesh>(this, "OutputMesh",  new GUIContent("Output", UI.Icons.GameObject), settings.FindPropertyRelative("output")));
            sections.Add(UI.Section.Create<SplineCurveSettingsEditor.Caps>(this, "Caps",  new GUIContent("Caps", UI.Icons.Cap), settings.FindPropertyRelative("caps")));

            Verify();
            
            base.Initialize();
        }

        private void Verify()
        {
            SplineCurveSettingsEditor.Renderer rendererEditor = (SplineCurveSettingsEditor.Renderer)sections[1].editor;
            missingMaterials = rendererEditor.HasMissingMaterials();

            hasLightmapUV = false;
            StaticEditorFlags staticFlags = GameObjectUtility.GetStaticEditorFlags(component.gameObject);
            //Mesh renderer marked as static
            if (staticFlags.HasFlag(StaticEditorFlags.ContributeGI))
            {
                foreach (var container in component.Containers)
                {
                    if (!container) continue;
                    foreach (var segment in container.Segments)
                    {
                        if (segment.mesh)
                        {
                            if (segment.mesh.HasVertexAttribute(VertexAttribute.TexCoord1))
                            {
                                hasLightmapUV = true;
                                return;
                            }
                        }
                    }
                }
            }
            
            SplineCurveMesher curveMesher = (SplineCurveMesher)component;
            hasBrokenLightmap = curveMesher.HasBrokenLightmap();
        }
        
        public override void OnInspectorGUI()
        {
            DrawHeader();
            
            SplineCurveMesher component = (SplineCurveMesher)target;
            
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            requiresRebuild = false;
            //base.OnInspectorGUI();

            if (component.InputMeshReadable() == false)
            {
                Mesh mesh = component.settings.input.mesh;
                
                string msg = $"For runtime use, the mesh \"{mesh.name}\" requires the Read/Write option enabled in its import settings";

                if (EditorUtility.IsPersistent(mesh) == false)
                {
                    msg +=
                        "\n\n For procedurally created geometry, use \"Mesh.UploadMeshData(false)\" when creating the mesh.";
                }
                EditorGUILayout.HelpBox(msg, MessageType.Error);
            }

            if (hasLightmapUV)
            {
                EditorGUILayout.HelpBox("This mesh has lightmap UV generated for it." +
                                        "\n\n" +
                                        "Altering it in any way will cause them to be invalidated until the next light bake starts again.", MessageType.Info);
            }

            if (hasBrokenLightmap)
            {
                EditorGUILayout.HelpBox("Mesh was previously lightmapped, then rebuild. Lightmap UVs is now invalid and lightmapping appears broken." +
                                        "\n\n" +
                                        "Rebake lighting to re-generate a new lightmap UV", MessageType.Warning);
            }
            
            DrawSplineContainerField();
            
            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(autoSplineSampleDistance.boolValue);
                EditorGUILayout.PropertyField(splineSampleDistance, new GUIContent("Accuracy", splineSampleDistance.tooltip), GUILayout.MaxWidth(UI.DropDownWidth - 85f));
                EditorGUI.EndDisabledGroup();
                
                autoSplineSampleDistance.boolValue = GUILayout.Toggle(autoSplineSampleDistance.boolValue, new GUIContent(" Auto", autoSplineSampleDistance.tooltip), "Button", GUILayout.MaxWidth(60f), GUILayout.MaxHeight(19f));
            }

            requiresRebuild |= EditorGUI.EndChangeCheck();
            EditorGUI.indentLevel--;
            
            if (splineContainer.objectReferenceValue || isInspectingPrefab)
            {
                DrawRebuildTriggers();
                
                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(root);
                    if (GUILayout.Button("This", EditorStyles.miniButton, GUILayout.Width(60f)))
                    {
                        root.objectReferenceValue = component.gameObject;
                        requiresRebuild = true;
                    }
                }

                if (root.objectReferenceValue)
                {
                    Transform rootTransform = root.objectReferenceValue as Transform;
                    
                    bool isUniformScale = Mathf.Abs(rootTransform.lossyScale.x - rootTransform.lossyScale.y) < 0.01f && Mathf.Abs(rootTransform.lossyScale.y - rootTransform.lossyScale.z) < 0.01f;
                    
                    if(!isUniformScale)
                    {
                        EditorGUILayout.HelpBox("Root transform has a non-uniform scale. Mesh's shading will be inaccurate.", MessageType.Warning);
                    }
                }
                
                if (missingMaterials)
                {
                    EditorGUILayout.HelpBox("One or materials are missing or have not been assigned", MessageType.Error);
                    //SwitchSection(sections[1]);
                }

                if (rendererSection.Expanded == false)
                {
                    PerformMaterialDragAndDrop(ref requiresRebuild);
                }
                
                EditorGUILayout.Separator();
                
                foreach (UI.Section section in sections)
                {
                    section.DrawHeader(() => SwitchSection(section));
                    EditorGUILayout.BeginFadeGroup(section.anim.faded);
                    {
                        if (section.Expanded)
                        {
                            EditorGUILayout.Space();
                            
                            section.DrawUI(ref requiresRebuild);
                            
                            EditorGUILayout.Space();
                        }
                    }
                    EditorGUILayout.EndFadeGroup();
                }

                EditorGUILayout.Space();
                
                DrawStats();
                
                //base.OnInspectorGUI();
            }

            if (component.RebuildTriggersEnabled(SplineMesher.RebuildTriggers.OnUIChange) == false)
                requiresRebuild = false;
            
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();

                if (requiresRebuild)
                {
                    if(component.RebuildTriggersEnabled(SplineMesher.RebuildTriggers.OnUIChange))
                    {
                        Rebuild();
                    }
                }

                Verify();
            }
            
            UI.DrawFooter();
        }
        
        public override void Rebuild()
        {
            requiresRebuild = false;

            if (SplineMesherSettings.RebuildEveryFrame)
            {
                RebuildTargets();
            }
            else
            {
                EditorApplication.delayCall += RebuildTargets;
            }
            
            Recalculate();
        }
        
        private void RebuildTargets()
        {
            if (isAllowedToRebuild == false) return;
            
            foreach (var m_target in targets)
            {
                SplineCurveMesher mesher = (SplineCurveMesher)m_target;
                
                //mesher.RebuildSplineCache();
                mesher.Rebuild();
                EditorUtility.SetDirty(mesher);
            }
        }

        public override SplineContainer CreateDefaultSpline()
        {
            GameObject gameObject = ((SplineCurveMesher)target).gameObject;
            SplineContainer container = SplineMesherEditor.AddSplineContainer(gameObject);
            container.Spline = SplineMesherEditor.CreateDefaultCurveSpline();

            return container;
        }

        private void SwitchSection(UI.Section targetSection)
        {
            if (SplineMesherSettings.SectionStyleMode == SplineMesherSettings.SectionStyle.Foldouts)
            {
                //Classic foldout behaviour
                targetSection.Expanded = !targetSection.Expanded;
            }
            else
            {
                //Accordion behaviour
                foreach (var section in sections)
                {
                    section.Expanded = (targetSection == section) && !section.Expanded;
                    //section.Expanded = true;
                }
            }
        }

        private void OnSceneGUI()
        {
            foreach (UI.Section section in sections)
            {
                if (section.Expanded)
                {
                    section.DrawSceneGUI();
                }
            }
        }

        new void OnDisable()
        {
            base.OnDisable();
            
            foreach (var section in sections)
            {
                section.Disable();
            }
        }
    }
}