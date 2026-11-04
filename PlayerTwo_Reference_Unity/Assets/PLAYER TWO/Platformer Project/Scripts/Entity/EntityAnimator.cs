using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	/// <summary>
	/// Base class for components that synchronize an Entity's state with an Animator Controller.
	/// </summary>
	/// <typeparam name="T">The type of the Entity this animator represents.</typeparam>
	public abstract class EntityAnimator<T> : MonoBehaviour
		where T : Entity<T>
	{
		[Tooltip("Reference to the Animator component of the Entity's model.")]
		public Animator animator;

		[Header("Parameters Names")]
		[Tooltip("The name of the state parameter in the Animator Controller.")]
		public string stateName = "State";

		[Tooltip("The name of the last state parameter in the Animator Controller.")]
		public string lastStateName = "Last State";

		[Tooltip("The name of the lateral speed parameter in the Animator Controller.")]
		public string lateralSpeedName = "Lateral Speed";

		[Tooltip("The name of the vertical speed parameter in the Animator Controller.")]
		public string verticalSpeedName = "Vertical Speed";

		[Tooltip("The name of the health parameter in the Animator Controller.")]
		public string healthName = "Health";

		[Tooltip("The name of the is grounded parameter in the Animator Controller.")]
		public string isGroundedName = "Is Grounded";

		[Tooltip("The name of the on state changed trigger in the Animator Controller.")]
		public string onStateChangedName = "On State Changed";

		[Tooltip("The name of the damage type parameter in the Animator Controller.")]
		public string damageTypeName = "Damage Type";

		protected int m_stateHash;
		protected int m_lastStateHash;
		protected int m_lateralSpeedHash;
		protected int m_verticalSpeedHash;
		protected int m_healthHash;
		protected int m_isGroundedHash;
		protected int m_onStateChangedHash;
		protected int m_damageTypeHash;

		protected T m_entity;

		protected Dictionary<int, bool> m_parameterExists = new();

		/// <summary>
		/// Returns the last damage type applied to the animated Entity.
		/// </summary>
		protected abstract DamageType EntityLastDamageType { get; }

		/// <summary>
		/// Caches the reference to the Entity this animator represents.
		/// </summary>
		protected virtual void InitializeEntity() => m_entity = GetComponent<T>();

		/// <summary>
		/// Computes the hashes of the shared Animator Controller parameters and caches
		/// which parameters actually exist in the assigned Animator Controller.
		/// </summary>
		protected virtual void InitializeParametersHash()
		{
			m_stateHash = Animator.StringToHash(stateName);
			m_lastStateHash = Animator.StringToHash(lastStateName);
			m_lateralSpeedHash = Animator.StringToHash(lateralSpeedName);
			m_verticalSpeedHash = Animator.StringToHash(verticalSpeedName);
			m_healthHash = Animator.StringToHash(healthName);
			m_isGroundedHash = Animator.StringToHash(isGroundedName);
			m_onStateChangedHash = Animator.StringToHash(onStateChangedName);
			m_damageTypeHash = Animator.StringToHash(damageTypeName);

			m_parameterExists.Clear();

			foreach (var parameter in animator.parameters)
				m_parameterExists[parameter.nameHash] = true;
		}

		/// <summary>
		/// Updates the damage type parameter in the Animator Controller, if it exists.
		/// </summary>
		protected virtual void HandleDamageTypeChange()
		{
			if (ParameterExists(m_damageTypeHash))
				animator.SetInteger(m_damageTypeHash, (int)EntityLastDamageType);
		}

		/// <summary>
		/// Checks if a parameter with the given hash exists in the Animator Controller.
		/// </summary>
		/// <param name="hash">The hash of the parameter to check.</param>
		/// <returns>True if the parameter exists, false otherwise.</returns>
		public virtual bool ParameterExists(int hash) => m_parameterExists.ContainsKey(hash);
	}
}
