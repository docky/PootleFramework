using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator MovementFeedbackScenePlaysBorrowedAudioAndParticles()
        {
            var feedback=character.GetComponent<PootleMovementFeedback>();
            Assert.That(feedback,Is.Not.Null);Assert.That(feedback.jumps.Length,Is.EqualTo(5));
            foreach(var clip in feedback.jumps)Assert.That(clip && clip.samples>0);
            foreach(var clip in feedback.footsteps)Assert.That(clip && clip.samples>0);
            Assert.That(feedback.stepDust.GetComponent<Renderer>().sharedMaterial.shader.isSupported);
            character.simulateAutomatically=false;character.Teleport(new Vector3(0,.03f,-4),Quaternion.identity);
            for(int i=0;i<3;i++)character.Tick(1f/60,default);
            character.Tick(1f/60,new PootleInputFrame{jumpPressed=true,jumpHeld=true});
            Assert.That(feedback.voiceSource.isPlaying);Assert.That(feedback.jumpDust.particleCount,Is.EqualTo(9));
            yield return new WaitForSeconds(.03f);
            Assert.That(feedback.voiceSource.isPlaying);
            for(int i=0;i<180 && !character.IsGrounded;i++){character.Tick(1f/60,default);feedback.Tick(1f/60);}
            Assert.That(character.IsGrounded);Assert.That(feedback.feetSource.isPlaying);
            Assert.That(feedback.landingDust.particleCount,Is.InRange(8,24));
            yield return new WaitForSeconds(1);
            Assert.That(feedback.jumpDust.particleCount+feedback.landingDust.particleCount,Is.Zero,"Puffs must expire");
            character.Teleport(new Vector3(0,.03f,-4),Quaternion.identity);
            Assert.That(feedback.voiceSource.isPlaying || feedback.feetSource.isPlaying,Is.False);
            character.simulateAutomatically=true;
        }
    }
}
