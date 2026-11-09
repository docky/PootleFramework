# Riggle v0.2 — Unity landmark-to-skeleton tool

**Version 0.2 — experimental; not yet validated through use in real projects.**

`Core/` contains the rig authoring/runtime code, editor tools and required control-rig resource. `Demo/` contains example scenes, character imports, generated example meshes/prefabs, scene builders and tests. Core has no dependency on Demo; keep its Control Rig and Animation Rigging dependencies when copying it to another project. The reusable pose gallery and muscle preview remain in Core because the rig helper and exporter use them.

New user characters still default to `Assets/RiggleGenerated`; the supplied examples are stored in `Demo/Generated`.

## Riggle Generic — quadrupeds and custom skeletons

**Riggle Generic is a separate authoring component inside Riggle, sharing its skinning engine.** The original Riggle component remains the humanoid workflow. Generic anatomy is a named joint hierarchy and a list of leg chains, so it is not constrained by the biped landmark enum, Mecanim Humanoid mapping, or humanoid Control Rig. This is an experimental first version for unskinned meshes; it does not retrofit an existing imported skinned skeleton.

1. Select a character root and use **Tools → Riggle → Add Generic Rig to Selected** (or Add Component → Riggle → Riggle Generic). Use a separate root from humanoid Riggle. Orient the mesh +Y up and +Z forward.
2. **Reset to quadruped preset fitted to mesh bounds** creates a starting layout: hips, spine, chest, neck/head, two tail bones and four legs. This replaces the current definition. Bounds are only an initial estimate; place joints at the actual articulations.
3. Click joint dots in Scene view and move the handles. Terminal bones also expose a tip handle. **Mirror Left joints to Right** reflects positions and tips across Hips X. Edit the Joints list for other anatomy, tails, or additional leg pairs. Parents refer to unique joint names; End Joint selects a direct child as the envelope endpoint, otherwise Tip is used. Radius is in character-local units. Renaming a joint requires updating its parent/end/leg references.
4. **Bake Generic • Bones + Skinning** creates an ordinary transform skeleton and cloned, persistent skinned mesh assets. Both envelope and contiguous-surface weighting use Riggle's existing solver. Original mesh assets are preserved and source renderers hidden. There is no generated Humanoid Avatar, Humanoid retargeting, IK controller, or runtime pose writer. Automatic weights are a starting point; generic bones do not use humanoid Skin Binding masks, named body-part assembly, or the humanoid deformation helper UI.
5. **Set up / reconnect Legs Animator** optionally adds the installed FImpossible Legs Animator and explicitly assigns Hips, Base Transform, leg upper/middle/foot bones, side and opposite-leg pairing. Core has no compile-time dependency on Legs Animator and includes none of its vendor code. The adapter requires positive uniform character scale, a slight knee bend, and direct ordered chains beneath Hips. Pairing must be reciprocal. Generic rigs may have any number of legs. Two-segment legs use the vendor solver; longer chains use the optional Riggle Multi Segment Legs Animator adapter and Riggle chain IK.
6. Configure ground layers, feet, movement input, gait and modules in **Legs Animator**. The rigger supplies anatomy and skinning; it does not create locomotion clips or a character controller. Existing manually configured Legs Animator components are left alone; assign their bones manually or remove that component before using automatic setup.
7. **Save new prefab** saves the authored character with its generated bones, skins and optional Legs Animator. **Clear generated rig** removes its generated hierarchy and owned Legs Animator and restores source renderer visibility. Generated asset files remain on disk so exported prefabs remain valid. Rebakes create new mesh assets and reconnect the owned Legs Animator; component settings and per-leg settings are retained by list index, so review them after reordering legs. Bones are rebuilt, so other external bone references must be reconnected manually.

Open `Demo/Scenes/Riggle Generic Quadruped.unity` for the saved example with a ground collider, camera and configured Legs Animator. Its reusable prefab is `Demo/Generated/Generic/Riggle Demo Quadruped.prefab`. For a fresh example, use **GameObject → 3D Object → Riggle Demo Quadruped**. It creates and bakes a capsule-based quadruped and configures Legs Animator when installed. The demo has no floor or movement controller; place it over your scene's ground collider. Its output goes to `Demo/Generated/Generic`.

The editor tests cover four/six-legged anatomy, both weighting methods, transformed bind geometry, deformation, invalid-input protection, Undo/Clear, prefab references and preservation of Legs Animator settings across rebakes.

### Legs with more segments

