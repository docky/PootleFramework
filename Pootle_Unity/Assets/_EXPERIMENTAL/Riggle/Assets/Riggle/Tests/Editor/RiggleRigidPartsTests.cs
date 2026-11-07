using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Docky.RiggleTools.Editor;
using Object = UnityEngine.Object;

namespace Docky.RiggleTools.Tests
{
    public sealed class RiggleRigidPartsTests
    {
        Riggle rig; string folder; Material red, blue;
        [SetUp] public void Setup()
        {
            rig=new GameObject("Rigid test").AddComponent<Riggle>();rig.SetFallbackPoints();
            folder="Assets/RiggleGenerated/RigidTest_"+Guid.NewGuid().ToString("N");RiggleModelPreparation.Folder(folder);
            var so=new SerializedObject(rig);so.FindProperty("assemblyMode").enumValueIndex=1;so.FindProperty("assetFolder").stringValue=folder;so.ApplyModifiedPropertiesWithoutUndo();
            red=new Material(Shader.Find("Universal Render Pipeline/Lit"));blue=new Material(red);
            AssetDatabase.CreateAsset(red,folder+"/red.mat");AssetDatabase.CreateAsset(blue,folder+"/blue.mat");
        }
        [TearDown] public void Cleanup()
        {
            if(rig)
            {
                foreach(var t in rig.GetComponentsInChildren<Transform>(true)){Undo.ClearUndo(t.gameObject);foreach(var c in t.GetComponents<Component>())Undo.ClearUndo(c);}
                Object.DestroyImmediate(rig.gameObject);
            }
            AssetDatabase.DeleteAsset(folder);
        }
        Transform Group(string name,Transform parent=null)
        {var t=new GameObject(name).transform;t.SetParent(parent?parent:rig.transform,false);return t;}
        MeshFilter Cube(string name,Vector3 position,Material material,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent?parent:rig.transform,false);go.transform.localPosition=position;go.transform.localScale=Vector3.one*.2f;
            go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.GetComponent<MeshFilter>();
        }
        [TestCase("Head","Riggle_Head")]
        [TestCase("Riggle_UpperChest","Riggle_UpperChest")]
        [TestCase("UpperArm.L","Riggle_LeftUpperArm")]
        [TestCase("forearm_R","Riggle_RightLowerArm")]
        [TestCase("LeftFoot (2)","Riggle_LeftFoot")]
        [TestCase("UpperLeg.R.001","Riggle_RightUpperLeg")]
        public void RecognizesExactBoneNamesAndSides(string input,string expected)
        {Assert.That(RiggleRigidParts.TryBone(input,out var bone),Is.True);Assert.That(bone,Is.EqualTo(expected));}
        [Test] public void RejectsPartialAndConflictingBoneNames()
        {Assert.That(RiggleRigidParts.TryBone("Headlight",out _),Is.False);Assert.That(RiggleRigidParts.TryBone("LeftHand.R",out _),Is.False);}
        [Test] public void MergePreservesGeometryMaterialsAndRigidBoneOwnership()
        {
            rig.transform.SetPositionAndRotation(new Vector3(3,2,5),Quaternion.Euler(0,35,0));rig.transform.localScale=new Vector3(1.2f,.9f,1.1f);
            var group=Group("UpperArm.L");group.localRotation=Quaternion.Euler(12,8,19);group.localScale=new Vector3(-1,1.2f,.8f);
            var a=Cube("Arm casing",new Vector3(-.5f,1.3f,0),red,group);var b=Cube("Head",new Vector3(0,1.7f,0),blue);
            var expected=new[]{a,b}.SelectMany(f=>f.sharedMesh.vertices.Select(f.transform.TransformPoint)).ToArray();
            RiggleBuilder.Bake(rig);var skin=rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single();var mesh=skin.sharedMesh;
            Assert.That(mesh.vertexCount,Is.EqualTo(expected.Length));Assert.That(mesh.subMeshCount,Is.EqualTo(2));
            Assert.That(skin.sharedMaterials,Is.EqualTo(new[]{red,blue}));
            var actual=mesh.vertices.Select(skin.transform.TransformPoint).ToArray();for(int i=0;i<actual.Length;i++)Assert.That(Vector3.Distance(actual[i],expected[i]),Is.LessThan(.00001));
            for(int i=0;i<mesh.vertexCount;i++)
            {var w=mesh.boneWeights[i];Assert.That(w.weight0,Is.EqualTo(1));Assert.That(w.weight1+w.weight2+w.weight3,Is.Zero);Assert.That(skin.bones[w.boneIndex0].name,Is.EqualTo(i<a.sharedMesh.vertexCount?"Riggle_LeftUpperArm":"Riggle_Head"));}
            var before=mesh.vertices;var arm=skin.bones.First(t=>t.name=="Riggle_LeftUpperArm");arm.localRotation=Quaternion.Euler(0,0,60);
            var baked=new Mesh();try{skin.BakeMesh(baked,true);Assert.That(Vector3.Distance(baked.vertices[0],baked.vertices[1]),Is.EqualTo(Vector3.Distance(before[0],before[1])).Within(.00001));}finally{Object.DestroyImmediate(baked);}
        }
        [Test] public void AccessoriesPreserveIdentityHierarchyExportAndRestoreAcrossRebakes()
        {
            Cube("Chest",Vector3.up,red);var parent=Group("Equipment");parent.localPosition=new Vector3(.1f,.2f,.3f);
            var hat=Group("Head^ Hat",parent);hat.localPosition=new Vector3(0,1.5f,0);hat.localRotation=Quaternion.Euler(0,15,0);
            var brim=Cube("Brim",Vector3.zero,blue,hat);var marker=brim.gameObject.AddComponent<BoxCollider>();var world=hat.localToWorldMatrix;var local=hat.localPosition;
            RiggleBuilder.Bake(rig);Assert.That(hat.parent.name,Is.EqualTo("Riggle_Head"));Assert.That(hat.localToWorldMatrix,Is.EqualTo(world).Using(MatrixComparer.Instance));Assert.That(marker,Is.Not.Null);
            Assert.That(rig.GetComponentsInChildren<SkinnedMeshRenderer>().Single().sharedMesh.vertexCount,Is.EqualTo(24));
            RiggleBuilder.Bake(rig);Assert.That(rig.AttachedObjects.Count,Is.EqualTo(1));Assert.That(hat.parent.name,Is.EqualTo("Riggle_Head"));
            var prefab=RigglePrefabExporter.Save(rig);Assert.That(prefab.GetComponentsInChildren<MeshFilter>(true).Length,Is.EqualTo(1));Assert.That(prefab.GetComponentInChildren<BoxCollider>(true),Is.Not.Null);
            RiggleBuilder.Clear(rig);Assert.That(hat.parent,Is.EqualTo(parent));Assert.That(hat.localPosition,Is.EqualTo(local));Assert.That(rig.AttachedObjects,Is.Empty);Assert.That(brim,Is.Not.Null);
        }
        [Test] public void CustomSymbolInheritanceAndInvalidNamesAreCheckedBeforeReplacingOutput()
        {
            Cube("Chest",Vector3.up,red);var hand=Group("Hand.R");var gear=Group("@ Gadget",hand);Cube("Gadget mesh",Vector3.zero,blue,gear);
            var so=new SerializedObject(rig);so.FindProperty("attachmentSymbol").stringValue="@";so.ApplyModifiedPropertiesWithoutUndo();
            RiggleBuilder.Bake(rig);Assert.That(gear.parent.name,Is.EqualTo("Riggle_RightHand"));var bones=rig.GeneratedBones;
            Cube("Unknown part",Vector3.zero,red);Assert.Throws<InvalidOperationException>(()=>RiggleBuilder.Bake(rig));Assert.That(rig.GeneratedBones,Is.EqualTo(bones));Assert.That(gear.parent.name,Is.EqualTo("Riggle_RightHand"));
        }
        sealed class MatrixComparer : System.Collections.Generic.IEqualityComparer<Matrix4x4>
        {
            public static readonly MatrixComparer Instance=new MatrixComparer();
            public bool Equals(Matrix4x4 a,Matrix4x4 b){for(int i=0;i<16;i++)if(Mathf.Abs(a[i]-b[i])>.00001)return false;return true;}
            public int GetHashCode(Matrix4x4 m)=>0;
        }
    }
}
