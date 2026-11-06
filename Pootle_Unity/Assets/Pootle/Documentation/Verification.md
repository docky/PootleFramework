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

## Ledge validation, traversal handoffs and mountain course · 4 November 2026

- Compilation: zero errors/warnings. Final suites: **115/115 EditMode + 37/37 PlayMode = 152 passing tests.**
- Regressions exercise the vertical sides of 44/45° ramps, reject an unrelated platform behind a face, retain valid flat ledges/blocked top-outs, verify no airborne event between top-out and ground, release disabled colliders/markers and prevent a hook taking over an existing climb or immediately recapturing its jump. Existing narrow-wall-to-rail, pole-top and rail-junction coverage passes.
- Slide flight participates in rail landing and clears ride state on attachment. A separate test presses B over an open jump gap and verifies ground pounding begins.
- New 413 m mountain trough descends about 59 m through four banked bends and two open gaps. Complete neutral-input rides reach the finish at 30, 60 and 120 Hz, cross both gaps in ballistic flight, exceed 26 m/s and retain more than 15 m/s at the finish. Collider rays verify the gaps are open and landing troughs exceed 12 m wide. Live tests verify the access and return pads.
- Inspected the actual Cinemachine ride in `Builds/Pootle-v0.1/pootle-mountain-slide-validation-22s.mp4`: 660 frames at 1280×720/30 fps, H.264, stereo audio. The capture shows both jumps, banked bends and runout. Unity's offline recorder reported an FMOD output-resume error after capture; resetting the audio configuration restored normal playback, confirmed by listener output peak 0.0477. No runtime controller exception was observed.
- Scene saved; Editor returned to stopped mode with automatic simulation/access pads enabled and background override/auto-tick disabled. No standalone rebuild or upload in this pass.

## Body blob shadow and run · 5 November 2026

- Compilation: zero errors/warnings. Final suites: **115/115 EditMode + 39/39 PlayMode = 154 passing tests**.
- DualSense HID test exercises L1 held before input enable, confirms 12 m/s movement and release to 7.5 m/s with other gamepads present. The previous 9 m/s tuning assertion was updated to the intended new run speed.
- Shadow checks verify 15 bones, a populated transparent RenderTexture mask, receiving geometry, rejection beyond projection range and restoration of model shadow settings on disable.
- Live rendered previews inspected: `Builds/Pootle-v0.1/body-blob-shadow-air.png` (airborne body/limb blobs) and `body-blob-shadow.png` (44° ramp). Shader supported, 778 receiving triangles in the airborne view, advancing player loop, no current console errors. Saved scene and prefab; stopped Editor and restored background/auto-tick settings. No standalone rebuild in this pass.

## Depth-projected shadows and ball correction · 5 November 2026

- Compilation: zero errors/warnings. Final suites: **115/115 EditMode + 41/41 PlayMode = 156 passing tests**.
- Replaced the receiver raycast grid with a scene-depth projector and a 256×256 animated circle mask. Rendered-pixel tests verify shadows on thin straight and curved rails without colliders, coverage right up to a platform edge, and rejection beyond projection range.
- Ball mode uses one circular stamp and suppresses the ball renderer's regular cast shadow as well as Lily's. Tests verify switching back restores the body mask and disabling the component restores renderer shadow settings.
- The camera shortcut regression now waits for the side view to settle before checking its input lock, avoiding a timing-dependent failure during the camera transition. Shadow pixel comparisons wait for the updated mask to render before reading back.
- Visually inspected the ball, steps and narrow wall-top rail in the live scene. Saved previews: `Builds/Pootle-v0.1/depth-blob-ball.png` and `depth-blob-rail.png`. The projector receives on opaque geometry; transparent receivers are unsupported.
- Editor returned to stopped mode; scene clean, automatic character simulation enabled, background override and auto-tick disabled. No standalone rebuild or upload in this pass.

## Configurable prop shadows · 5 November 2026

