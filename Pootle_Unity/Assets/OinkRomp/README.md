# OinkRomp

Open `Assets/OinkRomp/OinkRomp.unity` or use **Pootle → OinkRomp → Open OinkRomp**.

OinkRomp is a fresh copy of the current Coastal Hike scene. All original landscape roots, bridges, caves, camera zones, rollercoaster and chair lift remain active. The Quirky Pig replaces Lily and uses its Idle, Walk, Run, Jump, Fly, Swim, Attack and Eat animations. A rope fits around its belly.

## Controls and movement

Uses Coastal's input asset, camera-relative movement, acceleration, running, automatic traversal, and camera handover system. Atari CX fire, joystick trigger, gamepad jump, and Space share the action. WASD, arrows, D-pad and sticks move the pig. Native run, detach and interaction bindings remain available.

Oink-specific smoothing: 0.26 seconds to full movement, 0.14 seconds to release, 0.48 seconds for full reversal, no analogue direction snapping, 360 degrees/second visual facing. Minimum analogue speed is 0.6 for gentle positioning. These are scene/profile settings; Coastal's settings are unchanged.

- Begin with one ordinary jump and zero balloon upgrades.
- Find seven unique balloons. Each permanently adds one extra jump during the current play session, including after recovery.
- Each extra tap deploys one transparent balloon. Seven upgrades permit seven airborne deployments after the first hop.
- Balloons deployed inside cave/interior volumes pop immediately and supply no extra lift or hover. Unlocked capacity remains and recharges on landing.
- Balloons gradually shrink. Used slots stay used for that flight; landing recharges all unlocked slots. Balloons are never consumable inventory.
- Holding jump for 0.42 seconds pops all deployed balloons and plunges. Impact radius grows with fall height. The cracked lookout floor requires at least a two-metre slam to break.
- Springs count as the first hop. Push into gold buttons to attack and open their gates. Walk into apples to eat them.
- Birds start and re-enable in Idle_A, use Idle_B nearby, and return to Idle_A afterward. OinkRomp uses local copies of the Quirky animal controllers with Idle_A as their default.
- Bird speech appears only nearby. The HUD contains only small counters and nearby speech.

## Added discoveries

Seven upgrades span the trailhead, beach, sun terrace, high meadow, hidden fern room, top of the copied spiral rail and summit. Added apples, two shove gates, a cracked lookout floor, spring, mud pool and muddy banks live on the original island.

The helix spline rail and both hanging handles are copied from Pootle Playground. Their native rail, hook and spline motion components are retained. Two slippery ramps connect terrace heights. All 35 Coastal camera zones remain, with a minimum 65-degree overhead pitch (68 degrees in caves). Original cave lighting, doorway iris transitions and tunnels remain active.

The pig's projected blob shadow combines seven circles attached to its body, head, four legs and tail. The shared blob component now accepts optional generic-rig bones; existing humanoid defaults are unchanged. Pootle's jump buffer also remains available to native traversal when a movement policy is installed, allowing the pig to jump off rails and handles.

## Verification and video

Play mode regression coverage is in `Tests/OinkRompPlayModeTests.cs`, including physical rail/hook release, ramp sliding, cave travel ownership, input smoothing, permanent balloon capacity and height-gated floor breaking. Latest results are saved under `Artifacts/OinkRomp`.

The new recording is `Builds/oinkromp/oinkromp-coastal-revision-2026-11-08.mp4`. It is a scripted gameplay walkthrough using the actual controller, collisions and camera zones, with cuts between discoveries. The older `oinkromp-gameplay-2026-11-08.mp4` shows the superseded orchard prototype.

`AgentScripts/OinkRomp/Build.cs` recreates this scene from Coastal Hike and copies the Pootle traversal objects. It backs up the former orchard scene under `Builds/oinkromp/archive`. The original Coastal Hike and Pootle Playground scene files are not edited.
