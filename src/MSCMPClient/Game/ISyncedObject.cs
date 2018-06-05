using UnityEngine;

namespace MSCMP.Game {
	interface ISyncedObject {

		/// <summary>
		/// Called to determine if the object should be synced. 
		/// </summary>
		/// <returns>True if object should be synced, false if object shouldn't be synced.</returns>
		bool CanSync();

		/// <summary>
		/// Called to return variables that need to be synced on the remote client.
		/// </summary>
		/// <returns>Variables to send to remote client.</returns>
		float[] ReturnSyncedVariables();

		/// <summary>
		/// Handle synced variables sent from the remote client.
		/// </summary>
		/// <param name="variables"></param>
		void HandleSyncedVariables(float[] variables);

		/// <summary>
		/// Called when sync is forcefully taken from client.
		/// </summary>
		void SyncTakenByForce();

		/// <summary>
		/// Called when an object is constantly syncing. (Usually when a pickupable is picked up, or when a vehicle is being driven)
		/// </summary>
		/// <param name="newValue"></param>
		void ConstantSyncChanged(bool newValue);
	}
}
