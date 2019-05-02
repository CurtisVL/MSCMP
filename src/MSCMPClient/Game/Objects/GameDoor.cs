using HutongGames.PlayMaker;
using UnityEngine;

namespace MSCMP.Game.Objects
{
	/// <summary>
	/// Game doors wrapper.
	/// </summary>
	internal class GameDoor
	{
		/// <summary>
		/// Game doors game obejct.
		/// </summary>
		private readonly GameObject _gameObject;

		/// <summary>
		/// Returns GameObject of the door.
		/// </summary>
		/// <returns>GameObject</returns>
		public GameObject GameObject => _gameObject;

		/// <summary>
		/// The owning object manager.
		/// </summary>
		private readonly GameDoorsManager _manager;

		/// <summary>
		/// Doors PlayMaker finite state machine.
		/// </summary>
		private readonly PlayMakerFSM _fsm;

		/// <summary>
		/// Are doors open?
		/// </summary>
		public bool IsOpen => _fsm.FsmVariables.FindFsmBool("DoorOpen").Value;

		/// <summary>
		/// Position of the doors in world.
		/// </summary>
		public Vector3 Position => _gameObject.transform.position;

		private const string OPEN_EVENT_NAME = "OPEN";
		private const string CLOSE_EVENT_NAME = "CLOSE";

		private const string MP_OPEN_EVENT_NAME = "MPOPEN";
		private const string MP_CLOSE_EVENT_NAME = "MPCLOSE";

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="manager">The manager that owns this door.</param>
		/// <param name="gameObject">Game object of the doors to represent by this wrapper.</param>
		public GameDoor(GameDoorsManager manager, GameObject gameObject)
		{
			_manager = manager;
			_gameObject = gameObject;

			_fsm = Utils.GetPlaymakerScriptByName(gameObject, "Use");
			if (_fsm.Fsm.HasEvent(MP_OPEN_EVENT_NAME))
			{
				Logger.Log("Failed to hook game door " + gameObject.name + ". It is already hooked.");
				return;
			}

			FsmEvent mpOpenEvent = _fsm.Fsm.GetEvent(MP_OPEN_EVENT_NAME);
			FsmEvent mpCloseEvent = _fsm.Fsm.GetEvent(MP_CLOSE_EVENT_NAME);

			PlayMakerUtils.AddNewGlobalTransition(_fsm, mpOpenEvent, "Open door");
			PlayMakerUtils.AddNewGlobalTransition(_fsm, mpCloseEvent, "Close door");

			PlayMakerUtils.AddNewAction(_fsm.Fsm.GetState("Open door"), new OnOpenDoorsAction(this));
			PlayMakerUtils.AddNewAction(_fsm.Fsm.GetState("Close door"), new OnCloseDoorsAction(this));
		}

		/// <summary>
		/// PlayMaker state action executed when doors are opened.
		/// </summary>
		private class OnOpenDoorsAction : FsmStateAction
		{
			private readonly GameDoor _gameDoor;

			public OnOpenDoorsAction(GameDoor door)
			{
				_gameDoor = door;
			}

			public override void OnEnter()
			{
				Finish();

				// If open was not triggered by local player do not send call the callback.

				if (State.Fsm.LastTransition.EventName != OPEN_EVENT_NAME)
				{
					return;
				}

				// Notify manager about the action.

				_gameDoor._manager.HandleDoorsAction(_gameDoor, true);
			}
		}

		/// <summary>
		/// PlayMaker state action executed when doors are closed.
		/// </summary>
		private class OnCloseDoorsAction : FsmStateAction
		{
			private readonly GameDoor _gameDoor;

			public OnCloseDoorsAction(GameDoor door)
			{
				_gameDoor = door;
			}

			public override void OnEnter()
			{
				Finish();

				// If close was not triggered by local player do not send call the callback.

				if (State.Fsm.LastTransition.EventName != CLOSE_EVENT_NAME)
				{
					return;
				}

				// Notify manager about the action.

				_gameDoor._manager.HandleDoorsAction(_gameDoor, false);

			}
		}

		/// <summary>
		/// Opens or closes the doors.
		/// </summary>
		/// <param name="open">Open or close?</param>
		public void Open(bool open)
		{
			if (open)
			{
				_fsm.SendEvent(MP_OPEN_EVENT_NAME);
			}
			else
			{
				_fsm.SendEvent(MP_CLOSE_EVENT_NAME);
			}
		}
	}
}
