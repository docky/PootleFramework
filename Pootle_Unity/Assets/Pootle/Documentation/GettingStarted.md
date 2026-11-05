# Pootle v0.1

Lily has an optional `PootleBlobShadow`: 15 soft body/limb circles form a 256×256 render texture. A decal-style volume reconstructs the visible opaque surface from camera depth and projects the mask per pixel, including narrow/curved rails without colliders. This avoids triangulated gaps at steps and ledges. Near-vertical faces are rejected; projection stops above the feet to avoid self-shadowing and fades at its maximum depth. Ball mode uses one circle sized to the motor radius. All character renderers (including inactive/new ball parts) have regular cast shadows suppressed while enabled and restored when disabled. Opacity, projection width and maximum drop are configurable. URP Depth Texture is required; sample PC/Mobile pipeline assets enable it. Transparent receivers are not supported. The sample prefab includes the component; `Pootle > Add Body Blob Shadow` configures existing sample characters.

Hold **LB/L1** (or Left Shift) to run. The regular profile now runs at **12 m/s** versus **7.5 m/s** walking. Run also recognizes a button already held when input is enabled.

Lily's locomotion animation plays at **2× while jogging** and **3× while running**. `PootleAvatar` exposes both rates; only the Locomotion state's speed multiplier changes, leaving idle, jumps and traversal at their normal rate. Character shadows extend uphill along the supporting plane, so a sloped receiver is not cut off at the horizontal height of the feet.

The playground contains **20 floating collectibles: 12 coins, 4 stars and 4 hearts**, using models borrowed from the reference project. Touching one adds its Amount to its assigned Soap `IntVariable` and hides the item and shadow. The three counters are `Assets/Pootle/Data/Collectibles/Coins.asset`, `Stars.asset` and `Hearts.asset`; they start at zero and reset on scene unload, without PlayerPrefs persistence. There is no additional inventory or score manager. Prefabs live in `Assets/Pootle/Samples/Collectibles`. Coins cast rotating narrow rectangular blobs; stars/hearts cast circles. Flat-ground placements use transparent quads, and elevated/uneven placements use depth projection. **Pootle → Add Collectible Trail** recreates the sample's 20 placements and connects the character's locomotion rate parameter.

**Pootle → Add Turnstiles And One Way Floors** builds a new area west of the central playground: four four-arm spring gates and one with slot 3 omitted, preserving 90° spacing. Push against an arm to turn it; passing the halfway point advances the spring to the next quarter-turn. `TurnstileGateHinge` exposes signed rotation clicks, CW/CCW UnityEvents, spring, damping and angular speed. `TurnstileGateBuilder` supports local X/Y/Z axes, arm dimensions/materials and a missing arm slot; use its **Setup Or Rebuild** context menu outside Play mode after editing geometry settings. Force-target context commands allow manual actuation. The variants use 1.15–2.5 m arms and spring values 35–180. Arms are staggered vertically in 8 mm increments above the hub to avoid surface flicker.

Six mint **one-way floors** sit east of the playground and beside the gates. Jump up through them, land from above, jump again or walk off; ground pounds land too. `PootleOneWayPlatform` marks a BoxCollider's local top face. Pootle's motor filters sweeps, ground probes, clearance and penetration recovery so the underside and sides do not block the avatar. This is character-specific filtering; ordinary PhysX rigidbodies still treat the collider as solid. Disable the marker to restore an ordinary solid platform.

Props use `PootlePropBlobShadow`. Select a prop and choose **Pootle → Add Prop Blob Shadow to Selection** to connect both materials. The Inspector offers Circle, Ellipse, Rounded Rectangle and Custom Texture (alpha mask), with world-space size, softness, opacity, yaw following and maximum drop. Place Local Origin at the bottom of tall props. Circle uses Size X as its diameter; other shapes use both dimensions. Shapes are cached until edited, rather than repainted every frame. Regular cast shadows are optionally suppressed and restored when disabled.

