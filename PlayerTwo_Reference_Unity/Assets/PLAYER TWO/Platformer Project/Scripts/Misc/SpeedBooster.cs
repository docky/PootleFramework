using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	[RequireComponent(typeof(Collider))]
	[AddComponentMenu("PLAYER TWO/Platformer Project/Misc/Speed Booster")]
	public class SpeedBooster : MonoBehaviour
	{
		public enum BoostDirection
		{
			Forward,
			Upward,
		}

		public enum BoostMode
		{
			Clamp,
			Additive,
		}

		[Header("Speed Booster Settings")]
		[Tooltip("Direction in which the player is boosted.")]
		public BoostDirection boostDirection = BoostDirection.Forward;

		[Tooltip(
			"How the boost force affects the player's speed. Clamp: the player's speed is raised to at least the boost force. Additive: the boost force is added on top of the player's current speed."
		)]
		public BoostMode boostMode = BoostMode.Clamp;

		[Tooltip(
			"If true, the boost counts as a jump. It's useful for allowing double jump or other jump-based abilities."
		)]
		public bool countAsJump;

		[Tooltip("The force applied to the player when boosting speed.")]
		public float boostForce = 40f;

		[Tooltip("Angle at which the player is boosted.")]
		[Range(0f, 90f)]
		public float boostAngle;

		[Tooltip("Duration to lock player input after boosting speed.")]
		public float inputLockDuration = 0.5f;

		[Tooltip("Sound played when the player boosts speed.")]
		public AudioClip boostSound;

		[Tooltip(
			"If true, the booster will trigger the BoostingPlayerState when boosting upwards."
		)]
		public bool triggerBoostState;

		[Header("Positioning Settings")]
		[Tooltip(
			"If true, the player will be repositioned to the center of the booster when boosting."
		)]
		public bool repositionPlayer;

		[Tooltip(
			"Offset applied to the player's position relative to the booster when repositioning."
		)]
		public Vector3 positionOffset;

		[Header("Camera Settings")]
		[Tooltip("If true, the camera will aim towards the boost direction.")]
		public bool aimToBoostDirection = true;

		[Tooltip("Time in seconds the camera takes to aim towards the boost direction.")]
		public float aimDuration = 0.25f;

		[Tooltip(
			"Yaw offset applied to the aiming direction. Values different than 0 rotates the camera sideways."
		)]
		public float aimYawOffset;

		[Tooltip(
			"Pitch offset applied to the aiming direction. Values different than 0 rotates the camera up or down."
		)]
		public float aimPitchOffset = 10;

		protected Collider m_collider;
		protected AudioSource m_audioSource;

		protected readonly float k_upwardThreshold = 0.1f;

		protected virtual void Start()
		{
			InitializeCollider();
			InitializeAudioSource();
		}

		protected virtual void OnTriggerEnter(Collider other)
		{
			if (!other.CompareTag(GameTags.Player))
				return;

			if (other.TryGetComponent(out Player player))
			{
				HandleBoost(player);
				HandleCamera(player);
				PlayBoostSound();
			}
		}

		protected virtual void OnDrawGizmosSelected()
		{
			var boostDir = GetBoostDirection();
			var approximateLength = 0.3f * boostForce;

			Gizmos.color = Color.cyan;
			Gizmos.DrawRay(transform.position, boostDir * approximateLength);
		}

		protected virtual void InitializeCollider()
		{
			m_collider = GetComponent<Collider>();
			m_collider.isTrigger = true;
		}

		protected virtual void InitializeAudioSource()
		{
			if (!TryGetComponent(out m_audioSource))
				m_audioSource = gameObject.AddComponent<AudioSource>();
		}

		protected virtual void HandleBoost(Player player)
		{
			HandlePlayerState(player);
			HandlePlayerAbilities(player);
			HandlePositioning(player);
			HandleBoostForce(player);
		}

		protected virtual void HandlePlayerState(Player player)
		{
			if (player.onRails)
				return;

			var boostDirection = GetBoostDirection();

			if (Vector3.Dot(boostDirection, player.transform.up) > k_upwardThreshold)
			{
				if (triggerBoostState)
					player.states.Change<BoostingPlayerState>();
				else
					player.states.Change<FallPlayerState>();
			}
			else if (!player.states.IsCurrentOfType(typeof(RollingPlayerState)))
				player.states.Change<WalkPlayerState>();
		}

		protected virtual void HandlePositioning(Player player)
		{
			if (!repositionPlayer || player.onRails)
				return;

			var targetPosition = transform.position + transform.rotation * positionOffset;
			player.transform.position = targetPosition;
		}

		protected virtual void HandlePlayerAbilities(Player player)
		{
			if (player.onRails)
				return;

			player.ResetJumps();
			player.ResetAirDash();
			player.ResetAirSpins();

			if (countAsJump)
				player.SetJumps(1);
		}

		protected virtual void HandleBoostForce(Player player)
		{
			if (player.onRails)
				HandleRailBoostForce(player);
			else
				HandleRegularBoostForce(player);
		}

		protected virtual void HandleRegularBoostForce(Player player)
		{
			var boostDirection = GetBoostDirection();
			var worldToLocal = Quaternion.FromToRotation(player.transform.up, Vector3.up);
			var localBoostDirection = worldToLocal * boostDirection;

			if (player.isGrounded)
				player.verticalVelocity = Vector3.zero;

			if (localBoostDirection.y > 0)
				player.SetJumps(1);

			var lateralBoost = new Vector3(localBoostDirection.x, 0f, localBoostDirection.z);
			var verticalBoost = new Vector3(0f, localBoostDirection.y, 0f);
			var targetLateralSpeed = GetTargetLateralSpeed(player, lateralBoost);

			player.lateralVelocity = lateralBoost * targetLateralSpeed;
			player.verticalVelocity = GetTargetVerticalVelocity(player, verticalBoost);
			player.targetMoveDirection = player.lateralVelocity.normalized;
			player.inputs.LockMovementDirection(inputLockDuration);
			player.FaceDirection(player.lateralVelocity.normalized);
		}

		protected virtual void HandleRailBoostForce(Player player)
		{
			var direction = GetBoostDirection();
			var targetSpeed = boostForce;

			if (boostMode == BoostMode.Additive)
				targetSpeed += GetSpeedAlong(player.velocity, direction);

			player.velocity = direction * targetSpeed;
			player.FaceDirection(direction, Space.World);
		}

		/// <summary>
		/// Returns the lateral speed the player should have after boosting, based on the boost mode.
		/// </summary>
		/// <param name="player">The Player being boosted.</param>
		/// <param name="lateralBoost">The lateral component of the local boost direction.</param>
		protected virtual float GetTargetLateralSpeed(Player player, Vector3 lateralBoost)
		{
			var currentLateralSpeed = player.lateralVelocity.magnitude;
			var lateralBoostSpeed = boostForce * lateralBoost.magnitude;

			if (boostMode == BoostMode.Additive)
				return currentLateralSpeed + lateralBoostSpeed;

			return Mathf.Max(currentLateralSpeed, lateralBoostSpeed);
		}

		/// <summary>
		/// Returns the local vertical velocity the player should have after boosting, based on the boost mode.
		/// In additive mode, only the current vertical speed moving along the boost direction is kept.
		/// </summary>
		/// <param name="player">The Player being boosted.</param>
		/// <param name="verticalBoost">The vertical component of the local boost direction.</param>
		protected virtual Vector3 GetTargetVerticalVelocity(Player player, Vector3 verticalBoost)
		{
			var targetVelocity = verticalBoost * boostForce;

			if (boostMode == BoostMode.Additive)
			{
				var direction = verticalBoost.normalized;
				targetVelocity += direction * GetSpeedAlong(player.verticalVelocity, direction);
			}

			return targetVelocity;
		}

		/// <summary>
		/// Returns the speed of a velocity along a given direction, ignoring opposing movement.
		/// </summary>
		/// <param name="velocity">The velocity to project.</param>
		/// <param name="direction">The normalized direction to project onto.</param>
		protected virtual float GetSpeedAlong(Vector3 velocity, Vector3 direction) =>
			Mathf.Max(0f, Vector3.Dot(velocity, direction));

		protected virtual void HandleCamera(Player player)
		{
			if (PlayerCameraManager.current is not PlayerCamera camera)
				return;

			if (!aimToBoostDirection || player.IsSideScroller)
				return;

			var direction = GetBoostDirection();
			direction -= Vector3.Dot(direction, player.transform.up) * player.transform.up;
			camera.RedirectTo(direction, aimYawOffset, aimPitchOffset, aimDuration);
		}

		protected virtual void PlayBoostSound()
		{
			if (!boostSound || !m_audioSource)
				return;

			m_audioSource.PlayOneShot(boostSound);
		}

		protected virtual Vector3 GetBoostDirection()
		{
			var direction =
				boostDirection == BoostDirection.Forward ? transform.forward : transform.up;

			if (boostAngle > 0f)
			{
				var forward = transform.forward;
				var upward = transform.up;
				direction =
					Quaternion.AngleAxis(boostAngle, Vector3.Cross(forward, upward)) * forward;
			}

			return direction;
		}
	}
}
