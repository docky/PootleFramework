# Riggle v0.1 — Unity landmark-to-skeleton tool

**Purpose:** a quick, scene-authored biped skeleton and automatic skin bind for low-poly, cartoon, soft-vinyl and other strongly stylised characters. **No facial rig and no fingers.** Independent C# implementation: no Auto-Rig Pro scripts or other Blender add-on code are included.

**Designed for:** Unity 2022.3+ / Unity 6, including URP. This project integration requires the included Ash Assets Control Rig and Unity Animation Rigging package. The project integration is compiled and exercised in Unity 6000.6.3f1. See the test-scene workflow below.

## Install

Keep the Riggle and Ash Assets Control Rig folders together, with Unity Animation Rigging installed. Unity will pick up the components and editor scripts. Source and output are both Unity-native; there is no Blender dependency.

## Five-minute test

1. In Unity, select **GameObject → 3D Object → Riggle Demo Biped**. The menu spawns a simple cartoon bean character made from built-in primitives, complete with markers.
2. Select its root, which has the **Riggle** component.
3. Turn on **Gizmos** in the Scene view if necessary. Click one of the coloured dots to select it, then drag its position handle. The yellow dot is selected; pink dots are the mirrored side. Alternatively select a dot in the Inspector and use **Place dot by clicking Scene** (click positions a dot on a constant-depth plane, so colliders are unnecessary).
4. Click **BAKE RIGGLE • Bones + Skinning**. The tool generates `Riggle_Bones` and one `Riggle_Skin_*` child per source mesh, and writes cloned skinned mesh assets under `Assets/RiggleGenerated/`.
5. Expand the rig and rotate `Riggle_LeftLowerArm`, `Riggle_LeftUpperArm`, or `Riggle_Head` to inspect the deform. Reset their local rotations to (0,0,0) afterwards.
6. Move a landmark and **Bake** again to rebuild from the original, unmodified meshes.

## Using your own character

- Put the Riggle component on the **character root**, with mesh objects beneath it. The character's local **+Y is up, +Z faces forward, -X is character left**. For best results, start with a T-pose, arms separated from the torso, and feet near the ground.
- The **Fit from mesh bounds** button guesses the landmark positions. This only gives a *starting point*, especially for oversized heads or downturned arms. Correct the markers manually.
- There are **eight independent points by default**, with the corresponding five right-hand points mirrored across the pelvis X coordinate. Uncheck the mirror option for asymmetrical characters. In the inspector, you can edit every point numerically; in the Scene you can drag or click-to-place.
- The **Source Meshes** array can explicitly restrict which MeshFilters are bound. Leave it empty to use all descendant ordinary MeshFilters with MeshRenderers. Input must not be a pre-skinned mesh.
- Click **Bake**. To go back to the original renderers, click **Restore original meshes / Clear**. The source mesh assets and renderer settings are not modified apart from temporarily hiding the source MeshRenderer.

## The dots

| Point | Why it's needed |
|---|---|
| Pelvis | Root/hips transform and mirror centre |
| Neck | Upper end of torso, start of neck/head chain |
| HeadTop | End of head and guide for head joint, **not an eye or facial landmark** |
| LeftShoulder | Location of upper-arm pivot |
| LeftElbow | Lower-arm pivot |
| LeftWrist | Hand pivot (one hand bone; no fingers) |
| LeftKnee | Lower-leg pivot |
| LeftAnkle | Foot pivot |

Right shoulder/elbow/wrist/knee/ankle points are mirrored automatically unless **Mirror right-side landmarks** is disabled. Riggle guesses two hip joints, clavicle pivots, spine/chest/upper-chest joints, head joint and short foot/hand end points from these landmarks and the proportion controls.

## Generated hierarchy

- `Riggle_Bones/Riggle_Hips` is the deform-root bone.
- `Riggle_Spine`, `Riggle_Chest`, `Riggle_UpperChest`, `Riggle_Neck` and `Riggle_Head` form the body/head chain.
- Each side has `Shoulder → UpperArm → LowerArm → Hand` and `UpperLeg → LowerLeg → Foot`.
- End-of-head, hand and foot helper transforms mark bone directions but have **no skin influence**.
- The standard biped has **20 Humanoid bones plus 24 weighted twist helpers** (three on each upper/lower arm and leg), plus five non-deforming tip helpers. Disable **Distribute Twist** and rebake for the original 20-bone skin.
- Generated `Riggle_Skin_*` renderers are children of the original MeshFilter objects (identity transform) to preserve the correct mesh-to-world transform, including nested/unevenly scaled parents.

