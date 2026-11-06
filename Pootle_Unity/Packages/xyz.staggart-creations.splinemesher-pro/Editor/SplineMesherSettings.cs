using System.Collections.Generic;
using System.Linq;
using sc.splinemesher.pro.runtime;
using UnityEditor;
using UnityEngine;

namespace sc.splinemesher.pro.editor
{
    [FilePath("ProjectSettings/SplineMesher.asset", FilePathAttribute.Location.ProjectFolder)]
    public class SplineMesherSettings : ScriptableSingleton<SplineMesherSettings>
    {
        public const string PreferencesPath = "Preferences/Splines/Spline Mesher Pro";
        public const string ProjectSettingsPath = "Project/Splines/Spline Mesher Pro";
        
        private const bool SaveAsText = true;
        
        public enum SectionStyle
        {
            Accordion,
            Foldouts
        }
        
        public static SectionStyle SectionStyleMode
        {
            get => (SectionStyle)EditorPrefs.GetInt(PlayerSettings.productName + "_SM_SectionStyle", (int)SectionStyle.Accordion);
            set => EditorPrefs.SetInt(PlayerSettings.productName + "_SM_SectionStyle", (int)value);
        }
        
        public static bool RebuildEveryFrame
        {
            get => EditorPrefs.GetBool(PlayerSettings.productName + "_SM_RebuildEveryFrame", true);
            set => EditorPrefs.SetBool(PlayerSettings.productName + "_SM_RebuildEveryFrame", value);
        }
        
        public static bool HidePrefabWarning
        {
            get => EditorPrefs.GetBool(PlayerSettings.productName + "_SM_HidePrefabWarning", false);
            set => EditorPrefs.SetBool(PlayerSettings.productName + "_SM_HidePrefabWarning", value);
        }

        public static void OpenPreferences()
        {
            SettingsService.OpenUserPreferences(PreferencesPath);
        }
        
        [SettingsProvider]
        public static SettingsProvider PreferencesProvider()
        {
            var provider = new SettingsProvider(PreferencesPath, SettingsScope.User)
            {
                label = $"Spline Mesher Pro",
                guiHandler = (searchContent) =>
                {
                    EditorGUILayout.LabelField($"v{AssetInfo.VERSION}", EditorStyles.miniLabel);
                    EditorGUILayout.Space();

                    SectionStyleMode = (SectionStyle)EditorGUILayout.EnumPopup("Inspector section style", SectionStyleMode, GUILayout.Width(EditorGUIUtility.labelWidth + 180f));
                    EditorGUILayout.HelpBox("• Accordion: Allows one section to expand at a time\n" +
                                                    "• Foldouts: Manually expand/collapse sections", MessageType.Info);
                    
                    EditorGUILayout.Space();
                    
                    RebuildEveryFrame = EditorGUILayout.Toggle("Rebuild every frame", RebuildEveryFrame);
                    EditorGUILayout.HelpBox("When disabled, the spline mesh is rebuild at a lower frame rate when changing parameters in the Spline Mesh inspector. This yields better editor performance." +
                                            "\n\nWhen enabled, rebuilding occurs every time the UI repaints (much smoother)", MessageType.Info);
                    
                    HidePrefabWarning = EditorGUILayout.Toggle("Hide prefab warnings", HidePrefabWarning);
                    EditorGUILayout.HelpBox("Acknowledge that procedural meshes cannot be used in prefabs, without enabling them to rebuild on Start()", MessageType.Info);
                }
            };

            return provider;
        }

        public enum PrecisionMode
        {
            [InspectorName("Half (16-bit)")]
            Half,
            [InspectorName("Full (32-bit)")]
            Full
        }

        public static PrecisionMode GetCurrentPrecisionMode()
        {
            #if SPLINE_MESHER_HALF_PRECISION
            return PrecisionMode.Half;
            #else
            return PrecisionMode.Full;
            #endif
        }
        
        public enum BakingMode
        {
            Manual,
            Automatic
        }

        public BakingMode generateLightmapUV = BakingMode.Automatic;
        public static BakingMode GenerateLightmapUV
        {
            get => instance.generateLightmapUV;
            set
            {
                instance.generateLightmapUV = value;
                instance.Save(SaveAsText);
            }
        }
        
        public BakingMode generateLODs = BakingMode.Automatic;
        public static BakingMode GenerateLODs
        {
            get => instance.generateLODs;
            set
            {
                instance.generateLODs = value;
                instance.Save(SaveAsText);
            }
        }
        
        public bool hideUpdateNotice = false;
        [Tooltip("Project-wide setting, hides the \"update to\" message in the inspector.")]
        public static bool HideUpdateNotice
        {
            get => instance.hideUpdateNotice;
            set
            {
                instance.hideUpdateNotice = value;
                instance.Save(SaveAsText);
            }
        }

        public static void OpenProjectSettings()
        {
            SettingsService.OpenProjectSettings(SplineMesherSettings.ProjectSettingsPath);
        }
        
