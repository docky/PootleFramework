# Riggle model structure and one button rigging specification

Revision: 7 November 2026. Scope: the current Unity project implementation, exercised in Unity 6000.6.3f1.

For model makers, start with the shorter [model handoff sheet](MODEL_HANDOFF.md).

Riggle turns an upright character, joint landmarks and optional part assignments into a deform skeleton, skinned mesh assets, an optional Humanoid Avatar and a reusable prefab. The dependable route to one-button rigging is a **prepared model contract**: predictable orientation, explicit joints, unambiguous parts and a saved configuration. Arbitrary meshes without that information still need fitting and review.

Sections 1–7 describe current behavior and the preferred authoring convention. Section 8 defines the **proposed one-button workflow**; it is a handoff specification, not an already implemented command. “Riggle v1” here is a target workflow, not a release claim.

## 1 Model delivery contract

Deliver a Unity-imported model/prefab or an editable character GameObject, its meshes/materials/textures, and the authoring configuration. Riggle itself runs in Unity; Blender and Auto-Rig Pro are not runtime or generation dependencies. This project’s control integration requires Ash Assets Control Rig and Unity Animation Rigging.

Preferred authoring rules:

- One character root. Local **+Y is up, +Z is forward, −X is the character’s left**. Side labels refer to the character, not the viewer.
- Root position/rotation zero and scale one for delivery. Apply negative/non-uniform modelling transforms where possible, particularly around accessories. Some nested transforms are supported, but attachment reparenting rejects a shape-changing transform.
- Upright rest pose, preferably T-pose. A-pose arms are supported through temporary Humanoid reference-pose calibration; pose/landmark quality still matters. Separate arms from the torso and legs from each other.
- Feet near the ground; toe shape should agree with forward. Toe/heel reach assists control-rig facing detection, but unusual or symmetric feet need an explicit facing choice.
- Enough vertices around joints to bend. No weighting scheme can bend a single rigid polygon into a smooth elbow.
- Keep decorative objects away from the landmark search group. Use unique, meaningful child paths; reserve `Riggle_Bones`, generated `Riggle_Skin_*`, control-rig and `FK_*` containers for generated output.

The core baker consumes readable **MeshFilter + MeshRenderer** sources. It does not directly rebind an imported SkinnedMeshRenderer. The helper can prepare an already-skinned import by sampling its current mesh pose into independent static source meshes. Supply the intended rest pose; existing skin weights and the imported skeleton are not retained as the new Riggle rig.

`sourceMeshes` can explicitly select the input MeshFilters. If empty, eligible descendant MeshFilters are collected, including inactive descendants. Named-part modes separately reject hidden/inactive parts being merged. Use an explicit source selection to exclude scenery and unwanted geometry.

## 2 Three separate naming systems

A joint reference, a rigid bone assignment and a bending region are different things. For example:

| Name and context | Meaning |
|---|---|
| Empty `Shoulder.L` in references | Left upper-arm joint location |
| Mesh/group `UpperArm.L` in Named Rigid Parts | All its vertices follow the left upper-arm bone |
| Mesh/group `Arm.L` in Named Body Parts | Vertices may blend between left upper/lower arm bones |
| `HeadTop` reference | Crown/end of head, not the head joint |
| `Head` mesh/group | Head geometry; not a valid crown reference |

Use the canonical names below for interchange. Compatibility aliases exist, but the three parsers are not one universal naming grammar. Do not rely on arbitrary descriptive suffixes being ignored.

## 3 Joint references and part origins

Preferred reference objects are empty descendants under a dedicated `Reference points` group. Assign that group as the reference search root.

| Canonical empty name | Position |
|---|---|
| `Pelvis` | Central hips/pelvis reference and symmetry-plane X |
| `Neck` | Top of torso/base of neck |
| `HeadTop` | Crown of head, excluding hats and ears |
| `Shoulder.L`, `Shoulder.R` | Upper-arm pivots |
| `Elbow.L`, `Elbow.R` | Lower-arm pivots |
| `Wrist.L`, `Wrist.R` | Hand pivots |
| `Knee.L`, `Knee.R` | Lower-leg pivots |
| `Ankle.L`, `Ankle.R` | Foot pivots |

