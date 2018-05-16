using MSCMP.Game.Components;
using System.Collections.Concurrent;

namespace MSCMP.Game {
	/// <summary>
	/// Class managing sync of objects.
	/// </summary>
	class ObjectSyncManager {
		/// <summary>
		/// Instance.
		/// </summary>
		public static ObjectSyncManager Instance = null;

		/// <summary>
		/// Dictionary of ObjectIDs.
		/// </summary>
		public ConcurrentDictionary<int, ObjectSyncComponent> ObjectIDs = new ConcurrentDictionary<int, ObjectSyncComponent>();

		/// <summary>
		/// Local player's Steam ID.
		/// </summary>
		public Steamworks.CSteamID steamID;

		/// <summary>
		/// Constructor.
		/// </summary>
		public ObjectSyncManager (Steamworks.CSteamID SteamID) {
			Instance = this;
			steamID = SteamID;
		}

		/// <summary>
		/// Adds new object to the ObjectIDs Dictionary.
		/// </summary>
		/// <param name="osc">Object to add.</param>
		/// <returns>ObjectID of object.</returns>
		public int AddNewObject(ObjectSyncComponent osc) {
			Logger.Log($"Added new ObjectID at: {ObjectIDs.Count + 1}");
			ObjectIDs.GetOrAdd(ObjectIDs.Count + 1, osc);
			return ObjectIDs.Count;
		}

		/// <summary>
		/// Force add a new object to the ObectIDs Dictionary.
		/// </summary>
		/// <param name="osc"></param>
		/// <param name="objectID"></param>
		public void ForceAddNewObject(ObjectSyncComponent osc, int objectID) {
			Logger.Log($"Force adding new ObjectID at: {objectID}");
			if (ObjectIDs.ContainsKey(objectID)) {
				ObjectIDs[objectID] = osc;
			}
			else {
				ObjectIDs.GetOrAdd(objectID, osc);
			}
		}

		/// <summary>
		/// Print ObjectID owner's debug.
		/// </summary>
		/// <returns></returns>
		public void PrintDebug() {
			Logger.Log("---------------------------------------");
			Logger.Log($"Printing debug info. Total ObjectIDs: {ObjectIDs.Count}");
			Logger.Log("Local owned objects:");
			int i = 0;
			while (i < ObjectIDs.Count) {
				if (ObjectIDs.ContainsKey(i)) {
					if (ObjectIDs[i].Owner == steamID.m_SteamID) {
						Logger.Log($"> {ObjectIDs[i].transform.name}");
					}
				}
				i++;
			}

			Logger.Log("Remote owned objects:");
			int i2 = 0;
			while (i2 < ObjectIDs.Count) {
				if (ObjectIDs.ContainsKey(i2)) {
					if (ObjectIDs[i2].Owner != 0 && ObjectIDs[i2].Owner != steamID.m_SteamID) {
						Logger.Log($"> {ObjectIDs[i2].transform.name}");
					}
				}
				i2++;
			}
			Logger.Log("---------------------------------------");
		}
	}
}
