using System;
using UnityEngine;

namespace MSCMP.Game.Objects
{
	class SewageWell : ISyncedObject
	{
		GameObject gameObject;
		Components.ObjectSyncComponent osc;

		// Update rate for weather in frames.
		float syncInterval = 150;
		float currentFrame = 0;

		GameObject fsmGO;
		PlayMakerFSM levelFSM;

		public enum WellStates
		{
			Full,
			WaitCall,
			Reset
		}
		WellStates currentState = WellStates.Reset;

		/// <summary>
		/// Constructor.
		/// </summary>
		public SewageWell(GameObject go, Components.ObjectSyncComponent syncComponent) {
			gameObject = go;
			osc = syncComponent;

			fsmGO = gameObject.transform.FindChild("WasteWell_2000litre/Shit/Level/ShitLevelTrigger").gameObject;
			levelFSM = Utils.GetPlaymakerScriptByName(fsmGO, "Level");

			if (Network.NetManager.Instance.IsHost) {
				osc.TakeSyncControl();
			}

			HookEvents();
		}

		/// <summary>
		/// Hook events for this sewage well.
		/// </summary>
		void HookEvents() {
			// Get currently active state.
			if (levelFSM.Fsm.ActiveStateName == "Full") {
				currentState = WellStates.Full;
			}
			else if (levelFSM.Fsm.ActiveStateName == "Reset") {
				currentState = WellStates.Reset;
			}
			if (levelFSM.Fsm.ActiveStateName == "Wait call") {
				currentState = WellStates.WaitCall;
			}

			// Hook events and sync them.
			EventHook.AddWithSync(levelFSM, "Full", new Func<bool>(() => {
				currentState = WellStates.Full;
				return false;
			}));
			EventHook.AddWithSync(levelFSM, "Reset", new Func<bool>(() => {
				currentState = WellStates.Reset;
				return false;
			}));
			EventHook.AddWithSync(levelFSM, "Wait call", new Func<bool>(() => {
				currentState = WellStates.WaitCall;
				return false;
			}));

			EventHook.AddWithSync(levelFSM, "Pay");
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags flags() {
			return ObjectSyncManager.Flags.VariablesOnly;
		}

		/// <summary>
		/// Get object's Transform.
		/// </summary>
		/// <returns>Object's Transform.</returns>
		public Transform ObjectTransform() {
			return gameObject.transform;
		}

		/// <summary>
		/// Check is periodic sync of the object is enabled.
		/// </summary>
		/// <returns>Periodic sync enabled or disabled.</returns>
		public bool PeriodicSyncEnabled() {
			return true;
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync() {
			// Only sync sewage wells as the host.
			if (Network.NetManager.Instance.IsHost) {
				if (currentFrame >= syncInterval) {
					currentFrame = 0;
					return true;
				}
				else {
					currentFrame++;
					return false;
				}
			}
			else {
				return false;
			}
		}

		/// <summary>
		/// Called when a player enters range of an object.
		/// </summary>
		/// <returns>True if the player should try to take ownership of the object.</returns>
		public bool ShouldTakeOwnership() {
			if (Network.NetManager.Instance.IsHost) {
				return true;
			}
			else {
				return false;
			}
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables) {
			float called = 0;
			if (levelFSM.Fsm.GetFsmBool("Called").Value) {
				called = 1;
			}
			float[] variables = { levelFSM.Fsm.GetFsmFloat("ShitLevel").Value, (float)currentState, called };
			return variables;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables) {
			// Shit level.
			levelFSM.Fsm.GetFsmFloat("ShitLevel").Value = variables[0];

			// Current well state.
			if (currentState != (WellStates)variables[1]) {
				switch ((WellStates)variables[1]) {
					case WellStates.Full:
						levelFSM.SendEvent("MP_Full");
						break;
					case WellStates.Reset:
						levelFSM.SendEvent("MP_Reset");
						break;
					case WellStates.WaitCall:
						levelFSM.SendEvent("MP_Wait call");
						break;
				}
			}

			// If the house has been called.
			if (variables[1] == 1) {
				levelFSM.Fsm.GetFsmBool("Called").Value = true;
			}
			else {
				levelFSM.Fsm.GetFsmBool("Called").Value = false;
			}
		}

		/// <summary>
		/// Called when owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote() {

		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved() {

		}

		/// <summary>
		/// Called when sync control is taken by force.
		/// </summary>
		public void SyncTakenByForce() {

		}

		/// <summary>
		/// Called when an object is constantly syncing. (Usually when a pickupable is picked up, or when a vehicle is being driven)
		/// </summary>
		/// <param name="newValue">If object is being constantly synced.</param>
		public void ConstantSyncChanged(bool newValue) {

		}
	}
}