        [SettingsProvider]
        public static SettingsProvider ProjectSettingsProvider()
        {
            SplineCurveMesher[] splineMeshers;
            List<SplineCurveMesher> meshersNeedingLightmapUV = new List<SplineCurveMesher>();

            void Refresh()
            {
#if UNITY_6000_4_OR_NEWER
                splineMeshers = Object.FindObjectsByType<SplineCurveMesher>();
#else
                splineMeshers = Object.FindObjectsByType<SplineCurveMesher>(FindObjectsSortMode.None);
#endif
            }

            void RefreshLightmapInfo()
            {
                Refresh();
                
                meshersNeedingLightmapUV.Clear();
                foreach (SplineCurveMesher splineMesher in splineMeshers)
                {
                    if (SplineMesherEditor.RequiresLightmapUV(splineMesher))
                    {
                        meshersNeedingLightmapUV.Add(splineMesher);
                    }
                }
            }
            
            var provider = new SettingsProvider(ProjectSettingsPath, SettingsScope.Project)
            {
                label = $"Spline Mesher Pro",
                activateHandler = (searchContext, rootElement) =>
                {
                    RefreshLightmapInfo();
                },
                
                guiHandler = (searchContent) =>
                {
                    EditorGUILayout.LabelField($"v{AssetInfo.VERSION}", EditorStyles.miniLabel);
                    
                    HideUpdateNotice = EditorGUILayout.Toggle("Hide update notice", HideUpdateNotice);
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Precision", EditorStyles.boldLabel);
                    PrecisionMode currentPrecisionMode = GetCurrentPrecisionMode();
                    if (currentPrecisionMode == PrecisionMode.Half)
                    {
                        EditorGUILayout.HelpBox("Half 16-bit precision is used. This will reduce the memory footprint of generated meshes (~10-20%), but limits their maximum dimensions.", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Full 32-bit precision is used (default). Meshes support large dimensions without visible distortions. Use half-precision if a smaller memory footprint (~10-20%) is desired.", MessageType.Info);
                    }
                    
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        var targetMode = (currentPrecisionMode == PrecisionMode.Half ? "Full (32-bit)" : "Half (16-bit)");
                        if (GUILayout.Button($"Switch to {targetMode} precision", GUILayout.MaxWidth(240f)))
                        {
                            var buildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
                            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);

                            int option = EditorUtility.DisplayDialogComplex($"Enable {targetMode} precision?", $"For the {namedBuildTarget.TargetName} platform, or all?", "All platforms","Cancel", $"Only {namedBuildTarget.TargetName}");
                            
                            //Cancelled
                            if (option == 1) return;
                            
                            EditorApplication.delayCall += () =>
                            {
                                EditorUtility.DisplayDialog("Spline Mesher Pro", $"Precision mode for vertices will be changed after a domain reload." +
                                    "\n\n" +
                                    "This configuration is project-wide, be sure to commit the \"ProjectSettings.asset\" file to source control." +
                                    "\n\n" +
                                    "Rebuild Spline Mesher instances to apply the change to them.",
                                    "Ok");
                                
                                //Option=0= All platforms, 1= Cancel, 2= Only current target
                                SplineMesherEditor.DefineSymbolManager.SetDefineSymbol(
                                    SplineMesherEditor.DefineSymbolManager.SPLINE_MESHER_HALF_PRECISION,
                                    currentPrecisionMode == PrecisionMode.Full,
                                    option == 2 ? buildTargetGroup : BuildTargetGroup.Unknown);
                            };
                        }
                    }
                    EditorGUILayout.Space();
                    
                    GenerateLightmapUV = (BakingMode)EditorGUILayout.EnumPopup("Lightmap UV generation", GenerateLightmapUV, GUILayout.Width(EditorGUIUtility.labelWidth + 180f));
                    if(GenerateLightmapUV == BakingMode.Automatic) EditorGUILayout.HelpBox("When light baking starts, check all (static) Spline Curve Mesher components in the scene and auto-generate lightmap UVs for the created meshes." +
                        "\n\n" +
                        "Spline Fill Meshers already have a lightmap UV.", MessageType.Info);
                    else if (GenerateLightmapUV == BakingMode.Manual)
                    {
                        EditorGUILayout.HelpBox("Call the SplineMesherEditor.GenerateLightmapUVs function from your external script", MessageType.Info);
                    }
                    
                    if (meshersNeedingLightmapUV.Count > 0)
                    {
                        EditorGUILayout.HelpBox($"The following curve meshers require lightmap UVs to be generated:", MessageType.None);
                        EditorGUILayout.BeginVertical(EditorStyles.textArea);
                        foreach (var mesher in meshersNeedingLightmapUV)
                        {
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                EditorGUILayout.LabelField($"{mesher.name}", GUILayout.MaxWidth(200f));
                            }
                        }
                        EditorGUILayout.EndVertical();
                        
                    }
                    if (GUILayout.Button("Generate lightmap UVs now", GUILayout.MaxWidth(200f)))
                    {
                        SplineMesherEditor.GenerateLightmapUVs();
                        RefreshLightmapInfo();
                    }
                    
                    EditorGUILayout.Space();

                    using (new EditorGUI.DisabledGroupScope(!SplineMesherEditor.meshLodSupport))
                    {
                        GenerateLODs = (BakingMode)EditorGUILayout.EnumPopup("Mesh LOD generation", GenerateLODs, GUILayout.Width(EditorGUIUtility.labelWidth + 180f));
                        if(GenerateLODs == BakingMode.Automatic) EditorGUILayout.HelpBox("When a scene is saved, all spline meshers have LODs generated for them if needed.", MessageType.Info);
                        else if(GenerateLODs == BakingMode.Manual) EditorGUILayout.HelpBox("Call the static SplineMesherEditor.GenerateLODS function from your external script", MessageType.Info);

                        List<SplineMesher> meshersNeedingLODS = SplineMesherEditor.GetMeshersRequiringLODGeneration();
                        if (meshersNeedingLODS.Count > 0)
                        {
                            EditorGUILayout.HelpBox($"The following spline meshers require LODs to be generated:", MessageType.None);
                            EditorGUILayout.BeginVertical(EditorStyles.textArea);
                            foreach (var mesher in meshersNeedingLODS)
                            {
                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    EditorGUILayout.LabelField($"{mesher.name}", GUILayout.MaxWidth(200f));
                                }
                            }
                            EditorGUILayout.EndVertical();
                        }
                        
                        if (GUILayout.Button("Generate LODs now", GUILayout.MaxWidth(200f)))
                        {
                            SplineMesherEditor.GenerateLODS();
                        }
                    }
                    
#pragma warning disable CS0162 // Unreachable code detected
                    if(SplineMesherEditor.meshLodSupport == false) EditorGUILayout.HelpBox("This functionality requires Unity 6.2 or newer.", MessageType.Warning);
#pragma warning restore CS0162 // Unreachable code detected
                    EditorGUILayout.Space();
                    
