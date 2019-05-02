using UnityEngine;
using MSCMP.Network;
using MSCMP.Game.Objects;

namespace MSCMP.Game.Components {
	/// <summary>
	/// Attached to objects that require position/rotation sync.
	/// Sync is provided based on distance from the player and paramters inside an ISyncedObject.
	/// </summary>
	class ObjectSyncComponent : MonoBehaviour {
		/// <summary>
		/// If sync is enabled.
		/// </summary>
		public bool SyncEnabled = false;

		/// <summary>
		/// Sync owner of the object.
		/// </summary>
		public NetPlayer Owner = null;

		/// <summary>
		/// Object ID.
		/// </summary>
		public int ObjectID = ObjectSyncManager.AUTOMATIC_ID;

		/// <summary>
		/// Object type.
		/// </summary>
		public ObjectSyncManager.ObjectTypes ObjectType;

		/// <summary>
		/// If sync of an object should be sent constantly.
		/// </summary>
		bool sendConstantSync = false;

		/// <summary>
		/// The object sub-type that is being synced.
		/// </summary>
		ISyncedObject syncedObject;

		/// <summary>
		/// True if the object is setup and ready to sync.
		/// </summary>
		public bool IsSetup = false;

		/// <summary>
		/// GameObject this component is attached to. Used as a reference for when object is disabled.
		/// </summary>
		GameObject thisObject;

		/// <summary>
		/// Sync interval in frames.
		/// </summary>
		int currentFrame = 0;
		int syncInterval = 1;


		/// <summary>
		/// Constructor.
		/// </summary>
		public ObjectSyncComponent() {
			thisObject = this.gameObject;
		}

		/// <summary>
		/// Setup object.
		/// </summary>
		/// <param name="type">Object type.</param>
		/// <param name="objectID">Object ID to assign.</param>
		/// <returns>Assigned Object ID</returns>
		public int Setup(ObjectSyncManager.ObjectTypes type, int objectID) {
			if (!NetWorld.Instance.playerIsLoading) {
				if (!NetManager.Instance.IsHost && objectID == ObjectSyncManager.AUTOMATIC_ID) {
					Logger.Debug("Ignoring spawned object as client is not host!");
					GameObject.Destroy(gameObject);
					return -1;
				}
			}
			IsSetup = false;
			SyncEnabled = false;
			Owner = null;
			ObjectType = type;
			ObjectID = objectID;

			// Assign object's ID.
			ObjectID = ObjectSyncManager.Instance.AddNewObject(this, ObjectID);

			if (!NetWorld.Instance.playerIsLoading && !IsSetup) {
				CreateObjectSubtype();
			}

			return ObjectID;
		}

		/// <summary>
		/// Called on start.
		/// </summary>
		void Start() {
			if (NetWorld.Instance.playerIsLoading && !IsSetup) {
				CreateObjectSubtype();
			}
		}

