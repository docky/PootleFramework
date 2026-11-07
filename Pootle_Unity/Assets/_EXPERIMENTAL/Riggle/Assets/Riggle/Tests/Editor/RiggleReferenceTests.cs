using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Docky.RiggleTools.Editor;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleReferenceTests
    {
        Riggle rig;
        [SetUp] public void Setup() { rig = new GameObject("Reference test").AddComponent<Riggle>(); rig.MirrorRight = false; }
        [TearDown] public void Cleanup()
        {
            // Remove records belonging to disposable objects before native test-runner Undo cleanup.
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                Undo.ClearUndo(t.gameObject);
                foreach (var component in t.GetComponents<Component>()) Undo.ClearUndo(component);
            }
            Object.DestroyImmediate(rig.gameObject);
        }
        Transform Marker(string name, Vector3 local)
        { var t = new GameObject(name).transform; t.SetParent(rig.transform, false); t.localPosition = local; return t; }
        [TestCase("Shoulder.L", Riggle.Point.LeftShoulder)]
        [TestCase("Elbow_L", Riggle.Point.LeftElbow)]
        [TestCase("Wrist-R", Riggle.Point.RightWrist)]
        [TestCase("LeftKnee", Riggle.Point.LeftKnee)]
        [TestCase("rightfoot_ref", Riggle.Point.RightAnkle)]
        [TestCase("Head Top", Riggle.Point.HeadTop)]
        public void NamesRecognizeSidesAndAliases(string name, Riggle.Point expected)
        { Assert.That(RiggleReferenceSetup.TryPoint(name, out var point), Is.True); Assert.That(point, Is.EqualTo(expected)); }
        [Test] public void ConflictingNamesAndBareHeadAreNotGuessed()
        { Assert.That(RiggleReferenceSetup.TryPoint("LeftWrist.R",out _),Is.False); Assert.That(RiggleReferenceSetup.TryPoint("Head",out _),Is.False); }
        [Test] public void ReflectionUsesCharacterLocalPelvisAndExplicitSidesWin()
        {
            rig.transform.SetPositionAndRotation(new Vector3(7,3,2), Quaternion.Euler(10,71,3)); rig.transform.localScale=new Vector3(2,1,3);
            Marker("Pelvis", new Vector3(.2f,1,0)); var left=Marker("Wrist.L",new Vector3(-.5f,1.3f,.1f));
            RiggleReferenceSetup.Scan(rig);
            Assert.That(Vector3.Distance(rig.GetLocal(Riggle.Point.RightWrist),new Vector3(.9f,1.3f,.1f)),Is.LessThan(.00001));
            rig.SetLocal(Riggle.Point.RightWrist,new Vector3(1,1.4f,.2f));
            Assert.That(Vector3.Distance(left.localPosition,new Vector3(-.6f,1.4f,.2f)),Is.LessThan(.00001));
            Marker("Wrist_R",new Vector3(.75f,1.2f,0));RiggleReferenceSetup.Scan(rig);rig.MirrorRight=true;
            Assert.That(Vector3.Distance(rig.GetLocal(Riggle.Point.RightWrist),new Vector3(.75f,1.2f,0)),Is.LessThan(.00001));
        }
        [Test] public void DuplicateAndImportedMarkersAreNotSilentlyAssigned()
        {
            Marker("Elbow.L",Vector3.zero);Marker("Elbow_L",Vector3.one);
            var imported=Marker("Original",Vector3.zero);imported.gameObject.AddComponent<RiggleImportedSource>();
            var fake=Marker("Wrist.L",Vector3.zero);fake.SetParent(imported,false);
            Assert.That(RiggleReferenceSetup.Scan(rig),Does.Contain("ambiguous"));
            Assert.That(rig.GetComponent<RiggleReferencePoints>().Assigned(Riggle.Point.LeftElbow),Is.Null);
            Assert.That(rig.GetComponent<RiggleReferencePoints>().Assigned(Riggle.Point.LeftWrist),Is.Null);
        }
        [Test] public void DisablingInferenceUsesStoredPositionAndReferenceEditsUndo()
        {
            rig.SetLocal(Riggle.Point.RightWrist,new Vector3(.8f,1,0));var left=Marker("Wrist.L",new Vector3(-.4f,1,0));
            RiggleReferenceSetup.Scan(rig);var refs=rig.GetComponent<RiggleReferencePoints>();refs.inferMissingSide=false;rig.MirrorRight=true;
            Assert.That(rig.GetLocal(Riggle.Point.RightWrist).x,Is.EqualTo(.8f));
            refs.inferMissingSide=true;Undo.IncrementCurrentGroup();RiggleEditor.RecordPointUndo(rig,Riggle.Point.RightWrist,"Test reference undo");
            rig.SetLocal(Riggle.Point.RightWrist,new Vector3(1.2f,1,0));Undo.FlushUndoRecordObjects();Undo.PerformUndo();
            Assert.That(left.localPosition.x,Is.EqualTo(-.4f).Within(.00001));
        }
        [Test] public void SourceOriginsRespectTransformsOffsetsReflectionAndDoNotMoveMeshWhenEdited()
        {
            rig.transform.SetPositionAndRotation(new Vector3(7,3,2),Quaternion.Euler(10,71,3));rig.transform.localScale=new Vector3(2,1,3);
            Marker("Pelvis",new Vector3(.2f,1,0));RiggleReferenceSetup.Scan(rig);
            var source=GameObject.CreatePrimitive(PrimitiveType.Cube);source.transform.SetParent(rig.transform,false);
            source.transform.localPosition=new Vector3(-.5f,1.3f,.1f);source.transform.localRotation=Quaternion.Euler(12,30,8);source.transform.localScale=new Vector3(.4f,.7f,.3f);
            Assert.That(RiggleReferenceSetup.IsCandidate(rig,source.transform),Is.False);
            var origin=RiggleReferenceSetup.AssignOrigin(rig,source.transform,Riggle.Point.LeftWrist);
            origin.localOffset=new Vector3(.2f,.1f,-.3f);var expected=rig.transform.InverseTransformPoint(source.transform.TransformPoint(origin.localOffset));
            Assert.That(Vector3.Distance(rig.GetLocal(Riggle.Point.LeftWrist),expected),Is.LessThan(.00001));
            var mirrored=expected;mirrored.x=.4f-mirrored.x;
            Assert.That(Vector3.Distance(rig.GetLocal(Riggle.Point.RightWrist),mirrored),Is.LessThan(.00001));
            var matrix=source.transform.localToWorldMatrix;var offset=origin.localOffset;
            Undo.IncrementCurrentGroup();RiggleEditor.RecordPointUndo(rig,Riggle.Point.RightWrist,"Offset origin");
            rig.SetLocal(Riggle.Point.RightWrist,new Vector3(1.1f,1.2f,.2f));
            Assert.That(source.transform.localToWorldMatrix,Is.EqualTo(matrix));
            Assert.That(Vector3.Distance(rig.GetLocal(Riggle.Point.RightWrist),new Vector3(1.1f,1.2f,.2f)),Is.LessThan(.00001));
            Undo.FlushUndoRecordObjects();Undo.PerformUndo();
            Assert.That(Vector3.Distance(origin.localOffset,offset),Is.LessThan(.00001));
            Assert.That(source.transform.localToWorldMatrix,Is.EqualTo(matrix));
        }
        [Test] public void OriginScanUsesExplicitLandmarkAndReassignmentRemovesOldSlot()
        {
            var source=Marker("Arbitrary part name",new Vector3(-.5f,1,0));source.gameObject.AddComponent<MeshFilter>();
            var origin=source.gameObject.AddComponent<RiggleOriginReference>();origin.landmark=Riggle.Point.LeftElbow;
            RiggleReferenceSetup.Scan(rig);var refs=rig.GetComponent<RiggleReferencePoints>();
            Assert.That(refs.Assigned(Riggle.Point.LeftElbow),Is.EqualTo(source));
            RiggleReferenceSetup.AssignOrigin(rig,source,Riggle.Point.LeftShoulder);
            Assert.That(refs.Assigned(Riggle.Point.LeftElbow),Is.Null);Assert.That(refs.Assigned(Riggle.Point.LeftShoulder),Is.EqualTo(source));
            origin.enabled=false;Assert.That(refs.Assigned(Riggle.Point.LeftShoulder),Is.Null);
        }
        [Test] public void AnimatedAccessoriesAndGeneratedBonesCannotBecomeOriginReferences()
        {
            var so=new SerializedObject(rig);so.FindProperty("assemblyMode").enumValueIndex=2;so.ApplyModifiedPropertiesWithoutUndo();
            var accessory=Marker("Head^ Hat",Vector3.zero);var child=new GameObject("Hat detail").transform;child.SetParent(accessory,false);
            Assert.Throws<System.InvalidOperationException>(()=>RiggleReferenceSetup.AssignOrigin(rig,child,Riggle.Point.HeadTop));
            var bones=Marker("Riggle_Bones",Vector3.zero);rig.GeneratedBones=bones;
            Assert.Throws<System.InvalidOperationException>(()=>RiggleReferenceSetup.AssignOrigin(rig,bones,Riggle.Point.Pelvis));
        }
        [Test] public void PreparingSkinnedAndStaticModelsAppliesNonUniformScaleOnce()
        {
            var model=new GameObject("Scaled source test");
            var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);cube.transform.SetParent(model.transform,false);cube.transform.localScale=new Vector3(.5f,2,1);
            var original=cube.GetComponent<MeshFilter>().sharedMesh;var mesh=Object.Instantiate(original);
            var material=cube.GetComponent<MeshRenderer>().sharedMaterial;
            string name="PreparationTest_"+System.Guid.NewGuid().ToString("N");Riggle first=null,second=null;
            try
            {
                first=RiggleModelPreparation.Create(model,name,1.8f);var expected=first.CollectSourceMeshes()[0].sharedMesh.bounds;
                Object.DestroyImmediate(cube.GetComponent<MeshFilter>());Object.DestroyImmediate(cube.GetComponent<MeshRenderer>());
                var bone=new GameObject("Skin bone").transform;bone.SetParent(cube.transform,false);
                mesh.bindposes=new[]{Matrix4x4.identity};mesh.boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=1},mesh.vertexCount).ToArray();
                var skin=cube.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterial=material;skin.bones=new[]{bone};skin.rootBone=bone;
                second=RiggleModelPreparation.Create(model,name,1.8f);var actual=second.CollectSourceMeshes()[0].sharedMesh.bounds;
                Assert.That(Vector3.Distance(expected.size,actual.size),Is.LessThan(.0001f));
            }
            finally
            {
                foreach(var prepared in new[]{first,second})if(prepared)
                {
                    foreach(var t in prepared.GetComponentsInChildren<Transform>(true)){Undo.ClearUndo(t.gameObject);foreach(var c in t.GetComponents<Component>())Undo.ClearUndo(c);}
                    Object.DestroyImmediate(prepared.gameObject);
                }
                Object.DestroyImmediate(model);Object.DestroyImmediate(mesh);AssetDatabase.DeleteAsset("Assets/RiggleGenerated/"+name);
            }
        }
        [TestCase("Fox Girl",13)] [TestCase("Base Mesh",1)]
        public void ImportedCharacterPrefabContainsOnlyGeneratedSelfContainedRig(string name,int skins)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RiggleGenerated/"+name+"/"+name+"_Skinned.prefab");Assert.That(prefab,Is.Not.Null);
            Assert.That(prefab.GetComponent<Animator>().avatar.isValid,Is.True);Assert.That(prefab.GetComponent<Animator>().avatar.isHuman,Is.True);
            Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,Is.EqualTo(skins));
            Assert.That(prefab.GetComponentsInChildren<RiggleImportedSource>(true),Is.Empty);Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true),Is.Empty);
            Assert.That(prefab.GetComponent<RiggleReferencePoints>(),Is.Null);Assert.That(prefab.GetComponentInChildren<ControlRig.ControlRigModes>(true),Is.Not.Null);
            foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Assert.That(skin.sharedMesh.boneWeights.Length,Is.EqualTo(skin.sharedMesh.vertexCount));
                Assert.That(skin.bones.All(b=>b && b.IsChildOf(prefab.transform)),Is.True);
                foreach(var w in skin.sharedMesh.boneWeights)Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.0001));
            }
        }
    }
}
