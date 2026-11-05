using System.Collections;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator Features_GamepadDoubleJumpUsesNewDefaultProfile()
        {
            character.Teleport(new Vector3(-5,.03f,-5),Quaternion.identity);yield return new WaitForSeconds(.1f);
            int jumps=0;character.Jumped+=()=>jumps++;
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSeconds(.05f);
            Assert.That(character.Velocity.y,Is.GreaterThan(12));Assert.That(character.AirJumpsUsed,Is.EqualTo(1));
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return new WaitForSeconds(.05f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSeconds(.05f);
            Assert.That(jumps,Is.EqualTo(2));Assert.That(character.profile.applySlopeSpeed);
        }
        [UnityTest]
        public IEnumerator Features_NumberKeysSelectDifferentCinemachineViewsAndRestoreOrbit()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var styles=Object.FindAnyObjectByType<PootleCameraStyles>();var orbit=styles.GetComponent<CinemachineOrbitalFollow>();
                var keys=new[]{Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6};
                Vector3 original=character.outputCamera.position;
                for(int i=0;i<6;i++)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys[i]));yield return null;
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.6f);
                    Assert.That(styles.Style,Is.EqualTo((PootleCameraStyle)i));
                    Assert.That(Vector3.Distance(character.outputCamera.position,character.cameraTarget.position),Is.GreaterThan(2));
                    Assert.That(Vector3.Dot(character.outputCamera.forward,(character.cameraTarget.position-character.outputCamera.position).normalized),Is.GreaterThan(.8f));
                }
                Assert.That(Vector3.Distance(original,character.outputCamera.position),Is.GreaterThan(5));
                // Measure input lock after the eased side-view transition settles.
                float settleDeadline=Time.time+1;
                while(Mathf.Abs(Mathf.DeltaAngle(orbit.HorizontalAxis.Value,90))>.05f && Time.time<settleDeadline)yield return null;
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(orbit.HorizontalAxis.Value,90)),Is.LessThanOrEqualTo(.05f));
                float lockedYaw=orbit.HorizontalAxis.Value;
                InputSystem.QueueStateEvent(gamepad,new GamepadState{rightStick=Vector2.right});yield return new WaitForSeconds(.3f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(lockedYaw,orbit.HorizontalAxis.Value)),Is.LessThan(.2f));
                InputSystem.QueueStateEvent(gamepad,new GamepadState());
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit1));yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSeconds(.5f);
                float freeYaw=orbit.HorizontalAxis.Value;InputSystem.QueueStateEvent(gamepad,new GamepadState{rightStick=Vector2.right});yield return new WaitForSeconds(.2f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(freeYaw,orbit.HorizontalAxis.Value)),Is.GreaterThan(3));
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }
        [UnityTest]
        public IEnumerator Features_LeftTriggerTogglesBallAndBlockedUnrollKeepsItInsideTunnel()
        {
            character.outputCamera=null;character.Teleport(new Vector3(14,.03f,-4.8f),Quaternion.identity);yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState { leftTrigger = 1 });yield return new WaitForSeconds(.08f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.up});yield return new WaitForSeconds(.32f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState { leftTrigger = 1 });yield return new WaitForSeconds(.08f);
            Assert.That(character.IsRolling);Assert.That(character.LastUnrollBlocked);Assert.That(character.Motor.CanOccupy(character.transform.position));
            Assert.That(character.GetComponent<PootleBallVisual>().ball.gameObject.activeSelf);Assert.That(character.visualRoot.gameObject.activeSelf,Is.False);
            character.Teleport(new Vector3(0,.03f,-4),Quaternion.identity);yield return null;
            Assert.That(character.IsRolling,Is.False);Assert.That(character.visualRoot.gameObject.activeSelf);
        }
        [UnityTest]
        public IEnumerator Features_NorthPicksUpCubeAndDropsBeneathDuringJump()
        {
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.North));yield return new WaitForSeconds(.1f);
            var carry=character.GetComponent<PootleCarrying>();Assert.That(carry.Held,Is.Not.Null);var item=carry.Held;
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSeconds(.18f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.North));yield return new WaitForSeconds(.05f);
            Assert.That(carry.LastDrop,Is.EqualTo(PootleDropResult.Beneath));Assert.That(item.GetComponent<PootleFallingCrate>(),Is.Not.Null);Assert.That(item.GetComponent<PootleFallingCrate>().FallSpeed,Is.GreaterThan(0));
            Assert.That(item.Shape.bounds.max.y,Is.LessThan(character.transform.position.y));
            Assert.That(Vector3.ProjectOnPlane(item.transform.position-character.transform.position,Vector3.up).magnitude,Is.LessThan(.1f));
        }
    }
}
