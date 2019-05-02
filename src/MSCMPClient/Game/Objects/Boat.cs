using UnityEngine;

namespace MSCMP.Game.Objects
{
	/// <summary>
	/// Handles syncing and events of the boat.
	/// </summary>
	internal class Boat 
		: ISyncedObject
	{
		private readonly GameObject _gameObject;
		private readonly GameObject _boatGo;
		private readonly Rigidbody _rigidbody;
		private readonly PlayMakerFSM _jankFsm;
		private readonly PlayMakerFSM _ignitionFsm;
		private readonly PlayMakerFSM _shutOffFsm;
		private readonly PlayMakerFSM _engineFsm;
		private readonly GameObject _engineGo;
		private readonly PlayMakerFSM _gearFsm;
		private readonly GameObject _motorGo;
		private readonly PlayMakerFSM _driveFsm;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="go">Boat GameObject.</param>
		public Boat(GameObject go)
		{
			_gameObject = go;
			_boatGo = go.transform.parent.parent.parent.gameObject;
			_rigidbody = _boatGo.GetComponent<Rigidbody>();

			PlayMakerFSM[] fsms = _boatGo.GetComponentsInChildren<PlayMakerFSM>();
			foreach (PlayMakerFSM fsm in fsms)
			{
				if (fsm.Fsm.Name == "Jank" && fsm.gameObject.name == "Ignition")
				{
					_jankFsm = fsm;
				}
				else if (fsm.Fsm.Name == "Use" && fsm.gameObject.name == "Ignition")
				{
					_ignitionFsm = fsm;
				}
				else if (fsm.Fsm.Name == "Use" && fsm.gameObject.name == "ShutOff")
				{
					_shutOffFsm = fsm;
				}
				else if (fsm.Fsm.Name == "Use" && fsm.gameObject.name == "Gear")
				{
					_gearFsm = fsm;
				}
				else if (fsm.Fsm.Name == "PlayerTrigger" && fsm.gameObject.name == "DriveTrigger")
				{
					_driveFsm = fsm;
				}
			}

			PlayMakerFSM[] allFsms = Resources.FindObjectsOfTypeAll<PlayMakerFSM>();
			bool foundOther = false;
			foreach (PlayMakerFSM fsm in allFsms)
			{
				if (fsm.Fsm.Name == "ThrottleSteer" && fsm.gameObject.name == "Controls")
				{
					if (foundOther)
					{
						break;
					}
					foundOther = true;
				}
				else if (fsm.Fsm.Name == "Simulation" && fsm.gameObject.name == "Engine")
				{
					_engineFsm = fsm;
					_engineGo = fsm.gameObject;
					if (foundOther)
					{
						break;
					}
					foundOther = true;
				}
			}

			_motorGo = _boatGo.transform.FindChild("GFX").FindChild("Motor").FindChild("Pivot").gameObject;

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
		/// Hook events related to the boat.
		/// </summary>
		private void HookEvents()
		{
			// J A N K - Yes, it's called that.
			EventHook.AddWithSync(_jankFsm, "State 1");
			EventHook.AddWithSync(_jankFsm, "Fail");
			EventHook.AddWithSync(_jankFsm, "Start");

			// Ignition
			EventHook.AddWithSync(_ignitionFsm, "State 1");

			// Shut Off
			EventHook.AddWithSync(_shutOffFsm, "Shut Off");

			// Gears
			EventHook.AddWithSync(_gearFsm, "First");
			EventHook.AddWithSync(_gearFsm, "Neutral");
			EventHook.AddWithSync(_gearFsm, "Reverse");

			// Enter as driver
			EventHook.Add(_driveFsm, "Player in car", () =>
			{
				_gameObject.GetComponent<Components.ObjectSyncComponent>().TakeSyncControl();
				return false;
			});
		}

		/// <summary>
		/// Get object's Transform.
		/// </summary>
		/// <returns>Object's Transform.</returns>
		public Transform ObjectTransform()
		{
			return _boatGo.transform;
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
				return true;
			}

			return false;
		}

		/// <summary>
		/// Called when a player enters range of an object.
		/// </summary>
		/// <returns>True if the player should try to take ownership of the object.</returns>
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
			if (!_engineGo.activeSelf)
			{
				_engineGo.SetActive(true);
			}
			float[] variables = {
				_engineFsm.FsmVariables.GetFsmFloat("Throttle").Value,
				_engineFsm.FsmVariables.GetFsmFloat("RPMmax").Value,
				_motorGo.transform.localRotation.y
			};
			return variables;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables)
		{
			if (!_engineGo.activeSelf)
			{
				_engineGo.SetActive(true);
			}
			_engineFsm.FsmVariables.GetFsmFloat("Throttle").Value = variables[0];
			_engineFsm.FsmVariables.GetFsmFloat("RPMmax").Value = variables[1];
			_motorGo.transform.localRotation = new Quaternion(_motorGo.transform.localRotation.x, variables[2], _motorGo.transform.localRotation.z, _motorGo.transform.localRotation.w);
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
