using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Manages which darts are enabled on the map in the house.
	/// </summary>
	class MapManager
	{
		GameObject gameObject;
		public static MapManager Instance;

		GameObject dartsGO;
		List<GameObject> darts = new List<GameObject>();

		/// <summary>
		/// Setup the map manager once the map GameObject is found.
		/// </summary>
		/// <param name="go">Map GameObject.</param>
		public void Setup(GameObject go) {
			Instance = this;
			gameObject = go;

			dartsGO = gameObject.transform.FindChild("Darts").gameObject;
			foreach (Transform dart in dartsGO.transform) {
				darts.Add(dart.gameObject);
			}
		}

		/// <summary>
		/// Enable a dart on the map.
		/// </summary>
		/// <param name="dart">Dart ID.</param>
		public void EnableDart(int dart) {
			darts[dart].SetActive(true);
		}

		/// <summary>
		/// Disable a dart on the map.
		/// </summary>
		/// <param name="dart">Dart ID.</param>
		public void DisableDart(int dart) {
			darts[dart].SetActive(false);
		}

		/// <summary>
		/// Sync which darts are currently enabled with other clients.
		/// </summary>
		public void SyncDarts() {
			Network.Messages.DartSyncMessage msg = new Network.Messages.DartSyncMessage();
			msg.darts = ReturnActiveDarts();
			Network.NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Handle dart sync message.
		/// </summary>
		public void SyncDartsHandler(int[] dartsEnabled) {
			foreach (int dart in dartsEnabled) {
				EnableDart(dart);
			}
		}

		/// <summary>
		/// Return which darts are currently active on the map.
		/// </summary>
		/// <returns></returns>
		public int[] ReturnActiveDarts() {
			List<int> enabledDarts = new List<int>();
			int i = 0;
			foreach (GameObject dart in darts) {
				if (dart.activeSelf) {
					enabledDarts.Add(i);
				}
				i++;
			}

			if (enabledDarts.Count > 0) {
				return enabledDarts.ToArray();
			}
			else {
				return null;
			}
		}
	}
}
