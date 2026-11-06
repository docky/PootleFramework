#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Pootle.FairyDemo
{
    public sealed class FairyCourseDeviceChecks : MonoBehaviour
    {
        public string Result {get;private set;}="Running";
        Keyboard keyboard;Mouse mouse;
        readonly List<string> lines=new();
        void Check(bool pass,string name)=>lines.Add((pass?"PASS ":"FAIL ")+name);
        IEnumerator Start()
        {
            var course=FindAnyObjectByType<FairyCourse>();course.ResetCourse();
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            var point=course.collectibles[0].transform.position;course.EvaluateSegment(point,point);
            Check(course.Collected==1,"reset input test starts with collected seed");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));yield return new WaitForSeconds(.15f);
            Check(course.Collected==0,"R key resets through live Input System");InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            course.EvaluateSegment(point,point);
            var canvas=course.resetButton.GetComponentInParent<Canvas>();
            var rect=(RectTransform)course.resetButton.transform;
            var screen=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,rect.TransformPoint(rect.rect.center));
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);
            Check(hits.Exists(h=>h.gameObject==course.resetButton.gameObject),"reset button receives UI raycasts");
            InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=screen,buttons=1});yield return new WaitForSeconds(.08f);
            InputSystem.QueueStateEvent(mouse,new MouseState{position=screen});yield return new WaitForSeconds(.15f);
            Check(course.Collected==0,"mouse click resets through UI input module");
            Check(EventSystem.current.currentSelectedGameObject==null,"reset releases UI selection so Space stays a flight action");
            course.ResetCourse();Result=string.Join("\n",lines);Debug.Log("Fairy course device checks\n"+Result);Cleanup();
        }
        void Cleanup(){if(keyboard!=null){InputSystem.RemoveDevice(keyboard);keyboard=null;}if(mouse!=null){InputSystem.RemoveDevice(mouse);mouse=null;}}
        void OnDestroy()=>Cleanup();
    }
}
#endif
