#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Pootle.FairyDemo
{
    /// <summary>Temporary Play Mode probe; not part of the saved scene or player builds.</summary>
    public sealed class FairyDeviceChecks : MonoBehaviour
    {
        public string Result {get;private set;}="Running";
        Keyboard keyboard;Gamepad pad;FairyController fairy;
        readonly List<string> lines=new();
        void Check(bool value,string label)=>lines.Add((value?"PASS ":"FAIL ")+label);
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        IEnumerator Pause(float duration){yield return new WaitForSeconds(duration);}
        IEnumerator Tap(){Keys(Key.Space);yield return Pause(.04f);Keys();yield return Pause(.06f);}
        IEnumerator Start()
        {
            fairy=FindAnyObjectByType<FairyController>();keyboard=InputSystem.AddDevice<Keyboard>();pad=InputSystem.AddDevice<Gamepad>();fairy.readDevices=true;
            fairy.Character.Teleport(new Vector3(0,.02f,-3.5f),Quaternion.identity);yield return Pause(.35f);
            int count=fairy.LaunchCount;Keys(Key.Space);yield return Pause(.75f);Check(fairy.LaunchCount==count&&fairy.ConfirmedGround,"Input System: Space hold is a single tap");Keys();yield return Pause(.2f);
            yield return Tap();yield return Tap();Check(fairy.LaunchCount==count+1&&!fairy.ConfirmedGround,"Input System: Space double tap launches");yield return Pause(.35f);yield return Tap();Check(fairy.Flaps==1,"Input System: Space in flight flaps");
            yield return Pause(2.5f);
            Key[][] directions={new[]{Key.W},new[]{Key.S},new[]{Key.A},new[]{Key.D},new[]{Key.W,Key.A},new[]{Key.W,Key.D},new[]{Key.S,Key.A},new[]{Key.S,Key.D}};
            foreach(var keys in directions)
            {
                fairy.Character.Teleport(new Vector3(0,.02f,-3.5f),Quaternion.identity);Keys();yield return Pause(.25f);var start=fairy.transform.position;Keys(keys);yield return Pause(.25f);
                float speed=Vector3.ProjectOnPlane(fairy.Character.Velocity,Vector3.up).magnitude;Check(Vector3.Distance(start,fairy.transform.position)>.25f&&speed<=3.21f,"Input System: "+string.Join("+",keys)+" moves with normalized speed");Keys();yield return Pause(.3f);Check(fairy.ActualSpeed<.01f,"Input System: neutral stops");
            }
            fairy.Character.Teleport(new Vector3(0,.02f,-3.5f),Quaternion.identity);yield return Pause(.25f);var initial=fairy.transform.position;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));yield return Pause(.25f);Check(Vector3.Distance(initial,fairy.transform.position)>.25f,"Input System: gamepad D-pad moves");InputSystem.QueueStateEvent(pad,new GamepadState());yield return Pause(.3f);
            count=fairy.LaunchCount;
            for(int i=0;i<2;i++){InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return Pause(.04f);InputSystem.QueueStateEvent(pad,new GamepadState());yield return Pause(.06f);}
            Check(fairy.LaunchCount==count+1,"Input System: South double tap launches");
            var npc=System.Array.Find(FindObjectsByType<FairyTarget>(),t=>t.kind==FairyTargetKind.Dialogue);
            foreach(var key in new[]{Key.Escape,Key.B})
            {
                fairy.Character.Teleport(npc.focusPoint.position+Vector3.back*.75f+Vector3.up*.02f,Quaternion.identity);yield return Pause(.3f);yield return Tap();Check(fairy.CurrentAction==FairyAction.Dialogue,"Input System: enters dialogue before "+key);Keys(key);yield return Pause(.08f);Keys();yield return Pause(.08f);Check(fairy.Speech=="","Input System: "+key+" exits dialogue");
            }
            fairy.Respawn();Result=string.Join("\n",lines);Debug.Log("Fairy device checks\n"+Result);Cleanup();
        }
        void Cleanup(){if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}if(pad!=null){InputSystem.RemoveDevice(pad);pad=null;}}
        void OnDestroy()=>Cleanup();
    }
}
#endif
