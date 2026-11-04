using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	/// <summary>
	/// Carries the data describing a single instance of damage applied to a damageable object.
	/// </summary>
	public readonly struct DamageInfo
	{
		/// <summary>
		/// The amount of health to remove.
		/// </summary>
		public readonly int amount;

		/// <summary>
		/// The world-space position the damage came from.
		/// </summary>
		public readonly Vector3 origin;

		/// <summary>
		/// The category of damage being applied.
		/// </summary>
		public readonly DamageType type;

		public DamageInfo(int amount, Vector3 origin, DamageType type)
		{
			this.amount = amount;
			this.origin = origin;
			this.type = type;
		}
	}
}
