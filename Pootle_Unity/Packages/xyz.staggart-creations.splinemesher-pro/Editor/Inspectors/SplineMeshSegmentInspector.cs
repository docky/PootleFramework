using System.Reflection;
using sc.splinemesher.pro.runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace sc.splinemesher.pro.editor
{
    [CustomEditor(typeof(SplineMeshSegment))]
    public class SplineMeshSegmentInspector : Editor
    {
        private SplineMeshSegment component;

        private float memorySize;
        private int vertexCount, triangleCount;
        private bool readable;
        
        private MeshPreview sourceMeshPreview;
        private PreviewRenderUtility meshPreviewUtility;
        private float cameraDistance = 1f;
        private FieldInfo settingsField;
        private object settings;
        private FieldInfo orthoPositionField;
        private FieldInfo zoomFactor;

        private VertexAttributeDescriptor[] vertexAttributes;
        private int[] attributeSizes;
        
        void OnEnable()
        {
            component = (SplineMeshSegment)target;

            if (component.mesh)
            {
                memorySize = Utilities.GetMemorySize(component.mesh);
                readable = component.mesh.isReadable;
                
                vertexAttributes = component.mesh.GetVertexAttributes();
                attributeSizes = new int[vertexAttributes.Length];
                
                static int GetVertexAttributeSize(VertexAttributeDescriptor attribute)
                {
                    int bytesPerComponent = attribute.format switch
                    {
                        VertexAttributeFormat.Float32 => 4,
                        VertexAttributeFormat.Float16 => 2,
                        VertexAttributeFormat.UNorm8   => 1,
                        VertexAttributeFormat.SNorm8   => 1,
                        VertexAttributeFormat.UInt8    => 1,
                        VertexAttributeFormat.SInt8    => 1,
                        VertexAttributeFormat.UInt16   => 2,
                        VertexAttributeFormat.SInt16   => 2,
                        VertexAttributeFormat.UInt32   => 4,
                        VertexAttributeFormat.SInt32   => 4,
                        _ => 0
                    };

                    return bytesPerComponent * attribute.dimension;
                }
                
                for (int i = 0; i < attributeSizes.Length; i++)
                {
                    attributeSizes[i] = GetVertexAttributeSize(vertexAttributes[i]);
                }
            }

            (vertexCount, triangleCount) = component.GetMeshStats();
            
            sourceMeshPreview = new MeshPreview(new Mesh());
            
            //Override zoom level
            meshPreviewUtility = (PreviewRenderUtility)typeof(MeshPreview).GetField("m_PreviewUtility", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sourceMeshPreview);
            meshPreviewUtility.camera.fieldOfView = 25;
            meshPreviewUtility.camera.backgroundColor = UnityEngine.Color.white * 0.09f;
            
            //Use reflection to access the private m_Settings field
            settingsField = typeof(MeshPreview).GetField("m_Settings", BindingFlags.Instance | BindingFlags.NonPublic);
            settings = settingsField.GetValue(sourceMeshPreview);
            orthoPositionField = settings.GetType().GetField("m_PivotPositionOffset", BindingFlags.Instance | BindingFlags.NonPublic);
            orthoPositionField.SetValue(settings, Vector3.forward * 20f);
            
            FieldInfo m_PreviewDir = settings.GetType().GetField("m_PreviewDir", BindingFlags.Instance | BindingFlags.NonPublic);
            m_PreviewDir.SetValue(settings, new Vector2(0, -33));
            
            zoomFactor = settings.GetType().GetField("m_ZoomFactor", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public override void OnInspectorGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                UI.DrawHeader();
            }

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent(" Select Spline Mesher ", EditorGUIUtility.IconContent("back@2x").image), GUILayout.Height(32f)))
                {
                    Selection.activeGameObject = component.Container.Owner.gameObject;
                    return;
                }
                GUILayout.FlexibleSpace();
            }
            GUILayout.Space(5f);
            
            using (new EditorGUI.DisabledGroupScope(true))
            {
                EditorGUILayout.ObjectField("Container", component.Container, typeof(GameObject), true);
            }
            
            EditorGUILayout.Space();

            if (component.mesh)
            {
                Mesh inputMesh = component.mesh;

                if (sourceMeshPreview.mesh != inputMesh) sourceMeshPreview.mesh = inputMesh;
                Rect previewRect = EditorGUILayout.GetControlRect(false, 150f);

                var previewMouseOver = previewRect.Contains(Event.current.mousePosition);
                var meshPreviewFocus = previewMouseOver && (Event.current.type == EventType.MouseDown ||
                                                            Event.current.type == EventType.MouseDrag);
                
                //Handle scroll wheel separately - only needs mouse over, not dragging
                if (previewMouseOver && Event.current.type == EventType.ScrollWheel)
                {
                    float scrollDelta = Event.current.delta.y;
                    cameraDistance += scrollDelta * 0.025f;
                    
                    zoomFactor.SetValue(settings, cameraDistance);
                    
                    Event.current.Use();
                    Repaint();
                }

                if (meshPreviewFocus)
                {
                    sourceMeshPreview.OnPreviewGUI(previewRect, GUIStyle.none);
                }
                else
                {
                    if (Event.current.type == EventType.Repaint)
                    {
                        GUI.DrawTexture(previewRect,
                            sourceMeshPreview.RenderStaticPreview((int)previewRect.width, (int)previewRect.height));
                    }
                }
                
                using (new EditorGUILayout.HorizontalScope())
                {
                    sourceMeshPreview.OnPreviewSettings();
                }

                EditorGUILayout.Space();
            }
            
            //EditorGUIUtility.labelWidth *= 0.5f;
            void DrawItem(GUIContent label, string value)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(label);
                    EditorGUILayout.LabelField(value);
                }           
            }
            
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if(component.mesh)
                {
                    EditorGUILayout.LabelField("Mesh Info", EditorStyles.boldLabel);
                    DrawItem(new GUIContent($"  Vertices", EditorGUIUtility.IconContent("d_EditCollider").image), $"{vertexCount:N0}");
                    DrawItem( new GUIContent($" Triangles", EditorGUIUtility.IconContent("d_ProfilerColumn.WarningCount").image), $"{triangleCount:N0}");

                    if (triangleCount >= ushort.MaxValue)
                    {
                        EditorGUILayout.HelpBox("This mesh exceeds the limits of 16-bit precision. Some triangles may appear malformed. To avoid this, use smaller segments.", MessageType.Warning);
                    }
                    DrawItem( new GUIContent($" Size", EditorGUIUtility.IconContent("Profiler.Memory").image), $"{Utilities.FormatMemorySize(memorySize)}");
                    DrawItem( new GUIContent($" Readable", EditorGUIUtility.IconContent("d_SaveAs").image), $"{(readable ? "Yes" : "No")}");

                    EditorGUILayout.Separator();
                    
                    EditorGUILayout.LabelField("Vertex Attributes", EditorStyles.boldLabel);
                    for (int i = 0; i < vertexAttributes.Length; i++)
                    {
                        DrawItem(new GUIContent(vertexAttributes[i].attribute.ToString()), $"{vertexAttributes[i].format}x{vertexAttributes[i].dimension} ({attributeSizes[i]} bytes)");
                    }
                    EditorGUILayout.Separator();
                    
                    DrawItem(new GUIContent("Triangles"), (component.mesh.indexFormat == IndexFormat.UInt16 ? "16-bit" : "32-bit"));
                    
                    EditorGUILayout.Separator();
                }

                if (component.CurveInfo.GetLength() > 0)
                {
                    EditorGUILayout.LabelField("Segment Info", EditorStyles.boldLabel);
                    DrawItem(new GUIContent("Index"), $"{component.CurveInfo.index+1}/{component.Container.SegmentCount}");
                    DrawItem(new GUIContent("Range"), $"{System.Math.Round(component.CurveInfo.curveRange.x, 2)} > {System.Math.Round(component.CurveInfo.curveRange.y, 2)}");
                    DrawItem(new GUIContent("Length"), $"{System.Math.Round(component.CurveInfo.GetLength(), 2)}m");
                    DrawItem(new GUIContent("Tiles"), $"{component.CurveInfo.tileCount}");
                    DrawItem(new GUIContent("Tile Length"), $"{System.Math.Round(component.CurveInfo.tileLength, 2)}m");
                }
                
                EditorGUILayout.Separator();
            }
            //EditorGUIUtility.labelWidth *= 2f;

            UI.DrawFooter();
        }
        
        public void OnDisable()
        {
            if (sourceMeshPreview != null)
            {
                sourceMeshPreview.Dispose();
                sourceMeshPreview = null;
            }

            vertexAttributes = null;
            attributeSizes = null;
        }
    }
}