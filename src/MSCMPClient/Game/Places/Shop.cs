using UnityEngine;

namespace MSCMP.Game.Places
{
	/// <summary>
	/// Syncs items on shelves at the store as well as the cash registers, and switches behind counter.
	/// </summary>
	internal class Shop
	{
		private GameObject _shopGo;
		private GameObject _shopProducts;
		private GameObject _pubProducts;
		private GameObject _shopRegister;
		private GameObject _pubRegister;

		private PlayMakerFSM _switchPumps;
		private PlayMakerFSM _switchDoor;

		/// <summary>
		/// Setup shop event sync.
		/// </summary>
		public void Setup(GameObject shop)
		{
			_shopGo = shop;
			foreach (Transform transform in _shopGo.GetComponentsInChildren<Transform>())
			{
				switch (transform.name)
				{
					// Products on shelves.
					case "ActivateStore":
						_shopProducts = transform.gameObject;
						break;

					// Products in pub.
					case "ActivateBar":
						_pubProducts = transform.gameObject;
						break;

					// Cash register in store.
					case "Register":
						if (transform.parent.name == "StoreCashRegister")
						{
							_shopRegister = transform.gameObject;
						}
						if (transform.parent.name == "PubCashRegister")
						{
							_pubRegister = transform.gameObject;
						}
						break;
					// Switches behind counter.
					case "switch_pumps":
						_switchPumps = Utils.GetPlaymakerScriptByName(transform.gameObject, "Use");
						break;
					case "switch_door":
						_switchDoor = Utils.GetPlaymakerScriptByName(transform.gameObject, "Use");
						break;
				}
			}

			HookEvents();
		}

		/// <summary>
		/// Hook events.
		/// </summary>
		private void HookEvents()
		{
			// Products on shelves.
			foreach (PlayMakerFSM fsm in _shopProducts.GetComponentsInChildren<PlayMakerFSM>())
			{
				if (fsm.FsmName == "Buy")
				{
					EventHook.AddWithSync(fsm, "Remove");
					EventHook.AddWithSync(fsm, "Reset");
				}
				// Yes, the fan belt has a different state. :thonking:
				if (fsm.gameObject.name == "BuyFanbelt")
				{
					EventHook.AddWithSync(fsm, "Play anim 2");
				}
				else
				{
					EventHook.AddWithSync(fsm, "Play anim");
				}
			}

			// Products in pub.
			foreach (PlayMakerFSM fsm in _pubProducts.GetComponentsInChildren<PlayMakerFSM>())
			{
				if (fsm.FsmName == "Buy")
				{
					EventHook.AddWithSync(fsm, "Check money", () =>
					{
						if (fsm.Fsm.PreviousActiveState.Name.StartsWith("MP_") && Network.NetManager.Instance.IsHost)
						{
							Logger.Log("Ignoring 'Check money' event!");
							return true;
						}

						Logger.Log("Previous state is: " + fsm.Fsm.PreviousActiveState.Name);
						return false;
					});
				}
			}

			// Cash register in store.
			PlayMakerFSM storeRegisterFsm = Utils.GetPlaymakerScriptByName(_shopRegister, "Data");
			EventHook.AddWithSync(storeRegisterFsm, "Check money");

			// Switches behind counter.
			EventHook.AddWithSync(_switchPumps, "ON");
			EventHook.AddWithSync(_switchPumps, "OFF");

			EventHook.AddWithSync(_switchDoor, "ON");
			EventHook.AddWithSync(_switchDoor, "OFF");
		}
	}
}