There are **13 resolved landmarks**. A symmetric model can provide **eight empties**: the three central points and all five left-side points. With missing-side inference enabled, the five opposite points are reflected in character-local space using `mirroredX = 2 × pelvisX − sourceX`. Either side can supply the reference; explicitly assigned points on both sides take precedence over inference. This mirrors landmark positions, **not meshes or limbs**. Asymmetrical characters should supply both sides.

Recognized marker examples include `Shoulder.L`, `Elbow_L`, `Wrist-R`, `LeftKnee` and `rightfoot_ref`. Central aliases include `Hips`/`HipCenter` for Pelvis and `Crown`/`HeadTip` for HeadTop. Sided aliases include `UpperArm` → Shoulder, `Forearm`/`LowerArm` → Elbow, `Hand` → Wrist, `Shin`/`LowerLeg` → Knee and `Foot` → Ankle. `ref`, `reference`, `marker`, `point` and `riggle` tokens can be ignored by the marker parser.

Conflicting sides such as `LeftWrist.R` are rejected. Duplicate candidates are reported as ambiguous and are not automatically assigned. Marker names should not use Blender duplicate suffixes such as `.001`; numeric duplicate stripping is provided for part names, not marker names. Existing explicit assignments are preserved during a scan.

A mesh/group pivot can supply a reference **only by explicit opt-in** through `RiggleOriginReference`:

- Set `landmark` to the joint it represents; zero `localOffset` uses the pivot.
- The offset is in that source object’s local space, including rotation and scale.
- Editing the landmark adjusts the offset, not the geometry’s transform.
- One origin component represents one joint. An arm pivot at the shoulder does not also supply elbow and wrist positions.

Generated bones, control-rig objects and preserved imported originals are excluded from reference discovery. Marked accessories in named-part modes cannot become source references. Stored fitted landmarks are a fallback when references are absent; the proposed one-button contract requires that fallback to be explicitly reviewed.

## 4 Geometry assembly modes

| Mode | Intended models | Binding and output |
|---|---|---|
| `SmoothSkin` | Continuous characters or meshes needing smooth automatic weights | Default mode. One generated skin per source MeshFilter; up to four influences per vertex. |
| `NamedRigidParts` | Robots, skeletons, rigid segmented retro models | One target bone per part, weight 1. Parts merge into a material-preserving skin; marked accessories remain separate. |
| `NamedBodyParts` | Disconnected chunks that bend internally | Each chunk only considers its allowed anatomical chain. Chunks merge by default; selected limbs/chunks can remain separate skins on the same skeleton. |

### Smooth Skin

The default `ContiguousSurface` solver propagates influence along welded triangle connectivity. It is a surface/geodesic method, **not voxel or volumetric skinning**. Coincident seams can share weight information without rewriting the source topology. Separate islands do not become connected just because they are nearby.

`RiggleSkinBinding` can constrain a whole mesh or individual connected islands; island overrides identify an island by a seed vertex. Recheck those assignments after topology changes. This component has its own region definitions: for example its Torso region includes Neck, whereas the named `Torso` chunk below excludes Neck. Do not substitute one configuration for the other by name alone.

Weight painting and corrective-shape edits belong to the Smooth Skin workflow. Rebuilds retain compatible stored edits; topology/bone changes require validation. `^` accessory naming is **not interpreted in Smooth Skin**.

### Named Rigid Parts

Canonical central names: `Hips`, `Spine`, `Chest`, `UpperChest`, `Neck`, `Head`.

Canonical sided names: `Shoulder.L/R`, `UpperArm.L/R`, `LowerArm.L/R`, `Hand.L/R`, `UpperLeg.L/R`, `LowerLeg.L/R`, `Foot.L/R`. Here `.L/R` means two separate names, such as `UpperArm.L` and `UpperArm.R`.

