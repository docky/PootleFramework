using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Pootle
{
    /// <summary>Bone circles painted into a shared mask, projected per pixel onto rendered opaque surfaces below.</summary>
    [DefaultExecutionOrder(200),DisallowMultipleComponent]
    public sealed class PootleBlobShadow : MonoBehaviour
    {
        public PootleCharacter character;
        public Material material;
        [Range(0,1)] public float opacity=.58f;
        [Min(1)] public float projectionWidth=5;
        [Min(1)] public float maximumDrop=18;
        public bool replaceModelShadow=true;
        public RenderTexture Mask => target;
        public int CircleCount => character && character.IsRolling ? 1 : bones.Count;
        public bool ProjectionActive => surface && surface.activeSelf;
        const int Size=256;
        readonly List<Transform> bones=new List<Transform>();
        readonly List<float> radii=new List<float>();
        readonly Color32[] pixels=new Color32[Size*Size];
        readonly Dictionary<Renderer,ShadowCastingMode> shadowModes=new Dictionary<Renderer,ShadowCastingMode>();
        readonly List<Renderer> renderers=new List<Renderer>();
        GameObject surface;Material instance;Texture2D paint;RenderTexture target;
        void OnEnable()
        {
            if(!character)character=GetComponent<PootleCharacter>();
            if(!character || !material)return;
            var animator=character.visualRoot?character.visualRoot.GetComponentInChildren<Animator>():null;
            bones.Clear();radii.Clear();
            if(animator && animator.isHuman)
            {
                Add(animator,HumanBodyBones.Head,.3f);Add(animator,HumanBodyBones.Chest,.34f);Add(animator,HumanBodyBones.Hips,.28f);
                Add(animator,HumanBodyBones.LeftUpperArm,.14f);Add(animator,HumanBodyBones.RightUpperArm,.14f);
                Add(animator,HumanBodyBones.LeftLowerArm,.13f);Add(animator,HumanBodyBones.RightLowerArm,.13f);
                Add(animator,HumanBodyBones.LeftHand,.17f);Add(animator,HumanBodyBones.RightHand,.17f);
                Add(animator,HumanBodyBones.LeftUpperLeg,.19f);Add(animator,HumanBodyBones.RightUpperLeg,.19f);
                Add(animator,HumanBodyBones.LeftLowerLeg,.16f);Add(animator,HumanBodyBones.RightLowerLeg,.16f);
                Add(animator,HumanBodyBones.LeftFoot,.22f);Add(animator,HumanBodyBones.RightFoot,.22f);
            }
            if(bones.Count==0){bones.Add(transform);radii.Add(.5f);}
            paint=new Texture2D(Size,Size,TextureFormat.RGBA32,false,true){name="Limb circles",wrapMode=TextureWrapMode.Clamp};
            target=new RenderTexture(Size,Size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){name="Pootle body blob mask",wrapMode=TextureWrapMode.Clamp};target.Create();
            instance=new Material(material);instance.mainTexture=target;
            surface=GameObject.CreatePrimitive(PrimitiveType.Cube);surface.name="Projected body blob";surface.hideFlags=HideFlags.DontSave;
            var collider=surface.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
            var renderer=surface.GetComponent<MeshRenderer>();renderer.sharedMaterial=instance;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        void Add(Animator a,HumanBodyBones bone,float radius)
        {var t=a.GetBoneTransform(bone);if(t){bones.Add(t);radii.Add(radius);}}
        void LateUpdate()
        {
            if(!surface || !character)return;
            // Rolling creates its visual lazily. Include new renderers so that it
            // never contributes a second directional shadow over the blob.
            character.GetComponentsInChildren<Renderer>(true,renderers);
            foreach(var r in renderers)
                if(!shadowModes.ContainsKey(r))
                {shadowModes.Add(r,r.shadowCastingMode);if(replaceModelShadow)r.shadowCastingMode=ShadowCastingMode.Off;}
            System.Array.Clear(pixels,0,pixels.Length);
            Vector3 center=character.transform.position;
            if(character.IsRolling)Stamp(Vector3.zero,character.Motor.Radius*.98f);
            else for(int i=0;i<bones.Count;i++)if(bones[i])Stamp(bones[i].position-center,radii[i]);
            paint.SetPixels32(pixels);paint.Apply(false);Graphics.Blit(paint,target);
            instance.SetFloat("_Opacity",opacity);
            Vector3 normal=Vector3.up;
            if(character.Support.found && (character.Mode==PootleMode.Grounded || character.Mode==PootleMode.Sliding || character.IsRolling)
                && character.Support.normal.y>.15f)normal=character.Support.normal.normalized;
            instance.SetFloat("_UseReceiverPlane",1);
            instance.SetVector("_ReceiverPlane",new Vector4(normal.x,normal.y,normal.z,-Vector3.Dot(normal,center)-.03f));
            // The upper volume must contain the uphill side of the entire mask,
            // not stop at the horizontal height of the capsule's feet.
            float rise=.03f/normal.y+projectionWidth*.5f*(Mathf.Abs(normal.x)+Mathf.Abs(normal.z))/normal.y;
            float drop=Mathf.Max(1,maximumDrop),height=drop+rise;
            surface.transform.SetPositionAndRotation(center+Vector3.up*((rise-drop)*.5f),Quaternion.identity);
            surface.transform.localScale=new Vector3(projectionWidth,height,projectionWidth);
        }
        void Stamp(Vector3 p,float radius)
        {
            float cx=(p.x/projectionWidth+.5f)*Size,cy=(p.z/projectionWidth+.5f)*Size,r=radius*Size/projectionWidth;
            for(int y=Mathf.Max(0,Mathf.FloorToInt(cy-r));y<=Mathf.Min(Size-1,Mathf.CeilToInt(cy+r));y++)
            for(int x=Mathf.Max(0,Mathf.FloorToInt(cx-r));x<=Mathf.Min(Size-1,Mathf.CeilToInt(cx+r));x++)
            {
                float d=new Vector2(x+.5f-cx,y+.5f-cy).magnitude/r;
                byte alpha=(byte)(255*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.6f,1,d))));
                int at=y*Size+x;if(alpha>pixels[at].a)pixels[at]=new Color32(255,255,255,alpha);
            }
        }
        void OnDisable()
        {
            foreach(var pair in shadowModes)if(pair.Key)pair.Key.shadowCastingMode=pair.Value;
            shadowModes.Clear();
            if(target)target.Release();
            Destroy(surface);Destroy(instance);Destroy(paint);Destroy(target);
            bones.Clear();radii.Clear();
        }
    }
}