**Leg Segments** now supports 2–8 segments (3–9 joints) per leg. **Rebuild leg segments** resamples the current guide path while keeping upper and foot endpoints; fit the new handles to the anatomy, then **Bake Generic**. Leg definitions retain Upper, Middle and Foot for compatibility and add an ordered **Additional Joints** list between Middle and Foot. Every parent link must be included. Rebuilding changes extra bone names and references; update any retargeting profiles or external references afterwards. Accessory branches on removed joints must be moved first.

The optional `RiggleMultiSegmentLegsAnimator` subclass uses Legs Animator's custom IK hooks: its ground/glue algorithms still supply foot targets, while `RiggleChainIK` solves the complete chain without stretching its segments. Automatic setup chooses this adapter for longer chains and preserves an owned component's settings when upgrading it. The iterative solver starts from the animated pose. Quadruped conversion and the multi-segment adapter limit the last inter-segment bend to 55 degrees and hoof swing relative to its calibrated terminal frame to 35 degrees. Alternate bend seeds recover from folded poses without stretching. The converter resolves the sole offset after limiting hoof rotation. These terminal limits prevent the short final segment folding back; upper joints do not yet have anatomical hinge limits. A shaped reference pose and sensible target reach are still important.

### Convert Generic quadruped animation clips

Create **Assets → Create → Riggle → Quadruped Animation Profile**. Assign the source model, a baked Riggle Generic target prefab, source motion-root/hips paths, body mappings, four leg mappings and the source clips. Source paths are relative to the model root; target names are Riggle joint names. The optional neutral reference clip is important when animation axes differ from the imported bind pose. Source To Target Euler aligns character facing/up axes.

The converter transfers neutral-relative body rotations, proportion-scaled hips and hoof trajectories, and solves target legs using their own segment lengths. For longer legs, the optional Source Shape Chain maps intermediate source joints to the target chain and transfers hock/ankle shape. It must have the same joint count as the target; leave it empty to use trajectory-only adaptation. Source and target sole offsets identify each hoof's contact reference. Flat-ground clips can use **Clamp Feet To Reference Floor** and a limited **Max Body Lowering** correction. These are authored ground-plane corrections, not terrain raycasts.

Select the profile and run **Tools → Riggle → Convert Selected Quadruped Animation Profile**. Output is ordinary Unity transform `.anim` clips, sampled at the chosen rate, with quaternion continuity. Bind them to an Animator on that target hierarchy. **Include Root Motion** retains horizontal motion and turning on `RiggleGeneric_Bones`; in-place output retains vertical motion. This carrier motion is an animated child transform, not an automatically extracted `Animator.deltaPosition`. Repeated conversions create new assets, preserving earlier clips. Animation events, facial blendshapes and additive-layer semantics are not copied; convert full-body Generic clips first.

The supplied profile is `Demo/Generated/Horse Retarget/LP Horse to Pink Pony.asset`. It converts LP Horse idle, collected walk, extended walk, trot, canter and gallop onto a new four-segment pink pony. `Pink Pony Animated.prefab` has an Animator Controller with all six converted clips. Its multi-segment Legs Animator component starts disabled so animation can be reviewed directly; enable and tune it when adding terrain correction. The original two-segment walking prefabs remain available.

