using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Editor
{
    // Drawn inside the existing Riggle IMGUI inspector; no separate authoring window.
    internal sealed class RiggleDeformationInspector : IDisposable
    {
        readonly Riggle rig;
        bool expanded = true;
        int sourceIndex, boneIndex, shapeIndex, tool, strokeGroup = -1, control;
        float radius = .15f, strength = .3f, targetWeight = .7f, sculptStep = .012f;
        Transform previewBone;
        Quaternion originalRotation;
        Riggle.BakedSource previewSource;
        string previewShape;
        float originalShapeWeight;
        public RiggleDeformationInspector(Riggle rig)
        {
            this.rig=rig;
            Undo.undoRedoPerformed+=OnUndo;
            EditorSceneManager.sceneSaving+=BeforeSave;
            EditorApplication.playModeStateChanged+=BeforePlay;
        }
        internal void EnterInspection() { FinishStroke(); if (previewBone) StopPreview(); tool=0; }
        public void Dispose()
        {
            StopPreview(); FinishStroke(); Undo.undoRedoPerformed-=OnUndo;
            EditorSceneManager.sceneSaving-=BeforeSave;EditorApplication.playModeStateChanged-=BeforePlay;
        }
        void BeforeSave(Scene scene,string path) { StopPreview(); }
        void BeforePlay(PlayModeStateChange state) { StopPreview(); tool=0; }
        void OnUndo() { StopPreview(); SceneView.RepaintAll(); }
        public void StopPreview()
        {
            if(previewBone)previewBone.localRotation=originalRotation;
            if(previewSource!=null && previewSource.baked && previewSource.baked.sharedMesh)
            {
                int index=previewSource.baked.sharedMesh.GetBlendShapeIndex(previewShape);
                if(index>=0)previewSource.baked.SetBlendShapeWeight(index,originalShapeWeight);
            }
            previewBone=null;previewSource=null;
            var twists=rig?rig.GetComponent<RiggleTwistDistribution>():null;if(twists)twists.Evaluate();
            var driver=rig?rig.GetComponent<RiggleCorrectives>():null;if(driver)driver.Evaluate();
            SceneView.RepaintAll();
        }
        internal void Preview(Riggle.BakedSource source,RiggleMeshEdits.Corrective shape)
        {
            StopPreview();previewBone=source.baked.bones.First(b=>b.name==shape.bone);originalRotation=previewBone.localRotation;
            previewSource=source;previewShape=shape.name;int index=source.baked.sharedMesh.GetBlendShapeIndex(shape.name);
            originalShapeWeight=index>=0?source.baked.GetBlendShapeWeight(index):0;
            RefreshPreview(shape);
        }
        void RefreshPreview(RiggleMeshEdits.Corrective shape)
        {
            if(!previewBone)return;
            previewBone.localRotation=shape.restRotation*Quaternion.AngleAxis(shape.fullAngle,shape.axis);
            var twists=rig.GetComponent<RiggleTwistDistribution>();if(twists)twists.Evaluate();
            var driver=rig.GetComponent<RiggleCorrectives>();if(driver)driver.Evaluate();
            int index=previewSource.baked.sharedMesh.GetBlendShapeIndex(shape.name);
            if(index>=0)previewSource.baked.SetBlendShapeWeight(index,100);
            SceneView.RepaintAll();
        }
        public void Draw()
        {
            expanded=EditorGUILayout.Foldout(expanded,"Weight corrections & shoulder / hip shapes",true);
            if(!expanded){if(tool!=0){tool=0;StopPreview();}return;}
            using(new EditorGUI.DisabledScope(EditorApplication.isPlaying || AnimationMode.InAnimationMode()))
            {
                EditorGUILayout.HelpBox("Edits survive rebakes while source topology stays the same. Paint affects the clicked mesh island and keeps seam duplicates together. Undo is supported. Source mesh assets stay untouched.",MessageType.Info);
                if(GUILayout.Button("Create shoulder & hip starter shapes"))
                {
                    StopPreview();RiggleDeformationTools.AddStarters(rig);
                    SceneView.RepaintAll();
                }
                if(rig.BakedSources.Count==0)return;
                int next=EditorGUILayout.Popup("Mesh to edit",Mathf.Clamp(sourceIndex,0,rig.BakedSources.Count-1),rig.BakedSources.Select(s=>s.source.name).ToArray());
                if(next!=sourceIndex){StopPreview();sourceIndex=next;shapeIndex=0;boneIndex=0;}
                var source=rig.BakedSources[sourceIndex];var skin=source.baked;
                int nextTool=GUILayout.Toolbar(tool,new[]{"Inspect","Paint weights","Sculpt shape"});
                if(nextTool!=tool){StopPreview();tool=nextTool;SceneView.RepaintAll();}
                var edits=source.source.GetComponent<RiggleMeshEdits>();
                EditorGUILayout.LabelField("Saved weight edits",(edits?edits.weights.Count:0)+" vertices");
                if(tool==1)
                {
                    boneIndex=EditorGUILayout.Popup("Bone",Mathf.Clamp(boneIndex,0,skin.bones.Length-1),skin.bones.Select(b=>b.name.Replace("Riggle_","")).ToArray());
                    targetWeight=EditorGUILayout.Slider("Target weight",targetWeight,0,1);
                    strength=EditorGUILayout.Slider("Brush strength",strength,.01f,1);
                    EditorGUILayout.HelpBox("Drag on the surface to paint. Blue = 0, red = 1. Shift paints toward zero. Remaining influences are renormalized to four bones.",MessageType.None);
                    if(edits && edits.weights.Count>0 && GUILayout.Button("Clear weight edits on this mesh & rebake"))
                    {
                        StopPreview();Undo.RecordObject(edits,"Clear painted weights");edits.weights.Clear();RiggleBuilder.Bake(rig);GUIUtility.ExitGUI();
                    }
                }
                if(tool!=0)radius=EditorGUILayout.Slider("Brush radius (world)",radius,.005f,1);
                if(edits && edits.correctives.Count>0)
                {
                    int nextShape=EditorGUILayout.Popup("Corrective shape",Mathf.Clamp(shapeIndex,0,edits.correctives.Count-1),edits.correctives.Select(c=>c.name.Replace(RiggleDeformationTools.Prefix,"")).ToArray());
                    if(nextShape!=shapeIndex){StopPreview();shapeIndex=nextShape;}
                    var shape=edits.correctives[shapeIndex];
                    EditorGUI.BeginChangeCheck();
                    float start=EditorGUILayout.Slider("Starts at bend",shape.startAngle,0,150);
                    float full=EditorGUILayout.Slider("Full at bend",shape.fullAngle,start+1,175);
                    float amount=EditorGUILayout.Slider("Correction strength",shape.strength,0,2);
                    if(EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(edits,"Tune corrective activation");shape.startAngle=start;shape.fullAngle=full;shape.strength=amount;
                        EditorUtility.SetDirty(edits);RiggleDeformationTools.Bind(rig);RefreshPreview(shape);
                    }
                    if(GUILayout.Button(previewBone?"Restore normal pose":"Preview full bend / sculpt at 100%"))
                    {if(previewBone)StopPreview();else Preview(source,shape);}
                    if(tool==2)
                    {
                        sculptStep=EditorGUILayout.Slider("Sculpt step (world)",sculptStep,.001f,.05f);
                        EditorGUILayout.HelpBox("Preview the full bend, then drag to push the surface outward. Hold Shift to push inward. Edits are converted back through the skinning into the saved shape. Preview restores on deselect, scene save or entering Play Mode.",MessageType.None);
                        if(GUILayout.Button("Clear selected shape displacement"))
                        {
                            StopPreview();Undo.RecordObject(edits,"Clear corrective sculpt");shape.deltas.Clear();RiggleDeformationTools.UpdateMeshes(rig);
                        }
                    }
                }
                else if(tool==2)EditorGUILayout.HelpBox("Create starter shapes first, then choose a mesh near a shoulder or hip.",MessageType.Info);
                if(GUILayout.Button("Save edits to prefab & refresh pose row"))
                {
                    StopPreview();RiggleDeformationTools.UpdateMeshes(rig);
                    if(rig.GetComponent<RigglePoseGallery>())RigglePoseGalleryBuilder.Rebuild(rig);else RigglePrefabExporter.Save(rig);
                    EditorSceneManager.SaveScene(rig.gameObject.scene);
                }
            }
        }
        void FinishStroke()
        {
            if(strokeGroup<0)return;
            Undo.CollapseUndoOperations(strokeGroup);strokeGroup=-1;
            if(GUIUtility.hotControl==control)GUIUtility.hotControl=0;
            AssetDatabase.SaveAssets();if(rig)EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        }
        public bool SceneGUI()
        {
            if(tool==0 || !expanded || EditorApplication.isPlaying || AnimationMode.InAnimationMode() || rig.BakedSources.Count==0)return false;
            sourceIndex=Mathf.Clamp(sourceIndex,0,rig.BakedSources.Count-1);var source=rig.BakedSources[sourceIndex];
            var skin=source.baked;var edits=source.source.GetComponent<RiggleMeshEdits>();
            if(tool==2 && (!previewBone || !edits || edits.correctives.Count==0))return true;
            var evt=Event.current;control=GUIUtility.GetControlID("RiggleDeformationBrush".GetHashCode(),FocusType.Passive);
            if(evt.type==EventType.Layout)HandleUtility.AddDefaultControl(control);
            if(evt.type==EventType.KeyDown && evt.keyCode==KeyCode.Escape){FinishStroke();tool=0;StopPreview();evt.Use();return true;}
            if(evt.type==EventType.MouseUp && evt.button==0 && strokeGroup>=0){FinishStroke();evt.Use();return true;}
            var mesh=new Mesh();
            try
            {
                skin.BakeMesh(mesh,true);var vertices=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();
                if(tool==1 && evt.type==EventType.Repaint)
                {
                    var weights=skin.sharedMesh.boneWeights;int stride=Mathf.Max(1,vertices.Length/2500);
                    for(int i=0;i<vertices.Length;i+=stride)
                    {Handles.color=Color.Lerp(new Color(.1f,.3f,1,.65f),new Color(1,.15f,.05f,.9f),RiggleDeformationTools.Weight(weights[i],boneIndex));Handles.DotHandleCap(0,vertices[i],Quaternion.identity,HandleUtility.GetHandleSize(vertices[i])*.015f,EventType.Repaint);}
                }
                Ray ray=HandleUtility.GUIPointToWorldRay(evt.mousePosition);
                if(!Hit(ray,vertices,mesh.triangles,out var point,out var normal,out var seed))return true;
                Handles.color=tool==1?Color.cyan:Color.yellow;Handles.DrawWireDisc(point,normal,radius);
                if((evt.type==EventType.MouseDown || evt.type==EventType.MouseDrag) && evt.button==0 && !evt.alt && !Tools.viewToolActive)
                {
                    if(evt.type==EventType.MouseDown){Undo.IncrementCurrentGroup();strokeGroup=Undo.GetCurrentGroup();Undo.SetCurrentGroupName(tool==1?"Paint Riggle weights":"Sculpt Riggle corrective");GUIUtility.hotControl=control;}
                    if(strokeGroup>=0)
                    {
                        if(tool==1)RiggleDeformationTools.PaintWeights(source,boneIndex,vertices,point,radius,strength,evt.shift?0:targetWeight,seed);
                        else {RiggleDeformationTools.Sculpt(source,shapeIndex,vertices,point,normal,radius,sculptStep*(evt.shift?-1:1),seed);RefreshPreview(edits.correctives[shapeIndex]);}
                        evt.Use();SceneView.RepaintAll();
                    }
                }
                if(evt.type==EventType.MouseMove)SceneView.RepaintAll();
            }
            finally{Object.DestroyImmediate(mesh);Handles.color=Color.white;}
            return true;
        }
        internal static bool Hit(Ray ray,Vector3[] vertices,int[] triangles,out Vector3 point,out Vector3 normal,out int seed)
        {
            float nearest=float.PositiveInfinity;point=normal=Vector3.zero;seed=-1;
            for(int i=0;i<triangles.Length;i+=3)
            {
                Vector3 a=vertices[triangles[i]],e1=vertices[triangles[i+1]]-a,e2=vertices[triangles[i+2]]-a;
                Vector3 p=Vector3.Cross(ray.direction,e2);float det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<.00000001f)continue;
                float inverse=1/det;Vector3 s=ray.origin-a;float u=Vector3.Dot(s,p)*inverse;if(u<0 || u>1)continue;
                Vector3 q=Vector3.Cross(s,e1);float v=Vector3.Dot(ray.direction,q)*inverse;if(v<0 || u+v>1)continue;
                float t=Vector3.Dot(e2,q)*inverse;if(t<0 || t>=nearest)continue;
                nearest=t;point=ray.GetPoint(t);normal=Vector3.Cross(e1,e2).normalized;if(Vector3.Dot(normal,ray.direction)>0)normal=-normal;seed=triangles[i];
            }
            return seed>=0;
        }
    }
}
