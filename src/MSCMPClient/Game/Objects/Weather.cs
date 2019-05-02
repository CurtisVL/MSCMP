using System;
using UnityEngine;
using MSCMP.Game.Components;

namespace MSCMP.Game.Objects
{
	class Weather : ISyncedObject
	{
		GameObject gameObject;
		ObjectSyncComponent osc;
		PlayMakerFSM weatherFSM;

		// Update rate for weather in frames.
		float syncInterval = 150;
		float currentFrame = 0;

		public enum WeatherStates
		{
			NoWeather,
			Rain,
			Thunder
		}
		WeatherStates currentWeather = WeatherStates.NoWeather;

		/// <summary>
		/// Constructor.
		/// </summary>
		public Weather(GameObject go, ObjectSyncComponent syncComponent) {
			gameObject = go;
			osc = syncComponent;
			weatherFSM = gameObject.GetComponent<PlayMakerFSM>();

			if (Network.NetManager.Instance.IsHost) {
				osc.TakeSyncControl();
			}

			HookEvents();
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags flags() {
			return ObjectSyncManager.Flags.Full;
		}

		/// <summary>
		/// Get object's Transform.
		/// </summary>
		/// <returns>Object's Transform.</returns>
		public Transform ObjectTransform() {
			return gameObject.transform;
		}

		/// <summary>
		/// Set weather position and rotation.
		/// </summary>
		/// <param name="pos">Position.</param>
		/// <param name="rot">Rotation.</param>
		public void SetPosAndRot(Vector3 pos, Quaternion rot) {
			weatherFSM.FsmVariables.GetFsmFloat("PosX").Value = pos.x;
			weatherFSM.FsmVariables.GetFsmFloat("PosZ").Value = pos.z;
			weatherFSM.FsmVariables.GetFsmFloat("Rotation").Value = rot.y;
			gameObject.transform.localRotation = rot;
		}

		/// <summary>
		/// Check is periodic sync of the object is enabled.
		/// </summary>
		/// <returns>Periodic sync enabled or disabled.</returns>
		public bool PeriodicSyncEnabled() {
			return true;
		}

		/// <summary>
		/// Hook vehicle door related events.
		/// </summary>
		void HookEvents() {
			EventHook.Add(weatherFSM, "Rain", new Func<bool>(() => {
				currentWeather = WeatherStates.Rain;
				return false;
			}));
			EventHook.Add(weatherFSM, "Thunder", new Func<bool>(() => {
				currentWeather = WeatherStates.Thunder;
				return false;
			}));
			EventHook.Add(weatherFSM, "No weather", new Func<bool>(() => {
				currentWeather = WeatherStates.NoWeather;
				return false;
			}));
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync() {
			// Only sync weather as the host.
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
			float[] variables = {
				weatherFSM.FsmVariables.GetFsmFloat("Offset").Value,
				weatherFSM.FsmVariables.GetFsmInt("WeatherType").Value,
				(float)currentWeather
			};
			return variables;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables) {
			weatherFSM.FsmVariables.GetFsmFloat("Offset").Value = variables[0];
			weatherFSM.FsmVariables.GetFsmInt("WeatherType").Value = Convert.ToInt32(variables[1]);
			WeatherStates newState = (WeatherStates)variables[2];
			if (newState != currentWeather) {
				switch (newState) {
					case WeatherStates.NoWeather:
						weatherFSM.SendEvent("MP_NoWeather");
						currentWeather = WeatherStates.NoWeather;
						break;
					case WeatherStates.Rain:
						weatherFSM.SendEvent("MP_Rain");
						currentWeather = WeatherStates.Rain;
						break;
					case WeatherStates.Thunder:
						weatherFSM.SendEvent("MP_Thunder");
						currentWeather = WeatherStates.Thunder;
						break;
				}
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
