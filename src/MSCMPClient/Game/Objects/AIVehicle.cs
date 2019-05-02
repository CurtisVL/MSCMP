using MSCMP.Game.Components;
using System;
using UnityEngine;

namespace MSCMP.Game.Objects
{
	/// <summary>
	/// Handles syncing and events of Ai vehicles.
	/// </summary>
	internal class AiVehicle 
		: ISyncedObject
	{
		private readonly ObjectSyncComponent _syncComponent;
		private bool _isSyncing;

		private readonly GameObject _gameObject;
		private readonly Rigidbody _rigidbody;

		private readonly GameObject _parentGameObject;

		private PlayMakerFSM _throttleFsm;
		private PlayMakerFSM _navigationFsm;
		private readonly PlayMakerFSM _directionFsm;

		private readonly CarDynamics _dynamics;

		private float _isClockwise;

		public enum VehicleTypes
		{
			Bus,
			Amis,
			Traffic,
			TrafficDirectional,
			Fitan,
		}

		public VehicleTypes Type;

		private class MpCarController : AxisCarController
		{
			public readonly float RemoteThrottleInput = 0.0f;
			public readonly float RemoteBrakeInput = 0.0f;
			public readonly float RemoteSteerInput = 0.0f;
			public readonly float RemoteHandbrakeInput = 0.0f;
			public readonly float RemoteClutchInput = 0.0f;
			public readonly bool RemoteStartEngineInput = false;
			public readonly int RemoteTargetGear = 0;

			protected override void GetInput(out float throttleInput, out float brakeInput, out float steerInput, out float handbrakeInput, out float clutchInput, out bool startEngineInput, out int targetGear)
			{
				throttleInput = RemoteThrottleInput;
				brakeInput = RemoteBrakeInput;
				steerInput = RemoteSteerInput;
				handbrakeInput = RemoteHandbrakeInput;
				clutchInput = RemoteClutchInput;
				startEngineInput = RemoteStartEngineInput;
				targetGear = RemoteTargetGear;
			}
		}

		public float Steering
		{
			get => _dynamics.carController.steering;
			set => _dynamics.carController.steering = value;
		}

		public float Throttle
		{
			get => _dynamics.carController.throttleInput;
			set => _dynamics.carController.throttleInput = value;
		}

		public float Brake
		{
			get => _dynamics.carController.brakeInput;
			set => _dynamics.carController.brakeInput = value;
		}

		public float TargetSpeed
		{
			get => _throttleFsm.FsmVariables.GetFsmFloat("TargetSpeed").Value;
			set => _remoteTargetSpeed = value;
		}

		public int Waypoint => Convert.ToInt32(_navigationFsm.FsmVariables.GetFsmGameObject("Waypoint").Value.name);

		public GameObject WaypointSet
		{
			set => _navigationFsm.FsmVariables.GetFsmGameObject("Waypoint").Value = value;
		}

		public int Route
		{
			get
			{
				string route = _navigationFsm.FsmVariables.GetFsmGameObject("Waypoint").Value.transform.parent.name;
				if (route == "BusRoute")
				{
					return 0;
				}

				if (route == "DirtRoad")
				{
					return 1;
				}
				if (route == "Highway")
				{
					return 2;
				}
				if (route == "HomeRoad")
				{
					return 3;
				}
				if (route == "RoadRace")
				{
					return 4;
				}
				if (route == "Trackfield")
				{
					return 5;
				}
				return 6;
			}
		}

		public int WaypointStart
		{
			get => _navigationFsm.FsmVariables.GetFsmInt("WaypointStart").Value;
			set => _navigationFsm.FsmVariables.GetFsmInt("WaypointStart").Value = value;
		}

		public int WaypointEnd
		{
			get => _navigationFsm.FsmVariables.GetFsmInt("WaypointEnd").Value;
			set => _navigationFsm.FsmVariables.GetFsmInt("WaypointEnd").Value = value;
		}

		private float _remoteTargetSpeed;


		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="go"></param>
		public AiVehicle(GameObject go, ObjectSyncComponent osc)
		{
			_gameObject = go;
			_syncComponent = osc;
			_parentGameObject = go.transform.parent.gameObject;

			// Set vehicle type, used to apply vehicle-specific event hooks.
			string goName = _gameObject.transform.parent.gameObject.name;
			if (goName == "AMIS2" || goName == "KYLAJANI")
			{
				Type = VehicleTypes.Amis;
			}
			else if (goName == "BUS")
			{
				Type = VehicleTypes.Bus;
			}
			else if (goName == "FITTAN" && _parentGameObject.transform.FindChild("Navigation") != null)
			{
				Type = VehicleTypes.Fitan;
			}
			else if (_parentGameObject.transform.FindChild("NavigationCW") != null || _parentGameObject.transform.FindChild("NavigationCCW") != null)
			{
				Type = VehicleTypes.TrafficDirectional;
			}
			else
			{
				Type = VehicleTypes.Traffic;
			}

			_rigidbody = _parentGameObject.GetComponent<Rigidbody>();

			_dynamics = _parentGameObject.GetComponent<CarDynamics>();

			_throttleFsm = Utils.GetPlaymakerScriptByName(_parentGameObject, "Throttle");

			if (Type == VehicleTypes.TrafficDirectional)
			{
				if (_parentGameObject.transform.FindChild("NavigationCW") != null)
				{
					_navigationFsm = Utils.GetPlaymakerScriptByName(_parentGameObject.transform.FindChild("NavigationCW").gameObject, "Navigation");
					_isClockwise = 1;
				}
				else
				{
					_navigationFsm = Utils.GetPlaymakerScriptByName(_parentGameObject.transform.FindChild("NavigationCCW").gameObject, "Navigation");
					_isClockwise = 0;
				}
				_directionFsm = Utils.GetPlaymakerScriptByName(_parentGameObject, "Direction");
			}
			else
			{
				_navigationFsm = Utils.GetPlaymakerScriptByName(_parentGameObject.transform.FindChild("Navigation").gameObject, "Navigation");
			}

			EventHooks();
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
			return _parentGameObject.transform;
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
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync()
		{
			if (_rigidbody.velocity.sqrMagnitude >= 0.01f)
			{
				_isSyncing = true;
				return true;
			}

			_isSyncing = false;
			return false;
		}

		/// <summary>
		/// Called when a player enters range of an object.
		/// </summary>
		/// <returns>True if the player should tkae ownership of the object.</returns>
		public bool ShouldTakeOwnership()
		{
			return true;
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables)
		{
			if (_isSyncing)
			{
				float[] variables = { Steering, Throttle, Brake, TargetSpeed, Waypoint, Route, WaypointStart, WaypointEnd, _isClockwise };
				return variables;
			}

			return null;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables)
		{
			if (variables != null)
			{
				Steering = variables[0];
				Throttle = variables[1];
				Brake = variables[2];
				TargetSpeed = variables[3];
				WaypointSet = TrafficManager.GetWaypoint(variables[4], (int)variables[5]);
				WaypointStart = Convert.ToInt32(variables[6]);
				WaypointEnd = Convert.ToInt32(variables[7]);
				if (_isClockwise != variables[8])
				{
					_isClockwise = variables[8];
					if (_isClockwise == 1)
					{
						_navigationFsm = Utils.GetPlaymakerScriptByName(_parentGameObject.transform.FindChild("NavigationCW").gameObject, "Navigation");
					}
					else
					{
						_navigationFsm = Utils.GetPlaymakerScriptByName(_parentGameObject.transform.FindChild("NavigationCCW").gameObject, "Navigation");
					}
				}
			}
		}

		/// <summary>
		/// Called when sync control is taken by force.
		/// </summary>
		public void SyncTakenByForce()
		{

		}

		/// <summary>
		/// Called when owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote()
		{
			_gameObject.SetActive(true);
		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved()
		{

		}

		/// <summary>
		/// Called when an object is constantly syncing. (Usually when a pickupable is picked up, or when a vehicle is being driven)
		/// </summary>
		/// <param name="newValue">If object is being constantly synced.</param>
		public void ConstantSyncChanged(bool newValue)
		{

		}

		// Event hooks
		public void EventHooks()
		{
			// Generic vehicle FSMs.
			_throttleFsm = Utils.GetPlaymakerScriptByName(_parentGameObject, "Throttle");
			EventHook.SyncAllEvents(_throttleFsm, () =>
			{
				if (_syncComponent.Owner != Network.NetManager.Instance.GetLocalPlayer() && _syncComponent.Owner != null || _syncComponent.Owner == null && !Network.NetManager.Instance.IsHost)
				{
					return true;
				}
				return false;
			});

			// Traffic FSMs.
			EventHook.AddWithSync(_directionFsm, "CW", () =>
			{
				_isClockwise = 1;
				return false;
			});
			EventHook.AddWithSync(_directionFsm, "CCW", () =>
			{
				_isClockwise = 0;
				return false;
			});

			// Bus specific FSMs.
			if (Type == VehicleTypes.Bus)
			{
				PlayMakerFSM doorFsm = Utils.GetPlaymakerScriptByName(_parentGameObject.transform.FindChild("Route").gameObject, "Door");
				PlayMakerFSM startFsm = Utils.GetPlaymakerScriptByName(_parentGameObject.transform.FindChild("Route").gameObject, "Start");

				EventHook.SyncAllEvents(doorFsm, () =>
				{
					if (_syncComponent.Owner != Network.NetManager.Instance.GetLocalPlayer() && _syncComponent.Owner != null || _syncComponent.Owner == null && !Network.NetManager.Instance.IsHost)
					{
						return true;
					}
					return false;
				});

				EventHook.SyncAllEvents(startFsm, () =>
				{
					if (_syncComponent.Owner != Network.NetManager.Instance.GetLocalPlayer() && _syncComponent.Owner != null || _syncComponent.Owner == null && !Network.NetManager.Instance.IsHost)
					{
						return true;
					}
					return false;
				});
			}

			// None traffic cars specific FSMs.
			if (Type == VehicleTypes.Amis || Type == VehicleTypes.Fitan)
			{
				PlayMakerFSM crashFsm = Utils.GetPlaymakerScriptByName(_parentGameObject.transform.FindChild("CrashEvent").gameObject, "Crash");

				EventHook.SyncAllEvents(crashFsm, () =>
				{
					if (_syncComponent.Owner != Network.NetManager.Instance.GetLocalPlayer() && _syncComponent.Owner != null || _syncComponent.Owner == null && !Network.NetManager.Instance.IsHost)
					{
						return true;
					}
					return false;
				});
			}

			// Sync vehicle data with the host on spawn.
			if (Network.NetManager.Instance.IsOnline && !Network.NetManager.Instance.IsHost)
			{
				_syncComponent.RequestObjectSync();
			}
		}
	}
}
