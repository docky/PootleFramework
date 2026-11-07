using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Docky.BodyShapeMotion.Editor
{
    public static class BodyShapeClipBaker
    {
        public const string OutputFolder = DoggyBodyShapeLabBuilder.Folder + "/Generated/Baked Clips";
        static readonly string[] Properties = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };

        sealed class Track
        {
            public EditorCurveBinding binding;
            public readonly List<Keyframe> keys = new List<Keyframe>();
            public void Add(float time, float value) => keys.Add(new Keyframe(time, value));
            public bool[] KeptKeys()
            {
                bool constant = keys.All(k => k.value == keys[0].value);
                var keep = Enumerable.Repeat(!constant, keys.Count).ToArray();
                keep[0] = keep[keep.Length - 1] = true;
                return keep;
            }
            public AnimationCurve Curve(bool[] keep = null)
            {
                keep = keep ?? KeptKeys();
                var values = keys.Where((k, i) => keep[i]).ToArray();
                var curve = new AnimationCurve(values);
                for (int i = 0; i < curve.length; i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                    AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                }
                return curve;
            }
        }

        public static GameObject CreateEvaluationCopy(BodyShapeSample source)
        {
            if (!source || !source.clip || !source.GetComponent<BodyShapeAdjustment>().profile)
                throw new ArgumentException("Choose a sample with an animation and body-shape profile.");
            var prefab = PrefabUtility.GetCorrespondingObjectFromSource(source.gameObject);
            if (!prefab) throw new InvalidOperationException("Baking requires a prefab-backed sample so it can start from the original rest pose.");
            var copy = Object.Instantiate(prefab);
            try
            {
                copy.name = "Body shape bake preview";
                copy.transform.SetPositionAndRotation(new Vector3(0, source.transform.position.y, 0), Quaternion.identity);
                copy.transform.localScale = source.transform.lossyScale;
                copy.SetActive(true);
                foreach (var script in copy.GetComponentsInChildren<MonoBehaviour>(true)) if (script) script.enabled = false;
                var animator = copy.GetComponent<Animator>();
                if (!animator || !animator.isHuman) throw new InvalidOperationException("The source must have a valid Humanoid Animator.");
                animator.runtimeAnimatorController = null;
                animator.fireEvents = false;
                var adjustment = copy.GetComponent<BodyShapeAdjustment>() ?? copy.AddComponent<BodyShapeAdjustment>();
                EditorUtility.CopySerialized(source.GetComponent<BodyShapeAdjustment>(), adjustment);
                adjustment.enabled = false;
                adjustment.manualEvaluation = true;
                var sample = copy.GetComponent<BodyShapeSample>() ?? copy.AddComponent<BodyShapeSample>();
                sample.enabled = false;
                sample.clip = source.clip;
                sample.Initialize();
                return copy;
            }
            catch { Object.DestroyImmediate(copy); throw; }
        }

        // Exact hierarchy curves deliberately avoid a second Humanoid retargeting pass.
        public static AnimationClip BakeToMemory(BodyShapeSample source, int fps = 60, bool smooth = true, Action<float, GameObject> onSample = null)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before baking.");
            if (fps < 15 || fps > 120) throw new ArgumentOutOfRangeException(nameof(fps), "Use 15–120 samples per second.");
            GameObject copy = null;
            AnimationClip result = null;
            try
            {
                copy = CreateEvaluationCopy(source);
                var sample = copy.GetComponent<BodyShapeSample>();
                var adjustment = copy.GetComponent<BodyShapeAdjustment>();
                float duration = source.clip.length;
                if (duration <= 0) throw new InvalidOperationException("Cannot bake a zero-length clip.");
                int steps = Mathf.CeilToInt(duration * fps);
                float dt = duration / steps;
                bool loop = AnimationUtility.GetAnimationClipSettings(source.clip).loopTime;
                if (smooth && loop)
                {
                    int warmup = Mathf.CeilToInt(Mathf.Max(.5f, adjustment.profile.smoothingSeconds * 10) / dt);
                    for (int i = warmup; i > 0; i--) sample.Sample(-i * dt, dt);
                }
                var transforms = copy.GetComponentsInChildren<Transform>(true).Where(t => t != copy.transform).ToArray();
                var paths = transforms.Select(t => AnimationUtility.CalculateTransformPath(t, copy.transform)).ToArray();
                if (paths.Distinct().Count() != paths.Length)
                    throw new InvalidOperationException("Bone/object paths must be unique before baking. Rename duplicate siblings in the source prefab.");
                var transformTracks = paths.Select(p => Properties.Select(n => new Track { binding = EditorCurveBinding.FloatCurve(p, typeof(Transform), n) }).ToArray()).ToArray();
                var renderers = copy.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var shapeTracks = renderers.Select(r => Enumerable.Range(0, r.sharedMesh ? r.sharedMesh.blendShapeCount : 0).Select(i => new Track
                {
                    binding = EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(r.transform, copy.transform), typeof(SkinnedMeshRenderer), "blendShape." + r.sharedMesh.GetBlendShapeName(i))
                }).ToArray()).ToArray();
                var lastRotations = transforms.Select(t => t.localRotation).ToArray();
                for (int frame = 0; frame <= steps; frame++)
                {
                    float time = frame == steps ? duration : frame * dt;
                    sample.Sample(time, dt, !smooth || (!loop && frame == 0), false);
                    DoggyBodyShapeLabBuilder.FinishPose(sample);
                    onSample?.Invoke(time, copy);
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        var t = transforms[i]; var p = t.localPosition; var q = t.localRotation; var s = t.localScale;
                        if (frame > 0 && Quaternion.Dot(lastRotations[i], q) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        lastRotations[i] = q;
                        float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w, s.x, s.y, s.z };
                        for (int j = 0; j < values.Length; j++) transformTracks[i][j].Add(time, values[j]);
                    }
                    for (int i = 0; i < renderers.Length; i++)
                        for (int j = 0; j < shapeTracks[i].Length; j++) shapeTracks[i][j].Add(time, renderers[i].GetBlendShapeWeight(j));
                }
                result = new AnimationClip { name = source.clip.name + "_BodyShape_" + Mathf.RoundToInt(adjustment.strength * 100), frameRate = fps };
                var bindings = new List<EditorCurveBinding>(); var curves = new List<AnimationCurve>();
                foreach (var tracks in transformTracks)
                {
                    foreach (var group in new[] { new[] { 0, 1, 2 }, new[] { 3, 4, 5, 6 }, new[] { 7, 8, 9 } })
                    {
                        var groupKeys = new bool[steps + 1];
                        foreach (int axis in group)
                        {
                            var kept = tracks[axis].KeptKeys();
                            for (int i = 0; i < kept.Length; i++) groupKeys[i] |= kept[i];
                        }
                        foreach (int axis in group) { bindings.Add(tracks[axis].binding); curves.Add(tracks[axis].Curve(groupKeys)); }
                    }
                }
                foreach (var track in shapeTracks.SelectMany(t => t)) { bindings.Add(track.binding); curves.Add(track.Curve()); }
                AnimationUtility.SetEditorCurves(result, bindings.ToArray(), curves.ToArray());
                result.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(result);
                settings.startTime = 0; settings.stopTime = duration; settings.loopTime = loop; settings.loopBlend = false;
                AnimationUtility.SetAnimationClipSettings(result, settings);
                AnimationUtility.SetAnimationEvents(result, AnimationUtility.GetAnimationEvents(source.clip));
                return result;
            }
            catch { if (result) Object.DestroyImmediate(result); throw; }
            finally { if (copy) { copy.GetComponent<BodyShapeSample>()?.Stop(); Object.DestroyImmediate(copy); } }
        }

        public static string BakeSelection(BodyShapeSample[] samples, int fps, bool smooth)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("No samples selected.");
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before baking.");
            EnsureFolder(OutputFolder);
            string folder = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/Doggy " + Mathf.RoundToInt(samples[0].GetComponent<BodyShapeAdjustment>().strength * 100) + "pct " + DateTime.Now.ToString("yyyy-MM-dd HHmmss"));
            AssetDatabase.CreateFolder(OutputFolder, Path.GetFileName(folder));
            var clips = new List<AnimationClip>();
            GameObject playback = null;
            try
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("Bake body-shape clips", samples[i].clip.name, (float)i / samples.Length);
                    var clip = BakeToMemory(samples[i], fps, smooth);
                    AssetDatabase.CreateAsset(clip, AssetDatabase.GenerateUniqueAssetPath(folder + "/" + clip.name + ".anim"));
                    clips.Add(clip);
                }
                var controller = AnimatorController.CreateAnimatorControllerAtPath(folder + "/Baked Motion.controller");
                var machine = controller.layers[0].stateMachine;
                foreach (var clip in clips) machine.AddState(clip.name).motion = clip;
                playback = CreateEvaluationCopy(samples[0]);
                var driver = playback.GetComponent<BodyShapeSample>();
                driver.Stop(); Object.DestroyImmediate(driver);
                playback.GetComponent<BodyShapeAdjustment>().RestoreAnimationPose();
                Object.DestroyImmediate(playback.GetComponent<BodyShapeAdjustment>());
                foreach (var script in playback.GetComponentsInChildren<MonoBehaviour>(true)) if (script) script.enabled = false;
                var animator = playback.GetComponent<Animator>();
                animator.avatar = null; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.fireEvents = true;
                animator.Rebind();
                playback.name = "Doggy · baked body-shape motion";
                clips[0].SampleAnimation(playback, 0);
                PrefabUtility.SaveAsPrefabAsset(playback, folder + "/Doggy Baked Playback.prefab");
                File.WriteAllText(folder + "/Bake notes.txt", "Exact character-specific transform clips, baked at " + fps + " fps.\nSmoothing: " + smooth + ". In-place root, animated hips retained. Original clip durations, loop flags and events retained.\nUse Doggy Baked Playback.prefab or an Animator with Avatar=None on the same skeleton. Do not run BodyShapeAdjustment, twist drivers or IK again. These are Generic hierarchy clips, not retargetable Humanoid muscle clips.\nThe controller contains one state per clip; the first state plays by default.\n\n" + string.Join("\n", samples.Select(s => s.clip.name + " | strength=" + s.GetComponent<BodyShapeAdjustment>().strength + " arms=" + s.GetComponent<BodyShapeAdjustment>().armWeight + " feet=" + s.GetComponent<BodyShapeAdjustment>().footWeight + " | source=" + AssetDatabase.GetAssetPath(s.clip))));
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                return folder;
            }
            finally { if (playback) Object.DestroyImmediate(playback); EditorUtility.ClearProgressBar(); }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
