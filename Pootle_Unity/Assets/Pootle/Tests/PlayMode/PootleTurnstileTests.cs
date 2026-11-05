using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator Turnstile_SpringsSupportAllAxesAndCrossAngleWrap()
        {
            foreach(TurnstileGateHinge.LocalAxis axis in System.Enum.GetValues(typeof(TurnstileGateHinge.LocalAxis)))
            {
                var root=new GameObject("Axis hinge test");root.transform.SetPositionAndRotation(new Vector3(400,12,400),Quaternion.Euler(17,30,11));
                var anchor=root.AddComponent<Rigidbody>();anchor.isKinematic=true;
                var pivot=GameObject.CreatePrimitive(PrimitiveType.Cube);pivot.transform.SetParent(root.transform,false);pivot.transform.localScale=new Vector3(.4f,.4f,1.2f);
                var body=pivot.AddComponent<Rigidbody>();body.useGravity=false;body.mass=2;body.inertiaTensor=Vector3.one;body.inertiaTensorRotation=Quaternion.identity;
                var joint=pivot.AddComponent<HingeJoint>();joint.connectedBody=anchor;joint.anchor=Vector3.zero;joint.autoConfigureConnectedAnchor=false;joint.connectedAnchor=Vector3.zero;
                joint.axis=TurnstileGateHinge.AxisVector(axis);
                var gate=root.AddComponent<TurnstileGateHinge>();gate.hinge=joint;gate.pivot=pivot.transform;gate.axis=axis;gate.spring=60;gate.damper=18;
                try
                {
                    yield return null;yield return new WaitForFixedUpdate();
                    int turns=axis==TurnstileGateHinge.LocalAxis.Y?3:1;
                    for(int i=0;i<turns;i++)
                    {
                        gate.ForceTargetCwOneStep();float end=Time.time+3;
                        while(Time.time<end && Mathf.Abs(gate.CurrentAngle-gate.TargetAngle)>1)yield return null;
                        Assert.That(gate.CurrentAngle,Is.EqualTo((i+1)*90).Within(2),"Axis "+axis);
                        Assert.That(gate.rotationClicks,Is.EqualTo(i+1));
                    }
                    gate.ForceTargetCcwOneStep();float finish=Time.time+3;
                    while(Time.time<finish && Mathf.Abs(gate.CurrentAngle-gate.TargetAngle)>1)yield return null;
                    Assert.That(gate.CurrentAngle,Is.EqualTo((turns-1)*90).Within(2));Assert.That(gate.rotationClicks,Is.EqualTo(turns-1));
                }
                finally{Object.Destroy(root);}
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator Turnstile_PlayerPushesBothDirectionsAndSpringSettles()
        {
            character.simulateAutomatically=false;character.outputCamera=null;
            var gates=Object.FindObjectsByType<TurnstileGateHinge>().Where(g=>g.GetComponent<TurnstileGateBuilder>().missingArm<0).OrderBy(g=>g.name).ToArray();
            Assert.That(gates.Length,Is.EqualTo(2));
            for(int k=0;k<2;k++)
            {
                var gate=gates[k];int cw=0,ccw=0;gate.rotationClickCw.AddListener(()=>cw++);gate.rotationClickCcw.AddListener(()=>ccw++);
                float direction=k==0?1:-1;
                character.Teleport(gate.transform.position+new Vector3(1.25f,.03f,-direction*1.3f),Quaternion.identity);
                float end=Time.time+3.5f;
                while(Time.time<end && (character.transform.position.z-gate.transform.position.z)*direction<1.5f)
                {
                    character.Tick(Time.deltaTime,new PootleInputFrame{move=new Vector2(0,direction)});yield return null;
                }
                Assert.That((character.transform.position.z-gate.transform.position.z)*direction,Is.GreaterThan(1.4f),"Player should push through arm: "+gate.name+" angle="+gate.CurrentAngle+" target="+gate.TargetAngle+" clicks="+gate.rotationClicks);
                Assert.That(Mathf.Abs(gate.rotationClicks),Is.GreaterThanOrEqualTo(1),gate.name+" angle="+gate.CurrentAngle+" target="+gate.TargetAngle);
                Assert.That(gate.rotationClicks,Is.EqualTo(cw-ccw),"Click events match signed count");
                Assert.That(k==0?ccw:cw,Is.GreaterThan(0),"Both push directions should work");
                character.Teleport(new Vector3(150,.03f,280),Quaternion.identity);
                yield return new WaitForSeconds(3);
                Assert.That(Mathf.Abs(gate.CurrentAngle-gate.TargetAngle),Is.LessThan(5),"Spring returns to a detent");
                Assert.That(Vector3.Distance(gate.pivot.position,gate.transform.position),Is.LessThan(.05f),"Hinge stays anchored");
            }
        }
        [UnityTest]
        public IEnumerator Turnstile_MissingArmKeepsQuarterSpacingAndGapCanBeEntered()
        {
            character.simulateAutomatically=false;character.outputCamera=null;
            var builder=Object.FindObjectsByType<TurnstileGateBuilder>().Single(b=>b.missingArm>=0);
            var arms=builder.pivot.Find("Arms");Assert.That(arms.Find("Arm_03"),Is.Null);
            for(int i=0;i<3;i++)Assert.That(Mathf.Abs(Mathf.DeltaAngle(arms.Find($"Arm_{i:00}").localEulerAngles.y,i*90)),Is.LessThan(.01f));
            // Slot 3 points +Z. Enter along that gap, without hitting an existing arm or the hub.
            character.Teleport(builder.transform.position+new Vector3(0,.03f,3),Quaternion.identity);
            float end=Time.time+.6f;
            while(Time.time<end){character.Tick(Time.deltaTime,new PootleInputFrame{move=Vector2.down});yield return null;}
            Assert.That(character.transform.position.z-builder.transform.position.z,Is.LessThan(2));
            Assert.That(builder.GetComponent<TurnstileGateHinge>().rotationClicks,Is.Zero);
        }
        [UnityTest]
        public IEnumerator OneWay_LiveGamepadJumpLandsOnMintFloor()
        {
            var platform=Object.FindObjectsByType<PootleOneWayPlatform>().OrderBy(p=>p.Top.y).First();
            var p=platform.Top;character.Teleport(new Vector3(p.x,.03f,p.z),Quaternion.identity);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(gamepad,new UnityEngine.InputSystem.LowLevel.GamepadState().WithButton(UnityEngine.InputSystem.LowLevel.GamepadButton.South));
            yield return new WaitForSeconds(1.7f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(gamepad,new UnityEngine.InputSystem.LowLevel.GamepadState());
            Assert.That(character.IsGrounded);Assert.That(character.Support.collider.GetComponent<PootleOneWayPlatform>(),Is.SameAs(platform));
            Assert.That(character.transform.position.y,Is.EqualTo(platform.Top.y).Within(.08f));
        }
    }
}
