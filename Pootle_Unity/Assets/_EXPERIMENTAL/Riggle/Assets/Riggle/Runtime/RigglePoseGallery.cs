using UnityEngine;
namespace Docky.RiggleTools
{
    public sealed class RigglePoseGallery : MonoBehaviour
    {
        public Riggle source;
        [Min(2)] public float spacing = 2.6f;
        public bool visible = true;
        public Transform generatedRow;
        public Transform correctiveRow;
        public bool compareCorrectives = true;
        public void ApplyVisibility()
        {
            bool compare = compareCorrectives && correctiveRow;
            if (generatedRow) generatedRow.gameObject.SetActive(visible && !compare);
            if (correctiveRow) correctiveRow.gameObject.SetActive(visible && compare);
        }
        void OnEnable() => ApplyVisibility();
    }
}
