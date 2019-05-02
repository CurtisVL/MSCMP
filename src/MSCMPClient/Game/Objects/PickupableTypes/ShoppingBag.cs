using UnityEngine;

namespace MSCMP.Game.Objects.PickupableTypes
{
	internal class ShoppingBag
	{
		private readonly GameObject _shoppingBagGo;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="go"></param>
		public ShoppingBag(GameObject go)
		{
			_shoppingBagGo = go;
			HookEvents();
		}

		/// <summary>
		/// Hook events for shopping bag.
		/// </summary>
		private void HookEvents()
		{
			// Shopping bag.
			PlayMakerFSM bagFsm = Utils.GetPlaymakerScriptByName(_shoppingBagGo, "Open");
			EventHook.AddWithSync(bagFsm, "Play anim");
			// This class also handles the fireworks bag.
		}
	}
}