- Compilation: zero errors/warnings. Final suites: **115/115 EditMode + 43/43 PlayMode = 158 passing tests**.
- Rendered-pixel regressions verify both depth projection and transparent quads on a receiver. Additional checks cover ignoring a prop's own colliders, alignment to tilted ground, hiding over missing ground, circle/ellipse proportions, rounded-rectangle masks, yaw following, custom textures, missing custom textures and restoration of normal cast shadows on disable.
- Saved circular blobs on 11 poles and two hooks. The moving shuttle uses the simple transparent technique; the other hook and poles use depth projection. Live checks confirmed all 13 projections active and the shuttle's projection tracking its movement. Inspected `Builds/Pootle-v0.1/pole-blob-shadow.png` and `hook-blob-shadow.png`; the hook was temporarily held over unobstructed floor for its close-up, with its saved path unchanged.
- Editor restored to stopped mode with a clean scene, normal automatic simulation, background override off and auto-tick off. No standalone rebuild or upload in this pass.

## Slope shadow coverage, animation cadence and collectibles · 5 November 2026

- Final compilation: zero errors/warnings. **115/115 EditMode + 46/46 PlayMode = 161 passing tests**.
- New rendered-pixel regression checks both +44° and −44° slopes, requiring shadow coverage on uphill geometry above the former horizontal cutoff. The isolated test ramp sits above the playground floor to avoid intersecting receivers. Existing rail, edge, prop and ball shadow tests still pass.
- Live animation regression checks the LocomotionRate parameter is 2 while jogging, 3 while running and 1 after jumping; the Animator's global speed remains 1. Scene and prefab use the updated controller.
- Pickup regression checks all 20 authored objects, three distinct Soap counters, floating/spinning motion, rotating rectangular coin shadows, actual player trigger collection, exactly one increment, other counters unchanged, non-player rejection and immediate shadow removal. All 20 live projections were active during visual inspection.
- Inspected `Builds/Pootle-v0.1/collectible-trail.png` and `slope-shadow-fixed.png` in the running scene. The temporary isolated slope and preview camera changes were confined to Play mode. Editor restored to stopped mode, with normal automatic simulation and background/auto-tick settings. No standalone rebuild or upload in this pass.

## Spring turnstiles and one-way floors · 5 November 2026

- Compilation: zero errors/warnings. Final suites: **121/121 EditMode + 50/50 PlayMode = 171 passing tests**.
- One-way tests cover jumping through and landing at 30/60/120 Hz, insufficient-height jumps, underside/side clearance, high-speed ground pounds, walking off and restoring solid collision when the marker is disabled. A live gamepad jump verifies the authored mint platforms.
- Turnstile tests exercise real PhysX springs on all three axes with rotated parents, passing ±180°, CW/CCW commands and events, player pushes in both directions, settling and anchored position. The omitted-arm gate preserves quarter spacing and allows entry through its gap.
- Scene-reload testing found that a newly loaded HingeJoint can briefly report a NaN angle before its first simulation step. Initialization now waits for a finite angle instead of propagating it into the spring target. Rotating arms use speculative CCD. The western floor extends beneath all three gates.
- Visually inspected live rendered previews `Builds/Pootle-v0.1/spring-turnstiles.png` and `one-way-platforms.png`; all three gates remained stationary and anchored at rest. Saved three gates and six mint floors. Editor returned to stopped mode, clean scene, normal automatic simulation, background override off and auto-tick off. No standalone rebuild or upload in this pass.

## Ledge boundary, rail pressure and general physics · 5 November 2026

- Compilation: zero errors/warnings. Final suites: **131/131 EditMode + 52/52 PlayMode = 183 passing tests**.
- New edge regressions cover high catches 0.1, 0.35, 0.9 and 1.5 m below the lip, marked and ordinary walls, neutral-input automatic top-out, and head-height hanging when the destination is blocked. Existing ramp rejection and smooth movement regressions pass. Peak top-out lift speed is bounded; the older fixed 40-frame completion check now waits for the bounded transition, up to one second.
- Rail side-pressure regressions run at 30/60/120 Hz: held landing input cannot dismount; neutral settling plus sustained pressure releases with outward/downward velocity, no immediate magnetic recatch, and normal air movement. An armed rider still chooses a valid nearby T branch. Existing corner and spline tests pass; sideways-standing checks use brief input under the new dismount threshold.
- Real-physics tests verify ordinary light/heavy box displacement, disabling interaction, torque from standing weight, staying supported on a tilting board and return toward level after unloading. Existing CW/CCW player-push, detent settling, X/Y/Z hinge and angle-wrap tests pass through the new general motor interaction.
- Authored five turnstiles, two push boxes and one seesaw. Moved the seesaw clear of the enlarged soft gate's rotation and increased that gate's damping to 25. Every arm is offset vertically above the hub and other arms.
- Inspected live `Builds/Pootle-v0.1/physics-playground.png`, `weighted-seesaw.png` and `edge-top-out.png`. Lily remained grounded on the authored seesaw at its 18° limit; a high catch on the narrow wall completed directly into RailWalking. Current Editor console: zero errors/warnings. Restored stopped mode, automatic character simulation, input reference, clean saved scene, background override off and auto-tick off. No standalone rebuild/upload in this pass.

