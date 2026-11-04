using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace Pootle.Editor
{
    public static class PootleSlideRideSetup
    {
        [MenuItem("Pootle/Configure Momentum Slide and Follow Camera")]
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode before saving slide settings.");
            string path="Assets/Pootle/Data/PootleControls.inputactions";
            var actions=Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(path));
            var map=actions.FindActionMap("Camera",true);
            var action=map.FindAction("Style7")??map.AddAction("Style7",InputActionType.Button);
            if(!action.bindings.Any(b=>b.path=="<Keyboard>/7"))action.AddBinding("<Keyboard>/7");
            File.WriteAllText(path,actions.ToJson());Object.DestroyImmediate(actions);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(PootleSceneSetup.SampleScene);
            if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(PootleSceneSetup.SampleScene);
            DeepenBanks();
            var floor=GameObject.Find("Environment course floor");
            if(floor){floor.transform.position=new Vector3(60,-.5f,4);floor.transform.localScale=new Vector3(80,1,112);}
            foreach(var camera in Object.FindObjectsByType<PootleCameraStyles>())
            {camera.automaticallyFollowSlides=true;EditorUtility.SetDirty(camera);}
            foreach(var slide in Object.FindObjectsByType<PootleSlideSurface>())EditorUtility.SetDirty(slide);
            foreach(var text in Object.FindObjectsByType<TextMesh>())
                if(text.text.StartsWith("SPIRAL SLIDE"))
                {text.text="MOMENTUM SLIDE\nRide the lift · lean to steer · pull back to brake\n7 Follow cam · jump to leave the slide";EditorUtility.SetDirty(text);}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        private static void DeepenBanks()
        {
            var slide=GameObject.Find("Spiral slide · slippery trough");if(!slide)return;
            var mesh=slide.GetComponent<MeshFilter>().sharedMesh;var vertices=mesh.vertices;
            const int sections=640,across=32,runout=80,width=33,total=720;
            if(vertices.Length!=(total+1)*width*2)throw new System.InvalidOperationException("Unexpected spiral mesh layout.");
            var left=new Vector3[total+1];var right=new Vector3[total+1];var normals=new Vector3[vertices.Length];
            for(int i=0;i<=total;i++)
            {
                float centerY=vertices[(i*width+across/2)*2].y;
                float rim=i<=sections?1:Mathf.Pow(1-(i-sections)/(float)runout,2);
                for(int j=0;j<=across;j++)
                {
                    float x=j/(float)across*2-1;int index=(i*width+j)*2;
                    vertices[index].y=centerY+3.2f*x*x*rim;vertices[index+1]=vertices[index]-Vector3.up*.2f;
                }
                left[i]=vertices[i*width*2]+Vector3.up*.03f;right[i]=vertices[(i*width+across)*2]+Vector3.up*.03f;
            }
            // Keep the top normals continuous at the rim; bottom/side triangles should
            // not contaminate the bank's interpolated support normal.
            for(int i=0;i<=total;i++)for(int j=0;j<=across;j++)
            {
                Vector3 along=vertices[(Mathf.Min(i+1,total)*width+j)*2]-vertices[(Mathf.Max(i-1,0)*width+j)*2];
                Vector3 acrossVector=vertices[(i*width+Mathf.Min(j+1,across))*2]-vertices[(i*width+Mathf.Max(j-1,0))*2];
                int index=(i*width+j)*2;normals[index]=Vector3.Cross(along,acrossVector).normalized;normals[index+1]=-normals[index];
            }
            mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var collider=slide.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;
            slide.transform.Find("Inner rim").GetComponent<LineRenderer>().SetPositions(left);
            slide.transform.Find("Outer rim").GetComponent<LineRenderer>().SetPositions(right);
        }
    }
}
