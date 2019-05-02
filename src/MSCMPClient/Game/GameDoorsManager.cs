using HutongGames.PlayMaker;
using MSCMP.Game.Objects;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Class managing state of the doors in game.
	/// </summary>
	internal class GameDoorsManager 
		: IGameObjectCollector
	{
		/// <summary>
		/// Singleton of the doors manager.
		/// </summary>
		public static GameDoorsManager Instance;

		/// <summary>
		/// List of the doors.
		/// </summary>
		public readonly List<GameDoor> Doors = new List<GameDoor>();

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

		public GameDoorsManager()
		{
			Instance = this;
		}

		~GameDoorsManager()
		{
			Instance = null;
		}

		/// <summary>
		/// Handle collected objects destroy.
		/// </summary>
		public void DestroyObjects()
		{
			Doors.Clear();
		}

		/// <summary>
		/// Handle doors action.
		/// </summary>
		/// <param name="door">The doors that sent the action.</param>
		/// <param name="open">Is action door open or door close?</param>
		public void HandleDoorsAction(GameDoor door, bool open)
		{
			if (open)
			{
				onDoorsOpen?.Invoke(door.GameObject);
			}
			else
			{
				onDoorsClose?.Invoke(door.GameObject);
			}
		}

		/// <summary>
		/// Check if given game object is a door.
		/// </summary>
		/// <param name="gameObject">The game object to check.</param>
		/// <returns>true if game object is a door, false otherwise</returns>
		private bool IsDoorGameObject(GameObject gameObject)
		{
			if (!gameObject.name.StartsWith("Door"))
			{
				return false;
			}

			if (gameObject.transform.childCount == 0)
			{
				return false;
			}

			Transform pivot = gameObject.transform.GetChild(0);
			if (pivot == null || pivot.name != "Pivot")
			{
				return false;
			}

			PlayMakerFSM playMakerFsm = Utils.GetPlaymakerScriptByName(gameObject, "Use");
			if (playMakerFsm == null)
			{
				return false;
			}

			bool isValid = false;
			foreach (FsmEvent e in playMakerFsm.FsmEvents)
			{
				if (e.Name == "OPENDOOR")
				{
					isValid = true;
					break;
				}
			}

			return isValid;
		}

		/// <summary>
		/// Registers given gameObject as door if it's door.
		/// </summary>
		/// <param name="gameObject">The game object to check and eventually register.</param>
		public void CollectGameObject(GameObject gameObject)
		{
			if (!IsDoorGameObject(gameObject) || GetDoorByGameObject(gameObject) != null) return;

			GameDoor door = new GameDoor(this, gameObject);
			Doors.Add(door);

			if (Network.NetWorld.DisplayObjectRegisteringDebug)
			{
				Logger.Debug("Registered doors " + gameObject.name);
			}
		}

		/// <summary>
		/// Handle destroy of game object.
		/// </summary>
		/// <param name="gameObject">The destroyed game object.</param>
		public void DestroyObject(GameObject gameObject)
		{
			if (!IsDoorGameObject(gameObject))
			{
				return;
			}

			GameDoor door = GetDoorByGameObject(gameObject);
			if (door != null)
			{
				Doors.Remove(door);
			}
		}


		/// <summary>
		/// Find doors at given world location.
		/// </summary>
		/// <param name="position">The location of the doors.</param>
		/// <returns></returns>
		public GameDoor FindGameDoors(Vector3 position)
		{
			foreach (GameDoor door in Doors)
			{
				if (door.Position == position)
				{
					return door;
				}
			}
			return null;
		}

		/// <summary>
		/// Get game door by game object.
		/// </summary>
		/// <param name="gameObject">The game object.</param>
		/// <returns>Game door instance or null if given game object is not a door.</returns>
		public GameDoor GetDoorByGameObject(GameObject gameObject)
		{
			foreach (GameDoor door in Doors)
			{
				if (door.GameObject == gameObject)
				{
					return door;
				}
			}
			return null;
		}
	}
}