## Driven physics movers and demo · 5 November 2026

- Final suites: **131/131 EditMode + 55/55 PlayMode = 186 passing tests**. Current Editor console: zero errors/warnings; compilation successful.
- Added a continuous spring-driven four-paddle rotor, reciprocating dynamic shuttle and spring-driven pendulum on an extended western floor. Contact transfers body point velocity to the player and applies a limited reaction impulse; physics-driven momentum preserves the player's facing. Existing bumper facing timeout remains unchanged.
- Live tests verify incoming body pushes, reaction slowdown, facing throughout contact, rotor speed near 40 degrees/second, modest slowdown against Lily and recovery after unloading. Shuttle and pendulum displace a neutral player without turning them. Existing box mass, standing weight, seesaw and turnstile tests pass.
- Rotor startup uses quaternion twist relative to its initial pose, avoiding the undefined native hinge angle seen on a newly loaded sleeping joint. Tested against the saved scene after reload.
- Recorded and visually checked a **50-second, 1280×720, 30 fps H.264 video with audio**, showing box pushing, seesaw weight, rotor contact, shuttle and pendulum pushes. Capture completed all 1,500 frames; audio duration is 50 seconds with nonzero signal.
- Uploaded through Transmit to https://docky.net/builds/pootle-v0.1-physics-movers-demo-50s.mp4. Transmit reports upload complete; public HTTP HEAD returns 200, video/mp4, and 7,316,834 bytes matching the local file.
- Editor restored to stopped mode with a clean saved scene, background override off and auto-tick off. Recording-only camera/input/timing overrides were restored. No standalone build in this pass.

## Smooth physics nudges and pink turnstile demo · 5 November 2026

- Compilation: zero errors/warnings. Final suites: **131/131 EditMode + 56/56 PlayMode = 187 passing tests**.
- Fixed overlap recovery mixing interpolated Transform poses with the simulated collider poses used by sweeps. Child colliders now use their attached body's simulated pose. Incoming contacts anticipate the next physics step, and newly received velocity contributes to the full current displacement instead of half of it.
- Passive physics/wind momentum no longer drives walking animation or footsteps. Player locomotion speed blends over 0.12 seconds; jogging and running retain their requested 2× and 3× rates. Unmarked dynamic physics props no longer interrupt contact with an automatic ledge catch or mini hop; explicitly marked climb surfaces still use traversal.
- New live regression renders 720 frames at 120 fps with real 50 Hz physics and body interpolation, without per-frame SyncTransforms. It checks actual rotor displacement, no repeated false stops, bounded changes in displacement, grounded state, unchanged facing, idle animation speed and 1× idle cadence. Existing turnstile, pushing, weight, traversal, sound and animation checks pass.
- Recorded **50 seconds at 1280×720, 60 fps, H.264 with stereo audio**. The first 24 seconds show the basic pink turnstile pushed both directions and settling into its spring detents. Remaining footage shows passive rotor, shuttle and pendulum nudges with idle feet. Reviewed rendered contact frames and motion/event audit; all 3,000 frames completed, audio signal present.
- Uploaded via Transmit: https://docky.net/builds/pootle-v0.1-smooth-physics-turnstile-demo-50s.mp4. Transmit reports upload complete; public HTTP HEAD returns 200 and 12,308,957 bytes matching the local video.
- Restored stopped Editor, clean saved playground, normal automatic character simulation, capture rate zero, background override off and auto-tick off. No standalone rebuild in this pass.

