Name : Tiny Car Controller
Version : 2.1.0
Author : David Jalbert
Last modified : 2025-10-28
Description : Simple dynamic vehicle controller for Unity 6.

Support: 
- Unity Asset Store : https://u3d.as/1AY1
- WebGL demo : https://davidjalbert.itch.io/tiny-car-controller-webgl-demo
- Email : jalbert.d@hotmail.com
- Bluesky : https://bsky.app/profile/davidjalbertgames.ca

=====================================================
Package structure
=====================================================

TinyCarController
    Root of the package. Contains this help file and the documentation in pdf format.
TinyCarController/Examples
    Example scenes; a minimal setup, and a sandbox that showcases different features.
TinyCarController/Scripts
    /Core : Core scripts that manage the behavior of the vehicles.
    /Helpers : Optional scripts to help with different generic tasks such as camera, input, and visual effects.
    /Internal : Classes and functions that are only meant to be used by the system internally.

=====================================================
Getting started
=====================================================

The basics

1) Create an empty GameObject in your scene.
2) Add the script at 'Assets/DavidJalbert/TinyCarController/Scripts/Core/TCCPlayer.cs' to the GameObject.
3) Create empty GameObjects as children of the root GameObject for the body of the vehicle and for each wheel.
4) Add the script at 'Assets/DavidJalbert/TinyCarControllerAdvance/Scripts/Core/TCCABody.cs' to the object that will serve as the vehicle body.
5) Add the script at 'Assets/DavidJalbert/TinyCarControllerAdvance/Scripts/Core/TCCAWheel.cs' to each of the objects you created to serve as the vehicle wheels.
6) Add your models as children of the corresponding GameObjects. Make sure you have one object per wheel and one for the body, and that they are separated.
7) The pivot of your body and wheel objects will determine how the spring joints will be configured, so make sure to position them accordingly. The models should be centered as much as possible with the pivot of their parent object.
8) Set the parameters of the body and wheel objects to your liking. The default parameters are generally good enough.
9) Set the parameters for the vehicle in the TCCPlayer script that you added to the root object. This is what will control things like max speed, acceleration, tire grip, and roll and pitch countering.

For surfaces

1) Add the script at 'Assets/DavidJalbert/TinyCarController/Scripts/Core/TCCSurface.cs' to the collider on which you want to apply the modifiers.
2) Adjust the parameters to modify the behavior of the surface when the vehicle runs over it.

For input

1) Add the script at 'Assets/DavidJalbert/TinyCarController/Scripts/Helpers/TCCStandardInput.cs' to an empty GameObject.
2) Drag and drop the player object to the "Car Controller" field.
3) Set each input action to the desired key or button.

For the camera

1) Add the script at 'Assets/DavidJalbert/TinyCarController/Scripts/Helpers/TCCCamera.cs' to your camera GameObject.
2) Drag and drop the player object to the "Car Controller" field.
3) Adjust the parameters to define the behavior of the camera relative to the vehicle.

For sound and visual effects

1) Create an empty GameObject as a child of your vehicle object.
2) Add the script at 'Assets/DavidJalbert/TinyCarController/Scripts/Helpers/TCCBasicEffects.cs' to the GameObject.
3) Set each parameters to add sound and effects for the engine, braking, collision, and boost.

=====================================================
Tips
=====================================================

- Fields tooltips and contextual tips in the editor explain each parameter's functionality.
- Don't be afraid to experiment with parameter values to find the right behavior for your needs.
- Check out the scene at 'Assets/DavidJalbert/TinyCarController/Examples/ExampleSandbox.unity'.
- You can open the helper scripts in your code editor to see how they work, and learn how to code your own.
- Don't forget to read documentation.pdf !

=====================================================
Version History:
=====================================================

Version 2.1.0 - 2025-10-28

- Added a downhill and uphill max angle parameter.
- Fixed the wheels not spinning in the right direction when not having a model container.

Version 2.0.0 - 2025-10-13

- Major update, completely changes the system.
- Bridges the gap between Tiny Car Controller and TCC Advance, which has been discontinued.
- Now supports Unity 6.

Version 1.7.1 - 2022-08-19

- Now uses Unity 2021.3.6f1

Version 1.7.0 - 2022-04-04

- Added the parameter "Side friction multiplier" to the TinyCarSurface script

Version 1.6.0 - 2021-06-09

- Added the TinyCarAudio component
- Minor bug fixes

Version 1.5.0 - 2020-10-29

- The old "player" script has been segmented into individual components
- Simplified ground detection parameters (removed skin width and ground check distance)
- Added a speed boost parameter to the controller
- Added a script for car destruction effects
- Added a script for mobile (on-screen touch) input
- Surface modifiers can now take multipliers that are more than one
- Triggers can now have surface parameters. The ground takes precedence over triggers
- The car visuals now leans forward when in the air
- Added new elements in the example scene

Version 1.4.0 - 2020-06-07

- Added reverse speed/acceleration and brake parameters
- Improved the calculations of the angle of the car's body

Version 1.3.1 - 2020-05-19

- Fixed the third-person camera jitter when turning

Version 1.3.0 - 2020-04-15

- Minimum compatible version is now 2018.4
- Added a parameter to define slope friction when gravity is set to toward slopes
- Added the option to toggle camera mode in demo

Version 1.2.1 - 2020-03-25

- Fixed delta time for acceleration functions

Version 1.2.0 - 2020-03-23

- Added the Tiny Car Camera script
- Added the Tiny Car Surface script
- Added a parameter to define the layers checked on the controller
- Added the option to only rotate on the x axis (for motorcycles for instance)
- Misc bug fixes

Version 1.1.0 - 2020-02-14

- Added the option to change the gravity behavior
- Minor bug fixes

Version 1.0.0 - 2020-01-14

- Initial release
