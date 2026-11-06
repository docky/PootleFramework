using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
namespace Pootle.FairyDemo
{
    [DefaultExecutionOrder(100)]
    public sealed class FairyCourse : MonoBehaviour
    {
        public FairyController fairy;
        public FairyCollectible[] collectibles;
        public FairyHoop[] hoops;
        public FairySign[] signs;
        public TMP_Text counter, signText;
        public RectTransform signPanel;
        public UnityEngine.UI.Button resetButton;
        public GameObject finishBlossom;
        public int Collected {get;private set;}
        public int HoopsCleared {get;private set;}
        public FairySign NearbySign {get;private set;}
        public bool Complete => Collected==collectibles.Length&&HoopsCleared==hoops.Length&&collectibles.Length>0;
        Vector3 previousPosition;
        bool resetting, resetRequested;
        FairyTarget[] targets;
        void Start()
        {
            previousPosition=BodyPosition;
            fairy.Character.Teleported+=OnTeleport;
            targets=FindObjectsByType<FairyTarget>();
            if(resetButton)resetButton.onClick.AddListener(RequestReset);
            RefreshHud();
        }
        void OnDestroy()
        {
            if(fairy&&fairy.Character)fairy.Character.Teleported-=OnTeleport;
            if(resetButton)resetButton.onClick.RemoveListener(RequestReset);
        }
        Vector3 BodyPosition=>fairy.transform.position+Vector3.up*.41f;
        void OnTeleport(Vector3 delta)=>previousPosition=BodyPosition;
        public void RequestReset()=>resetRequested=true;
        void LateUpdate()
        {
            if(Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame)RequestReset();
            if(resetRequested){ResetCourse();return;}
            EvaluateSegment(previousPosition,BodyPosition);previousPosition=BodyPosition;
            UpdateSigns();
        }
        // Called after the movement tick, never from a trigger or a second movement owner.
        public void EvaluateSegment(Vector3 from,Vector3 to)
        {
            bool changed=false;
            var delta=to-from;float length=delta.sqrMagnitude;
            foreach(var item in collectibles)
            {
                if(!item||item.Collected)continue;
                float t=length>.000001f?Mathf.Clamp01(Vector3.Dot(item.transform.position-from,delta)/length):0;
                if(Vector3.Distance(from+delta*t,item.transform.position)>item.radius)continue;
                item.Collect();Collected++;changed=true;
            }
            foreach(var hoop in hoops)if(hoop&&hoop.TryCross(from,to,fairy.Character.Motor.Height*.5f)) {HoopsCleared++;changed=true;}
            if(changed){fairy.presentation.Cue(FairyCue.Use);RefreshHud();}
        }
        public void UpdateSigns()
        {
            FairySign best=null;float closest=float.PositiveInfinity;
            foreach(var sign in signs)
            {
                if(!sign||!sign.isActiveAndEnabled)continue;
                float distance=Vector3.Distance(fairy.transform.position,sign.transform.position);
                if(distance<sign.revealDistance&&distance<closest){closest=distance;best=sign;}
            }
            if(NearbySign!=best){if(NearbySign)NearbySign.Show(false);NearbySign=best;if(best)best.Show(true);}
            if(!signPanel)return;
            // NPC bubbles retain priority; signs never consume the gameplay button.
            bool shown=best&&string.IsNullOrEmpty(fairy.Speech);
            signPanel.gameObject.SetActive(shown);
            if(shown)signText.text=best.message;
        }
        public void ResetCourse()
        {
            if(resetting)return;resetting=true;resetRequested=false;
            foreach(var item in collectibles)if(item)item.ResetCollectible();
            foreach(var hoop in hoops)if(hoop)hoop.ResetHoop();
            if(targets!=null)foreach(var target in targets)if(target)target.ResetTarget();
            Collected=HoopsCleared=0;
            fairy.Respawn();previousPosition=BodyPosition;
            RefreshHud();UpdateSigns();resetting=false;
            // Avoid retaining keyboard submit selection on the clickable reset button.
            if(UnityEngine.EventSystems.EventSystem.current)UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }
        void RefreshHud()
        {
            if(counter)counter.text=Complete?"Grove complete!":$"Seeds {Collected}/{collectibles.Length}   Hoops {HoopsCleared}/{hoops.Length}";
            if(finishBlossom)finishBlossom.SetActive(Complete);
        }
    }
}