Choose **Depth Projection** for rails, steps and uneven opaque surfaces; it uses the same depth shader as Lily and needs no receiving colliders. Choose **Transparent Ground Quad** for a simple floor: one downward ray positions and tilts a transparent plane, using Ground Layers, Maximum Ground Angle and Ground Offset. It ignores the prop's own colliders and triggers, and hides without a valid receiver. The quad does not wrap around edges or multiple height levels, so use depth projection in those cases. The sample's 11 poles and two hooks have circular blobs; the shuttle over the flat floor demonstrates the simple technique, while the obstruction hook and poles use depth projection.

Open `Assets/Pootle/Samples/PootlePlayground.unity` directly and press Play. The sample uses animated Lily, ordinary solid colliders, and a Cinemachine orbit camera. There is no bootstrap scene or game manager. The Pootle test model is not used.

| Action | Gamepad | Keyboard and mouse |
| --- | --- | --- |
| Move / analogue walk | Left stick | WASD |
| Orbit camera | Right stick | Hold right mouse button and move mouse; or arrow keys |
| Jump / short jump | South face button; release early for a shorter jump | Space |
| Run | Left shoulder (LB / L1) | Left Shift |
| Grab wall / cylinder / pole / rope / hook | Automatic on contact | Automatic on contact |
| Detach / ground pound in free air | B / Circle (east face button) | Q |
| Manual grip (when automatic grip is disabled in profile) | Hold LT / L2 | Hold E |
| Glide while descending | Hold right shoulder (RB / R1) | Hold F |
| Recenter camera behind facing | Right stick click | C |
| Reset to starting position | Select / View | R |

Camera pitch is inverted by default for both stick and mouse; toggle `PootleOrbitInput.invertY` to change it. Mouse delta has its own action, so a connected gamepad does not suppress RMB look.

The two sticks control movement and camera independently. Movement uses the rendered camera's horizontal orientation. The camera does not automatically follow the avatar's facing yaw. Stick drift filtering comes from the Input System's stick processor; Pootle does not add a second cross-shaped deadzone. Keyboard diagonals and stick magnitudes are clamped to one.

## Use in another scene

1. Add ordinary solid floor and obstacle colliders. Triggers are ignored by the movement motor.
2. Open **Pootle → Setup**. Select or create a character, then use **Create Or Connect Character**. Repeating it reuses the selected character and its camera rig.
3. Assign a movement profile, `PootleControls`, the **rendered output Camera**, a visual child, and a camera target. The helper connects Cinemachine when needed. `PootleCharacter.prefab` contains the reusable character; its output-camera reference is assigned per scene.
4. Keep the character root at unit world scale with identity rotation. Scale the visual child; orient the model beneath Visual if its forward axis needs correction. Pootle owns the Visual root rotation. The default root is at the feet; capsule centre is `(0, 1, 0)`, height 2 m, radius 0.5 m, step 0.6 m, slope limit 45°.
5. To replace Lily later, assign a model asset and choose **Replace Visual Model**. This edits a scene instance, preserves the source importer, disables root motion, fits visual height, and aligns the feet. **Fit Capsule To Visual Bounds** is a separate undoable operation. Review radius manually for models with outstretched arms.
6. Run **Validate Setup**. Fix missing references, an empty collision mask, root scale/rotation, or absent solid geometry before playing.

`PootleDefault` follows Lily's Regular profile. `PootleFastReference` preserves the core fast-profile values for comparison. Copy a profile before making a separate character feel. Profiles contain tuning only; live velocity and timers stay on the character. Additional air jumps, slope speed modulation, landing momentum conversion, and visual slope tilt are configurable. The defaults preserve the regular baseline except where documented in `SourceAudit.md`.

## Sliding and traversal

Making capsule contact with a surface steeper than the motor's slope limit enters a seated, unsteerable slide. Facing snaps to the closest forward/backward/sideways direction **relative to downhill**, then stays at that relative angle while the slope curves. Entry retains only 10% of sideways velocity and quickly damps the rest; downhill comes from the slope normal, with independent lateral-retention/damping tuning. Jump escapes the slide. Leaving contact switches to the airborne pose; a connected flat runout retains seated momentum. Ground pound onto a shallow slope also starts a downhill slide. Momentum carries the seated character onto flat ground and decays using `slideFlatDeceleration`; ordinary movement resumes after stopping. The movement profile exposes slide gravity, friction, maximum speed, minimum seated time and `seatedSlopeNormalBlend` (0 upright, 1 follows slope normal). The collision capsule stays upright; pose tilt changes the visual only.

