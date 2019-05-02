using UnityEngine;

/// <summary>
/// Syncs consuming of consumable items. (Such as food and drink)
/// </summary>
namespace MSCMP.Game.Objects.PickupableTypes {
	internal class Consumable {
		private readonly GameObject _itemGo;

		/// <summary>
		/// Constructor.
		/// </summary>
		public Consumable(GameObject go) {
			_itemGo = go;
			HookEvents();
		}

		/// <summary>
		/// Hook events for food or drink items.
		/// </summary>
		public void HookEvents() {
			foreach (PlayMakerFSM fsm in _itemGo.GetComponents<PlayMakerFSM>()) {
				if (fsm.Fsm.Name == "Use") {
					EventHook.AddWithSync(fsm, "Destroy");
				}
			}
		}
	}
}
