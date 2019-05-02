using System;
using UnityEngine;
using MSCMP.Game.Components;

namespace MSCMP.Game.Objects {
	class VehicleDoor : ISyncedObject {

		GameObject gameObject;
		Rigidbody rigidbody;
		ObjectSyncComponent osc;
		HingeJoint hinge;
		float lastRotation;

		public enum DoorTypes
		{
			Null,
			DriverDoor,
			RearDoor,
		}
		DoorTypes doorType = DoorTypes.Null;

		/// <summary>
		/// Constructor.
		/// </summary>
		public VehicleDoor(GameObject go, ObjectSyncComponent syncComponent) {
			gameObject = go;
			osc = syncComponent;

			// Rear door of van and vehicle trunks.
			if (go.name == "RearDoor" || go.name == "Bootlid") {
				doorType = DoorTypes.RearDoor;
				// Van, Old car.
				if (go.transform.FindChild("doorear")) {
					gameObject = go.transform.FindChild("doorear").gameObject;
				}
				// Ferndale.
				else {
					gameObject = go.transform.FindChild("Bootlid").gameObject;
				}
			}
			// Driver and passenger doors.
			else if (go.name == "doorl" || go.name == "doorr" || go.name == "door(leftx)" || go.name == "door(right)") {
				doorType = DoorTypes.DriverDoor;
			}

			hinge = gameObject.GetComponent<HingeJoint>();
			lastRotation = hinge.angle;
			rigidbody = gameObject.GetComponent<Rigidbody>();

			HookEvents();
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags flags() {
			return ObjectSyncManager.Flags.RotationOnly;
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
			return false;
		}

		/// <summary>
		/// Hook vehicle door related events.
		/// </summary>
		void HookEvents() {
			// Rear door on van and trunk of Satsuma.
			if (doorType == DoorTypes.RearDoor) {
				PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(gameObject, "Use");

				EventHook.AddWithSync(fsm, "Mouse off");
				EventHook.AddWithSync(fsm, "Open hood");
				EventHook.AddWithSync(fsm, "Close hood");

				EventHook.Add(fsm, "Mouse over", new Func<bool>(() => {
					osc.TakeSyncControl();
					return false;
				}));
			}
			// Driver and passenger doors of all vehicles.
			else if (doorType == DoorTypes.DriverDoor) {
				GameObject fsmGO = gameObject;
				// Van.
				if (gameObject.transform.FindChild("Handle")) {
					fsmGO = gameObject.transform.FindChild("Handle").gameObject;
				}
				// Ferndale, Old car.
				else if (gameObject.transform.FindChild("door/Handle")) {
					fsmGO = gameObject.transform.FindChild("door/Handle").gameObject;
				}
				// Truck, Tractor.
				else {
					fsmGO = gameObject;
				}

				PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(fsmGO, "Use");

				EventHook.AddWithSync(fsm, "Mouse off");
				EventHook.AddWithSync(fsm, "Open door");
				EventHook.AddWithSync(fsm, "Open door 2");
				EventHook.AddWithSync(fsm, "Close door");

				EventHook.Add(fsm, "Mouse over 1", new Func<bool>(() => {
					osc.TakeSyncControl();
					return false;
				}));
				EventHook.Add(fsm, "Mouse over 2", new Func<bool>(() => {
					osc.TakeSyncControl();
					return false;
				}));
			}
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync() {
			//Logger.Log("Current rotations, X: " + gameObject.transform.localRotation.x + ", Y: " + gameObject.transform.localRotation.y + ", Z: " + gameObject.transform.localRotation.z);
			if ((lastRotation - hinge.angle) > 0.1 || (lastRotation - hinge.angle) < -0.1) {
				if (osc.Owner == Network.NetManager.Instance.GetLocalPlayer()) {
					lastRotation = hinge.angle;
					return true;
				}
				return false;
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
			return true;
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables) {
			// Rear door variables.
			if (doorType == DoorTypes.RearDoor) {
				float[] variables = { hinge.limits.min, hinge.limits.max, rigidbody.velocity.y };
				return variables;
			}
			return null;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables) {
			// Set rear door variables.
			if (doorType == DoorTypes.RearDoor) {
				// Hinge limits.
				JointLimits limits = hinge.limits;
				limits.min = variables[0];
				limits.max = variables[1];
				hinge.limits = limits;

				// Door velocity.
				Vector3 velocityNew = rigidbody.velocity;
				velocityNew.y = variables[2];
				rigidbody.velocity = velocityNew;
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
