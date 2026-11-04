using NUnit.Framework;
using UnityEngine;
namespace Pootle.Tests
{
    public sealed partial class PootleCharacterTests
    {
        private PootleMovementFeedback MovementFeedback()
        {
            var feedback=character.gameObject.AddComponent<PootleMovementFeedback>();
            feedback.stepDust=FeedbackParticles("Steps");feedback.jumpDust=FeedbackParticles("Jump");feedback.landingDust=FeedbackParticles("Land");
            // EditMode does not invoke the runtime MonoBehaviour lifecycle.
            typeof(PootleMovementFeedback).GetMethod("OnEnable",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(feedback,null);
            return feedback;
        }
        private ParticleSystem FeedbackParticles(string name)
        {
            var go=new GameObject(name);objects.Add(go);var ps=go.AddComponent<ParticleSystem>();
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.startLifetime=10;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.enabled=false;return ps;
        }
        private void FeedbackStep(PootleMovementFeedback feedback,int count,PootleInputFrame frame=default)
        {for(int i=0;i<count;i++){Step(1,frame);feedback.Tick(Dt);frame.jumpPressed=false;}}
        [Test]
        public void MovementFeedback_WalkStepsButIdleBlockedAndRollingAreQuiet()
        {
            var feedback=MovementFeedback();FeedbackStep(feedback,60);
            Assert.That(feedback.stepDust.particleCount,Is.Zero);
            FeedbackStep(feedback,40,new PootleInputFrame{move=Vector2.up});
            Assert.That(feedback.stepDust.particleCount,Is.GreaterThan(0));
            ResetAt(new Vector3(0,.02f,0));Box("Step blocker",new Vector3(0,2,.7f),new Vector3(10,4,.2f));
            FeedbackStep(feedback,60,new PootleInputFrame{move=Vector2.up});
            Assert.That(feedback.stepDust.particleCount,Is.Zero);
            ResetAt(new Vector3(5,.02f,-5));Step(2);Step(1,new PootleInputFrame{rollPressed=true});
            FeedbackStep(feedback,60,new PootleInputFrame{move=Vector2.right});
            Assert.That(feedback.stepDust.particleCount,Is.Zero);
        }
        [Test]
        public void MovementFeedback_ConveyorDoesNotMakeFootsteps()
        {
            var deck=Box("Quiet belt",new Vector3(0,.15f,0),new Vector3(6,.3f,20));deck.AddComponent<PootleConveyor>().speed=3;
            ResetAt(new Vector3(0,.32f,0));Step(3);var feedback=MovementFeedback();
            FeedbackStep(feedback,90);Assert.That(character.transform.position.z-origin.z,Is.GreaterThan(4));
            Assert.That(feedback.stepDust.particleCount,Is.Zero);
        }
        [Test]
        public void MovementFeedback_JumpDoubleJumpLandingAndResetEmitOnce()
        {
            profile.additionalAirJumps=1;var feedback=MovementFeedback();
            FeedbackStep(feedback,12,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(feedback.jumpDust.particleCount,Is.EqualTo(9));
            FeedbackStep(feedback,12,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(feedback.jumpDust.particleCount,Is.EqualTo(18));
            FeedbackStep(feedback,180);int landed=feedback.landingDust.particleCount;
            Assert.That(landed,Is.InRange(8,24));FeedbackStep(feedback,30);
            Assert.That(feedback.landingDust.particleCount,Is.EqualTo(landed));
            ResetAt(new Vector3(0,.02f,0));Assert.That(feedback.jumpDust.particleCount+feedback.landingDust.particleCount,Is.Zero);
            FeedbackStep(feedback,3);Assert.That(feedback.landingDust.particleCount,Is.Zero,"Spawn snap should be silent");
        }
        [TestCase(false)] [TestCase(true)]
        public void MovementFeedback_RailAndPoleLandingHaveContactFeedback(bool pole)
        {
            if(pole){var p=Pole(new Vector3(0,2,0),4);p.standOnTop=true;p.allowExitTop=true;}
            else Rail(new Vector3(-3,4,0),new Vector3(3,4,0));
            var feedback=MovementFeedback();ResetAt(new Vector3(0,7,0));
            FeedbackStep(feedback,90);
            Assert.That(character.Mode,Is.EqualTo(pole?PootleMode.PoleStanding:PootleMode.RailWalking));
            Assert.That(feedback.landingDust.particleCount,Is.InRange(8,24));
        }
    }
}
