using UnityEngine;
using Pootle;
namespace OinkRomp
{
    [DefaultExecutionOrder(50), RequireComponent(typeof(PootleCharacter))]
    public sealed class OinkRompController : MonoBehaviour, IPootleMovementPolicy
    {
        public bool simulateAutomatically = true;
        public Animator animator;
        public OinkRompCourse course;
        public OinkRompBalloons balloons;
        public Transform body;
        public AudioClip hopSound, inflateSound, popSound, eatSound, bumpSound;
        [Min(.1f)] public float balloonLifetime = 7;
        [Min(.1f)] public float holdToPop = .42f;
        [Range(0,7)] public int startingBalloons;
        public int maximumSupply = 7;
        public int BalloonCapacity { get; private set; }
        public int BalloonSupply => BalloonCapacity; // Capacity, never consumable inventory.
        public int AvailableBalloons => Mathf.Max(0,BalloonCapacity-deployed);
        public int Apples { get; private set; }
        public int Hop { get; private set; }
        public int InflatedCount { get { int n=0; foreach(float life in lives) if(life>0)n++;return n; } }
        public bool InMud { get; private set; }
        public bool Scrambling { get; private set; }
        public bool FastDrop { get; private set; }
        public float LastSlamHeight { get; private set; }
        public float LastSlamStrength { get; private set; }
        public int SlamCount { get; private set; }
        public string AnimationState => animationState;
        public Vector3 Checkpoint { get; set; }
        public PootleCharacter Character { get; private set; }
        public string Message { get; private set; }
        public float MessageUntil { get; private set; }
        public float[] BalloonLives => lives;
        readonly float[] lives = new float[7];
        PootleProfile original, runtime;
        PootleMovementFeedback feedback;
        PootleCaveTravel caveTravel;
        PootleCaveLighting[] caveVolumes;
        AudioSource audioSource;
        float held, coyote, buffered, specialUntil, dropFrom;
        int deployed;
        bool holdConsumed, variableHop, jumpedThisTick;
        string animationState;
        void Awake()
        {
            Character=GetComponent<PootleCharacter>();
            original=Character.profile; runtime=Instantiate(original); Character.profile=runtime;
            Character.simulateAutomatically=false; Character.movementPolicy=this;
            Checkpoint=transform.position; BalloonCapacity=Mathf.Clamp(startingBalloons,0,7);
            audioSource=GetComponent<AudioSource>();
            if(!audioSource)audioSource=gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake=false;audioSource.spatialBlend=0;
            feedback=GetComponent<PootleMovementFeedback>();
            Character.Landed+=OnLanding;Character.Teleported+=OnTeleport;Character.Jumped+=OnJumped;
            caveTravel=FindAnyObjectByType<PootleCaveTravel>();
            caveVolumes=FindObjectsByType<PootleCaveLighting>();
            if(animator){animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
        }
        void Update(){if(simulateAutomatically && Character.isActiveAndEnabled)Step(Time.deltaTime,Character.input?Character.input.Sample():default);}
        public void Step(float dt,PootleInputFrame frame)
        {
            if(dt<=0 || !float.IsFinite(dt) || !Character.isActiveAndEnabled)return;
            if(caveTravel && caveTravel.Busy){Animate();return;}
            dt=Mathf.Min(dt,.1f);jumpedThisTick=false;
            if(frame.resetPressed || transform.position.y<-.8f){Respawn();return;}
            frame.glide=false; // Balloon lift replaces the glider; all Coastal movement bindings remain.
            InMud=course && course.IsInMud(transform.position);
            Scrambling=course && course.IsOnBank(transform.position) && frame.move.sqrMagnitude>.04f;
            if(!frame.jumpHeld){held=0;holdConsumed=false;}else held+=dt;
            if(frame.jumpHeld && held>=holdToPop && !holdConsumed && !Character.IsGrounded)
            {
                holdConsumed=true;FastDrop=true;dropFrom=transform.position.y;
                System.Array.Clear(lives,0,lives.Length);buffered=0;variableHop=false;
                frame.detachPressed=true;Sound(popSound);
            }
            if(course)course.BeforeStep(this,frame,dt);
            Character.Tick(dt,frame);
            if(course)course.AfterStep(this,dt);
            if(feedback)feedback.Tick(dt);
            Animate();
        }
        public float BeforeMovement(PootleCharacter character,float dt,PootleInputFrame input,bool wasGrounded)
        {
            if(character.IsGrounded){Recharge();coyote=original.coyoteTime;}
            else{coyote=Mathf.Max(0,coyote-dt);if(Hop==0 && coyote==0)Hop=1;}
            for(int i=0;i<lives.Length;i++)lives[i]=Mathf.Max(0,lives[i]-dt);
            if(input.jumpPressed && !FastDrop && !jumpedThisTick)buffered=original.jumpBufferTime;else buffered=Mathf.Max(0,buffered-dt);
            float impulse=float.NaN;
            bool balloonLaunched=false;
            if(buffered>0 && !FastDrop)
            {
                if(character.IsGrounded || coyote>0 && Hop==0)
                {Hop=1;impulse=original.jumpLaunchSpeed;coyote=buffered=0;variableHop=true;}
                else if(deployed<BalloonCapacity)
                {
                    lives[deployed++]=balloonLifetime;Hop=1+deployed;impulse=8.5f;buffered=0;variableHop=false;Sound(inflateSound);
                    balloonLaunched=true;
                }
            }
            if(variableHop && !input.jumpHeld && character.Velocity.y>original.jumpReleaseSpeed)
            {impulse=original.jumpReleaseSpeed;variableHop=false;}
            if(InflatedCount>0 && IsInsideCave())
            {
                System.Array.Clear(lives,0,lives.Length);
                // An immediately burst balloon supplies neither a jump impulse nor hover gravity.
                if(balloonLaunched)impulse=float.NaN;
                Sound(popSound);
            }
            bool floating=InflatedCount>0;
            runtime.risingGravity=floating?5:original.risingGravity;
            runtime.fallingGravity=FastDrop?85:floating?3:original.fallingGravity;
            runtime.maximumFallSpeed=FastDrop?55:floating?1.8f:original.maximumFallSpeed;
            runtime.walkSpeed=InMud?original.walkSpeed*.4f:original.walkSpeed;
            runtime.runSpeed=InMud?original.runSpeed*.4f:original.runSpeed;
            runtime.groundAcceleration=original.groundAcceleration;
            return impulse;
        }
        bool IsInsideCave()
        {
            // Sample the authored interior volumes directly; lighting updates later in the frame.
            foreach(var cave in caveVolumes)
                if(cave && cave.isActiveAndEnabled && cave.localInterior.Contains(cave.transform.InverseTransformPoint(transform.position)))return true;
            return false;
        }
        public Vector3 AirVelocity(Vector3 current,Vector3 direction,float magnitude,float dt)
        {
            if(magnitude<=0)return current;
            bool running=Character.IsRunning;
            return PootleMovement.Accelerate(current,direction,running?1:magnitude,original.airAcceleration,original.airTurningDrag,running?original.runSpeed:original.walkSpeed,original.minimumAnalogueSpeed,dt);
        }
        void OnJumped()
        {
            jumpedThisTick=true;
            var spring=Character.Support.collider?Character.Support.collider.GetComponentInParent<PootleSpring>():null;
            if(spring && Vector3.Dot(Character.Velocity,spring.Direction)>=spring.launchSpeed-.01f)Recharge();
            if(Hop==0){Hop=1;coyote=buffered=0;variableHop=false;}
        }
        void Recharge(){Hop=deployed=0;FastDrop=false;variableHop=false;System.Array.Clear(lives,0,lives.Length);}
        void OnTeleport(Vector3 delta){Recharge();held=coyote=buffered=0;holdConsumed=false;}
        void OnLanding(float speed)
        {
            if(FastDrop)
            {
                LastSlamHeight=Mathf.Max(0,dropFrom-transform.position.y);
                LastSlamStrength=Mathf.Clamp01(LastSlamHeight/12f);SlamCount++;
                if(course)course.Slam(this,LastSlamHeight);
                Sound(bumpSound);PlaySpecial("Attack",.35f);
            }
            Recharge();
        }
        public void Spring(float speed)
        {
            Recharge();Hop=1;coyote=buffered=0;
            Character.ApplyImpulse(Vector3.ProjectOnPlane(Character.Velocity,Vector3.up)+Vector3.up*speed);
            Sound(hopSound);
        }
        public void EatApple(){Apples++;PlaySpecial("Eat",.65f);Sound(eatSound);}
        public int AddBalloons(int count)
        {
            int before=BalloonCapacity;BalloonCapacity=Mathf.Clamp(BalloonCapacity+Mathf.Max(0,count),0,7);
            if(before!=BalloonCapacity){Sound(inflateSound);Notify("Balloon "+BalloonCapacity+" / 7 — recharges when you land.",3);}
            return BalloonCapacity-before;
        }
        public void Bump(){PlaySpecial("Attack",.6f);Sound(bumpSound);}
        public void Notify(string text,float seconds=3){Message=text;MessageUntil=Time.time+seconds;}
        public void Respawn(){Character.Teleport(Checkpoint,Quaternion.identity);}
        void Sound(AudioClip clip){if(clip && audioSource)audioSource.PlayOneShot(clip,.5f);}
        void PlaySpecial(string state,float seconds){specialUntil=Time.time+seconds;SetAnimation(state);}
        void SetAnimation(string state)
        {if(!animator || animationState==state)return;animationState=state;animator.CrossFadeInFixedTime("Base Layer."+state,.12f,0);}
        void Animate()
        {
            if(Time.time<specialUntil)return;
            var mode=Character.Mode;
            SetAnimation(InMud?"Swim":mode==PootleMode.Dangling||InflatedCount>0?"Fly":mode==PootleMode.Sliding||mode==PootleMode.RailSliding?"Swim":!Character.IsGrounded&&mode!=PootleMode.RailWalking?"Jump":Character.LocomotionSpeed>.2f?Character.IsRunning||Scrambling?"Run":"Walk":"Idle_A");
        }
        void OnDestroy()
        {
            if(Character){Character.Jumped-=OnJumped;Character.Landed-=OnLanding;Character.Teleported-=OnTeleport;if(original)Character.profile=original;}
            if(runtime)Destroy(runtime);
        }
    }
}
