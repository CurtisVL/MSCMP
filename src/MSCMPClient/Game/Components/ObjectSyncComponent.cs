using MSCMP.Game.Objects;
using MSCMP.Network;
using UnityEngine;

namespace MSCMP.Game.Components
{
	/// <summary>
	/// Attached to objects that require position/rotation sync.
	/// Sync is provided based on distance from the player and paramters inside an ISyncedObject.
	/// </summary>
	internal class ObjectSyncComponent
		: MonoBehaviour
	{
		/// <summary>
		/// If sync is enabled.
		/// </summary>
		public bool SyncEnabled;

		/// <summary>
		/// Sync owner of the object.
		/// </summary>
		public NetPlayer Owner;

		/// <summary>
		/// Object ID.
		/// </summary>
		public int ObjectId = ObjectSyncManager.AutomaticId;

		/// <summary>
		/// Object type.
		/// </summary>
		public ObjectSyncManager.ObjectTypes ObjectType;

		/// <summary>
		/// If sync of an object should be sent constantly.
		/// </summary>
		private bool _sendConstantSync;

		/// <summary>
		/// The object sub-type that is being synced.
		/// </summary>
		private ISyncedObject _syncedObject;

		/// <summary>
		/// True if the object is setup and ready to sync.
		/// </summary>
		public bool IsSetup;

		/// <summary>
		/// GameObject this component is attached to. Used as a reference for when object is disabled.
		/// </summary>
		private readonly GameObject _thisObject;

		/// <summary>
		/// Sync interval in frames.
		/// </summary>
		private int _currentFrame;

		private readonly int _syncInterval = 1;


		/// <summary>
		/// Constructor.
		/// </summary>
		public ObjectSyncComponent()
		{
			_thisObject = gameObject;
		}

		/// <summary>
		/// Setup object.
		/// </summary>
		/// <param name="type">Object type.</param>
		/// <param name="objectId">Object ID to assign.</param>
		/// <returns>Assigned Object ID</returns>
		public int Setup(ObjectSyncManager.ObjectTypes type, int objectId)
		{
			if (!NetWorld.Instance.PlayerIsLoading)
			{
				if (!NetManager.Instance.IsHost && objectId == ObjectSyncManager.AutomaticId)
				{
					Logger.Debug("Ignoring spawned object as client is not host!");
					Destroy(gameObject);
					return -1;
				}
			}
			IsSetup = false;
			SyncEnabled = false;
			Owner = null;
			ObjectType = type;
			ObjectId = objectId;

			// Assign object's ID.
			ObjectId = ObjectSyncManager.Instance.AddNewObject(this, ObjectId);

			if (!NetWorld.Instance.PlayerIsLoading && !IsSetup)
			{
				CreateObjectSubtype();
			}

			return ObjectId;
		}

		/// <summary>
		/// Called on start.
		/// </summary>
		private void Start()
		{
			if (NetWorld.Instance.PlayerIsLoading && !IsSetup)
			{
				CreateObjectSubtype();
			}
		}

		/// <summary>
		/// Creates the object's subtype.
		/// </summary>
		private void CreateObjectSubtype()
		{
			// Set object type.
			switch (ObjectType)
			{
				// Pickupable.
				case ObjectSyncManager.ObjectTypes.Pickupable:
					_syncedObject = new Pickupable(gameObject);
					break;
				// AI Vehicle.
				case ObjectSyncManager.ObjectTypes.AiVehicle:
					_syncedObject = new AiVehicle(gameObject, this);
					break;
				// Boat.
				case ObjectSyncManager.ObjectTypes.Boat:
					_syncedObject = new Boat(gameObject);
					break;
				// Garage door.
				case ObjectSyncManager.ObjectTypes.GarageDoor:
					_syncedObject = new GarageDoor(gameObject);
					break;
				// Player vehicle.
				case ObjectSyncManager.ObjectTypes.PlayerVehicle:
					_syncedObject = new PlayerVehicle(gameObject, this);
					break;
				// Vehicle door.
				case ObjectSyncManager.ObjectTypes.VehicleDoor:
					_syncedObject = new VehicleDoor(gameObject, this);
					break;
				// Weather.
				case ObjectSyncManager.ObjectTypes.Weather:
					_syncedObject = new Weather(gameObject, this);
					break;
				// Sewage well.
				case ObjectSyncManager.ObjectTypes.SewageWell:
					_syncedObject = new SewageWell(gameObject, this);
					break;
			}
			IsSetup = true;
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		private void Update()
		{
			if (_currentFrame >= _syncInterval)
			{
				_currentFrame = 0;

				if (!IsSetup && !SyncEnabled)
				{
					return;
				}

				// Updates object's position continuously, or, if the CanSync criteria is met.
				if (_syncedObject.CanSync() || _sendConstantSync)
				{
					SendObjectSync(ObjectSyncManager.SyncTypes.GenericSync, true, false);
				}

				// Periodically update the object's position if periodic sync is enabled.
				if (_syncedObject.PeriodicSyncEnabled() && ObjectSyncManager.Instance.ShouldPeriodicSync(Owner, SyncEnabled))
				{
					SendObjectSync(ObjectSyncManager.SyncTypes.PeriodicSync, true, false);
				}
			}
			else
			{
				_currentFrame++;
			}
		}

		/// <summary>
		/// Sends a sync update of the object.
		/// </summary>
		public void SendObjectSync(ObjectSyncManager.SyncTypes type, bool sendVariables, bool syncWasRequested)
		{
			if (ObjectType == ObjectSyncManager.ObjectTypes.Weather)
			{
				SendObjectSync(ObjectId, _syncedObject.ObjectTransform().localPosition, _syncedObject.ObjectTransform().localRotation, type, _syncedObject.ReturnSyncedVariables(true), _syncedObject.Flags());
			}

			if (sendVariables)
			{
				SendObjectSync(ObjectId, _syncedObject.ObjectTransform().position, _syncedObject.ObjectTransform().rotation, type, _syncedObject.ReturnSyncedVariables(true), _syncedObject.Flags());
			}
			else
			{
				SendObjectSync(ObjectId, _syncedObject.ObjectTransform().position, _syncedObject.ObjectTransform().rotation, type, null, _syncedObject.Flags());
			}
		}

		/// <summary>
		/// Request a sync update from the host.
		/// </summary>
		public void RequestObjectSync()
		{
			RequestObjectSync(ObjectId);
		}

		/// <summary>
		/// Called when object sync request is accepted by the remote client.
		/// </summary>
		public void SyncRequestAccepted()
		{
			Owner = NetManager.Instance.GetLocalPlayer();
			Logger.Log("Sync request accepted, object: " + gameObject.name);
			SyncEnabled = true;
		}

		/// <summary>
		/// Called when the player enter sync range of the object.
		/// </summary>
		public void SendEnterSync()
		{
			if (Owner == null && _syncedObject.ShouldTakeOwnership())
			{
				SendObjectSync(ObjectSyncManager.SyncTypes.SetOwner, true, false);
			}
		}

		/// <summary>
		/// Called when the player exits sync range of the object.
		/// </summary>
		public void SendExitSync()
		{
			if (Owner == NetManager.Instance.GetLocalPlayer())
			{
				Owner = null;
				SyncEnabled = false;
				SendObjectSync(ObjectSyncManager.SyncTypes.RemoveOwner, false, false);
			}
		}

		/// <summary>
		/// Take sync control of the object by force.
		/// </summary>
		public void TakeSyncControl()
		{
			if (Owner != NetManager.Instance.GetLocalPlayer())
			{
				SendObjectSync(ObjectSyncManager.SyncTypes.ForceSetOwner, true, false);
			}
		}

		/// <summary>
		/// Called when sync owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote(NetPlayer newOwner)
		{
			Owner = newOwner;
			_syncedObject?.OwnerSetToRemote();
		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved()
		{
			Owner = null;
			_syncedObject?.OwnerRemoved();
		}

		/// <summary>
		/// Called when sync control of an object has been taken from local player.
		/// </summary>
		public void SyncTakenByForce()
		{
			_syncedObject?.SyncTakenByForce();
		}

		/// <summary>
		/// Set object to send position and rotation sync constantly.
		/// </summary>
		/// <param name="newValue">If object should be constantly synced.</param>
		public void SendConstantSync(bool newValue)
		{
			_sendConstantSync = newValue;
			_syncedObject?.ConstantSyncChanged(newValue);
		}

		/// <summary>
		/// Handles synced variables sent from remote client.
		/// </summary>
		/// <param name="syncedVariables">Synced variables</param>
		public void HandleSyncedVariables(float[] syncedVariables)
		{
			_syncedObject?.HandleSyncedVariables(syncedVariables);
		}

		/// <summary>
		/// Check if object owner is local client.
		/// </summary>
		/// <returns>True is object owner is local client.</returns>
		public bool IsLocallyOwned()
		{
			return Owner == NetManager.Instance.GetLocalPlayer();
		}

		/// <summary>
		/// Set object's postion and rotationn.
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="rot"></param>
		public void SetPositionAndRotation(Vector3 pos, Quaternion rot)
		{
			if (_syncedObject == null)
			{
				// Can be caused by moving an object whilst the remote client is still loading.
				// Object should become synced after the client has finished loading anyway.
				Logger.Debug($"Tried to set position of object '{gameObject.name}' but object isn't setup. (This is usually fine)");
				return;
			}
			if (_syncedObject != null)
			{
				// Weather requires a specific way of syncing.
				if (ObjectType == ObjectSyncManager.ObjectTypes.Weather)
				{
					Weather weather = _syncedObject as Weather;
					weather?.SetPosAndRot(pos, rot);
				}
				// All other objects are synced based on what is required.
				switch (_syncedObject.Flags())
				{
					case ObjectSyncManager.Flags.Full:
						_syncedObject.ObjectTransform().position = pos;
						_syncedObject.ObjectTransform().rotation = rot;
						break;
					case ObjectSyncManager.Flags.PositionOnly:
						_syncedObject.ObjectTransform().position = pos;
						break;
					case ObjectSyncManager.Flags.RotationOnly:
						_syncedObject.ObjectTransform().rotation = rot;
						break;
				}
			}
		}

		/// <summary>
		/// Return the GameObject of this component.
		/// </summary>
		/// <returns>GameObject.</returns>
		public GameObject GetGameObject()
		{
			return _thisObject;
		}

		/// <summary>
		/// Return the object subtype componennt.
		/// </summary>
		/// <returns>Synced object component.</returns>
		public ISyncedObject GetObjectSubtype()
		{
			return _syncedObject;
		}

		/// <summary>
		/// Send object sync.
		/// </summary>
		/// <param name="objectId">The Object ID of the object.</param>
		/// <param name="pos"></param>
		/// <param name="rot"></param>
		/// <param name="syncType"></param>
		/// <param name="syncedVariables"></param>
		/// <param name="flags"></param>
		public void SendObjectSync(int objectId, Vector3 pos, Quaternion rot, ObjectSyncManager.SyncTypes syncType, float[] syncedVariables, ObjectSyncManager.Flags flags)
		{
			if (NetManager.Instance.IsHost)
			{
				SendObjectSyncHost(objectId, pos, rot, syncType, syncedVariables, flags);
				return;
			}

			Network.Messages.ObjectSyncMessage msg = new Network.Messages.ObjectSyncMessage
			{
				objectID = objectId,
				SyncType = (int)syncType
			};
			if (syncedVariables != null)
			{
				msg.SyncedVariables = syncedVariables;
			}

			switch (flags)
			{
				case ObjectSyncManager.Flags.Full:
					msg.Position = Utils.GameVec3ToNet(pos);
					msg.Rotation = Utils.GameQuatToNet(rot);
					break;
				case ObjectSyncManager.Flags.RotationOnly:
					msg.Rotation = Utils.GameQuatToNet(rot);
					break;
				case ObjectSyncManager.Flags.PositionOnly:
					msg.Position = Utils.GameVec3ToNet(pos);
					break;
			}

			NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Send object sync as the host.
		/// </summary>
		public void SendObjectSyncHost(int objectId, Vector3 pos, Quaternion rot, ObjectSyncManager.SyncTypes syncType, float[] syncedVariables, ObjectSyncManager.Flags flags)
		{
			Network.Messages.ObjectSyncMessage msg = new Network.Messages.ObjectSyncMessage
			{
				objectID = objectId,
				SyncType = (int)syncType
			};

			if (syncedVariables != null)
			{
				msg.SyncedVariables = syncedVariables;
			}

			switch (flags)
			{
				case ObjectSyncManager.Flags.Full:
					msg.Position = Utils.GameVec3ToNet(pos);
					msg.Rotation = Utils.GameQuatToNet(rot);
					break;
				case ObjectSyncManager.Flags.RotationOnly:
					msg.Rotation = Utils.GameQuatToNet(rot);
					break;
				case ObjectSyncManager.Flags.PositionOnly:
					msg.Position = Utils.GameVec3ToNet(pos);
					break;
			}

			if (syncType != ObjectSyncManager.SyncTypes.PeriodicSync && syncType != ObjectSyncManager.SyncTypes.GenericSync)
			{
				Network.Messages.ObjectSyncMessage msgBroadcast = new Network.Messages.ObjectSyncMessage();
				// Set owner as host.
				if (syncType == ObjectSyncManager.SyncTypes.SetOwner)
				{
					if (Owner == null)
					{
						Owner = NetManager.Instance.GetLocalPlayer();
						SyncEnabled = true;
						msgBroadcast.SyncType = (int)ObjectSyncManager.SyncTypes.SetOwner;
					}
				}
				// Remove owner as host.
				else if (syncType == ObjectSyncManager.SyncTypes.RemoveOwner)
				{
					Owner = null;
					OwnerRemoved();
					msgBroadcast.SyncType = (int)ObjectSyncManager.SyncTypes.RemoveOwner;
				}
				// Force take sync control as host.
				else if (syncType == ObjectSyncManager.SyncTypes.ForceSetOwner)
				{
					SyncEnabled = true;
					msgBroadcast.SyncType = (int)ObjectSyncManager.SyncTypes.ForceSetOwner;
				}

				// Send updated ownership info to other clients.
				msgBroadcast.objectID = msg.objectID;
				msgBroadcast.OwnerPlayerID = NetManager.Instance.GetPlayerIdBySteamId(Steamworks.SteamUser.GetSteamID());
				NetManager.Instance.BroadcastMessage(msgBroadcast, Steamworks.EP2PSend.k_EP2PSendReliable);
			}
			else
			{
				NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			}
		}

		/// <summary>
		/// Request object sync from the host.
		/// </summary>
		/// <param name="objectId">The Object ID of the object.</param>
		public void RequestObjectSync(int objectId)
		{
			Network.Messages.ObjectSyncRequestMessage msg = new Network.Messages.ObjectSyncRequestMessage
			{
				objectID = objectId
			};
			NetManager.Instance.SendMessage(NetManager.Instance.GetHostPlayer(), msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}
	}
}
