# Riggle v0.1 — Unity landmark-to-skeleton tool

**Purpose:** a quick, scene-authored biped skeleton and automatic skin bind for low-poly, cartoon, soft-vinyl and other strongly stylised characters. **No facial rig and no fingers.** Independent C# implementation: no Auto-Rig Pro scripts or other Blender add-on code are included.

**Designed for:** Unity 2022.3+ / Unity 6, including URP; no additional packages are required. The project integration is compiled and exercised in Unity 6000.6.3f1. See the test-scene workflow below.

## Install

Copy `Assets/Riggle/` into your project's `Assets/`. Unity will pick up the component and its editor scripts. Source and output are both Unity-native; there is no Blender dependency.

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
- The standard biped has **20 deforming bone transforms**, plus five non-deforming tip helpers.
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

Riggle generates the deform skeleton. This project also integrates the imported **Ash Assets Control Rig** for hand/foot IK targets, elbow/knee hints, pelvis, spine and head aim. It does not supply authored animation clips, automatic IK/FK snapping, limb twist bones, a weight-paint editor, an FBX exporter or quadruped presets.

## Safety and editor integration

- Mesh assets from source FBXs and Unity primitives are *cloned*, never rewritten. The original renderer is switched off and its previous visibility state is saved. **Clear** restores it.
- Generated meshes and any successful Avatar are saved under **Asset Folder**. Rebuilding writes new uniquely named assets and **deliberately does not delete old assets**, because they may be used by prefabs or scenes. Clean up stale assets manually if no longer needed.
- Scene hierarchy changes use Unity Undo. Scene-view dots are drawn only when the component is selected. There are no `ExecuteAlways`, `ExecuteInEditMode` or `OnValidate` hooks.
- The Inspector is a normal Unity custom editor. It works without Odin, and can be extended with Odin buttons and inline inspectors if preferred.
- The tool is intended to be run only in Edit Mode on an editable scene object or a prefab opened in Prefab Mode; it refuses to run on a prefab asset itself.

## Reference idea

Auto-Rig Pro's human *Smart* landmark workflow and Quick Rig's editable/non-destructive philosophy informed the **staging** here: landmarks → editable reference bones → bind → optional game-ready Humanoid Avatar. Riggle is separate Unity-native code and does not copy or execute the uploaded Blender add-on.


## Riggle test scene: two working humanoids

Open `Assets/_EXPERIMENTAL/Riggle/riggle test.unity`. Both **Bean Biped** and **model_doggy_avatar_sketch** have fitted landmarks, saved Humanoid Avatars, skinned meshes and an imported **Control Rig** child. The camera frames both characters.

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
| Deformation refinement | Weight painting, corrective shapes, secondary/twist/bendy controls | Four influences per vertex and local smoothing; no generated corrective shapes or limb twist distribution |
| Animation controls | IK/FK snapping/baking, poles, space switching, foot roll, pinning, spline IK, head/finger/facial controls | Imported two-bone IK, hints, hip, spine twist, head aim and fitted foot pivot transforms; no automatic IK/FK or space-switch bake tools |
| Retargeting | Remap bone mappings/presets, animation transfer and adjustments | Valid Unity Humanoid mapping and tested muscle poses; no custom Remap equivalent |
| Existing skeletons | Quick Rig adds controllers while preserving existing weights, including nonstandard skeletons | Control Rig attaches to valid Unity Humanoids; Riggle binding itself still requires unskinned MeshFilters |
| Engine delivery | Humanoid/Universal FBX and GLTF, export validation, animation/root-motion options | Native Unity scene, mesh and Avatar assets; no new interchange exporter or root-motion extraction |
| Workflow | Picker/collections, rig-data presets, animation helpers | Unity hierarchy/Inspector targets, Undo, non-destructive source meshes and retained previous bake assets |

Sources: [Auto-Rig/Smart/skinning](https://www.lucky3d.fr/auto-rig-pro/doc/auto_rig.html), [rig controls](https://www.lucky3d.fr/auto-rig-pro/doc/rig_behaviour_doc.html), [Remap](https://www.lucky3d.fr/auto-rig-pro/doc/remap_doc.html), [Quick Rig](https://www.lucky3d.fr/auto-rig-pro/doc/quick_rig_doc.html), [engine export](https://www.lucky3d.fr/auto-rig-pro/doc/ge_export_doc.html).

For these disconnected cartoon characters, contiguous propagation plus explicit island ownership addresses the observed defects without remeshing. The next useful additions would be pose-preserving IK/FK switching, twist distribution and corrective deformation. A true volume solver would be a separate feature for difficult layered/connected characters. Auto-Rig Pro's documentation distinguishes its approximate Voxelized engine from the separate Voxel Heat Diffuse add-on; they should not be described as the same algorithm.

### Verification — 7 November 2026

All **8 Riggle Edit Mode** and **3 Riggle Play Mode** tests pass. Live camera captures also confirmed raised-hand, bent-knee and head-aim poses on both characters without the original seam tears. Full project results: **174/174 Edit Mode**, **151/154 Play Mode**. The remaining Play Mode failures are two CoastalHike coaster-camera assertions (the saved coaster has `fixedThirdPerson: 0`) and `Car_XEntersTriggerDrivesBStopsExitsAndCanReenter` (parked displacement 0.01115 m against a 0.01 m limit). No coaster/car settings or code were changed by this rigging task.
