using System;
using System.Collections.Generic;
using UnityEngine;
namespace Pootle.FairyDemo.Editor
{
    // Run against the saved scene in Play Mode through Pipeline, without requiring synthetic OS input.
    public static class FairyPlayChecks
    {
        public static string Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Run in Play Mode.");
            var f=UnityEngine.Object.FindAnyObjectByType<FairyController>();f.readDevices=false;f.RefreshTargets();
            var lines=new List<string>();var c=f.Character;float dt=1f/60;
            void Check(bool condition,string name){lines.Add((condition?"PASS ":"FAIL ")+name);}
            void Step(int count,Vector2 stick=default,bool held=false){for(int i=0;i<count;i++)f.Step(dt,stick,held);}
            void Reset(Vector3 position){c.Teleport(position,Quaternion.identity);Step(40);}
            void Tap(){f.Step(dt,Vector2.zero,true);f.Step(dt,Vector2.zero,false);}
            void Launch(){Tap();Step(5);Tap();}
            Reset(new Vector3(0,.02f,-3.5f));Check(f.ConfirmedGround&&f.CurrentAction==FairyAction.Launch,"direct scene spawn settles");
            Tap();Check(f.Windup>0&&c.IsGrounded,"first tap winds up");
            Reset(new Vector3(0,.02f,-3.5f));int launches=f.LaunchCount;Step(45,default,true);Check(f.LaunchCount==launches&&c.IsGrounded,"held button cannot repeat launch");
            Step(1);Reset(new Vector3(0,.02f,-3.5f));Launch();Check(!c.IsGrounded&&f.LaunchCount==launches+1,"released second tap launches");
            int flaps=f.Flaps;float cap=f.tuning.flapCap;f.tuning.flapCap=c.Velocity.y+.25f;Tap();Check(f.Flaps==flaps,"fast-rise flap preserves charge");f.tuning.flapCap=cap;
            Step(25);Tap();Check(f.Flaps==1,"first accepted flap consumes one charge");Step(22);Tap();Check(f.Flaps==0,"second accepted flap consumes last charge");Step(20);float v=c.Velocity.y;Tap();Check(f.Flaps==0&&c.Velocity.y<v,"empty flap does not lift");
            Step(240);Check(f.ConfirmedGround&&f.Flaps==2,"confirmed landing restores flaps");
            Reset(new Vector3(0,.02f,-3.5f));Tap();Step(5,Vector2.right);Check(f.Windup==0,"moving cancels wind-up");
            Step(30);Tap();Step(40);Check(f.Windup==0&&c.IsGrounded,"late second tap expires");Tap();Check(f.Windup>0&&c.IsGrounded,"fresh tap after expiry re-arms");
            Reset(new Vector3(0,.02f,-3.5f));Step(20,Vector2.right);Step(1);int before=f.LaunchCount;Tap();Check(f.Windup==0&&f.LaunchCount==before,"press during settling is discarded");
            Reset(new Vector3(0,.02f,-3.5f));Launch();Step(20,Vector2.right);var positive=c.Velocity;Step(35,Vector2.left);Check(Vector3.Dot(positive,c.Velocity)<0,"air reverse responds in opposite direction");Step(20);Check(Vector3.ProjectOnPlane(c.Velocity,Vector3.up).magnitude<.01f,"neutral air braking stops drift");
            var targets=UnityEngine.Object.FindObjectsByType<FairyTarget>();
            FairyTarget chat=null,dialogue=null,button=null;foreach(var t in targets){if(t.kind==FairyTargetKind.Chatter)chat=t;else if(t.kind==FairyTargetKind.Dialogue)dialogue=t;else if(t.kind==FairyTargetKind.Plunger)button=t;}
            void Approach(FairyTarget target){Reset(target.focusPoint.position+Vector3.back*.75f+Vector3.up*.02f);}
            Approach(button);Check(f.Focus==button,"plunger has distinct focused prompt");int uses=button.UseCount;int preUseLaunch=f.LaunchCount;Tap();Check(button.UseCount==uses+1&&f.LaunchCount==preUseLaunch,"plunger tap toggles without launch");
            Approach(button);button.gameObject.SetActive(false);uses=button.UseCount;Tap();Check(button.UseCount==uses&&f.Windup==0,"disappearing target consumes press");button.gameObject.SetActive(true);Physics.SyncTransforms();
            Approach(chat);Check(f.Focus==chat,"chatty NPC focuses");Tap();Check(f.Speaker==chat&&f.CurrentAction==FairyAction.Launch,"chatter frees next action for launch");Launch();Check(!c.IsGrounded&&f.Speech.Contains("Up you go"),"launch during chatter changes reaction");
            Approach(dialogue);Tap();Check(f.CurrentAction==FairyAction.Dialogue,"manual dialogue owns fresh press");Tap();Check(f.Speech.Contains("rainbow"),"manual dialogue advances second page");Tap();Check(f.Speech==""&&f.Focus==null,"last page closes without reacquisition");Step(30);Check(f.Focus==null,"closed dialogue remains suppressed while stationary");
            Approach(dialogue);Tap();f.Step(dt,Vector2.zero,false,true);Check(f.Speech=="","back closes dialogue and clears action");
            Approach(dialogue);Tap();Step(80,Vector2.down);Check(f.Speech=="","walking away closes manual dialogue");
            Reset(new Vector3(4.6f,.02f,3.1f));Launch();bool ceiling=false;for(int i=0;i<40;i++){f.Step(dt,Vector2.zero,false);for(int k=0;k<c.Motor.ContactCount;k++)ceiling|=c.Motor.Contacts[k].ceiling;}Check(ceiling,"low arch reports ceiling collision");
            Reset(new Vector3(4.7f,1.12f,4.8f));Step(60,Vector2.right);Check(!f.ConfirmedGround||c.transform.position.y<1,"ledge can be walked off");
            c.Teleport(new Vector3(-3.2f,.04f,.15f),Quaternion.identity);uses=button.UseCount;f.Step(dt,Vector2.zero,false);f.Step(dt,Vector2.zero,true);Check(button.UseCount==uses,"touchdown press cannot use prop");
            c.Teleport(new Vector3(0,-6,0),Quaternion.identity);f.Step(dt,Vector2.zero,true);Check(c.transform.position.y> -1&&f.Windup==0&&c.Velocity==Vector3.zero,"fall respawn clears state");
            // Movement intent blocks focus even when a wall stops all actual motion.
            Reset(new Vector3(3.0f,.02f,3.1f));Step(60,new Vector2(.88f,-.48f));Check(f.Focus==null&&f.Windup==0&&f.Settle==0,"wall pressure never settles into an action");
            Reset(new Vector3(0,.02f,-3.5f));Tap();var oldChat=chat.transform.position;chat.transform.position=new Vector3(0,0,-2.5f);Physics.SyncTransforms();Step(5);Tap();Check(!c.IsGrounded&&f.Speaker!=chat,"target entering range cannot steal armed launch");chat.transform.position=oldChat;Physics.SyncTransforms();
            Approach(button);uses=button.UseCount;Step(15,Vector2.up);Check(button.UseCount==uses,"walking onto plunger never uses it");
            foreach(int fps in new[]{30,60,120})
            {
                dt=1f/fps;Reset(new Vector3(0,.02f,-3.5f));Launch();float peak=c.transform.position.y;float flight=0;
                for(int i=0;i<fps*4&&!f.ConfirmedGround;i++){f.Step(dt,Vector2.zero,false);peak=Mathf.Max(peak,c.transform.position.y);flight+=dt;}
                Check(peak>1.35f&&peak<1.55f&&flight>1.15f&&flight<1.4f,$"{fps} fps: apex {peak:F3} m, airtime {flight:F3} s");
            }
            dt=1f/60;Reset(new Vector3(0,.02f,-3.5f));c.Teleport(new Vector3(0,.02f,-3.5f),Quaternion.Euler(0,180,0));f.readDevices=true;
            var report=string.Join("\n",lines);Debug.Log("Fairy Play checks\n"+report);return report;
        }
    }
}
