using System.Collections.Generic;
using System.Linq;

namespace PLAYERTWO.PlatformerProject
{
	[System.Serializable]
	public class CollectibleInstanceList : List<CollectibleInstance>
	{
		public CollectibleInstanceList()
			: base() { }

		public CollectibleInstanceList(CollectibleInstance[] instances)
			: base(instances) { }

		/// <summary>
		/// Records one physical collection of this collectible's own profile (amount += 1).
		/// Only call this when the profile has a 'countsAs' target, so the own-profile
		/// count reflects how many times this specific item was picked up.
		/// </summary>
		/// <param name="collectible">The collectible that was collected.</param>
		public virtual void AddOrStackSelf(Collectible collectible)
		{
			if (collectible == null)
				return;

			StackProfile(collectible.profile, collectible.identifier, 1);
		}

		/// <summary>
		/// Adds one unit to the contribution target of this collectible.
		/// If the profile has a 'countsAs' target, increments that profile's instance.
		/// Otherwise increments the collectible's own profile instance (self-contribution).
		/// </summary>
		/// <param name="collectible">The collectible whose contribution target to increment.</param>
		public virtual void AddOrStackContribution(Collectible collectible)
		{
			if (collectible == null)
				return;

			var target =
				collectible.profile.countsAs != null
					? collectible.profile.countsAs
					: collectible.profile;
			StackProfile(target, collectible.identifier, 1);
		}

		/// <summary>
		/// Adds a collectible instance to the list. If an instance with the same profile already exists,
		/// it increments the amount and merges identifiers.
		/// </summary>
		/// <param name="newItem">The collectible instance to add or stack.</param>
		public virtual void AddOrStack(CollectibleInstance newItem)
		{
			if (newItem == null)
				return;

			if (Find((i) => i.profile == newItem.profile) is CollectibleInstance currentItem)
			{
				if (currentItem.profile.collectOnce)
				{
					if (newItem.identifiers != null && newItem.identifiers.Count > 0)
					{
						var newIdentifiers = newItem
							.identifiers.Except(currentItem.identifiers)
							.ToList();
						currentItem.identifiers.AddRange(newIdentifiers);
						currentItem.amount = currentItem.identifiers.Count;
					}
				}
				else
				{
					currentItem.amount += newItem.amount;
				}

				return;
			}

			Add(newItem.Clone());
		}

		/// <summary>
		/// Adds or stacks multiple collectible instances to the list.
		/// </summary>
		/// <param name="instances">The collectible instances to add or stack.</param>
		public virtual void AddOrStackMany(CollectibleInstanceList instances)
		{
			if (instances == null || instances.Count == 0)
				return;

			foreach (var instance in instances)
				AddOrStack(instance);
		}

		/// <summary>
		/// Returns true if the collectible has been collected (identified by its profile
		/// and unique identifier).
		/// </summary>
		/// <param name="collectible">The collectible to check for.</param>
		/// <returns>True if the collectible has been collected.</returns>
		public virtual bool HasCollected(Collectible collectible)
		{
			if (collectible == null)
				return false;

			var instance = Find(i => i.profile == collectible.profile);
			return instance != null
				&& instance.identifiers != null
				&& instance.identifiers.Contains(collectible.identifier);
		}

		/// <summary>
		/// Returns the sum of amounts for all instances matching the given profile.
		/// </summary>
		/// <param name="profile">The collectible profile to sum amounts for.</param>
		/// <returns>The sum of amounts for the given profile.</returns>
		public virtual int SumAmount(CollectibleProfile profile) =>
			this.Where(instance => instance.profile == profile).Sum(instance => instance.amount);

		/// <summary>
		/// Creates a deep copy of the current CollectibleInstanceList.
		/// </summary>
		public virtual CollectibleInstanceList Clone()
		{
			var clone = new CollectibleInstanceList();
			foreach (var instance in this)
				clone.Add(instance.Clone());
			return clone;
		}

		/// <summary>
		/// Finds or creates an instance for the given profile and increments its amount.
		/// For "collect once" profiles, tracks identifiers instead of a numeric delta.
		/// </summary>
		/// <param name="profile">The profile whose instance to increment.</param>
		/// <param name="identifier">The unique identifier of the source collectible.</param>
		/// <param name="delta">The amount to add for normal (non-collect-once) profiles.</param>
		protected virtual void StackProfile(
			CollectibleProfile profile,
			string identifier,
			int delta
		)
		{
			if (profile == null)
				return;

			if (Find(i => i.profile == profile) is CollectibleInstance instance)
			{
				if (instance.profile.collectOnce)
				{
					if (
						!string.IsNullOrEmpty(identifier)
						&& !instance.identifiers.Contains(identifier)
					)
					{
						instance.identifiers.Add(identifier);
						instance.amount = instance.identifiers.Count;
					}
				}
				else
				{
					instance.amount += delta;
				}

				return;
			}

			Add(
				new CollectibleInstance()
				{
					profile = profile,
					amount = profile.collectOnce ? 1 : delta,
					identifiers = profile.collectOnce ? new() { identifier } : null,
				}
			);
		}
	}
}
