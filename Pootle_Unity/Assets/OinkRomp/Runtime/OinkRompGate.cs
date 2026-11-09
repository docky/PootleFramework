using UnityEngine;
using Pootle;
namespace OinkRomp
{
    public sealed class OinkRompGate : MonoBehaviour
    {
        public enum Requirement { Bump, Apples, HoverPlate, Mud }
        public Requirement requirement;
        public Transform door, button, checkpoint;
        public Collider blocker;
        public Renderer lamp;
        public int applesRequired=3;
        public bool IsOpen {get; private set;}
        public float Pressure {get; private set;}
        public bool Unlocked {get;set;}
        Vector3 rest,buttonRest;
        MaterialPropertyBlock colors;
        void Awake(){rest=door.localPosition;buttonRest=button.localPosition;colors=new MaterialPropertyBlock();}
        public void Sample(OinkRompController pig,PootleInputFrame input,float dt)
        {
            if(IsOpen)return;
            Vector3 delta=pig.transform.position+Vector3.up*.65f-button.position;
            Vector3 direction=PootleMovement.CameraDirection(input.move,pig.Character.outputCamera,out float magnitude);
            bool push=delta.sqrMagnitude<2.2f && pig.Character.IsGrounded && magnitude>.4f && Vector3.Dot(direction,transform.forward)>.65f;
            Pressure=push?Mathf.Min(1,Pressure+dt/.3f):0;
            button.localPosition=buttonRest+Vector3.forward*(Pressure*.13f);
            if(Pressure<1)return;
            pig.Bump();Pressure=0;
            bool allowed=requirement==Requirement.Bump || requirement==Requirement.Apples && pig.Apples>=applesRequired || Unlocked;
            if(!allowed){pig.Notify(requirement==Requirement.Apples?"Find and eat 3 apples to open the orchard gate.":requirement==Requirement.Mud?"Take a dip in the mud, then push this button.":"Land on the golden plate above the spring first.",3); return;}
            IsOpen=true;blocker.enabled=false;
            if(checkpoint)pig.Checkpoint=checkpoint.position;
            pig.Notify("Gate open! On to the next paddock.",3);
        }
        void Update()
        {
            if(IsOpen)door.localPosition=Vector3.Lerp(door.localPosition,rest-Vector3.up*4.2f,1-Mathf.Exp(-3*Time.deltaTime));
            if(lamp){colors.SetColor("_BaseColor",IsOpen?new Color(.55f,.8f,.3f):new Color(.95f,.55f,.2f));lamp.SetPropertyBlock(colors);}
        }
    }
}
