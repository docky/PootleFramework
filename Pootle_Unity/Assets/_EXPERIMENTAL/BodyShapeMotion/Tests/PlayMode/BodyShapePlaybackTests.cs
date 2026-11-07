using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace Docky.BodyShapeMotion.Tests
{
    public sealed class BodyShapePlaybackTests
    {
        [UnityTest] public IEnumerator ExportedPrefabPlaysAllSixteenBakedClipsWithoutProceduralDrivers()
        {
            const string folder="Assets/_EXPERIMENTAL/BodyShapeMotion/Generated/Baked Clips";
            var prefabs=UnityEditor.AssetDatabase.FindAssets("t:Prefab",new[]{folder}).Select(UnityEditor.AssetDatabase.GUIDToAssetPath).OrderByDescending(p=>p).ToArray();
            Assert.That(prefabs.Length,Is.GreaterThan(0),"Bake the supplied 16-clip Doggy set first.");
            var go=Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabs.First(p=>p.EndsWith("Doggy Baked Playback.prefab"))));
            try
            {
                go.transform.position=new Vector3(2,0,3);var root=go.transform.position;
                var animator=go.GetComponent<Animator>();animator.fireEvents=false;
                Assert.That(animator.avatar,Is.Null);Assert.That(go.GetComponent<BodyShapeAdjustment>(),Is.Null);Assert.That(go.GetComponent<BodyShapeSample>(),Is.Null);
                Assert.That(go.GetComponentsInChildren<MonoBehaviour>().Where(b=>b).Any(b=>b.enabled),Is.False);
                var clips=animator.runtimeAnimatorController.animationClips.Distinct().ToArray();Assert.That(clips.Length,Is.EqualTo(16));
                var bones=go.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r=>r.bones).Distinct().ToArray();
                foreach(var clip in clips)
                {
                    animator.Play(clip.name,0,.2f);yield return null;
                    var rotations=bones.Select(b=>b.localRotation).ToArray();
                    animator.Play(clip.name,0,.7f);yield return null;
                    Assert.That(bones.Select((b,i)=>Quaternion.Angle(rotations[i],b.localRotation)).Max(),Is.GreaterThan(1),clip.name+" must move limbs");
                    Assert.That(go.transform.position,Is.EqualTo(root),"Baked clips must not overwrite scene placement");
                }
                var before=bones.Select(b=>b.localRotation).ToArray();yield return new WaitForSeconds(.2f);
                Assert.That(bones.Select((b,i)=>Quaternion.Angle(before[i],b.localRotation)).Max(),Is.GreaterThan(.5f),"Baked playback must keep advancing");
            }
            finally{Object.Destroy(go);}
        }
        [UnityTest] public IEnumerator ComparisonsAnimateTogetherAndRootStaysInItsCell()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_EXPERIMENTAL/BodyShapeMotion/Doggy Body Shape Lab.unity",new LoadSceneParameters(LoadSceneMode.Single));
            var lab=Object.FindAnyObjectByType<BodyShapeLab>();lab.Focus(14,true);lab.playing=true;yield return null;
            var samples=lab.samples.Where(s=>s.row==14).OrderBy(s=>s.column).ToArray();
            var starts=samples.Select(s=>s.transform.position).ToArray();
            var hips=samples.Select(s=>s.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.Hips)).ToArray();
            var knees=samples.Select(s=>s.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftLowerLeg)).ToArray();
            var before=knees.Select(t=>t.localRotation).ToArray();
            yield return new WaitForSeconds(.35f);
            for(int i=0;i<4;i++)
            {
                Assert.That(samples[i].transform.position,Is.EqualTo(starts[i]));
                Assert.That(Quaternion.Angle(before[i],knees[i].localRotation),Is.GreaterThan(2),"Limbs must actually animate");
                Assert.That(Quaternion.Angle(hips[0].localRotation,hips[i].localRotation),Is.LessThan(.05f),"Variants must share the same clip time");
            }
            Assert.That(samples[3].GetComponent<BodyShapeAdjustment>().MaxDisplacement,Is.GreaterThan(.005f));
            lab.playing=false;var held=knees[3].localRotation;yield return new WaitForSeconds(.1f);Assert.That(Quaternion.Angle(held,knees[3].localRotation),Is.LessThan(.05f));
            lab.Scrub(.7f);Assert.That(Quaternion.Angle(held,knees[3].localRotation),Is.GreaterThan(1));
        }
        [UnityTest] public IEnumerator StandaloneLayerWorksWithAnAnimatorAndRestoresPoseOnDisable()
        {
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RiggleGenerated/Dog/model_doggy_avatar_sketch_Skinned.prefab");
            var go=Object.Instantiate(prefab);
            try
            {
                foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                var animator=go.GetComponent<Animator>();animator.runtimeAnimatorController=UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/RiggleGenerated/Retarget Lab/Kawaii Walk.controller");animator.applyRootMotion=false;
                var correction=go.AddComponent<BodyShapeAdjustment>();correction.profile=UnityEditor.AssetDatabase.LoadAssetAtPath<BodyShapeProfile>("Assets/_EXPERIMENTAL/BodyShapeMotion/Generated/Doggy Body Shape.asset");correction.strength=1;
                yield return new WaitForSeconds(.25f);
                Assert.That(correction.MaxDisplacement,Is.GreaterThan(.005f));
                Assert.That(float.IsFinite(correction.HandPenetration+correction.FootPenetration),Is.True);
                correction.RestoreAnimationPose();var leg=animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);var unmodified=leg.localRotation;
                correction.Evaluate(0,true);correction.enabled=false;
                Assert.That(Quaternion.Angle(unmodified,leg.localRotation),Is.LessThan(.05f));
            }
            finally{Object.Destroy(go);}
        }
    }
}
