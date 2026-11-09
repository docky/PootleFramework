using System;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Docky.RiggleTools.Editor;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleDeformationTests
    {
        Riggle rig;
        readonly List<Mesh> meshes=new List<Mesh>();
        string folder;
        [SetUp] public void Setup()
        {
            EditorSceneManager.OpenScene("Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/riggle test.unity");
            rig=Object.Instantiate(Object.FindObjectsByType<Riggle>().First(r=>r.name.Contains("Bean")));
            foreach(var source in rig.BakedSources){var mesh=Object.Instantiate(source.baked.sharedMesh);meshes.Add(mesh);source.baked.sharedMesh=mesh;}
        }
        [TearDown] public void Cleanup()
        {
            if(rig)Object.DestroyImmediate(rig.gameObject);foreach(var mesh in meshes)if(mesh)Object.DestroyImmediate(mesh);meshes.Clear();
            if(!string.IsNullOrEmpty(folder))AssetDatabase.DeleteAsset(folder);folder=null;
            EditorSceneManager.OpenScene("Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/riggle test.unity");
        }
        Riggle.BakedSource Arm => rig.BakedSources.First(s=>s.source.name=="Left Arm");
        static Vector3[] World(Riggle.BakedSource source)
        {
            var mesh=new Mesh();try{source.baked.BakeMesh(mesh,true);return mesh.vertices.Select(source.baked.transform.TransformPoint).ToArray();}finally{Object.DestroyImmediate(mesh);}
        }
        static int Seed(Riggle.BakedSource source)
        {
            var topology=new RiggleTopology(source.source.GetComponent<MeshFilter>().sharedMesh);
            return Enumerable.Range(0,topology.welded.Length).First(i=>topology.welded[i]==i && topology.welded.Count(v=>v==i)>1);
        }
        int Paint(Riggle.BakedSource source)
        {
            int bone=Array.FindIndex(source.baked.bones,b=>b.name=="Riggle_LeftUpperArm");int seed=Seed(source);var vertices=World(source);
            Assert.That(RiggleDeformationTools.PaintWeights(source,bone,vertices,vertices[seed],.12f,1,.8f,seed),Is.GreaterThan(0));return seed;
        }
        [Test] public void WeightPaintingNormalizesPreservesSeamsAndReplaysByName()
        {
            var source=Arm;var original=source.source.GetComponent<MeshFilter>().sharedMesh;var originalVertices=original.vertices;
            var before=source.baked.sharedMesh.boneWeights;int seed=Paint(source);var after=source.baked.sharedMesh.boneWeights;
            int bone=Array.FindIndex(source.baked.bones,b=>b.name=="Riggle_LeftUpperArm");
            Assert.That(RiggleDeformationTools.Weight(after[seed],bone),Is.EqualTo(.8f).Within(.00001));
            var topology=new RiggleTopology(original);
            foreach(var w in after)Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.00001));
            for(int i=0;i<after.Length;i++)Assert.That(after[i],Is.EqualTo(after[topology.welded[i]]));
            Assert.That(original.vertices,Is.EqualTo(originalVertices));Assert.That(original.boneWeights,Is.Empty);
            source.baked.sharedMesh.boneWeights=before;
            var names=source.baked.bones.Select(b=>b.name).Reverse().ToArray();
            RiggleDeformationTools.ApplyWeights(original,source.baked.sharedMesh,names,source.source.GetComponent<RiggleMeshEdits>());
            Assert.That(RiggleDeformationTools.Weight(source.baked.sharedMesh.boneWeights[seed],Array.IndexOf(names,"Riggle_LeftUpperArm")),Is.EqualTo(.8f).Within(.00001));
        }
        [Test] public void WeightStrokeSupportsUndoAndRedo()
        {
            var source=Arm;var before=source.baked.sharedMesh.boneWeights;int savedCount=source.source.GetComponent<RiggleMeshEdits>().weights.Count;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Paint(source);Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            var painted=source.baked.sharedMesh.boneWeights;Assert.That(painted,Is.Not.EqualTo(before));
            Undo.PerformUndo();Assert.That(source.baked.sharedMesh.boneWeights,Is.EqualTo(before));Assert.That(source.source.GetComponent<RiggleMeshEdits>().weights.Count,Is.EqualTo(savedCount));
            Undo.PerformRedo();Assert.That(source.baked.sharedMesh.boneWeights,Is.EqualTo(painted));
        }
        [Test] public void ChangedTopologyIsRejectedBeforeSavedVertexEditsCanBeApplied()
        {
            var source=Arm;Paint(source);var altered=Object.Instantiate(source.source.GetComponent<MeshFilter>().sharedMesh);meshes.Add(altered);
            var vertices=altered.vertices;vertices[0]+=Vector3.one*.01f;altered.vertices=vertices;
            Assert.Throws<InvalidOperationException>(()=>RiggleDeformationTools.ApplyWeights(altered,source.baked.sharedMesh,source.baked.bones.Select(b=>b.name).ToArray(),source.source.GetComponent<RiggleMeshEdits>()));
        }
        [Test] public void SculptConvertsWorldDisplacementThroughBentScaledSkinAndKeepsSeams()
        {
            var source=Arm;var edits=source.source.GetComponent<RiggleMeshEdits>();var shape=edits.correctives[0];
            var bone=source.baked.bones.First(b=>b.name==shape.bone);bone.localRotation=shape.restRotation*Quaternion.AngleAxis(65,shape.axis);
            rig.GetComponent<RiggleTwistDistribution>().Evaluate();rig.GetComponent<RiggleCorrectives>().Evaluate();
            int seed=Seed(source);var vertices=World(source);var before=shape.deltas.FirstOrDefault(d=>d.vertex==seed)?.delta??Vector3.zero;
            var matrix=RiggleDeformationTools.SkinMatrix(source.baked,seed);
            RiggleDeformationTools.Sculpt(source,0,vertices,vertices[seed],Vector3.up,.12f,.015f,seed);
            var after=shape.deltas.First(d=>d.vertex==seed).delta;
            Assert.That(Vector3.Distance(matrix.MultiplyVector(after-before),Vector3.up*.015f),Is.LessThan(.00001f));
            var topology=new RiggleTopology(edits.sourceMesh);var deltas=shape.deltas.ToDictionary(d=>d.vertex,d=>d.delta);
            foreach(var pair in deltas)Assert.That(pair.Value,Is.EqualTo(deltas[topology.welded[pair.Key]]));
        }
        [Test] public void CorrectivesAreDirectionalSmoothAndLeaveUnrelatedShapesAlone()
        {
            float Activate(float angle)=>RiggleCorrectives.Activation(Quaternion.AngleAxis(angle,Vector3.back),Quaternion.identity,Vector3.back,Vector3.left,20,90);
            Assert.That(Activate(0),Is.Zero);Assert.That(Activate(-90),Is.Zero);Assert.That(Activate(20),Is.EqualTo(0).Within(.000001));
            Assert.That(Activate(55),Is.EqualTo(.5f).Within(.00001));Assert.That(Activate(90),Is.EqualTo(1).Within(.00001));Assert.That(Activate(20.01f),Is.LessThan(.000001));
            var source=Arm;var mesh=source.baked.sharedMesh;var delta=new Vector3[mesh.vertexCount];delta[0]=Vector3.up*.01f;
            mesh.AddBlendShapeFrame("Unrelated",50,delta,null,null);mesh.AddBlendShapeFrame("Unrelated",100,delta,null,null);
            source.baked.SetBlendShapeWeight(mesh.GetBlendShapeIndex("Unrelated"),37);
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();Undo.RegisterCompleteObjectUndo(mesh,"Refresh corrective shapes");
            RiggleDeformationTools.RefreshShapes(source.baked,source.source.GetComponent<RiggleMeshEdits>());
            Undo.FlushUndoRecordObjects();Undo.CollapseUndoOperations(group);
            Undo.PerformUndo();Assert.That(source.baked.GetBlendShapeWeight(mesh.GetBlendShapeIndex("Unrelated")),Is.EqualTo(37));
            Undo.PerformRedo();
            int index=mesh.GetBlendShapeIndex("Unrelated");Assert.That(mesh.GetBlendShapeFrameCount(index),Is.EqualTo(2));Assert.That(source.baked.GetBlendShapeWeight(index),Is.EqualTo(37));
            var driver=rig.GetComponent<RiggleCorrectives>();driver.Evaluate();driver.enabled=false;
            Assert.That(source.baked.GetBlendShapeWeight(index),Is.EqualTo(37));
        }
        [Test] public void EndingSculptPreviewRestoresBonesAndShapeWeights()
        {
            var source=Arm;var shape=source.source.GetComponent<RiggleMeshEdits>().correctives[0];
            var bone=source.baked.bones.First(b=>b.name==shape.bone);var before=bone.localRotation;
            using(var inspector=new RiggleDeformationInspector(rig))
            {
                inspector.Preview(source,shape);
                Assert.That(Quaternion.Angle(before,bone.localRotation),Is.GreaterThan(60));
                Assert.That(source.baked.GetBlendShapeWeight(source.baked.sharedMesh.GetBlendShapeIndex(shape.name)),Is.EqualTo(100));
            }
            Assert.That(Quaternion.Angle(before,bone.localRotation),Is.LessThan(.01));
            foreach(var b in rig.GetComponent<RiggleCorrectives>().bindings)Assert.That(b.skin.GetBlendShapeWeight(b.Index),Is.EqualTo(0).Within(.001));
        }
        [Test] public void RealRebakePreservesWeightEditsSculptsAndExportsOnlyRuntimeBindings()
        {
            var source=Arm;int seed=Paint(source);var edits=source.source.GetComponent<RiggleMeshEdits>();var saved=edits.correctives[0].deltas.Select(d=>d.delta).ToArray();
            folder="Assets/_EXPERIMENTAL/Riggle/Demo/Generated/DeformationTest_"+Guid.NewGuid().ToString("N");
            var so=new SerializedObject(rig);so.FindProperty("assetFolder").stringValue=folder;so.ApplyModifiedPropertiesWithoutUndo();
            RiggleBuilder.Bake(rig);source=Arm;
            int bone=Array.FindIndex(source.baked.bones,b=>b.name=="Riggle_LeftUpperArm");
            Assert.That(RiggleDeformationTools.Weight(source.baked.sharedMesh.boneWeights[seed],bone),Is.EqualTo(.8f).Within(.00001));
            Assert.That(source.source.GetComponent<RiggleMeshEdits>().correctives[0].deltas.Select(d=>d.delta),Is.EqualTo(saved));
            Assert.That(source.baked.sharedMesh.GetBlendShapeIndex(edits.correctives[0].name),Is.GreaterThanOrEqualTo(0));
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(folder+"/"+rig.name+"_Skinned.prefab");Assert.That(prefab,Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<RiggleMeshEdits>(true),Is.Empty);
            var driver=prefab.GetComponent<RiggleCorrectives>();Assert.That(driver.bindings.Length,Is.EqualTo(4));
            foreach(var b in driver.bindings){Assert.That(b.skin.transform.IsChildOf(prefab.transform),Is.True);Assert.That(b.bone.IsChildOf(prefab.transform),Is.True);}
        }
        [Test] public void ComparisonPairsUseSamePoseWithCorrectionOffAndOn()
        {
            foreach(var original in Object.FindObjectsByType<Riggle>().Where(r=>r!=rig))
            {
                var gallery=original.GetComponent<RigglePoseGallery>();Assert.That(gallery.correctiveRow,Is.Not.Null);
                var models=gallery.correctiveRow.GetComponentsInChildren<Animator>(true);Assert.That(models.Length,Is.EqualTo(4));
                for(int i=0;i<4;i++)
                {
                    var driver=models[i].GetComponent<RiggleCorrectives>();float maximum=driver.bindings.Max(b=>b.skin.GetBlendShapeWeight(b.Index));
                    Assert.That(maximum,i%2==0?Is.EqualTo(0):Is.GreaterThan(0));
                    Assert.That(driver.enabled,Is.False);
                }
            }
        }
    }
}
