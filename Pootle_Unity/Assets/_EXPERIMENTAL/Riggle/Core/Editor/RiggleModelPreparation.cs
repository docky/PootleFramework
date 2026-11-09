using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Docky.RiggleTools.Editor
{
    internal static class RiggleModelPreparation
    {
        public static void Folder(string path)
        {
            if(path!="Assets" && !path.StartsWith("Assets/",StringComparison.Ordinal))throw new InvalidOperationException("Choose a folder inside Assets.");
            var parts=path.Split('/');string current=parts[0];
            for(int i=1;i<parts.Length;i++){if(string.IsNullOrWhiteSpace(parts[i]) || parts[i]=="..")throw new InvalidOperationException("Invalid output folder.");string next=current+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(current,parts[i]);current=next;}
        }
        public static Riggle Create(GameObject model,string characterName,float height)
        {
            if(!model || EditorApplication.isPlaying)throw new InvalidOperationException("Select a model in Edit Mode.");
            if(height<=.1f)throw new InvalidOperationException("Character height must be greater than 0.1.");
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Prepare Riggle character");
            var root=new GameObject(characterName);Undo.RegisterCreatedObjectUndo(root,"Prepare Riggle character");
            try
            {
                var imported=Object.Instantiate(model,root.transform);imported.name="Imported source · preserved";
                imported.transform.localPosition=Vector3.zero;imported.transform.localRotation=Quaternion.identity;
                imported.AddComponent<RiggleImportedSource>();
                foreach(var animator in imported.GetComponentsInChildren<Animator>(true))animator.enabled=false;
                var renderers=imported.GetComponentsInChildren<Renderer>(true).Where(r=>r is SkinnedMeshRenderer || r.GetComponent<MeshFilter>()).ToArray();
                if(renderers.Length==0)throw new InvalidOperationException("No mesh renderers found in the model.");
                string folder="Assets/RiggleGenerated/"+characterName.Replace('/','_').Replace('\\','_');Folder(folder);Folder(folder+"/Sources");
                var copies=new List<(Renderer renderer,Mesh mesh)>();Bounds bounds=default;bool haveBounds=false;
                foreach(var renderer in renderers)
                {
                    Mesh mesh;
                    if(renderer is SkinnedMeshRenderer skin){mesh=new Mesh();skin.BakeMesh(mesh,true);}
                    else mesh=Object.Instantiate(renderer.GetComponent<MeshFilter>().sharedMesh);
                    var matrix=root.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                    mesh.vertices=mesh.vertices.Select(matrix.MultiplyPoint3x4).ToArray();
                    var normal=matrix.inverse.transpose;mesh.normals=mesh.normals.Select(n=>normal.MultiplyVector(n).normalized).ToArray();mesh.RecalculateBounds();
                    if(!haveBounds){bounds=mesh.bounds;haveBounds=true;}else bounds.Encapsulate(mesh.bounds);
                    copies.Add((renderer,mesh));
                }
                if(bounds.size.y<.000001f)throw new InvalidOperationException("The source has no measurable height.");
                float scale=height/bounds.size.y;Vector3 offset=new Vector3(bounds.center.x,bounds.min.y,0);
                var hints=new Dictionary<Riggle.Point,Vector3>();
                string[] keys={"hips","neck","headtop","leftarm","leftforearm","lefthand","leftleg","leftfoot","rightarm","rightforearm","righthand","rightleg","rightfoot"};
                foreach(var bone in imported.GetComponentsInChildren<Transform>(true))
                {
                    string name=bone.name.Split(':').Last().Replace("_","").Replace(" ","").ToLowerInvariant();int index=Array.IndexOf(keys,name);
                    if(index>=0)hints[(Riggle.Point)index]=(root.transform.InverseTransformPoint(bone.position)-offset)*scale;
                }
                var sourceGroup=new GameObject("Riggle sources");sourceGroup.transform.SetParent(root.transform,false);var filters=new List<MeshFilter>();
                foreach(var copy in copies)
                {
                    copy.mesh.name=copy.renderer.name+" · Riggle source";copy.mesh.vertices=copy.mesh.vertices.Select(v=>(v-offset)*scale).ToArray();
                    copy.mesh.boneWeights=Array.Empty<BoneWeight>();copy.mesh.bindposes=Array.Empty<Matrix4x4>();copy.mesh.RecalculateBounds();
                    string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/Sources/"+copy.renderer.name.Replace('/','_')+".asset");AssetDatabase.CreateAsset(copy.mesh,path);
                    var go=new GameObject(copy.renderer.name);go.transform.SetParent(sourceGroup.transform,false);var filter=go.AddComponent<MeshFilter>();filter.sharedMesh=copy.mesh;filters.Add(filter);
                    go.AddComponent<MeshRenderer>().sharedMaterials=copy.renderer.sharedMaterials;
                }
                imported.SetActive(false);
                var rig=Undo.AddComponent<Riggle>(root);var so=new SerializedObject(rig);
                so.FindProperty("assetFolder").stringValue=folder;so.FindProperty("tryHumanoid").boolValue=true;
                var sources=so.FindProperty("sourceMeshes");sources.arraySize=filters.Count;for(int i=0;i<filters.Count;i++)sources.GetArrayElementAtIndex(i).objectReferenceValue=filters[i];so.ApplyModifiedPropertiesWithoutUndo();
                rig.FitPointsToMeshBounds();rig.MirrorRight=false;
                foreach(var hint in hints)rig.SetLocal(hint.Key,hint.Value);
                if(hints.Count>3)rig.SetLocal(Riggle.Point.HeadTop,new Vector3(rig.GetLocal(Riggle.Point.Neck).x,height,rig.GetLocal(Riggle.Point.Neck).z));
                RiggleReferenceSetup.CreateMissing(rig,hints.Count<=3);
                EditorUtility.SetDirty(rig);AssetDatabase.SaveAssets();Selection.activeGameObject=root;return rig;
            }
            catch{Undo.RevertAllDownToGroup(group);throw;}
            finally{Undo.CollapseUndoOperations(group);}
        }
    }
}