## Weighting controls

Riggle computes per-vertex distance to bone line segments with adjustable trunk, arm, leg and head envelope radii. Scores are normalised, smoothed along connected mesh triangles, reduced to the **four strongest weighted bones**, and saved together with per-bone bind poses in a new Mesh asset. Vertex positions, normals, UVs, submeshes and materials come from the source copy.

| Setting | Effect |
|---|---|
| Weight Falloff | Higher = tighter regions; lower = gentler blending |
| Joint Softness | Higher = broader joint blends, often nicer for flexible vinyl limbs |
| Surface Smooth Steps | How many times neighbouring surface vertices smooth weight fields |
| Surface Smooth Amount | Fraction of each pass taken from adjacent vertices |
| Trunk / Arm / Leg / Head Radius | Relative radius of each region's weight envelope |
| Hip To Shoulder Width | Estimated spacing of the two thigh/hip roots |
| Head Joint Height | How far from neck to crown the head pivot sits |
| Foot Length Height Ratio | Reach of the approximate forward-pointing foot bone |

This is **not bone-heat weighting or a volumetric solver**. Weight calculation welds coincident seam vertices, while preserving the original mesh topology, UVs and normals. Smoothing shares weights across those seams and never connects separate islands. Expect to rebake after tweaking envelopes and to fix troublesome deformation around attached arms, crotches, overlapping limbs or heavily disconnected geometry with dedicated tooling/weight painting. It deliberately favours a simple, editable, fast workflow over professional skinning quality.

## Humanoid vs generic animation

**Try Humanoid Avatar** is *off* by default. If enabled, Riggle attempts `AvatarBuilder.BuildHumanAvatar` and checks `Avatar.isValid` / `Avatar.isHuman` before attaching the Avatar to the root Animator. A valid humanoid generally requires suitable proportions, correct laterality, bone hierarchy and a T-pose. Strongly stylised rigs may be rejected; **the generated skeleton and skin remain usable for direct bone animation** when that happens. A Unity-produced Humanoid Avatar is saved as a project asset if valid.

Riggle generates the deform skeleton. This project also integrates the imported **Ash Assets Control Rig** for hand/foot IK targets, elbow/knee hints, pelvis, spine and head aim. It now includes pose-preserving IK/FK switching, distributed limb twist helpers, generated character prefabs and a visible pose gallery. It also supplies a seam-aware weight brush and sculptable shoulder/hip corrective blend shapes. It does not supply authored animation clips, an FBX exporter or quadruped presets.

## Safety and editor integration

- Mesh assets from source FBXs and Unity primitives are *cloned*, never rewritten. The original renderer is switched off and its previous visibility state is saved. **Clear** restores it.
- Generated meshes and any successful Avatar are saved under **Asset Folder**. Rebuilding writes new uniquely named assets and **deliberately does not delete old assets**, because they may be used by prefabs or scenes. Clean up stale assets manually if no longer needed.
- Scene hierarchy changes use Unity Undo. Scene-view dots are drawn only when the component is selected. There are no `ExecuteAlways`, `ExecuteInEditMode` or `OnValidate` hooks.
- The Inspector is a normal Unity custom editor. It works without Odin, and can be extended with Odin buttons and inline inspectors if preferred.
- The tool is intended to be run only in Edit Mode on an editable scene object or a prefab opened in Prefab Mode; it refuses to run on a prefab asset itself.

## Reference idea

Auto-Rig Pro's human *Smart* landmark workflow and Quick Rig's editable/non-destructive philosophy informed the **staging** here: landmarks → editable reference bones → bind → optional game-ready Humanoid Avatar. Riggle is separate Unity-native code and does not copy or execute the uploaded Blender add-on.


## Riggle test scene: two working humanoids

Open `Assets/_EXPERIMENTAL/Riggle/riggle test.unity`. Both **Bean Biped** and **model_doggy_avatar_sketch** have fitted landmarks, saved Humanoid Avatars, skinned meshes and an imported **Control Rig** child. The camera frames both characters and their six-pose comparison rows.

Enter Play Mode and select transforms under each **Control Rig**: move **Left/Right Hand Target** or **Left/Right Leg Target**, adjust their **Hint** to steer the elbow/knee, move **Hip target**, rotate the spine targets, or move **Head LookAt Target**. The `Rig` weight blends the controls with the base pose. Unity Animation Rigging also supports preview through the Animation window. Stop Play Mode to return to the saved rest pose; runtime target edits are temporary.

