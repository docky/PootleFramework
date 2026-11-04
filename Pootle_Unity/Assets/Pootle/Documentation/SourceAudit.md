# Reference audit · 4 November 2026

Destination: `/Volumes/Mac Data/Git/PootleFramework/Pootle_Unity`.

Reference: `/Volumes/Mac Data/Git/PootleFramework/PlayerTwo_Reference_Unity`.

Both projects use Unity **6000.6.3f1** (revision `45d8eee7de74`), Input System **1.20.0**, and URP **17.6.0**. Odin Inspector is already present in the destination. Cinemachine **6.6.0** was added individually using Package Manager; this is the installed package's current version of the Cinemachine 3 architecture. It matches the reference. No reference Packages, ProjectSettings, game scripts, scenes, or gameplay prefabs were copied wholesale.

## Chosen working configuration

The reference's `Library/LastSceneManagerSetup.txt` and open Editor identify `Assets/PLAYER TWO/Platformer Project/Examples/Scenes/TestLevelScene.unity`. Its character is `Examples/Prefabs/Entities/Lily.prefab` (GUID `eb0f77ac3ce65a04db54ff55e59b0815`). Lily's enabled stats manager has one entry, `Examples/Data/Stats/Player/RegularPlayerStats.asset` (GUID `00bcb4afab4c5b64b839b19378747f53`). Scene overrides affect pose, not the stats selection. The stats manager starts at entry zero.

The prefab has radius **0.5 m**, height **2 m**, centre **zero** (original root at the capsule centre), skin **0.01 m**, step offset **0.2 m**, slope limit **45°**, and collision mask **-5** (DefaultRaycastLayers). `rotateToGround` is false, `rotateToPlatforms` true, and `slideOffLedges` is absent so its source initializer is false.

Saphy is a different baseline: `TestLevelScene 4.unity` instantiates `Saphy.prefab` with `FastPlayerStats`, radius 0.3 m, height 1.6 m, and ground rotation enabled. It is not silently substituted for Lily. Its core tuning is retained in `PootleFastReference` for comparison; it is not a promise to reproduce Saphy's excluded abilities.

Effective Regular values: walk/run 7.5/9 m/s, minimum analogue speed 2 m/s, ground/run acceleration 16/20 m/s², release friction 28 m/s², turning drag 30/14, air acceleration 32, air turning drag **60 from the source initializer**, reversal deceleration 30, minimum reversal speed **5 from the source initializer**, unnormalised brake threshold -0.8, gravity 49/65 m/s², terminal falling speed 50 m/s, jump launch/release 19/10 m/s, coyote/buffer 0.15 s, ground snap 15 m/s, facing 970°/s, one total jump, and slope speed disabled. Original names `maxJumpHeight`/`minJumpHeight` actually mean velocities.

## Source mapping

| Reference under `Scripts/` | Pootle implementation |
| --- | --- |
| `Entity/Entity.cs`: Accelerate, Decelerate, ground/air velocity, walls | `PootleMovement`, `PootleCharacter` |
| `Entity/EntityController.cs`: sweeps, split movement, contacts, steps, penetration | `PootleMotor` |
| `Player/Player.cs`, Idle/Walk/Brake/Fall states | Two explicit modes and a grounded braking flag in `PootleCharacter` |
| `Player/PlayerStats.cs`, Regular/Fast assets | `PootleProfile`, destination Regular/Fast core profiles |
| `Player/PlayerInputManager.cs` | `PootleInput`, `PootleJumpBuffer`, `PootleControls` |
| `Player/PlayerCamera.cs` | Cinemachine OrbitalFollow, RotationComposer, InputAxisController, Deoccluder, `PootleOrbitInput` |
| `Player/PlayerAnimator.cs` | Optional `PootleAvatar`; named typed parameters rather than state-list positions |

## Intentional differences and corrections

