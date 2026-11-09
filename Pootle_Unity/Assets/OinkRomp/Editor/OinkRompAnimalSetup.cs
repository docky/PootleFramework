using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using OinkRomp;

public static class OinkRompAnimalSetup
{
    const string Folder="Assets/OinkRomp/Animations";
    public static void UseIdleDefault(Animator animator)
    {
        if(!animator || !animator.runtimeAnimatorController)return;
        var source=animator.runtimeAnimatorController as AnimatorController;
        if(!source)throw new InvalidOperationException("Expected an AnimatorController for "+animator.name);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/OinkRomp","Animations");
        string path=Folder+"/"+source.name+".controller";
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(!controller)
        {
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source),path))throw new InvalidOperationException("Could not copy "+source.name);
            controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        }
        var machine=controller.layers[0].stateMachine;
        var idle=machine.states.Select(s=>s.state).FirstOrDefault(s=>s.name=="Idle_A");
        if(!idle)throw new InvalidOperationException("Missing Idle_A in "+controller.name);
        machine.defaultState=idle;
        EditorUtility.SetDirty(machine);EditorUtility.SetDirty(controller);
        animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;
        EditorUtility.SetDirty(animator);
        if(PrefabUtility.IsPartOfPrefabInstance(animator))PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
    }
    public static string ApplyToScene()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit mode only.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/OinkRomp/OinkRomp.unity")throw new InvalidOperationException("Open OinkRomp first.");
        foreach(var bird in UnityEngine.Object.FindObjectsByType<OinkRompBird>())UseIdleDefault(bird.animator);
        UseIdleDefault(UnityEngine.Object.FindAnyObjectByType<OinkRompController>().animator);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "OinkRomp animal controllers now default to Idle_A.";
    }
}
