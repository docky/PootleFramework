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
        public IEnumerator Environment_CylinderCameraKeepsRelativeYawWhileClimbing()
        {
            character.Teleport(new Vector3(-17,1.5f,22.98f),Quaternion.identity);
            yield return new WaitForSeconds(.1f);
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));
            var orbit=Object.FindAnyObjectByType<CinemachineOrbitalFollow>();
            float relative=Mathf.DeltaAngle(Mathf.Atan2(character.Facing.x,character.Facing.z)*Mathf.Rad2Deg,orbit.HorizontalAxis.Value);
            InputSystem.QueueStateEvent(gamepad,new GamepadState{leftStick=Vector2.right});yield return new WaitForSeconds(.9f);
            float after=Mathf.DeltaAngle(Mathf.Atan2(character.Facing.x,character.Facing.z)*Mathf.Rad2Deg,orbit.HorizontalAxis.Value);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(relative,after)),Is.LessThan(2));
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Climbing));
        }
        [UnityTest]
        public IEnumerator Environment_UpdraftAndBumperPreserveFacing()
        {
            character.Teleport(new Vector3(22,.03f,8),Quaternion.identity);yield return new WaitForSeconds(.65f);
            Assert.That(character.transform.position.y,Is.GreaterThan(3));Assert.That(character.Facing.z,Is.GreaterThan(.99f));
            character.Teleport(new Vector3(25.2f,.03f,20),Quaternion.identity);yield return new WaitForSeconds(.25f);
            Assert.That(character.transform.position.x,Is.GreaterThan(27));Assert.That(character.Facing.z,Is.GreaterThan(.99f));
        }
        [UnityTest]
        public IEnumerator Environment_BothBeltsCarryWithoutInputAlongTheirSurfaces()
        {
            foreach(var adapter in Object.FindObjectsByType<PootleSplineConveyor>())
            {
                var p=(Vector3)adapter.path.EvaluatePosition(.35f);character.Teleport(p+Vector3.up*.05f,Quaternion.identity);
                yield return new WaitForSeconds(.1f);var start=character.transform.position;var facing=character.Facing;
                yield return new WaitForSeconds(.8f);
                Assert.That(Vector3.Distance(start,character.transform.position),Is.GreaterThan(1.8f));
                Assert.That(character.IsGrounded);Assert.That(character.Support.collider.GetComponent<PootleConveyor>(),Is.Not.Null);
                var belt=adapter.GetComponent<PootleConveyor>();
                Assert.That(Vector3.Angle(character.Facing,belt.RotationBetween(start,character.transform.position)*facing),Is.LessThan(2));
            }
        }
        [UnityTest]
        public IEnumerator Environment_DroppedCrateCanBeLandedOnAndJumpedFromInAir()
        {
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.North));yield return new WaitForSeconds(.08f);
            var carry=character.GetComponent<PootleCarrying>();var item=carry.Held;Assert.That(item,Is.Not.Null);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSeconds(.18f);
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.North));yield return null;
            InputSystem.QueueStateEvent(gamepad,new GamepadState());
            bool riding=false;
            for(int i=0;i<120;i++)
            {
                yield return null;
                if(character.IsGrounded && character.Support.collider==item.Shape){riding=true;break;}
            }
            Assert.That(riding);Assert.That(item.transform.position.y,Is.GreaterThan(.5f),"Catch should occur before crate settles on the floor");
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));yield return new WaitForSeconds(.03f);
            Assert.That(character.Velocity.y,Is.GreaterThan(15));Assert.That(character.IsGrounded,Is.False);
        }
    }
}
