using HutongGames.PlayMaker;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Game.Objects {

	/// <summary>
	/// Game doors wrapper.
	/// </summary>
	class GameDoor {
		/// <summary>
		/// Game doors game obejct.
		/// </summary>
		GameObject gameObject = null;

		/// <summary>
		/// Returns GameObject of the door.
		/// </summary>
		/// <returns>GameObject</returns>
		public GameObject GameObject {
			get {
				return gameObject;
			}
		}

		/// <summary>
		/// The owning object manager.
		/// </summary>
		GameDoorsManager manager = null;

		/// <summary>
		/// Doors PlayMaker finite state machine.
		/// </summary>
		PlayMakerFSM fsm = null;

		/// <summary>
		/// Are doors open?
		/// </summary>
		public bool IsOpen
		{
			get {
				return fsm.FsmVariables.FindFsmBool("DoorOpen").Value;
			}
		}

		/// <summary>
		/// Position of the doors in world.
		/// </summary>
		public Vector3 Position {
			get {
				return gameObject.transform.position;
			}
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="manager">The manager that owns this door.</param>
		/// <param name="gameObject">Game object of the doors to represent by this wrapper.</param>
		public GameDoor(GameDoorsManager manager, GameObject gameObject) {
			this.manager = manager;
			this.gameObject = gameObject;

			fsm = Utils.GetPlaymakerScriptByName(gameObject, "Use");
			if (fsm.Fsm.HasEvent("MP_Open door")) {
				Logger.Log("Failed to hook game door " + gameObject.name + ". It is already hooked.");
				return;
			}

			EventHook.AddWithSync(fsm, "Open door");
			EventHook.AddWithSync(fsm, "Close door");
		}

		/// <summary>
		/// Opens or closes the doors.
		/// </summary>
		/// <param name="open">Open or close?</param>
		public void Open(bool open) {
			if (open) {
				fsm.SendEvent("MP_Open door");
			}
			else {
				fsm.SendEvent("MP_Close door");
			}
		}
	}
}
