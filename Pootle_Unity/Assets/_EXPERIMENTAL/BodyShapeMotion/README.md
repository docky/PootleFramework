# Body Shape Motion — experimental comparison lab

A post-animation adjustment layer for Humanoid characters with broad torsos, short limbs and large shoes. It is separate from Riggle: `BodyShapeMotion.Runtime` references only Unity, and does not alter animation clips, Avatars, meshes, weights or source prefabs. The Doggy lab uses the existing Riggle-generated Doggy as a test asset; its editor preview also refreshes that asset's twist/corrective components.

## Try the lab

Open **Tools → Body Shape Motion → Open Doggy Lab**, then **Open comparison scene**. The scene is `Doggy Body Shape Lab.unity` in this folder. Use **Build / rebuild lab** to regenerate the scene; an existing edited shape profile is retained.

There are **16 animations × 4 levels = 64 samples**. Columns are Original (0%), Light (33%), Medium (66%), Strong (100%). Each row shares its animation and time. By default only the selected row is visible; disable **Show this row only** for the complete grid. Enter Play Mode for live animation, use Play/pause and speed, or scrub a normalized pose in either Edit or Play Mode. Previous/Next changes the row; Frame row frames it in the Scene view. Playback is in place so cells do not drift; the original hip motion is retained. Scrubbing applies corrections immediately; live playback uses smooth correction blending.

Clips: Shy, Shy Refusal, Triple Pose, Dance 03, Dance 04, Cheers, Laugh, Surprised, Stretch 2, Pick Up, Cross Legs, Hand On Hip, Curtsy, Walk, Run, Run Turn Left. The original FBX clips remain untouched.

## Adjustment model

`BodyShapeProfile` stores editable ellipsoids attached to Humanoid bones, hand-center offsets and hand/forearm radii, plus oriented foot boxes. The fitter gathers weighted rest-pose vertices; the initial torso fit excludes far-back appendages so a tail does not become a giant belly. These are approximations to inspect and tune, not a full mesh collision model. Enable Show Volumes on a selected Body Shape Adjustment to inspect them.

After animation, `BodyShapeAdjustment` projects hand and forearm sample points out of torso volumes, computes foot separation where the shoe boxes overlap, and optionally lifts shoe soles above a fixed world-Y floor. An analytic two-bone solve preserves segment lengths and hand/foot orientation. Correction offsets are bounded and exponentially blended. The root, hips, spine and head are not repositioned. The layer restores the underlying animated limb pose before the next animation evaluation, avoiding accumulated drift. Zero strength is the unmodified animation.

Strength blends the correction amount, not body size. Arm Weight and Foot Weight allow per-clip tuning. In this test scene, Cross Legs uses 45% foot weight and Hand On Hip uses 65% arm weight at every strength level, retaining some deliberate contact. These are editable sample settings rather than changes to the shared profile or clips.

Under **Knee direction** in the shape profile, **Knee Follow** (default 1) carries the animated bend plane with the corrected leg, avoiding inward knees when shoes are spread apart. **Knee Outward Degrees** (default 25°) adds turnout as lateral shoe correction increases. It fades with strength and the smoothed correction, leaves feet oriented as animated, and retains knees already turned further outward. Nearly straight legs use a stable character-forward hint; this assumes the character root's +Z faces forward. Pure floor lifts receive no extra turnout. Set Knee Follow to zero for the previous fixed knee hint, or set only Knee Outward Degrees to zero to keep bend-plane following without added turnout. Existing baked clips need rebaking to include these settings.

## Use outside the lab

Add `BodyShapeAdjustment` to a character with a valid Humanoid Animator, assign a fitted profile, and leave Manual Evaluation off. Use normal Animator update timing and positive uniform character scale. It runs after animation and before late twist/corrective drivers. Other scripts that overwrite the same arm/leg bones later will override the result; the lab releases the existing manual control rig so it cannot pin the animated limbs.

For an external/manual Playable driver: enable Manual Evaluation, call RestoreAnimationPose before evaluating the source animation, then Evaluate(deltaTime). The lab's BodyShapeSample demonstrates this. Profile coordinates belong to the fitted character's bone-local rest pose; do not reuse Doggy's dimensions blindly on another character. The editor `BodyShapeProfileFitter.Fit` utility supplies an initial profile for another Humanoid; inspect the result before use.

## Measure and review

**Run 16-clip overlap audit** samples 48 phases of each clip and strength, producing `Builds/BodyShapeMotion/doggy-overlap-audit.csv`. It reports mean/max torso-hand proxy penetration (sum of both hands), mean/max shoe-box penetration, and maximum tip displacement. These are proxy measurements, not pixel/mesh collision certification. The report includes the Original baseline and intentional-contact clips. Review the visible comparisons as well as the numbers.

The initial Doggy audit reduces mean hand proxy penetration by about 86% and shoe proxy penetration by about 90% at Strong versus Original. Those numbers describe the supplied profile and 16 selected clips, not arbitrary characters or animations.

## Limits

This is bounded procedural pose adaptation, not a universal collision-free retargeter. Short limbs can be unable to reach an outside point. Strong correction can change a gesture, produce foot sliding or sacrifice contact. Crossed legs, hugging, hand-on-hip, face contact, props and seated poses need tuning or authored contact constraints. The default profile covers torso contact and shoes; it does not resolve head/ear, upper-arm or arbitrary mesh/cloth intersections. Floor clearance assumes a flat floor, not terrain or stairs; it is not foot planting. Source clips with non-looping endpoints may visibly reset when repeated.

## Bake new clips

Expand **Bake adjusted clips** in the lab panel. Choose Original, Light, Medium or Strong, set **Samples per second** (15–120; default 60), and choose whether to include temporal smoothing. **Bake this animation** exports the selected row; **Bake all 16 animations** exports all rows at the selected strength. Each sample's profile, arm/foot weights and floor settings are used. Run another bake for another strength.

Each bake creates a new folder under `Generated/Baked Clips`, with `.anim` clips, an Animator Controller, a **Doggy Baked Playback.prefab**, and bake notes. Existing bakes and source clips are preserved. The controller has one state per clip; the first plays by default. Change state through the Animator as usual.

These are **character-specific Generic transform clips**, capturing the corrected skeleton, twist bones, attachment transforms and blend shapes. They are not retargetable Humanoid muscle clips. Use the supplied playback prefab, or the same hierarchy with its Animator **Avatar set to None** and procedural pose/IK/twist drivers disabled. The supplied prefab already has those settings and has no BodyShapeAdjustment or BodyShapeSample component. There is no live body-shape solver cost during baked playback.

The bake is in place, matching the comparison lab: the character's scene/root placement is excluded and hip/body motion is retained. Source duration, loop flag and animation events are copied. Non-looping clips include their actual final pose instead of wrapping to frame zero. Moving channels retain all sampled keys for playback fidelity; exactly constant channels use just their first and last keys. This prioritizes accuracy over file size. Smooth loop bakes pre-roll the end of the clip to settle the correction filter; smoothing is evaluated at the chosen bake rate. This does not repair a discontinuity already present in the source loop. Baked corrections cannot adapt to a later body-size/profile change, terrain or props; rebake after tuning. The generated prefab disables existing scripts, so attach any desired animation-event handlers separately.
