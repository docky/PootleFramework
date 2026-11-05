using System.Collections;
using System.Linq;
using NUnit.Framework;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.TestTools;

namespace Pootle.Tests
{
    public sealed partial class PootlePlayModeTests
    {
        [UnityTest]
        public IEnumerator PickupPolish_SlopeShadow_CoversUphillSideAboveFootHeight()
        {
            character.simulateAutomatically=false;
            foreach(var renderer in character.GetComponentsInChildren<Renderer>(true))renderer.forceRenderingOff=true;
            var slope=GameObject.CreatePrimitive(PrimitiveType.Cube);
            slope.transform.position=new Vector3(300,4,300);slope.transform.localScale=new Vector3(10,.2f,8);
            var red=new Material(Shader.Find("Universal Render Pipeline/Unlit"));red.SetColor("_BaseColor",Color.red);slope.GetComponent<Renderer>().sharedMaterial=red;
            var go=new GameObject("Uphill shadow camera");var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1;
            camera.transform.SetPositionAndRotation(new Vector3(300,12,300),Quaternion.LookRotation(Vector3.down,Vector3.forward));
            var rt=new RenderTexture(256,256,24);rt.Create();var pixels=new Texture2D(256,256,TextureFormat.RGBA32,false);
            var blob=character.GetComponent<PootleBlobShadow>();
            try
            {
                foreach(float angle in new[]{44f,-44f})
                {
                    slope.transform.rotation=Quaternion.Euler(0,0,angle);Physics.SyncTransforms();
                    character.Teleport(new Vector3(300,6,300),Quaternion.identity);
                    for(int i=0;i<120;i++)character.Tick(1f/60,default);
                    Assert.That(character.Support.found);Assert.That(character.Support.angle,Is.InRange(43,45));
                    blob.opacity=0;yield return null;yield return null;var baseline=ReadShadowView(camera,rt,pixels);
                    blob.opacity=.8f;yield return null;yield return null;var shaded=ReadShadowView(camera,rt,pixels);
                    int uphill=0,totalShaded=0;float maxY=-1000;
                    for(int y=0;y<256;y++)for(int x=0;x<256;x++)
                    {
                        float worldX=300+((x+.5f)/256*2-1);
                        float surfaceY=4+Mathf.Tan(angle*Mathf.Deg2Rad)*(worldX-300)+.1f/Mathf.Cos(angle*Mathf.Deg2Rad);
                        int at=y*256+x;
                        if(baseline[at].r>100 && baseline[at].r>baseline[at].g*2 && baseline[at].r>baseline[at].b*2 && baseline[at].r-shaded[at].r>20)
                        {totalShaded++;maxY=Mathf.Max(maxY,surfaceY);if(surfaceY>character.transform.position.y+.04f)uphill++;}
                    }
                    Assert.That(uphill,Is.GreaterThan(20),$"Shadow must cover uphill geometry above old cutoff at {angle}; root {character.transform.position}, normal {character.Support.normal}, mode {character.Mode}, total {totalShaded}, max shaded Y {maxY}");
                }
            }
            finally{camera.targetTexture=null;rt.Release();Object.Destroy(rt);Object.Destroy(pixels);Object.Destroy(go);Object.Destroy(slope);Object.Destroy(red);}
        }
        [UnityTest]
        public IEnumerator PickupPolish_LocomotionAnimation_JogsTwiceRunsTripleAndLeavesAirborneAtNormalRate()
        {
            character.simulateAutomatically=false;character.Teleport(new Vector3(150,.03f,280),Quaternion.identity);
            var animator=character.GetComponent<PootleAvatar>().animator;
            for(int i=0;i<30;i++)character.Tick(1f/60,new PootleInputFrame{move=Vector2.up});
            yield return null;yield return null;
            Assert.That(animator.GetFloat("LocomotionRate"),Is.EqualTo(2));
            for(int i=0;i<30;i++)character.Tick(1f/60,new PootleInputFrame{move=Vector2.up,run=true});
            yield return null;yield return null;
            Assert.That(character.IsRunning);Assert.That(animator.GetFloat("LocomotionRate"),Is.EqualTo(3));
            character.Tick(1f/60,new PootleInputFrame{jumpPressed=true,jumpHeld=true});yield return null;yield return null;
            Assert.That(animator.GetFloat("LocomotionRate"),Is.EqualTo(1));Assert.That(animator.speed,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator PickupPolish_Collectibles_TwentyFloatSpinAndPickupUpdatesOnlyAssignedSoapIntOnce()
        {
            character.simulateAutomatically=false;
            var items=Object.FindObjectsByType<PootleCollectible>();Assert.That(items.Length,Is.EqualTo(20));
            Assert.That(items.Select(i=>i.counter).Distinct().Count(),Is.EqualTo(3));
            Assert.That(items.Count(i=>i.name.EndsWith("Coin")),Is.EqualTo(12));
            var coin=items.First(i=>i.name.EndsWith("Coin"));var shadow=coin.GetComponent<PootlePropBlobShadow>();
            Assert.That(shadow.shape,Is.EqualTo(PootlePropBlobShadow.ShadowShape.RoundedRectangle));
            var position=coin.transform.position;var rotation=coin.transform.rotation;
            yield return new WaitForSeconds(.15f);
            Assert.That(Vector3.Distance(position,coin.transform.position),Is.GreaterThan(.001f));
            Assert.That(Quaternion.Angle(rotation,coin.transform.rotation),Is.GreaterThan(5));Assert.That(shadow.ProjectionActive);
            var counters=items.Select(i=>i.counter).Distinct().ToDictionary(c=>c,c=>c.Value);
            character.Teleport(coin.transform.position-Vector3.up,Quaternion.identity);Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();yield return null;
            Assert.That(coin.Collected,Is.True,"Actual player trigger contact collects it");
            Assert.That(coin.counter.Value,Is.EqualTo(counters[coin.counter]+coin.amount));
            Assert.That(coin.TryCollect(character),Is.False);Assert.That(shadow.ProjectionActive,Is.False);
            foreach(var pair in counters)if(!ReferenceEquals(pair.Key,coin.counter))Assert.That(pair.Key.Value,Is.EqualTo(pair.Value));
            var star=items.First(i=>i.name.EndsWith("Star"));int before=star.counter.Value;
            Assert.That(star.TryCollect(null),Is.False);Assert.That(star.counter.Value,Is.EqualTo(before));
            Assert.That(star.TryCollect(character));Assert.That(star.counter.Value,Is.EqualTo(before+1));
        }
    }
}
