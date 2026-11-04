# Pootle v0.1

Open `Assets/Pootle/Samples/PootlePlayground.unity` directly and press Play. The sample uses animated Lily, ordinary solid colliders, and a Cinemachine orbit camera. There is no bootstrap scene or game manager. The Pootle test model is not used.

| Action | Gamepad | Keyboard and mouse |
| --- | --- | --- |
| Move / analogue walk | Left stick | WASD |
| Orbit camera | Right stick | Hold right mouse button and move mouse; or arrow keys |
| Jump / short jump | South face button; release early for a shorter jump | Space |
| Run | Left shoulder (LB / L1) | Left Shift |
| Grip wall / cylinder / pole / rope / hook | Hold left trigger (LT / L2) | Hold E |
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

Landing on a surface steeper than the motor's slope limit enters a seated, unsteerable slide. Facing snaps to the closest forward/backward/sideways direction **relative to downhill**, then stays at that relative angle while the slope curves. Entry retains only 10% of sideways velocity and quickly damps the rest; downhill comes from the slope normal, with independent lateral-retention/damping tuning. Jump escapes the slide. Momentum carries the seated character onto flat ground and decays using `slideFlatDeceleration`; ordinary movement resumes after stopping. The movement profile exposes slide gravity, friction, maximum speed, minimum seated time and `seatedSlopeNormalBlend` (0 upright, 1 follows slope normal). The collision capsule stays upright; pose tilt changes the visual only.

Add `PootleClimbSurface` to a solid wall or an ancestor of its colliders. Hold grip near it and move up/down or sideways. Cylinders need a true curved collider with a flat top (the sample uses the cylinder mesh as a static MeshCollider, replacing Unity's rounded capsule). Moving up at the lip automatically lifts the capsule and mini-hops onto the top. An airborne descending character can catch an ordinary vertical ledge within hand reach without a marker. The catch automatically tops out when there is space; a blocked destination stays hanging. Move down to drop, or jump. Every part of the hop uses capsule sweeps and destination clearance. Its lift and landing phases ease to rest; jump/fall animation avoids the reference clip’s baked top-out displacement. The 0.6 m step height also permits side entry halfway up the sample stairs.

Add `PootlePole` to a straight pole or rope with a **trigger** collider. Author the marker at its centre with local up along the rope, unit scale, and set its length/radius in metres. Hold grip nearby to attach. Left-stick/W/S vertical input climbs, left-stick/A/D horizontal input turns facing in place. The body attaches behind its facing direction, tracks around the pole when turning, and uses procedural hand placement. Attachment preserves the incoming facing; jumping launches along that facing, including toward/across the pole, rather than forcing an outward jump. Release grip to drop. These are static straight ropes/poles; swinging ropes and moving-wall transport are later work.

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

`PootleRail` defines a static polyline using local-space **walking-surface** points. Give its visual no solid colliders (or trigger colliders). Jumping from below passes through; a descending foot crossing within `landingHalfWidth` plus a configurable fraction of capsule radius lands on the top. Walk toward an endpoint touching the floor to step onto a rail without jumping. The sample includes a low corner rail, a gentle incline, and a steep rail with a flat runout. Use **Pootle → Add Rail Playground** to add these examples to an older sample.

Movement projects camera-relative stick/WASD input along the rail. On landing, facing snaps to the closest of four directions relative to the rail; while walking it faces forward/back along the rail. Jump uses the requested movement direction, or the current facing when idle, with some carried momentum. Walking off an end falls normally and retains coyote jumping.

Above the motor slope limit, rail movement becomes a seated gravity-driven slide, preserving a cardinal facing relative to each section. Slide gravity, maximum speed, friction, flat deceleration and seated tilt share the slope profile settings. Momentum continues across connected sections and off an endpoint; a flat section slows the slide until ordinary walking resumes. External solid obstacles still block the capsule. Rails are static polylines; moving rails are outside this pass. Spline authoring is available separately for platforms and hooks.

## Moving platforms and hooks

`PootleMovingSurface` on a solid platform carries the grounded character using translation and rotation around a local anchor. Jumps inherit platform velocity. Capsule sweeps and overlap recovery remain active; a blocked character does not teleport through geometry. The playground has a travelling platform and a vertical lift.

`PootleDangleHook` has an authored grip point, reach radius, hanging height/body offset and obstruction tolerance. The offset rotates with facing to keep Lily’s hands at the ring and her head clear. Hold LT/L2 or E near the grip to attach and keep holding to ride. Move the left stick/WASD in any camera-relative direction to turn; the hook rotates with Lily. Jump launches in the requested direction, or retained facing when the stick is neutral, with carrier momentum. Release grip to drop. If collision prevents following beyond the distance/grace-time threshold, attachment releases automatically. The playground includes a shuttle and a hook that passes through an obstruction for testing.

Add `PootleSplineMover` from the optional `Pootle.Splines` integration to either object and assign a Unity `SplineContainer`. Speed is metres/second, open paths can ping-pong, and closed paths loop. Optional path-facing rotates platforms; leave it off for hooks whose facing the player controls. Rebuild the mover after editing a path at runtime. **Pootle → Add Moving Platforms and Hooks** adds the sample objects.

## Gliding and springs

Hold RB/R1 or F while descending to deploy Lily's glider. The left stick/WASD steers relative to the camera, while the right stick/RMB controls the camera independently. Default descent is capped at 2 m/s, forward cruise is 3 m/s and full-stick speed is 9 m/s. Releasing the button resumes falling; landing, gripping or catching a rail closes the glider. Deploying does not add height. There is no stamina system in this pass.

`PootleSpring` launches a descending or grounded character supported on its solid pad. Launch speed, local direction, retained lateral momentum, cooldown, sound and launch event are configurable. It resets air jumps and does not require holding jump for full height. Side contact and upward passage do not launch. The sample has vertical and angled springs; **Pootle → Add Glider and Springs** upgrades the prefab and playground. Glider art, pose and short audio cues come from the reference project.
