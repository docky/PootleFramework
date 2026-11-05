using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Pootle.Tests
{
 public sealed partial class PootlePlayModeTests
 {
  static Color32[] ReadShadowView(Camera camera,RenderTexture rt,Texture2D pixels)
  {
   var old=RenderTexture.active;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();RenderTexture.active=old;return pixels.GetPixels32();
  }
  [UnityTest]
  public IEnumerator BlobProjection_RendersOnThinColliderlessStraightAndCurvedRails()
  {
   character.simulateAutomatically=false;var blob=character.GetComponent<PootleBlobShadow>();
   character.Teleport(new Vector3(150,3.05f,280),Quaternion.identity);
   foreach(var r in character.GetComponentsInChildren<Renderer>(true))r.forceRenderingOff=true;
   var go=new GameObject("Shadow pixel camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1;
   camera.transform.SetPositionAndRotation(new Vector3(150,10,280),Quaternion.LookRotation(Vector3.down,Vector3.forward));
   var rt=new RenderTexture(256,256,24);rt.Create();var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);
   var mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));mat.SetColor("_BaseColor",Color.red);
   GameObject rail=null;Mesh curved=null;
   try
   {
    for(int variant=0;variant<3;variant++)
    {
     if(variant!=1)
     {
      rail=GameObject.CreatePrimitive(PrimitiveType.Cube);rail.transform.position=new Vector3(150,2.95f,280);rail.transform.localScale=new Vector3(.08f,.1f,4);
      if(variant==2){rail.transform.position=new Vector3(150.5f,2.5f,280);rail.transform.localScale=new Vector3(1,1,2);}
      rail.GetComponent<Collider>().enabled=false;rail.GetComponent<Renderer>().sharedMaterial=mat;
     }
     else
     {
      rail=new GameObject("Colliderless curved rail");rail.transform.position=new Vector3(149.3f,3,280);
      var v=new Vector3[130];var tris=new int[64*6];
      for(int i=0;i<=64;i++){float a=Mathf.Lerp(-1.7f,1.7f,i/64f);for(int side=0;side<2;side++)v[i*2+side]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(.67f+side*.06f);}
      for(int i=0;i<64;i++){int k=i*6,j=i*2;tris[k]=j;tris[k+1]=j+2;tris[k+2]=j+1;tris[k+3]=j+1;tris[k+4]=j+2;tris[k+5]=j+3;}
      curved=new Mesh();curved.vertices=v;curved.triangles=tris;curved.RecalculateNormals();curved.RecalculateBounds();
      rail.AddComponent<MeshFilter>().sharedMesh=curved;rail.AddComponent<MeshRenderer>().sharedMaterial=mat;
     }
     blob.opacity=0;yield return null;yield return null;var baseline=ReadShadowView(camera,rt,pixels);
     blob.opacity=.8f;yield return null;yield return null;var shadow=ReadShadowView(camera,rt,pixels);
     int shadedRail=0;for(int i=0;i<baseline.Length;i++)if(baseline[i].r>160 && baseline[i].g<30 && baseline[i].r-shadow[i].r>20)shadedRail++;
     Assert.That(shadedRail,Is.GreaterThan(50),"Rendered rail variant "+variant+" should receive the blob without a collider");
     if(variant==2)
     {
      int nearEdge=0;for(int y=108;y<148;y++)for(int x=129;x<133;x++){int i=y*256+x;if(baseline[i].r>160 && baseline[i].g<30 && baseline[i].r-shadow[i].r>20)nearEdge++;}
      Assert.That(nearEdge,Is.GreaterThan(20),"Shadow reaches the actual step edge without a grid-sized gap");
     }
     Object.Destroy(rail);yield return null;
    }
    character.Teleport(new Vector3(150,50,280),Quaternion.identity);yield return null;
    blob.opacity=0;yield return null;yield return null;var farBaseline=ReadShadowView(camera,rt,pixels);
    blob.opacity=.8f;yield return null;yield return null;var far=ReadShadowView(camera,rt,pixels);
    int changed=0;for(int i=0;i<far.Length;i++)if(Mathf.Abs(far[i].r-farBaseline[i].r)>2)changed++;
    Assert.That(changed,Is.Zero,"No shadow outside projection depth");
   }
   finally{camera.targetTexture=null;rt.Release();Object.Destroy(go);Object.Destroy(rt);Object.Destroy(pixels);Object.Destroy(rail);Object.Destroy(curved);Object.Destroy(mat);}
  }
  [UnityTest]
  public IEnumerator BlobProjection_BallUsesOneCircleAndSuppressesAllModelShadows()
  {
   character.simulateAutomatically=false;character.Teleport(new Vector3(150,.03f,280),Quaternion.identity);
   character.Tick(1f/60,new PootleInputFrame{rollPressed=true});yield return null;yield return null;
   Assert.That(character.IsRolling);
   var blob=character.GetComponent<PootleBlobShadow>();Assert.That(blob.CircleCount,Is.EqualTo(1));
   var ball=character.GetComponent<PootleBallVisual>().ball;
   foreach(var renderer in ball.GetComponentsInChildren<Renderer>())Assert.That(renderer.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
   character.Tick(1f/60,new PootleInputFrame{rollPressed=true});yield return null;
   Assert.That(blob.CircleCount,Is.GreaterThan(12));
  }
 }
}
