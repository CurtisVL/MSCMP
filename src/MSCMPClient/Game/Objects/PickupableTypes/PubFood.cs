using UnityEngine;

namespace MSCMP.Game.Objects.PickupableTypes
{
	/// <summary>
	/// Hooks events for food purchased in the pub.
	/// </summary>
	internal class PubFood
	{
		private readonly GameObject _foodGo;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="go"></param>
		public PubFood(GameObject go)
		{
			_foodGo = go;

			HookEvents();
		}

		/// <summary>
		/// Hook events for pub food.
		/// </summary>
		private void HookEvents()
		{
			PlayMakerFSM foodFsm = Utils.GetPlaymakerScriptByName(_foodGo, "Use");
			EventHook.AddWithSync(foodFsm, "State 2");
		}
	}
}