## Visible physics contact and 20-second demo · 5 November 2026

- Compilation: zero errors/warnings. Final suites: **131/131 EditMode + 57/57 PlayMode = 188 passing tests**.
- Removed velocity-scaled contact anticipation. Incoming transfer now requires the capsule's fixed contact skin (maximum 1.5 cm), regardless of the prop's approach speed. Saved Lily's capsule at 0.34-m radius in the scene and prefab and updated new-character authoring to use that fit.
- Actual collision poses remain authoritative. The optional avatar applies a bounded 25-ms visual smoothing response only after physics momentum arrives, fading it out when the shove ends; ordinary movement, traversal and teleports stay immediate. The smooth-nudge regression now measures the completed rendered pose instead of assuming physics corrections occur at render frequency.
- New regression checks boxes and spheres at 2.5, 6 and 15 cm clear gaps, each approaching at 0.5, 3 and 8 m/s: neither player momentum nor reaction force changes while separated. Actual touching transfers velocity. The 120-fps rendered-nudge, facing, idle-feet, rotor slowdown, shuttle, pendulum and traversal suites pass with the tighter capsule.
- Recorded **20 seconds, 1280×720, 60 fps H.264, stereo audio**: pendulum approach/contact, continuous rotor, shuttle contact and basic pink spring turnstile. All 1,200 frames and 960,000 audio sample frames completed. Reviewed close contact images; a temporary baked-mesh/rendered-collider audit at first momentum transfer measured a 2.1-cm visible gap for the pendulum and touching vertices for rotor and shuttle. Staging and camera changes were Play-mode only.
- Uploaded through Transmit to https://docky.net/builds/pootle-v0.1-contact-physics-demo-20s.mp4. Transmit reports upload complete; public HTTP HEAD returns 200, video/mp4 and 6,730,047 bytes, matching the local clip.
- Editor restored to stopped mode, clean saved playground, original spawn, normal automatic simulation, capture rate zero, background override off and auto-tick off. Current console: zero errors/warnings. No standalone build in this pass.


## Frobject sensor / interaction (2026-11-05)

Implemented the configurable awareness/focus sensor, A/F interaction routing, independent dot-to-circle presentation, a depressing toggle button, and advancing world-space NPC speech. Scene and character prefab contain the sensor; the two examples are at (2, 0, -10) and (6, 0, -10), clear of the mountain access pad.

Validation: Unity 6000.6.3f1 compiled with zero errors/warnings. All **137 EditMode** and **59 PlayMode** tests passed. Six new EditMode cases cover omnidirectional awareness, facing/distance focus, single-target dispatch, readiness, layer masks, wall occlusion, disable, cooldown, and shared jump-input consumption. Two new PlayMode cases use the authored examples to verify actual gamepad A input, hold-without-repeat, jump fallback, marker readiness, dialogue advancement, wide-text fitting, timeout and hiding on loss of awareness. The final PlayMode suite was rerun after the visual text-size correction and passed 59/59.

Live visual inspection confirmed the white awareness dot, mint ready circle, and readable world-space speech panel. Frame-count advancement was verified via Pipeline; no new console errors were logged during the final inspection. Play mode was stopped and temporary background/auto-tick overrides were removed. The linked Notion page was inaccessible; implementation follows the user's written brief.

## Carry obstruction and adjustable turning pivot · 5 November 2026

