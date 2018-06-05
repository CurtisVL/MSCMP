using System;
using UnityEngine;
using MSCMP.Game.Components;

namespace MSCMP.Game.Objects {
	class AIVehicle : ISyncedObject {

		ObjectSyncComponent syncComponent;
		bool isSyncing = false;

		GameObject gameObject;
		Rigidbody rigidbody;

		GameObject parentGameObject;

		PlayMakerFSM throttleFsm;

		CarDynamics dynamics;

		class MPCarController : AxisCarController {
			public float remoteThrottleInput = 0.0f;
			public float remoteBrakeInput = 0.0f;
			public float remoteSteerInput = 0.0f;
			public float remoteHandbrakeInput = 0.0f;
			public float remoteClutchInput = 0.0f;
			public bool remoteStartEngineInput = false;
			public int remoteTargetGear = 0;

			protected override void GetInput(out float throttleInput, out float brakeInput, out float steerInput, out float handbrakeInput, out float clutchInput, out bool startEngineInput, out int targetGear) {
				throttleInput = remoteThrottleInput;
				brakeInput = remoteBrakeInput;
				steerInput = remoteSteerInput;
				handbrakeInput = remoteHandbrakeInput;
				clutchInput = remoteClutchInput;
				startEngineInput = remoteStartEngineInput;
				targetGear = remoteTargetGear;
			}
		}

		public float Steering {
			get {
				return dynamics.carController.steering;
			}
			set {
				dynamics.carController.steering = value;
			}
		}

		public float Throttle {
			get {
				return dynamics.carController.throttleInput;
			}
			set {
				dynamics.carController.throttleInput = value;
			}
		}

		public float Brake {
			get {
				return dynamics.carController.brakeInput;
			}
			set {
				dynamics.carController.brakeInput = value;
			}
		}

		public float TargetSpeed {
			get {
				return throttleFsm.FsmVariables.GetFsmFloat("TargetSpeed").Value;
			}
			set {
				remoteTargetSpeed = value;
			}
		}

		float remoteTargetSpeed;

		float steamID = Steamworks.SteamUser.GetSteamID().m_SteamID;


		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="go"></param>
		public AIVehicle(GameObject go, ObjectSyncComponent osc) {
			gameObject = go;
			syncComponent = osc;
			parentGameObject = go.transform.parent.gameObject;

			rigidbody = parentGameObject.GetComponent<Rigidbody>();
			dynamics = parentGameObject.GetComponent<CarDynamics>();

			throttleFsm = Utils.GetPlaymakerScriptByName(parentGameObject, "Throttle");

			EventHooks();
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync() {
			Logger.Log($"TargetSpeed: {TargetSpeed}");
			if (rigidbody.velocity.sqrMagnitude >= 0.01f) {
				isSyncing = true;
				return true;
			}
			else {
				isSyncing = false;
				return false;
			}
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables() {
			if (isSyncing == true) {
				float[] variables = { Steering, Throttle, Brake, TargetSpeed };
				return variables;
			}
			else {
				return null;
			}
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables) {
			if (variables != null) {
				Steering = variables[0];
				Throttle = variables[1];
				Brake = variables[2];
				TargetSpeed = variables[3];
			}
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

		// Event hooks
		public void EventHooks() {
			EventHook.AddWithSync(throttleFsm, "Accelerate", new Func<bool>(() => {
				if (syncComponent.Owner != steamID && syncComponent.Owner != 0) {
					return true;
				}
				return false;
			}));
			EventHook.AddWithSync(throttleFsm, "Burnout", new Func<bool>(() => {
				if (syncComponent.Owner != steamID && syncComponent.Owner != 0) {
					return true;
				}
				return false;
			}));
			EventHook.AddWithSync(throttleFsm, "Stopped", new Func<bool>(() => {
				if (syncComponent.Owner != steamID && syncComponent.Owner != 0) {
					return true;
				}
				return false;
			}));
			EventHook.AddWithSync(throttleFsm, "Other", new Func<bool>(() => {
				if (syncComponent.Owner != steamID && syncComponent.Owner != 0) {
					return true;
				}
				return false;
			}));
			EventHook.AddWithSync(throttleFsm, "Cruise", new Func<bool>(() => {
				if (syncComponent.Owner != steamID && syncComponent.Owner != 0) {
					return true;
				}
				return false;
			}));
			EventHook.AddWithSync(throttleFsm, "Cruise 2", new Func<bool>(() => {
				if (syncComponent.Owner != steamID && syncComponent.Owner != 0) {
					return true;
				}
				return false;
			}));
			EventHook.AddWithSync(throttleFsm, "Cruise 3", new Func<bool>(() => {
				if (syncComponent.Owner != steamID && syncComponent.Owner != 0) {
					return true;
				}
				return false;
			}));

			if (parentGameObject.name == "BUS") {
				PlayMakerFSM doorFsm = Utils.GetPlaymakerScriptByName(parentGameObject.transform.FindChild("Route").gameObject, "Door");
				PlayMakerFSM startFsm = Utils.GetPlaymakerScriptByName(parentGameObject.transform.FindChild("Route").gameObject, "Start");

				EventHook.AddWithSync(doorFsm, "Driver close", new Func<bool>(() => {
					if (syncComponent.SyncEnabled == false) {
						return true;
					}
					return false;
				}));
				EventHook.AddWithSync(doorFsm, "Door close", new Func<bool>(() => {
					if (syncComponent.SyncEnabled == false) {
						return true;
					}
					return false;
				}));
				EventHook.AddWithSync(doorFsm, "Driver open", new Func<bool>(() => {
					if (syncComponent.SyncEnabled == false) {
						return true;
					}
					return false;
				}));
				EventHook.AddWithSync(doorFsm, "Door open", new Func<bool>(() => {
					if (syncComponent.SyncEnabled == false) {
						return true;
					}
					return false;
				}));

				EventHook.AddWithSync(startFsm, "Perajarvi Stop", new Func<bool>(() => {
					if (syncComponent.SyncEnabled == false) {
						return true;
					}
					return false;
				}));
				EventHook.AddWithSync(startFsm, "GO", new Func<bool>(() => {
					if (syncComponent.SyncEnabled == false) {
						return true;
					}
					return false;
				}));
				EventHook.AddWithSync(startFsm, "Drive", new Func<bool>(() => {
					if (syncComponent.SyncEnabled == false) {
						return true;
					}
					return false;
				}));
			}
		}
	}
}