`Tools → Control Rig Builder` builds/refits from a valid scene Humanoid Animator. It reuses its existing rig, keeps other layers, fits both target positions and rotations, and places hints/pivots using the character's orientation and proportions. Riggle rebakes reconnect existing controls; Clear removes the control layer tied to the deleted skeleton and restores source renderers. The imported runtime script no longer references UnityEditor.

The optional `RiggleHumanoidPreview` muscle exercise is **disabled** on both roots so it cannot overwrite IK. To inspect that exercise separately, disable Rig Builder, then enable the preview in Play Mode. It restores the pose when disabled.

## Contiguous surface weighting

**Weighting → Contiguous Surface** is enabled for both scene characters and is the default for new Riggle components. Envelope scores select surface seeds; multi-source Dijkstra propagation measures distance along welded triangle edges. Unreachable islands receive no influence from those seeds. Four-bone reduction and seam-aware smoothing follow. **Envelopes** remains available for comparison.

This is surface-geodesic propagation, not a voxel remesh or volumetric heat solver. It retains the source geometry and uses world-space edge lengths, including transformed/scaled sources. Landmark placement and explicit region constraints still matter: a bad initial seed can give a bad result, and this is not a universal automatic anatomy detector.

`RiggleSkinBinding` restricts a whole mesh or individual connected islands to an anatomical chain. The bean uses per-part bindings; the dog's 16 islands are assigned to head/ears, torso/backpack/collar, hands, arms, legs, feet and hips/tail. Island overrides refer to an original vertex index; refit them after changing the imported topology. No fingers or independent ear/tail bones are generated.

The regression checks cover source preservation, transformed rest geometry, extreme bends without seam cracks, anatomical masks, contiguous distances across a folded strip versus a nearby disconnected triangle, Humanoid muscle motion, safe control refits, and live hand/foot/hip/head controls.

## Auto-Rig Pro technology review

This is a review of the documented systems, not an audit of Auto-Rig Pro's implementation source or a claim of feature parity.

| System | Auto-Rig Pro | Riggle / this integration |
|---|---|---|
| Detection and skeleton authoring | Smart markers, optional AI guessing, finger detection; presets and modular limbs | Mirrored landmarks and bounds fitting; manually fitted biped templates; no AI, fingers, facial or quadruped generation |
| Skin binding | Heat Maps, approximate Voxelized surface binding, optional third-party Voxel Heat Diffuse; split-parts and selected-region options | Envelope or contiguous surface propagation, welded seams, anatomical island masks; no volumetric solver |
| Deformation refinement | Weight painting, corrective shapes, secondary/twist/bendy controls | Four influences per vertex, a seam-aware weight brush, twist helpers and sculptable pose-driven shoulder/hip correctives |
| Animation controls | IK/FK snapping/baking, poles, space switching, foot roll, pinning, spline IK, head/finger/facial controls | Imported two-bone IK, hints, hip, spine twist, head aim, fitted foot pivots, matched IK/FK switching and FK rotation controls; no space-switch or animation bake tools |
| Retargeting | Remap bone mappings/presets, animation transfer and adjustments | Valid Unity Humanoid mapping and tested muscle poses; no custom Remap equivalent |
| Existing skeletons | Quick Rig adds controllers while preserving existing weights, including nonstandard skeletons | Control Rig attaches to valid Unity Humanoids; Riggle binding itself still requires unskinned MeshFilters |
| Engine delivery | Humanoid/Universal FBX and GLTF, export validation, animation/root-motion options | Native Unity character prefabs, scene, mesh and Avatar assets; no interchange exporter or root-motion extraction |
| Workflow | Picker/collections, rig-data presets, animation helpers | Unity hierarchy/Inspector targets, Undo, non-destructive source meshes and retained previous bake assets |

