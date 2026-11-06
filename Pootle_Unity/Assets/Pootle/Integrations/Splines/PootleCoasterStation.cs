using UnityEngine;
using TMPro;
namespace Pootle
{
    [AddComponentMenu("Pootle/Coaster Station")]
    public sealed class PootleCoasterStation : MonoBehaviour, IPootleIntentReceiver
    {
        public PootleRollercoaster coaster;
        public PootleIntentGate gate;
        public Transform trackAnchor, boardingPoint, exitPoint;
        public Renderer[] countdownLights = new Renderer[3];
        public TMP_Text statusSign;
        public float Distance { get; internal set; }
        MaterialPropertyBlock block;
        public bool TryAccept(PootleCharacter character, PootleIntentGate source) => coaster && coaster.RequestBoard(this, character);
        public void Signal(string message, int lights = 0)
        {
            if (statusSign) statusSign.text = message;
            block ??= new MaterialPropertyBlock();
            for (int i = 0; i < countdownLights.Length; i++)
            {
                if (!countdownLights[i]) continue;
                Color c = i == 0 ? new Color(1, .16f, .08f) : i == 1 ? new Color(1, .65f, .03f) : new Color(.2f, 1, .45f);
                c *= i < lights ? 1 : .15f;
                block.SetColor("_BaseColor", c); block.SetColor("_EmissionColor", c * (i < lights ? 1.2f : 0));
                countdownLights[i].SetPropertyBlock(block);
            }
        }
    }
}
