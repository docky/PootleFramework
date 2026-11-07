using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using ControlRig;
namespace Docky.RiggleTools
{
    [RequireComponent(typeof(Animator),typeof(ControlRigAnimation))]
    public sealed class RiggleRetargetSample : MonoBehaviour
    {
        public AnimationClip clip;
        [Range(0,1)] public float previewTime=.35f;
        public bool moveRoot;
        public bool animate=true;
        [Min(.1f)] public float resetAfterSeconds=4;
        public float Travelled {get;private set;}
        PlayableGraph graph;
        AnimationClipPlayable playable;
        Vector3 startPosition; Quaternion startRotation;
        float elapsed;
        [SerializeField,HideInInspector] bool hasOrigin;
        [SerializeField,HideInInspector] Vector3 originPosition;
        [SerializeField,HideInInspector] Quaternion originRotation;
        void Start(){if(clip)Begin();}
        public void Begin()
        {
            Stop();
            if(!hasOrigin){originPosition=transform.localPosition;originRotation=transform.localRotation;hasOrigin=true;}
            transform.SetLocalPositionAndRotation(originPosition,originRotation);
            startPosition=transform.position;startRotation=transform.rotation;elapsed=0;Travelled=0;
            var a=GetComponent<Animator>();var control=GetComponent<ControlRigAnimation>();
            control.poseSource=ControlRigAnimation.PoseSource.Animation;control.rootMotion=moveRoot;control.Refresh();
            a.enabled=true;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();
            graph=PlayableGraph.Create(name+" · retarget");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(true);
            var output=AnimationPlayableOutput.Create(graph,"Humanoid",a);output.SetSourcePlayable(playable);graph.Play();
            playable.SetTime(0);graph.Evaluate(0);
        }
        public void Step(float delta)
        {
            if(!graph.IsValid() || delta<=0)return;
            var before=transform.position;graph.Evaluate(delta);Travelled+=Vector3.Distance(before,transform.position);elapsed+=delta;
        }
        void Update()
        {
            if(!animate || !graph.IsValid())return;
            if(elapsed>=Mathf.Min(resetAfterSeconds,Mathf.Max(.1f,clip.length)))
            {transform.SetPositionAndRotation(startPosition,startRotation);playable.SetTime(0);elapsed=0;graph.Evaluate(0);}
            Step(Time.deltaTime);
        }
        public void Preview()
        {
            Begin();float time=clip.length*previewTime;while(time>0){float step=Mathf.Min(time,1f/30f);Step(step);time-=step;}Stop();
        }
        public void Stop(){if(graph.IsValid())graph.Destroy();}
        void OnDisable()=>Stop();
    }
}
