using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Pootle.Editor
{
    public static class PootleMovementFeedbackSetup
    {
        private const string Samples = "Assets/Pootle/Samples/";
        [MenuItem("Pootle/Add Movement Sounds and Particles")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play mode before authoring feedback.");
            ConfigureMaterial();
            var prefab = PrefabUtility.LoadPrefabContents(PootleSceneSetup.SamplePrefab);
            try
            {
                Configure(prefab.GetComponent<PootleCharacter>());
                PrefabUtility.SaveAsPrefabAsset(prefab, PootleSceneSetup.SamplePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var character in root.GetComponentsInChildren<PootleCharacter>(true)) Configure(character);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        private static void ConfigureMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Samples + "Particles/Smoke.mat");
            material.shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            material.shaderKeywords = System.Array.Empty<string>();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Samples + "Particles/smoke.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0); material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_ColorMode", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
        }
        public static void Configure(PootleCharacter character)
        {
            var feedback = character.GetComponent<PootleMovementFeedback>();
            if (!feedback) feedback = character.gameObject.AddComponent<PootleMovementFeedback>();
            var root = character.transform.Find("Movement feedback");
            if (!root) { root = new GameObject("Movement feedback").transform; root.SetParent(character.transform, false); }
            feedback.voiceSource = Source(root, "Jump voice"); feedback.feetSource = Source(root, "Foot contacts");
            feedback.jumps = new AudioClip[5]; feedback.footsteps = new AudioClip[3];
            for (int i = 0; i < 5; i++) feedback.jumps[i] = Clip("jump_" + i);
            for (int i = 0; i < 3; i++) feedback.footsteps[i] = Clip("footstep_regular_" + i);
            feedback.landing = Clip("footstep_regular_0"); feedback.heavyLanding = Clip("stomp");
            feedback.stepDust = Particles(root, "Footstep dust", "Dust", .12f, .35f);
            feedback.jumpDust = Particles(root, "Jump puff", "Dust", .3f, .5f);
            feedback.landingDust = Particles(root, "Landing puff", "Landing Smoke", .45f, .65f);
            EditorUtility.SetDirty(feedback);
        }
        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Samples + "Audio/" + name + ".wav");
        private static AudioSource Source(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (!child) { child = new GameObject(name).transform; child.SetParent(parent, false); }
            var source = child.GetComponent<AudioSource>();
            if (!source) source = child.gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = .65f;
            source.dopplerLevel = 0; source.minDistance = 6; source.maxDistance = 35;
            source.rolloffMode = AudioRolloffMode.Linear; source.volume = 1;
            return source;
        }
        private static ParticleSystem Particles(Transform parent, string name, string template, float radius, float lifetime)
        {
            var child = parent.Find(name);
            if (!child)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Samples + "Particles/" + template + ".prefab");
                child = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, parent)).transform; child.name = name;
            }
            child.localPosition = Vector3.zero; child.localRotation = Quaternion.identity; child.localScale = Vector3.one;
            var particles = child.GetComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.playOnAwake = false; main.loop = false; main.duration = 1;
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 64;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .7f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .6f); main.startSize = .25f;
            main.startColor = new Color(.92f, .9f, .8f, .65f); main.gravityModifier = -.05f;
            main.stopAction = ParticleSystemStopAction.None; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius; shape.rotation = new Vector3(90, 0, 0);
            var velocity = particles.velocityOverLifetime; velocity.enabled = false;
            var color = particles.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); color.color = gradient;
            var size = particles.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .65f, 1, 1.6f));
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Samples + "Particles/Smoke.mat");
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            return particles;
        }
    }
}
