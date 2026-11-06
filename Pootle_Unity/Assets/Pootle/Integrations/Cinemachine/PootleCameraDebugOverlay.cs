using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle
{
    // Optional development overlay, following the project's existing OnGUI debug tools.
    public sealed class PootleCameraDebugOverlay : MonoBehaviour
    {
        public PootleZoneCameraDirector director;
        public bool showOverlay = true;
        GUIStyle style;
        void Update() { if(Keyboard.current!=null && Keyboard.current.f8Key.wasPressedThisFrame)showOverlay=!showOverlay; }
        void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(!showOverlay || !director)return;
            if(style==null)style=new GUIStyle(GUI.skin.label){fontSize=13,richText=true,normal={textColor=new Color(.92f,.94f,.81f)}};
            var camera=director.ActiveCamera;
            var shot=camera?camera.GetComponent<PootleExplorationShot>():null;
            var basis=director.movementBasis;
            GUI.color=new Color(.08f,.16f,.17f,.93f);GUI.DrawTexture(new Rect(14,14,410,142),Texture2D.whiteTexture);GUI.color=Color.white;
            string blend=director.brain && director.brain.IsBlending ? director.brain.ActiveBlend.Description : "Settled";
            GUI.Label(new Rect(26,22,385,130),"<b>EXPLORATION CAMERA</b>   ·   F8 hide\n"+
                (camera?camera.name:"No camera")+"\n"+(shot?shot.mode.ToString():"Legacy follow")+" · "+director.Status+"\n"+
                blend+"\nBasis: "+(basis?basis.Yaw.ToString("F1")+"° · "+(basis.IsLocked?"HELD until input release":"camera relative"):"not connected")+
                (shot && shot.mode==ExplorationCameraMode.RailFollow?" · rail "+shot.RailT.ToString("F2"):"")+"\nMove: WASD / left stick · jump / glide unchanged",style);
#endif
        }
    }
}
