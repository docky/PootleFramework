# Riggle v0.2 and Humanoid Body Shape Modifier v0.1

Experimental tools, exported 8 November 2026. Riggle has not yet been validated through use in real projects. Exported from Unity 6000.6.3f1; older editor versions have not been verified.

## Core tools only

1. For Riggle, install Animation Rigging (`com.unity.animation.rigging`) with Package Manager first. This export uses version 6.6.0 on Unity 6000.6.3f1.
2. Import `Riggle-v0.2-Core.unitypackage` using Assets > Import Package > Custom Package. It includes the required Control Rig runtime/editor scripts and resource prefabs under `Assets/Ash Assets/Control Rig`; it excludes Control Rig demo scenes/models. Keep these dependencies when importing. Open Tools > Riggle > Rig Helper.
3. Import `Humanoid-Body-Shape-Modifier-v0.1-Core.unitypackage` independently. It needs only Unity: no Riggle, Control Rig, Animation Rigging or demo assets. Add Animation > Humanoid Body Shape Modifier to a Humanoid Animator and assign a fitted profile, created through Assets > Create > Animation > Humanoid Body Shape Modifier > Profile. `BodyShapeProfileFitter.Fit(character, profile)` supplies an initial fit from editor code.

Core imports contain no demo scenes or generated character assets. Existing C# type and assembly identities are retained to preserve references. Import into an empty project first if it already has another version of Control Rig.

## Optional demos

Import both Core packages first, followed by `Riggle-v0.2-Demo.unitypackage`, then `Humanoid-Body-Shape-Modifier-v0.1-Demo.unitypackage`. The modifier demo uses Doggy from the Riggle demo. Riggle Demo itself only requires Riggle Core.

The demos were authored for URP (17.6.0 in the source project). Use a URP project for matching materials. Their test assemblies require Unity Test Framework (1.8.0 in the source project). Each demo includes its referenced example assets and animation FBXs; overlapping source assets keep the same GUIDs. Third-party example characters/animations retain their original licensing terms; these exports are for your project testing, not a license to redistribute those assets.

Scenes live under `Assets/_EXPERIMENTAL/Riggle/Demo/Scenes` and `Assets/_EXPERIMENTAL/Humanoid Body Shape Modifier/Demo/Scenes`. Open the modifier lab through Tools > Humanoid Body Shape Modifier > Demo > Open Doggy Lab. The manually removed baked animation demo is excluded; the live comparisons and baking tools remain available.

`.unitypackage` exports do not install Package Manager dependencies or change project render-pipeline settings. `package-manifests.json` lists the exact assets selected for each package; `SHA256SUMS.txt` records archive checksums.

## Verification

Both Core archives passed compilation in separate clean Unity 6000.6.3f1 projects. Modifier Core compiled without Riggle or Animation Rigging. Riggle Core compiled with Animation Rigging 6.6.0 from the existing local package cache; its resource prefab had no missing scripts and its helper UXML loaded. Archive payloads match the saved manifests and Core archives contain no Demo paths. The optional demos were not run in these scratch projects. See `clean-project-verification.json` for the results.
