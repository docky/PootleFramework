using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator PhysicsInteraction_PushesOrdinaryBodiesAndRespectsDisableAndMass()
        {
            character.simulateAutomatically=false;character.outputCamera=null;
            var root=new GameObject("Isolated push fixture");var origin=new Vector3(450,5,450);
            GameObject Cube(string name,Vector3 offset,Vector3 scale)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root.transform);
                go.transform.position=origin+offset;go.transform.localScale=scale;return go;
            }
            try
            {
                Cube("Ground",new Vector3(0,-.5f,0),new Vector3(20,1,20));
                float light=0,heavy=0;
                for(int attempt=0;attempt<3;attempt++)
                {
                    var box=Cube("Ordinary dynamic box",new Vector3(0,1,1.5f),new Vector3(2,2,1));
                    var body=box.AddComponent<Rigidbody>();body.mass=attempt==1?80:5;body.useGravity=false;
                    body.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionY;
                    character.Motor.enablePhysicsInteraction=attempt!=2;
                    character.Teleport(origin+new Vector3(0,.03f,0),Quaternion.identity);Physics.SyncTransforms();
                    for(int i=0;i<45;i++){yield return new WaitForFixedUpdate();character.Tick(Time.fixedDeltaTime,new PootleInputFrame{move=Vector2.up});}
                    float distance=body.position.z-origin.z-1.5f;
                    if(attempt==0)light=distance;else if(attempt==1)heavy=distance;
                    else Assert.That(Mathf.Abs(distance),Is.LessThan(.01f),"Interaction switch prevents pushing");
                    Object.Destroy(box);yield return null;
                }
                Assert.That(light,Is.GreaterThan(.3f));Assert.That(heavy,Is.GreaterThan(.05f));
                Assert.That(light,Is.GreaterThan(heavy),"Heavier objects respond less to the same contact");
            }
            finally{character.Motor.enablePhysicsInteraction=true;Object.Destroy(root);}
        }
        [UnityTest]
        public IEnumerator PhysicsInteraction_StandingWeightTiltsSeesawAndCarriesPlayer()
        {
            character.simulateAutomatically=false;character.outputCamera=null;
            var root=new GameObject("Isolated weight fixture");root.transform.position=new Vector3(450,8,450);
            var anchor=root.AddComponent<Rigidbody>();anchor.isKinematic=true;
            var board=GameObject.CreatePrimitive(PrimitiveType.Cube);board.transform.SetParent(root.transform,false);board.transform.localScale=new Vector3(6,.3f,3);
            var body=board.AddComponent<Rigidbody>();body.mass=100;body.useGravity=false;
            var joint=board.AddComponent<HingeJoint>();joint.connectedBody=anchor;joint.axis=Vector3.forward;joint.anchor=Vector3.zero;
            joint.useLimits=true;joint.limits=new JointLimits{min=-18,max=18};joint.useSpring=true;joint.spring=new JointSpring{spring=2500,damper=400};
            try
            {
                character.Teleport(root.transform.position+new Vector3(1.8f,.18f,0),Quaternion.identity);Physics.SyncTransforms();
                float maxTilt=0;int grounded=0;
                for(int i=0;i<100;i++)
                {
                    yield return new WaitForFixedUpdate();Physics.SyncTransforms();character.Tick(Time.fixedDeltaTime,default);
                    maxTilt=Mathf.Max(maxTilt,Vector3.Angle(board.transform.up,Vector3.up));
                    if(character.IsGrounded)grounded++;
                }
                Assert.That(maxTilt,Is.GreaterThan(5),"Weight applies torque at the feet");
                Assert.That(grounded,Is.GreaterThan(85),"Player follows the tilting support");
                Assert.That(character.Support.collider,Is.SameAs(board.GetComponent<Collider>()));
                character.Teleport(root.transform.position+Vector3.up*10,Quaternion.identity);
                for(int i=0;i<150;i++)yield return new WaitForFixedUpdate();
                Assert.That(Vector3.Angle(board.transform.up,Vector3.up),Is.LessThan(2),"Unloaded board returns toward level");
            }
            finally{Object.Destroy(root);}
        }
    }
}