Sources: [Auto-Rig/Smart/skinning](https://www.lucky3d.fr/auto-rig-pro/doc/auto_rig.html), [rig controls](https://www.lucky3d.fr/auto-rig-pro/doc/rig_behaviour_doc.html), [Remap](https://www.lucky3d.fr/auto-rig-pro/doc/remap_doc.html), [Quick Rig](https://www.lucky3d.fr/auto-rig-pro/doc/quick_rig_doc.html), [engine export](https://www.lucky3d.fr/auto-rig-pro/doc/ge_export_doc.html).

For these disconnected cartoon characters, contiguous propagation plus explicit island ownership addresses the observed defects without remeshing. Pose-preserving IK/FK switching, twist distribution, editable weight corrections and shoulder/hip corrective shapes are now implemented. A true volume solver would be a separate feature for difficult layered/connected characters. Auto-Rig Pro's documentation distinguishes its approximate Voxelized engine from the separate Voxel Heat Diffuse add-on; they should not be described as the same algorithm.

## Generated prefabs, switching and visible poses

Each bake saves a reusable `<character>_Skinned.prefab` in **Asset Folder**. **Save skinned character prefab** also updates it manually. The prefab contains the skin renderers, skeleton, Avatar, controls and twist component, with internal bone/control references. Authoring meshes, Riggle components, preview scripts and pose rows are excluded. Re-saving updates this stable prefab path; mesh and Avatar bake assets retain their unique filenames.

The test scene contains:

- `Assets/RiggleGenerated/Bean/Riggle Demo - Bean Biped_Skinned.prefab`
- `Assets/RiggleGenerated/Dog/model_doggy_avatar_sketch_Skinned.prefab`

In Play Mode, select either original character's **Riggle** component, or use **Tools → Control Rig Builder**. Each limb has **Switch to FK / Switch to IK**. Switching matches all three FK controls to the current bones, or matches the IK target and pole to the current FK pose. In FK, rotate the nested root/bend/tip controls under **Control Rig/FK controls**; the builder window has selection buttons. Hips/spine evaluate before the FK base and limb IK, so a rotated character root does not introduce a pose jump. The matched FK base stays beneath IK to preserve axial rotation. These controls own the limb pose: disable Rig Builder to let ordinary Animator clips fully drive the character. This is a pose switch, not a keyframe or animation-curve baking system.

**Distribute Twist** adds three weighted helpers along each upper/lower arm and leg on the next bake. `RiggleTwistDistribution` extracts relative axial rotation and spreads it in thirds; **Weight** blends its strength. Skin influence interpolates along the segment before reduction to four weights. Twist has no stretch; shoulder/hip corrective shapes are a separate system. Very large rotations still have ordinary linear-skinning limitations.

**Build / refresh visible pose row to the right** creates six labelled skinned snapshots: rest, reach, crouch, knee lift, wrist roll and matched FK. A valid Humanoid is required. **Show pose tests** hides/reveals the row; **Compare corrections** switches to four shoulder/hip OFF/ON snapshots at the same positions; **Spacing** on Riggle Pose Gallery changes separation when rebuilt. The row uses world-right placement and keeps the source orientation. It lives outside the avatar and is excluded from the exported prefab. Rebuild after rebaking the source to refresh the shared meshes. Snapshot Animators and control solvers are disabled so their poses stay visible in both Edit and Play Mode.

## Editable weights and shoulder/hip corrective shapes

Select the original character root and expand **Weight corrections & shoulder / hip shapes** in its Riggle Inspector. The two test characters already contain conservative starter shapes for both shoulders and hips. These are editable starting points, not anatomically finished corrective sculpts.

### Read-only bone influence viewer

Select the original character's **Riggle** component and enable **View bone influences (read only)** at the top of the Inspector. Search or browse bones with **Previous bone / Next bone**, or click a skeleton dot in the Scene view. The selected bone is yellow; vertex dots show its weight from blue (0%) to red (100%) across all generated meshes. **Show zero influence** hides or reveals unweighted regions, **See through mesh** exposes obscured dots, and **Frame bone** focuses the view.

The viewer shows exact affected-vertex/mesh counts and maximum weight; dense meshes use sampled display dots. It works with smooth skins, rigid parts and body chunks. Bone-parented accessories have no vertex weights. Authoring controls and transform handles are hidden while viewing; browsing does not change weights, poses, materials or saved assets. Turn the viewer off or press Escape in the Scene view to return to authoring.

### Weight corrections

1. Choose **Mesh to edit**, then **Paint weights** and a bone.
2. Set **Target weight**, **Brush strength** and world-space **Brush radius**. Drag over the mesh in Scene view; blue dots mean zero influence, red means full influence. Shift paints toward zero; Escape exits the tool.
3. The brush acts within its radius on the clicked connected island. Coincident seam vertices change together. The chosen bone keeps its requested influence while the three strongest other influences share the remainder. Removing the only influence falls back to a parent bone.
4. Ctrl/Cmd-Z undoes a stroke. **Clear weight edits on this mesh & rebake** returns that source to automatic weights. **Save edits to prefab & refresh pose row** saves the current scene, generated meshes and prefab and refreshes the snapshots.

The original Mesh asset stays untouched. `RiggleMeshEdits` on each source stores edited vertex weights by bone name, so they are reapplied after automatic weighting on a rebake. This preserves the painted values even if the generated bone indices change. A fingerprint checks vertex positions and triangle topology before applying edits; changing the source mesh or removing a painted bone refuses the bake rather than applying edits to unrelated vertices. Restore the matching source, or explicitly clear the edits before proceeding. There is no automatic transfer to a remeshed model, mirror brush, influence locking or general Blender-style weight editor yet.

### Corrective shapes

1. **Create shoulder & hip starter shapes** adds missing shapes without overwriting existing sculpts. It generates small radial displacements on weighted vertices near the joints.
2. Pick a source mesh and corrective shape. **Starts at bend**, **Full at bend** and **Correction strength** control its activation. The driver measures a signed bend around the authored axis and uses a smooth ramp; opposite-direction bends do not activate it.
3. Choose **Sculpt shape**, then **Preview full bend / sculpt at 100%**. Drag to push the surface outward; Shift pushes inward. Brush displacements are converted through the current skinning back into the saved mesh-local shape, keeping seam duplicates together. This is a push/pull brush, not a full sculpting package.
4. **Restore normal pose** exits the preview. Deselecting, saving the scene and entering Play Mode also restore the original pose. Preview deliberately shows the selected shape at 100% for editing; use the OFF/ON comparisons or Play Mode to judge its configured activation strength.
5. Save/refresh the prefab and rows. Use **Compare corrections** to switch between the six basic poses and shoulder/hip OFF/ON pairs. The comparison meshes use identical joint poses within each pair.

`RiggleCorrectives` evaluates after animation, IK and twist and drives only its own named blend shapes. Disabling it clears its weights. Existing unrelated blend-shape frames and values are preserved. Generated prefabs contain this runtime driver and shape data but omit authoring profiles. Corrective deltas survive rebakes with matching source topology; the source vertex positions and normals remain unchanged. Corrective frame normals are regenerated from displacement differences.

These four starters cover shoulder elevation and hip flexion. They do not automatically fix every bend direction, preserve anatomical volume, prevent collisions or create elbow/knee/face corrections. Broad shoulders, clothing, very large bends and changed landmark proportions still need visual judgement and tuning.

### Verification — 7 November 2026

All **19 Riggle Edit Mode** and **5 Riggle Play Mode** tests pass. The deformation tests cover normalized painting, seam preservation, Undo/Redo, bone-name replay, changed-topology rejection, posed/scaled sculpt conversion, preview restoration, unrelated blend-shape preservation and a real rebake/prefab export. Live tests confirm corrective mesh movement after FK bends and release at rest, alongside the existing IK/FK round trips. The full Edit Mode suite passed **185/185**.

The earlier broad Play Mode run was **153/155**, with two existing CoastalHike coaster-camera failures (`fixedThirdPerson: 0`). This corrective change was verified with the five scoped Riggle Play Mode tests; coaster settings were not changed.

Progress screenshots are saved in the repository’s `screenshots/riggle-2026-11-07/` folder and uploaded through Transmit: [final pose suite](https://docky.net/builds/riggle-04-final-pose-suite.png), [live controls](https://docky.net/builds/riggle-05-live-ik-fk-twist.png).

Latest comparison capture: [shoulder and hip correctives](https://docky.net/builds/riggle-06-corrective-comparisons.png).

## Guided character preparation and child reference points

Open **Tools → Riggle → Rig Helper** (also available from the Riggle Inspector).
The six steps are Model, References, Fit, Bake, Test and Save. Preparation accepts
static or already-skinned models, preserves an inactive original, and creates
independent normalized source meshes. Review the landmarks before baking: bounds
are only an initial guess, particularly with large heads, tails and A-poses.

Empty child transforms can drive the 13 landmarks. Scan recognizes names such as
`Shoulder.L`, `Elbow_L`, `Wrist-R`, `LeftKnee`, `rightfoot_ref`, `Pelvis`, `Neck`
and `HeadTop`. Use the crown for HeadTop; a bone named Head is deliberately not
assumed to be the crown. Duplicate matches are reported for manual assignment.
Generated bones, control-rig objects and the preserved import are excluded.

An unassigned side can be reflected across the pelvis in character-local X.
Explicit references on both sides override reflection. Toggle inference off for
asymmetrical fitting. Editing an inferred landmark moves its assigned opposite
reference; reference moves support Undo. The reference group may be nested,
rotated or scaled. Export excludes reference authoring components and the original
import, retaining the generated runtime skeleton, skins and controls.

## Imported character lab

`Riggle Imported Characters.unity` contains **Fox Girl** and **Base Mesh**, with
six visible pose tests to the right of each authoring character. Their reusable
prefabs are:

- `Assets/RiggleGenerated/Fox Girl/Fox Girl_Skinned.prefab`
- `Assets/RiggleGenerated/Base Mesh/Base Mesh_Skinned.prefab`

Both have valid Humanoid Avatars, contiguous surface weighting, twist distribution
and matched IK/FK controls. Fox Girl uses one-sided reference empties with inferred
opposites. The base mesh starts from the supplied model's A-pose and bone positions,
then receives a new Riggle skeleton and weights. Its original skin is preserved in
the inactive import, not used as the generated skin.

Fox hair and eyes follow the head; gloves and shoes are constrained to their sides;
the skirt is bound around the hips. These are body rigs: no separate fingers,
facial controls, tail dynamics or cloth collision simulation are generated. Deep
leg bends can intersect the skirt and need garment-specific work for production.

## Named rigid parts (robots, skeletons and retro characters)

In **Rig Helper → 4 Bake**, choose **Assembly mode → Named Rigid Parts**, then
**Scan named parts and accessories**. The same options are in the Riggle Inspector.
The original Smooth Skin mode remains the default.

Name child meshes after the generated bone: `Head`, `Chest`, `Hips`,
`LeftUpperArm`, `Riggle_LeftHand`, etc. Side suffixes also work: `UpperArm.L`,
`Forearm_R`, `Hand.R`, `Thigh.L`, `Shin_R`, `Foot.L`. `Shoulder` is the clavicle;
use `UpperArm` for the upper-arm segment. Names match whole bones, not substrings
(`Headlight` does not match Head). Unity duplicate suffixes `(1)` and `.001` are
accepted. A named empty group assigns its unnamed child meshes to that bone;
a child with another bone name overrides that inheritance.

The bake combines these meshes into one SkinnedMeshRenderer, with a material
submesh for each distinct material and exactly one bone influence per vertex.
UV channels, vertex colours, normals and tangents are retained. Transformed and
mirrored mesh children are supported. Parts remain rigid, without smooth joint
blending or twist-weight distribution. This mode uses the existing fitted
landmarks, Humanoid creation, controls and visible pose tests.

The **Attachment symbol** defaults to `^` and is configurable (empty disables it):

- `Head^ Hat` or `^Head`: parent the object and its entire hierarchy to Head.
- `Chest^ Backpack`: parent the complete backpack to Chest.
- `^ Visor` below a group named `Head`: inherit the group's bone.

Accessories are the actual GameObjects, not merged copies: components, children,
materials and identity are retained. Their world pose is preserved when attached.
A sheared hierarchy that cannot retain its world matrix is rejected; apply its
non-uniform parent scale first. Clear restores the original parent, sibling order
and local pose. Rebaking does not duplicate accessories. Exported prefabs include
the separate accessories beneath their bones.

Unmatched names and hidden merge parts are reported before replacing the existing
rig. Ordinary merged parts contribute mesh data only; use an attachment for an
object whose scripts or colliders must move with its bone. Saved weight edits and
blend shapes require Smooth Skin mode. Source geometry and materials are not
modified by the merge.

Example: `Riggle Rigid Parts.unity` has a retro robot with 16 merged parts, a
`Head^ Hat`, a `Chest^ Backpack`, and six posed copies. Its runtime prefab is
`Assets/RiggleGenerated/Retro Robot/Retro Robot_Skinned.prefab`.

### Body chunks with strict bone scopes

Choose **Rig Helper → Bake → Named Body Parts** for a character assembled from
separate chunks which bend within their own anatomical chains. Unlike a rigid
single-bone part, an arm chunk can bend at the elbow. The weight solver receives
only that chunk's allowed bones, so proximity, surface smoothing and twist
redistribution cannot introduce torso, opposite-side or other unrelated weights.

| Child/group name | Allowed primary bones |
| --- | --- |
| `Arm.L`, `ArmWithoutHand.L` | LeftUpperArm, LeftLowerArm |
| `ArmWithHand.L` | Above, plus LeftHand |
| `Leg_R`, `LegWithoutFoot_R` | RightUpperLeg, RightLowerLeg |
| `LegWithFoot_R` | Above, plus RightFoot |
| `Torso`, `TorsoWithoutNeck` | Hips, Spine, Chest, UpperChest |
| `TorsoWithNeck` | Above, plus Neck |
| `Head`, `HeadWithoutNeck` | Head |
| `HeadWithNeck` | Head, Neck |

Both sides accept `.L`/`.R`, `_L`/`_R`, and `Left`/`Right` prefixes/suffixes.
Arm/leg scopes require a side. A torso never considers clavicle/shoulder or upper
arm bones. Optional twist bones are included only for the allowed limb segments.
Existing exact bone names (`UpperArm.L`, `Hand.R`, etc.) remain rigid assignments.
Named groups apply to unnamed mesh descendants; a nearer explicit name or body
part component starts a new scope. No geometry is mirrored or invented.

For arbitrary object names, select a source mesh/group and use **Add body-part
settings to selected mesh / group** in the wizard (or add **Riggle Body Part**).
Choose Arm/Leg/Torso/Head, side, and inclusion of hand/foot or neck. Component
settings override the object's name. The scan lists the resolved bone scopes.
These settings govern Named Body Parts; Smooth Skin's existing Skin Binding and
island overrides retain their existing behavior.

By default the scoped parts merge while preserving materials and per-part
weights. Enable **Separate Limb Meshes** to retain each named arm/leg chunk as a
separate `SkinnedMeshRenderer`. **Keep Separate Renderer** on a body-part override
separates an individual chunk, including a head or torso. Unnamed descendants of
that chunk share its renderer. Each separate renderer uses a compact bone list
and shares the same character skeleton, control rig, and exported prefab. Its
mesh/bone array/bind poses form the unit for a future limb replacement; merely
swapping in an unrelated mesh without matching those bindings is not supported.
Rubber-hose geometry, extra hose bones and runtime limb-swapping are deferred.

The existing `^` accessory convention also works. Accessories on a multi-bone
chunk need an explicit bone, e.g. `Hand.L^ Glove` or `Head^ Hat`. Mesh authoring
edits/blendshapes are rejected before baking in both named modes; use a Smooth
Skin version for that workflow. Normal rebaking and Clear restore source
visibility and accessory parents. Ordinary merged/separate skinned chunks carry
mesh data; put objects with gameplay scripts/colliders under a marked accessory
when those objects must follow a bone.

### Use a source part's origin as a landmark

In **Rig Helper → References**, choose **Part origin represents**, select a source
mesh/group in the Hierarchy, and click **Use selected part origin for this
landmark**. This adds a `RiggleOriginReference` and assigns it immediately. You
can also add that component manually and use **Scan child names** within the
configured search root. Explicit assignments take precedence over inferred sides.

A zero **Local pivot offset** uses the object's origin. The offset is in that
object's local coordinates, so its scale and rotation are respected. Moving the
source part updates the reference. Dragging a Riggle landmark or editing its
position adjusts the offset, not the mesh transform; this is undoable. Existing
reference empties continue to work and missing-side inference reflects the
resolved point across the pelvis in character-local space.

The origin must represent the selected joint: use an arm's shoulder pivot for
Shoulder, a forearm's elbow pivot for Elbow, and so on. A centre pivot needs an
offset. A whole limb's one origin does not also locate its elbow/knee and end;
keep fitting or supplying those landmarks. The available targets are Riggle's
existing landmark set; hips and the internal head-joint proportion continue to
use the existing skeleton proportions. Generated bones, imported originals,
controls and marked moving accessories cannot act as source-origin references.
Origin references are authoring data and are removed from the generated prefab.

### Optional bunny ears, long tail and back wings

In **Rig Helper → Fit**, click **Add optional ears, tail and wings**, then enable
any combination of **Two bendy bunny ears**, **Long tail**, and **Two back wings**.
All are off by default. Ear chains attach to Head, the tail to Hips, and wing
chains to UpperChest. These are generic extra bones; the existing Humanoid bone
mapping and body control rig are retained.

Each path has 2–16 bend segments (default four per ear/wing, eight for the tail),
a weighting radius, and an editable root-to-tip guide list. Default guide paths
are fitted to the existing character landmarks. The guide handles are shown when
selecting the Riggle component. Guides can use character-local coordinates or an
explicit source-object/empty reference with a local pivot offset. Generated bones,
controls and moving accessories cannot drive guide positions. Right ears/wings
mirror the left across the character-local pelvis plane by default; turn off that
feature to edit independent right-side guides. Bone samples follow the guide
polyline at equal arc-length intervals. Re-bake after changing guides/counts.

In **Named Body Parts**, use `Ear.L`, `Ear.R`, `Tail`, `Wing.L`, `Wing.R`, or the
Ear/Tail/Wing kinds on **Riggle Body Part**. Each mesh considers only its own chain.
The **Separate Limb Meshes** option also separates these appendage chunks; the
per-chunk **Keep Separate Renderer** option remains available. In **Smooth Skin**,
use LeftEar/RightEar/Tail/LeftWing/RightWing on **Riggle Skin Binding** for whole
meshes or disconnected islands. Unrestricted Automatic skinning includes all
enabled bones and may need these assignments or weight editing near attachments.

Individual bones are `Riggle_LeftEar01…04`, `Riggle_Tail01…08`, and
`Riggle_LeftWing01…04` (right-side equivalents also exist; counts are configurable).
Rigid parts/accessories accept e.g. `Ear02.L`, `Tail03`, `Wing02.R^ Ornament`.
Disabled chains or segment numbers beyond the configured count are rejected.
The source mesh must have enough vertices along its length to show smooth bending;
baking adds bones/weights, not subdivision or new ear/wing geometry to your model.

Generated **Riggle Appendage Rig** provides hierarchical FK controls under each
attachment bone. Rotate `FK_LeftEar01`, etc. in Play Mode or animate those control
transforms. The driver copies local rotations after animation; disable it to use
animation clips on the appendage bones directly. These are manual/animated FK
chains, not a spring-physics simulation or flight controller. The **Test** step
includes rest, curl/spread and sweep/fold previews, plus control-selection buttons.
The pose row adds two labelled appendage poses when any extra chain is enabled.
Restore the rest pose before rebaking. The exported prefab retains bones, weights
and FK controls, and excludes the authoring guide component.

### Anatomical bends and the Riggle control variant

Riggle uses `Resources/Riggle Control Rig`, a prefab variant of the shared Control Rig. It removes unbound effectors while keeping hand/foot targets, poles, body controls and IK/FK switching. Animation Rigging component icons are hidden through the Scene Gizmos settings without hiding the actual controls. This Editor setting also hides those icons on non-Riggle rigs.

The generated `ControlRigAnatomy` component estimates facing from toe-versus-heel reach around both ankles. Nearly symmetric feet fall back to +Z. Choose Positive Z or Negative Z explicitly for unusual feet, then Build / Refit Rig. Knee and elbow poles sit outside the limb reach to avoid crossing the target line during ordinary lifts.

Humanoid reference poses exclude scene position/yaw. The muscle preview samples in character space, so rotating an example in the scene does not reverse its limb bends. This is separate from the visible pose gallery, whose elbow flexion now points the hands forwards.

### Kawaii retargeting and root motion

Open `Riggle Kawaii Retargeting.unity` for seven character rows and eight shared Kawaii clips: Shy, Triple Pose, Dance 03, Cheers, Stretch 2, Walk, Run, and Run Turn Left. Saved poses give an immediate comparison; Play animates each cell and repeats from its marked root origin. Root motion comes from the clips, including turning and vertical body movement, scaled through each Humanoid Avatar.

`ControlRigAnimation` chooses pose ownership. Automatic releases the manual body rig when an Animator Controller is assigned. Animation also supports external PlayableGraphs. Controls re-enables IK/FK editing. The Root Motion toggle controls clip-driven movement; the rig no longer pins hips and feet over a playing animation. `RiggleRetargetSample` supplies manual-step playback for the comparison scene and tests. `Kawaii Walk.controller` is included for normal Animator Controller testing.

Offline capture must refresh skinning as well as evaluate the animation graph. Multiple manual `Step` / `Camera.Render` calls without a player frame can render a cached skinned pose while the root keeps moving. The showcase recorder bakes each current `SkinnedMeshRenderer` pose into temporary render meshes per captured frame, then removes them and restores the saved scene. This is capture-only; live playback retains normal skinned renderers. The retarget tests also check arm/leg rotation and a changing rendered silhouette with root motion disabled across real player frames.

The reusable recorder is `Tools/RiggleRetargetVideo.cs` (outside Assets). With the saved comparison scene open in Edit Mode, run `unity command script run --file Tools/RiggleRetargetVideo.cs --entry RiggleRetargetVideo.Main` from the Unity project directory. It writes the 90-second MP4, frame checks and progress status under `Builds/Riggle Showcase`, and restores the saved scene after capture.
