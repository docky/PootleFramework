using System;
using UnityEngine;

namespace Docky.RiggleTools
{
    /// <summary>Position-based chain IK; keeps segment lengths and starts from the supplied animated pose.</summary>
    public sealed class RiggleChainIK
    {
        readonly Transform[] bones;
        readonly Vector3[] points;
        readonly Vector3[] bestPoints;
        readonly float[] lengths;
        readonly float total;
        readonly bool stabilizeTerminal;
        readonly Vector3 terminalLocalDirection;
        readonly float terminalBend;
        public RiggleChainIK(Transform[] chain, bool stabilizeTerminal = false, float terminalBendDegrees = 55f)
        {
            if(chain==null||chain.Length<3)throw new ArgumentException("A chain needs at least two segments.");
            this.stabilizeTerminal=stabilizeTerminal && chain.Length>3;
            terminalBend=Mathf.Clamp(terminalBendDegrees,0,90)*Mathf.Deg2Rad;
            bones=chain;points=new Vector3[chain.Length];bestPoints=new Vector3[chain.Length];lengths=new float[chain.Length-1];
            for(int i=0;i<lengths.Length;i++)
            {
                if(!chain[i]||!chain[i+1]||chain[i+1].parent!=chain[i])throw new ArgumentException("IK chain must have direct, ordered parent links.");
                lengths[i]=Vector3.Distance(chain[i].position,chain[i+1].position);
                if(lengths[i]<.00001f)throw new ArgumentException("Zero-length IK segment.");total+=lengths[i];
            }
            terminalLocalDirection=Quaternion.Inverse(chain[chain.Length-1].rotation)*(chain[chain.Length-1].position-chain[chain.Length-2].position).normalized;
        }
        public float TotalLength => total;
        public float TerminalBendDegrees => terminalBend*Mathf.Rad2Deg;
        public float Solve(Vector3 target,Quaternion rotation,int iterations=32)
        {
            for(int i=0;i<bones.Length;i++)points[i]=bones[i].position;
            Vector3 root=points[0];
            if(stabilizeTerminal)return SolveTerminal(target,rotation,iterations,root);
            if((target-root).magnitude>=total)
            {
                var axis=(target-root).normalized;
                for(int i=1;i<points.Length;i++)points[i]=points[i-1]+axis*lengths[i-1];
            }
            else for(int k=0;k<iterations;k++)
            {
                points[points.Length-1]=target;
                for(int i=points.Length-2;i>=0;i--)points[i]=points[i+1]+Direction(points[i]-points[i+1],-bones[i].up)*lengths[i];
                points[0]=root;
                for(int i=1;i<points.Length;i++)points[i]=points[i-1]+Direction(points[i]-points[i-1],bones[i-1].up)*lengths[i-1];
                if((points[points.Length-1]-target).sqrMagnitude<.00000001f)break;
            }
            for(int i=0;i<bones.Length-1;i++)bones[i].rotation=Quaternion.FromToRotation(bones[i+1].position-bones[i].position,points[i+1]-points[i])*bones[i].rotation;
            bones[bones.Length-1].rotation=rotation;
            return Vector3.Distance(bones[bones.Length-1].position,target);
        }
        // Bound the final hinge in both FABRIK passes instead of spending excess
        // reach by folding the shortest segment back through the lower leg.
        float SolveTerminal(Vector3 target,Quaternion rotation,int iterations,Vector3 root)
        {
            int end=points.Length-1;
            float bestError=float.PositiveInfinity;
            var axis=Direction(target-root,Vector3.down);
            var bend=Vector3.ProjectOnPlane(points[1]-root,axis).normalized;
            if(bend.sqrMagnitude<.001f)bend=Vector3.Cross(axis,Mathf.Abs(axis.x)<.8f?Vector3.right:Vector3.forward).normalized;
            // A constrained FABRIK chain can get stuck on a folded source seed.
            // Try deterministic alternate bend planes only when that pose fails.
            for(int attempt=0;attempt<5;attempt++)
            {
                if(attempt>0)
                {
                    var plane=attempt<=2?bend:Vector3.Cross(axis,bend).normalized;
                    if(attempt==2||attempt==4)plane=-plane;
                    for(int i=1;i<end;i++)
                    {
                        float f=i/(float)end;
                        points[i]=Vector3.Lerp(root,target,f)+plane*(total*.3f*Mathf.Sin(Mathf.PI*f));
                    }
                }
                for(int k=0;k<iterations;k++)
                {
                    points[end]=target;
                    for(int i=end-1;i>=0;i--)
                    {
                        var direction=Direction(points[i]-points[i+1],Vector3.up);
                        if(i==end-2)
                        {
                            var next=Direction(points[i+1]-points[i+2],Vector3.up);
                            direction=Vector3.RotateTowards(next,direction,terminalBend,0).normalized;
                        }
                        points[i]=points[i+1]+direction*lengths[i];
                    }
                    points[0]=root;
                    for(int i=1;i<=end;i++)
                    {
                        var direction=Direction(points[i]-points[i-1],Vector3.down);
                        if(i==end)
                        {
                            var previous=Direction(points[i-1]-points[i-2],Vector3.down);
                            direction=Vector3.RotateTowards(previous,direction,terminalBend,0).normalized;
                        }
                        points[i]=points[i-1]+direction*lengths[i-1];
                    }
                    if((points[end]-target).sqrMagnitude<.00000001f)break;
                }
                float error=(points[end]-target).sqrMagnitude;
                if(error<bestError){bestError=error;Array.Copy(points,bestPoints,points.Length);}
                if(bestError<.00000001f)break;
            }
            Array.Copy(bestPoints,points,points.Length);
            for(int i=0;i<bones.Length-1;i++)bones[i].rotation=Quaternion.FromToRotation(bones[i+1].position-bones[i].position,points[i+1]-points[i])*bones[i].rotation;
            var terminal=Direction(points[end]-points[end-1],Vector3.down);
            var requested=rotation*terminalLocalDirection;
            var limited=Vector3.RotateTowards(terminal,requested,35f*Mathf.Deg2Rad,0).normalized;
            bones[end].rotation=Quaternion.FromToRotation(requested,limited)*rotation;
            return Vector3.Distance(bones[end].position,target);
        }
        static Vector3 Direction(Vector3 v,Vector3 fallback)=>v.sqrMagnitude>.000000001f?v.normalized:fallback.normalized;
    }
}
