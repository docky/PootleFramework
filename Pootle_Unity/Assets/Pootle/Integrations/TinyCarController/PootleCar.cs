using System.Collections;
using DavidJalbert.TinyCarController;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Pootle
{
    /// <summary>Transfers control from a nearby Pootle character to a Tiny Car Controller vehicle.</summary>
    [DefaultExecutionOrder(-20), DisallowMultipleComponent, RequireComponent(typeof(TCCPlayer))]
    public sealed partial class PootleCar : MonoBehaviour
    {
        public PootleFrobject interaction;
        public CinemachineCamera chaseCamera;
        public Vector3 cameraOffset = new Vector3(0, 3, -6);
        public Vector3 lookOffset = new Vector3(0, .8f, 0);
        [Min(0)] public float cameraSharpness = 8;
        public LayerMask obstructionLayers = Physics.DefaultRaycastLayers;
        public PootleCharacter Driver { get; private set; }
        public bool IsOccupied => Driver;
        public TCCPlayer Controller { get; private set; }
        public Transform Body => Controller.carBody.transform;
        InputActionAsset inputInstance;
        InputAction move, exit, brake, reset, jump, boost;
        Vector3 startingPosition, entryPosition;
        Quaternion startingRotation, entryRotation;
        bool initialized;
        AudioSource engine;
        RaycastHit[] hits = new RaycastHit[32];

        void Awake()
        {
            Controller = GetComponent<TCCPlayer>();
            if (!interaction) interaction = GetComponentInChildren<PootleFrobject>();
            var effects = GetComponentInChildren<TCCBasicEffects>();
            if (effects) engine = effects.audioEngine;
            foreach (var legacyInput in GetComponentsInChildren<TCCStandardInput>()) legacyInput.enabled = false;
        }

        void OnEnable()
        {
            if (interaction) { interaction.Interacted += Enter; interaction.interactable = true; }
            if (chaseCamera) chaseCamera.gameObject.SetActive(false);
        }

        IEnumerator Start()
        {
            startingPosition = Controller.getPosition(); startingRotation = Controller.getRotation();
            initialized = true;
            InitializeAbilities();
            // Let the scaled suspension settle onto the floor before locking the parked vehicle.
            Controller.setBrake(1);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            if (!IsOccupied) SetParked(true);
        }

        void Enter(PootleFrobjectSensor sensor)
        {
            if (!initialized || IsOccupied || !isActiveAndEnabled || !sensor || !sensor.PlayerReady || !chaseCamera) return;
            var character = sensor.Character;
            if (!character.input || !character.input.actions) return;
            inputInstance = Instantiate(character.input.actions);
            move = inputInstance.FindAction("Character/Move", true);
            reset = inputInstance.FindAction("Character/Reset", true);
            exit = new InputAction("Exit car", InputActionType.Button, "<Gamepad>/buttonEast");
            exit.AddBinding("<Keyboard>/b");
            brake = new InputAction("Car brake", InputActionType.Button, "<Gamepad>/rightTrigger");
            brake.AddBinding("<Keyboard>/leftCtrl");
            jump = new InputAction("Car jump", InputActionType.Button, "<Gamepad>/buttonSouth");
            jump.AddBinding("<Keyboard>/space");
            boost = new InputAction("Car boost", InputActionType.Button, "<Gamepad>/rightShoulder");
            boost.AddBinding("<Keyboard>/leftShift");
            move.Enable(); reset.Enable(); exit.Enable(); brake.Enable(); jump.Enable(); boost.Enable();
            Driver = character;
            entryPosition = character.transform.position;
            entryRotation = Quaternion.LookRotation(character.Facing);
            interaction.interactable = false;
            // Deactivate the entire avatar so its motor, collider, input, shadow and effects relinquish control.
            character.gameObject.SetActive(false);
            SetParked(false);
            UpdateCamera(true);
            chaseCamera.gameObject.SetActive(true);
            if (engine) engine.mute = false;
        }

        void Update()
        {
            if (!initialized) return;
            TickAbilities(Time.deltaTime);
            if (Body.position.y < -25 || IsOccupied && reset.WasPressedThisFrame())
            {
                Controller.immobilize(); Controller.setRotation(startingRotation); Controller.setPosition(startingPosition);
                Physics.SyncTransforms();
                if (IsOccupied) RestoreDriver(FindExit(out var safe) ? safe : entryPosition, entryRotation);
                SetParked(true); return;
            }
            if (!IsOccupied) { Controller.setMotor(0); Controller.setSteering(0); Controller.setBrake(1); if (engine) engine.mute = true; return; }
            if (exit.WasPressedThisFrame()) { TryExit(); return; }
            if (jump.WasPressedThisFrame()) TryJump();
            if (boost.WasPressedThisFrame()) TryBoost();
            Vector2 axes = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1);
            Controller.setMotor(axes.y);
            Controller.setSteering(axes.x);
            Controller.setBrake(brake.IsPressed() ? 1 : 0);
        }

        public bool TryExit()
        {
            if (!IsOccupied) return false;
            Controller.immobilize(); Controller.setBrake(1);
            Physics.SyncTransforms();
            if (!FindExit(out Vector3 destination)) return false;
            Vector3 facing = Vector3.ProjectOnPlane(Body.forward, Vector3.up).normalized;
            RestoreDriver(destination, facing.sqrMagnitude > .01f ? Quaternion.LookRotation(facing) : entryRotation);
            SetParked(true);
            return true;
        }

        bool FindExit(out Vector3 destination)
        {
            // Try both doors, then behind/in front. A blocked door must never embed Lily in a wall.
            Vector3[] offsets = { new Vector3(-1, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, -1.5f), new Vector3(0, 0, 1.5f) };
            var yaw = Quaternion.Euler(0, Body.eulerAngles.y, 0);
            foreach (var offset in offsets)
            {
                Vector3 candidate = Body.position + yaw * Vector3.Scale(offset, transform.lossyScale);
                int count = Cast(candidate + Vector3.up * 2, Vector3.down, 5);
                float nearest = float.PositiveInfinity;
                RaycastHit ground = default;
                for (int i = 0; i < count; i++)
                    if (!OwnCollider(hits[i].collider) && hits[i].distance < nearest)
                    { ground = hits[i]; nearest = hits[i].distance; }
                if (!ground.collider || Vector3.Angle(ground.normal, Vector3.up) > Driver.Motor.slopeLimit) continue;
                candidate = ground.point + Vector3.up * (Driver.Motor.Skin * 3 + .01f);
                if (!Driver.Motor.CanOccupy(candidate)) continue;
                destination = candidate; return true;
            }
            destination = default; return false;
        }

        void SetParked(bool value)
        {
            Controller.immobilize(); Controller.setBrake(value ? 1 : 0);
            var constraints = value ? RigidbodyConstraints.FreezeAll : RigidbodyConstraints.None;
            Controller.carBody.setConstraints(constraints);
            Controller.getCarRigidBody().constraints = constraints;
            foreach (var wheel in Controller.wheels)
            { wheel.setConstraints(constraints); wheel.getRigidbody().constraints = constraints; }
        }

        void RestoreDriver(Vector3 position, Quaternion rotation)
        {
            StopBoost();
            var character = Driver; Driver = null;
            if (chaseCamera) chaseCamera.gameObject.SetActive(false);
            ReleaseInput();
            if (character)
            {
                character.Teleport(position, rotation);
                character.gameObject.SetActive(true);
            }
            if (interaction) interaction.interactable = true;
            if (engine) engine.mute = true;
        }

        void LateUpdate()
        {
            if (!IsOccupied) return;
            Driver.transform.position = Body.position;
            UpdateCamera(false);
        }

        void UpdateCamera(bool snap)
        {
            Quaternion yaw = Quaternion.Euler(0, Body.eulerAngles.y, 0);
            Vector3 target = Body.position + lookOffset;
            Vector3 desired = Body.position + yaw * cameraOffset;
            float blend = snap ? 1 : 1 - Mathf.Exp(-cameraSharpness * Time.deltaTime);
            desired = Vector3.Lerp(chaseCamera.transform.position, desired, blend);
            Vector3 delta = desired - target;
            int count;
            while ((count = Physics.SphereCastNonAlloc(target, .2f, delta.normalized, hits, delta.magnitude, obstructionLayers, QueryTriggerInteraction.Ignore)) == hits.Length)
                System.Array.Resize(ref hits, hits.Length * 2);
            float distance = delta.magnitude;
            for (int i = 0; i < count; i++) if (!OwnCollider(hits[i].collider)) distance = Mathf.Min(distance, Mathf.Max(.1f, hits[i].distance - .05f));
            desired = target + delta.normalized * distance;
            chaseCamera.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(target - desired, Vector3.up));
        }

        int Cast(Vector3 origin, Vector3 direction, float distance)
        {
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, direction, hits, distance, Driver.Motor.collisionMask, QueryTriggerInteraction.Ignore)) == hits.Length)
                System.Array.Resize(ref hits, hits.Length * 2);
            return count;
        }
        bool OwnCollider(Collider shape) => shape.transform.IsChildOf(transform) || Driver && shape.transform.IsChildOf(Driver.transform);

        void ReleaseInput()
        {
            exit?.Dispose(); brake?.Dispose(); jump?.Dispose(); boost?.Dispose();
            exit = brake = jump = boost = move = reset = null;
            if (inputInstance) { inputInstance.Disable(); Destroy(inputInstance); inputInstance = null; }
        }
        void OnDisable()
        {
            if (interaction) interaction.Interacted -= Enter;
            if (IsOccupied)
            {
                Vector3 position = FindExit(out var safe) ? safe : entryPosition;
                RestoreDriver(position, entryRotation);
            }
            else ReleaseInput();
            StopBoost();
            if (interaction) interaction.interactable = false;
            if (initialized && Controller && Controller.getCarRigidBody()) SetParked(true);
        }
    }
}
