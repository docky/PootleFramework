using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Pootle.Tests
{
    public sealed class CoastalExplorationPlayModeTests
    {
        PootleCharacter player;
        PootleZoneCameraDirector director;
        Transform output;
        Gamepad pad;
        InputSettings.EditorInputBehaviorInPlayMode inputBehavior;
        [UnitySetUp]public IEnumerator Setup()
        {
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/CoastalHike/CoastalHike.unity",new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("Editor prototype scene checks");yield break;
#endif
            player=Object.FindAnyObjectByType<PootleCharacter>();director=Object.FindAnyObjectByType<PootleZoneCameraDirector>();
            Assert.That(player,Is.Not.Null);Assert.That(director,Is.Not.Null);
            output=player.outputCamera;player.simulateAutomatically=false;
            inputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            if(pad!=null)InputSystem.RemoveDevice(pad);
            InputSystem.settings.editorInputBehaviorInPlayMode=inputBehavior;
            if(player){player.outputCamera=output;player.simulateAutomatically=true;}
            yield return null;
        }
        PootleCameraZone Region(string name)=>GameObject.Find(name).GetComponent<PootleCameraZone>();
        void Tick(int count,PootleInputFrame input=default)
        {for(int i=0;i<count;i++){player.Tick(1f/60,input);Physics.SyncTransforms();}}
        void WalkTo(Vector3 point)
        {
            for(int i=0;i<700;i++)
            {
                Vector3 delta=Vector3.ProjectOnPlane(point-player.transform.position,Vector3.up);
                if(delta.magnitude<.25f){Assert.That(player.transform.position.y,Is.EqualTo(point.y).Within(.3f));return;}
                delta=delta.normalized;Tick(1,new PootleInputFrame{move=new Vector2(delta.x,delta.z)*.65f});
            }
            Assert.Fail("Route blocked at "+player.transform.position+" while walking to "+point);
        }
        [UnityTest]public IEnumerator BothClimbingAreasAndCoastalLoopAreTraversable()
        {
            player.outputCamera=null;
            foreach(var location in new[]{new Vector3(29,2.15f,24.5f),new Vector3(-32,3.15f,19.1f)})
            {
                player.Teleport(location,Quaternion.identity);bool climbed=false;float highest=location.y;
                for(int i=0;i<165;i++){Tick(1,new PootleInputFrame{move=Vector2.up});climbed|=player.Mode==PootleMode.Climbing;highest=Mathf.Max(highest,player.transform.position.y);}
                Assert.That(climbed,Is.True);Assert.That(highest,Is.GreaterThan(location.y+4));
            }
            player.Teleport(new Vector3(26,.15f,7),Quaternion.identity);
            foreach(var p in new[]{new Vector3(29,2,20),new Vector3(35,2,24),new Vector3(35,2,36),new Vector3(35,2,41),new Vector3(22,7,43),new Vector3(18,7,40)})WalkTo(p);
            Assert.That(player.IsGrounded,Is.True);yield return null;
        }
        [UnityTest]public IEnumerator RealBrainBlendKeepsHeldBasisAndFreshInputUsesNewView()
        {
            var south=Region("ZONE · Cliff corner south");var north=Region("ZONE · Lower stacked route");
            player.Teleport(south.playerArrival.position,Quaternion.identity);director.Preview(south);
            yield return new WaitForSeconds(1.5f);
            pad=InputSystem.AddDevice<Gamepad>();InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});
            yield return null;yield return null;
            Vector3 before=PootleMovement.CameraDirection(Vector2.up,output,out _);
            director.Preview(north);yield return new WaitForSeconds(1.5f);
            Assert.That(Mathf.DeltaAngle(output.eulerAngles.y,north.cameraRig.GetComponent<PootleExplorationShot>().AuthoredYaw),Is.EqualTo(0).Within(.2f));
            Assert.That(Vector3.Angle(before,PootleMovement.CameraDirection(Vector2.up,output,out _)),Is.LessThan(.1f));
            Assert.That(director.movementBasis.IsLocked,Is.True);
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState{leftStick=Vector2.up});yield return null;yield return null;
            Assert.That(Vector3.Angle(PootleMovement.CameraDirection(Vector2.up,output,out _),PootleMovementBasis.HorizontalForward(output.rotation)),Is.LessThan(.1f));
            Quaternion view=output.rotation;player.transform.rotation=Quaternion.Euler(0,170,0);
            InputSystem.QueueStateEvent(pad,new GamepadState{rightStick=Vector2.one});yield return new WaitForSeconds(.3f);
            Assert.That(Quaternion.Angle(view,output.rotation),Is.LessThan(.1f));
        }
        [UnityTest]public IEnumerator StackedRoutesStaticViewAndSplineRailWorkInScene()
        {
            player.Teleport(new Vector3(35,2.2f,36),Quaternion.identity);
            Assert.That(director.ActiveZone.name,Does.Contain("Lower stacked"));
            player.Teleport(new Vector3(35,10.2f,36),Quaternion.identity);
            Assert.That(director.ActiveZone.name,Does.Contain("Upper stacked"));
            var region=Region("ZONE · Jetty static composition");director.Preview(region);
            var shot=region.cameraRig.GetComponent<PootleExplorationShot>();
            player.Teleport(new Vector3(7,.2f,-12),Quaternion.identity);yield return new WaitForSeconds(1.5f);Vector3 start=output.position;
            player.Teleport(new Vector3(10,.2f,-9),Quaternion.identity);yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(output.position,start),Is.LessThan(.01f));
            var rail=GameObject.Find("CAM · Curved beach rail").GetComponent<PootleExplorationShot>();
            Assert.That(rail.spline,Is.Not.Null);
            player.Teleport(new Vector3(-100,.2f,-5),Quaternion.identity);rail.TickShot(.02f);
            Assert.That(rail.Project(player.transform.position),Is.EqualTo(0).Within(.02f));
            Assert.That(Vector3.Distance(rail.transform.position,rail.EvaluateRail(rail.RailT)),Is.LessThan(.001f));
            Assert.That(player.transform.position.x,Is.EqualTo(-100));
        }
        [UnityTest]public IEnumerator AuthoredViewsKeepPlayerVisibleInHedgesCourtyardAndBelowBridge()
        {
            foreach(var point in new[]{new Vector3(19,.2f,4),new Vector3(-32,8.55f,21),new Vector3(35,2.2f,39)})
            {
                player.Teleport(point,Quaternion.identity);Tick(20);director.ReturnToRegions();
                yield return new WaitForSeconds(1.5f);
                Vector3 chest=player.transform.position+Vector3.up;
                Vector3 viewport=Camera.main.WorldToViewportPoint(chest);
                Assert.That(viewport.z,Is.GreaterThan(0));Assert.That(viewport.x,Is.InRange(.1f,.9f));Assert.That(viewport.y,Is.InRange(.1f,.9f));
                Vector3 ray=chest-output.position;
                if(Physics.Raycast(output.position,ray.normalized,out var hit,ray.magnitude,~0,QueryTriggerInteraction.Ignore))
                    Assert.That(hit.transform.IsChildOf(player.transform),Is.True,"Occluded by "+hit.collider.name+" at "+point);
            }
        }
        [UnityTest]public IEnumerator YawHandoversWaitForActualLandingInBothDirections()
        {
            director.minimumDwell=0;player.outputCamera=null;
            player.Teleport(new Vector3(29,2.1f,24),Quaternion.identity);Tick(20);director.Evaluate();
            Assert.That(director.ActiveZone.name,Does.Contain("south"));
            player.transform.position=new Vector3(35,16,36);Tick(1);director.Evaluate();
            Assert.That(director.focus.IsAirborne,Is.True);Assert.That(director.ActiveZone.name,Does.Contain("south"));
            Tick(180);director.Evaluate();Assert.That(player.IsGrounded,Is.True);Assert.That(director.ActiveZone.name,Does.Contain("Upper stacked"));
            player.transform.position=new Vector3(29,15,24);Tick(1);director.Evaluate();
            Assert.That(director.ActiveZone.name,Does.Contain("Upper stacked"));
            Tick(180);director.Evaluate();Assert.That(player.IsGrounded,Is.True);Assert.That(director.ActiveZone.name,Does.Contain("south"));
            yield return null;
        }
    }
}
