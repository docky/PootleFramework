# Verification record

Target: Unity 6000.6.3f1 on macOS. Record actual results here before handoff.

## Development observations

- Initial compile identified a Cinemachine default-value helper that is internal in 6.6; setup now uses explicit public settings.
- Initial physics tests identified rounded-edge step stalls and vertical ground-snap sliding down walkable slopes. Step landing reach and downward walkable contact handling were corrected.
- Explicit end-of-tick movement measured stop distances of 0.8800 / 0.9423 / 0.9733 m at 30 / 60 / 120 fps. Average-velocity integration reduced this to approximately 1.0050 / 1.0048 / 1.0044 m. Jump-launch integration was also corrected to avoid an entire gravity-free launch frame.
- Editor test input needs a real Play Mode lifecycle; virtual-gamepad coverage is therefore in Play Mode tests rather than pretending an Edit Mode MonoBehaviour automatically enabled its actions.
- A save-scene dialog blocked the Editor's main-thread command queue, although status still reported ready. A stack sample identified `SaveCurrentModifiedScenesIfUserWantsTo`; the user dismissed the dialog. Scene authoring now rejects cameras from other scenes, and Edit Mode fixtures use disposable scenes to avoid dirtying the playground.

The final suite, runtime, build, and camera observations follow.

## Edit Mode results

**17 passed, 0 failed**, full Edit Mode suite. Compile: **0 errors, 0 warnings** in Pootle's final compile.

The fixture replaces the Test Runner's disposable bootstrap scene in Single mode. Unity 6.6 rejects both an additive Editor scene when an untitled bootstrap scene is unsaved and the runtime CreateScene API outside Play Mode. The final fixture respects these restrictions without requiring another save prompt.

| Rate | Held jump apex | Airtime | Stop from 7.5 m/s | Reversal to negative velocity |
| --- | --- | --- | --- | --- |
| 30 fps | 3.6800 m | 0.7333 s | 1.0050 m | 0.3000 s |
| 60 fps | 3.6832 m | 0.7333 s | 1.0048 m | 0.2667 s |
| 120 fps | 3.6833 m | 0.7250 s | 1.0044 m | 0.2583 s |

These are actual capsule simulations against an isolated floor using explicit Update ticks, not a stopwatch or full rendering-throughput benchmark. Apex and stopping-distance spreads are below 0.1%, within the 10% target. Reversal retains whole-tick entry/exit quantisation; 30 fps reverses about 42 ms later than 120 fps.

Passed coverage: independent forward gain/turning drag; camera-relative diagonals and vertical-camera fallback; analogue walking, running, release and reversal; buffer retention/expiry and one-shot acceptance; coyote expiry and reuse prevention; extra-air-jump limits; shorter released jumps; steps versus tall walls; walkable versus steep slopes; ceilings and tight corners; central-support edge slipping; offset-capsule initial-overlap recovery; teleport clearing; diagnostic capture/freeze query and movement parity; repeatable camera setup and model-only capsule fitting.

## Play Mode results

**3 passed, 0 failed**, full Play Mode suite:

- Direct scene load: static Lily renders, the motor is grounded, Cinemachine Brain is connected, and simulation ticks advance.
- A virtual Input System gamepad preserves partial left-stick magnitude, moves the character, and rotates the camera with the right stick. Releasing camera input and changing character facing does not move the orbit yaw. The source action asset stays disabled because the adapters own private copies.
- Holding jump produces one jump through landing. Disabling/re-enabling the character preserves working input and leaves exactly one PootleInput and CapsuleCollider.

Runtime tests wait for elapsed game time, not an assumed number of rendered frames. This matters in an unfocused Editor where test frames can advance much faster than the target frame rate.

## Standalone build

The representative macOS player build succeeded using only `PootlePlayground.unity`, with **0 errors**. Output: `/tmp/Pootle-v0.1/Pootle.app` (about 84.8 MB). The initial full build took about 169 s, dominated by first-time Metal shader compilation.

