using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
 public static class PootleBlobShadowSetup
 {
  [MenuItem("Pootle/Add Body Blob Shadow")]
  public static void Apply()
  {
   if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode first");
   string path="Assets/Pootle/Samples/Materials/Body blob shadow.mat";
   var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(!mat){mat=new Material(Shader.Find("Pootle/Blob Shadow"));AssetDatabase.CreateAsset(mat,path);}
   var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
   if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
   foreach(var c in Object.FindObjectsByType<PootleCharacter>()){var blob=c.GetComponent<PootleBlobShadow>()??c.gameObject.AddComponent<PootleBlobShadow>();blob.character=c;blob.material=mat;EditorUtility.SetDirty(blob);}
   string prefab="Assets/Pootle/Samples/PootleCharacter.prefab";
   var root=PrefabUtility.LoadPrefabContents(prefab);
   try{var blob=root.GetComponent<PootleBlobShadow>()??root.AddComponent<PootleBlobShadow>();blob.character=root.GetComponent<PootleCharacter>();blob.material=mat;PrefabUtility.SaveAsPrefabAsset(root,prefab);}finally{PrefabUtility.UnloadPrefabContents(root);}
   var profile=AssetDatabase.LoadAssetAtPath<PootleProfile>("Assets/Pootle/Data/PootleDefault.asset");profile.runSpeed=12;EditorUtility.SetDirty(profile);
   foreach(string asset in new[]{"Assets/Settings/PC_RPAsset.asset","Assets/Settings/Mobile_RPAsset.asset"})
   {
    var pipeline=AssetDatabase.LoadMainAssetAtPath(asset);var settings=new SerializedObject(pipeline);
    settings.FindProperty("m_RequireDepthTexture").boolValue=true;settings.ApplyModifiedPropertiesWithoutUndo();
   }
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
  }
 }
}
