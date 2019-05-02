using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Manages which darts are enabled on the map in the house.
	/// </summary>
	internal class MapManager
	{
		private GameObject _gameObject;
		public static MapManager Instance;

		private GameObject _dartsGo;
		private readonly List<GameObject> _darts = new List<GameObject>();

		/// <summary>
		/// Setup the map manager once the map GameObject is found.
		/// </summary>
		/// <param name="go">Map GameObject.</param>
		public void Setup(GameObject go)
		{
			Instance = this;
			_gameObject = go;

			_dartsGo = _gameObject.transform.FindChild("Darts").gameObject;
			foreach (Transform dart in _dartsGo.transform)
			{
				_darts.Add(dart.gameObject);
			}
		}

		/// <summary>
		/// Enable a dart on the map.
		/// </summary>
		/// <param name="dart">Dart ID.</param>
		public void EnableDart(int dart)
		{
			_darts[dart].SetActive(true);
		}

		/// <summary>
		/// Disable a dart on the map.
		/// </summary>
		/// <param name="dart">Dart ID.</param>
		public void DisableDart(int dart)
		{
			_darts[dart].SetActive(false);
		}

		/// <summary>
		/// Sync which darts are currently enabled with other clients.
		/// </summary>
		public void SyncDarts()
		{
			Network.Messages.DartSyncMessage msg = new Network.Messages.DartSyncMessage
			{
				darts = ReturnActiveDarts()
			};
			Network.NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Handle dart sync message.
		/// </summary>
		public void SyncDartsHandler(int[] dartsEnabled)
		{
			foreach (int dart in dartsEnabled)
			{
				EnableDart(dart);
			}
		}

		/// <summary>
		/// Return which darts are currently active on the map.
		/// </summary>
		/// <returns></returns>
		public int[] ReturnActiveDarts()
		{
			List<int> enabledDarts = new List<int>();
			int i = 0;
			foreach (GameObject dart in _darts)
			{
				if (dart.activeSelf)
				{
					enabledDarts.Add(i);
				}
				i++;
			}

			if (enabledDarts.Count > 0)
			{
				return enabledDarts.ToArray();
			}

			return null;
		}
	}
}