Four inherited warnings were reported: Soap's `AppDomain.GetAssemblies()` use, two stripped URP hidden debug-occlusion shaders, and a deprecated pragma in TextMeshPro's mobile distance-field shader. No Pootle compilation errors or Pootle-specific build warnings occurred.

A final metadata-only correction keeps each probe's rejection reason and a frozen snapshot's slope/step settings with that sample. Stale capture is explicitly labelled. The full Edit Mode suite still passes after the correction.

The final incremental build, including the diagnostic metadata correction, also succeeded: **0 errors, 0 warnings**, 84,761,104 bytes, about 10.2 s. The built app is also copied to the ignored local output folder `Pootle_Unity/Builds/Pootle-v0.1/Pootle.app` for convenient use. No build binaries are added to source control.

The final full suites after this correction again passed **17 Edit Mode + 3 Play Mode** checks. The actual Odin Setup window opened and validated the character and its camera in `PootlePlayground` successfully.

## Visual check and remaining limits

A live Play Mode preview advanced from frame 8899 to 8945 during the server-side wait (condition met, two frame observations). A 1280×720 camera capture confirmed the static Lily model, material, lighting, course geometry, and camera framing. Oversized world-space labels were reduced and the corrected scene was captured again. The Setup window, character and output camera all resolve inside the standalone playground scene.

The original controller was audited through its working scene, prefab, source equations, and effective serialized settings. An instrumented side-by-side runtime benchmark of the original was **not** performed. The frame-rate table measures Pootle's explicit simulation, not a rendered game capped to those frame rates. Input coverage uses a virtual gamepad; physical controller feel, longer play sessions and personal camera-sensitivity tuning remain hands-on review tasks. Moving/rotating platform transport and the later abilities are deliberately outside v0.1.

The last rebuild after the scene-label adjustment succeeded in about 5.4 s: **0 errors**, **3 inherited shader warnings** (the two URP debug shaders and the TextMeshPro pragma described above). The persistent local app copy and `Preview.png` were updated under `Pootle_Unity/Builds/Pootle-v0.1/`.

Normal Editor background settings were restored after the preview. Incidental Unity/Odin-generated editor settings were removed from the intended change set. The original reference and the Pootle test model have no tracked changes.

The final console check reports **0 current errors and 0 current warnings**, with compilation complete. Historical captured logs retain the initial corrected compile error; it is not a current or runtime failure. Untouched render-pipeline settings were restored after Unity's build-cache serialization, and new local Odin configuration files are excluded from the intended source changes.

## Animation, surface fixes and traversal pass · 4 November 2026

The results below supersede the earlier static-model/scope notes for the expanded character pass. Recompile: **0 errors, 0 warnings**. Full Edit Mode suite: **27 passed, 0 failed**, about 2.02 s. Full Play Mode suite: **5 passed, 0 failed**, about 3.82 s.

New behavioral coverage: walking along/off a thin wall top; 0.15 m stairs at 30°, 45° and 70° approach; stable rest after falling onto a 44° slope; steep seated entry, downhill-relative facing, ignored steering, continued flat-ground coasting and jump escape; marked-wall climbing with automatic swept top-out; curved cylinder traversal; ordinary ledge catch, hanging under blocked clearance and automatic hop when clear; retained pole facing and a jump toward/across the pole. Existing jump, acceleration, ceiling/corner, overlap, diagnostics and helper regressions still pass.

Play Mode uses an Input System virtual gamepad, keyboard and mouse to verify WASD + held RMB look while the gamepad is connected, LB run and RB not run, inverted vertical camera input for both devices, active Lily jump/climb states, independent twin-stick input, private action ownership and input re-enabling. Animation root motion stays disabled.

Live visual verification advanced from frame 6894 to 10066, then to 28136. Captures confirmed seated Lily on the 60° ramp and a climbing pose on the marked wall. The visual captures used a temporary fixed review camera for unobstructed framing; normal gameplay remains Cinemachine. The initial orbit angle behind the steep ramp was obstructed and Cinemachine brought the camera close, as designed by its deoccluder.

