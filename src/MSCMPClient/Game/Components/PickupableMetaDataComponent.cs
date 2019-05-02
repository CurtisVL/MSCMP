using UnityEngine;

namespace MSCMP.Game.Components {
	/// <summary>
	/// Pickupable meta data component used to associate prefab/instances with prefab id.
	/// </summary>
	internal class PickupableMetaDataComponent : MonoBehaviour {
		public int PrefabId = -1;

		/// <summary>
		/// Getter for the prefab descriptor.
		/// </summary>
		public GamePickupableDatabase.PrefabDesc PrefabDescriptor
		{
			get {
				Client.Assert(PrefabId != -1, "Prefab id is not set!");
				return GamePickupableDatabase.Instance.GetPickupablePrefab(PrefabId);
			}
		}
	}
}