- Inspected the user's running state at approximately (−13.53, −0.005, −11.59). Carry cube 2 was released, with valid pickup geometry and ordinary solid/falling-crate settings; invoking pickup successfully reattached it. The follow code allowed Lily to leave a blocked box behind, then automatically released it when the tether distance grew too large.
- Held-box sweeps now constrain character translation and slide along obstructions. Blocked following retains ownership instead of automatically releasing. Pickup transfer ignores separating/tangential contacts. Carry turns sweep both capsule and box in small angular steps, stopping at an obstruction while keeping the box held.
- Added `PootleCarrying.rotationPivotForward`, default **0.4 world units forward**, saved in scene and prefab. Tests cover zero, 0.4 and 0.8 offsets, fixed world-pivot geometry, blocked rotation, sustained wall contact without separation, wall-touch pickup and repeated drop/pickup.
- Final suites: **143/143 EditMode + 59/59 PlayMode = 202 passing tests**. The first live suite exposed three keyboard tests depending on Game-view focus; their fixture now temporarily routes synthetic keyboard/mouse events to the game and restores the user's input setting in teardown. The final full live suite passes.
- Restored Lily to the reported wall, then verified an actual North-face-button drop and pickup through the Input System. Live result: drop succeeded, Carry cube 2 held again, grounded, pivot 0.4, saved scene clean, original keyboard-focus policy restored. Inspected `Builds/Pootle-v0.1/carry-wall-restored.png`.
- Left the game running with the cube in Lily's hands. Removed the temporary gamepad/player-loop hook and restored background/auto-tick settings. No current console errors; no standalone build or video requested in this pass.

## Non-blocking held boxes and safe release · 5 November 2026

- Removed the held-box sweep from the motor, box-volume restrictions from carry turns, and blocked-pose freezing from following. Held boxes remain kinematic triggers, always follow Lily, and do not obstruct jumping, landing or one-way passage. The adjustable forward turning pivot still checks Lily's capsule. Picking up the current support clears platform attachment so the held box cannot lift its owner.
- Release validates the entire box volume and reachability from Lily instead of sweeping from a possibly embedded held pose. Airborne release prefers beneath the feet; front placement is the fallback. A blocked standing front drop searches backward steps up to the configurable **0.6-unit Maximum Drop Backstep**, requiring clear capsule travel and central walkable footing. Beneath is the last fallback; no safe position leaves the item held.
- One-way release checks permit passage from below but prevent dropping down through their top. Standing slope placement accounts for the full projected box extent, including the uphill corner. Reset/disable tries safe local placement, then a previously safe carry/pickup pose; it does not activate physics at an embedded carry pose. Grip targets use the live box transform rather than stale broadphase bounds during a jump.
- Compilation: zero errors/warnings. Final suites: **152/152 EditMode + 61/61 PlayMode = 213 passing tests**. New regressions cover box-only overhangs, matching loaded/unloaded jump heights through a one-way platform, own-support pickup, safe backsteps, blocked backsteps, embedded air drops, wall/floor rejection, thin one-way release and whole-box slope clearance. Existing carry obstruction cases were updated to the requested non-blocking behavior.
- Live gamepad check picked up Carry cube 1, jumped through the authored mint platform and landed on it with the box held throughout. Peak player height: **3.713 m**; measured box-follow error: **0 m**. The temporary virtual device was allowed to initialize before its first button press. Inspected `Builds/Pootle-v0.1/nonblocking-carry-one-way.png`; current console has no errors/warnings.
- Restored the initially stopped Editor and clean saved scene, with normal automatic character simulation. Temporary gamepad/player-loop hooks were removed; auto-tick is off and the background override is cleared, preserving the project's enabled background setting. No build or video requested in this pass.


## Revised gamepad layout (2026-11-05)

B/Circle and LB/L1 hold Run; LT/L2 toggles ball mode; X/Square interacts; Y/Triangle carries/drops; RT/R2 detaches or ground-pounds; RB/R1 holds Glide. A/Cross remains Jump, including beside ready Frobjects. The Grip action and its bindings were removed from the input asset and input adapter; traversal continues to attach automatically. Keyboard interaction is now E, freeing F exclusively for glide. Playground boards, sample object names, NPC dialogue, setup tools and GettingStarted reflect the layout.

Unity compilation passed without errors or warnings. All **143 EditMode tests** passed. The first complete PlayMode run passed 58/61: two existing rail tests and the keyboard backup test failed, then passed on targeted reruns (9/9 gamepad tests and 1/1 keyboard test). A final complete PlayMode run passed **61/61** in 93.29 seconds. New checks cover independent B/LB holds, B in the air versus RT ground-pound, LT hold/release/repress, and non-overlapping E interaction/F glide. Updated existing tests cover RT detachment, LT blocked unroll, Y carry/drop, RB glide and X interaction with independent A jump.

## Tiny Car Controller Sedan (2026-11-05)

