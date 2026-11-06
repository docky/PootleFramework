using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
namespace Pootle
{
    public enum PootleCameraStyle { FreeOrbit, CloseOrbit, Chase, AngledOverview, Overhead, SideOn, SlideFollow }
    [DefaultExecutionOrder(-60),DisallowMultipleComponent,RequireComponent(typeof(CinemachineOrbitalFollow))]
    public sealed class PootleCameraStyles : MonoBehaviour
    {
        public InputActionAsset actions;
        public PootleCharacter character;
        public bool automaticallyFollowSlides=true;
        public bool SlideFollowActive { get; private set; }
        public PootleCameraStyle Style { get; private set; }
        public bool AllowsLook => transition<=0 && (Style==PootleCameraStyle.FreeOrbit || Style==PootleCameraStyle.CloseOrbit || Style==PootleCameraStyle.Chase || Style==PootleCameraStyle.SlideFollow);
        private InputActionAsset instance;
        private readonly InputAction[] selectors=new InputAction[7];
        private InputAction look,mouseLook;
        private CinemachineCamera rig;
        private CinemachineOrbitalFollow orbit;
        private float radius=8,pitch=20,yaw,fov=55,transition,chaseManualUntil;
        private float slideFollowUntil,yawSpeed;
        private void OnEnable()
        {
            rig=GetComponent<CinemachineCamera>();orbit=GetComponent<CinemachineOrbitalFollow>();
            if(actions)
            {
                instance=Instantiate(actions);instance.FindActionMap("Camera",true).Enable();
                for(int i=0;i<selectors.Length;i++)selectors[i]=instance.FindAction("Camera/Style"+(i+1));
                look=instance.FindAction("Camera/Look");mouseLook=instance.FindAction("Camera/MouseLook");
            }
            Select(PootleCameraStyle.FreeOrbit);transition=0;
        }
        public void Select(PootleCameraStyle style)
        {
            Style=style;transition=.35f;chaseManualUntil=0;slideFollowUntil=0;yawSpeed=0;
            yaw=orbit.HorizontalAxis.Value;pitch=orbit.VerticalAxis.Value;
            switch(style)
            {
                case PootleCameraStyle.FreeOrbit: radius=8;fov=55;pitch=20;break;
                case PootleCameraStyle.CloseOrbit: radius=4.5f;fov=65;pitch=15;break;
                case PootleCameraStyle.Chase: radius=6;fov=65;pitch=16;yaw=FacingYaw();break;
                case PootleCameraStyle.AngledOverview: radius=14;fov=45;pitch=40;yaw=45;break;
                case PootleCameraStyle.Overhead: radius=18;fov=50;pitch=80;yaw=0;break;
                case PootleCameraStyle.SideOn: radius=12;fov=45;pitch=8;yaw=90;break;
                case PootleCameraStyle.SlideFollow: radius=8;fov=62;pitch=34;yaw=FacingYaw();break;
            }
            orbit.VerticalAxis.Range=style==PootleCameraStyle.Overhead?new Vector2(-15,85):new Vector2(-15,70);
            orbit.VerticalAxis.Center=pitch;
        }
        private float FacingYaw()=>character?Mathf.Atan2(character.Facing.x,character.Facing.z)*Mathf.Rad2Deg:0;
        private void Update()
        {
            if (character && !character.isActiveAndEnabled) return;
            for(int i=0;i<selectors.Length;i++)if(selectors[i]!=null && selectors[i].WasPressedThisFrame())Select((PootleCameraStyle)i);
            bool manual=look!=null && look.ReadValue<Vector2>().sqrMagnitude>.0025f;
            manual|=Mouse.current!=null && Mouse.current.rightButton.isPressed && mouseLook!=null && mouseLook.ReadValue<Vector2>().sqrMagnitude>.01f;
            if(manual)chaseManualUntil=Time.time+1.25f;
            if(character && character.SlideRideActive)slideFollowUntil=Time.time+1;
            SlideFollowActive=Style==PootleCameraStyle.SlideFollow || automaticallyFollowSlides &&
                (Style==PootleCameraStyle.FreeOrbit || Style==PootleCameraStyle.CloseOrbit || Style==PootleCameraStyle.Chase) && Time.time<slideFollowUntil;
            float blend=1-Mathf.Exp(-Time.deltaTime*10);
            if(SlideFollowActive && character)
            {
                Vector3 velocity=Vector3.ProjectOnPlane(character.Velocity,Vector3.up);
                float speed=Mathf.InverseLerp(4,28,velocity.magnitude);
                float smooth=1-Mathf.Exp(-Time.deltaTime*4);
                orbit.Radius=Mathf.Lerp(orbit.Radius,Mathf.Lerp(8,10,speed),smooth);
                rig.Lens.FieldOfView=Mathf.Lerp(rig.Lens.FieldOfView,Mathf.Lerp(60,70,speed),smooth);
                if(Time.time>chaseManualUntil)
                {
                    float heading=velocity.sqrMagnitude>1?Mathf.Atan2(velocity.x,velocity.z)*Mathf.Rad2Deg:FacingYaw();
                    orbit.HorizontalAxis.Value=Mathf.SmoothDampAngle(orbit.HorizontalAxis.Value,heading,ref yawSpeed,.22f,200,Time.deltaTime);
                    orbit.VerticalAxis.Value=Mathf.Lerp(orbit.VerticalAxis.Value,34,smooth);
                }
                transition=Mathf.Max(0,transition-Time.deltaTime);return;
            }
            orbit.Radius=Mathf.Lerp(orbit.Radius,radius,blend);
            rig.Lens.FieldOfView=Mathf.Lerp(rig.Lens.FieldOfView,fov,blend);
            if(transition>0 || !AllowsLook)
            {
                orbit.HorizontalAxis.Value=Mathf.LerpAngle(orbit.HorizontalAxis.Value,yaw,blend);
                orbit.VerticalAxis.Value=Mathf.Lerp(orbit.VerticalAxis.Value,pitch,blend);
            }
            else if(Style==PootleCameraStyle.Chase)
            {
                if(manual)chaseManualUntil=Time.time+1.5f;
                if(Time.time>chaseManualUntil && character && character.Mode!=PootleMode.Climbing && Vector3.ProjectOnPlane(character.Velocity,Vector3.up).magnitude>.1f)
                    orbit.HorizontalAxis.Value=Mathf.LerpAngle(orbit.HorizontalAxis.Value,FacingYaw(),1-Mathf.Exp(-Time.deltaTime*3));
            }
            transition=Mathf.Max(0,transition-Time.deltaTime);
        }
        private void OnDisable()
        {
            if(instance){instance.Disable();Destroy(instance);}instance=null;
            for(int i=0;i<selectors.Length;i++)selectors[i]=null;
        }
    }
}
