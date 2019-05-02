using MSCMP.Game.Components;
using UnityEngine;

namespace MSCMP.Game.Objects
{
	internal class VehicleDoor 
		: ISyncedObject
	{
		private readonly GameObject _gameObject;
		private readonly Rigidbody _rigidbody;
		private readonly ObjectSyncComponent _osc;
		private readonly HingeJoint _hinge;
		private float _lastRotation;

		public enum DoorTypes
		{
			Null,
			DriverDoor,
			RearDoor,
		}

		private readonly DoorTypes _doorType = DoorTypes.Null;

		/// <summary>
		/// Constructor.
		/// </summary>
		public VehicleDoor(GameObject go, ObjectSyncComponent syncComponent)
		{
			_gameObject = go;
			_osc = syncComponent;

			// Rear door of van and vehicle trunks.
			if (go.name == "RearDoor" || go.name == "Bootlid")
			{
				_doorType = DoorTypes.RearDoor;
				// Van, Old car.
				if (go.transform.FindChild("doorear"))
				{
					_gameObject = go.transform.FindChild("doorear").gameObject;
				}
				// Ferndale.
				else
				{
					_gameObject = go.transform.FindChild("Bootlid").gameObject;
				}
			}
			// Driver and passenger doors.
			else if (go.name == "doorl" || go.name == "doorr" || go.name == "door(leftx)" || go.name == "door(right)")
			{
				_doorType = DoorTypes.DriverDoor;
			}

			_hinge = _gameObject.GetComponent<HingeJoint>();
			_lastRotation = _hinge.angle;
			_rigidbody = _gameObject.GetComponent<Rigidbody>();

			HookEvents();
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags Flags()
		{
			return ObjectSyncManager.Flags.RotationOnly;
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
		/// Check is periodic sync of the object is enabled.
		/// </summary>
		/// <returns>Periodic sync enabled or disabled.</returns>
		public bool PeriodicSyncEnabled()
		{
			return false;
		}

		/// <summary>
		/// Hook vehicle door related events.
		/// </summary>
		private void HookEvents()
		{
			// Rear door on van and trunk of Satsuma.
			if (_doorType == DoorTypes.RearDoor)
			{
				PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(_gameObject, "Use");

				EventHook.AddWithSync(fsm, "Mouse off");
				EventHook.AddWithSync(fsm, "Open hood");
				EventHook.AddWithSync(fsm, "Close hood");

				EventHook.Add(fsm, "Mouse over", () =>
				{
					_osc.TakeSyncControl();
					return false;
				});
			}
			// Driver and passenger doors of all vehicles.
			else if (_doorType == DoorTypes.DriverDoor)
			{
				GameObject fsmGo = _gameObject;
				// Van.
				if (_gameObject.transform.FindChild("Handle"))
				{
					fsmGo = _gameObject.transform.FindChild("Handle").gameObject;
				}
				// Ferndale, Old car.
				else if (_gameObject.transform.FindChild("door/Handle"))
				{
					fsmGo = _gameObject.transform.FindChild("door/Handle").gameObject;
				}
				// Truck, Tractor.
				else
				{
					fsmGo = _gameObject;
				}

				PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(fsmGo, "Use");

				EventHook.AddWithSync(fsm, "Mouse off");
				EventHook.AddWithSync(fsm, "Open door");
				EventHook.AddWithSync(fsm, "Open door 2");
				EventHook.AddWithSync(fsm, "Close door");

				EventHook.Add(fsm, "Mouse over 1", () =>
				{
					_osc.TakeSyncControl();
					return false;
				});
				EventHook.Add(fsm, "Mouse over 2", () =>
				{
					_osc.TakeSyncControl();
					return false;
				});
			}
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync()
		{
			//Logger.Log("Current rotations, X: " + gameObject.transform.localRotation.x + ", Y: " + gameObject.transform.localRotation.y + ", Z: " + gameObject.transform.localRotation.z);
			if (_lastRotation - _hinge.angle > 0.1 || _lastRotation - _hinge.angle < -0.1)
			{
				if (_osc.Owner == Network.NetManager.Instance.GetLocalPlayer())
				{
					_lastRotation = _hinge.angle;
					return true;
				}
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
			return true;
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables)
		{
			// Rear door variables.
			if (_doorType == DoorTypes.RearDoor)
			{
				float[] variables = { _hinge.limits.min, _hinge.limits.max, _rigidbody.velocity.y };
				return variables;
			}
			return null;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables)
		{
			// Set rear door variables.
			if (_doorType == DoorTypes.RearDoor)
			{
				// Hinge limits.
				JointLimits limits = _hinge.limits;
				limits.min = variables[0];
				limits.max = variables[1];
				_hinge.limits = limits;

				// Door velocity.
				Vector3 velocityNew = _rigidbody.velocity;
				velocityNew.y = variables[2];
				_rigidbody.velocity = velocityNew;
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
