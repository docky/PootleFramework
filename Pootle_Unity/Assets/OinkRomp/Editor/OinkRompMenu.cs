using UnityEditor;
using UnityEditor.SceneManagement;
public static class OinkRompMenu
{
    [MenuItem("Pootle/OinkRomp/Open OinkRomp")]
    public static void Open()
    {
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            EditorSceneManager.OpenScene("Assets/OinkRomp/OinkRomp.unity");
    }
}
