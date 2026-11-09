using UnityEngine;
namespace OinkRomp
{
    public sealed class OinkRompCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset=new Vector3(0,13,-16);
        public OinkRompZone CurrentZone {get;private set;}
        Vector3 velocity,blendedOffset;
        bool placed;
        Light sun;
        float sunIntensity;
        Color sky,equator,ground;
        void OnEnable()
        {
            sun=RenderSettings.sun;if(sun)sunIntensity=sun.intensity;
            sky=RenderSettings.ambientSkyColor;equator=RenderSettings.ambientEquatorColor;ground=RenderSettings.ambientGroundColor;
            blendedOffset=offset;
        }
        void LateUpdate()
        {
            if(!target)return;
            CurrentZone=null;
            foreach(var zone in OinkRompZone.Active)
                if(zone.Contains(target.position) && (!CurrentZone || zone.priority>CurrentZone.priority))CurrentZone=zone;
            foreach(var zone in OinkRompZone.Active)zone.SetInside(zone==CurrentZone);
            if(sun)sun.intensity=CurrentZone?CurrentZone.sunlight:sunIntensity;
            RenderSettings.ambientSkyColor=CurrentZone?CurrentZone.ambient:sky;
            RenderSettings.ambientEquatorColor=CurrentZone?CurrentZone.ambient*.65f:equator;
            RenderSettings.ambientGroundColor=CurrentZone?CurrentZone.ambient*.4f:ground;
            blendedOffset=Vector3.Lerp(blendedOffset,CurrentZone?CurrentZone.cameraOffset:offset,1-Mathf.Exp(-5*Time.deltaTime));
            Vector3 focus=target.position+Vector3.up;
            Vector3 desired=focus+blendedOffset;
            if(!placed || Vector3.Distance(transform.position,desired)>35){transform.position=desired;placed=true;velocity=Vector3.zero;}
            else transform.position=Vector3.SmoothDamp(transform.position,desired,ref velocity,.18f);
            transform.rotation=Quaternion.LookRotation(-blendedOffset,Vector3.up);
        }
        void OnDisable()
        {
            if(sun)sun.intensity=sunIntensity;
            RenderSettings.ambientSkyColor=sky;RenderSettings.ambientEquatorColor=equator;RenderSettings.ambientGroundColor=ground;
            foreach(var zone in OinkRompZone.Active)zone.SetInside(false);
        }
    }
}