Full generated names also work, such as `Riggle_LeftUpperArm`. Supported aliases include `Pelvis`, sided `Forearm`/`Elbow`, `Wrist`, `Thigh`, `Shin`/`Knee` and `Ankle`. A trailing `(2)` or `.001` is stripped from part names. A named group passes its assignment to unnamed mesh descendants; a nearer recognized name overrides the inherited assignment.

### Named Body Parts

Names below are mesh names or group names. Sided examples show `.L`; `.R` works equivalently.

| Canonical chunk | Allowed main bones |
|---|---|
| `Arm.L` or `ArmWithoutHand.L` | LeftUpperArm, LeftLowerArm |
| `ArmWithHand.L` | Above, plus LeftHand |
| `Leg.L` or `LegWithoutFoot.L` | LeftUpperLeg, LeftLowerLeg |
| `LegWithFoot.L` | Above, plus LeftFoot |
| `Torso` or `TorsoWithoutNeck` | Hips, Spine, Chest, UpperChest |
| `TorsoWithNeck` | Above, plus Neck |
| `Head` or `HeadWithoutNeck` | Head |
| `HeadWithNeck` | Head, Neck |
| `Ear.L`, `Tail`, `Wing.L`, `Ponytail`, `Ponytail.L`, `Ponytail.R` | The corresponding enabled appendage chain |

Torso chunks never consider shoulder/arm bones. Arm chunks exclude the clavicle/Shoulder bone. Twist helpers remain within their associated allowed limb region. Exact bone assignments such as `UpperArm.L` also work in this mode and remain rigid.

Add `RiggleBodyPart` to override arbitrary names: `kind`, `side`, `includeHandOrFoot`, `includeNeck`, `keepSeparateRenderer`. The component takes precedence over a parsed chunk name. An unnamed descendant inherits the nearest scope; a recognized child scope or exact bone assignment overrides it.

`separateLimbMeshes` retains arms, legs, ears, tails, wings and ponytails as separate skinned outputs. `keepSeparateRenderer` can retain another specific chunk. This provides a basis for later part replacement; automatic rubber-hose limb generation/replacement is **not implemented**.

Both named modes require child parts rather than a mesh on the Riggle root. Merged sources must be active, visible, readable triangle meshes, with material count matching submesh count, and without blend shapes or stored deformation edits. Unassigned parts are errors, not silently discarded. Merging preserves material use; multiple material submeshes do not imply a single draw call.

### Rigid accessories

The default attachment symbol is `^`, configurable per Riggle root. Doubling it (`Head^^ Alternate hat`) attaches the object separately and starts it inactive; custom symbols double likewise. Prefab export retains the hidden alternative and its components. Rebake reapplies the hidden default; Clear restores original active state and parent/local transform. Prefer explicit names:

- `Head^ Hat`
- `UpperChest^ Backpack`
- `Hand.L^ Tool`

The marked object and its descendants remain separate and are parented to the named bone, preserving their world transform. A marked child can inherit an exact bone from a named parent; an ambiguous multi-bone region such as Torso is insufficient. Clear/rebuild restores the recorded original parent, sibling order and local pose. Reparenting fails if non-uniform transforms would change the accessory’s shape.

## 5 Optional ears, tail, wings and ponytails

`RiggleAppendages` enables these explicitly; matching mesh names alone do not create chains. All optional features start disabled.

| Feature | Skeleton parent | Default weighted segments |
|---|---|---:|
| Two animal ears | Head | 4 per ear |
| Long tail | Hips | 8 |
| Two back wings | UpperChest | 4 per wing |
| Middle / Left / Right ponytails (independent) | Head | 5 points including tip (4 deforming segments) |

Ear/tail/wing chains need 2–16 segments. Ponytails need 2–7 points including the unweighted tip (1–6 deforming segments), with 2–7 authored guides. Each chain needs a positive weighting radius and at least two distinct, finite root-to-tip guide points. Guides use character-local positions or a source transform plus a local offset. Right-ear/right-wing guides can mirror the left; the meshes themselves must still be supplied. Guide transforms are assigned explicitly; there is no general appendage-empty name scanner.

