using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator SpiralSlide_LiftCarriesPlayerAndWaitsForDeckExit()
        {
            var lift=GameObject.Find("Slide access lift").GetComponent<PootleSplineMover>();
            lift.simulateAutomatically=false;character.simulateAutomatically=false;
            var camera=character.outputCamera;character.outputCamera=null;
            try
            {
                character.Teleport(lift.transform.position+Vector3.up*.23f,Quaternion.identity);
                for(int i=0;i<3;i++){Physics.SyncTransforms();character.Tick(1f/60,default);}
                for(int i=0;i<700 && lift.transform.position.y<17.94f;i++)
                {lift.Tick(1f/60);Physics.SyncTransforms();character.Tick(1f/60,default);}
                Assert.That(character.IsGrounded);Assert.That(character.transform.position.y,Is.GreaterThan(18.1f));
                for(int i=0;i<45;i++){lift.Tick(1f/60);Physics.SyncTransforms();character.Tick(1f/60,new PootleInputFrame{move=Vector2.left});}
                Assert.That(lift.transform.position.y,Is.EqualTo(17.95f).Within(.02f));
                Assert.That(character.IsGrounded);Assert.That(character.Support.collider.name,Is.EqualTo("Slide entry deck"));
            }
            finally{lift.simulateAutomatically=true;character.simulateAutomatically=true;character.outputCamera=camera;}
            yield return null;
        }
        [UnityTest]
        public IEnumerator Conveyor_CrateTurnsWithCurveAndStopsAtBlocker()
        {
            var adapter=GameObject.Find("Curving spline conveyor").GetComponent<PootleSplineConveyor>();
            var belt=adapter.GetComponent<PootleConveyor>();
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.transform.localScale=Vector3.one*.8f;
            go.transform.position=(Vector3)adapter.path.EvaluatePosition(.3f)+Vector3.up*.65f;
            go.AddComponent<PootleCarryable>();var fall=go.AddComponent<PootleFallingCrate>();fall.simulateAutomatically=false;
            try
            {
                for(int i=0;i<60;i++){Physics.SyncTransforms();fall.Tick(1f/60);}
                Assert.That(fall.Grounded);
                var from=go.transform.position;var rotation=go.transform.rotation;
                for(int i=0;i<150;i++){Physics.SyncTransforms();fall.Tick(1f/60);}
                Assert.That(Vector3.Distance(from,go.transform.position),Is.GreaterThan(5));
                Assert.That(Quaternion.Angle(rotation,go.transform.rotation),Is.GreaterThan(15));
                Assert.That(Quaternion.Angle(belt.RotationBetween(from,go.transform.position)*rotation,go.transform.rotation),Is.LessThan(3));
                Assert.That(fall.Grounded);
                // A wall across the belt must stop the swept box, rather than being ignored as a carrier.
                var block=GameObject.CreatePrimitive(PrimitiveType.Cube);
                var direction=Vector3.ProjectOnPlane(belt.VelocityAt(go.transform.position),Vector3.up).normalized;
                block.transform.SetPositionAndRotation(go.transform.position+direction*1.2f,Quaternion.LookRotation(direction));block.transform.localScale=new Vector3(6,6,.2f);
                try
                {
                    var before=go.transform.position;
                    for(int i=0;i<60;i++){Physics.SyncTransforms();fall.Tick(1f/60);}
                    Assert.That(Vector3.Dot(go.transform.position-before,direction),Is.LessThan(.9f));
                }
                finally{Object.Destroy(block);}
            }
            finally{Object.Destroy(go);}
            yield return null;
        }
        [UnityTest]
        public IEnumerator SpiralSlide_FollowsTroughToRunoutAndCanJumpOff()
        {
            character.simulateAutomatically=false;character.Teleport(new Vector3(74,18.3f,12.4f),Quaternion.identity);
            int sliding=0;float minY=100;bool runout=false;float turn=0,previousAngle=Mathf.Atan2(.4f,8)*Mathf.Rad2Deg;
            for(int i=0;i<2100;i++)
            {
                Physics.SyncTransforms();character.Tick(1f/60,default);
                if(character.Mode==PootleMode.Sliding)sliding++;
                minY=Mathf.Min(minY,character.transform.position.y);
                var offset=character.transform.position-new Vector3(66,0,12);
                float angle=Mathf.Atan2(offset.z,offset.x)*Mathf.Rad2Deg;turn+=Mathf.DeltaAngle(previousAngle,angle);previousAngle=angle;
                Assert.That(character.transform.position.y,Is.GreaterThan(-.1f),"Fell through slide/floor at tick "+i);
                if(character.transform.position.x<63 && character.transform.position.y<.4f){runout=true;break;}
            }
            Assert.That(sliding,Is.GreaterThan(100));
            Assert.That(runout,Is.True,"Slide did not reach the runout: "+character.transform.position+" / "+character.Mode+" / min y "+minY);
            Assert.That(turn,Is.GreaterThan(420),"Must follow the spiral, not fall onto a lower turn");
            character.Teleport(new Vector3(74,18.3f,12.4f),Quaternion.identity);
            for(int i=0;i<90;i++){Physics.SyncTransforms();character.Tick(1f/60,default);}
            Assert.That(character.Mode,Is.EqualTo(PootleMode.Sliding));
            character.Tick(1f/60,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(character.Velocity.y,Is.GreaterThan(15));Assert.That(character.Mode,Is.EqualTo(PootleMode.Airborne));
            character.simulateAutomatically=true;yield return null;
        }
    }
}
