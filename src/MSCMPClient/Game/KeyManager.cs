using System;
using UnityEngine;
using HutongGames.PlayMaker;

namespace MSCMP.Game
{
	/// <summary>
	/// Manages which keys the host player owns and syncs this with connected players.
	/// </summary>
	class KeyManager : IGameObjectCollector
	{
		/// <summary>
		/// Find key GameObjects.
		/// </summary>
		/// <param name="obj"></param>
		public void CollectGameObject(GameObject obj) {
			// Truck and van keys.
			if (obj.name == "GifuKeys" || obj.name == "VanKeys") {
				HookEvent(obj);
			}
		}

		/// <summary>
		/// Hook key take event.
		/// </summary>
		/// <param name="key">Key GameObject.</param>
		private void HookEvent(GameObject key) {
			key.SetActive(true);
			PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(key, "Use");
			EventHook.AddWithSync(fsm, "State 1");
			Logger.Log("Hooked key: " + key.name);
			if (fsm == null) {
				Logger.Log("FSM is null for: " + key.name);
			}
			key.SetActive(false);
		}

		/// <summary>
		/// On object destroyed. (Not used for keys)
		/// </summary>
		public void DestroyObject(GameObject obj) {

		}

		/// <summary>
		/// On all objects destroyed. (Not used for keys)
		/// </summary>
		public void DestroyObjects() {

		}
	}
}
