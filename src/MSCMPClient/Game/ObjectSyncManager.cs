using MSCMP.Game.Components;
using MSCMP.Network;
using System.Collections.Concurrent;
using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Class managing sync of objects.
	/// </summary>
	internal class ObjectSyncManager
	{
		/// <summary>
		/// Instance.
		/// </summary>
		public static ObjectSyncManager Instance;

		/// <summary>
		/// Dictionary of ObjectIDs.
		/// </summary>
		public readonly ConcurrentDictionary<int, ObjectSyncComponent> ObjectIDs = new ConcurrentDictionary<int, ObjectSyncComponent>();

		/// <summary>
		/// Type of objects.
		/// </summary>
		public enum ObjectTypes
		{
			Pickupable,
			PlayerVehicle,
			AiVehicle,
			Boat,
			GarageDoor,
			VehicleDoor,
			Weather,
			SewageWell
		}

		/// <summary>
		/// Sync types.
		/// </summary>
		public enum SyncTypes
		{
			GenericSync,
			SetOwner,
			RemoveOwner,
			ForceSetOwner,
			PeriodicSync
		}

		/// <summary>
		/// Specific flags for what to sync on each object.
		/// </summary>
		public enum Flags
		{
			Full,
			RotationOnly,
			PositionOnly,
			VariablesOnly
		}

		/// <summary>
		/// Used when adding an ObjectSyncComponent for an ObjectID to be automatically assigned.
		/// </summary>
		public static int AutomaticId = -1;

		/// <summary>
		/// Local player's Steam ID.
		/// </summary>
		public Steamworks.CSteamID SteamId;

		/// <summary>
		/// Constructor.
		/// </summary>
		public ObjectSyncManager()
		{
			Instance = this;
		}

		/// <summary>
		/// Adds new object to the ObjectIDs Dictionary.
		/// </summary>
		/// <param name="osc">Object to add.</param>
		/// <param name="objectId">Object ID to assign to object.</param>
		/// <returns>ObjectID of object.</returns>
		public int AddNewObject(ObjectSyncComponent osc, int objectId)
		{
			// Assign ObjectID automatically.
			if (objectId == AutomaticId)
			{
				if (SteamId.m_SteamID == 0)
				{
					SteamId = Steamworks.SteamUser.GetSteamID();
				}
				if (NetWorld.DisplayObjectRegisteringDebug)
				{
					Logger.Debug($"Added new ObjectID at: {ObjectIDs.Count + 1} Object type: {osc.ObjectType}");
				}
				ObjectIDs.GetOrAdd(ObjectIDs.Count + 1, osc);
				return ObjectIDs.Count;
			}
			// Assign object a specific ObjectID.

			if (NetWorld.DisplayObjectRegisteringDebug)
			{
				Logger.Debug($"Force adding new ObjectID at: {objectId}");
			}
			if (ObjectIDs.ContainsKey(objectId))
			{
				ObjectIDs[objectId] = osc;
			}
			else
			{
				ObjectIDs.GetOrAdd(objectId, osc);
			}
			return objectId;
		}

		/// <summary>
		/// Check if a periodic object sync should be performed.
		/// </summary>
		/// <returns>True if object periodic sync should be sent.</returns>
		public bool ShouldPeriodicSync(NetPlayer owner, bool syncEnabled)
		{
			if (!NetManager.Instance.IsNetworkPlayerConnected())
			{
				return false;
			}
			if (NetManager.Instance.TicksSinceConnectionStarted % 500 == 0)
			{
				if (syncEnabled || owner == null && NetManager.Instance.IsHost)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Returns the GameObject assigned the specified ID or null if object couldn't be found.
		/// </summary>
		/// <param name="objectId">Object ID.</param>
		/// <returns>GameObject at specified ID.</returns>
		public static GameObject GetObjectById(int objectId)
		{
			if (Instance.ObjectIDs.ContainsKey(objectId))
			{
				return Instance.ObjectIDs[objectId].GetGameObject();
			}

			return null;
		}

		/// <summary>
		/// Returns tghe ObjectSyncComponent assigned the specified ID or null if the component couldn't be found.
		/// </summary>
		/// <param name="objectId">Object ID.</param>
		/// <returns>ObjectSyncComponent at specified ID.</returns>
		public static ObjectSyncComponent GetSyncComponentById(int objectId)
		{
			if (Instance.ObjectIDs.ContainsKey(objectId))
			{
				return Instance.ObjectIDs[objectId];
			}

			return null;
		}

		/// <summary>
		/// Handle set owner messages on the host.
		/// </summary>
		public static void SetOwnerHandler(Network.Messages.ObjectSyncMessage msg, ObjectSyncComponent osc, Steamworks.CSteamID sender)
		{
			NetPlayer player = NetManager.Instance.GetPlayer(sender);
			// No one owns the object, accept ownership request.
			if (osc.Owner == null || osc.Owner == player)
			{
				osc.Owner = player;
				osc.OwnerSetToRemote(NetManager.Instance.GetPlayer(sender));
				SendSyncResponse(player, msg.objectID, true);

				// Send updated ownership info to other clients.
				Network.Messages.ObjectSyncMessage msgBroadcast = new Network.Messages.ObjectSyncMessage();
				msgBroadcast.objectID = msg.objectID;
				msgBroadcast.OwnerPlayerID = NetManager.Instance.GetPlayerIdBySteamId(sender);
				msgBroadcast.SyncType = (int)SyncTypes.SetOwner;
				NetManager.Instance.BroadcastMessage(msgBroadcast, Steamworks.EP2PSend.k_EP2PSendReliable);
			}
			// Someone else owns the object, deny ownership request.
			else
			{
				SendSyncResponse(player, msg.objectID, false);
			}
		}

		/// <summary>
		/// Handles remove owner messages on the host.
		/// </summary>
		public static void RemoveOwnerHandler(Network.Messages.ObjectSyncMessage msg, ObjectSyncComponent osc, Steamworks.CSteamID sender)
		{
			osc.Owner = null;
			osc.OwnerRemoved();

			// Send updated ownership info to other clients.
			Network.Messages.ObjectSyncMessage msgBroadcast = new Network.Messages.ObjectSyncMessage();
			msgBroadcast.objectID = msg.objectID;
			msgBroadcast.OwnerPlayerID = NetManager.Instance.GetPlayerIdBySteamId(sender);
			msgBroadcast.SyncType = (int)SyncTypes.RemoveOwner;
			NetManager.Instance.BroadcastMessage(msgBroadcast, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Handles sync taken by force messages on the host.
		/// </summary>
		public static void SyncTakenByForceHandler(Network.Messages.ObjectSyncMessage msg, ObjectSyncComponent osc, Steamworks.CSteamID sender)
		{
			if (osc.Owner == NetManager.Instance.GetLocalPlayer())
			{
				osc.SyncTakenByForce();
				osc.SyncEnabled = false;
			}
			osc.Owner = NetManager.Instance.GetPlayer(sender);

			if (osc.Owner == NetManager.Instance.GetLocalPlayer())
			{
				osc.SyncEnabled = true;
			}

			// Send updated ownership info to other clients.
			Network.Messages.ObjectSyncMessage msgBroadcast = new Network.Messages.ObjectSyncMessage();
			msgBroadcast.objectID = msg.objectID;
			msgBroadcast.OwnerPlayerID = NetManager.Instance.GetPlayerIdBySteamId(sender);
			msgBroadcast.SyncType = (int)SyncTypes.ForceSetOwner;
			NetManager.Instance.BroadcastMessage(msgBroadcast, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Send sync response to clients.
		/// </summary>
		/// <param name="player">Player to send message to.</param>
		/// <param name="objectId"></param>
		/// <param name="accepted">If the request was approved.</param>
		public static void SendSyncResponse(NetPlayer player, int objectId, bool accepted)
		{
			Network.Messages.ObjectSyncResponseMessage msgResponse = new Network.Messages.ObjectSyncResponseMessage();
			msgResponse.objectID = objectId;
			msgResponse.accepted = accepted;
			NetManager.Instance.SendMessage(player, msgResponse, Steamworks.EP2PSend.k_EP2PSendReliable);
		}
	}
}
