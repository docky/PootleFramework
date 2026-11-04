using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace PLAYERTWO.PlatformerProject
{
	[AddComponentMenu("PLAYER TWO/Platformer Project/Level/Level Score")]
	public class LevelScore : Singleton<LevelScore>
	{
		/// <summary>
		/// Called when the collectibles list is updated with a new collectible item.
		/// </summary>
		public UnityEvent<Collectible> OnCollectibleAdded;

		/// <summary>
		/// Called when the amount of a specific collectible profile is set or reset.
		/// </summary>
		public UnityEvent<CollectibleProfile> OnCollectibleSet;

		/// <summary>
		/// Called after the level data is fully loaded.
		/// </summary>
		public UnityEvent OnScoreLoaded;

		/// <summary>
		/// Returns the time since the current Level started.
		/// </summary>
		public float time { get; protected set; }

		/// <summary>
		/// Returns true if the time counter should be updating.
		/// </summary>
		public bool stopTime { get; set; } = true;

		protected CollectibleInstanceList m_collectibles;

		/// <summary>
		/// Returns the list of collected collectible items on the current Level.
		/// </summary>
		public CollectibleInstanceList collectibles
		{
			get
			{
				if (m_collectibles == null)
				{
					m_collectibles = new();
					if (m_level)
						foreach (var instance in m_level.GetTrackedCollectibles())
							m_collectibles.Add(instance.Clone());
				}
				return m_collectibles;
			}
		}

		protected Level m_level => Level.instance;

		/// <summary>
		/// Resets the Level Score to its default values.
		/// </summary>
		public virtual void ResetScore()
		{
			time = 0;
		}

		/// <summary>
		/// Records one physical collection of the collectible's own profile (amount += 1).
		/// Only call this when the profile has a 'countsAs' target, so the own-profile count
		/// reflects how many times this specific item was picked up. Does not fire
		/// <see cref="OnCollectibleAdded"/>; the accompanying <see cref="CollectContribution"/>
		/// call for the same pickup handles that.
		/// </summary>
		/// <param name="collectible">The collectible that was physically picked up.</param>
		public virtual void CollectSelf(Collectible collectible)
		{
			if (!collectible)
				return;

			collectibles.AddOrStackSelf(collectible);
		}

		/// <summary>
		/// Adds one unit to the contribution target of this collectible.
		/// If the profile has a 'countsAs' target, that profile's count is incremented.
		/// Otherwise the collectible's own profile is incremented (self-contribution).
		/// </summary>
		/// <param name="collectible">The collectible whose contribution target to increment.</param>
		public virtual void CollectContribution(Collectible collectible)
		{
			if (!collectible)
				return;

			collectibles.AddOrStackContribution(collectible);
			OnCollectibleAdded?.Invoke(collectible);
		}

		/// <summary>
		/// Resets all collected items this session by clearing the collectibles list.
		/// The list will be lazily re-initialized from save data on next access, so items
		/// collected in previous sessions are preserved while session items are cleared.
		/// Fires <see cref="OnCollectibleSet"/> for each affected profile.
		/// </summary>
		public virtual void ResetCollectibles()
		{
			if (m_collectibles == null)
				return;

			var profiles = CollectAffectedProfiles(m_collectibles);
			m_collectibles = null;
			FireCollectibleSetForProfiles(profiles);
		}

		/// <summary>
		/// Restores the collectibles list from a snapshot, such as one saved at a checkpoint.
		/// Fires <see cref="OnCollectibleSet"/> for every profile affected by the change.
		/// </summary>
		/// <param name="snapshot">The collectible snapshot to restore from.</param>
		public virtual void RestoreFromSnapshot(CollectibleInstanceList snapshot)
		{
			var profiles = CollectAffectedProfiles(m_collectibles);
			m_collectibles = snapshot != null ? snapshot.Clone() : new();
			profiles.UnionWith(CollectAffectedProfiles(m_collectibles));
			FireCollectibleSetForProfiles(profiles);
		}

		/// <summary>
		/// Sends the current score to the Game Level to persist the data.
		/// </summary>
		public virtual void Consolidate()
		{
			if (!m_level)
				return;

			m_level.BeatLevel(collectibles, time);
		}

		/// <summary>
		/// Collects the set of profiles present in the given list.
		/// </summary>
		/// <param name="list">The list to collect profiles from.</param>
		/// <returns>A set of all profiles found in the list.</returns>
		protected virtual HashSet<CollectibleProfile> CollectAffectedProfiles(
			CollectibleInstanceList list
		)
		{
			var profiles = new HashSet<CollectibleProfile>();

			if (list == null)
				return profiles;

			foreach (var instance in list)
				profiles.Add(instance.profile);

			return profiles;
		}

		/// <summary>
		/// Fires <see cref="OnCollectibleSet"/> once for each profile in the set.
		/// </summary>
		/// <param name="profiles">The profiles to fire the event for.</param>
		protected virtual void FireCollectibleSetForProfiles(HashSet<CollectibleProfile> profiles)
		{
			foreach (var profile in profiles)
				OnCollectibleSet?.Invoke(profile);
		}

		protected virtual void Start()
		{
			OnScoreLoaded.Invoke();
		}

		protected virtual void Update()
		{
			if (!stopTime)
			{
				time += Time.deltaTime;
			}
		}
	}
}