Add `PootleClimbSurface` to a solid wall or an ancestor of its colliders. Approach it to attach automatically, then move up/down or sideways. B/Circle or Q detaches and briefly suppresses re-grabbing. Cylinders need a true curved collider with a flat top (the sample uses the cylinder mesh as a static MeshCollider, replacing Unity's rounded capsule). Moving up at the lip automatically lifts the capsule and mini-hops onto the top. An airborne descending character can catch an ordinary vertical ledge within hand reach without a marker. The catch automatically tops out when there is space; a blocked destination stays hanging. Move down to drop, or jump. Every part of the hop uses capsule sweeps and destination clearance. Its lift and landing phases ease to rest; jump/fall animation avoids the reference clip’s baked top-out displacement. The 0.6 m step height also permits side entry halfway up the sample stairs.

Add `PootlePole` to a straight pole or rope with a **trigger** collider. Author the marker at its centre with local up along the rope, unit scale, and set its length/radius in metres. Approach to attach automatically. Left-stick/W/S vertical input climbs, left-stick/A/D horizontal input turns facing in place. The body attaches behind its facing direction, tracks around the pole when turning, and uses procedural hand placement. Attachment preserves the incoming facing; jumping launches along that facing, including toward/across the pole, rather than forcing an outward jump. Press B/Circle or Q to drop. These are static straight ropes/poles; swinging ropes and moving-wall transport are later work.

The playground contains blue climb walls/cylinders and yellow grip poles/ropes. **Pootle → Upgrade Playground Traversal** adds these examples and animation to an older playground once, preserving the rest of the course. New characters created by the setup helper also receive the Lily controller. The reference project and Pootle test model remain untouched.

## Diagnostics

Select the character in the Scene view. **Pootle → Setup → Scene diagnostics** offers Essentials, Grounding, and Everything presets, marker size, labels, Freeze Snapshot, Resume Live, Clear History, and Focus Last Rejection. Freeze affects recorded diagnostics only; the simulation continues. Captured tick and age distinguish a frozen sample. Before Play, geometry is explicitly labelled Preview and performs no physics queries.

Green means accepted support, red means rejected support, amber means a collision hit, and grey means a miss or an explicitly skipped stage. The view shows feet, capsule centre and head, true capsule/sphere sweep start and end shapes, query distance and mask, ignored triggers, support normal, central support, movement, overlap corrections, input buffer and coyote timers. Everything also shows displacement and velocity with separate units. Captures use fixed buffers (64 probes and 32 transitions) and report overflow. Drawing never performs physics queries. Release players disable capture.

The core owns the Update tick: sample input, establish support, decide jump, accelerate/brake, integrate gravity, resolve movement, reconcile contacts and landing, update facing, publish state. `Tick(dt, frame)` supports explicit test input. `Teleport(position, rotation)` clears velocity, support, jump consumption, timers and diagnostics, and informs the camera of the warp.

`PootleAvatar` is an optional Animator adapter. It accepts correctly typed `Speed` (float), `VerticalSpeed` (float), `Grounded` (bool), and `Mode` (int); absent parameters are ignored. Animation never drives the motor. The included Lily controller has idle/walk/run, jump/fall, climb up/down/idle, ledge hang, gliding and seated slide states. Its clips come from the already-copied reference Lily FBX. A small procedural humanoid leg/hip pose adds seated sliding; disable the presentation components to use a static model.

## Scope

Included: ground and air locomotion, independent turning drag and forward acceleration, run, braking, variable jump, buffering, coyote time, configurable air jumps, capsule sweeps, overlap recovery, small steps, walkable/steep slopes, central support, walls/ceilings, camera orbit/recenter/obstruction, animated Lily, seated slope slides, marked-wall/cylinder climbing, ledge catches and automatic top-out mini hops, facing-directed pole/rope climbing and jumps, one-way rails, moving platforms, rotating dangle hooks, gliding, launch springs, profiles, setup tools, diagnostics and tests.

Later work: rolling and other selected abilities, pushing/bonks, climbing stamina, object handling, and swinging ropes. There are no combat, health, inventory, save, level progression, singleton or Splines dependencies in Pootle's core.

## Verification

Use Test Runner to run `Pootle.Tests.Editor` and `Pootle.Tests.PlayMode`. The playground must be in the enabled scene list for runtime scene-loading tests. `Verification.md` records actual outcomes and measurement limits. Compile-time inspection is not a runtime pass.

## One-way rails

`PootleRail` defines a static polyline using local-space **walking-surface** points. Give its visual no solid colliders (or trigger colliders). Jumping from below passes through; a descending foot crossing within `landingHalfWidth` plus a configurable fraction of capsule radius lands on the top. Walk toward an endpoint touching the floor to step onto a rail without jumping. During descent, configurable lateral magnetism helps align near misses without raising the character or pulling through walls. The sample includes a low corner rail, a gentle incline, and a steep rail with a flat runout. Use **Pootle → Add Rail Playground** to add these examples to an older sample.

Movement projects camera-relative stick/WASD input along the rail. Facing snaps to one of four directions. Point across the rail to stand sideways; point along it to walk. At sharp corners, steer explicitly onto the next section. Touching rails form junctions: pressing toward a branch within `railJunctionApproach` (default 0.9 m) walks to the intersection on the current rail, then selects that branch; continuing straight does not automatically turn a right-angle corner. Smooth curves and slope changes remain continuous. Jump uses requested movement or existing movement momentum. A stationary neutral jump is vertical, with no added forward kick. Falling onto an incline transfers horizontal momentum only; fall speed cannot kick the character along the rail. Walking off an end falls normally and retains coyote jumping.

Above the motor slope limit, rail movement becomes a seated gravity-driven slide, preserving a cardinal facing relative to each section. Slide gravity, maximum speed, friction, flat deceleration and seated tilt share the slope profile settings. Momentum continues across connected sections and off an endpoint; a flat section slows the slide until ordinary walking resumes. External solid obstacles still block the capsule. Rails can be authored as polylines or with `PootleSplineRail`, which evaluates the actual Unity spline position and tangent, using a distance table to maintain speed between samples. Adjacent spline samples never act as junctions. The helix uses Unity `SplineExtrude` for its non-solid visual. Spline edits rebuild the walking path; moving rails remain outside this pass.

## Moving platforms and hooks

`PootleMovingSurface` on a solid platform carries the grounded character using translation and rotation around a local anchor. Jumps inherit horizontal and upward platform velocity; descending supports cannot cancel the upward jump. Capsule sweeps and overlap recovery remain active; a blocked character does not teleport through geometry. The playground has a travelling platform and a vertical lift.

`PootleDangleHook` has an authored grip point, reach radius, hanging height/body offset and obstruction tolerance. The offset rotates with facing to keep Lily’s hands at the ring and her head clear. Move within reach to attach automatically and remain attached. Move the left stick/WASD in any camera-relative direction to turn; the hook rotates with Lily. Jump launches vertically with neutral input, or in the camera-relative direction currently held. Hook travel no longer adds a horizontal kick; dropping still retains carrier momentum. Press B/Circle or Q to drop. If collision prevents following beyond the distance/grace-time threshold, attachment releases automatically. The playground includes a shuttle and a hook that passes through an obstruction for testing.

Add `PootleSplineMover` from the optional `Pootle.Splines` integration to either object and assign a Unity `SplineContainer`. Speed is metres/second, open paths can ping-pong, and closed paths loop. Optional path-facing rotates platforms; leave it off for hooks whose facing the player controls. Rebuild the mover after editing a path at runtime. **Pootle → Add Moving Platforms and Hooks** adds the sample objects.

## Gliding and springs

Hold RB/R1 or F while descending to deploy Lily's glider. The left stick/WASD steers relative to the camera, while the right stick/RMB controls the camera independently. Default descent is capped at 2 m/s, forward cruise is 3 m/s and full-stick speed is 9 m/s. Releasing the button resumes falling; landing, gripping or catching a rail closes the glider. Deploying does not add height. There is no stamina system in this pass.

`PootleSpring` launches a descending or grounded character supported on its solid pad. Launch speed, local direction, retained lateral momentum, cooldown, sound and launch event are configurable. It resets air jumps and does not require holding jump for full height. Side contact and upward passage do not launch. The sample has vertical and angled springs; **Pootle → Add Glider and Springs** upgrades the prefab and playground. Glider art, pose and short audio cues come from the reference project.

## Pole endpoints, rail junctions and recovery

Reaching an open pole top mini-hops to `PoleStanding`: the feet stay at one point until a jump. The stick changes facing without walking off. Pole-top jumps launch vertically, without a facing-based push; normal air steering can then move toward the next post. Climbing jumps still launch in the retained facing direction. Short posts can disable `climbable` while retaining `standOnTop`; descending onto them uses one-way landing. The playground includes a row of five short standing posts. Descending past an elevated pole's open bottom drops into free fall. `allowExitTop` and `allowExitBottom` lock the corresponding end; the sample displays a sphere at each locked end.

`limitFacing`, `minimumYaw` and `maximumYaw` constrain pole facing relative to the pole's forward direction. Set both limits to zero for the wall-facing ladder example. Endpoint locks prevent traversal across the end; jumping still detaches. Author the pole root at unit scale and keep stop-marker spheres non-solid. The `topBlockMarker`/`bottomBlockMarker` references toggle with the flags.

**Pootle → Add Junctions Poles and Helix** adds the T-junction, rails on the original narrow walls plus a new wall-top rail, short-post row, elevated/open pole, capped pole, limited-facing pole, ladder and two-turn helix. A climbable narrow wall samples several top depths; completing the mini-hop mounts a nearby rail automatically. Ground pounding onto a spline rail or shallow slope starts a gravity-driven seated slide.

`PootleRecovery` is a last resort for sustained unresolved penetration or blocked input between opposing walls. After the configured delay it restores the most recent clear grounded position, or searches nearby clear capsule positions. It does not activate from an ordinary single-wall block. `RecoveryCount` exposes when it intervenes; R/View reset remains available.

## Double jump, slopes, rolling and carrying

The default profile enables one extra air jump. Release and press Jump again while airborne; landing resets it. The extra jump can also exit gliding and works in ball mode. Ordinary walking gains speed downhill and loses acceleration uphill, capped at 12 m/s. Walkable slopes do not apply idle gravity, though a landing can transfer a modest part of fall speed downhill. Steep seated sliding retains its existing separate rules.

Press **X on the keyboard / West face button (Xbox X, PlayStation Square)** to toggle ball mode. It remains active through walking, coasting, jumping, falling and spring launches. Stick/WASD input drives velocity toward the requested direction; with no input the ball retains momentum with low friction, and slope gravity accelerates it downhill. Its spherical capsule can fit through the new 1.10 m tunnel. Toggling back checks the full standing capsule immediately: if blocked, the character stays a ball. Exit and toggle again to stand. Moving-platform carry and jump momentum also work in this mode. The sample uses a pink procedural ball with cyan rotation markers; Lily remains the standing avatar.

Press **V / North face button (Xbox Y, PlayStation Triangle)** to pick up a nearby `PootleCarryable` cube or drop the held object. Grounded drops place it ahead. Airborne drops first try below the feet; if the object's full volume or transfer path is blocked, the same front-drop rule is used. If the front placement is also blocked, the object remains held. Released props restore their collider and physics settings with no throwing impulse. The sample cubes also have `PootleFallingCrate`, which owns their kinematic fall instead of built-in Rigidbody gravity. Reset/disable releases held objects. Carrying occupies the hands, so automatic gripping and entering ball mode are unavailable until the object is released. Author carryable props with one BoxCollider and Rigidbody on the component's root.

The held box constrains movement at walls and ceilings; bumping an obstruction keeps it attached. Turns sweep the box and character through an arc and stop where blocked. On `PootleCarrying`, **Rotation Pivot Forward** defaults to **0.4 units** in front of Lily, nearer the middle of Lily and the box. Set it to zero for turns around Lily's root, or adjust it to change the pivot. Pickup transfer ignores separating contacts so a box resting against a wall can be lifted away.

**Pootle → Add Rolling Carrying and Camera Styles** upgrades the profile, controls, prefab and sample scene without duplicating the tunnel, cubes or camera component.

## Camera styles

Use the keyboard's number row:

| Key | Style | Behaviour |
| --- | --- | --- |
| 1 | Free orbit | Original 8 m twin-stick orbit. |
| 2 | Close orbit | 4.5 m distance with a wider field of view. |
| 3 | Chase | Follows movement facing; manual orbit overrides following for 1.5 seconds. |
| 4 | Angled overview | Fixed 45° yaw / 40° pitch, wider view of the course. |
| 5 | Overhead | Fixed 80° pitch from 18 m away. |
| 6 | Side-on | Fixed side view that follows the player. |

All styles use Cinemachine, keep obstacle handling, and retain camera-relative 3D movement. Styles 1–3 support right-stick/RMB orbit; styles 4–6 hold their viewing angles. Switching interpolates the orbit, distance and field of view. C/R3 still recenters the orbit styles.


## Smooth contact and external movement

Ordinary ramps use surface-tangent capsule motion; stair stepping only applies to a face steeper than the walkable slope limit. `smallObstacleHop` fills the gap above the 0.6 m step limit: a clear ledge up to `smallHopHeight` (default 1.05 m) starts a short lift-and-transfer hop. A blocked destination or ceiling keeps the player outside the obstacle.

`PootleClimbSurface` can opt into `allowCornerWrapping`, with `maximumCornerAngle` capped at 95 degrees. Box corners follow the rounded capsule-clearance arc. Enable `cylindrical` on round walls to follow the continuous circular side instead of render-mesh facets; `localCylinderRadius` defaults to the primitive's 0.5 m radius. `rotateCamera` rotates free/close/chase orbit yaw with the climbing normal, preserving the relative view. Fixed camera styles retain their selected viewing angle. Ground pounds can land on eligible pole tops.

Slide exits and bumper impulses hold the avatar's facing for `externalFacingHold` (0.9 s), allowing backward or sideways travel before normal facing resumes. Wind holds facing throughout the volume and for the same brief interval after leaving. Player input still controls movement.

- `PootleBumper` uses a spherical trigger to launch radially outward and upward. Tune launch speed, upward speed and cooldown independently.
- `PootleWind` is a box trigger with a local target velocity and acceleration. Use positive Y for an updraft, X/Z for sideways wind, or combine them. It affects normal airborne movement, gliding and rolling. Grips and rails retain their constrained attachment.
- `PootleFallingCrate` adds a brief low-gravity phase (0.32 s), then accelerates at 65 m/s² up to 35 m/s. It sweeps its box against solids and updates before the player. Its moving-surface marker carries a standing player through the descent; jumping launches upward even from a rapidly falling crate. Pickup resets the fall, and a new drop restarts the delay.
- `PootleConveyor` adds collision-checked surface displacement without rotating the player. A straight belt uses local direction; `PootleSplineConveyor` instead samples the nearest point and tangent on an actual Unity spline. The sample includes a straight gentle incline and a continuous curved, gently sloping mesh. Animated arrows show the flow direction. Conveyor velocity is inherited when jumping.

Use **Pootle → Add Wind Bumpers Conveyors and Falling Crates** to upgrade an older playground. The course extension is east/right of the original obstacles. The original three carryable cubes near spawn gain delayed falling and can be used as in-air stepping stones: jump, drop beneath, land on the cube, and jump again.

### Movement sounds and particles

Lily's sample prefab and playground include `PootleMovementFeedback`. Jump and double-jump voices, alternating footstep puffs and impact-scaled landing smoke reuse the reference project's assets. Hard landings use the reference stomp sound. The component exposes clip arrays, source volumes, step distance, minimum landing speed and particle references; disable it to mute and clear movement feedback. Jump voice and foot contacts use separate AudioSources so their pitch variation does not affect glider/spring audio.

`Pootle > Add Movement Sounds and Particles` reapplies the sample setup to the prefab and scene. Effects use reusable world-space emitters and clear on reset/disable. Landing feedback includes rails and pole tops; walking onto a rail and small support snaps stay quiet.

### Curved conveyors and spiral slide

Conveyors now turn riders and loose `PootleFallingCrate` props with the change in curve heading, preserving their relative orientation. `PootleConveyor.rotateWithCurve` disables that turning when desired. Crate transport sweeps for blockers and checks space before rotating; held crates are excluded. Two arrow-marked test crates start on the curved conveyor.

The eastern floor extends to x=86 and z=-22…42. The coral spiral slide is centered at (66, 0, 12): ride the teal lift on its east side to the entry deck, then walk into the trough. The lift waits two seconds at each end. It descends 18 m over 1¼ turns and ends in a 12 m flat runout. `PootleSlideSurface` makes this gentle trough slippery enough to sit and slide automatically; jump leaves it normally. The trough is a continuous high-resolution collision mesh, independent of the rail system.

`Pootle > Add Spiral Slide and Expand Playground` authors the extension without replacing existing stations.

### Momentum slide ride and follow camera

The coral trough now uses a momentum ride, separate from ordinary steep-slope slides. Banks preserve uphill and sideways speed; gravity and surface contact turn the rider, and excessive speed can carry Lily up and over a rim. Lean with the movement stick/WASD to steer; pull opposite the travel direction to brake. Jump retains forward momentum. A brief ballistic departure keeps the seated pose without repeatedly standing up and sitting down.

The normal orbit/chase cameras automatically follow travel direction during a slide ride, with a short grace period on exit. Press **7** for the dedicated slide-follow style; right stick or RMB+mouse temporarily overrides automatic turning. Other fixed camera styles remain available on 4–6. Disable `automaticallyFollowSlides` on `PootleCameraStyles` to keep the default orbit fully manual.

`PootleSlideSurface` exposes gravity, maximum driving speed, friction, steering, braking and runout deceleration. The spiral's banks are deeper, its support normals continuous, and the eastern landing floor now spans x=20…100 and z=-52…60. `Pootle > Configure Momentum Slide and Follow Camera` reapplies the sample configuration.

### Ledges, handoffs and the mountain course

Automatic ledge catches require a vertical lip on the same collider as the top, a hand-width of support, and a top slope no steeper than `ledgeMaximumTopAngle` (20° by default). Walking still uses the motor's 45° limit. Marked climbing walls can top out onto walkable slopes. A blocked flat ledge can still hold Lily until its destination clears. Disabled walls release their grip; release/jump cooldowns also prevent immediate attachment to a nearby different handle. Hooks do not take over an existing wall/pole grip.

Top-outs transfer directly to supported ground or a nearby rail; no transient airborne state is emitted on a successful handoff. Ballistic slide rides can land on rails and clear the slide camera/pose state. B/Q over a slide jump starts the normal ground pound.

Step onto the **pink pad near spawn** at (-3, 0, -8) to enter the new mountain slide; another access pad is beside the spiral at (92, 0, 3). The separate ice-blue course descends about 59 m over 413 m, with four banked bends, two open jump gaps, wider landing troughs and a 40 m easing runout. Momentum carries a normal descent across the gaps; steering and braking let you change the line. Press **7** for slide follow, use the stick/WASD to lean and pull back to brake. A pink pad past the finish returns to the playground. R still resets to the original spawn.

`Pootle > Add Mountain Slide Course` rebuilds this procedural extension. It does not replace the coral spiral or other playground stations. The course uses normal swept contact and gravity, with no path attachment or scripted jump launches.

### Ledge boundary, rail dismount and physics interaction

A climbable wall's top is now a hand-height boundary. Reaching it, or catching the face high while falling, automatically starts a short smooth top-out with no upward input required. A blocked standing destination holds Lily below the lip. Ordinary ledges still require a vertical face and a supported flat top; ramps do not become hanging surfaces.

To step sideways off a rail, first settle with the stick released for **0.18 seconds**, then hold strong sideways input for **0.3 seconds**. Lily gets a small outward push and immediately uses ordinary falling controls. Brief sideways input still selects the sideways standing pose. Held landing input cannot arm the dismount; connected branches take priority. These thresholds and the outward speed are configurable in the movement profile.

`PootleMotor` now handles ordinary rigidbodies, including turnstiles. **Enable Physics Interaction**, **Character Mass**, **Push Force Scale** and **Maximum Push Force** control relative-velocity/mass-based pushing at the contact point. **Apply Standing Downward Force** and its scale control weight on supported bodies, using world physics gravity rather than the exaggerated jump/fall gravity. Static and kinematic bodies are excluded. Dynamic supports carry the avatar as they move or tilt. The motor remains a swept, kinematic character controller.

The western test area contains **5 kg and 30 kg push boxes** and a **spring seesaw** beside the turnstiles. Jump onto either end of the seesaw to tilt it with Lily's weight. It returns toward level when unloaded. Use **Pootle → Add Turnstiles And One Way Floors** to rebuild these examples.

### Physics movers

The western extension (x = −62…−42) contains a coral **continuous spring rotor**, lime **reciprocating shuttle**, and gold **swinging pendulum**. All are dynamic rigidbodies driven by bounded spring/damper forces, rather than transform animation. The rotor targets 40°/s and yields slightly to player contact before recovering speed. The shuttle travels sideways; the pendulum swings through the player lane.

Incoming side contact transfers the body's point velocity into player momentum. Lily keeps her facing while being carried sideways or backwards by that momentum. This works for ordinary moving dynamic bodies too, respects **Enable Physics Interaction**, and ignores static/kinematic bodies and one-way floor undersides. **Maximum Incoming Speed** caps the velocity change; **Incoming Reaction Scale** controls how much the body reacts to the player. Standing on a moving body's top continues to use support carrying.

Recovery and contact queries use the same simulated body pose, including child colliders on interpolated bodies. Velocity transfer requires contact within a fixed skin of at most 1.5 cm; speed does not enlarge this range. Lily's authored capsule radius is 0.34 m to fit her body. After a real shove, a short visual blend smooths the physics steps without moving the collider before contact. Passive physics/wind movement is excluded from walking animation and footsteps; player-driven walking blends its speed over 0.12 seconds and retains the 2× jog / 3× run cadence. Unmarked dynamic props do not trigger automatic ledge catches or small-obstacle hops. Add a climb-surface marker when climbing a physics prop is intended.

**Pootle → Add Physics Movers** rebuilds these three examples and their floor. `PootleDrivenHinge` exposes rotation speed or swing amplitude/period, spring, damping and maximum torque; `PootlePhysicsShuttle` exposes travel axis, amplitude, period and bounded spring forces.


### Frobject sensor and interactables

The playground has a push button at **(2, 0, -10)** and a speaking neighbour at **(6, 0, -10)**. Approach, face the object and stop: a white awareness dot expands into a mint circle when ready. Press **A / Cross** or **F** to interact. A still jumps when there is no ready interaction; Space always jumps. Holding A does not repeat or queue a jump after a successful interaction. The button depresses and toggles its status lamp; each NPC interaction advances a short world-space speech bubble.

`PootleFrobjectSensor` is attached to the sample player and character prefab. Defaults are **8 m awareness**, **2.5 m focus**, and **45° either side of character facing** (not camera facing). Distances are measured in 3D between the sensor origin and the target interaction point. Awareness is omnidirectional; only the best-facing nearby available target is focused. Solid obstructions hide targets by default; trigger volumes, the player and the target's own colliders are ignored. Target and obstruction layer masks are configurable. Readiness requires ordinary grounded mode, speed at most **1 m/s**, no carried object, and `interactionEnabled`; set that flag false during cutscenes or other busy states.

Add `PootleFrobject` to any object, set its interaction point (or local offset), and wire **On Interact** in the Inspector. A collider is optional. `interactable` and `cooldown` gate activation; unavailable objects still show an awareness dot. Add `PootleFrobjectIndicator` for the temporary dot/circle, `PootleFrobjectButton` for a depressing toggle button, or `PootleFrobjectSpeech` for an NPC. The button's **On Value Changed(bool)** can drive another component. Speech entries advance on each press and hide on timeout, disable, or loss of awareness; keep each entry short (four lines of 32 characters).

For the future outline plugin, replace/disable `PootleFrobjectIndicator` and use `sensor.GetState(target)` (`Hidden`, `Aware`, `Ready`), `Focused`, `Aware`, and `FocusChanged`. Presentation does not own interaction logic. Assign a sensor explicitly for multiple-player scenes; automatic presentation lookup is intended for this single-player prototype. Assign the supplied **Frobject marker** and **Frobject speech** materials when authoring new visuals so the shader is retained in builds. **Pootle → Add Frobject Examples** installs the input action and sensor and adds the examples without duplicating an existing station.
