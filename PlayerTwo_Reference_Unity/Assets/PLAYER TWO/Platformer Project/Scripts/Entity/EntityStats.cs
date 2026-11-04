using System.Collections.Generic;
using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	public abstract class EntityStats : ScriptableObject
	{
		protected static readonly Dictionary<EntityStats, List<EntityStats>> s_instances = new();

		/// <summary>
		/// Registers a runtime instance as a clone of the given original Stats asset,
		/// so that Inspector edits made to the asset during Play Mode can be
		/// propagated into it.
		/// </summary>
		/// <param name="original">The Stats asset the instance was cloned from.</param>
		/// <param name="instance">The runtime instance cloned from the asset.</param>
		public static void RegisterInstance(EntityStats original, EntityStats instance)
		{
			if (original == null || instance == null)
				return;

			if (!s_instances.TryGetValue(original, out var instances))
				s_instances[original] = instances = new List<EntityStats>();

			instances.Add(instance);
		}

		/// <summary>
		/// Unregisters a runtime instance previously registered with <see cref="RegisterInstance"/>.
		/// </summary>
		/// <param name="original">The Stats asset the instance was cloned from.</param>
		/// <param name="instance">The runtime instance to unregister.</param>
		public static void UnregisterInstance(EntityStats original, EntityStats instance)
		{
			if (original == null || !s_instances.TryGetValue(original, out var instances))
				return;

			instances.Remove(instance);

			if (instances.Count == 0)
				s_instances.Remove(original);
		}

#if UNITY_EDITOR
		/// <summary>
		/// Propagates Inspector edits made to this asset during Play Mode into every
		/// runtime instance cloned from it, keeping the instancing fix (runtime code
		/// never writes into the asset) while restoring live tweaking from the Inspector.
		/// </summary>
		protected virtual void OnValidate()
		{
			if (!Application.isPlaying)
				return;

			if (!s_instances.TryGetValue(this, out var instances))
				return;

			foreach (var instance in instances)
			{
				if (instance != null)
					UnityEditor.EditorUtility.CopySerialized(this, instance);
			}
		}
#endif
	}

	public abstract class EntityStats<T> : EntityStats
		where T : ScriptableObject { }
}
