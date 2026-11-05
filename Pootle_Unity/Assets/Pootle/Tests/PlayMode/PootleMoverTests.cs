using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator PhysicsMover_SeparatedFacesCannotPushAtAnyApproachSpeed()
        {
            character.simulateAutomatically=false;character.outputCamera=null;
            Assert.That(character.Motor.Radius,Is.EqualTo(.34f).Within(.001f),"Authored Lily capsule fits her body");
            var origin=new Vector3(480,.03f,480);
            foreach(var type in new[]{PrimitiveType.Cube,PrimitiveType.Sphere})
            {
                var prop=GameObject.CreatePrimitive(type);
                prop.transform.localScale=type==PrimitiveType.Cube?new Vector3(1,1.6f,3):Vector3.one*1.3f;
                var body=prop.AddComponent<Rigidbody>();body.useGravity=false;body.mass=30;
                try
                {
                    float halfWidth=prop.transform.localScale.x*.5f;
                    foreach(float speed in new[]{.5f,3f,8f})
                    foreach(float gap in new[]{.025f,.06f,.15f})
                    {
                        character.Teleport(origin,Quaternion.identity);
                        prop.transform.position=origin+Vector3.up+Vector3.left*(character.Motor.Radius+halfWidth+gap);
                        body.linearVelocity=Vector3.right*speed;Physics.SyncTransforms();
                        character.Motor.BeginTick(1f/60);character.Motor.ReceiveBodyMotion(character);
                        Assert.That(character.Velocity,Is.EqualTo(Vector3.zero),$"{type}, {gap}m clear gap, {speed}m/s approach");
                        Assert.That(body.linearVelocity.x,Is.EqualTo(speed).Within(.001f),"No distant reaction impulse either");
                    }
                    character.Teleport(origin,Quaternion.identity);
                    prop.transform.position=origin+Vector3.up+Vector3.left*(character.Motor.Radius+halfWidth-.005f);
                    body.linearVelocity=Vector3.right*3;Physics.SyncTransforms();
                    character.Motor.BeginTick(1f/60);character.Motor.ReceiveBodyMotion(character);
                    Assert.That(character.Velocity.x,Is.GreaterThan(2.5f),"Actual contact still transfers momentum");
                }
                finally{prop.SetActive(false);Object.Destroy(prop);}
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator PhysicsMover_InterpolatedRotorNudgesRemainSmoothAt120Fps()
        {
            int previousRate=Time.captureFramerate;
            try
            {
                // Deliberately render faster than PhysX. SyncTransforms/FixedUpdate-only
                // tests hide the difference between rendered and simulated body poses.
                Time.captureFramerate=120;
                character.simulateAutomatically=true;character.outputCamera=null;
                InputSystem.QueueStateEvent(gamepad,new GamepadState());
                var drive=Object.FindObjectsByType<PootleDrivenHinge>().Single(d=>!d.oscillate);
                yield return new WaitForSeconds(.5f);
                var radial=drive.transform.right*2;
                var start=drive.transform.position+radial+Vector3.Cross(Vector3.up,radial).normalized*.65f;start.y=.03f;
                character.Teleport(start,Quaternion.identity);
                var avatar=character.GetComponent<PootleAvatar>();var animator=avatar.animator;
                Vector3 previous=start,previousDelta=Vector3.zero;
                float jitter=0,travel=0;int falseStops=0;float previousSpeed=0;
                for(int i=0;i<720;i++)
                {
                    yield return null;
                    // Read the previous completed LateUpdate; the new physics root
                    // may already have moved before this coroutine resumes.
                    Vector3 rendered=avatar.RenderedRootPosition;
                    Vector3 delta=rendered-previous;delta.y=0;
                    if(i>5)jitter=Mathf.Max(jitter,(delta-previousDelta).magnitude);
                    float speed=Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude;
                    if(previousSpeed>.4f && speed<.01f)falseStops++;
                    previousSpeed=speed;travel+=delta.magnitude;previous=rendered;previousDelta=delta;
                    Assert.That(animator.GetFloat("Speed"),Is.LessThan(.05f),$"A passive nudge stays in the idle blend, frame {i}, self speed {character.LocomotionSpeed}, velocity {character.Velocity}, mode {character.Mode}");
                    Assert.That(animator.GetFloat("LocomotionRate"),Is.EqualTo(1));
                    Assert.That(Vector3.Dot(character.Facing,Vector3.forward),Is.GreaterThan(.999f));
                    if(i>5)Assert.That(character.Mode,Is.EqualTo(PootleMode.Grounded),"A passing paddle must not trigger an automatic ledge hop");
                }
                Assert.That(travel,Is.GreaterThan(2),"The rotor must actually push Lily during this check");
                Assert.That(falseStops,Is.Zero,"No repeated contact stop/start");
                Assert.That(jitter,Is.LessThan(.015f),"No fixed-step depenetration bursts between rendered frames (formerly over 4 cm)");
            }
            finally{Time.captureFramerate=previousRate;}
        }
        [UnityTest]
        public IEnumerator PhysicsMover_IncomingBodyPushesAndKeepsFacing()
        {
            character.simulateAutomatically=false;character.outputCamera=null;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(450,-.5f,450);floor.transform.localScale=new Vector3(20,1,20);
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.transform.position=new Vector3(448,.8f,450);box.transform.localScale=new Vector3(1,1.6f,2);
            var rb=box.AddComponent<Rigidbody>();rb.mass=30;rb.useGravity=false;rb.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionY|RigidbodyConstraints.FreezePositionZ;
            rb.linearVelocity=Vector3.right*3;
            try
            {
                character.Teleport(new Vector3(450,.03f,450),Quaternion.identity);Physics.SyncTransforms();float speed=0;
                for(int i=0;i<100;i++){yield return new WaitForFixedUpdate();Physics.SyncTransforms();character.Tick(Time.fixedDeltaTime,default);speed=Mathf.Max(speed,character.Velocity.x);Assert.That(Vector3.Dot(character.Facing,Vector3.forward),Is.GreaterThan(.999f));}
                Assert.That(character.transform.position.x,Is.GreaterThan(451));Assert.That(speed,Is.GreaterThan(1));
                Assert.That(rb.linearVelocity.x,Is.LessThan(2.99f));Assert.That(rb.linearVelocity.x,Is.GreaterThanOrEqualTo(-.01f));
            }
            finally{Object.Destroy(box);Object.Destroy(floor);}
        }
        [UnityTest]
        public IEnumerator PhysicsMover_RotorTurnsThroughContactWithSmallSpeedLoss()
        {
            character.simulateAutomatically=false;character.outputCamera=null;
            var drive=Object.FindObjectsByType<PootleDrivenHinge>().Single(d=>!d.oscillate);
            yield return new WaitForSeconds(2);
            float free=drive.AngularSpeed;Assert.That(free,Is.EqualTo(drive.degreesPerSecond).Within(5));
            var center=drive.transform.position;
            Vector3 radius=drive.transform.right*2,tangent=Vector3.Cross(Vector3.up,radius).normalized;
            var start=center+radius+tangent*.7f;start.y=.03f;character.Teleport(start,Quaternion.identity);
            float minimum=free,maxSpeed=0;
            for(int i=0;i<120;i++)
            {
                yield return new WaitForFixedUpdate();Physics.SyncTransforms();character.Tick(Time.fixedDeltaTime,default);
                minimum=Mathf.Min(minimum,drive.AngularSpeed);maxSpeed=Mathf.Max(maxSpeed,Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude);
                Assert.That(Vector3.Dot(character.Facing,Vector3.forward),Is.GreaterThan(.999f));
            }
            Assert.That(Vector3.Distance(start,character.transform.position),Is.GreaterThan(.5f));Assert.That(maxSpeed,Is.GreaterThan(.5f));
            Assert.That(minimum,Is.LessThan(free-.1f),"Reaction slows the physical rotor");
            Assert.That(minimum,Is.GreaterThan(free*.45f),"Motor continues turning through player contact");
            character.Teleport(new Vector3(0,.03f,-4),Quaternion.identity);yield return new WaitForSeconds(2);
            Assert.That(drive.AngularSpeed,Is.EqualTo(drive.degreesPerSecond).Within(5));
        }
        [UnityTest]
        public IEnumerator PhysicsMover_ShuttleAndPendulumBumpWithoutTurningPlayer()
        {
            character.simulateAutomatically=false;character.outputCamera=null;
            var shuttle=Object.FindAnyObjectByType<PootlePhysicsShuttle>();
            foreach(var center in new[]{new Vector3(-50,.03f,8),new Vector3(-52,.03f,22)})
            {
                character.Teleport(center,Quaternion.Euler(0,180,0));float displacement=0;
                for(int i=0;i<200;i++)
                {
                    yield return new WaitForFixedUpdate();Physics.SyncTransforms();character.Tick(Time.fixedDeltaTime,default);
                    displacement=Mathf.Max(displacement,Vector3.Distance(center,character.transform.position));
                    Assert.That(Vector3.Dot(character.Facing,Vector3.back),Is.GreaterThan(.999f));
                }
                Assert.That(displacement,Is.GreaterThan(.7f));
            }
            Assert.That(Vector3.Angle(shuttle.transform.up,Vector3.up),Is.LessThan(.1f));
        }
    }
}
