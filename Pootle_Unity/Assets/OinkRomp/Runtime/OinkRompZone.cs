using System.Collections.Generic;
using UnityEngine;
namespace OinkRomp
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class OinkRompZone : MonoBehaviour
    {
        public static readonly List<OinkRompZone> Active=new List<OinkRompZone>();
        public Vector3 cameraOffset=new Vector3(0,6,-7);
        public float sunlight=.15f;
        public Color ambient=new Color(.12f,.15f,.19f);
        public Renderer[] cutaway;
        public int priority;
        public bool Entered {get;private set;}
        BoxCollider volume;
        void OnEnable(){volume=GetComponent<BoxCollider>();volume.isTrigger=true;Active.Add(this);}
        void OnDisable(){Active.Remove(this);SetInside(false);}
        public bool Contains(Vector3 position)=>OinkRompCourse.Contains(volume?volume:GetComponent<BoxCollider>(),position+Vector3.up*.5f);
        public void SetInside(bool inside)
        {if(inside)Entered=true;if(cutaway!=null)foreach(var r in cutaway)if(r)r.enabled=!inside;}
    }
}