Added the 1.6× Sedan, local URP body/wheel/effect materials, Frobject entry, exclusive driving input, a Cinemachine chase camera, B stop/exit and safe exit placement. The parked car ignores movement input. Four PlayMode regressions cover gamepad driving/exit/re-entry, keyboard driving/exit, parked input/materials and blocked exits/component cleanup.

The first focused run passed parked-input/material and blocked-exit/cleanup tests. Both driving tests exposed the original Pickup parking position pointing directly into Carry cube 3; the Sedan has been moved to the clear space at (8, 0, -5). A live gamepad check then drove it to approximately (8.03, -0.01, 0.55), with its chase camera following. Inspected `Builds/car-diagnostic.png`: the Sedan renders with its textures and no error shader. All 34 vehicle prefab/material references resolve; `git diff --check` passes.

After the Save Modified Scenes dialog was dismissed, all four focused car tests passed. Final complete suites passed **152/152 EditMode** (9.36 s) and **65/65 PlayMode** (96.89 s): **217 passing tests**, including the disabled-interaction and safe-exit lifecycle changes. Unity reports no current compile errors or warnings. The playground is saved with the Sedan at (8, 0, -5), and the Editor is left out of Play Mode.

## Sedan air jumps and rechargeable boosts (2026-11-05)

Added one grounded jump plus four extra air jumps on A/Cross/Space. The chassis and wheel rigidbodies receive the same vertical velocity change; two supported tires refill the air allowance after landing, with takeoff debounce and upward-velocity checks to reject stale ground contacts. Added four boost charges on RB/R1/Left Shift: one-second bursts, a forward kick, acceleration in air and on the ground, twice the normal speed limit, and one charge restored every three seconds. Repeated presses chain burst duration; holding either ability button never retriggers. Charges recharge while parked and are not reset by entering/exiting. Exit/disable cancels active boost multipliers. Keyboard braking moved to Left Ctrl; RT/R2 and B exit remain unchanged.

Saved tuning on the Sedan prefab. All **7/7 focused car tests** passed, including air-jump exhaustion/refill, synchronized wheel takeoff, boost exhaustion, sequential recharge to capacity, re-entry without refilling, keyboard air boosts, expiry and cleanup. Final full suites: **152/152 EditMode** (10.42 s) and **68/68 PlayMode** (115.67 s), **220 passing tests**.

## Pickup truck, demo props and trigger pedals (2026-11-05)

Added the Tiny Car Controller Pickup at **1.7× scale**, parked at (11, 0, -12) facing south. It uses the existing interaction, B exit, chase camera, four air jumps and rechargeable boosts. The new south apron borrows the demo asphalt, race ramp, quarter pipe and mound; props have static mesh colliders and local URP material copies. Vehicle controls now use analog **RT/R2 acceleration** and **LT/L2 braking**, with left-stick steering/pull-back reverse and WASD/Ctrl keyboard backup. Existing car tests explicitly select the Sedan so the second vehicle cannot make their target ambiguous; new tests exercise both vehicles' analog pedals and the Pickup/demo geometry.

All **38 vehicle asset references** resolve. The save dialog was dismissed during the following camera task; Unity then hit an ADTM audio-thread fatal error and was reopened on the same project. After recovery, Pickup geometry, abilities and analog RT/LT controls passed as part of the **14/14 focused vehicle tests**. The pedal test now compares steering against the Input System's processed stick value, accounting for its configured dead zone. Unity also synchronized legacy texture/color aliases and transparent render settings on the imported demo materials when saving assets.

## Vehicle camera modes on 8, 9 and 0 (2026-11-05)

Added three modes to both vehicle prefabs: **8 Demo swing**, **9 Camera-relative**, and **0 Warthog-style**. Demo swing retains world yaw at rest and follows travel increasingly with speed, allowing the nose to swing away from the view. Camera-relative steering interprets the stick in the camera's horizontal frame; Warthog-style steers the nose toward camera aim while driving, with sideways stick offsets. Both direction modes correct steering sign when reversing. Right stick/arrows and RMB+mouse orbit the camera; mode changes preserve the view, camera collision protection remains active, and private look/mode actions are released on exit. The suspended on-foot camera ignores driving look input.