Open `Demo/Scenes/Riggle Pink Pony Horse Animations.unity` for the synchronized source/target comparison. Rebuild with **Tools → Riggle → Build Pink Pony Horse Animation Showcase**. In Edit Mode, call `Docky.RiggleTools.Demo.RiggleQuadrupedRetargetRecording.Arm()` then Play to record 48 seconds at 1080p/30fps under `Builds/Horse Retarget`; it pauses at completion. The video compares baked animation with runtime ground IK disabled. [Watch the uploaded comparison](https://docky.net/builds/riggle-pink-pony-horse-retarget-hoof-fix-2026-11-08.mp4). The converter preserves source asset data and uses isolated preview scenes. Tests cover reference calibration, rotated imported motion roots, deterministic sampling, saved-clip equivalence, optional root travel, chain reach/lengths and existing two-segment behavior.

The blue pony comparison is `Demo/Scenes/Riggle Blue Pony Horse Animations.unity`, rebuilt with **Tools → Riggle → Build Blue Pony Horse Animation Showcase**. Its fitted four-segment rig, mapping profile, animated prefab and six clips are in `Demo/Generated/Horse Retarget/Blue Pony`. Use the same recorder `Arm()` call with this scene open; its recording goes to `Builds/Horse Retarget/Blue Pony`. Both pony profiles run through the same seven retarget tests, including 120 Hz terminal-angle and contact checks across all six gaits. [Watch the blue pony comparison](https://docky.net/builds/riggle-blue-pony-horse-retarget-hoof-fix-2026-11-08.mp4).

The combined terrain trial is `Demo/Scenes/Riggle Pink Pony Retarget Uneven Walk.unity`, rebuilt with **Tools → Riggle → Build Pink Pony Retarget Terrain Trial**. It plays the converted collected walk at a speed matched to 0.3 m/s travel, uses Legs Animator Moving glue mode, and solves all four segments. `RiggleAnimatedHoofGrounding` adjusts sole/reach targets without scheduling steps. Its reusable prefab is `Demo/Generated/Horse Retarget/Pink Pony Retarget Terrain.prefab`. The recorder uses `.Arm("Pink Pony Retarget")`; validate its measured report with `python3 Pootle_Unity/Tools/QuadrupedTrial/verify_retarget_terrain.py`. This authored walk can have two supporting feet, so its validation differs from the earlier three-support procedural walk. Physical support counts actual sole positions within 2 cm of the collider; `minimumSupports` separately retains the vendor glue-state count, which can fall to one while two soles touch the floor. Step counts follow animated detach cycles. Terminal-bend validation uses post-IK bone positions. [Watch the terrain recording](https://docky.net/builds/riggle-pink-pony-retarget-hoof-fix-uneven-walk-2026-11-08.mp4).

### Horse and fawn uneven-floor walking trials

Open `Demo/Scenes/Riggle Pink Horse Uneven Walk.unity` or `Riggle Toy Fawn Uneven Walk.unity` and press Play. Each textured animal walks 12.3 metres over rolling ground, a ramp, a cross-slope and shallow descending steps. The saved walking prefabs and baked meshes are in `Demo/Generated/Quadruped Trials`. **Tools → Riggle → Build Horse and Fawn Walking Trials** rebuilds both scenes from their fitted joint definitions; it requires Legs Animator to be installed.

The supplied GLBs had no skeleton or animation. Their converted, unskinned FBXs and extracted textures are in `Demo/Characters/Quadruped Trials`; Riggle generates the skinned skeletons in the Unity prefabs. Conversion normalizes height to 1.8 metres, grounds the feet and aligns forward. `Tools/QuadrupedTrial/convert_models.py` reproduces conversion through Blender. `Builds/Quadruped Trials/horse-and-fawn-fbx.zip` packages both FBXs with their textures.

`Integrations/LegsAnimatorDemo` is an optional integration with a direct dependency on the installed FImpossible package. Keep it out of projects without Legs Animator. The demo drives forward travel and schedules a four-beat walk, with one swinging leg at a time. Legs Animator supplies foot arcs, glue, ground alignment and IK. The custom cadence module also corrects the final hoof sole against the collider and lowers the hips when necessary to keep targets reachable. These recordings therefore demonstrate Legs Animator with that module, rather than its default configuration. Core's optional reflection adapter remains independent of the vendor assembly.

Expected results: all four legs step, planted sole references remain on the ground, at least three legs support the body, feet adopt different heights on uneven terrain, and chains stay within their reach. The 2026-11-08 recordings completed 45 seconds each with 30 steps per leg, zero missed ground rays, zero invalid bone samples and a maximum reach ratio of 0.975. Maximum swing clearance was 10.95 cm for the horse and 10.17 cm for the fawn. Both passed all 12 checks in `Tools/QuadrupedTrial/verify_recordings.py`. Contact measurements use actual post-IK hoof reference points; they do not certify every skinned surface vertex against the floor.

To record again, open the corresponding saved scene in Edit Mode, call `Docky.RiggleTools.Demo.RiggleQuadrupedRecording.Arm("Pink Horse")` or `.Arm("Toy Fawn")`, then enter Play Mode. The editor-only recorder captures 45 seconds at 1920×1080 / 30 fps, writes MP4, reference frames and measured JSON reports under `Builds/Quadruped Trials/Videos`, and pauses when complete. Stop Play Mode afterwards. From the repository root, run `python3 Pootle_Unity/Tools/QuadrupedTrial/verify_recordings.py` to check both reports. The original scene is not automatically restored by the recorder.

Uploaded through Transmit: [horse recording](https://docky.net/builds/riggle-pink-horse-uneven-walk-2026-11-08.mp4), [fawn recording](https://docky.net/builds/riggle-toy-fawn-uneven-walk-2026-11-08.mp4).

The blue pony uses the same course in `Demo/Scenes/Riggle Blue Pony Uneven Walk.unity`, with `Demo/Generated/Quadruped Trials/Blue Pony Walking.prefab`. Rebuild it separately with **Tools → Riggle → Build Blue Pony Walking Trial**. Its fitted short legs, three-bone tail and body-height settings are specific to this model. The source FBX and textures are in `Demo/Characters/Quadruped Trials/Blue Pony`, also packaged in `Builds/Quadruped Trials/blue-pony-fbx.zip`. To convert this input again, run Blender with `--python Tools/QuadrupedTrial/convert_models.py -- --source /path/to/Cute_Blue_Toy_Pony.glb --label "Blue Pony"` from the Unity project directory. Record with `.Arm("Blue Pony")`; verify with `python3 Pootle_Unity/Tools/QuadrupedTrial/verify_recordings.py pink-horse toy-fawn blue-pony` from the repository root. [Blue pony recording](https://docky.net/builds/riggle-blue-pony-uneven-walk-2026-11-08.mp4).

For model delivery and automation, see the [model handoff sheet](MODEL_HANDOFF.md) and [model structure and one-button specification](MODEL_SPEC.md). The specification distinguishes current behavior from the proposed one-button workflow.

**Purpose:** a quick, scene-authored biped skeleton and automatic skin bind for low-poly, cartoon, soft-vinyl and other strongly stylised characters. **No facial rig and no fingers.** Independent C# implementation: no Auto-Rig Pro scripts or other Blender add-on code are included.

**Designed for:** Unity 2022.3+ / Unity 6, including URP. This project integration requires the included Ash Assets Control Rig and Unity Animation Rigging package. The project integration is compiled and exercised in Unity 6000.6.3f1. See the test-scene workflow below.

## Install

Keep the Riggle and Ash Assets Control Rig folders together, with Unity Animation Rigging installed. Unity will pick up the components and editor scripts. Source and output are both Unity-native; there is no Blender dependency.

## Five-minute test

1. In Unity, select **GameObject → 3D Object → Riggle Demo Biped**. The menu spawns a simple cartoon bean character made from built-in primitives, complete with markers.
2. Select its root, which has the **Riggle** component.
3. Turn on **Gizmos** in the Scene view if necessary. Click one of the coloured dots to select it, then drag its position handle. The yellow dot is selected; pink dots are the mirrored side. Alternatively select a dot in the Inspector and use **Place dot by clicking Scene** (click positions a dot on a constant-depth plane, so colliders are unnecessary).
4. Click **BAKE RIGGLE • Bones + Skinning**. The tool generates `Riggle_Bones` and one `Riggle_Skin_*` child per source mesh, and writes cloned skinned mesh assets under `Assets/_EXPERIMENTAL/Riggle/Demo/Generated/`.
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

Open `Assets/_EXPERIMENTAL/Riggle/Demo/Scenes/riggle test.unity`. Both **Bean Biped** and **model_doggy_avatar_sketch** have fitted landmarks, saved Humanoid Avatars, skinned meshes and an imported **Control Rig** child. The camera frames both characters and their six-pose comparison rows.

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
| Detection and skeleton authoring | Smart markers, optional AI guessing, finger detection; presets and modular limbs | Mirrored landmarks and bounds fitting; manually fitted biped templates; separate Generic quadruped preset; no AI, fingers or facial generation |
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

- `Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Bean/Riggle Demo - Bean Biped_Skinned.prefab`
- `Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Dog/model_doggy_avatar_sketch_Skinned.prefab`

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

- `Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Fox Girl/Fox Girl_Skinned.prefab`
- `Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Base Mesh/Base Mesh_Skinned.prefab`

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
`Assets/_EXPERIMENTAL/Riggle/Demo/Generated/Retro Robot/Retro Robot_Skinned.prefab`.

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

Humanoid reference poses calibrate the arms to a temporary T-pose, including models authored in an A-pose. The original skeleton pose, mesh bind poses and landmarks are restored after Avatar creation. This prevents retargeted forearm flexion from folding backwards or across the torso on lowered-arm source models. Gallery elbow bends use each forearm’s anatomical bend axis rather than a fixed local Y rotation.

Humanoid reference poses exclude scene position/yaw. The muscle preview samples in character space, so rotating an example in the scene does not reverse its limb bends. This is separate from the visible pose gallery, whose elbow flexion now points the hands forwards.

### Kawaii retargeting and root motion

Open `Riggle Kawaii Retargeting.unity` for seven character rows and eight shared Kawaii clips: Shy, Triple Pose, Dance 03, Cheers, Stretch 2, Walk, Run, and Run Turn Left. Saved poses give an immediate comparison; Play animates each cell and repeats from its marked root origin. Root motion comes from the clips, including turning and vertical body movement, scaled through each Humanoid Avatar.

`ControlRigAnimation` chooses pose ownership. Automatic releases the manual body rig when an Animator Controller is assigned. Animation also supports external PlayableGraphs. Controls re-enables IK/FK editing. The Root Motion toggle controls clip-driven movement; the rig no longer pins hips and feet over a playing animation. `RiggleRetargetSample` supplies manual-step playback for the comparison scene and tests. `Kawaii Walk.controller` is included for normal Animator Controller testing.

Offline capture must refresh skinning as well as evaluate the animation graph. Multiple manual `Step` / `Camera.Render` calls without a player frame can render a cached skinned pose while the root keeps moving. The showcase recorder bakes each current `SkinnedMeshRenderer` pose into temporary render meshes per captured frame, then removes them and restores the saved scene. This is capture-only; live playback retains normal skinned renderers. The retarget tests also check arm/leg rotation and a changing rendered silhouette with root motion disabled across real player frames.

The reusable recorder is `Tools/RiggleRetargetVideo.cs` (outside Assets). With the saved comparison scene open in Edit Mode, run `unity command script run --file Tools/RiggleRetargetVideo.cs --entry RiggleRetargetVideo.Main` from the Unity project directory. It writes the 90-second MP4, frame checks and progress status under `Builds/Riggle Showcase`, and restores the saved scene after capture.

## Ponytails

In Rig Helper → Fit, add optional appendages and enable **Middle ponytail**, **Left ponytail**, and/or **Right ponytail**. All start disabled; all three can coexist. Each chain attaches to `Riggle_Head`. Choose **2–7 points including tip** (1–6 deforming bones and a terminal marker), weighting radius, source-origin/empty guides, and **Y direction (degrees)**. Defaults are straight and horizontal in character space: middle points back (180°), left outward (-90°), right outward (90°). Y direction rotates the guide path around its first point; reposition guides to fit the model, then rebake. A path accepts 2–7 authored guides.

Named Body Parts use `Ponytail` or `Ponytail.M` / `Ponytail.L` / `Ponytail.R`; the Body Part component also has Ponytail and Middle/Left/Right choices. Smooth Skin supports corresponding whole-mesh/island Skin Binding parts. Numbered rigid targets include `MiddlePonytail01`, `Ponytail02.L`, and `RightPonytail06`. Generated names are `Riggle_MiddlePonytail01`…`06`, likewise Left/Right, with a `*PonytailEnd` tip. Hair stays scoped to its selected chain.

Ponytail chains default to **External Motion** on Riggle Appendage Rig, so its LateUpdate and pose preview leave those bones alone. Configure your chosen dynamic bone system with the corresponding `Riggle_*Ponytail01` root and terminal child; the bones follow the head through parenting. Riggle generates and skins the chains; it does not simulate hair physics. Clear External Motion only when you want the generated FK controls to own that chain instead. Existing ear/tail/wing FK behavior is retained.

### Dynamic animal ears

Enable **Two animal ears** in Rig Helper → Fit and **Ear external motion (dynamic bones)** for external simulation. Fit the ear paths and use **Orientation (X / Y / Z)** for upright, sideways, back-facing or floppy-ear rest directions. Mirrored ears reflect both guide positions and orientation; disable ear mirroring to fit each independently. Ears keep their existing 2–16 bend-segment setting. They attach to Head and use `Riggle_LeftEar01` / `Riggle_RightEar01` as dynamic-system roots. Existing FK ear setups remain compatible; enable external motion explicitly for a dynamic setup.

### Hidden alternate attachments

In either named-part assembly mode, a doubled attachment symbol means attach separately and start inactive: `Head^^ Alternate hat`, `^^Head`, or a `^^ Alternate hat` under a Head-named parent. A single `^` retains its usual visibility. Custom symbols double in the same way (`Head@@ Alternate hat` for `@`). Alternatives retain their meshes/components and can be enabled with `SetActive(true)`. Rebaking reapplies the hidden default; Clear restores the source's original parent, local pose and active state. Exported prefabs retain the hidden alternatives, so they can be switched at runtime.