Generated names use two-digit segments: `Riggle_LeftEar01`, `Riggle_RightEar01`, `Riggle_Tail01`, `Riggle_LeftWing01`, `Riggle_RightWing01`, continuing through the configured count. Ponytails use `Riggle_MiddlePonytail01`, `Riggle_LeftPonytail01`, `Riggle_RightPonytail01`, up to `06`. Each chain has an unweighted `End` helper. For rigid segment targeting, names such as `Ear01.L` and `Tail03` address individual generated segments. The target feature and segment must exist.

These are extra Generic bones, not Humanoid muscle mappings. `FK_LeftEar01`-style controls and `RiggleAppendageRig` drive them. The per-chain `externalMotion` flag relinquishes control to animation or a dynamic bone system. It defaults on for ponytails and is optional for ears (`dynamicEars`). Existing ear/tail/wing FK setups retain their behavior. Ponytails default to horizontal paths with independent Y directions (Middle 180°, Left −90°, Right 90°); ponytail and ear paths also accept XYZ orientation around their root, with ear mirroring reflecting orientation. There is no automatic ear/tail physics or wing-flight solver.

## 6 Example source and generated structures

This is a preferred **Named Body Parts authoring layout**, not a mandatory group-name parser. `Geometry`, `Accessories` and `Reference points` are organizational names; the child names carry meaning.

```text
Character_Name                         Riggle + RiggleReferencePoints
├── Geometry
│   ├── TorsoWithNeck                   mesh or group of meshes
│   ├── Head                            mesh or group of meshes
│   ├── ArmWithHand.L
│   ├── ArmWithHand.R
│   ├── LegWithFoot.L
│   └── LegWithFoot.R
├── Accessories
│   ├── Head^ Hat
│   └── UpperChest^ Backpack
└── Reference points                    empty; reference search root
    ├── Pelvis
    ├── Neck
    ├── HeadTop
    ├── Shoulder.L
    ├── Elbow.L
    ├── Wrist.L
    ├── Knee.L
    └── Ankle.L                         right side inferred if enabled
```

The generated main skeleton is fixed in structure; every bone below carries the `Riggle_` prefix:

```text
Riggle_Bones
└── Riggle_Hips
    ├── Riggle_Spine
    │   └── Riggle_Chest
    │       └── Riggle_UpperChest
    │           ├── Riggle_Neck → Riggle_Head → Riggle_HeadEnd
    │           ├── Riggle_LeftShoulder → Riggle_LeftUpperArm
    │           │   → Riggle_LeftLowerArm → Riggle_LeftHand → Riggle_LeftHandEnd
    │           └── Riggle_RightShoulder → Riggle_RightUpperArm
    │               → Riggle_RightLowerArm → Riggle_RightHand → Riggle_RightHandEnd
    ├── Riggle_LeftUpperLeg → Riggle_LeftLowerLeg → Riggle_LeftFoot → Riggle_LeftFootEnd
    └── Riggle_RightUpperLeg → Riggle_RightLowerLeg → Riggle_RightFoot → Riggle_RightFootEnd
```

There are 20 main Humanoid bones and five unweighted end helpers. With `distributeTwist` enabled, another 24 helper bones are generated: `_Twist1`, `_Twist2`, `_Twist3` under each upper/lower arm and leg, for example `Riggle_LeftLowerArm_Twist1`. Smooth/body-part weighting can use them; rigid assignment does not gain extra influences. Appendages add their configured chains separately. Feet have end helpers, not mapped Humanoid toe bones; there are no finger or facial bones.

Current output conventions:

| Output | Convention |
|---|---|
| Smooth skin scene object | `Riggle_Skin_<source>` under that source transform, at identity |
| Named merged skin | `Riggle_MergedParts` under `Riggle_Bones` |
| Separate named chunk skin | `Riggle_Part_<sanitized scope name>` under `Riggle_Bones` |
| Smooth mesh asset | `<character>_<source>_Riggle.asset` |
| Named mesh asset | `<character>_MergedParts.asset` or `<character>_Part_<scope>.asset` |
| Humanoid Avatar | `<character>_RiggleHumanoid.asset`, when valid |
| Runtime prefab | `<character>_Skinned.prefab` |

