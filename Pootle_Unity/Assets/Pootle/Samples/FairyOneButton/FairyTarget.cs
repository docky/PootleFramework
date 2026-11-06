using UnityEngine;
namespace Pootle.FairyDemo
{
    public enum FairyTargetKind { Chatter, Dialogue, Plunger, Reset }
    public sealed class FairyTarget : MonoBehaviour
    {
        public FairyTargetKind kind;
        public FairyCourse course;
        public Transform focusPoint, highlight, plunger, gate, indicator;
        public float acquireDistance=1.3f, releaseDistance=1.65f, facingDot=.15f;
        public int stableOrder;
        public bool IsOn { get; private set; }
        public int UseCount { get; private set; }
        float pressTime;
        Vector3 plungerRest, gateRest;
        void Awake() { if(plunger)plungerRest=plunger.localPosition; if(gate)gateRest=gate.localPosition; }
        public void Use() { UseCount++; if(kind==FairyTargetKind.Reset){if(course)course.RequestReset();return;} if(kind==FairyTargetKind.Plunger){IsOn=!IsOn;pressTime=.32f;} }
        public void ResetTarget()
        {
            IsOn=false;UseCount=0;pressTime=0;
            if(plunger)plunger.localPosition=plungerRest;
            if(gate)gate.localPosition=gateRest;
            if(indicator)indicator.localScale=Vector3.one*.65f;
            Highlight(false);
        }
        public void Highlight(bool value) { if(highlight)highlight.gameObject.SetActive(value); }
        void Update()
        {
            pressTime=Mathf.Max(0,pressTime-Time.deltaTime);
            if(plunger)plunger.localPosition=plungerRest-Vector3.up*(Mathf.Sin(pressTime/.32f*Mathf.PI)*.18f);
            if(gate)gate.localPosition=Vector3.Lerp(gate.localPosition,gateRest+Vector3.up*(IsOn?1.7f:0),1-Mathf.Exp(-7*Time.deltaTime));
            if(indicator)indicator.localScale=Vector3.one*(IsOn?1.25f:.65f);
        }
    }
}
