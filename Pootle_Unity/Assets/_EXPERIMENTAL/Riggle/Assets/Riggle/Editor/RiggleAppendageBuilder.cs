using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Docky.RiggleTools.Editor
{
    internal static class RiggleAppendageBuilder
    {
        internal sealed class Definition
        {
            public string name, parent;
            public RiggleAppendages.Chain settings;
            public bool mirror;
        }
        internal static IEnumerable<Definition> Definitions(Riggle rig)
        {
            var extra=rig.GetComponent<RiggleAppendages>(); if (!extra || !extra.AnyEnabled) yield break;
            if (extra.bunnyEars)
            {
                yield return new Definition { name="LeftEar", parent="Riggle_Head", settings=extra.leftEar };
                yield return new Definition { name="RightEar", parent="Riggle_Head", settings=extra.mirrorEars?extra.leftEar:extra.rightEar, mirror=extra.mirrorEars };
            }
            if (extra.longTail) yield return new Definition { name="Tail", parent="Riggle_Hips", settings=extra.tail };
            if (extra.backWings)
            {
                yield return new Definition { name="LeftWing", parent="Riggle_UpperChest", settings=extra.leftWing };
                yield return new Definition { name="RightWing", parent="Riggle_UpperChest", settings=extra.mirrorWings?extra.leftWing:extra.rightWing, mirror=extra.mirrorWings };
            }
        }
        internal static string BoneName(string chain,int index) => "Riggle_"+chain+(index+1).ToString("00");
        internal static bool IsAppendageBone(string name) => name.StartsWith("Riggle_LeftEar") || name.StartsWith("Riggle_RightEar") || name.StartsWith("Riggle_Tail") || name.StartsWith("Riggle_LeftWing") || name.StartsWith("Riggle_RightWing");
        internal static HashSet<string> ScopeBones(Riggle rig,string chain)
        {
            var definition=Definitions(rig).FirstOrDefault(d=>d.name==chain);
            if (definition==null) throw new InvalidOperationException(chain+": enable this appendage in Rig Helper → Fit before baking its mesh.");
            if (definition.settings.segments<2 || definition.settings.segments>16) throw new InvalidOperationException(chain+": choose 2–16 bone segments.");
            return new HashSet<string>(Enumerable.Range(0,definition.settings.segments).Select(i=>BoneName(chain,i)));
        }
        internal static Vector3[] Path(Riggle rig,Definition definition)
        {
            var chain=definition.settings;
            if (chain==null || chain.segments<2 || chain.segments>16 || chain.guides==null || chain.guides.Length<2)
                throw new InvalidOperationException(definition.name+": needs 2–16 segments and at least two guide points.");
            if (!float.IsFinite(chain.radius) || chain.radius<=0) throw new InvalidOperationException(definition.name+": weighting radius must be positive.");
            var positions=new Vector3[chain.guides.Length];
            for(int i=0;i<positions.Length;i++)
            {
                var guide=chain.guides[i];if(guide==null)throw new InvalidOperationException(definition.name+": missing guide "+i);
                if(guide.reference && !RiggleReferenceSetup.IsSourceTransform(rig,guide.reference))throw new InvalidOperationException(definition.name+": guides must use source transforms, not generated bones or animated accessories.");
                var point=guide.reference?rig.transform.InverseTransformPoint(guide.reference.TransformPoint(guide.offset)):guide.position;
                if(definition.mirror)point.x=2*rig.GetLocal(Riggle.Point.Pelvis).x-point.x;
                if(!float.IsFinite(point.x)||!float.IsFinite(point.y)||!float.IsFinite(point.z))throw new InvalidOperationException(definition.name+": guide coordinates must be finite.");
                positions[i]=rig.transform.TransformPoint(point);
                if(i>0 && Vector3.Distance(positions[i-1],positions[i])<.0001f)throw new InvalidOperationException(definition.name+": consecutive guides overlap.");
            }
            return positions;
        }
        internal static Vector3[] Samples(Riggle rig,Definition definition)
        {
            var path=Path(rig,definition);var distances=new float[path.Length];
            for(int i=1;i<path.Length;i++)distances[i]=distances[i-1]+Vector3.Distance(path[i-1],path[i]);
            var result=new Vector3[definition.settings.segments+1];
            for(int s=0;s<result.Length;s++)
            {
                float distance=distances.Last()*s/(result.Length-1);int edge=1;
                while(edge<path.Length-1 && distances[edge]<distance)edge++;
                result[s]=Vector3.Lerp(path[edge-1],path[edge],Mathf.InverseLerp(distances[edge-1],distances[edge],distance));
            }
            return result;
        }
        internal static void AddSpecs(Riggle rig,List<RiggleBuilder.BoneSpec> bones)
        {
            foreach(var definition in Definitions(rig))
            {
                var samples=Samples(rig,definition);
                for(int i=0;i<samples.Length-1;i++)
                    bones.Add(new RiggleBuilder.BoneSpec(BoneName(definition.name,i),i==0?definition.parent:BoneName(definition.name,i-1),
                        samples[i],samples[i+1],RiggleBuilder.Region.Appendage,HumanBodyBones.LastBone)
                        { appendage=definition.name, radiusFraction=definition.settings.radius });
                bones.Add(new RiggleBuilder.BoneSpec("Riggle_"+definition.name+"End",BoneName(definition.name,samples.Length-2),
                    samples.Last(),samples.Last(),RiggleBuilder.Region.Appendage,HumanBodyBones.LastBone,false){appendage=definition.name});
            }
        }
        internal static void DrawGuides(Riggle rig)
        {
            var extra=rig.GetComponent<RiggleAppendages>();if(!extra || !extra.AnyEnabled || !extra.showGuides || EditorApplication.isPlaying)return;
            var old=Handles.color;Handles.color=new Color(.85f,.4f,1);
            try
            {
                foreach(var definition in Definitions(rig))
                {
                    Vector3[] path;try{path=Path(rig,definition);}catch(InvalidOperationException){continue;}
                    Handles.DrawAAPolyLine(3,path);Handles.Label(path[0],definition.name+(definition.mirror?" (mirrored)":""));
                    if(definition.mirror)continue;
                    for(int i=0;i<path.Length;i++)
                    {
                        EditorGUI.BeginChangeCheck();var position=Handles.PositionHandle(path[i],rig.transform.rotation);
                        if(!EditorGUI.EndChangeCheck())continue;
                        Undo.RecordObject(extra,"Fit appendage guide");var guide=definition.settings.guides[i];
                        if(guide.reference)guide.offset=guide.reference.InverseTransformPoint(position);
                        else guide.position=rig.transform.InverseTransformPoint(position);
                        EditorUtility.SetDirty(extra);
                    }
                }
            }
            finally{Handles.color=old;}
        }
        internal static void BuildControls(Riggle rig,List<RiggleBuilder.BoneSpec> specs,Dictionary<string,Transform> lookup)
        {
            var definitions=Definitions(rig).ToArray();if(definitions.Length==0)return;
            var driver=rig.GetComponent<RiggleAppendageRig>()??Undo.AddComponent<RiggleAppendageRig>(rig.gameObject);
            Undo.RecordObject(driver,"Bind appendage FK controls");var chains=new List<RiggleAppendageRig.Chain>();
            foreach(var definition in definitions)
            {
                var anchor=new GameObject("FK_"+definition.name);Undo.RegisterCreatedObjectUndo(anchor,"Create appendage FK");anchor.transform.SetParent(lookup[definition.parent],false);
                var bones=specs.Where(s=>s.appendage==definition.name && s.skinned).Select(s=>s.transform).ToArray();
                var controls=new Transform[bones.Length];
                for(int i=0;i<bones.Length;i++)
                {
                    var go=new GameObject("FK_"+definition.name+(i+1).ToString("00"));Undo.RegisterCreatedObjectUndo(go,"Create appendage FK");
                    controls[i]=go.transform;controls[i].SetParent(i==0?anchor.transform:controls[i-1],false);
                    controls[i].localPosition=bones[i].localPosition;controls[i].localRotation=bones[i].localRotation;
                }
                chains.Add(new RiggleAppendageRig.Chain { name=definition.name,bones=bones,controls=controls,restRotations=bones.Select(b=>b.localRotation).ToArray() });
            }
            driver.chains=chains.ToArray();EditorUtility.SetDirty(driver);
        }
        internal static void Defaults(Riggle rig,RiggleAppendages extra)
        {
            Undo.RecordObject(extra,"Fit default appendage paths");
            var top=rig.GetLocal(Riggle.Point.HeadTop);var pelvis=rig.GetLocal(Riggle.Point.Pelvis);var neck=rig.GetLocal(Riggle.Point.Neck);
            float h=Mathf.Max(.1f,top.y-(rig.GetLocal(Riggle.Point.LeftAnkle).y+rig.GetLocal(Riggle.Point.RightAnkle).y)*.5f);
            RiggleAppendages.Guide[] Guides(params Vector3[] points)=>points.Select(p=>new RiggleAppendages.Guide{position=p}).ToArray();
            var ear=top+new Vector3(-h*.065f,-h*.045f,0);
            extra.leftEar.guides=Guides(ear,ear+new Vector3(-h*.025f,h*.22f,0),ear+new Vector3(-h*.055f,h*.4f,-h*.025f));
            var tail=pelvis+new Vector3(0,-h*.02f,-h*.09f);
            extra.tail.guides=Guides(tail,tail+new Vector3(0,-h*.13f,-h*.25f),tail+new Vector3(0,-h*.18f,-h*.58f),tail+new Vector3(0,h*.04f,-h*.83f));
            var wing=Vector3.Lerp(pelvis,neck,.83f)+new Vector3(-h*.08f,0,-h*.08f);
            extra.leftWing.guides=Guides(wing,wing+new Vector3(-h*.28f,h*.16f,-h*.1f),wing+new Vector3(-h*.63f,h*.23f,-h*.18f));
            RiggleAppendages.Guide[] Mirror(RiggleAppendages.Guide[] guides)=>guides.Select(g=>new RiggleAppendages.Guide{position=new Vector3(2*pelvis.x-g.position.x,g.position.y,g.position.z)}).ToArray();
            extra.rightEar.guides=Mirror(extra.leftEar.guides);extra.rightWing.guides=Mirror(extra.leftWing.guides);EditorUtility.SetDirty(extra);
        }
        internal static void Pose(RiggleAppendageRig driver,int pose,bool undo=true)
        {
            foreach(var chain in driver.chains)
                for(int i=0;i<chain.controls.Length;i++)
                {
                    var control=chain.controls[i];if(!control)continue;
                    if(undo){Undo.RecordObject(control,"Pose appendage controls");if(chain.bones[i])Undo.RecordObject(chain.bones[i],"Pose appendage bones");}
                    float side=chain.name.StartsWith("Left")?-1:1;Vector3 angles=Vector3.zero;
                    if(pose!=0)
                    {
                        if(chain.name.Contains("Ear"))angles=new Vector3(pose==1?15:-12,0,side*(pose==1?12:-8));
                        else if(chain.name=="Tail")angles=new Vector3(pose==1?-12:5,pose==1?9:-14,0);
                        else if(chain.name.Contains("Wing"))angles=new Vector3(0,side*(pose==1?8:-12),side*(pose==1?14:-10));
                    }
                    control.localRotation=chain.restRotations[i]*Quaternion.Euler(angles);
                }
            driver.Evaluate();if(undo)UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(driver.gameObject.scene);
        }
    }
}
