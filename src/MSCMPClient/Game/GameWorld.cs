using System.Collections.Generic;
using UnityEngine;
using MSCMP.Game.Objects;
using MSCMP.Game.Components;
using HutongGames.PlayMaker;
using System.Text;

namespace MSCMP.Game {

	/// <summary>
	/// Object managing state of the game world.
	/// </summary>
	class GameWorld : IGameObjectCollector {
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
		/// Setup red mailbox next to the player's home.
		/// </summary>
		public void SetupMailbox(GameObject mailboxGameObject) {
			lastnameTextMesh = mailboxGameObject.GetComponent<TextMesh>();
			lastnameFSM = mailboxGameObject.GetComponent<PlayMakerFSM>();
		}

		List<IGameObjectCollector> gameObjectUsers = new List<IGameObjectCollector>();

		public GameWorld() {
			Instance = this;

			gameObjectUsers.Add(this);
			gameObjectUsers.Add(doorsManager);
			gameObjectUsers.Add(gamePickupableDatabase);
			gameObjectUsers.Add(beerCaseManager);
			gameObjectUsers.Add(lightSwitchManager);
			gameObjectUsers.Add(gameWeatherManager);
		}

		~GameWorld() {
			Instance = null;
		}

		Dictionary<int, GameObject> gameObjectLibrary = new Dictionary<int, GameObject>();



		/// <summary>
		/// The current game world hash.
		/// </summary>
		int worldHash = 0;

		/// <summary>
		/// Was game world has already generated?
		/// </summary>
		bool worldHashGenerated = false;

		/// <summary>
		/// Collect given objects.
		/// </summary>
		/// <param name="gameObject">The game object to collect.</param>
		public void CollectGameObject(GameObject gameObject) {
			if (gameObject.name == "SUN") {
				// Yep it's called "Color" :>
				worldTimeFsm = Utils.GetPlaymakerScriptByName(gameObject, "Color");

				// Register refresh world time event.
				if (!worldTimeFsm.Fsm.HasEvent(REFRESH_WORLD_TIME_EVENT)) {
					FsmEvent mpRefreshWorldTimeEvent = worldTimeFsm.Fsm.GetEvent(REFRESH_WORLD_TIME_EVENT);
					PlayMakerUtils.AddNewGlobalTransition(worldTimeFsm, mpRefreshWorldTimeEvent, "State 1");
				}

				// Make sure world time is up-to-date with cache.
				WorldTime = worldTimeCached;
			}

			if (gameObject.transform.parent != null && gameObject.transform.parent.name == "mailbox_bottom_player" && gameObject.name == "Name") {
				SetupMailbox(gameObject);
			}

			if (IsVehicleGameObject(gameObject)) {
				vehicles.Add(new GameVehicle(gameObject));
			}
		}
		/// <summary>
		/// Handle collected objects destroy.
		/// </summary>
		public void DestroyObjects() {
			worldTimeFsm = null;
			vehicles.Clear();
		}

		/// <summary>
		/// Callback called when world is loaded.
		/// </summary>
		public void OnLoad() {
			// Register all game objects.

			GameObject[] gos = Resources.FindObjectsOfTypeAll<GameObject>();

			foreach (GameObject go in gos) {
				if (!worldHashGenerated) {
					Transform transform = go.transform;
					while (transform != null) {
						worldHash ^= Utils.StringJenkinsHash(transform.name);
						transform = transform.parent;
					}
				}

				foreach (IGameObjectCollector collector in gameObjectUsers) {
					collector.CollectGameObject(go);
				}
			}

			Logger.Log("World hash: " + worldHash);
			worldHashGenerated = true;

			// Check mandatory objects.

			Client.Assert(worldTimeFsm != null, "Now world time FSM found :(");
			Client.Assert(lastnameFSM != null, "Mailbox FSM couldn't be found!");
			Client.Assert(lastnameTextMesh != null, "Mailbox TextMesh couldn't be found!");

			// Notify different parts of the mod about the world load.

			if (GameCallbacks.onWorldLoad != null) {
				GameCallbacks.onWorldLoad();
			}
		}

		/// <summary>
		/// Callback called when world gets unloaded.
		/// </summary>
		public void OnUnload() {
			foreach (IGameObjectCollector collector in gameObjectUsers) {
				collector.DestroyObjects();
			}

			if (GameCallbacks.onWorldUnload != null) {
				GameCallbacks.onWorldUnload();
			}

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
		/// List of vehicle gameobject names.
		/// </summary>
		static readonly string[] vehicleGoNames = {
			"JONNEZ ES(Clone)", "HAYOSIKO(1500kg, 250)", "SATSUMA(557kg, 248)",
			"RCO_RUSCKO12(270)", "KEKMET(350-400psi)", "FLATBED", "FERNDALE(1630kg)", "GIFU(750/450psi)"
		};


		/// <summary>
		/// Check if given game object is vehicle.
		/// </summary>
		/// <param name="gameObject">The game object to check.</param>
		/// <returns>true if given game object is a vehicle, false otherwise</returns>
		bool IsVehicleGameObject(GameObject gameObject) {
			foreach (var name in vehicleGoNames) {
				if (gameObject.name == name) {
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Get game vehicle object by game object name.
		/// </summary>
		/// <param name="name">The name of the vehicle game object to look for game vehicle for.</param>
		/// <returns>The game vehicle object or null if there is no game vehicle matching this name.</returns>
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
