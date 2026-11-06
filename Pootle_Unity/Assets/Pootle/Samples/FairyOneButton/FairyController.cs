using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle.FairyDemo
{
    public enum FairyAction { Settling, Launch, Windup, Talk, Press, Dialogue, Flight }
    [RequireComponent(typeof(PootleCharacter))]
    public sealed class FairyController : MonoBehaviour, IPootleMovementPolicy
    {
        public FairyTuning tuning;
        public InputActionAsset controls;
        public FairyPresentation presentation;
        public bool readDevices=true;
        public FairyAction CurrentAction { get; private set; }
        public FairyTarget Focus { get; private set; }
        public FairyTarget Speaker { get; private set; }
        public string Speech { get; private set; }="";
        public int Flaps { get; private set; }
        public float Settle { get; private set; }
        public float Windup { get; private set; }
        public float ActualSpeed { get; private set; }
        public bool ConfirmedGround { get; private set; }
        public int LaunchCount { get; private set; }
        public int FlapCount { get; private set; }
        public PootleCharacter Character { get; private set; }
        public Vector3 GroundFacing { get; private set; }=Vector3.forward;
        FairyTarget[] targets;
        FairyTarget suppressed;
        InputActionAsset instance;
        InputAction move, action, back;
        InputActionRebindingExtensions.RebindingOperation rebind;
        Vector3 spawn, windupPosition;
        bool heldLast, press, backPressed, moved, manual, suppressionMoved;
        float speechTimer;
        int page;
        void Awake()
        {
            Character=GetComponent<PootleCharacter>();
            Character.simulateAutomatically=false; Character.movementPolicy=this;
            spawn=transform.position; Flaps=tuning.flapCount;
            Character.Teleported+=OnTeleport;
            Character.Teleport(spawn,Quaternion.Euler(0,180,0));
        }
        void OnEnable()
        {
            if(!controls)return;
            instance=Instantiate(controls);
            move=instance.FindAction("Fairy/Move",true); action=instance.FindAction("Fairy/Action",true); back=instance.FindAction("Fairy/Back",true);
            instance.Enable();
        }
        void Start()=>RefreshTargets();
        public void RefreshTargets()=>targets=FindObjectsByType<FairyTarget>();
        void OnDisable()
        {
            rebind?.Dispose(); rebind=null;
            if(instance){instance.Disable();Destroy(instance);} instance=null;
        }
        void OnDestroy(){if(Character)Character.Teleported-=OnTeleport;}
        void Update()
        {
            if(readDevices)Step(Time.deltaTime,move?.ReadValue<Vector2>()??Vector2.zero,action!=null&&(action.IsPressed()||action.WasPressedThisFrame()),back!=null&&back.WasPressedThisFrame());
        }
        // Also used by deterministic Play Mode checks. Held state, not press events, guarantees release gating.
        public void Step(float dt, Vector2 stick, bool held, bool cancel=false)
        {
            if(dt<=0)return;
            dt=Mathf.Min(dt,.1f);
            stick=new Vector2(Mathf.Abs(stick.x)>.35f?Mathf.Sign(stick.x):0,Mathf.Abs(stick.y)>.35f?Mathf.Sign(stick.y):0).normalized;
            press=held&&!heldLast;heldLast=held;backPressed=cancel;moved=stick.sqrMagnitude>0;
            if(transform.position.y < -5){Respawn();return;}
            var start=transform.position;
            Character.Tick(dt,new PootleInputFrame{move=stick});
            ActualSpeed=Vector3.ProjectOnPlane(transform.position-start,Vector3.up).magnitude/dt;
            ConfirmedGround=Character.IsGrounded&&Character.Support.walkable&&Character.Support.central&&Character.Support.distance<=Character.Motor.Skin*3;
            if(ConfirmedGround)
            {
                Flaps=tuning.flapCount;
                if(moved)GroundFacing=PootleMovement.CameraDirection(stick,Character.outputCamera,out _);
                Settle=!moved&&ActualSpeed<tuning.settleSpeed?Settle+dt:0;
            }
            else Settle=0;
            UpdateSpeech(dt);
            if(suppressed&&(Vector3.Distance(transform.position,suppressed.focusPoint.position)>suppressed.releaseDistance || suppressionMoved&&Settle>=tuning.settleTime))suppressed=null;
            ResolveAction();
            press=false;
        }
        public float BeforeMovement(PootleCharacter c,float dt,PootleInputFrame input,bool groundedAtTickStart)
        {
            var shown=CurrentAction;var shownTarget=Focus;
            if(Windup>0)Windup=Mathf.Max(0,Windup-dt);
            if(moved)
            {
                Windup=0;Settle=0;SetFocus(null);
                if(suppressed)suppressionMoved=true;
            }
            bool supported=c.IsGrounded&&c.Support.central&&c.Support.walkable&&c.Support.distance<=c.Motor.Skin*3;
            if(!supported || Vector3.Distance(transform.position,windupPosition)>tuning.windupRadius)Windup=0;
            if(backPressed&&manual){CloseDialogue();press=false;return float.NaN;}
            if(manual&&Speaker&&Vector3.Distance(transform.position,Speaker.focusPoint.position)>2.1f)CloseDialogue();
            // Input captured in flight retains flight ownership even if this tick finds the floor.
            if(!groundedAtTickStart || !supported)
            {
                SetFocus(null); Windup=0;
                if(manual&&press){AdvanceDialogue();return float.NaN;}
                if(!press)return float.NaN;
                // A touchdown-frame flap spends the existing flight budget; it cannot refresh-and-spend or use a prop.
                if(Flaps<=0){presentation?.Cue(FairyCue.Empty);return float.NaN;}
                float boost=Mathf.Min(tuning.flapCap,Mathf.Max(0,c.Velocity.y)+tuning.flapBoost);
                if(boost-c.Velocity.y<tuning.minimumLift){presentation?.Cue(FairyCue.Fast);return float.NaN;}
                Flaps--;FlapCount++;presentation?.Cue(FairyCue.Flap);return boost;
            }
            if(manual){if(press)AdvanceDialogue();return float.NaN;}
            if(!press || moved || Settle<tuning.settleTime || ActualSpeed>=tuning.settleSpeed)return float.NaN;
            if(shown==FairyAction.Talk||shown==FairyAction.Press)
            {
                if(shownTarget&&Eligible(shownTarget,true))Use(shownTarget);
                return float.NaN; // A stale prompt consumes its press, never becomes a launch.
            }
            if(shown==FairyAction.Windup&&Windup>0)
            {
                Windup=0;Settle=0;LaunchCount++;presentation?.Cue(FairyCue.Launch);
                if(Speaker&&!manual){Speech="Oh! Up you go, then.";speechTimer=2.6f;}
                return tuning.launchSpeed;
            }
            if(shown!=FairyAction.Launch)return float.NaN;
            windupPosition=transform.position;Windup=tuning.doubleTapWindow;
            SetFocus(null);presentation?.Cue(FairyCue.Windup);return float.NaN;
        }
        public Vector3 AirVelocity(Vector3 current,Vector3 direction,float magnitude,float dt)
            =>Vector3.MoveTowards(current,direction*(tuning.airSpeed*magnitude),(magnitude>0?tuning.airAcceleration:tuning.airBraking)*dt);
        void ResolveAction()
        {
            if(manual){CurrentAction=FairyAction.Dialogue;SetFocus(null);return;}
            if(!ConfirmedGround){CurrentAction=FairyAction.Flight;SetFocus(null);return;}
            if(moved||Settle<tuning.settleTime){CurrentAction=FairyAction.Settling;SetFocus(null);return;}
            if(Windup>0){CurrentAction=FairyAction.Windup;return;}
            if(Focus&&!Eligible(Focus,true))SetFocus(null);
            if(!Focus && targets!=null)
            {
                FairyTarget best=null;float bestDot=-2,bestDistance=float.MaxValue;
                foreach(var t in targets)
                {
                    if(!Eligible(t,false))continue;
                    var delta=t.focusPoint.position-transform.position;float distance=delta.magnitude;
                    float dot=Vector3.Dot(GroundFacing,Vector3.ProjectOnPlane(delta,Vector3.up).normalized);
                    if(dot>bestDot+.001f || Mathf.Abs(dot-bestDot)<=.001f&&(distance<bestDistance-.001f || Mathf.Abs(distance-bestDistance)<=.001f&&(!best||t.stableOrder<best.stableOrder)))
                    {best=t;bestDot=dot;bestDistance=distance;}
                }
                SetFocus(best);
            }
            CurrentAction=Focus?((Focus.kind==FairyTargetKind.Plunger||Focus.kind==FairyTargetKind.Reset)?FairyAction.Press:FairyAction.Talk):FairyAction.Launch;
        }
        bool Eligible(FairyTarget t,bool retained)
        {
            if(!t||!t.isActiveAndEnabled||!t.focusPoint||t==suppressed||t==Speaker)return false;
            Vector3 delta=t.focusPoint.position-transform.position;
            if(delta.magnitude>(retained?t.releaseDistance:t.acquireDistance))return false;
            if(Vector3.Dot(GroundFacing,Vector3.ProjectOnPlane(delta,Vector3.up).normalized)<t.facingDot)return false;
            var origin=transform.position+Vector3.up*.35f;var end=t.focusPoint.position+Vector3.up*.35f;
            foreach(var hit in Physics.RaycastAll(origin,(end-origin).normalized,(end-origin).magnitude,Character.Motor.collisionMask,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&!hit.transform.IsChildOf(t.transform))return false;
            return true;
        }
        void SetFocus(FairyTarget t){if(Focus==t)return;if(Focus)Focus.Highlight(false);Focus=t;if(Focus)Focus.Highlight(true);}
        void Use(FairyTarget t)
        {
            t.Use();SetFocus(null);presentation?.Cue(FairyCue.Use);
            if(t.kind==FairyTargetKind.Plunger||t.kind==FairyTargetKind.Reset)return;
            Speaker=t;page=0;manual=t.kind==FairyTargetKind.Dialogue;speechTimer=4;
            Speech=manual?"The flowers make lovely stepping stones.":"A fine day for a little flutter!";
        }
        void AdvanceDialogue(){presentation?.Cue(FairyCue.Use);if(++page>=2)CloseDialogue();else Speech="Try the rainbow. Mind your head!";}
        void CloseDialogue(){suppressed=Speaker;suppressionMoved=false;manual=false;Speaker=null;Speech="";SetFocus(null);Windup=0;Settle=0;press=false;}
        void UpdateSpeech(float dt)
        {
            if(!Speaker){Speech="";return;}
            if(manual)return;
            speechTimer-=dt;
            if(speechTimer<=0 || ConfirmedGround&&Vector3.Distance(transform.position,Speaker.focusPoint.position)>2.2f){Speaker=null;Speech="";}
        }
        public void Respawn(){Character.Teleport(spawn,Quaternion.identity);}
        void OnTeleport(Vector3 delta)
        {
            GroundFacing=Character.Facing;Windup=Settle=ActualSpeed=0;Flaps=tuning.flapCount;SetFocus(null);Speaker=suppressed=null;Speech="";manual=false;press=false;heldLast=true;ConfirmedGround=false;CurrentAction=FairyAction.Settling;
            presentation?.ResetPose();
        }
        // Binding indices are those in the local FairyControls asset. Runtime overrides never touch standard Pootle maps.
        public void RebindJoystickButton(int bindingIndex)
        {
            if(action==null)return;
            rebind?.Dispose();action.Disable();
            rebind=action.PerformInteractiveRebinding(bindingIndex).WithControlsHavingToMatchPath("<Joystick>/<Button>")
                .OnComplete(op=>{op.Dispose();rebind=null;action.Enable();heldLast=true;})
                .OnCancel(op=>{op.Dispose();rebind=null;action.Enable();}).Start();
        }
    }
}
