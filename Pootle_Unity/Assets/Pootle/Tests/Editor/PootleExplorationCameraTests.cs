using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace Pootle.Tests
{
    public sealed class PootleExplorationCameraTests
    {
        GameObject root;
        GameObject Child(string name){var go=new GameObject(name);go.transform.SetParent(root.transform);return go;}
        [SetUp]public void Setup()=>root=new GameObject("Exploration camera test");
        [TearDown]public void Cleanup()=>Object.DestroyImmediate(root);
        PootleCameraZone Zone(string name,Vector3 center,Vector3 size,int priority)
        {
            var zone=Child(name).AddComponent<PootleCameraZone>();zone.transform.position=center;
            zone.GetComponent<BoxCollider>().size=size;zone.cameraRig=Child(name+" shot").AddComponent<CinemachineCamera>();zone.precedence=priority;return zone;
        }
        PootleZoneCameraDirector Director(params PootleCameraZone[] zones)
        {
            var director=Child("Director").AddComponent<PootleZoneCameraDirector>();director.target=Child("Player").transform;
            director.fallbackCamera=Child("Fallback").AddComponent<CinemachineCamera>();director.zones=zones;return director;
        }
        [Test]public void HeightSelectsStackedRoutesAndEqualPriorityHasStableOrder()
        {
            var lower=Zone("Lower",Vector3.up*2,new Vector3(10,4,10),5);
            var upper=Zone("Upper",Vector3.up*10,new Vector3(10,4,10),5);
            var director=Director(lower,upper);director.target.position=Vector3.up*2;director.EvaluateAt(0,false,true);
            Assert.That(director.ActiveZone,Is.EqualTo(lower));director.target.position=Vector3.up*10;director.EvaluateAt(1,false);
            Assert.That(director.ActiveZone,Is.EqualTo(upper));
            upper.transform.position=lower.transform.position;director.target.position=Vector3.up*2;director.EvaluateAt(2,false);
            Assert.That(director.ActiveZone,Is.EqualTo(lower));
        }
        [Test]public void DwellAndAirborneYawDeferralWorkInBothDirections()
        {
            var a=Zone("A",Vector3.zero,new Vector3(4,8,4),1);var b=Zone("B",Vector3.right*10,new Vector3(4,8,4),2);
            b.cameraRig.transform.rotation=Quaternion.Euler(55,60,0);
            var d=Director(a,b);d.minimumDwell=.8f;d.EvaluateAt(0,false,true);d.target.position=Vector3.right*10;
            d.EvaluateAt(.2f,false);Assert.That(d.ActiveZone,Is.EqualTo(a));
            d.EvaluateAt(1,true);Assert.That(d.ActiveZone,Is.EqualTo(a));
            d.EvaluateAt(1.1f,false);Assert.That(d.ActiveZone,Is.EqualTo(b));
            d.target.position=Vector3.zero;d.EvaluateAt(2.1f,true);Assert.That(d.ActiveZone,Is.EqualTo(b));
            d.EvaluateAt(2.2f,false);Assert.That(d.ActiveZone,Is.EqualTo(a));
        }
        [Test]public void HeldMovementAndGlideInputKeepWorldDirectionUntilRelease()
        {
            var camera=Child("Rendered camera");var basis=camera.AddComponent<PootleMovementBasis>();
            basis.Sample(new Vector2(.3f,.8f),Quaternion.identity,0);
            var before=PootleMovement.CameraDirection(new Vector2(.3f,.8f),camera.transform,out _);
            basis.BeginHandover(90,1.2f,0);camera.transform.rotation=Quaternion.Euler(55,90,0);
            basis.Sample(new Vector2(.3f,.8f),camera.transform.rotation,.7f);
            var during=PootleMovement.CameraDirection(new Vector2(.3f,.8f),camera.transform,out _);
            Assert.That(Vector3.Angle(before,during),Is.LessThan(.01f));Assert.That(basis.IsLocked,Is.True);
            basis.Sample(Vector2.zero,camera.transform.rotation,2);basis.Sample(Vector2.up,camera.transform.rotation,2.1f);
            Assert.That(Vector3.Angle(PootleMovement.CameraDirection(Vector2.up,camera.transform,out _),Vector3.right),Is.LessThan(.01f));
            basis.enabled=false;camera.transform.rotation=Quaternion.identity;
            Assert.That(PootleMovement.CameraDirection(Vector2.up,camera.transform,out _),Is.EqualTo(Vector3.forward));
        }
        [Test]public void FreshPressDuringBlendUsesVisibleBasisThenHoldsIt()
        {
            var basis=Child("Camera").AddComponent<PootleMovementBasis>();basis.Sample(Vector2.up,Quaternion.identity,0);basis.BeginHandover(90,1.2f,0);
            basis.Sample(Vector2.zero,Quaternion.Euler(0,30,0),.3f);basis.Sample(Vector2.up,Quaternion.Euler(0,40,0),.4f);
            basis.Sample(Vector2.up,Quaternion.Euler(0,60,0),.6f);Assert.That(basis.Yaw,Is.EqualTo(40).Within(.01f));
        }
        [Test]public void RailProjectionClampsEndsAndAllowsOffRailPlayerMotion()
        {
            var shot=Child("Rail camera").AddComponent<PootleExplorationShot>();shot.lineStart=Vector3.zero;shot.lineEnd=Vector3.forward*20;
            Assert.That(shot.Project(new Vector3(50,9,-4)),Is.EqualTo(0));Assert.That(shot.Project(new Vector3(-50,9,24)),Is.EqualTo(1));
            Assert.That(shot.Project(new Vector3(8,90,8)),Is.EqualTo(.4f).Within(.001f));
            Assert.That(shot.EvaluateRail(3),Is.EqualTo(Vector3.forward*20+shot.railOffset));
        }
        [Test]public void StaticCameraDoesNotFollowAndBoundedModeNeverExceedsLimits()
        {
            var focus=Child("Focus").AddComponent<PootleCameraFocus>();var shot=Child("Shot").AddComponent<PootleExplorationShot>();
            shot.focus=focus;shot.mode=ExplorationCameraMode.StaticOrBounded;shot.anchor=new Vector3(4,20,-10);shot.TickShot(.1f);
            focus.transform.position=new Vector3(20,50,20);shot.TickShot(.1f);Assert.That(shot.transform.position,Is.EqualTo(shot.anchor));
            shot.bounded=true;shot.positionBounds=new Vector3(6,0,4);
            for(int i=0;i<100;i++)shot.TickShot(.1f);
            Assert.That(shot.transform.position.x,Is.InRange(1,7));Assert.That(shot.transform.position.y,Is.EqualTo(20));Assert.That(shot.transform.position.z,Is.InRange(-12,-8));
        }
        [Test]public void FocusIgnoresPlayerFacingAndSmoothsClimbingHeight()
        {
            var target=Child("Player").transform;var focus=Child("Focus").AddComponent<PootleCameraFocus>();focus.player=target;focus.lookahead=0;focus.Snap();
            Vector3 start=focus.transform.position;target.rotation=Quaternion.Euler(0,160,0);focus.Step(.1f);Assert.That(focus.transform.position,Is.EqualTo(start));
            target.position=Vector3.up*4;focus.Step(.1f);Assert.That(focus.transform.position.y,Is.GreaterThan(start.y).And.LessThan(5.5f));
            target.position=Vector3.right*40;focus.Step(.1f);Assert.That(focus.transform.position,Is.EqualTo(target.position+focus.offset));
        }
    }
}
