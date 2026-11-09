# Riggle model handoff

7 November 2026. Model-authoring convention and target for a one-button rigging workflow. See the [full specification](MODEL_SPEC.md) for exact parser behavior, generated hierarchy and implementation requirements.

## Deliver this

One upright character root, its mesh/material/texture dependencies, joint references and an explicit assembly mode. Deliver the intended rest pose: preferably T-pose, with arms clear of the torso and legs separated. A-pose is supported but still needs good joint placement.

Use local **+Y up, +Z forward, −X character left**. Prefer root position/rotation zero and scale one. Apply negative/non-uniform modelling transforms where possible. Keep enough mesh vertices around bending joints. Toes should point forward; hats and ears must not determine the crown reference.

Riggle generates a new skeleton and weights. Imported skinned models must first be converted to static rest-pose sources; their existing weights are not reused.

## Joint markers

Use empty objects under a dedicated `Reference points` group:

```text
Pelvis
Neck
HeadTop
Shoulder.L
Elbow.L
Wrist.L
Knee.L
Ankle.L
```

These eight markers are sufficient for a declared symmetric character: Riggle can infer the five `.R` markers across the pelvis X plane. For asymmetry, supply both sides. Explicit opposite-side markers win. Missing-side inference does not create missing geometry.

`HeadTop` means the crown, not eyes or the head pivot. `.L` and `.R` refer to the character’s sides. Use one unambiguous marker per joint; avoid numeric duplicate suffixes. Compatibility forms such as `Elbow_L` and `LeftKnee` exist, but use the canonical names above for new deliveries.

A disconnected part’s origin can replace a marker when explicitly assigned through **Riggle Origin Reference**. It represents one joint plus an optional local offset. A shoulder pivot alone does not locate the elbow and wrist.

## Choose one assembly mode

| Mode | Use it for | Names |
|---|---|---|
| Smooth Skin | Continuous, deforming characters | Mesh names are descriptive; use joint markers and optional skin-region assignments. |
| Named Rigid Parts | Robots, skeletons, rigid retro models | Exact bones: `Head`, `UpperChest`, `UpperArm.L`, `LowerArm.L`, `Hand.L`, `UpperLeg.L`, `LowerLeg.L`, `Foot.L`; corresponding `.R` names. |
| Named Body Parts | Separate chunks that bend within a limited region | `Torso`, `TorsoWithNeck`, `Head`, `HeadWithNeck`, `Arm.L`, `ArmWithHand.L`, `Leg.L`, `LegWithFoot.L`; corresponding `.R` names. |

Plain Arm/Leg exclude Hand/Foot. Plain Torso/Head exclude Neck. Torso never considers arm or shoulder bones. An exact bone name inside Named Body Parts still means a rigid assignment.

Names can be on a mesh or a parent group containing unnamed meshes. Use a **Riggle Body Part** component for an explicit override. Named modes require visible, active triangle-mesh children with matching material/submesh counts, and no blend shapes or stored deformation edits on merged sources.

## Accessories and optional appendages

In either named mode, use **`Head^ Hat`**, **`UpperChest^ Backpack`** or **`Hand.L^ Tool`**. These remain separate objects and follow the target bone. Double the marker (`Head^^ Alternate hat`) to attach but start hidden; Clear restores original source visibility. The `^` convention does not apply to Smooth Skin.

Optional bendable parts are `Ear.L`/`Ear.R`, `Tail`, `Wing.L`/`Wing.R`, and `Ponytail` (Middle), `Ponytail.L`/`Ponytail.R`. Enable them in Riggle Appendages and provide ordered root-to-tip guides. Names alone do not enable them. Default segment counts are four per ear/wing and eight for the tail; each chain supports 2–16. Supply both sides’ geometry even when guides are mirrored. Ponytails instead use 2–7 points including the tip (1–6 deforming segments), default five points, with straight horizontal paths and a Y direction. All three attach to Head. Ear paths support XYZ orientation and optional external motion; ponytails default to external motion. Configure the external dynamic bone system on each first bone; Riggle does not simulate bending or flopping.

Enable separate limb meshes when future swapping is needed. This preserves separate skins sharing one skeleton; it does not generate rubber-hose replacements.

## Expected output and use

Riggle creates `Riggle_Bones`, 20 main Humanoid bones, optional twist/appendage bones, skinned mesh assets, a valid Humanoid Avatar where possible, and `<Character>_Skinned.prefab`. Source assets remain available for rebuilding. Keep the authoring scene; use the generated prefab in gameplay.

Use Animator/Animation ownership for playing clips and Controls ownership for manual IK/FK. Choose root motion explicitly. Inspect elbows, knees, shoulders, weights and actual moving animations before acceptance. Fingers, facial rigs, quadrupeds and automatic appendage physics are outside the current scope.

## One button target

**Validate → preserve/prepare sources → resolve references → generate → configure controls → test → publish.**

A compliant prepared character should complete without manual steps. Ambiguous joints, unknown parts or invalid required Humanoid output should stop with an actionable review report. Bounds-based guesses must not be silently certified as correct.

The existing Rig Helper is a staged workflow. A complete one-button wrapper still needs hierarchy-preserving preparation, structured validation/results and safe publication. In particular, the current generic preparation path flattens renderer copies, so it must not be used blindly on a carefully named marker/part hierarchy.
