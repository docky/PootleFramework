using UnityEngine;
namespace Pootle
{
    // Resolve world-space suspension lines after the visual view-tilt pivot.
    [DisallowMultipleComponent, DefaultExecutionOrder(10010)]
    public sealed class PootleGliderVisual : MonoBehaviour
    {
        public PootleCharacter character;
        public Transform gliderRoot;
        public AudioClip openSound, closeSound;
        public AudioSource audioSource;
        public Animator animator;
        public LineRenderer[] suspensionLines;
        [Min(0.01f)] public float deployTime = 0.18f;
        private Vector3 scale = Vector3.one;
        private float amount;
        private bool open;
        private void Awake()
        {
            if (!character) character = GetComponentInParent<PootleCharacter>();
            if (gliderRoot) { scale = gliderRoot.localScale; gliderRoot.localScale = Vector3.zero; }
        }
        private void LateUpdate()
        {
            if (!character || !gliderRoot) return;
            bool next = character.Mode == PootleMode.Gliding;
            if (next != open && audioSource)
            {
                var clip = next ? openSound : closeSound;
                if (clip) audioSource.PlayOneShot(clip);
            }
            open = next;
            amount = Mathf.MoveTowards(amount, open ? 1 : 0, Time.deltaTime / Mathf.Max(0.01f, deployTime));
            gliderRoot.localScale = scale * Mathf.SmoothStep(0, 1, amount);
            if (animator && animator.isHuman && suspensionLines != null)
                for (int i=0;i<suspensionLines.Length;i++)
                {
                    var line=suspensionLines[i]; if (!line) continue;
                    line.enabled=amount>0.05f;
                    var hand=animator.GetBoneTransform(i==0?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                    if (!hand) continue;
                    line.SetPosition(0,hand.position);
                    line.SetPosition(1,gliderRoot.TransformPoint(new Vector3(i==0?-.6f:.6f,.05f,0)));
                }
        }
        private void OnDisable()
        {
            if (gliderRoot) gliderRoot.localScale = Vector3.zero;
            if (suspensionLines != null) foreach (var line in suspensionLines) if (line) line.enabled = false;
            amount = 0; open = false;
        }
    }
}
