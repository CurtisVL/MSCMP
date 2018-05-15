using UnityEngine;
using MSCMP.Network;
using System.Threading.Tasks;

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

		// Rigidbody of object.
		Rigidbody rigidbody;

		/// <summary>
		/// Ran on script enable.
		/// </summary>
		/// <param name="objectID"></param>
		void Start() {
			Logger.Log($"Sync object added to: {this.transform.name}");
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
		void Update() {
			if (SyncEnabled) {
				if (rigidbody.velocity.sqrMagnitude >= 0.01f) {
					//Logger.Log($"Object moved and is being synced: {this.transform.name} Velocity magnitude: {rigidbody.velocity.sqrMagnitude}");
					if (transform.name == "CarColliderAI") {
						NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.parent.position, gameObject.transform.parent.rotation, 0);
					}
					else {
						NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.position, gameObject.transform.rotation, 0);
					}
				}
			}
		} 

		/// <summary>
		/// Send single sync packet of object's position and attempt to set owner.
		/// </summary>
		public void SendEnterSync() {
			if (Owner == 0) {
				Logger.Log($"--> Attempting to take ownership of object: {this.transform.name}");
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.position, gameObject.transform.rotation, 1);
			}
		}

		/// <summary>
		/// Send single sync packet of object's position and set owner.
		/// </summary>
		public void SendExitSync() {
			if (Owner == ObjectSyncManager.Instance.steamID.m_SteamID) {
				Logger.Log($"<-- Removing ownership of object: {this.transform.name} New owner: {Owner}");
				Owner = 0;
				SyncEnabled = false;
				NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.position, gameObject.transform.rotation, 2);
			}
			else {
				Logger.Log($"<-- Exited trigger, but not removing owner of object: {this.transform.name} Owner: {Owner}");
			}
		}

		/// <summary>
		/// Take sync control of the object.
		/// </summary>
		public void TakeSyncControl() {
			Logger.Log($"--> Force taking ownership of object: {this.transform.name}");
			NetLocalPlayer.Instance.SendObjectSync(ObjectID, gameObject.transform.position, gameObject.transform.rotation, 3);
		}

		/// <summary>
		/// Send the object to the remote client once it is setup.
		/// </summary>
		public void SendToRemote() {
			Task t = new Task(SendToRemoteWait);
			t.Start();
		}

		/// <summary>
		/// Wait for Object ID to be assigned.
		/// </summary>
		public void SendToRemoteWait() {
			while (ObjectID == -1) {
				// Temp solution to wait for ID to be assigned.
			}
			Logger.Log("ID assigned, sending to remote client!");
			if (transform.name == "CarColliderAI") {
				NetLocalPlayer.Instance.SendNewObject(ObjectID, transform.parent.name, transform.parent.position, transform.parent.rotation);
			}
			else {
				NetLocalPlayer.Instance.SendNewObject(ObjectID, transform.name, transform.position, transform.rotation);
			}
		}
	}
}
