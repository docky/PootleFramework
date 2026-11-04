using UnityEngine;

namespace PLAYERTWO.PlatformerProject
{
	[CreateAssetMenu(
		fileName = "New Collectible Profile",
		menuName = "PLAYER TWO/Platformer Project/Collectible/New Collectible Profile"
	)]
	public class CollectibleProfile : ScriptableObject
	{
		[Header("Collectible Settings")]
		[Tooltip(
			"If true, the collectible can only be collected once per level. "
				+ "After beating a level, it won't show up in following playthroughs. "
				+ "Useful for collectibles like stars, keys, or unique items."
		)]
		public bool collectOnce;

		[Tooltip(
			"If true, collectibles using this profile will not be restored when the player respawns. "
				+ "Useful for collectibles that should only be obtainable once per session, such as lives or power-ups."
		)]
		public bool doNotRespawn;

		[Min(1)]
		[Tooltip(
			"How many times to loop the collection audio/effect, and how many units to add "
				+ "to the 'Counts As' profile per collection. If 'Counts As' is not set, "
				+ "this many units are added to this profile's own count instead."
		)]
		public int collectionAmount = 1;

		[Tooltip(
			"Optional profile this collectible contributes to when collected. "
				+ "For example, a Red Coin can set this to the Coin profile so it counts "
				+ "as coins. The amount contributed is defined by 'Collection Amount'."
		)]
		public CollectibleProfile countsAs;

		protected virtual void OnValidate()
		{
			if (countsAs == this)
			{
				Debug.LogWarning(
					$"CollectibleProfile '{name}' cannot count as itself. Clearing the reference.",
					this
				);
				countsAs = null;
			}
		}
	}
}
