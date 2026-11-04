using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	[AddComponentMenu("PLAYER TWO/Platformer Project/Player/Player Animator")]
	public class PlayerAnimator : EntityAnimator<Player>
	{
		[System.Serializable]
		public class ForcedTransition
		{
			[Tooltip(
				"The index of the Player State from the Player State Manager that you want to force a transition from."
			)]
			public int fromStateId;

			[Tooltip(
				"The index of the layer from your Animator Controller that contains the target animation. (It's 0 if the animation is inside the 'Base Layer')"
			)]
			public int animationLayer;

			[Tooltip(
				"The name of the Animation State you want to play right after finishing the Player State from above."
			)]
			public string toAnimationState;
		}

		[Header("Player Parameters Names")]
		[Tooltip("The name of the ledge hanging speed parameter in the Animator Controller.")]
		public string ledgeHangingSpeed = "Ledge Hanging Speed";

		[Tooltip("The name of the lateral animation speed parameter in the Animator Controller.")]
		public string lateralAnimationSpeedName = "Lateral Animation Speed";

		[Tooltip("The name of the roll animation speed parameter in the Animator Controller.")]
		public string rollAnimationSpeedName = "Roll Animation Speed";

		[Tooltip("The name of the jump counter parameter in the Animator Controller.")]
		public string jumpCounterName = "Jump Counter";

		[Tooltip("The name of the jumped from ground boolean in the Animator Controller.")]
		public string jumpedFromGroundName = "Jumped From Ground";

		[Tooltip("The name of the is holding parameter in the Animator Controller.")]
		public string isHoldingName = "Is Holding";

		[Tooltip("The name of the parameters that mirrors the wall run animation.")]
		public string mirrorWallRunName = "Mirror Wall Run";

		[Header("Settings")]
		[Tooltip("The minimum playback speed for lateral movement animations.")]
		public float minLateralAnimationSpeed = 0.5f;

		[Tooltip("The minimum playback speed for the roll animation.")]
		public float minRollAnimationSpeed = 10f;

		[Tooltip("The list of forced transitions.")]
		public List<ForcedTransition> forcedTransitions;

		protected int m_ledgeHangingSpeedHash;
		protected int m_lateralAnimationSpeedHash;
		protected int m_rollAnimationSpeedHash;
		protected int m_jumpCounterHash;
		protected int m_jumpedFromGroundHash;
		protected int m_isHoldingHash;
		protected int m_mirrorWallRunHash;

		protected bool m_pendingStateUpdate;

		protected Dictionary<int, ForcedTransition> m_forcedTransitions;

		protected override DamageType EntityLastDamageType => m_entity.lastDamageType;

		protected virtual void InitializeAnimator()
		{
			if (animator)
				return;

			LogAnimatorWarning();
			var root = m_entity.skin ? m_entity.skin : transform;
			animator = root.GetComponentInChildren<Animator>();
		}

		protected virtual void InitializeForcedTransitions()
		{
			m_forcedTransitions = new Dictionary<int, ForcedTransition>();

			foreach (var transition in forcedTransitions)
			{
				if (!m_forcedTransitions.ContainsKey(transition.fromStateId))
				{
					m_forcedTransitions.Add(transition.fromStateId, transition);
				}
			}
		}

		protected virtual void InitializeCallbacks()
		{
			m_entity.health.onChange.AddListener(HandleHealthChange);
			m_entity.playerEvents.OnHurt.AddListener(HandleDamageTypeChange);
			m_entity.states.events.onChange.AddListener(() =>
			{
				HandleStateChange();
				HandleForcedTransitions();
			});
		}

		protected override void InitializeParametersHash()
		{
			base.InitializeParametersHash();

			m_ledgeHangingSpeedHash = Animator.StringToHash(ledgeHangingSpeed);
			m_lateralAnimationSpeedHash = Animator.StringToHash(lateralAnimationSpeedName);
			m_rollAnimationSpeedHash = Animator.StringToHash(rollAnimationSpeedName);
			m_jumpCounterHash = Animator.StringToHash(jumpCounterName);
			m_jumpedFromGroundHash = Animator.StringToHash(jumpedFromGroundName);
			m_isHoldingHash = Animator.StringToHash(isHoldingName);
			m_mirrorWallRunHash = Animator.StringToHash(mirrorWallRunName);
		}

		protected virtual void HandleStateChange()
		{
			m_pendingStateUpdate = true;
		}

		protected virtual void HandleHealthChange()
		{
			animator.SetInteger(m_healthHash, m_entity.health.current);
		}

		protected virtual void HandleForcedTransitions()
		{
			var lastStateIndex = m_entity.states.lastIndex;

			if (m_forcedTransitions.ContainsKey(lastStateIndex))
			{
				var layer = m_forcedTransitions[lastStateIndex].animationLayer;
				animator.Play(m_forcedTransitions[lastStateIndex].toAnimationState, layer);
			}
		}

		protected virtual void HandleStateUpdate()
		{
			if (!m_pendingStateUpdate)
				return;

			animator.SetInteger(m_stateHash, m_entity.states.index);
			animator.SetInteger(m_lastStateHash, m_entity.states.lastIndex);
			animator.SetTrigger(m_onStateChangedHash);

			m_pendingStateUpdate = false;
		}

		protected virtual void HandleAnimatorParameters()
		{
			var lateralSpeed = m_entity.lateralVelocity.magnitude;
			var verticalSpeed = m_entity.verticalVelocity.y;
			var lateralAnimationSpeed = Mathf.Max(
				minLateralAnimationSpeed,
				lateralSpeed / m_entity.stats.current.topSpeed
			);
			var ledgeHangingSpeed = Vector3.Dot(m_entity.lateralVelocity, m_entity.localRight);
			var rollAnimationSpeed = m_entity.isGrounded
				? lateralSpeed
				: Mathf.Max(minRollAnimationSpeed, Mathf.Abs(m_entity.velocity.magnitude));
			var mirrorWallRun = Vector3.Dot(m_entity.transform.right, m_entity.lastWallNormal) > 0;

			animator.SetFloat(m_lateralSpeedHash, lateralSpeed);
			animator.SetFloat(m_verticalSpeedHash, verticalSpeed);
			animator.SetFloat(m_ledgeHangingSpeedHash, ledgeHangingSpeed);
			animator.SetFloat(m_lateralAnimationSpeedHash, lateralAnimationSpeed);

			if (ParameterExists(m_rollAnimationSpeedHash))
				animator.SetFloat(m_rollAnimationSpeedHash, rollAnimationSpeed);

			animator.SetInteger(m_jumpCounterHash, m_entity.jumpCounter);

			if (ParameterExists(m_jumpedFromGroundHash))
				animator.SetBool(m_jumpedFromGroundHash, m_entity.jumpedFromGround);

			animator.SetBool(m_isGroundedHash, m_entity.isGrounded);
			animator.SetBool(m_isHoldingHash, m_entity.holding);

			if (ParameterExists(m_mirrorWallRunHash))
				animator.SetBool(m_mirrorWallRunHash, mirrorWallRun);
		}

		protected virtual void LogAnimatorWarning()
		{
			Debug.LogWarning(
				"PlayerAnimator: The animator reference is missing. "
					+ "The component will try to find it in the children of the player's skin. "
					+ "Manually assign it in the Inspector to avoid referencing the wrong animator.",
				this
			);
		}

		protected virtual void Start()
		{
			InitializeEntity();
			InitializeAnimator();
			InitializeForcedTransitions();
			InitializeParametersHash();
			InitializeCallbacks();
		}

		protected virtual void LateUpdate()
		{
			HandleStateUpdate();
			HandleAnimatorParameters();
		}
	}
}
