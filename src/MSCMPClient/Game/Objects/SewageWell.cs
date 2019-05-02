using UnityEngine;

namespace MSCMP.Game.Objects
{
	internal class SewageWell 
		: ISyncedObject
	{
		private readonly GameObject _gameObject;

		// Update rate for weather in frames.
		private readonly float _syncInterval = 150;
		private float _currentFrame;

		private readonly PlayMakerFSM _levelFsm;

		private enum WellStates
		{
			Full,
			WaitCall,
			Reset
		}

		private WellStates _currentState = WellStates.Reset;

		/// <summary>
		/// Constructor.
		/// </summary>
		public SewageWell(GameObject go, Components.ObjectSyncComponent syncComponent)
		{
			_gameObject = go;

			GameObject fsmGo = _gameObject.transform.FindChild("WasteWell_2000litre/Shit/Level/ShitLevelTrigger").gameObject;
			_levelFsm = Utils.GetPlaymakerScriptByName(fsmGo, "Level");

			if (Network.NetManager.Instance.IsHost)
			{
				syncComponent.TakeSyncControl();
			}

			HookEvents();
		}

		/// <summary>
		/// Hook events for this sewage well.
		/// </summary>
		private void HookEvents()
		{
			// Get currently active state.
			if (_levelFsm.Fsm.ActiveStateName == "Full")
			{
				_currentState = WellStates.Full;
			}
			else if (_levelFsm.Fsm.ActiveStateName == "Reset")
			{
				_currentState = WellStates.Reset;
			}
			if (_levelFsm.Fsm.ActiveStateName == "Wait call")
			{
				_currentState = WellStates.WaitCall;
			}

			// Hook events and sync them.
			EventHook.AddWithSync(_levelFsm, "Full", () =>
			{
				_currentState = WellStates.Full;
				return false;
			});
			EventHook.AddWithSync(_levelFsm, "Reset", () =>
			{
				_currentState = WellStates.Reset;
				return false;
			});
			EventHook.AddWithSync(_levelFsm, "Wait call", () =>
			{
				_currentState = WellStates.WaitCall;
				return false;
			});

			EventHook.AddWithSync(_levelFsm, "Pay");
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags Flags()
		{
			return ObjectSyncManager.Flags.VariablesOnly;
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
			return true;
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync()
		{
			// Only sync sewage wells as the host.
			if (!Network.NetManager.Instance.IsHost) return false;

			if (_currentFrame >= _syncInterval)
			{
				_currentFrame = 0;
				return true;
			}

			_currentFrame++;
			return false;

		}

		/// <summary>
		/// Called when a player enters range of an object.
		/// </summary>
		/// <returns>True if the player should try to take ownership of the object.</returns>
		public bool ShouldTakeOwnership()
		{
			return Network.NetManager.Instance.IsHost;
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables)
		{
			float called = 0;
			if (_levelFsm.Fsm.GetFsmBool("Called").Value)
			{
				called = 1;
			}
			float[] variables = { _levelFsm.Fsm.GetFsmFloat("ShitLevel").Value, (float)_currentState, called };
			return variables;
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables)
		{
			// Shit level.
			_levelFsm.Fsm.GetFsmFloat("ShitLevel").Value = variables[0];

			// Current well state.
			if (_currentState != (WellStates)variables[1])
			{
				switch ((WellStates)variables[1])
				{
					case WellStates.Full:
						_levelFsm.SendEvent("MP_Full");
						break;
					case WellStates.Reset:
						_levelFsm.SendEvent("MP_Reset");
						break;
					case WellStates.WaitCall:
						_levelFsm.SendEvent("MP_Wait call");
						break;
				}
			}

			// If the house has been called.
			if (variables[1] == 1)
			{
				_levelFsm.Fsm.GetFsmBool("Called").Value = true;
			}
			else
			{
				_levelFsm.Fsm.GetFsmBool("Called").Value = false;
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