Limits: static climb surfaces and straight, static poles/ropes; no rope swing, stamina, moving-wall transport or arbitrary gravity. The slide pose is procedural on top of Lily's crouch clip and keeps an upright collision capsule. Physical gamepad feel and personal animation/slide tuning remain hands-on review.

Final asset verification found and removed duplicate presentation overrides created while upgrading the reusable prefab. The upgrade now changes the prefab first and deduplicates scene presentation components; the Play Mode suite confirms one of each component and again passes all 5 tests. A separate isolated capsule simulation verified the flat-topped mesh cylinder also climbs, mini-hops and rests grounded at its 4 m top.

Final macOS build: **Succeeded, 0 errors, 0 warnings**, 84,996,760 bytes, about 8.55 s. The local app copy is updated at `Pootle_Unity/Builds/Pootle-v0.1/Pootle.app`. Review captures are saved beside it as `pootle-seated.png` and `pootle-climbing.png`. The previous uploaded v0.1 recording predates this traversal pass. Normal background settings and incidental build serialization were restored; the saved playground is ready to open and play.

## One-way rails · 4 November 2026

Full Edit Mode suite: **31 passed, 0 failed**. Full Play Mode suite: **6 passed, 0 failed**. New checks cover upward passage through rails, descending landing, directional jump-off, four-way facing on landing, walking orientation through a polyline corner, steep sliding into a flat runout with retained momentum, endpoint release, missed landings outside rail width, and teleport clearing attachment. A real scene test drives an Input System virtual gamepad through jump-through, landing and jump-off. Existing movement/traversal/animation checks continue to pass.

Live preview confirmed Lily at the 1.4 m rail surface, with frames advancing from 487 to 19054. `Builds/Pootle-v0.1/pootle-rails.png` records the view. Rail examples and the rider are saved in the playground and reusable character prefab.

