using UnityEngine;
namespace Docky.BodyShapeMotion
{
    public static class BodyShapeMath
    {
        // Conservative radial projection against an ellipsoid expanded by a limb radius.
        public static Vector3 OutsideEllipsoid(Vector3 point, Vector3 center, Vector3 radii, float radius, Vector3 fallback)
        {
            var r = new Vector3(Mathf.Max(.001f,radii.x+radius),Mathf.Max(.001f,radii.y+radius),Mathf.Max(.001f,radii.z+radius));
            var d=point-center;var n=new Vector3(d.x/r.x,d.y/r.y,d.z/r.z);float length=n.magnitude;
            if(length>=1)return point;
            if(length<.00001f){n=fallback.sqrMagnitude>.00001f?fallback.normalized:Vector3.right;return center+Vector3.Scale(n,r);}
            return center+d/length;
        }
        // Move the bend plane with the leg instead of pinning the knee to its old world position.
        public static Vector3 LegPole(Vector3 hip,Vector3 knee,Vector3 ankle,Vector3 target,
            Vector3 forward,Vector3 outward,float follow,float outwardDegrees,float turnout)
        {
            follow=Mathf.Clamp01(follow);if(follow<=0)return knee;
            var sourceAxis=(ankle-hip).normalized;var targetAxis=(target-hip).normalized;
            if(sourceAxis.sqrMagnitude<.5f || targetAxis.sqrMagnitude<.5f)return knee;
            float length=Vector3.Distance(hip,knee)+Vector3.Distance(knee,ankle);
            var animated=Vector3.ProjectOnPlane(knee-hip,sourceAxis);
            var restBend=Vector3.ProjectOnPlane(forward,sourceAxis).normalized;
            if(restBend.sqrMagnitude<.5f)restBend=Vector3.ProjectOnPlane(outward,sourceAxis).normalized;
            // Almost straight knees have no reliable bend plane. Fade to a stable anatomical hint.
            float confidence=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.005f,.04f,animated.magnitude/Mathf.Max(length,.00001f)));
            var sourceBend=Vector3.Slerp(restBend,animated.sqrMagnitude>1e-12f?animated.normalized:restBend,confidence);
            var bend=(Quaternion.FromToRotation(sourceAxis,targetAxis)*sourceBend).normalized;
            var front=Vector3.ProjectOnPlane(forward,targetAxis).normalized;
            var outside=Vector3.ProjectOnPlane(outward,targetAxis).normalized;
            if(front.sqrMagnitude>.5f && outside.sqrMagnitude>.5f && outwardDegrees>0)
            {
                float sign=Mathf.Sign(Vector3.Dot(Vector3.Cross(front,outside),targetAxis));
                float current=Vector3.SignedAngle(front,bend,targetAxis)*sign;
                // Retain already turned-out and deliberately folded/backward animated knees.
                float forwardWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-110,-60,current));
                float turn=Mathf.Clamp(outwardDegrees-current,0,60)*Mathf.Clamp01(turnout)*forwardWeight;
                bend=Quaternion.AngleAxis(turn*sign,targetAxis)*bend;
            }
            var oldBend=Vector3.ProjectOnPlane(knee-hip,targetAxis).normalized;
            if(oldBend.sqrMagnitude<.5f)oldBend=bend;
            return hip+Vector3.Slerp(oldBend,bend,follow).normalized*Mathf.Max(length,.001f);
        }
        public static void SolveTwoBone(Transform upper,Transform lower,Transform tip,Vector3 target,Vector3 pole)
        {
            float a=Vector3.Distance(upper.position,lower.position), b=Vector3.Distance(lower.position,tip.position);
            if(a<.00001f || b<.00001f)return;
            var root=upper.position;var direction=target-root;
            float distance=Mathf.Clamp(direction.magnitude,Mathf.Abs(a-b)+.00001f,(a+b)*.9995f);
            if(direction.sqrMagnitude<.00000001f)direction=tip.position-root;
            direction.Normalize();
            var bend=Vector3.ProjectOnPlane(pole-root,direction);
            if(bend.sqrMagnitude<.00000001f)bend=Vector3.ProjectOnPlane(lower.position-root,direction);
            if(bend.sqrMagnitude<.00000001f)bend=Vector3.Cross(direction,Mathf.Abs(direction.y)<.9f?Vector3.up:Vector3.right);
            float along=(a*a-b*b+distance*distance)/(2*distance);
            var elbow=root+direction*along+bend.normalized*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Quaternion tipRotation=tip.rotation;
            upper.rotation=Quaternion.FromToRotation(lower.position-root,elbow-root)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(tip.position-lower.position,root+direction*distance-lower.position)*lower.rotation;
            tip.rotation=tipRotation;
        }
    }
}