- The motor is a focused extraction of the custom capsule-sweep approach, not a verbatim copy. It uses nearest non-self non-trigger hits, five bounded slide iterations, explicit step-up/forward/down clearance, and bounded penetration recovery. It omits GameTags, platform/crunch special cases, gravity fields, and the old gap heuristic. Contacts remain available without interactions.
- Pootle's root is at the feet with an explicit offset capsule centre. The visual child owns facing and optional slope tilt. World up remains fixed. `ComputePenetration` receives the root pose so the collider centre is not applied twice.
- Central-support rejection and edge slipping default on to reflect the recorded surface preference, although Lily's original ledge slipping defaults off. This is separate from capsule size or slope tilt.
- Keyboard diagonals are clamped; analogue magnitude survives camera projection, including a nearly vertical camera. Only the Input System stick deadzone is applied. The reference's additional cross deadzone is intentionally omitted to avoid stacking deadzones and losing small diagonal inputs.
- Reversal uses the source's unnormalised dot threshold, deceleration, and minimum entry speed, with an immediate flag instead of a frame-count delay. Run ignores analogue magnitude like the reference.
- Average velocity integrates displacement across a tick, including the jump-launch tick. This corrects the measured frame-rate variation in jump apex and stopping distance. The system remains one bounded Update tick, with no fixed-step rewrite or hidden substeps.
- A buffered press is consumed only when accepted. Early release before a buffered landing produces a short jump. Coyote eligibility cannot be reused after jumping. `additionalAirJumps` is explicit and starts at zero.
- Gravity and movement never depend on an Animator, camera package types, or old numbered states. Cinemachine references live in the camera integration and editor setup assembly. Odin is editor-only.
- Camera orbit is independent of character yaw, with pitch -15° to 70°, radius 8 m, FOV 55°, position damping 0.15/0.3/0.15, stick rate 150°/s, mouse 0.12°/pixel, and obstruction handling. Mouse is gated by right-button hold; mouse delta cancels the standard driver's time multiplication exactly once.

## Borrowed visuals

Initially Lily's FBX, Lily material, Lily texture, and its Lit Rim Shader Graph were copied into `Assets/Pootle/Samples/Art/Lily/`, preserving their GUIDs inside the separate destination project. The gameplay Lily prefab, animation controller, effects and hitboxes were not copied. The later requested gliding pass also copies the glider FBX/material/texture and open/close/spring audio, preserving GUIDs. The source asset is Platformer Project **1.12.7**. Keep these assets within the user's applicable asset licence.

`Art Source/Pootle Pooch test model.blend` was inspected before the user requested Lily, but was not exported, modified, copied into Unity, or used by the implementation. The later user instruction controls the delivered avatar.

The supplied implementation plan is treated as a design reference. Its roadmap does not authorize expanding this delivery into acrobatics, game systems, or traversal. No supplementary Scripts.zip/Data.zip snapshot was present in the workspace; the working project is authoritative.

The original reference project's Assets, Packages and ProjectSettings are unchanged. Side-by-side live gameplay measurement of the original remains separate from source-level comparison; see `Verification.md` for what actually ran.

## API references checked

