using UnityEngine;
using MSCMP.Network;

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
		// Is object being picked up.
		public bool PickedUp = false;

		// Object type. (Yes, I know this could be an Enum)
		public ObjectSyncManager.ObjectTypes ObjectType;


		Rigidbody rigidbody;
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
			rigidbody = this.transform.GetComponentInChildren<Rigidbody>();
			if (rigidbody == null) {
				rigidbody = this.transform.parent.GetComponentInChildren<Rigidbody>();
			}
		}

		/// <summary>
		/// Called once per frame.
		/// </summary>
		void LateUpdate() {
			if (SyncEnabled) {
				frame++;
				// Updates the object's position when it moves.
				if (rigidbody.velocity.sqrMagnitude >= 0.01f && PickedUp == false) {
					//Logger.Log($"Object moved and is being synced: {this.transform.name} Velocity magnitude: {rigidbody.velocity.sqrMagnitude}");
					SendObjectSync();
				}
				
				// Updates object's position when picked up.
				else if (PickedUp == true) {
					SendObjectSync();
				}
				
				// Periodically updates the object's position.
				else if (frame == 100) {
					SendObjectSync();
					frame = 0;
				}
			}
		} 

		/// <summary>
		/// Sends a sync update of the object.
		/// </summary>
		void SendObjectSync() {
			if (ObjectType == ObjectSyncManager.ObjectTypes.AIVehicle) {
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.parent.position, gameObject.transform.parent.rotation, 0);
			}
			else {
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.position, gameObject.transform.rotation, 0);
			}
		}

		/// <summary>
		/// Send single sync packet of object's position and attempt to set owner.
		/// </summary>
		public void SendEnterSync() {
			if (Owner == 0) {
				//Logger.Debug($"--> Attempting to take ownership of object: {this.transform.name}");
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.position, gameObject.transform.rotation, 1);
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
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.position, gameObject.transform.rotation, 2);
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
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.position, gameObject.transform.rotation, 3);
			}
		}
	}
}
