# Rainbow Fairy Grove

Open `Assets/Pootle/Samples/FairyOneButton/Pootle_FairyOneButton.unity` and press Play. The scene works directly from disk; no builder or other scene is required.

WASD, arrows, D-pad or joystick stick move. Space, South or joystick trigger is the single action. Stop briefly, tap twice to fly, then tap in flight for two flaps. Talk/Press acts on the highlighted target. B, gamepad East or Esc closes manual dialogue. Holds never repeat and never back out.

## Branching seed trail

The mint branch offers broader, gentler steps; the pink branch takes higher hops. Both lead to the golden flower at 2.9 m. Follow 18 golden seeds and fly through five hoops. A seed collects on contact; a hoop only counts when the fairy crosses through its opening. Cleared hoops sprout gold petals. Collect everything to bloom the finish flower.

Five low recovery leaves (0.3–1.0 m) help rejoin the elevated routes. Two wide side shelves sit 0.7 m below the meadow and catch overshoots; both allow a normal launch back onto the island. Automatic fall recovery retains collected progress.

Five signposts reveal one short hint when nearby and hide it when you leave. No interaction is required. NPC dialogue takes display priority, and signs never own the flight button.

**Reset the whole course:** click **Reset [R]**, press **R**, or stop beside the mint reset acorn near spawn and use its one-button **Reset** prompt. Reset restores all seeds and hoops, closes the mushroom gate, returns the fairy home, restores flap charges and clears dialogue, wind-up and velocity. The UI releases selection afterward so Space remains available for flying.

`FairyCourse` checks swept movement after the existing Pootle tick; it never moves the fairy itself. Teleports reset the sweep origin so a respawn cannot collect everything along its path. `FairyCollectible`, `FairyHoop` and `FairySign` remain inspectable scene objects. The full builder includes the course; `Pootle > Samples > Add Fairy Branching Course` upgrades an existing saved demo without rebuilding its original objects.

**Extension verification, 6 November 2026:** 19 course-state checks, nine consecutive motor-driven branch landings, two catch-shelf return hops and five live reset-input checks passed, followed by the original movement/input regressions. Branch traversal used digital directions and normal launch/flap inputs, with no teleports between route landings. The saved scene was reopened before the final checks. No physical joystick operation was performed. All 97 checks passed; the final Play Mode console reported zero errors and warnings. Detailed results are in `CoursePlaytestResults.txt`.

Visual fix pass: turned the first pink hoop away from its nearly edge-on angle; kept the old NPC/button approaches clear; widened the fixed shot to orthographic size 9.0 so both catch shelves remain visible. The established palette, lighting and minimal UI treatment remain in use. Reviewed the full course, nearby-sign UI and completed-course frame; palette/style/UI 4/5 and lighting/silhouette/feedback 3/5.

## Assets and reuse

- `Rainbow Fairy.prefab`: original primitive fairy, inspectable before Play; one 0.82 m / 0.22 m gameplay capsule, unit root scale, no visual child colliders.
- `Fairy Movement.asset`, `Fairy Flight.asset`: separate walking/gravity and flight/action tuning.
- `FairyControls.inputactions`: isolated runtime-cloned map. The controller inspector offers **Rebind joystick fire button** in Play Mode; overrides last for that session. Direction axes are bound to `<Joystick>/stick`.
- `Generated/`: reusable vertex-coloured primitive meshes and one shared Pootle vertex-colour material. No downloaded artwork or new packages.
- `Editor/FairyGroveBuilder.cs`: explicit `Pootle > Samples > Build Rainbow Fairy Grove` authoring action. It preserves loaded scenes, temporarily hides their active roots, and saves the new scene additively. Use **Restore scene before Fairy preview** to restore them. Close an existing fairy scene before rebuilding.

`PootleCharacter` remains the sole velocity owner and calls the existing motor once per authoritative movement tick. The optional `IPootleMovementPolicy` supplies action impulses and air steering; an empty policy preserves the standard jump buffer/coyote path. The fairy reuses camera-relative directions, walking/braking, support probes, steps, collision contacts and ceiling handling. The existing motor diagnostic snapshot still works. Select the fairy for the compact action/focus/timer/support/speed/flap inspector.

## Tuning

Ground 3.2 m/s; air 4.2 m/s; air acceleration/braking 16/24 m/s²; launch 4.8 m/s; rising/falling gravity 8/6.5 m/s²; descent cap 4.5 m/s; two flaps, boost 3.1 m/s, cap 5.2 m/s, minimum added lift 0.5 m/s. Settle 0.12 s below 0.20 m/s; double tap 0.55 s; wind-up radius 0.16 m. Capsule step height 0.18 m. Focus acquisition/release 1.30/1.65 m; manual conversation radius 2.10 m. Fixed camera orthographic size 9.0, no camera input.

