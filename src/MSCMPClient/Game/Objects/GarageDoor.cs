using UnityEngine;

namespace MSCMP.Game.Objects
{
	/// <summary>
	/// Handles sync for the garage doors.
	/// </summary>
	internal class GarageDoor
		: ISyncedObject
	{
		private readonly GameObject _gameObject;
		private Rigidbody _rigidbody;
		private readonly HingeJoint _hinge;

		private float _lastRotation;

		/// <summary>
		/// Constructor.
		/// </summary>
		public GarageDoor(GameObject go)
		{
			_gameObject = go.transform.parent.gameObject;
			_hinge = _gameObject.GetComponent<HingeJoint>();
			_lastRotation = _hinge.angle;
			_rigidbody = _gameObject.GetComponent<Rigidbody>();

			HookEvents(go);
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
		/// Hook events.
		/// </summary>
		private void HookEvents(GameObject go)
		{
			PlayMakerFSM doorFsm = go.GetComponent<PlayMakerFSM>();
			EventHook.Add(doorFsm, "Open", () =>
			{
				go.GetComponent<Components.ObjectSyncComponent>().TakeSyncControl();
				return false;
			});
			EventHook.Add(doorFsm, "Close", () =>
			{
				go.GetComponent<Components.ObjectSyncComponent>().TakeSyncControl();
				return false;
			});
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
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync()
		{
			if (_lastRotation - _hinge.angle > 0.1 || _lastRotation - _hinge.angle < -0.1)
			{
				_lastRotation = _hinge.angle;
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
			return null;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables)
		{

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
