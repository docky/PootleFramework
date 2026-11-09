using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

namespace Docky.RiggleTools.Editor
{
    internal static class RiggleReferenceSetup
    {
        public static bool TryPoint(string name,out Riggle.Point point)
        {
            point=default;
            string spaced=Regex.Replace(name,@"([a-z0-9])([A-Z])","$1 $2");
            var tokens=Regex.Split(spaced.ToLowerInvariant(),@"[^a-z0-9]+").Where(t=>t.Length>0).ToList();
            tokens.RemoveAll(t=>t=="ref" || t=="reference" || t=="marker" || t=="point" || t=="riggle");
            int side=0;
            if(tokens.Count>1 && new[]{"l","r","left","right"}.Contains(tokens[0])){side=tokens[0]=="l" || tokens[0]=="left"?1:2;tokens.RemoveAt(0);}
            if(tokens.Count>1 && new[]{"l","r","left","right"}.Contains(tokens[tokens.Count-1]))
            {int end=tokens[tokens.Count-1]=="l" || tokens[tokens.Count-1]=="left"?1:2;if(side!=0 && side!=end)return false;side=end;tokens.RemoveAt(tokens.Count-1);}
            string key=string.Concat(tokens);
            // Handle all-lowercase LeftShoulder and Blender's dotted/underscore suffixes alike.
            if(key.StartsWith("left",StringComparison.Ordinal)){if(side==2)return false;side=1;key=key.Substring(4);}
            if(key.StartsWith("right",StringComparison.Ordinal)){if(side==1)return false;side=2;key=key.Substring(5);}
            if(side==0)
            {
                if(key=="pelvis" || key=="hips" || key=="hipcenter")point=Riggle.Point.Pelvis;
                else if(key=="neck")point=Riggle.Point.Neck;
                else if(key=="headtop" || key=="headtip" || key=="crown" || key=="topofhead")point=Riggle.Point.HeadTop;
                else return false;
                return true;
            }
            int left=key=="shoulder" || key=="upperarm"?3:key=="elbow" || key=="forearm" || key=="lowerarm"?4:key=="wrist" || key=="hand"?5:key=="knee" || key=="lowerleg" || key=="shin"?6:key=="ankle" || key=="foot"?7:-1;
            if(left<0)return false;point=(Riggle.Point)(left+(side==2?5:0));return true;
        }
        public static RiggleReferencePoints Ensure(Riggle rig)
        {
            var refs=rig.GetComponent<RiggleReferencePoints>()??Undo.AddComponent<RiggleReferencePoints>(rig.gameObject);
            if(refs.points==null || refs.points.Length!=Riggle.PointCount){Undo.RecordObject(refs,"Resize reference slots");Array.Resize(ref refs.points,Riggle.PointCount);}
            return refs;
        }
        public static bool IsSourceTransform(Riggle rig,Transform t)
        {
            if(!t || t==rig.transform || !t.IsChildOf(rig.transform))return false;
            if(rig.GeneratedBones && t.IsChildOf(rig.GeneratedBones))return false;
            if(rig.Assembly!=Riggle.AssemblyMode.SmoothSkin && !string.IsNullOrEmpty(rig.AttachmentSymbol))
                for(var parent=t;parent && parent!=rig.transform;parent=parent.parent)
                    if(parent.name.Contains(rig.AttachmentSymbol))return false;
            return !t.GetComponentInParent<RiggleImportedSource>() && !t.GetComponentInParent<ControlRig.Rig_References>();
        }
        public static bool IsCandidate(Riggle rig,Transform t)
        {
            if(!IsSourceTransform(rig,t))return false;
            var origin=t.GetComponent<RiggleOriginReference>();
            return origin ? origin.enabled : !t.GetComponent<Renderer>() && !t.GetComponent<MeshFilter>();
        }
        public static RiggleOriginReference AssignOrigin(Riggle rig,Transform source,Riggle.Point point)
        {
            if(!IsSourceTransform(rig,source))throw new InvalidOperationException("Select a source part/group outside generated bones, controls, imported originals and marked accessories.");
            var refs=Ensure(rig);var origin=source.GetComponent<RiggleOriginReference>()??Undo.AddComponent<RiggleOriginReference>(source.gameObject);
            Undo.RecordObject(origin,"Assign part origin landmark");Undo.RecordObject(refs,"Assign part origin landmark");
            origin.landmark=point;origin.enabled=true;
            for(int i=0;i<refs.points.Length;i++)if(refs.points[i]==source)refs.points[i]=null;
            refs.points[(int)point]=source;EditorUtility.SetDirty(origin);EditorUtility.SetDirty(refs);
            return origin;
        }
        public static string Scan(Riggle rig)
        {
            var refs=Ensure(rig);var root=refs.searchRoot?refs.searchRoot:rig.transform;
            if(root!=rig.transform && !root.IsChildOf(rig.transform))throw new InvalidOperationException("Reference search root must be inside the character.");
            var matches=new Dictionary<Riggle.Point,List<Transform>>();
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                if(!IsCandidate(rig,t))continue;
                var origin=t.GetComponent<RiggleOriginReference>();Riggle.Point point;
                if(origin)point=origin.landmark;else if(!TryPoint(t.name,out point))continue;
                if(!matches.ContainsKey(point))matches[point]=new List<Transform>();matches[point].Add(t);
            }
            Undo.RecordObject(refs,"Find child reference points");var report=new List<string>();int count=0;
            foreach(var pair in matches)
            {
                if(refs.Assigned(pair.Key))continue;
                if(pair.Value.Count!=1){report.Add(pair.Key+": ambiguous ("+pair.Value.Count+" objects); assign manually.");continue;}
                refs.points[(int)pair.Key]=pair.Value[0];count++;
            }
            EditorUtility.SetDirty(refs);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            return "Assigned "+count+" child references."+(report.Count>0?"\n"+string.Join("\n",report):"");
        }
        public static void CreateMissing(Riggle rig,bool oneSideOnly)
        {
            var refs=Ensure(rig);var positions=Enumerable.Range(0,Riggle.PointCount).Select(i=>rig.GetWorld((Riggle.Point)i)).ToArray();
            Transform root=refs.searchRoot;
            if(!root)
            {
                var group=new GameObject("Reference points");Undo.RegisterCreatedObjectUndo(group,"Create reference points");group.transform.SetParent(rig.transform,false);root=group.transform;
            }
            if(root!=rig.transform && !root.IsChildOf(rig.transform))throw new InvalidOperationException("Reference group must be inside the character.");
            Undo.RecordObject(refs,"Create reference points");refs.searchRoot=root;
            for(int i=0;i<Riggle.PointCount;i++)
            {
                if(refs.Assigned((Riggle.Point)i))continue;
                int other=RiggleReferencePoints.Opposite(i);
                if(oneSideOnly && ((other>=0 && refs.Assigned((Riggle.Point)other)) || i>=8))continue;
                string label=((Riggle.Point)i).ToString().Replace("Left","").Replace("Right","")+(i>=8?".R":i>=3?".L":"");
                var go=new GameObject(label);Undo.RegisterCreatedObjectUndo(go,"Create reference point");go.transform.SetParent(root,false);go.transform.position=positions[i];refs.points[i]=go.transform;
            }
            EditorUtility.SetDirty(refs);UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
        }
        public static string Status(Riggle rig,Riggle.Point point)
        {
            var refs=rig.GetComponent<RiggleReferencePoints>();
            if(refs && refs.useReferences && refs.Assigned(point))return refs.Assigned(point).GetComponent<RiggleOriginReference>()?"Part origin + local offset":"Assigned";
            if(refs && refs.TryGetLocal(rig,point,out _))return "Inferred from "+(Riggle.Point)RiggleReferencePoints.Opposite((int)point);
            return "Stored landmark";
        }
    }
}
