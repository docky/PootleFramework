using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Docky.RiggleTools.Demo
{
    public sealed class RiggleQuadrupedRetargetShowcase : MonoBehaviour
    {
        public GameObject source,target;
        public Transform sourceMotionRoot;
        public AnimationClip[] originalClips,convertedClips;
        public string[] labels;
        public string targetLabel="PINK PONY";
        public string recordingSlug="riggle-pink-pony-horse-retarget-four-segment-2026-11-08";
        public string recordingSubfolder="";
        public Camera camera;
        public float secondsPerClip=8;
        public float elapsed;
        public int Current {get;private set;}=-1;
        public string Label=>labels[Mathf.Max(0,Current)];
        PlayableGraph graph;
        AnimationClipPlayable a,b;
        Vector3 restMotionPosition;Quaternion restMotionRotation;
        void Start(){restMotionPosition=sourceMotionRoot.localPosition;restMotionRotation=sourceMotionRoot.localRotation;}
        void Update()
        {
            int index=Mathf.Min(originalClips.Length-1,(int)(elapsed/secondsPerClip));
            if(index!=Current)Begin(index);
            double time=(elapsed-index*secondsPerClip)%originalClips[index].length;
            a.SetTime(time);b.SetTime(time);graph.Evaluate(0);
            var p=sourceMotionRoot.localPosition;p.x=restMotionPosition.x;p.z=restMotionPosition.z;sourceMotionRoot.localPosition=p;
            sourceMotionRoot.localRotation=restMotionRotation;
            elapsed+=Time.deltaTime;
        }
        void Begin(int index)
        {
            if(graph.IsValid())graph.Destroy();Current=index;
            graph=PlayableGraph.Create("Generic quadruped comparison");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            a=Connect(source,originalClips[index]);b=Connect(target,convertedClips[index]);graph.Play();
        }
        AnimationClipPlayable Connect(GameObject go,AnimationClip clip)
        {
            var animator=go.GetComponent<Animator>();if(!animator)animator=go.AddComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(false);
            AnimationPlayableOutput.Create(graph,go.name,animator).SetSourcePlayable(playable);return playable;
        }
        void OnDisable(){if(graph.IsValid())graph.Destroy();}
    }
}
