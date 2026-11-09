using UnityEngine;
using TMPro;
namespace OinkRomp
{
    public sealed class OinkRompHud : MonoBehaviour
    {
        public OinkRompController pig;
        public TMP_Text status,objective,dialogue,toast;
        public GameObject dialoguePanel;
        void Update()
        {
            if(!pig)return;
            status.text="Apples "+pig.Apples+"   ·   "+pig.AvailableBalloons+" / "+pig.BalloonCapacity+" balloons";
            var bird=pig.course.NearbyBird;
            dialoguePanel.SetActive(bird);
            if(bird)dialogue.text="<b>"+bird.speaker.Split('·')[0].Trim()+"</b>  "+bird.dialogue;
            // Instructions live with the birds; routine action feedback is visual and audible.
            if(toast)toast.text="";
        }
    }
}
