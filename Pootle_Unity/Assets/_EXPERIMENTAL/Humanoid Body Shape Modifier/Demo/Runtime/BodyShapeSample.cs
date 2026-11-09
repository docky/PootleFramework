using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;
namespace Docky.BodyShapeMotion
{
    [RequireComponent(typeof(Animator),typeof(BodyShapeAdjustment))]
    public sealed class BodyShapeSample : MonoBehaviour
    {
        public AnimationClip clip;
        public int row, column;
        public float previewPhase=.35f;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        BodyShapeAdjustment adjustment;
        public void Initialize()
        {
            Stop();adjustment=GetComponent<BodyShapeAdjustment>();adjustment.manualEvaluation=true;adjustment.RestoreAnimationPose();adjustment.ResetBlend();
            var a=GetComponent<Animator>();a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;a.Rebind();
            graph=PlayableGraph.Create(name+" · body shape");graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            playable=AnimationClipPlayable.Create(graph,clip);playable.SetApplyFootIK(true);
            AnimationPlayableOutput.Create(graph,"Humanoid",a).SetSourcePlayable(playable);graph.Play();
            adjustment.Bind();
        }
        public void Sample(float time,float deltaTime,bool instant=false,bool wrap=true)
        {
            if(!clip)return;if(!graph.IsValid())Initialize();
            adjustment.RestoreAnimationPose();
            playable.SetTime(wrap?Mathf.Repeat(time,Mathf.Max(.001f,clip.length)):Mathf.Clamp(time,0,clip.length));graph.Evaluate(0);
            adjustment.Evaluate(deltaTime,instant);
        }
        public void Stop(){if(graph.IsValid())graph.Destroy();}
        void OnDisable(){Stop();}
    }
}
