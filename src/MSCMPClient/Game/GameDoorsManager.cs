using System.Collections.Generic;
using UnityEngine;
using MSCMP.Game.Objects;

namespace MSCMP.Game {
	/// <summary>
	/// Class managing state of the doors in game.
	/// </summary>
	class GameDoorsManager : IGameObjectCollector {

		/// <summary>
		/// Singleton of the doors manager.
		/// </summary>
		public static GameDoorsManager Instance = null;

		/// <summary>
		/// List of the doors.
		/// </summary>
		public List<GameDoor> doors = new List<GameDoor>();

		public delegate void OnDoorsOpen(GameObject door);
		public delegate void OnDoorsClose(GameObject door);

		/// <summary>
		/// Callback called when local player opens any doors.
		/// </summary>
		public OnDoorsOpen onDoorsOpen;

		/// <summary>
		/// Callback called when local players closes any doors.
		/// </summary>
		public OnDoorsClose onDoorsClose;

		public GameDoorsManager() {
			Instance = this;
		}

		~GameDoorsManager() {
			Instance = null;
		}

		/// <summary>
		/// Handle collected objects destroy.
		/// </summary>
		public void DestroyObjects() {
			doors.Clear();
		}

		/// <summary>
		/// Handle doors action.
		/// </summary>
		/// <param name="door">The doors that sent the action.</param>
		/// <param name="open">Is action door open or door close?</param>
		public void HandleDoorsAction(GameDoor door, bool open) {
			if (open) {
				onDoorsOpen?.Invoke(door.GameObject);
			}
			else {
				onDoorsClose?.Invoke(door.GameObject);
			}
		}


		/// <summary>
		/// Registers given gameObject as door if it's door.
		/// </summary>
		/// <param name="gameObject">The game object to check and eventually register.</param>
		public void CollectGameObject(GameObject gameObject) {
			if (!gameObject.name.StartsWith("Door")) {
				return;
			}


			if (gameObject.transform.childCount == 0) {
				return;
			}

			Transform pivot = gameObject.transform.GetChild(0);
			if (pivot == null || pivot.name != "Pivot") {
				return;
			}

			var playMakerFsm = Utils.GetPlaymakerScriptByName(gameObject, "Use");
			if (playMakerFsm == null) {
				return;
			}

			bool isValid = false;
			foreach (var e in playMakerFsm.FsmEvents) {
				if (e.Name == "OPENDOOR") {
					isValid = true;
					break;
				}
			}

			if (!isValid) {
				return;
			}

			GameDoor door = new GameDoor(this, gameObject);
			doors.Add(door);

			Logger.Debug("Registered doors " + gameObject.name);
		}

		/// <summary>
		/// Find doors at given world location.
		/// </summary>
		/// <param name="position">The location of the doors.</param>
		/// <returns></returns>
		public GameDoor FindGameDoors(Vector3 position) {
			foreach (var door in doors) {
				if (door.Position == position) {
					return door;
				}
			}
			return null;
		}
	}
}
