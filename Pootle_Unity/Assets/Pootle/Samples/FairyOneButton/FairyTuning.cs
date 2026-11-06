using UnityEngine;
namespace Pootle.FairyDemo
{
    [CreateAssetMenu(menuName="Pootle/Fairy Flight Tuning")]
    public sealed class FairyTuning : ScriptableObject
    {
        public float airSpeed=4.2f, airAcceleration=16, airBraking=24;
        public float launchSpeed=4.8f, flapBoost=3.1f, flapCap=5.2f, minimumLift=.5f;
        public int flapCount=2;
        public float settleTime=.12f, settleSpeed=.20f, doubleTapWindow=.55f, windupRadius=.16f;
    }
}