All **14/14 focused vehicle tests** passed (33.33 s), covering both vehicles' top-row number keys, unoccupied input exclusion, camera swing/follow, actual reverse turn direction, aim steering, mouse look, camera obstruction and existing jumps/boosts/pedals/exit. Final full suites passed **152/152 EditMode** (10.44 s) and **75/75 PlayMode** (125.02 s): **227 passing tests**.

Live verification confirmed advancing frames, the active Pickup Chase Camera, and the Pickup centered in the rendered viewport. Inspected `Builds/car-camera-warthog.png` and `Builds/car-camera-demo-swing.png`: the truck and demo geometry render correctly, and the stopped demo camera retains its 180° heading while the truck's nose swings to 245°. Unity's current console reports zero errors/warnings. Returned to the saved playground out of Play Mode, with auto-tick disabled and the background override removed.

## Four procedural double-decker platform buses (2026-11-05)

Added four six-wheel double-deckers at South Stunts, West Workshop, East Meadow and Spiral Lookout. Geometry is assembled from boxes and cylinders and baked into shared body, stair and wheel meshes with per-vertex colors. One `Pootle/Vertex Color Lit` URP material supplies lighting, shadows and fog without model textures. Each bus has two passenger decks, seats, windows, boarding steps, roof rails, engine audio, a wider chase camera and the existing vehicle controls/abilities. Parked buses freeze the chassis and wheel rigidbodies; access stairs unfold while unoccupied and stow when driving. The roof is 5.45 m high, with a nearby 6.45 m landing. Vehicle-specific exit offsets keep Lily clear of the large body and the stairs when they unfold again.

All **3/3 focused bus tests** passed (6.28 s): four vertex-colored buses, stable roof support under player weight, walking both stair flights without jumping, crossing to the roof, jumping from the roof onto the higher landing, driving, analog braking, safe exit, stair deployment and parked input exclusion. The intermediate stair waypoint height tolerance allows one riser because the capsule may already step onto the next tread. Final complete suites passed **152/152 EditMode** (10.29 s) and **78/78 PlayMode** (133.57 s): **230 passing tests**.

Inspected `Builds/double-decker-platform-bus.png` from an advancing live Play Mode session. Lily stands grounded on the bus's `Walkable roof` at approximately 5.43 m after suspension settling; the red/cream body, stairs, roof bridge and high landing render correctly. The vertex-color shader reports no errors. Removed the temporary preview by stopping Play Mode and restored normal background/ticking settings.

## Airborne parking and demo-floor overlap (2026-11-05)

Separated the parking request from the physical lock. Exiting stops horizontal/rotational motion while retaining each chassis/wheel body's vertical velocity; the vehicle continues its jump arc and falls until stationary ground supports at least two tires and vertical motion settles. Locked vehicles recheck support and release the lock if it disappears. Moving rigidbody/platform supports remain physical rather than being anchored in world space. Bus access stairs deploy only after the bus actually parks, so dismounting an airborne bus cannot unfold them in mid-air.

Raised the entire Tiny Car demo driving area by **0.01 m**, including its asphalt and props; measured asphalt collider top **0.009999633 m** above the regular Y=0 floor. Updated the setup tool to retain the same offset. The parking regressions exercise upward exits and subsequent landing/locking for the Sedan, Pickup and bus, plus removal of a static supporting platform. All **16/16 focused vehicle tests** passed (40.93 s). An initial run lost synthetic input while Unity's native File menu was open; closing it restored input and the isolated drive/exit check and focused rerun passed. Final full suites passed **152/152 EditMode** (13.06 s) and **80/80 PlayMode** (145.32 s): **232 passing tests**, including the bus platform/stair regressions.

Live check used a real Sedan jump and an exit 0.2 seconds later: exit succeeded at **1.099 m**, parking remained unlocked, and vertical velocity stayed **+5.039 m/s**. The Sedan then landed and engaged its parking lock with zero vertical velocity. Inspected `Builds/parking-and-demo-floor-fixed.png`: the car is grounded and the asphalt/regular-floor overlap renders cleanly. Current console has no errors or warnings. Returned to the saved scene out of Play Mode, removed the temporary preview callback/camera, disabled auto-tick and cleared the background override.