macOS build succeeded: **0 errors**, 3 inherited shader warnings (URP hidden debug-occlusion shaders and TextMeshPro's deprecated debug pragma), 85,005,112 bytes, about 11.53 s. The local app copy was updated. Background overrides were restored and incidental build settings restored to their pre-build contents. Rails are static polylines with non-solid visual geometry; moving/spline rails are not implemented.

## Exact wall-contact lock fix · 4 November 2026

Captured the user's live stuck position `(-10.428930, 0, -7.009997)` beside the ledge block. Mode was Grounded, with no climbing/ledge/rail attachment. Temporary actors at the exact position could not move in any of four directions: zero-distance capsule casts returned synthetic normals opposite every requested direction. Recovery found no meaningful penetration at this nearly exact contact.

Movement casts now use a radius 5 mm inside the recovery capsule (or 50% of skin for larger skins), retaining skin stopping clearance. This prevents the initial-overlap ambiguity while preserving inward wall blocking. Four new regression cases at the actual reported world coordinates cover escape away/along both wall directions and inward blocking plus upward jumping. Full suites: **35 Edit Mode + 6 Play Mode passed**, no failures. The macOS player was rebuilt and the user's live position/camera restored for retry.

Final live-world recheck from the restored position: both tangential directions move 3.24 m in 40 ticks; outward movement travels 1.26 m before correctly meeting the opposite wall; inward motion remains blocked. A 1 mm margin had passed the distant test fixture but failed at the original world coordinates, so the regression fixture now uses the original coordinates and geometry. Final build: 0 errors, 0 warnings. The Editor is left playing at the restored user position and camera, with its normal background settings.

## Movement feedback, carriers, hooks, glider and springs · 4 November 2026

Final suites: **52 Edit Mode tests passed, 0 failed; 10 Play Mode tests passed, 0 failed**. New deterministic checks cover facing-relative pole offsets/turning, wider rail catches, walking onto a rail endpoint at floor level, 0.6 m side step entry while taller obstacles remain blocked, lateral slide rejection, eased top-out, translated/rotated/vertical platform carry and jump inheritance, moving-hook hold/drop/jump/obstruction release, camera-relative hook turning, spline distance/speed/reversal, glide steering/descent/release/ground and rail landings, full-force spring bounce/repeat, angled springs and disabled pads. Play Mode drives real Input System gamepad input through the scene's moving hook, glider animation/deployment, platform carry and spring launch, alongside existing twin-stick, keyboard/mouse, climbing, rail and lifecycle tests.

Live visual verification confirmed an advancing player loop (frame count 4142 → 4167 in the server-side wait). Lily's pole grip, hook pose and glider were inspected. Hook body height/forward offset place the ring in her hands and keep her head clear while rotating with desired facing. The glider canopy is fitted to Lily and has hand-to-canopy cords. Images are in `Builds/Pootle-v0.1/pootle-pole-fixed.png`, `pootle-hook.png` and `pootle-glide.png`. No Pootle test model is used. The previous uploaded demo predates these additions.

Tests use disposable scenes and the sample/prefab are authored through the Editor. Source/document whitespace checks pass; Unity itself serializes blank YAML values with trailing spaces. Core compilation has no errors or warnings. Visual fitting remains a basic prototype, and spline carriers are authored static paths rather than runtime-editing or networking systems.

Final macOS build succeeded: **0 errors**, 3 existing URP/TextMeshPro shader warnings; 85,250,768 bytes, 14.20 s. Updated `Builds/Pootle-v0.1/Pootle.app`. The final Console ground-truth check reports no compile or runtime errors. Background overrides and incidental build serialization were restored. The saved playground opens in Edit Mode ready to Play.

## Rail junctions, pole endpoints and automatic grip · 4 November 2026

Reviewed the supplied 9.75-second gameplay recording using decoded frames and the two screenshots. Seated entry now requires actual capsule contact, airborne separation exits the seated mode, and a contacted slope-to-flat join preserves the requested slide momentum. A descending impact no longer contributes artificial rail speed.

Final suites: **68 Edit Mode tests passed, 0 failed; 13 Play Mode tests passed, 0 failed**. Added regressions cover perpendicular rail standing, vertical neutral rail jumps, permissive/distant landing cases, intentional 90-degree corners, separate-rail T-junction transfer, automatic wall/hook grabbing and B detachment with re-grab suppression, inherited hook momentum on drop, fixed short-post landing and post-to-post jumps, pole top-out, elevated open bottoms, capped endpoints, facing limits/ladder lock, narrow-wall rail mounting, shallow slope/rail ground pounds, seated contact/separation, and narrow-gap recovery without ordinary single-wall teleporting. Prior locomotion, stair, slope, moving-platform, glider and spring checks remain passing.

Play Mode uses real Input System virtual gamepad input to verify the helix extrusion and ground-pound slide, sideways rail stance, neutral jump, and short-post position lock/jump. The helix has 339 walking segments and 3,264 mesh vertices; a live clearance sweep found **zero blocked capsule positions** across all 340 walking samples after relocating it away from existing obstacles. The running player loop advanced from frame 2446 to 2468 in the server-side frame-change check. Visual previews of Lily on the helix and the short posts are saved in `Builds/Pootle-v0.1/pootle-helix.png` and `pootle-standing-posts.png`.

The saved course contains the original thin-wall top rails, a new narrow-wall rail, a T-junction, five short posts, open/locked-end elevated poles, facing-limited pole, wall-facing ladder and a two-turn extruded helix, with connected floor extensions. Capsule sweeps continue to govern attachment and movement; recovery teleports only after sustained penetration or opposing-wall trapping. Original source footage is unchanged. Source/document whitespace checks pass.

Final macOS build succeeded: **0 errors**, 3 existing URP/TextMeshPro shader warnings; 85,405,360 bytes, 11.78 s. `Builds/Pootle-v0.1/Pootle.app` was updated. Incidental build serialization and normal background settings were restored.

## Junction, continuous spline and pole-top jump follow-up — 2026-11-04

Final regression run: **76/76 Edit Mode tests and 15/15 Play Mode tests passed**, with no compile errors or warnings. Nearby branch input now approaches the actual intersection along the current rail before transferring. Coverage includes both approach sides, an uphill branch, disconnected nearby rails, and the existing explicit 90-degree corner rule.

Helix movement evaluates Unity spline positions and tangents through an arc-distance table. It no longer treats nearby samples of the same continuous curve as candidate branches. Walking tests check displacement against commanded speed, facing continuity and distance to the actual spline at 30, 60 and 120 Hz, including coarse 1.5 m samples. The live gamepad test checks for position spikes while walking the saved helix.

Neutral pole-top jumps are vertical regardless of facing; tests verify returning to the same post and using air steering to reach the next post at the playground's 2 m spacing. Climbing launches retain their facing-directed behaviour. A live helix check confirmed RailWalking and zero blocked positions across all 340 search samples; the player-loop check advanced from frame 1648 to 1668.

## Double jump, rolling, carrying and cameras — 2026-11-04

Final suites: **89/89 Edit Mode and 19/19 Play Mode tests passed**. New coverage includes one extra jump and landing reset, jumping out of gliding, uphill/downhill speed and idle stability, modal rolling/coasting, full-capsule unroll rejection under a low roof, ball slope response and moving-platform jump inheritance, object pickup obstruction, front/beneath drops, underfoot fallback, fully blocked drop retention, and physics/lifecycle restoration. Live Input System tests exercise number-row 1–6 camera switching, returning to manual orbit, double jump, West ball toggle/blocked unroll, and North pickup/airborne drop.

The full scene run caught the initial rolling tunnel obstructing the existing moving hook route. The tunnel and supports were relocated to x=14, z=-2, clear of that path; all existing hook and rail tests then passed. The low-air drop check uses the current capsule pose directly because the physics broadphase can still contain the previous pose during the same character tick.

Live player-loop verification advanced from frame 7083 to 7104. Eight rendered previews cover carrying, ball mode and all six camera views, saved under `Builds/Pootle-v0.1/features-*.png`. The final console ground-truth check reported no current errors or warnings.

Updated macOS build succeeded with **0 errors**, the same three pre-existing URP/TextMeshPro shader warnings, 85,438,320 bytes and a 12.49 s build time. The Editor was returned to stopped mode with normal background/ticking settings.

## Contact smoothing, external forces and conveyors — 2026-11-04

Reviewed all five supplied clips (6.02, 4.89, 9.37, 2.72 and 6.14 seconds) and the wall-corner screenshot. Ramp motion no longer enters the stair-step path; a clear 0.6–1.05 m obstacle can use the short automatic hop. Cylinder attachment follows a continuous circular side in one swept move, and supported camera styles receive the same yaw change. Optional box-corner wrapping follows the capsule-clearance arc and honors the 95-degree maximum. Ground pounds catch valid pole tops; neutral moving-hook jumps have no planar launch.

Final runtime suites: **103/103 Edit Mode tests and 23/23 Play Mode tests passed**. New checks cover monotonic uphill motion on 25/44-degree ramps, short-hop ceiling rejection, ground-pound post landing, neutral hook launches, external-facing retention/release, wind lift and sideways drift, wind resuming after a grip release, conveyor obstruction, delayed crate acceleration and exact rider descent, smooth full cylinder orbits, and disabled/limited corner wrapping. Live gamepad checks verify camera-relative cylinder yaw, the saved wind/bumper objects, both conveyor surfaces, and a pickup–jump–drop–land–jump sequence on a crate before it reaches the ground.

The saved course includes two conveyors with animated directional arrows, two wind volumes, three bumpers, two small-hop blocks and a corner-wrapping wall. All three original carry cubes have the delayed fall component. The existing character remains Lily. Rendered previews are `Builds/Pootle-v0.1/environment-0.png` (course), `environment-1.png` (updraft) and `environment-2.png` (cylinder). The live player loop advanced from frame 2051 to 2069; the console ground-truth snapshot reported no current errors or warnings. Source/document whitespace checks pass; scene serialization uses Unity's normal formatting.

Final macOS build succeeded: **0 errors**, three existing URP/TextMeshPro shader warnings, 85,538,896 bytes, 4.92 s. The initial conveyor mesh warning was resolved by enabling triangle collision pre-baking on both authored mesh assets. Incidental build settings serialization was restored.

## Movement sounds and particles · 4 November 2026

- Compile: zero errors or warnings. Full suites: **108/108 EditMode + 24/24 PlayMode = 132 passing tests**.
- Added regressions for idle/blocked/rolling/conveyor silence, walking cadence, jump/double-jump bursts, one landing burst per contact, reset cleanup, and rail/pole-top landings. PlayMode checks the borrowed clips play, URP particle material is supported, and particles expire.
- Live landing preview: 20 frames advanced during capture, 24 particles visible, AudioSource output peak 0.2923 (nonzero, below clipping). Inspected `Builds/Pootle-v0.1/movement-feedback-landing.png`; short smoke puff appears around Lily's feet.
- Sample prefab and playground saved with feedback. Editor restored to stopped, automatic character simulation enabled, auto-tick and background override disabled. This pass was validated in the Editor; the previously generated standalone app/video has not been rebuilt or uploaded.

## Conveyor turning and spiral slide · 4 November 2026

- Compilation: zero errors/warnings. **108/108 EditMode + 27/27 PlayMode = 135 passing tests.**
- Verified both conveyors carry Lily and turn her by their curve heading; loose crates travel and rotate along the curved belt and stop at a blocking wall.
- Verified the new trough carries Lily through more than 420 degrees of turn, reaches the flat runout and permits jumping out. This checks the whole spiral rather than merely falling to a lower turn.
- Verified the lift carries Lily from ground level to the entry deck, waits at the top, and allows walking onto the deck. Found/fixed recovery checking penetration before moving-platform carry; the recovery check now runs after carry so sustained upward motion is not mistaken for being trapped.
- Visually inspected `Builds/Pootle-v0.1/spiral-slide-0.png` (expanded course) and `spiral-slide-1.png` (Lily sliding in the trough). Scene saved; Editor stopped, normal automatic simulation restored, auto-tick/background override disabled. This pass was validated in the Editor; no new standalone build or upload was requested.

## Momentum slide and follow camera · 4 November 2026

- Final compilation: zero errors/warnings. **108/108 EditMode + 32/32 PlayMode = 140 passing tests.** The junction integration test now waits for its crossing within a one-second deadline rather than sampling a render frame at the threshold.
- Slide contacts interpolate the trough's top normals and use consistent capsule support. Physics substeps integrate full gravity and remove only inward normal velocity; banks do not cancel momentum leaving their surface. An ascending rim exit remains airborne through brief lip contact, retaining the seated pose.
- Normal full-course descent retains over 12 m/s into the runout and exceeds 20 m/s on the course without standing/sitting chatter. Tests compare 30/120 Hz trajectories and exercise overspeed bank launches at 30, 60 and 120 Hz, braking and momentum-preserving jumps.
- Cinemachine follows travel direction, eases its distance/FOV with speed and honors manual look. Live Input System coverage checks key 7 and return to regular orbit. Deeper banks and a larger landing floor are saved in the playground.
- Recorded and inspected `Builds/Pootle-v0.1/pootle-slide-ride-validation-11s.mp4`: 330 gameplay frames, 1280×720 at 30 fps, 11 seconds of stereo audio. The first eight seconds show the normal descent/runout; the final three apply an exaggerated outward impulse to demonstrate a clean rim exit and landing. No repeated mount/dismount at the lip. Editor returned to stopped mode with normal simulation and background settings. No standalone rebuild or upload in this pass.