Default asset root is `Assets/RiggleGenerated`; the helper prepares `Assets/RiggleGenerated/<character>/`, with static copies under `Sources/`. Mesh and Avatar paths are made unique on rebuild. Saving the prefab uses its stable sanitized name and updates that path. Old generated mesh/Avatar assets are not automatically deleted.

## 7 Expected use and ownership

Keep the **authoring scene** for references, original sources, fitting, painting, appendage guides and rebuilds. Use the **generated prefab** for gameplay. Export removes Riggle authoring components, source MeshFilter/MeshRenderer components for baked sources, and preserved imported originals; it keeps the generated skins, bones, necessary runtime drivers and marked accessories. Some empty organizational/reference transforms may remain; the exporter is not a general hierarchy-pruning pass.

For Humanoid use, enable `tryHumanoid` and require a valid human Avatar. The raw Riggle component defaults this off; helper preparation enables it. Avatar failure leaves a usable Generic rig unless an existing control-rig reconnection requires Humanoid success. Do not label Generic output as Humanoid-ready.

The current helper is **Tools → Riggle → Rig Helper**, with Model → References → Fit → Bake → Test → Save. Its bake action can build skeleton, skin and controls, then save the prefab. It is not an unattended arbitrary-model pipeline.

Body pose ownership is explicit through `ControlRigAnimation`:

- **Automatic:** an assigned Animator Controller owns the pose; otherwise controls do.
- **Animation:** use for external PlayableGraphs as well as animation playback.
- **Controls:** use IK/FK controls for posing. Pose-preserving switching is available in the control rig.

Choose root motion deliberately. Fixed manual IK/hip targets must not pin an animation’s limbs while its root moves. The Riggle-specific control-rig variant is selected during control construction for a Riggle character. Twist/corrective drivers operate on the resulting pose; appendages have their separate ownership rule above.

Inspect weights using the read-only influence view in the Riggle Inspector; use visible bend/pose tests and actual animation playback before accepting a model. Humanoid validity alone does not certify good deformation or collision-free motion.

Body Shape Motion is a **separate optional post-animation layer** for belly/shoe clearance. It needs character-specific fitting and is not part of the Riggle model contract. Dense character-specific animation baking is an optional downstream export, not a default rig-generation output.

## 8 Proposed one button workflow

### What the button should promise

**“Generate a validated rig from a compliant prepared character.”** It should not promise to infer every joint or produce production-quality weights from an arbitrary model.

Use one canonical input profile: character identity/output location, explicit assembly mode, reference assignments and inference policy, approved stored landmarks if any, source selection, weighting/twist options, appendage definitions, Humanoid requirement and intended pose/root-motion ownership. These map to existing authoring components; a versioned portable manifest would be a new integration feature.

Recommended defaults: ContiguousSurface weighting; twist enabled for deforming modes; Humanoid required for the gameplay biped preset; missing-side inference allowed only for declared symmetric characters; `^` attachments; appendages explicitly opt-in; no animation baking. Do not guess assembly mode from a single ambiguous name such as Head.

### Processing sequence

1. **Inspect without modifying output.** Resolve sources, markers, part scopes and accessories. Report every mesh’s role. Reject duplicate/conflicting markers and unassigned named parts. Confirm readable geometry, orientation, rest pose, transform suitability and an output path under Assets.
2. **Prepare sources while preserving semantics.** For a compliant static hierarchy, use it directly or duplicate it intact. For a pre-skinned model, preserve joint-reference positions, group scopes, origin mappings and attachment relationships before converting geometry.
3. **Resolve all 13 landmarks.** Record whether each is explicit, inferred or approved stored data. Bounds fitting supplies suggestions only. If a required point is ambiguous or unreviewed, stop at `NeedsReview` and open the fitting step.
4. **Validate anatomy and optional chains.** Check finite coordinates, meaningful height and segment lengths, pelvis below neck below crown, laterality/facing, and appendage guide validity. The existing bake checks crown-to-pelvis distance ≥0.1 world units and selected limb segments ≥2% of that distance; a production preflight should also check all remaining joints and transforms.
5. **Generate into a staging output.** Build skeleton, constrained weights, optional Avatar, appendages and accessories. For a Humanoid-required preset, invalid/non-human Avatar is a failed generation, not silent success.
6. **Configure runtime ownership and controls.** Build/refit controls only after a valid Humanoid Avatar, configure Animation/Automatic/Controls and root motion, and avoid competing pose drivers.
7. **Validate output and show a result.** Check bone references, bind-pose counts, finite normalized weights, per-part influence restrictions, accessory world transforms and renderer bounds. Exercise knee/elbow bends, twist, root motion and moving limbs on real frames; provide a pose/contact-sheet preview for visual review.
8. **Publish prefab and report.** Save the authoring state and generated assets only after successful validation. Return paths and warnings. Preserve source assets and previously published output on failure.

