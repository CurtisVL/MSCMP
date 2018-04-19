﻿using System.Collections.Generic;
using UnityEngine;
using MSCMP.Game.Objects;
using HutongGames.PlayMaker;

namespace MSCMP.Game {

	/// <summary>
	/// Object managing state of the game world.
	/// </summary>
	class GameWorld {


		public static GameWorld Instance = null;

		/// <summary>
		/// Doors manager.
		/// </summary>
		private GameDoorsManager doorsManager = new GameDoorsManager();

		/// <summary>
		/// List containing game vehicles.
		/// </summary>
		private List<GameVehicle> vehicles = new List<GameVehicle>();

		/// <summary>
		/// Game animations database.
		/// </summary>
		GameAnimDatabase gameAnimDatabase = new GameAnimDatabase();

		/// <summary>
		/// Game pickupables database.
		/// </summary>
		GamePickupableDatabase gamePickupableDatabase = new GamePickupableDatabase();

		/// <summary>
		/// World time managing fsm.
		/// </summary>
		PlayMakerFSM worldTimeFsm = null;

		/// <summary>
		/// Beer case manager.
		/// </summary>
		BeerCaseManager beerCaseManager = new BeerCaseManager();

		/// <summary>
		/// Light switch manager.
		/// </summary>
		LightSwitchManager lightSwitchManager = new LightSwitchManager();

		/// <summary>
		/// Weather manager.
		/// </summary>
		GameWeatherManager gameWeatherManager = new GameWeatherManager();

		private GamePlayer player = null;

		/// <summary>
		/// Get player game object.
		/// </summary>
		public GamePlayer Player {
			get {
				return player;
			}
		}

		private const string REFRESH_WORLD_TIME_EVENT = "MP_REFRESH_WORLD_TIME";

		float worldTimeCached = 0;

		/// <summary>
		/// Current world time. (hh.mm)
		/// </summary>
		public float WorldTime {
			set {
				// Make sure value is reasonable. (0 - 24 range)

				while (value > 24.0f) {
					value -= 24.0f;
				}

				// Make sure reported time is power of two..
				worldTimeCached = (float)((int)(value) / 2 * 2);

				if (worldTimeCached <= 2.0f) {
					worldTimeCached = 2.0f;
				}

				if (worldTimeFsm != null) {
					worldTimeFsm.Fsm.GetFsmInt("Time").Value = (int)worldTimeCached;
					worldTimeFsm.SendEvent(REFRESH_WORLD_TIME_EVENT);
				}
			}

			get {
				if (worldTimeFsm != null) {
					worldTimeCached = worldTimeFsm.Fsm.GetFsmInt("Time").Value;
				}
				return worldTimeCached;
			}
		}

		/// <summary>
		/// Current world day.
		/// </summary>
		public int WorldDay {
			get {
				return PlayMakerGlobals.Instance.Variables.GetFsmInt("GlobalDay").Value;
			}

			set {
				PlayMakerGlobals.Instance.Variables.GetFsmInt("GlobalDay").Value = value;
			}
		}

		/// <summary>
		/// Current Host in game last name.
		/// </summary>
		public string PlayerLastName {
			get {
				return lastnameTextMesh.text;
			}

			set {
				lastnameFSM.enabled = false;
				lastnameTextMesh.text = value;
			}
		}

		private TextMesh lastnameTextMesh = null;
		private PlayMakerFSM lastnameFSM = null;

		/// <summary>
		/// Search for the red mailbox next to the player's home.
		/// </summary>
		public void LoadMailbox() {
			GameObject mailboxGameObject = GameObject.Find("YARD/PlayerMailBox/mailbox_bottom_player/Name");
			lastnameTextMesh = mailboxGameObject.GetComponent<TextMesh>();
			lastnameFSM = mailboxGameObject.GetComponent<PlayMakerFSM>();

			Client.Assert(lastnameFSM != null, "Mailbox FSM couldn't be found!");
			Client.Assert(lastnameTextMesh != null, "Mailbox TextMesh couldn't be found!");
		}

		public GameWorld() {
			Instance = this;
		}

		~GameWorld() {
			Instance = null;
		}


		int StringJenkinsHash(string str) {
			int i = 0;
			int hash = 0;
			while (i != str.Length) {
				hash += str[i++];
				hash += hash << 10;
				hash ^= hash >> 6;
			 }
			hash += hash << 3;
			hash ^= hash >> 11;
			hash += hash << 15;
			return hash;
		}

