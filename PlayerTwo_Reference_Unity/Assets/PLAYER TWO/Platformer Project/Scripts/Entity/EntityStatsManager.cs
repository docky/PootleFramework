using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	public abstract class EntityStatsManager<T> : MonoBehaviour
		where T : EntityStats<T>
	{
		public T[] stats;

		/// <summary>
		/// The instance of the current activated Stats.
		/// </summary>
		public T current { get; protected set; }

		protected T[] m_instances;

		/// <summary>
		/// Changes from the current stats to the desired one.
		/// </summary>
		/// <param name="to">The desired index of the Stats you want.</param>
		public virtual void Change(int to)
		{
			if (to >= 0 && to < stats.Length)
			{
				var instance = GetOrCreateInstance(to);

				if (current != instance)
					current = instance;
			}
		}

		/// <summary>
		/// Casts the current stats to the desired type.
		/// </summary>
		/// <typeparam name="S">The desired type you want to cast to.</typeparam>
		/// <returns>>The casted stats.</returns>
		public virtual S CurrentAs<S>()
			where S : T => current as S;

		/// <summary>
		/// Returns a cached runtime instance of the stats at the given index,
		/// creating one from the original ScriptableObject if it does not exist yet.
		/// </summary>
		/// <param name="index">Index into the <see cref="stats"/> array.</param>
		protected virtual T GetOrCreateInstance(int index)
		{
			m_instances ??= new T[stats.Length];

			if (m_instances[index] == null && stats[index] != null)
			{
				m_instances[index] = Instantiate(stats[index]);
				EntityStats.RegisterInstance(stats[index], m_instances[index]);
			}

			return m_instances[index];
		}

		protected virtual void Start()
		{
			if (stats.Length > 0)
				current = GetOrCreateInstance(0);
		}

		protected virtual void OnDestroy()
		{
			if (m_instances == null)
				return;

			for (int i = 0; i < m_instances.Length; i++)
			{
				if (m_instances[i] == null)
					continue;

				EntityStats.UnregisterInstance(stats[i], m_instances[i]);
				Destroy(m_instances[i]);
			}
		}
	}
}