                    //HelpWindow.DrawReviewButton();
                    
                    /*
                    EditorGUILayout.Space();

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Rebuild all instances in scene"))
                        {
                            int count = RebuildAllInstances();
                            Debug.Log($"Rebuilt {count} Spline Mesher component instances.");
                        }
                    }
                    */
                    
                    void DrawStats(IEnumerable<SplineMesher> meshers)
                    {
                        var meshersList = meshers.ToList();
                        if (meshersList.Count > 0)
                        {
                            EditorGUILayout.BeginVertical(EditorStyles.textArea);
                            float totalSize = 0;
                            foreach (var mesher in meshersList)
                            {
                                float size = 0f;

                                foreach (var container in mesher.Containers)
                                {
                                    foreach (var segment in container.Segments)
                                    {
                                        size += segment.GetMemorySize();
                                    }
                                }

                                using (new EditorGUILayout.HorizontalScope())
                                {
                                    EditorGUILayout.LabelField($"{mesher.name}", GUILayout.MaxWidth(200f));
                                    EditorGUILayout.LabelField($"Mesh size: {Utilities.FormatMemorySize(size)}", EditorStyles.miniLabel, GUILayout.MaxWidth(200f));
                                    EditorGUILayout.LabelField($"LODS (max): {mesher.GetLODCount()}", EditorStyles.miniLabel, GUILayout.MaxWidth(80f));
                                }
                                totalSize += size;
                            }
                            EditorGUILayout.EndVertical();
                        
                            EditorGUILayout.LabelField($"Total size: {Utilities.FormatMemorySize(totalSize)}", EditorStyles.boldLabel);
                        }
                    }
                    
                    EditorGUILayout.LabelField($"Curve Meshers: {SplineCurveMesher.Instances.Count}", EditorStyles.boldLabel);
                    DrawStats(SplineCurveMesher.Instances);

                    if (GUILayout.Button("Rebuild all", GUILayout.MaxWidth(100f)))
                    {
                        foreach (var mesher in SplineCurveMesher.Instances)
                        {
                            mesher.Rebuild();
                            EditorUtility.SetDirty(mesher);
                        }
                        
                        RefreshLightmapInfo();
                    }
                    
                    EditorGUILayout.Space();
                    
                    EditorGUILayout.LabelField($"Fill Meshers: {SplineFillMesher.Instances.Count}", EditorStyles.boldLabel);
                    DrawStats(SplineFillMesher.Instances);
                    if (GUILayout.Button("Rebuild all", GUILayout.MaxWidth(100f)))
                    {
                        foreach (var mesher in SplineFillMesher.Instances)
                        {
                            mesher.Rebuild();
                            EditorUtility.SetDirty(mesher);
                        }
                    }
                }
            };

            return provider;
        }
    }
}