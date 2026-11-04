using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	[AddComponentMenu("PLAYER TWO/Platformer Project/Enemy/Enemy Animator")]
	public class EnemyAnimator : EntityAnimator<Enemy>
	{
		protected override DamageType EntityLastDamageType => m_entity.lastDamageType;

		protected virtual void InitializeAnimatorTriggers() =>
			m_entity.states.events.onChange.AddListener(
				() => animator.SetTrigger(m_onStateChangedHash)
			);

		protected virtual void InitializeDamageCallbacks() =>
			m_entity.enemyEvents.OnDamage.AddListener(HandleDamageTypeChange);

		protected virtual void Start()
		{
			InitializeEntity();
			InitializeParametersHash();
			InitializeAnimatorTriggers();
			InitializeDamageCallbacks();
		}

		protected virtual void LateUpdate()
		{
			var lateralSpeed = m_entity.lateralVelocity.magnitude;
			var verticalSpeed = m_entity.verticalVelocity.y;

			animator.SetInteger(m_stateHash, m_entity.states.index);
			animator.SetInteger(m_lastStateHash, m_entity.states.lastIndex);
			animator.SetFloat(m_lateralSpeedHash, lateralSpeed);
			animator.SetFloat(m_verticalSpeedHash, verticalSpeed);
			animator.SetInteger(m_healthHash, m_entity.health.current);
			animator.SetBool(m_isGroundedHash, m_entity.isGrounded);
		}
	}
}