		int worldHash = 0;
		bool worldHashGenerated = false;
		/// <summary>
		/// Callback called when world is loaded.
		/// </summary>
		public void OnLoad() {

			GameObject[] gos = Resources.FindObjectsOfTypeAll<GameObject>();

			foreach (GameObject go in gos) {
				if (!worldHashGenerated) {
					Transform transform = go.transform;
					while (transform != null) {
						worldHash ^= StringJenkinsHash(transform.name);
						transform = transform.parent;
					}
				}
			}

			Logger.Log("World hash: " + worldHash);
			worldHashGenerated = true;

			// Cache world time management fsm.
			GameObject sunGameObject = GameObject.Find("SUN");
			Client.Assert(sunGameObject != null, "SUN game object is missing!");

			// Yep it's called "Color" :>
			worldTimeFsm = Utils.GetPlaymakerScriptByName(sunGameObject, "Color");
			Client.Assert(worldTimeFsm != null, "Now world time FSM found :(");

			// Register refresh world time event.
			if (!worldTimeFsm.Fsm.HasEvent(REFRESH_WORLD_TIME_EVENT)) {
				FsmEvent mpRefreshWorldTimeEvent = worldTimeFsm.Fsm.GetEvent(REFRESH_WORLD_TIME_EVENT);
				PlayMakerUtils.AddNewGlobalTransition(worldTimeFsm, mpRefreshWorldTimeEvent, "State 1");
			}

			// Make sure world time is up-to-date with cache.
			WorldTime = worldTimeCached;

			gameAnimDatabase.Rebuild();
			gamePickupableDatabase.Rebuild();

			doorsManager.OnWorldLoad();
			beerCaseManager.OnWorldLoad();
			lightSwitchManager.OnWorldLoad();
			LoadMailbox();
			LoadVehicles();

			if (GameCallbacks.onWorldLoad != null) {
				GameCallbacks.onWorldLoad();
			}
		}

		/// <summary>
		/// Callback called when world gets unloaded.
		/// </summary>
		public void OnUnload() {
			worldTimeFsm = null;

			if (GameCallbacks.onWorldUnload != null) {
				GameCallbacks.onWorldUnload();
			}

			vehicles.Clear();
			player = null;
		}

		/// <summary>
		/// Update game world state.
		/// </summary>
		public void Update() {
			if (player == null) {
				var playerGo = GameObject.Find("PLAYER");

				if (playerGo != null) {
					player = new GamePlayer(playerGo);

					if (GameCallbacks.onLocalPlayerCreated != null) {
						GameCallbacks.onLocalPlayerCreated();
					}
				}
			}
		}

		/// <summary>
		/// Load game vehicles and create game objects for them.
		/// </summary>
		private void LoadVehicles() {
			vehicles.Clear();

			// Register all vehicles.

			vehicles.Add(new GameVehicle(GameObject.Find("JONNEZ ES(Clone)")));
			vehicles.Add(new GameVehicle(GameObject.Find("HAYOSIKO(1500kg, 250)")));
			vehicles.Add(new GameVehicle(GameObject.Find("SATSUMA(557kg, 248)")));
			vehicles.Add(new GameVehicle(GameObject.Find("RCO_RUSCKO12(270)")));
			vehicles.Add(new GameVehicle(GameObject.Find("KEKMET(350-400psi)")));
			vehicles.Add(new GameVehicle(GameObject.Find("FLATBED")));
			vehicles.Add(new GameVehicle(GameObject.Find("FERNDALE(1630kg)")));
			vehicles.Add(new GameVehicle(GameObject.Find("GIFU(750/450psi)")));
		}

		public GameVehicle FindVehicleByName(string name) {
			foreach (var veh in vehicles) {
				if (veh.Name == name) {
					return veh;
				}
			}
			return null;
		}

		public void UpdateIMGUI() {
			// noop
		}

		/// <summary>
		/// Spawns pickupable.
		/// </summary>
		/// <param name="prefabId">Pickupable prefab id.</param>
		/// <param name="position">The spawn position.</param>
		/// <param name="rotation">The spawn rotation.</param>
		/// <returns>Spawned pickupable game object.</returns>
		public GameObject SpawnPickupable(int prefabId, Vector3 position, Quaternion rotation) {
			GamePickupableDatabase.PrefabDesc prefabDescriptor = gamePickupableDatabase.GetPickupablePrefab(prefabId);
			Client.Assert(prefabDescriptor != null, $"Unable to find pickupable prefab {prefabId}");
			return prefabDescriptor.Spawn(position, rotation);
		}
	}
}