		/// <summary>
		/// Creates the object's subtype.
		/// </summary>
		void CreateObjectSubtype() {
			// Set object type.
			switch (ObjectType) {
				// Pickupable.
				case ObjectSyncManager.ObjectTypes.Pickupable:
					syncedObject = new Pickupable(this.gameObject);
					break;
				// AI Vehicle.
				case ObjectSyncManager.ObjectTypes.AIVehicle:
					syncedObject = new AIVehicle(this.gameObject, this);
					break;
				// Boat.
				case ObjectSyncManager.ObjectTypes.Boat:
					syncedObject = new Boat(this.gameObject);
					break;
				// Garage door.
				case ObjectSyncManager.ObjectTypes.GarageDoor:
					syncedObject = new GarageDoor(this.gameObject);
					break;
				// Player vehicle.
				case ObjectSyncManager.ObjectTypes.PlayerVehicle:
					syncedObject = new PlayerVehicle(this.gameObject, this);
					break;
				// Vehicle door.
				case ObjectSyncManager.ObjectTypes.VehicleDoor:
					syncedObject = new VehicleDoor(this.gameObject, this);
					break;
				// Weather.
				case ObjectSyncManager.ObjectTypes.Weather:
					syncedObject = new Weather(this.gameObject, this);
					break;
			}
			IsSetup = true;
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		void Update() {
			if (currentFrame >= syncInterval) {
				currentFrame = 0;

				if (!IsSetup && !SyncEnabled) {
					return;
				}

				// Updates object's position continuously, or, if the CanSync criteria is met.
				if (syncedObject.CanSync() || sendConstantSync) {
					SendObjectSync(ObjectSyncManager.SyncTypes.GenericSync, true, false);
				}

				// Periodically update the object's position if periodic sync is enabled.
				if (syncedObject.PeriodicSyncEnabled() && ObjectSyncManager.Instance.ShouldPeriodicSync(Owner, SyncEnabled)) {
					SendObjectSync(ObjectSyncManager.SyncTypes.PeriodicSync, true, false);
				}
			}
			else {
				currentFrame++;
			}
		}

		/// <summary>
		/// Sends a sync update of the object.
		/// </summary>
		public void SendObjectSync(ObjectSyncManager.SyncTypes type, bool sendVariables, bool syncWasRequested) {
			if (ObjectType == ObjectSyncManager.ObjectTypes.Weather) {
				SendObjectSync(ObjectID, syncedObject.ObjectTransform().localPosition, syncedObject.ObjectTransform().localRotation, type, syncedObject.ReturnSyncedVariables(true), syncedObject.flags());
			}

			if (sendVariables) {
				SendObjectSync(ObjectID, syncedObject.ObjectTransform().position, syncedObject.ObjectTransform().rotation, type, syncedObject.ReturnSyncedVariables(true), syncedObject.flags());
			}
			else {
				SendObjectSync(ObjectID, syncedObject.ObjectTransform().position, syncedObject.ObjectTransform().rotation, type, null, syncedObject.flags());
			}
		}

		/// <summary>
		/// Request a sync update from the host.
		/// </summary>
		public void RequestObjectSync() {
			RequestObjectSync(ObjectID);
		}

		/// <summary>
		/// Called when object sync request is accepted by the remote client.
		/// </summary>
		public void SyncRequestAccepted() {
			Owner = NetManager.Instance.GetLocalPlayer();
			Logger.Log("Sync request accepted, object: " + gameObject.name);
			SyncEnabled = true;
		}

		/// <summary>
		/// Called when the player enter sync range of the object.
		/// </summary>
		public void SendEnterSync() {
			if (Owner == null && syncedObject.ShouldTakeOwnership()) {
				SendObjectSync(ObjectSyncManager.SyncTypes.SetOwner, true, false);
			}
		}

		/// <summary>
		/// Called when the player exits sync range of the object.
		/// </summary>
		public void SendExitSync() {
			if (Owner == NetManager.Instance.GetLocalPlayer()) {
				Owner = null;
				SyncEnabled = false;
				SendObjectSync(ObjectSyncManager.SyncTypes.RemoveOwner, false, false);
			}
		}

		/// <summary>
		/// Take sync control of the object by force.
		/// </summary>
		public void TakeSyncControl() {
			if (Owner != NetManager.Instance.GetLocalPlayer()) {
				SendObjectSync(ObjectSyncManager.SyncTypes.ForceSetOwner, true, false);
			}
		}

		/// <summary>
		/// Called when sync owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote(NetPlayer newOwner) {
			Owner = newOwner;
			syncedObject?.OwnerSetToRemote();
		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved() {
			Owner = null;
			syncedObject?.OwnerRemoved();
		}

		/// <summary>
		/// Called when sync control of an object has been taken from local player.
		/// </summary>
		public void SyncTakenByForce() {
			syncedObject?.SyncTakenByForce();
		}

		/// <summary>
		/// Set object to send position and rotation sync constantly.
		/// </summary>
		/// <param name="newValue">If object should be constantly synced.</param>
		public void SendConstantSync(bool newValue) {
			sendConstantSync = newValue;
			syncedObject?.ConstantSyncChanged(newValue);
		}

		/// <summary>
		/// Handles synced variables sent from remote client.
		/// </summary>
		/// <param name="syncedVariables">Synced variables</param>
		public void HandleSyncedVariables(float[] syncedVariables) {
			syncedObject?.HandleSyncedVariables(syncedVariables);
		}

		/// <summary>
		/// Check if object owner is local client.
		/// </summary>
		/// <returns>True is object owner is local client.</returns>
		public bool IsLocallyOwned() {
			return (Owner == NetManager.Instance.GetLocalPlayer());
		}

		/// <summary>
		/// Set object's postion and rotationn.
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="rot"></param>
		public void SetPositionAndRotation(Vector3 pos, Quaternion rot) {
			if (syncedObject == null) {
				// Can be caused by moving an object whilst the remote client is still loading.
				// Object should become synced after the client has finished loading anyway.
				Logger.Debug($"Tried to set position of object '{gameObject.name}' but object isn't setup. (This is usually fine)");
				return;
			}
			if (syncedObject != null) {
				// Weather requires a specific way of syncing.
				if (ObjectType == ObjectSyncManager.ObjectTypes.Weather) {
					Weather weather = syncedObject as Weather;
					weather.SetPosAndRot(pos, rot);
				}
				// All other objects are synced based on what is required.
				switch (syncedObject.flags()) {
					case ObjectSyncManager.Flags.Full:
						syncedObject.ObjectTransform().position = pos;
						syncedObject.ObjectTransform().rotation = rot;
						break;
					case ObjectSyncManager.Flags.PositionOnly:
						syncedObject.ObjectTransform().position = pos;
						break;
					case ObjectSyncManager.Flags.RotationOnly:
						syncedObject.ObjectTransform().rotation = rot;
						break;
				}
			}
		}

		/// <summary>
		/// Return the GameObject of this component.
		/// </summary>
		/// <returns>GameObject.</returns>
		public GameObject GetGameObject() {
			return thisObject;
		}

		/// <summary>
		/// Return the object subtype componennt.
		/// </summary>
		/// <returns>Synced object component.</returns>
		public ISyncedObject GetObjectSubtype() {
			return syncedObject;
		}

		/// <summary>
		/// Send object sync.
		/// </summary>
		/// <param name="objectID">The Object ID of the object.</param>
		/// <param name="setOwner">Set owner of the object.</param>
		public void SendObjectSync(int objectID, Vector3 pos, Quaternion rot, ObjectSyncManager.SyncTypes syncType, float[] syncedVariables, ObjectSyncManager.Flags flags) {
			if (NetManager.Instance.IsHost) {
				SendObjectSyncHost(objectID, pos, rot, syncType, syncedVariables, flags);
				return;
			}

			Network.Messages.ObjectSyncMessage msg = new Network.Messages.ObjectSyncMessage();
			msg.objectID = objectID;
			msg.SyncType = (int)syncType;
			if (syncedVariables != null) {
				msg.SyncedVariables = syncedVariables;
			}

			switch (flags) {
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
		public void SendObjectSyncHost(int objectID, Vector3 pos, Quaternion rot, ObjectSyncManager.SyncTypes syncType, float[] syncedVariables, ObjectSyncManager.Flags flags) {
			Network.Messages.ObjectSyncMessage msg = new Network.Messages.ObjectSyncMessage();
			msg.objectID = objectID;
			msg.SyncType = (int)syncType;
			if (syncedVariables != null) {
				msg.SyncedVariables = syncedVariables;
			}

			switch (flags) {
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

			if (syncType != ObjectSyncManager.SyncTypes.PeriodicSync && syncType != ObjectSyncManager.SyncTypes.GenericSync) {
				Network.Messages.ObjectSyncMessage msgBroadcast = new Network.Messages.ObjectSyncMessage();
				// Set owner as host.
				if (syncType == ObjectSyncManager.SyncTypes.SetOwner) {
					Logger.Log("Host is taking ownership of this object! - " + gameObject.name);
					if (Owner == null) {
						Owner = NetManager.Instance.GetLocalPlayer();
						SyncEnabled = true;
						msgBroadcast.SyncType = (int)ObjectSyncManager.SyncTypes.SetOwner;
					}
				}
				// Remove owner as host.
				else if (syncType == ObjectSyncManager.SyncTypes.RemoveOwner) {
					Owner = null;
					OwnerRemoved();
					msgBroadcast.SyncType = (int)ObjectSyncManager.SyncTypes.RemoveOwner;
				}
				// Force take sync control as host.
				else if (syncType == ObjectSyncManager.SyncTypes.ForceSetOwner) {
					Logger.Log("Host is force taking ownership of this object! - " + gameObject.name);
					SyncEnabled = true;
				}

				// Send updated ownership info to other clients.
				msgBroadcast.objectID = msg.objectID;
				msgBroadcast.OwnerPlayerID = NetManager.Instance.GetPlayerIDBySteamID(Steamworks.SteamUser.GetSteamID());
				NetManager.Instance.BroadcastMessage(msgBroadcast, Steamworks.EP2PSend.k_EP2PSendReliable);
			}
			else {
				NetManager.Instance.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			}
		}

		/// <summary>
		/// Request object sync from the host.
		/// </summary>
		/// <param name="objectID">The Object ID of the object.</param>
		public void RequestObjectSync(int objectID) {
			Network.Messages.ObjectSyncRequestMessage msg = new Network.Messages.ObjectSyncRequestMessage();
			msg.objectID = objectID;
			NetManager.Instance.SendMessage(NetManager.Instance.GetHostPlayer(), msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}
	}
}
