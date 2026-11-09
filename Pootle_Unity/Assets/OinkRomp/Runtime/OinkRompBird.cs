using UnityEngine;
namespace OinkRomp
{
    public sealed class OinkRompBird : MonoBehaviour
    {
        public string speaker="Pip";
        [TextArea(2,5)] public string dialogue;
        public float range=4;
        public Animator animator;
        bool talking;
        void OnEnable()
        {
            talking=false;
            if(!animator)animator=GetComponentInChildren<Animator>();
            if(animator && animator.HasState(0,Animator.StringToHash("Base Layer.Idle_A")))
                animator.Play("Base Layer.Idle_A",0,0);
        }
        public bool InRange(Vector3 position) => Vector3.Distance(position,transform.position)<range;
        public void Face(OinkRompController pig,bool nearby)
        {
            if(nearby){Vector3 dir=pig.transform.position-transform.position;dir.y=0;if(dir.sqrMagnitude>.1f)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(dir),Time.deltaTime*3);}
            if(talking==nearby)return;talking=nearby;
            if(animator)
            {
                int state=Animator.StringToHash("Base Layer."+(nearby?"Idle_B":"Idle_A"));
                if(!animator.HasState(0,state))state=Animator.StringToHash("Base Layer.Idle_A");
                if(animator.HasState(0,state))animator.CrossFadeInFixedTime(state,.2f,0);
            }
        }
    }
}
