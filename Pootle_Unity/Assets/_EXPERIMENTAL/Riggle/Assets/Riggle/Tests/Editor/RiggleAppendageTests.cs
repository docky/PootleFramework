using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Docky.RiggleTools.Editor;
using Object=UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleAppendageTests
    {
        Riggle rig;RiggleAppendages extra;string folder;Material material;
        [SetUp] public void Setup()
        {
            rig=new GameObject("Appendage test").AddComponent<Riggle>();rig.SetFallbackPoints();
            extra=rig.gameObject.AddComponent<RiggleAppendages>();RiggleAppendageBuilder.Defaults(rig,extra);
            folder="Assets/RiggleGenerated/AppendageTest_"+Guid.NewGuid().ToString("N");RiggleModelPreparation.Folder(folder);
            var so=new SerializedObject(rig);so.FindProperty("assemblyMode").enumValueIndex=2;so.FindProperty("assetFolder").stringValue=folder;
            so.FindProperty("tryHumanoid").boolValue=true;so.FindProperty("separateLimbMeshes").boolValue=true;so.ApplyModifiedPropertiesWithoutUndo();
            material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,folder+"/part.mat");
        }
        [TearDown] public void Cleanup()
        {
            if(rig){foreach(var t in rig.GetComponentsInChildren<Transform>(true)){Undo.ClearUndo(t.gameObject);foreach(var c in t.GetComponents<Component>())Undo.ClearUndo(c);}Object.DestroyImmediate(rig.gameObject);}
            AssetDatabase.DeleteAsset(folder);
        }
        MeshFilter Cube(string name)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(rig.transform,false);go.transform.localPosition=new Vector3(0,1.5f,0);go.transform.localScale=Vector3.one*.2f;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.GetComponent<MeshFilter>();
        }
        [Test] public void OptionalFeaturesStartDisabledAndOnlySelectedChainsAreGenerated()
        {
            Assert.That(RiggleBuilder.MakeBoneSpecs(rig).Any(b=>b.appendage!=null),Is.False);
            extra.bunnyEars=true;
            var bones=RiggleBuilder.MakeBoneSpecs(rig).Where(b=>b.appendage!=null).ToArray();
            Assert.That(bones.Count(b=>b.skinned),Is.EqualTo(8));Assert.That(bones.Where(b=>b.name.EndsWith("01")).All(b=>b.parent=="Riggle_Head"),Is.True);
            Assert.That(bones.All(b=>b.human==HumanBodyBones.LastBone),Is.True);
        }
        [TestCase("Ear02.L","Riggle_LeftEar02")]
        [TestCase("Riggle_RightWing04","Riggle_RightWing04")]
        [TestCase("Tail08","Riggle_Tail08")]
        public void NumberedBonesAcceptSideNames(string name,string expected)
        {Assert.That(RiggleRigidParts.TryBone(name,out var bone),Is.True);Assert.That(bone,Is.EqualTo(expected));}
        [Test] public void GuideOriginsAndMirroringRespectCharacterLocalPlane()
        {
            extra.bunnyEars=true;rig.MirrorRight=false;rig.SetLocal(Riggle.Point.Pelvis,new Vector3(.2f,.85f,0));
            rig.transform.SetPositionAndRotation(new Vector3(7,2,4),Quaternion.Euler(7,65,8));rig.transform.localScale=new Vector3(1.2f,.8f,1.3f);
            var source=Cube("Ear.L").transform;source.localPosition=new Vector3(-.25f,1.7f,0);source.localRotation=Quaternion.Euler(5,30,10);
            extra.leftEar.guides[0].reference=source;extra.leftEar.guides[0].offset=new Vector3(.1f,0,0);
            var defs=RiggleAppendageBuilder.Definitions(rig).ToArray();
            var left=RiggleAppendageBuilder.Samples(rig,defs[0]).Select(rig.transform.InverseTransformPoint).ToArray();
            var right=RiggleAppendageBuilder.Samples(rig,defs[1]).Select(rig.transform.InverseTransformPoint).ToArray();
            Assert.That(Vector3.Distance(rig.transform.TransformPoint(left[0]),source.TransformPoint(extra.leftEar.guides[0].offset)),Is.LessThan(.00001));
            for(int i=0;i<left.Length;i++)Assert.That(Vector3.Distance(right[i],new Vector3(.4f-left[i].x,left[i].y,left[i].z)),Is.LessThan(.00001));
            extra.mirrorEars=false;extra.rightEar.guides[0].position=new Vector3(.7f,1.8f,0);
            Assert.That(Vector3.Distance(RiggleAppendageBuilder.Path(rig,RiggleAppendageBuilder.Definitions(rig).Last())[0],rig.transform.TransformPoint(extra.rightEar.guides[0].position)),Is.LessThan(.00001));
        }
        [Test] public void AllChainsHaveScopedWeightsSelfContainedControlsAndHumanoidExport()
        {
            extra.bunnyEars=extra.longTail=extra.backWings=true;
            Cube("Torso");foreach(string n in new[]{"Ear.L","Ear.R","Tail","Wing.L","Wing.R"})Cube(n);
            RiggleBuilder.Bake(rig);Assert.That(rig.BakedAvatar && rig.BakedAvatar.isHuman,Is.True);
            var driver=rig.GetComponent<RiggleAppendageRig>();Assert.That(driver.chains.Length,Is.EqualTo(5));
            foreach(var binding in rig.BakedSources)
            {
                var scope=RiggleBodyParts.Resolve(binding.source.transform);var skin=binding.baked;
                Assert.That(skin.bones.All(b=>scope.bones.Contains(b.name)),Is.True,binding.source.name);
                foreach(var w in skin.sharedMesh.boneWeights)Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.00001));
            }
            foreach(var chain in driver.chains)
            {
                var before=chain.bones.Last().position;chain.controls[0].localRotation*=Quaternion.Euler(23,17,19);driver.Evaluate();
                Assert.That(Vector3.Distance(chain.bones.Last().position,before),Is.GreaterThan(.01),chain.name);
                for(int i=0;i<chain.bones.Length;i++)Assert.That(Vector3.Distance(chain.bones[i].position,chain.controls[i].position),Is.LessThan(.00001),chain.name);
            }
            RiggleAppendageBuilder.Pose(driver,0);var prefab=RigglePrefabExporter.Save(rig);
            Assert.That(prefab.GetComponent<RiggleAppendages>(),Is.Null);Assert.That(prefab.GetComponent<RiggleAppendageRig>().chains.Length,Is.EqualTo(5));
            Assert.That(prefab.GetComponent<RiggleAppendageRig>().chains.SelectMany(c=>c.controls.Concat(c.bones)).All(t=>t.IsChildOf(prefab.transform)),Is.True);
            RiggleBuilder.Bake(rig);Assert.That(rig.GetComponent<RiggleAppendageRig>().chains.Length,Is.EqualTo(5));
            Assert.That(rig.GetComponentsInChildren<Transform>().Count(t=>t.name=="FK_LeftEar"),Is.EqualTo(1));
            RiggleBuilder.Clear(rig);Assert.That(rig.GetComponent<RiggleAppendageRig>().chains,Is.Empty);
        }
        [Test] public void InvalidPathsAndDisabledTargetsPreservePreviousOutput()
        {
            extra.longTail=true;Cube("Tail");RiggleBuilder.Bake(rig);var previous=rig.GeneratedBones;
            extra.tail.guides[1].position=extra.tail.guides[0].position;
            Assert.Throws<InvalidOperationException>(()=>RiggleBuilder.Bake(rig));Assert.That(rig.GeneratedBones,Is.EqualTo(previous));
            extra.longTail=false;Assert.Throws<InvalidOperationException>(()=>RiggleBuilder.Bake(rig));Assert.That(rig.GeneratedBones,Is.EqualTo(previous));
        }
        [Test] public void SmoothMeshBindingRestrictsTailAndPreviewPoseSupportsUndo()
        {
            extra.longTail=true;var source=Cube("Source tail");source.gameObject.AddComponent<RiggleSkinBinding>().wholeMesh=RiggleSkinBinding.Part.Tail;
            var so=new SerializedObject(rig);so.FindProperty("assemblyMode").enumValueIndex=0;so.ApplyModifiedPropertiesWithoutUndo();
            RiggleBuilder.Bake(rig);var skin=rig.BakedSources.Single().baked;
            foreach(var w in skin.sharedMesh.boneWeights)
            {
                void Check(int i,float weight){if(weight>0)Assert.That(skin.bones[i].name.StartsWith("Riggle_Tail"),Is.True);}
                Check(w.boneIndex0,w.weight0);Check(w.boneIndex1,w.weight1);Check(w.boneIndex2,w.weight2);Check(w.boneIndex3,w.weight3);
            }
            var driver=rig.GetComponent<RiggleAppendageRig>();var tip=driver.chains[0].bones.Last();var before=tip.position;
            Undo.IncrementCurrentGroup();RiggleAppendageBuilder.Pose(driver,1);Assert.That(Vector3.Distance(before,tip.position),Is.GreaterThan(.01));
            Undo.FlushUndoRecordObjects();Undo.PerformUndo();Assert.That(Vector3.Distance(before,tip.position),Is.LessThan(.00001));
        }
    }
}
