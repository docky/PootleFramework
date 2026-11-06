using UnityEngine;
using Sirenix.OdinInspector;

namespace Pootle
{
    // All interior geometry, camera volumes and arrival markers are children of this root.
    public sealed class PootleCaveNetwork : MonoBehaviour
    {
        public Vector3 hiddenOffset = new Vector3(0, 100, 0);
        [InfoBox("Interior coordinates match the island. Move this entire root to the origin to inspect alignment; restore Y=100 before playing.")]
        public Transform chamberDrop;
        [Button("Inspect alignment · move root to origin")]
        public void InspectAlignment() { if (!Application.isPlaying) MoveRoot(Vector3.zero); }
        [Button("Restore hidden location · Y +100")]
        public void RestoreHiddenLocation() { if (!Application.isPlaying) MoveRoot(hiddenOffset); }
        void MoveRoot(Vector3 point)
        {
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(transform, "Move cave network");
#endif
            transform.position = point;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }
    }
}
