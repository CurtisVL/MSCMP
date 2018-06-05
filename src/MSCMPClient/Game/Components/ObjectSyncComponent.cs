using UnityEngine;
using MSCMP.Network;
using MSCMP.Game.Objects;

namespace MSCMP.Game.Components {
	/// <summary>
	/// Attached to objects that require position/rotation sync.
	/// </summary>
	class ObjectSyncComponent : MonoBehaviour {
		// If sync is enabled.
		public bool SyncEnabled = false;
		// Sync frequency, 1 = Most frequent.
		public int Zone = 5;
		// Sync owner.
		public ulong Owner = 0;
		// Object ID.
		public int ObjectID = -1;
		// Object type.
		public ObjectSyncManager.ObjectTypes ObjectType;

		Vector3 position {
			get {
				if (ObjectType == ObjectSyncManager.ObjectTypes.AIVehicle) {
					// AI Vehicles have to return/set the position of a different GameObject.
					return gameObject.transform.parent.position;
				}
				else {
					return gameObject.transform.position;
				}
			}
			set {
				if (ObjectType == ObjectSyncManager.ObjectTypes.AIVehicle) {
					gameObject.transform.parent.position = value;
				}
				else {
					gameObject.transform.position = value;
				}
			}
		}

		Quaternion rotation {
			get {
				if (ObjectType == ObjectSyncManager.ObjectTypes.AIVehicle) {
					// AI Vehicles have to return/set the rotation of a different GameObject.
					return gameObject.transform.parent.rotation;
				}
				else {
					return gameObject.transform.rotation;
				}
			}
			set {
				if (ObjectType == ObjectSyncManager.ObjectTypes.AIVehicle) {
					gameObject.transform.parent.rotation = value;
				}
				else {
					gameObject.transform.rotation = value;
				}
			}
		}

		bool sendConstantSync = false;
		ISyncedObject syncedObject;
		int frame = 0;

		/// <summary>
		/// Ran on script enable.
		/// </summary>
		/// <param name="objectID"></param>
		void Start() {
			Logger.Debug($"Sync object added to: {this.transform.name}");

			if (ObjectID == -1) {
				ObjectID = ObjectSyncManager.Instance.AddNewObject(this);
			}
			else {
				ObjectSyncManager.Instance.ForceAddNewObject(this, ObjectID);
			}

			if (ObjectType == ObjectSyncManager.ObjectTypes.Pickupable) {
				syncedObject = new Pickupable(this.gameObject);
			}
			else if (ObjectType == ObjectSyncManager.ObjectTypes.AIVehicle) {
				syncedObject = new AIVehicle(this.gameObject, this);
			}
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		void Update() {
			if (SyncEnabled) {
				// Updates object's position constantly.
				if (sendConstantSync == true) {
					SendObjectSync();
				}

				// Updates the object's position when it moves.
				else if (syncedObject.CanSync() == true && sendConstantSync == false) {
					SendObjectSync();
				}

				// Periodically updates the object's position.
				else if (frame < 100) {
					// This is in an if statement as it could potentially overflow if 'SendConstantSync' is active for too long with it outside of here.
					frame++;
				}

				else if (frame >= 100) {
					SendObjectSync();
					frame = 0;
				}
			}
		}

		/// <summary>
		/// Sends a sync update of the object.
		/// </summary>
		void SendObjectSync() {
			NetLocalPlayer.Instance.SendObjectSync(ObjectID, position, rotation, 0, syncedObject.ReturnSyncedVariables());
		}

		/// <summary>
		/// Send single sync packet of object's position and attempt to set owner.
		/// </summary>
		public void SendEnterSync() {
			if (Owner == 0) {
				//Logger.Debug($"--> Attempting to take ownership of object: {this.transform.name}");
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, position, rotation, 1, syncedObject.ReturnSyncedVariables());
			}
		}

		/// <summary>
		/// Send single sync packet of object's position and set owner.
		/// </summary>
		public void SendExitSync() {
			if (Owner == ObjectSyncManager.Instance.steamID.m_SteamID) {
				//Logger.Debug($"<-- Removing ownership of object: {this.transform.name} New owner: {Owner}");
				Owner = 0;
				SyncEnabled = false;
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, position, rotation, 2, null);
			}
			else {
				//Logger.Debug($"<-- Exited trigger, but not removing owner of object: {this.transform.name} Owner: {Owner}");
			}
		}

		/// <summary>
		/// Take sync control of the object by force.
		/// </summary>
		public void TakeSyncControl() {
			if (Owner != Steamworks.SteamUser.GetSteamID().m_SteamID) {
				Logger.Debug($"--> Force taking ownership of object: {this.transform.name}");
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, position, rotation, 3, syncedObject.ReturnSyncedVariables());
			}
		}

		/// <summary>
		/// Called when sync control of an object has been taken from local player.
		/// </summary>
		public void SyncTakenByForce() {
			syncedObject.SyncTakenByForce();
		}

		/// <summary>
		/// Set object to send position and rotation sync constantly.
		/// </summary>
		/// <param name="newValue">If object should be constantly synced.</param>
		public void SendConstantSync(bool newValue) {
			sendConstantSync = newValue;
			syncedObject.ConstantSyncChanged(newValue);
		}

		/// <summary>
		/// Handles synced variables sent from remote client.
		/// </summary>
		/// <param name="syncedVariables">Synced variables</param>
		public void HandleSyncedVariables(float[] syncedVariables) {
			syncedObject.HandleSyncedVariables(syncedVariables);
		}

		/// <summary>
		/// Set object's postion and rotationn.
		/// </summary>
		/// <param name="pos"></param>
		/// <param name="rot"></param>
		public void SetPositionAndRotation(Vector3 pos, Quaternion rot) {
			position = pos;
			rotation = rot;
		}
	}
}
