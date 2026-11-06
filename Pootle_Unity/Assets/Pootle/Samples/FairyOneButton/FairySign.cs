using UnityEngine;
namespace Pootle.FairyDemo
{
    public sealed class FairySign : MonoBehaviour
    {
        [TextArea(1,3)]public string message;
        public float revealDistance=1.7f;
        public Transform glow;
        public void Show(bool shown){if(glow)glow.gameObject.SetActive(shown);}
    }
}
