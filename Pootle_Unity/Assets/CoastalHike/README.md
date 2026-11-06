# Coastal Hike — automatic exploration camera prototype

## Mountain skywalk and summit hut

[86-second feature playthrough](https://docky.net/builds/pootle-coastal-hike-bridges-hut-path-assist-2026-11-06.mp4) — view tilt, digital path assistance, mountain crossings and the hut interior.

The summit lookout now leads into a six-bridge timber loop across all five mountain tops, reaching Y=37 before returning to the lookout. Plank decks, rope handrails, open summit junctions and coral flags mark the walkable route. Each crossing and landing has an exploration camera region; jumping is optional. The two loop entrances are near **(2, 15.16, 66)** and **(5, 15.16, 65)**.

The wooden hut stands beside the summit path, with its black entrance at **(-3, 15.16, 58)**. Walk into the doorway to enter its furnished cutaway interior via the existing circle transition. Walk back through the interior doorway to return to the porch. The interior belongs to the existing +100Y network and shares its lighting switch, arrival buffers and camera policy. It is a separate room, not an additional branch of the three-way cave.

Objects live under **07 · Mountain skywalk and summit hut** and **Cave Network · hidden +100Y / Summit hut · interior**. `CoastalHighRoutesSetup.Build()` is an explicit additive authoring pass that refuses to duplicate the addition. `CoastalHighRoutesPlayModeTests` verifies both directions of each bridge and the full hut entry/exit sequence.

Open **CoastalHike.unity** and press Play. The existing Pootle/Lily controller is retained. The project already uses Cinemachine **6.6** (the Cinemachine 3 API family) and Splines 2.9; no packages were replaced.

## Controls and test areas

`PootleControls` binds the Atari CX stick and trigger directly to Character/Move and Character/Jump, with joystick fallback bindings. WASD, arrow keys, D-pad and analog sticks use the same movement smoothing: 0.12s acceleration, 0.08s release and 0.12s full reversal. Analog target direction defaults to 16 sectors (22.5°) with 3° hysteresis past each boundary; magnitude is preserved. Tune these in **Movement Smoothing** on `PootleInput`; zero direction steps disables analog quantization. Jump remains an immediate button action.

- **WASD / left stick:** camera-relative movement. **Space / south face button:** jump. **F / right shoulder:** glide. **Shift / east face button:** run. **R / Select:** reset. Approach the marked walls to climb automatically; up/down and left/right traverse the wall. No camera-control input is connected.
- **F8:** show/hide the development camera overlay. It is absent from non-development builds; the component can also be disabled.
- **Curved beach:** the default beach region uses a spline rail. Select `ZONE · Beach fixed · preview for A-B` and use its Odin **Preview This Shot** button to compare fixed-angle follow at the same location. Use **Return to automatic regions** on the director afterward.
- **West courtyard:** three climbable walls surround a tucked-away room. Coral marks identify the back-wall climb to a small reward ledge. Its camera moves only within authored bounds.
- **Hedge garden:** six-metre hedges, a south entrance and east exit. A 72° camera reveals the passages. Walk around the inner spine; jumping is not required.
- **East cliff loop:** walk the coastal ramp, round the rock in either direction, then return to the sun terrace. Alternatively climb the marked eight-metre wall onto the bridge. The upper bridge and lower walkway overlap at **X=35, Z=36**, with feet heights **10** and **2**.
- **Bridge / glide:** jump or glide from the upper bridge toward the sun terrace. Yaw-changing handovers wait for landing; same-yaw height changes are allowed.
- **Static viewpoints:** the jetty beach section and summit use genuinely stationary cameras. Move across the frame to compare with follow mode.

## Components and setup

`PootleCameraFocus` follows the player Transform with independent horizontal/vertical smoothing, capped velocity lookahead and climbing headroom. It does not inherit player rotation. Teleports snap it and reset shot histories.

Each `PootleExplorationShot` lives on an independent `CinemachineCamera` and selects one mode:

1. **FixedAngleFollow:** Cinemachine Position Composer translates the camera at authored world pitch/yaw. Tune distance, damping, screen position, dead zone and outer/soft limits on the shot.
2. **RailFollow:** project the focus onto a ground-guide spline (or world-space line endpoints), smooth its parameter, then position the camera on that rail plus `railOffset`. Parameter limits are 0–1. The player is never moved or constrained. Choose **Authored** rotation or **LookAtFocus**; authored is the default.
3. **StaticOrBounded:** use a world-space `anchor`; optionally track displacement from `focusOrigin` inside `positionBounds`. Zero-sized axes stay fixed. Rotation remains authored.

The normal starting profile is pitch 55°, FOV 30°, shot damping XZ 0.35 / Y 0.8 seconds, shared-focus damping XZ 0.3 / Y 0.8, and lookahead 0.16 seconds. The hedge and composed viewpoints have explicit exceptions. `orthographic` and `orthographicSize` provide an alternate profile; lens-mode overrides are enabled on the Brain. Settings apply during Play; **Apply shot settings** is the explicit edit-time button. Copy Play-mode tuning back before stopping if you want to retain it.

## Author a box region or rail

1. Duplicate a shot and set its mode/composition. Assign the shared focus.
2. Create a GameObject with a **BoxCollider**, mark it **Is Trigger**, and add `PootleCameraZone`. Set the collider's centre/size, including Y; assign the camera and precedence. The box can be rotated/scaled.
3. Add the region to the director's `zones` array. Add a child Transform at a safe **feet position** and assign `playerArrival` for testing. No Rigidbody is needed: the director evaluates the actual 3D box, including on spawn and teleport.
4. For a spline rail, create/duplicate a `SplineContainer`, author an **open** guide through the traversable area, and assign it to the shot. Edit knots with Unity's spline tools. `railOffset` places the camera above/behind the guide. With no spline, `lineStart`/`lineEnd` are world coordinates.

Region gizmos show volume, priority, selected state, and the expanded exit threshold when selected. Shot gizmos show the camera rail, closest projected point, forward direction, and static/bounded position limits. Region **Move Player** teleports to the authored arrival. **Test Handover** previews fallback → this shot; it deliberately stays in preview until **Return to automatic regions** is pressed.

## Selection and movement rules

Highest region precedence wins. Equal precedence uses lower `tieBreakOrder`, then the stable director-array order. The active region retains its horizontal exit padding (0.75 by default) and separate small vertical padding (0.15). A minimum dwell of 0.65 seconds prevents rapid reversals. Disabled regions/cameras are released immediately. When no region matches, the default fallback is selected.

The Brain uses Ease In Out over 1.2 seconds; individual regions can override blend duration and dwell. Optional yaw deferral retains the current shot while the character is airborne/gliding and the candidate's authored yaw differs by more than 12°. Both travel directions use the same rules. Landing re-evaluates the current position, not an old queued candidate.

`PootleMovementBasis` is **opt-in on the rendered camera**. It clones only the existing Move action. Before a significant yaw handover, the director freezes the current horizontal movement basis if input is held. This persists until release. Fresh input uses the visible camera basis; if a blend is still underway, that newly chosen basis stays stable until the next release. Gliding calls the same helper. Disable the component or `preserveHeldInput` to compare strategies.

The existing controller integration is only a two-line optional lookup in `PootleMovement.CameraDirection`. With no enabled adapter it follows its original path. Other controllers can use `basis.Forward` and `Vector3.Cross(Vector3.up, basis.Forward)` without changing their motion implementation. Neither camera code nor the adapter rotates the character.

## Tests and trade-offs

Editor fixtures: `PootleCameraZoneTests` and `PootleExplorationCameraTests`. Scene Play-mode fixture: `CoastalExplorationPlayModeTests`. All 15 checks passed (10 Editor, 5 Play mode). These cover priority/ties, vertical overlap, hysteresis/dwell, airborne handovers, real Brain blends with held input, neutral/re-press, no right-stick orbit, rail endpoints, static/bounded shots, both wall climbs and the coastal loop.

This is an authored prototype: no automatic occlusion solver or foreground fade is installed. Keep cameras and their blends outside walls; inspect all entry directions. Closed/self-crossing splines are intentionally unsupported as reliable projection routes; sampled nearest-point projection can jump between nearby branches. Long flights can retain an old yaw until landing; disable airborne deferral to compare, or author compatible landing shots. LookAtFocus rail rotation is an optional comparison, not the default; the basis lock protects notified handovers, not arbitrary continuous look-at motion. Focus and composer damping are cumulative and should be tuned together.

The builder creates the original base scene; the explicit **Add Exploration Camera Prototype** menu appends the demo once. It refuses to rerun over the authored extension. Edit ordinary objects thereafter. `CoastalHikeBuilder.Build(true)` replaces the base scene and removes extensions, so use a separate copy before rebuilding. New camera scripts use no ExecuteAlways, ExecuteInEditMode or OnValidate. Existing third-party procedural cones retain their own mesh-generation lifecycle.

Reference basis: authored area cameras and Cinemachine blending, as described in the supplied spec. The spline is a prototype technique, not a claim about A Short Hike's implementation. API details were checked against the installed package source and the [Position Composer documentation](https://docs.unity3d.com/Packages/com.unity.cinemachine@6.6/manual/CinemachinePositionComposer.html) and [SplineContainer documentation](https://docs.unity3d.com/Packages/com.unity.splines@2.9/api/UnityEngine.Splines.SplineContainer.html).

## Secret cave network

The `Cave Network · hidden +100Y` root is at **(0, 100, 0)**. Its children use island layout coordinates: the chamber floor is local Y=3 / world Y=103. All interior floors, walls, doorway thresholds, destination markers, lights, shots and BoxCollider camera zones move with this one root. Use its Odin **Inspect alignment** button (or set its position to zero) to compare the network with the island; **Restore hidden location** returns it to Y=100. Restore it before playing. The exterior entrances and screen overlay remain under `06 · Secret cave entrances`.

Three paired black archways connect the room to different terrace faces:

- **South lantern gate:** (9, 3, 19), approached from the pine meadow via the new short landing.
- **West echo gate:** (-8.2, 3, 34), reached from the western meadow spur.
- **East mineral gate:** (24, 3, 26), reached from the lower coastal route.
- **Open plateau drop:** (12, 7, 36), above the chamber. Stepping into the circular opening drops the player into the room at Y=103; leave via any tunnel.

`PootleCaveTravel` supplies temporary scripted input to the existing character's `Tick` method. It closes the circle while moving toward the opening, holds a fully black frame, teleports to the destination marker, resets the Cinemachine Brain so the camera cuts, opens the circle, and walks clear (or falls to the room floor). It restores the original simulation and camera-relative control references afterward, including when cancelled or disabled. Close, hold, open and walk-clear timings and walking speed are inspector values. No right-stick controls are added.

Each `PootleCavePortal` samples its **BoxCollider** in 3D, so it works with this controller without requiring Rigidbody trigger callbacks. `destination`, `arrivalWalkEnd` and `approachEnd` are editable transforms. The drop entrance uses a circular XZ test inside its box. Every transition disarms portals; a portal rearms only after its delay **and** after the player leaves the expanded buffer volume. Orange gizmos show that buffer. The automatic arrival walk ends beyond the threshold, preventing an immediate return teleport. `Test entry` is a Play-mode Odin button.

`PootleCircleIris` uses a non-interactive uGUI overlay with an aspect-correct circle centred on the character. The shader is referenced by the saved material. The interior uses an authored cutaway view, dark backdrop, mineral landmarks and amber guide lights; roofs are deliberately omitted to keep all three paths readable. Doorways conceal short teleport seams rather than cutting full tunnels through the outdoor terrain. The plateau drop **does** cut the visible mesh and collider; the sun terrace body and crown are baked versions of their procedural cones so the third-party cone Awake cannot fill the aperture on scene reload. Other plateaus remain procedural.

`CoastalCaveSetup.Build()` / **Pootle > Coastal Hike > Add Secret Cave Network** is an explicit, additive, one-time authoring pass and refuses to duplicate an existing network. Edit the saved scene afterward. `CoastalCavePlayModeTests` exercises all three door pairs, room paths, real aperture/fall, black-frame camera cuts, no automatic bounce, offset alignment and cancellation cleanup.

## Hidden fern room and cave light

A fourth, unmarked entrance is concealed by a passable bush at **(26.4, 3, 38)**, under the upper bridge. Approach along the new short spur from the lower walkway near **(35, 2, 38)**. There is no exposed arch or sign: walking into the bush uses the same circle transition and arrival buffers as the other cave doors. Foliage also appears inside the mouth. The small room is disconnected from the three-door cave: solid walls and a dark gap separate them. Its only entrance is the exterior bush door. The cutaway camera provides a glimpse of it from the main cave as a clue; both interiors belong to the same Y=100 layout root. Cutaway perimeter walls retain full-height collision to prevent jump/glide shortcuts.

The room has its own BoxCollider camera region. A low exterior approach shot looks beneath the bridge so it cannot hide the character and bush. Upper bridge cameras remain separate by height.

`PootleCaveLighting` on the network detects the player's position in a root-relative interior bounds volume. Inside, it disables the outdoor sun, applies a dim cool ambient probe, and enables the warm point light attached to the avatar. Exiting or disabling the component restores the previous sun, ambient mode/colours/probe and reflection intensity. This is a single-player prototype lighting switch; it is not intended for simultaneous split-screen interior and exterior views.

Tune **caveAmbient**, **avatarIntensity**, **avatarRange**, and **avatarColour** on that component. Current avatar light: intensity **3.5**, range **7.5**, local height **2.8**. Wall guide lights use intensity **0.7** and range **4**. The raised avatar light keeps the head readable without a blown-out highlight. Existing daylight materials and post-processing are retained.

A cool, shadowless point light (`Tunnel · dim ambient fill`, intensity **1.3**, range **15**) softly fills the main chamber and nearby tunnel. The separate room retains a faint warm clue light (intensity **1.5**, range **9**). Both are ordinary Inspector-tunable lights.


## View tilt and Edit Mode exploration preview

All 18 exploration shots use perspective **30° FOV**, including airborne travel. Camera distances were increased to retain approximate ground coverage with the narrower lens.

`PootleViewTilt` is on **Explorer · Lily / Visual / View tilt · visual only**. **Tilt Back Degrees** defaults to **25°** and is limited by camera elevation for low views. It leans away from the rendered camera, independent of the character's facing, revealing more of the standing silhouette. Only the dedicated visual pivot rotates; movement, colliders and animation remain on their original roots. For NPCs or props, create a foot/base pivot above their rendered model, add this component, and assign the game camera (or leave blank for MainCamera). Keep animated transforms and colliders outside this pivot. The rest pose is restored on disable. Preview/restore buttons are explicit, with no edit-time lifecycle execution.

Open **Pootle → Exploration → Surface Placement Preview** outside Play Mode:

- Enable preview and **Shift + left-click/drag** over a surface in Scene view. The nearest upward-facing solid surface is picked; triggers and player colliders are ignored. The player stays upright with a 0.03-unit feet offset. Layer filtering is available.
- Drag the placement handle or use **Snap current position** to cast from 2 units above to 12 below the chosen height. This local cast distinguishes stacked floors and the +100Y caves. If no surface is found, placement stays unchanged.
- Watch **Game view** for the correctly selected camera. Toggle **Blend zone changes** off to inspect harsh cuts, or leave it on for the configured duration with Ease In Out through Cinemachine Brain. Store A/B positions on opposite sides of a boundary, then alternate **Go to A/B**. The panel reports the selected region, blend progress and yaw change.
- Preview intentionally bypasses dwell/hysteresis and airborne deferral to expose exact spatial boundaries. It does not run the movement controller. Runtime steering and full flight behavior still need Play Mode testing.
- Closing, saving, reloading scripts or entering Play Mode stops preview and restores the original player, camera and cave lighting state. **Keep player here and stop preview** explicitly saves a new placement, with Undo support. Camera priorities are never modified by the preview.


## General camera policy, signs and View Tilt comparison

**Assets/CoastalHike/Exploration Camera Policy.asset** is shared by all 18 shots and the player's **View Tilt** component. Edit **Viewed Size Multiplier** (currently **1.3**), **Field Of View** (**30**), and **View Tilt Degrees** (**25**). Viewed size scales camera distance/rail offsets/static anchors inversely; 1.3 gives approximately 30% larger projected objects without scaling characters or changing collisions. Individual shots retain their authored base composition. Supporting NPCs/props can reference the same policy, or enable **Override Policy** on `PootleViewTilt` for their own angle.

**Pootle → Exploration → Camera Policy and Shot Settings** exposes the policy and shot composition directly, without needing to expand Cinemachine's component header. Policy changes update in Play Mode and while surface placement preview is active.

The player's visual pivot is named exactly **View Tilt**. **View Tilt.prefab** is a reusable avatar example with the policy assigned. Open **View Tilt Comparison.unity** to compare an upright reference against the policy-controlled tilt under the same 60-degree camera. The selected component draws upright/tilted axes and reports the applied angle. The effect is camera-relative lean away from the view, rather than a change to the player's physics orientation.

Four signposts use `PootleSignLabel`: **Show Distance = 4**, **Exit Distance = 6**, measured in 3D. Once revealed, a label stays visible through the gap between these radii, preventing boundary flicker. Edit the message, distances and overhead offset per sign. Labels use non-interactive, camera-facing TMP canvases and remain below the circle-transition overlay.

Door landings are raised 0.12 units above the underlying grass, with 0.04 separation from adjacent approach paths; tunnel floors sit 0.035 above the central floor where they overlap. These are geometry offsets, preserving ordinary collision behavior.

Crash investigation (6 November 2026): the supplied 13:06 crash is a native macOS `NSView addCursorRect:cursor:` / Unity `GUIRenderView resetCursorRects` exception during Inspector UI work. No managed game-script frame identifies its cause. The direct camera-policy window provides an alternate editing route; this is not a confirmed repair of the native Editor bug. No package or Editor upgrade was applied, and no crash logs were uploaded.

After restarting Unity, expanding the Cinemachine component succeeded in both Edit and Play Mode; expanding its Lens foldout also succeeded in Play Mode. The reported native crash was not reproduced in those checks.

## Experimental path input alignment

`PootlePathInputZone` is an oriented BoxCollider trigger whose blue local Z axis follows the path. Within the volume, movement within **12 degrees** of either direction along that axis aligns to the path. The threshold, priority, digital-only and grounded-only switches are editable per zone. Analog and airborne movement remain free by default; movement speed and jump are preserved. It aligns camera-relative input after conversion to world space and does not pull the avatar toward the centre of a path.

Two trial zones cover Meadow traverse (46.4°) and Lookout ascent (54.2°), under `07 · Mountain skywalk and summit hut`. Disable either component to compare unassisted movement. Keyboard, D-pad and the Atari CX stick share the digital source classification from movement smoothing.

The avatar prefab now places Lily and the glider beneath `Visual/View Tilt`; the motor and camera target stay outside that pivot. The base prefab uses zero tilt, while CoastalHike retains its authored tilt settings. `Has Visual Children` in the inspector helps catch an empty pivot.