The installed package source is the authority for the target API. Additional primary references: [Cinemachine 6.6 Orbital Follow](https://docs.unity.cn/Packages/com.unity.cinemachine%406.6/manual/CinemachineOrbitalFollow.html), [Cinemachine input integration source](https://github.com/Unity-Technologies/com.unity.cinemachine/blob/main/com.unity.cinemachine/Runtime/Helpers/CinemachineInputAxisController.cs), and [Unity Editor scene-management source](https://github.com/Unity-Technologies/UnityCsReference/blob/master/Editor/Mono/EditorSceneManager.bindings.cs).

## Destination change set

All new character code, profiles, input actions, sample art/prefab/scene, editor tools, tests and notes live under `Assets/Pootle/`. Outside that folder, the changes are the Cinemachine and Splines package manifest/lock, enabled playground scene in build settings, `.gitignore` rules for Pootle GUIDs and generated local Odin configuration, and Odin’s automatically enabled Unity.Mathematics editor drawer module/configuration. Unity regenerates that small editor integration when its dependency is available; it has no Pootle runtime role. Local macOS builds and preview images live in the ignored `Builds/Pootle-v0.1/` folder. No commits were created.

## Character feedback and traversal pass · 4 November 2026

The user's follow-up expands the initial scope to basic animation, wall/cylinder climbing, ledge catches/top-out, straight ropes/poles and seated steep-slope sliding. These are explicit selected modes rather than the original game's full state framework. Lily remains the only sample avatar. Existing reference animation clips were already embedded in the copied Lily FBX; `PootleLily.controller` selects idle/walk/run, jump/fall, climb up/idle/down, ledge hanging/climbing and crouch as the base for a procedural seated pose. No reference runtime controller, gameplay scripts or project settings were copied.

Intentional control changes: run is LB/L1 (left shoulder), grip LT/L2 or E, WASD/Shift/Space plus independently gated RMB mouse look; default camera pitch inverted. Poles retain facing and allow in-place turning; their jump direction is facing, never a forced radial outward vector. Seated slide facing snaps to four directions relative to downhill as the user selected. Slide tilt and coasting friction are profile settings.

The motor now resolves slope-tangent displacement once, filters tangential/separating sweep contacts, validates central support with actual capsule-to-plane clearance, and samples step tops in the riser's normal direction. A nearby rounded sphere contact cannot override a physically valid central face normal or trigger a seated slide on stairs. Top-out motion sweeps the capsule throughout and checks destination clearance. Static cylinder examples use mesh colliders with flat tops; straight pole/rope colliders are triggers so a jump can pass across them in the retained facing direction.

## Carrier, glider and spring additions · 4 November 2026

The user's follow-up authorizes moving platforms, spline hooks, gliding and launch springs. Reference `Platform`/`MovingPlatform` informed translation/rotation carry, speed and optional facing; `GlidingPlayerState` informed controlled descent and steering; `Spring` informed upward support activation, full-force launch and jump reset. Pootle adapts these behaviors without the reference entity/state/level framework. Unity Splines 2.9.1 is isolated in `Pootle.Splines`; the core motor depends on neither Splines nor the reference project.

The gliding pose uses the already-copied `Lily|Gliding` clip. A separate `PootleGliderVisual` handles deployment and sounds; `PootleGripPose` places hands on poles/hooks without root motion. Pole attachment is relative to retained facing, rail landing uses capsule-radius assistance, and step height is now 0.6 m for side entry onto the stairs. Seated slope entry removes 90% of lateral velocity and damps the remainder. Hook facing is omnidirectional and camera-relative, as explicitly requested.

## Rail, pole and contact feedback · 4 November 2026

This pass implements the user's explicit follow-up: contact-gated slope sitting, four-way stationary rail facing, intentional sharp-corner and T-junction selection, lateral rail landing assistance, neutral vertical rail jumps, automatic climbing/handle attachment with B/Circle/Q detach, short fixed-point pole tops, open/locked pole endpoints and facing ranges, narrow-wall rail mounting, ground pound and an extruded spline helix. No additional reference assets were copied. The optional Splines adapter samples the installed Unity spline and uses Unity `SplineExtrude` for visualization. Runtime graph junction selection uses nearby coincident rail segments; it does not introduce a level/game framework. Narrow-gap recovery retains verified capsule-clear positions and only intervenes after sustained trapping.

## Double jumps, rolling, carrying and camera styles · 4 November 2026

The requested expansion enables one additional air jump and tuned ordinary slope acceleration/landing momentum in the default profile. Rolling is a persistent toggle with its own spherical capsule and slope gravity. Reference `RollingPlayerState` informed momentum/friction and ground/air handling, but input drives requested velocity without importing crouch or charged-roll states. A blocked standing capsule leaves ball mode active.

Reference `PlayerObjectGrabber`/`Pickable` informed the optional carryable-prop contract. Pootle uses BoxCollider/Rigidbody props with validated volume and transfer paths, deterministic front/beneath placement, safe fallback and restoration of original physics settings. A procedural ball and sample cubes were authored locally; Lily's existing humanoid grip pose supplies carrying hands. No new reference assets or game systems were copied.

Six camera styles use the existing Cinemachine orbit/composition/obstruction pipeline and privately owned Input System actions on number-row keys 1–6. The reference project remains unchanged.