### Current integration gaps to account for

- `RiggleModelPreparation.Create` currently flattens renderer copies into `Riggle sources`. It does **not** generally carry arbitrary reference empties, inherited part-group semantics or origin components into the new editable hierarchy. Its imported-bone hints cover a narrow name list. A one-button wrapper must address this before claiming to support prepared named models through that path.
- The existing builder returns `void`, uses logs for some failures and catches/undoes some bake exceptions. The wrapper must check actual output/postconditions rather than treat “no thrown exception” as success.
- Current bake/export is not an atomic publication transaction. Failure can leave newly created assets, and the prefab exporter writes a stable path. Staging/rollback and an explicit replace/version policy are additional orchestration work.
- The editor helpers are largely internal APIs in `Riggle.Editor`; expose a supported orchestration entry point rather than using reflection as the shipping interface.

Suggested result states: `SuccessHumanoid`, `SuccessGeneric` only when explicitly allowed, `NeedsReview`, `Failed`. Return source identity, settings/version, marker provenance, mesh-to-bone/scope assignments, generated asset paths, Avatar status, validation results and actionable warnings. Re-running with the same source/settings must not duplicate controls or attachments; record old asset references so cleanup is deliberate.

### Acceptance examples

| Input | Required result |
|---|---|
| Symmetric biped with eight canonical empties | Resolve all 13 landmarks and report five inferred points |
| Explicit asymmetric right wrist | Use the explicit position, not a mirrored replacement |
| Two `Elbow.L` candidates | NeedsReview; never pick one by hierarchy order |
| Torso next to a large arm | No arm influence on the named Torso chunk |
| `Arm.L` plus a separate `Hand.L` | Arm excludes Hand; hand binds rigidly |
| `Head^ Hat` | Separate object follows Head without shifting at bind time |
| `Tail` mesh but tail feature disabled | Actionable failure identifying the missing chain |
| Valid Avatar with root moving but fixed limbs | Validation failure; check animation/control ownership |
| Rebuild after a source-topology change | Revalidate island/paint/corrective data before publication |

## 9 Implementation reference map

Paths below are relative to this spec. These are the current implementation points to consult when changing the contract.

- [Core data and source collection](Core/Runtime/Riggle.cs)
- [Reference names and origin assignment](Core/Editor/RiggleReferenceSetup.cs), [reference precedence](Core/Runtime/RiggleReferencePoints.cs)
- [Rigid names and attachment planning](Core/Editor/RiggleRigidParts.cs), [body-part names and allowed bones](Core/Editor/RiggleBodyParts.cs)
- [Appendage generation](Core/Editor/RiggleAppendageBuilder.cs), [appendage authoring data](Core/Runtime/RiggleAppendages.cs)
- [Import preparation](Core/Editor/RiggleModelPreparation.cs), [builder](Core/Editor/RiggleBuilder.cs), [prefab exporter](Core/Editor/RigglePrefabExporter.cs)
- [Current wizard orchestration](Core/Editor/RiggleHelperWindow.cs), [weight diagnostics](Core/Editor/RiggleDiagnostics.cs)
- [Reference contract tests](Demo/Tests/Editor/RiggleReferenceTests.cs), [rigid-part tests](Demo/Tests/Editor/RiggleRigidPartsTests.cs), [body-part tests](Demo/Tests/Editor/RiggleBodyPartsTests.cs)
