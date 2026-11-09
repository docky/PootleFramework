using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Docky.BodyShapeMotion.Editor;
using Object = UnityEngine.Object;

namespace Docky.BodyShapeMotion.Tests
{
    public sealed class BodyShapeBakeTests
    {
        [Test]
        public void AllSixteenBakedClipsReplayCorrectedBonesWithoutTheSolver()
        {
            EditorSceneManager.OpenScene(DoggyBodyShapeLabBuilder.ScenePath);
            var lab = Object.FindAnyObjectByType<BodyShapeLab>();
            foreach (var source in lab.samples.Where(s => s.column == 3))
            {
                var sourceHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(source.clip));
                GameObject playback = null;
                AnimationClip baked = null;
                PlayableGraph graph = default;
                try
                {
                    int steps = Mathf.CeilToInt(source.clip.length * 30);
                    var checkpoints = new[] { 0, steps / 5, steps / 2, steps * 4 / 5, steps - 1 };
                    var poses = new System.Collections.Generic.Dictionary<int, (string path, Vector3 position, Quaternion rotation)[]>();
                    baked = BodyShapeClipBaker.BakeToMemory(source, 30, false, (time, rig) =>
                    {
                        int frame = Mathf.RoundToInt(time / (source.clip.length / steps));
                        if (checkpoints.Contains(frame)) poses[frame] = rig.GetComponentsInChildren<SkinnedMeshRenderer>().SelectMany(r => r.bones).Distinct().Select(b => (AnimationUtility.CalculateTransformPath(b, rig.transform), b.position, b.rotation)).ToArray();
                    });
                    Assert.That(baked.humanMotion, Is.False);
                    Assert.That(baked.length, Is.EqualTo(source.clip.length).Within(.0001));
                    Assert.That(AnimationUtility.GetAnimationClipSettings(baked).loopTime, Is.EqualTo(AnimationUtility.GetAnimationClipSettings(source.clip).loopTime));
                    Assert.That(AnimationUtility.GetAnimationEvents(baked).Length, Is.EqualTo(AnimationUtility.GetAnimationEvents(source.clip).Length));
                    Assert.That(AnimationUtility.GetCurveBindings(baked).Any(b => b.path == ""), Is.False, "Scene/root placement must not be baked");
                    playback = BodyShapeClipBaker.CreateEvaluationCopy(source);
                    playback.GetComponent<BodyShapeSample>().Stop();
                    Object.DestroyImmediate(playback.GetComponent<BodyShapeSample>());
                    Object.DestroyImmediate(playback.GetComponent<BodyShapeAdjustment>());
                    var animator = playback.GetComponent<Animator>();
                    animator.avatar = null; animator.Rebind();
                    graph = PlayableGraph.Create("Baked clip regression"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var clip = AnimationClipPlayable.Create(graph, baked);
                    AnimationPlayableOutput.Create(graph, "Generic", animator).SetSourcePlayable(clip); graph.Play();
                    foreach (int frame in checkpoints)
                    {
                        float time = frame * (source.clip.length / steps);
                        clip.SetTime(time); graph.Evaluate(0);
                        foreach (var bone in poses[frame])
                        {
                            string path = bone.path;
                            var actual = playback.transform.Find(path);
                            Assert.That(Vector3.Distance(bone.position, actual.position), Is.LessThan(.001f), source.clip.name + " " + path + " position at " + frame);
                            Assert.That(RotationError(bone.rotation, actual.rotation), Is.LessThan(.15), source.clip.name + " " + path + " rotation at " + frame);
                        }
                    }
                    Assert.That(AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(source.clip)), Is.EqualTo(sourceHash));
                    Assert.That(source.transform.position.x, Is.EqualTo(9.6f).Within(.0001f));
                }
                finally
                {
                    if (graph.IsValid()) graph.Destroy();
                    if (playback) Object.DestroyImmediate(playback);
                    if (baked) Object.DestroyImmediate(baked);
                }
            }
        }

        [Test]
        public void SmoothedBakeMatchesSequentialPlaybackAndPreservesTheLastSample()
        {
            EditorSceneManager.OpenScene(DoggyBodyShapeLabBuilder.ScenePath);
            var source = Object.FindAnyObjectByType<BodyShapeLab>().samples.First(s => s.row == 0 && s.column == 3);
            var copy = BodyShapeClipBaker.CreateEvaluationCopy(source);
            AnimationClip baked = null;
            try
            {
                baked = BodyShapeClipBaker.BakeToMemory(source, 30, true);
                var sample = copy.GetComponent<BodyShapeSample>(); var adjustment = copy.GetComponent<BodyShapeAdjustment>();
                bool loop = AnimationUtility.GetAnimationClipSettings(source.clip).loopTime;
                int steps = Mathf.CeilToInt(source.clip.length * 30); float dt = source.clip.length / steps;
                if (loop)
                    for (int i = Mathf.CeilToInt(Mathf.Max(.5f, adjustment.profile.smoothingSeconds * 10) / dt); i > 0; i--) sample.Sample(-i * dt, dt);
                var hand = copy.GetComponent<Animator>().GetBoneTransform(HumanBodyBones.LeftHand);
                string path = AnimationUtility.CalculateTransformPath(hand, copy.transform);
                var curves = new[] { "x", "y", "z", "w" }.Select(axis => AnimationUtility.GetEditorCurve(baked, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalRotation." + axis))).ToArray();
                for (int frame = 0; frame <= steps; frame++)
                {
                    float time = frame == steps ? source.clip.length : frame * dt;
                    sample.Sample(time, dt, !loop && frame == 0, false); DoggyBodyShapeLabBuilder.FinishPose(sample);
                    var q = new Quaternion(curves[0].Evaluate(time), curves[1].Evaluate(time), curves[2].Evaluate(time), curves[3].Evaluate(time));
                    Assert.That(RotationError(hand.localRotation, q), Is.LessThan(.1f), "Smoothed hand at " + frame);
                }
                Assert.That(curves[0].keys.Last().time, Is.EqualTo(source.clip.length).Within(.00001));
            }
            finally { copy.GetComponent<BodyShapeSample>().Stop(); Object.DestroyImmediate(copy); if (baked) Object.DestroyImmediate(baked); }
        }
        static double RotationError(Quaternion a, Quaternion b)
        {
            double dot=(double)a.x*b.x+(double)a.y*b.y+(double)a.z*b.z+(double)a.w*b.w;
            double aa=(double)a.x*a.x+(double)a.y*a.y+(double)a.z*a.z+(double)a.w*a.w;
            double bb=(double)b.x*b.x+(double)b.y*b.y+(double)b.z*b.z+(double)b.w*b.w;
            return 2*System.Math.Acos(System.Math.Min(1,System.Math.Abs(dot)/System.Math.Sqrt(aa*bb)))*180/System.Math.PI;
        }

    }
}
