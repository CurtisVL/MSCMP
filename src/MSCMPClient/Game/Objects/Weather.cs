using MSCMP.Game.Components;
using System;
using UnityEngine;

namespace MSCMP.Game.Objects
{
	internal class Weather 
		: ISyncedObject
	{
		private readonly GameObject _gameObject;
		private readonly PlayMakerFSM _weatherFsm;

		// Update rate for weather in frames.
		private readonly float _syncInterval = 150;
		private float _currentFrame;

		public enum WeatherStates
		{
			NoWeather,
			Rain,
			Thunder
		}

		private WeatherStates _currentWeather = WeatherStates.NoWeather;

		/// <summary>
		/// Constructor.
		/// </summary>
		public Weather(GameObject go, ObjectSyncComponent syncComponent)
		{
			_gameObject = go;
			_weatherFsm = _gameObject.GetComponent<PlayMakerFSM>();

			if (Network.NetManager.Instance.IsHost)
			{
				syncComponent.TakeSyncControl();
			}

			HookEvents();
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags Flags()
		{
			return ObjectSyncManager.Flags.Full;
		}

		/// <summary>
		/// Get object's Transform.
		/// </summary>
		/// <returns>Object's Transform.</returns>
		public Transform ObjectTransform()
		{
			return _gameObject.transform;
		}

		/// <summary>
		/// Set weather position and rotation.
		/// </summary>
		/// <param name="pos">Position.</param>
		/// <param name="rot">Rotation.</param>
		public void SetPosAndRot(Vector3 pos, Quaternion rot)
		{
			_weatherFsm.FsmVariables.GetFsmFloat("PosX").Value = pos.x;
			_weatherFsm.FsmVariables.GetFsmFloat("PosZ").Value = pos.z;
			_weatherFsm.FsmVariables.GetFsmFloat("Rotation").Value = rot.y;
			_gameObject.transform.localRotation = rot;
		}

		/// <summary>
		/// Check is periodic sync of the object is enabled.
		/// </summary>
		/// <returns>Periodic sync enabled or disabled.</returns>
		public bool PeriodicSyncEnabled()
		{
			return true;
		}

		/// <summary>
		/// Hook vehicle door related events.
		/// </summary>
		private void HookEvents()
		{
			EventHook.Add(_weatherFsm, "Rain", () =>
			{
				_currentWeather = WeatherStates.Rain;
				return false;
			});
			EventHook.Add(_weatherFsm, "Thunder", () =>
			{
				_currentWeather = WeatherStates.Thunder;
				return false;
			});
			EventHook.Add(_weatherFsm, "No weather", () =>
			{
				_currentWeather = WeatherStates.NoWeather;
				return false;
			});
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync()
		{
			// Only sync weather as the host.
			if (Network.NetManager.Instance.IsHost)
			{
				if (_currentFrame >= _syncInterval)
				{
					_currentFrame = 0;
					return true;
				}

				_currentFrame++;
				return false;
			}

			return false;
		}

		/// <summary>
		/// Called when a player enters range of an object.
		/// </summary>
		/// <returns>True if the player should try to take ownership of the object.</returns>
		public bool ShouldTakeOwnership()
		{
			if (Network.NetManager.Instance.IsHost)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables)
		{
			float[] variables = {
				_weatherFsm.FsmVariables.GetFsmFloat("Offset").Value,
				_weatherFsm.FsmVariables.GetFsmInt("WeatherType").Value,
				(float)_currentWeather
			};
			return variables;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables)
		{
			_weatherFsm.FsmVariables.GetFsmFloat("Offset").Value = variables[0];
			_weatherFsm.FsmVariables.GetFsmInt("WeatherType").Value = Convert.ToInt32(variables[1]);
			WeatherStates newState = (WeatherStates)variables[2];
			if (newState != _currentWeather)
			{
				switch (newState)
				{
					case WeatherStates.NoWeather:
						_weatherFsm.SendEvent("MP_NoWeather");
						_currentWeather = WeatherStates.NoWeather;
						break;
					case WeatherStates.Rain:
						_weatherFsm.SendEvent("MP_Rain");
						_currentWeather = WeatherStates.Rain;
						break;
					case WeatherStates.Thunder:
						_weatherFsm.SendEvent("MP_Thunder");
						_currentWeather = WeatherStates.Thunder;
						break;
				}
			}
		}

		/// <summary>
		/// Called when owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote()
		{

		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved()
		{

		}

		/// <summary>
		/// Called when sync control is taken by force.
		/// </summary>
		public void SyncTakenByForce()
		{

		}

		/// <summary>
		/// Called when an object is constantly syncing. (Usually when a pickupable is picked up, or when a vehicle is being driven)
		/// </summary>
		/// <param name="newValue">If object is being constantly synced.</param>
		public void ConstantSyncChanged(bool newValue)
		{

		}
	}
}
