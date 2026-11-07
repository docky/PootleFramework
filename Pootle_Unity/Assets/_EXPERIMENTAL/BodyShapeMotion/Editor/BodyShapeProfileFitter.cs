using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Docky.BodyShapeMotion.Editor
{
    public static class BodyShapeProfileFitter
    {
        public static void Fit(GameObject model,BodyShapeProfile profile)
        {
            var a=model.GetComponent<Animator>();
            var categories=new Dictionary<Transform,int>();
            foreach(var id in new[]{HumanBodyBones.Hips,HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest}){var bone=a.GetBoneTransform(id);if(bone)categories[bone]=0;}
            categories[a.GetBoneTransform(HumanBodyBones.LeftHand)]=1;categories[a.GetBoneTransform(HumanBodyBones.RightHand)]=2;
            categories[a.GetBoneTransform(HumanBodyBones.LeftFoot)]=3;categories[a.GetBoneTransform(HumanBodyBones.RightFoot)]=4;
            // Other humanoid bones stop the ancestry walk: arms/head are not torso volume.
            for(int i=0;i<(int)HumanBodyBones.LastBone;i++){var b=a.GetBoneTransform((HumanBodyBones)i);if(b && !categories.ContainsKey(b))categories[b]=-1;}
            var points=Enumerable.Range(0,5).Select(_=>new List<Vector3>()).ToArray();
            var anchors=new[]{a.GetBoneTransform(HumanBodyBones.Hips),a.GetBoneTransform(HumanBodyBones.LeftHand),a.GetBoneTransform(HumanBodyBones.RightHand),a.GetBoneTransform(HumanBodyBones.LeftFoot),a.GetBoneTransform(HumanBodyBones.RightFoot)};
            foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var map=skin.bones.Select(b=>{while(b){if(categories.TryGetValue(b,out int c))return c;b=b.parent;}return -1;}).ToArray();
                var baked=new Mesh();skin.BakeMesh(baked,true);var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];var totals=new float[5];
                    void Add(int index,float weight){if(index>=0 && index<map.Length && map[index]>=0)totals[map[index]]+=weight;}
                    Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);
                    for(int c=0;c<5;c++)if(totals[c]>.5f)points[c].Add(anchors[c].InverseTransformPoint(skin.transform.TransformPoint(vertices[i])));
                }
                Object.DestroyImmediate(baked);
            }
            Bounds BoundsFor(int c){if(points[c].Count==0)throw new System.InvalidOperationException("No weighted vertices for shape group "+c);var b=new Bounds(points[c][0],Vector3.zero);foreach(var p in points[c])b.Encapsulate(p);return b;}
            var torso=BoundsFor(0);
            // Back appendages can share hips/spine weights. Keep the initial belly proxy near
            // the skeletal midline rather than stretching it over a long tail.
            var core=points[0].Where(p=>Mathf.Abs(p.z)<=torso.extents.x*1.1f).ToArray();
            if(core.Length>8){torso=new Bounds(core[0],Vector3.zero);foreach(var p in core)torso.Encapsulate(p);}
            profile.torso=new[]{new BodyShapeProfile.Volume{label="Torso / belly",bone=HumanBodyBones.Hips,center=torso.center,radii=torso.extents*.98f}};
            var lh=BoundsFor(1);var rh=BoundsFor(2);profile.leftHandCenter=lh.center;profile.rightHandCenter=rh.center;
            profile.handRadius=Mathf.Max(lh.extents.x,lh.extents.y,lh.extents.z,rh.extents.x,rh.extents.y,rh.extents.z)*.95f;
            profile.forearmRadius=profile.handRadius*.5f;
            var lf=BoundsFor(3);var rf=BoundsFor(4);profile.leftFoot=new BodyShapeProfile.FootShape{center=lf.center,halfExtents=lf.extents};profile.rightFoot=new BodyShapeProfile.FootShape{center=rf.center,halfExtents=rf.extents};
            profile.clearance=.012f;profile.maximumOffset=.3f;profile.smoothingSeconds=.065f;
        }
    }
}