The approximately 16 × 14 m floating garden contains a launch ring, 0.8 m and 1.7 m flower platforms, a sideways landing, a 1.1 m walk-off platform, low arch, adjacent chatty NPC and mushroom plunger, and a separate two-page storyteller. The plunger raises a coloured gate. Falling below −5 m returns to spawn.

## Playtest — 6 November 2026

**Actually run in Unity 6000.6.3f1 Play Mode:** reopened the saved scene from disk, confirmed advancing player-loop frames, ran 37 deterministic motor/action checks and 25 checks using virtual keyboard/gamepad events through the actual Input System map. All 62 passed. Full check names are in `PlaytestResults.txt`.

Covered held buttons; valid/late/interrupted tap pairs; presses during settling; disappearing targets; targets entering an armed wind-up; wall pressure; air reversal/braking; flap charge limits; touchdown press ownership; chatter reaction; both dialogue pages; walk-away/B/Esc exit; plunger versus automatic contact; ceiling contact; ledge walk-off; respawn. Eight WASD directions, neutral, D-pad and South were exercised through device events.

| Simulated tick rate | Takeoff apex | Airtime after launch helper | Air stopping distance |
| --- | --- | --- | --- |
| 30 fps | 1.435 m | 1.200 s | 0.370 m |
| 60 fps | 1.435 m | 1.233 s | 0.368 m |
| 120 fps | 1.435 m | 1.242 s | 0.368 m |

These are fixed-delta simulation comparisons inside Play Mode, not hardware performance benchmarks. The airtime helper includes a release tick before timing; support is resolved on frame boundaries.

**Baseline smoke test actually run:** loaded `PootlePlayground` in Play Mode, injected gamepad movement/camera input, observed Lily moving and camera yaw changing, then exercised its standard jump (18.18 m/s immediately after gravity integration). Its movement policy remained empty; buffer/coyote settings remained 0.15/0.15 s. This was a smoke test, not the entire existing test suite.

**Device limitation:** the connected Atari CX Wireless Controller was inspected and is recognised as a `Joystick` exposing `stick` and `trigger`, matching this sample's defaults. No physical joystick was operated. Virtual device checks do not substitute for a hands-on comfort test or a human full-route playthrough.

**Visual/feel observations:** the first draft's rounded soil protruded through the meadow and its ledge overlapped the low arch; both were corrected. The wide fixed shot makes the 0.85 m fairy small, so the wing silhouette, ground shadow, nearby charge pips and targeted prompts carry readability. Fast repeated flaps near the cap can preserve a charge; the soft cue distinguishes this from an accepted beat. Dialogue lines are deliberately short. Sound is synthesised action/landing chimes; this demo has no music, ambient bed, title screen, pause shell or post-processing volume.

Compilation finished with zero errors. The final Play Mode console reported zero current errors and warnings. Earlier log history contained unrelated CoastalHike/Odin inspector errors, predating this work. Unsaved original scene contents were retained and restored after verification.

## Visual review

Style: rounded garden toy diorama, pale wings, apple-green petal outfit and golden hair. Palette: meadow `#8DDC62`, foliage/shadow `#286957`, cream `#FFF3CD`, coral `#FF8EBD`, mint `#BAF6E7`, lavender `#B5A1F0`, gold accent `#FFD45F`. Warm afternoon key with cool ambient fill. All visible garden geometry uses coloured vertices.

Final review of the fixed gameplay view, fairy close-up and airborne feedback frame:

- palette discipline: 4/5 — shared palette throughout geometry and UI; no default materials.
- style uniformity: 4/5 — rounded primitives across fairy, NPCs, flowers and scenery.
- lighting depth: 3/5 — warm/cool separation and readable shadows; intentionally simple lighting.
- silhouette: 3/5 — topknot and four wings read; face detail needs the close-up.
- UI intentionality: 4/5 — one controls line, contextual pill/bubble, two flight pips.
- feedback visibility: 3/5 — squash, wing beats, lift sparkles, pips and chimes; subtle at the wide view.

Fix pass: corrected soil/arch intersections, tightened framing, reduced action-pill size, added gate guide posts. The initial framing/intersections rated silhouette and lighting readability at 2/5; the final frames meet the 3/5 floor. Game shell and broader live-service/save systems were excluded to keep this a directly playable minimal-UI demo. Existing Pootle shading was retained.

Guidance used: Unity CLI and its Play Mode verification loop; craft references for art direction, lighting, camera rigs, placeholder assets, character motion, game feel, VFX, audio, game shell and visual scorecard; uGUI guidance for the small camera-space HUD. Engine API references consulted: [Mesh.colors](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Mesh-colors.html), [AudioClip.Create](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/AudioClip.Create.html).

To repeat the deterministic checks with this scene playing, evaluate `Pootle.FairyDemo.Editor.FairyPlayChecks.Run()`. For live input checks, add the editor-only `FairyDeviceChecks` component to a temporary GameObject in Play Mode and inspect its `Result` after about 15 seconds. Neither helper is present in the saved scene or player build.
