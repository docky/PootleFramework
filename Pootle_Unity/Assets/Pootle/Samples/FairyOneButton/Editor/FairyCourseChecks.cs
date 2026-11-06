using System.Collections.Generic;
using UnityEngine;
namespace Pootle.FairyDemo.Editor
{
    public static class FairyCourseChecks
    {
        public static string Run()
        {
            var course=Object.FindAnyObjectByType<FairyCourse>();var f=course.fairy;f.readDevices=false;
            var lines=new List<string>();void Check(bool pass,string label)=>lines.Add((pass?"PASS ":"FAIL ")+label);
            course.ResetCourse();Check(course.collectibles.Length==18&&course.hoops.Length==5,"18 seeds and five authored hoops");
            var seed=course.collectibles[0];Vector3 p=seed.transform.position;
            course.EvaluateSegment(p-Vector3.right,p+Vector3.right);Check(seed.Collected&&course.Collected==1,"swept seed pickup without button");
            course.EvaluateSegment(p-Vector3.right,p+Vector3.right);Check(course.Collected==1,"seed scores only once");
            course.ResetCourse();var hoop=course.hoops[0];Vector3 centre=hoop.transform.position,forward=hoop.transform.forward;
            Check(!hoop.TryCross(centre,centre,.41f),"standing in hoop plane does not score");
            Check(!hoop.TryCross(centre+hoop.transform.right*2-forward,centre+hoop.transform.right*2+forward,.41f),"crossing outside hoop aperture does not score");
            course.EvaluateSegment(centre-forward,centre+forward);Check(hoop.Cleared&&course.HoopsCleared==1,"passing through aperture scores and blooms");
            course.EvaluateSegment(centre+forward,centre-forward);Check(course.HoopsCleared==1,"repeated hoop passage cannot score twice");
            course.ResetCourse();f.Character.Teleport(p-Vector3.up*.41f,Quaternion.identity);course.SendMessage("LateUpdate");Check(seed.Collected,"normal post-movement update collects a nearby seed");
            int collected=course.Collected;f.Character.Teleport(p+Vector3.right*10-Vector3.up*.41f,Quaternion.identity);course.SendMessage("LateUpdate");Check(course.Collected==collected,"teleport does not sweep across collectibles");
            course.resetButton.onClick.Invoke();course.SendMessage("LateUpdate");Check(course.Collected==0&&course.HoopsCleared==0&&!seed.Collected&&!hoop.Cleared,"UI reset restores all progress and visuals");
            Check(f.Character.Velocity==Vector3.zero&&f.Windup==0&&f.Flaps==2&&f.Speech=="","reset clears flight and dialogue state");
            var sign=course.signs[0];f.Character.Teleport(sign.transform.position+Vector3.right*.6f+Vector3.up*.02f,Quaternion.identity);
            for(int i=0;i<30;i++)f.Step(1f/60,Vector2.zero,false);course.UpdateSigns();Check(course.NearbySign==sign&&course.signPanel.gameObject.activeSelf,"nearby sign reveals text automatically");
            f.Step(1f/60,Vector2.zero,true);Check(f.Windup>0,"sign text does not consume the launch button");
            f.Character.Teleport(new Vector3(0,.02f,-3.5f),Quaternion.identity);course.UpdateSigns();Check(!course.signPanel.gameObject.activeSelf,"sign text disappears outside radius");
            course.ResetCourse();foreach(var item in course.collectibles)course.EvaluateSegment(item.transform.position,item.transform.position);
            foreach(var ring in course.hoops)course.EvaluateSegment(ring.transform.position-ring.transform.forward,ring.transform.position+ring.transform.forward);
            Check(course.Complete&&course.finishBlossom.activeSelf&&course.counter.text=="Grove complete!","all seeds and hoops complete the grove");
            var reset=System.Array.Find(Object.FindObjectsByType<FairyTarget>(),t=>t.kind==FairyTargetKind.Reset);
            f.Character.Teleport(reset.focusPoint.position+Vector3.back*.6f+Vector3.up*.02f,Quaternion.identity);for(int i=0;i<35;i++)f.Step(1f/60,Vector2.zero,false);
            Check(f.Focus==reset,"garden reset acquires a one-button prompt");f.Step(1f/60,Vector2.zero,true);course.SendMessage("LateUpdate");Check(course.Collected==0&&!course.Complete,"garden reset executes after movement and restores course");
            // Drop onto both side shelves using the actual Pootle motor.
            foreach(float x in new[]{-8f,8f})
            {
                f.Character.Teleport(new Vector3(x,1,-.5f),Quaternion.identity);for(int i=0;i<120;i++)f.Step(1f/60,Vector2.zero,false);
                Check(f.ConfirmedGround&&Mathf.Abs(f.transform.position.y+.7f)<.08f,$"catch shelf at x={x} supports a fall and restores flaps");
            }
            course.ResetCourse();f.readDevices=true;string result=string.Join("\n",lines);Debug.Log("Fairy course checks\n"+result);return result;
        }
        public static string FlyRoutes()
        {
            var course=Object.FindAnyObjectByType<FairyCourse>();var f=course.fairy;var c=f.Character;f.readDevices=false;
            var lines=new List<string>();const float dt=1f/60;
            Vector3 forward=Vector3.ProjectOnPlane(c.outputCamera.forward,Vector3.up).normalized;Vector3 right=Vector3.Cross(Vector3.up,forward);
            void Step(Vector2 stick,bool held=false){var before=f.transform.position+Vector3.up*.41f;f.Step(dt,stick,held);course.EvaluateSegment(before,f.transform.position+Vector3.up*.41f);}
            Vector3[][] routes={new[]{new Vector3(-.4f,.8f,0),new Vector3(-1.8f,.8f,.45f),new Vector3(-2.3f,1.6f,2.4f),new Vector3(-1.9f,2.35f,3.9f),new Vector3(0,2.9f,5.1f)},new[]{new Vector3(-.4f,.8f,0),new Vector3(2.5f,1.7f,1.2f),new Vector3(2,2.35f,3.4f),new Vector3(0,2.9f,5.1f)}};
            for(int branch=0;branch<routes.Length;branch++)
            {
                course.ResetCourse();c.Teleport(new Vector3(0,.02f,-1.8f),Quaternion.identity);
                foreach(var target in routes[branch])
                {
                    for(int i=0;i<35;i++)Step(Vector2.zero);
                    Step(Vector2.zero,true);Step(Vector2.zero);Step(Vector2.zero,true);
                    bool reached=false,wasAir=false;int flapCooldown=0;
                    for(int i=0;i<240;i++)
                    {
                        Vector3 delta=Vector3.ProjectOnPlane(target-f.transform.position,Vector3.up);float distance=delta.magnitude;
                        var input=new Vector2(Vector3.Dot(delta,right),Vector3.Dot(delta,forward)).normalized;
                        if(distance<.42f)input=Vector2.zero;
                        bool flap=!f.ConfirmedGround&&c.Velocity.y<.2f&&f.transform.position.y<target.y+.55f&&f.Flaps>0&&flapCooldown<=0;
                        Step(input,flap);flapCooldown=flap?15:flapCooldown-1;
                        wasAir|=!f.ConfirmedGround;
                        if(wasAir&&f.ConfirmedGround){reached=distance<.8f&&Mathf.Abs(f.transform.position.y-target.y)<.1f;break;}
                    }
                    lines.Add((reached?"PASS ":"FAIL ")+$"branch {branch+1} landing {target}, actual {f.transform.position}");
                    if(!reached)break;
                }
            }
            course.ResetCourse();f.readDevices=true;return string.Join("\n",lines);
        }
    }
}
