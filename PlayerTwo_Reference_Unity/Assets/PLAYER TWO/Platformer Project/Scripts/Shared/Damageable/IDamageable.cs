namespace PLAYERTWO.PlatformerProject
{
	/// <summary>
	/// Represents an object that can receive damage.
	/// </summary>
	public interface IDamageable
	{
		void ApplyDamage(DamageInfo damage);
	}
}
