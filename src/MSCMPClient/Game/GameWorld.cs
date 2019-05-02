using HutongGames.PlayMaker;
using MSCMP.Game.Components;
using MSCMP.Game.Objects;
using MSCMP.Game.Places;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Game
{

	/// <summary>
	/// Object managing state of the game world.
	/// </summary>
	internal class GameWorld 
		: IGameObjectCollector
	{
		public static GameWorld Instance;

		/// <summary>
		/// Event hook.
		/// </summary>
		private EventHook _eventHook = new EventHook();

		/// <summary>
		/// Doors manager.
		/// </summary>
		private readonly GameDoorsManager _doorsManager = new GameDoorsManager();

		/// <summary>
		/// Game pickupables database.
		/// </summary>
		private readonly GamePickupableDatabase _gamePickupableDatabase = new GamePickupableDatabase();

		/// <summary>
		/// World time managing fsm.
		/// </summary>
		private PlayMakerFSM _worldTimeFsm;

		/// <summary>
		/// Light switch manager.
		/// </summary>
		private readonly LightSwitchManager _lightSwitchManager = new LightSwitchManager();

		/// <summary>
		/// Game vehicle database.
		/// </summary>
		private readonly GameVehicleDatabase _gameVehicleDatabase = new GameVehicleDatabase();

		/// <summary>
		/// Object sync manager.
		/// </summary>
		private ObjectSyncManager _objectSyncManager = new ObjectSyncManager();

		/// <summary>
		/// Traffic manager.
		/// </summary>
		private readonly TrafficManager _trafficManager = new TrafficManager();

		/// <summary>
		/// Shop manager.
		/// </summary>
		private readonly Shop _shopManager = new Shop();

		/// <summary>
		/// Phone manager.
		/// </summary>
		private readonly PhoneManager _phoneManager = new PhoneManager();

		/// <summary>
		/// Map manager.
		/// </summary>
		private readonly MapManager _mapManager = new MapManager();

		private GamePlayer _player;

		/// <summary>
		/// Get player game object.
		/// </summary>
		public GamePlayer Player => _player;

		private const string REFRESH_WORLD_TIME_EVENT = "MP_REFRESH_WORLD_TIME";

		private float _worldTimeCached;

		/// <summary>
		/// Current world time. (hh.mm)
		/// </summary>
		public float WorldTime
		{
			set
			{
				// Make sure value is reasonable. (0 - 24 range)

				while (value > 24.0f)
				{
					value -= 24.0f;
				}

				// Make sure reported time is power of two..
				_worldTimeCached = (float)((int)value / 2 * 2);

				if (_worldTimeCached <= 2.0f)
				{
					_worldTimeCached = 2.0f;
				}

				if (_worldTimeFsm != null)
				{
					_worldTimeFsm.Fsm.GetFsmInt("Time").Value = (int)_worldTimeCached;
					_worldTimeFsm.SendEvent(REFRESH_WORLD_TIME_EVENT);
				}
			}

			get
			{
				if (_worldTimeFsm != null)
				{
					_worldTimeCached = _worldTimeFsm.Fsm.GetFsmInt("Time").Value;
				}
				return _worldTimeCached;
			}
		}

		/// <summary>
		/// Current world day.
		/// </summary>
		public int WorldDay
		{
			get => PlayMakerGlobals.Instance.Variables.GetFsmInt("GlobalDay").Value;

			set => PlayMakerGlobals.Instance.Variables.GetFsmInt("GlobalDay").Value = value;
		}

		/// <summary>
		/// Current Host in game last name.
		/// </summary>
		public string PlayerLastName
		{
			get => _lastnameTextMesh.text;

			set
			{
				_lastnameFsm.enabled = false;
				_lastnameTextMesh.text = value;
			}
		}

		private TextMesh _lastnameTextMesh;
		private PlayMakerFSM _lastnameFsm;

		/// <summary>
		/// Setup red mailbox next to the player's home.
		/// </summary>
		public void SetupMailbox(GameObject mailboxGameObject)
		{
			_lastnameTextMesh = mailboxGameObject.GetComponent<TextMesh>();
			_lastnameFsm = mailboxGameObject.GetComponent<PlayMakerFSM>();
		}

		/// <summary>
		/// List containing all game objects collectors.
		/// </summary>
		private readonly List<IGameObjectCollector> _gameObjectUsers = new List<IGameObjectCollector>();

		public GameWorld()
		{
			Instance = this;

			// Make sure game world will get notified about play maker CreateObject/DestroyObject calls.

			GameCallbacks.onPlayMakerObjectCreate += (instance, prefab) =>
			{
				HandleNewObject(instance);
			};
			GameCallbacks.onPlayMakerObjectDestroy += HandleObjectDestroy;

			// Register game objects users.

			_gameObjectUsers.Add(this);
			_gameObjectUsers.Add(_doorsManager);
			_gameObjectUsers.Add(_gamePickupableDatabase);
			_gameObjectUsers.Add(_lightSwitchManager);
			_gameObjectUsers.Add(_gameVehicleDatabase);
		}

		~GameWorld()
		{
			Instance = null;
		}

		/// <summary>
		/// The current game world hash.
		/// </summary>
		private int _worldHash;

		/// <summary>
		/// Get unique world hash.
		/// </summary>
		public int WorldHash => _worldHash;

		/// <summary>
		/// Was game world has already generated?
		/// </summary>
		private bool _worldHashGenerated;

		/// <summary>
		/// Collect given objects.
		/// </summary>
		/// <param name="gameObject">The game object to collect.</param>
		public void CollectGameObject(GameObject gameObject)
		{
			if (gameObject.name == "SUN" && _worldTimeFsm == null)
			{
				// Yep it's called "Color" :>
				_worldTimeFsm = Utils.GetPlaymakerScriptByName(gameObject, "Color");
				if (_worldTimeFsm == null)
				{
					return;
				}

				// Register refresh world time event.
				if (!_worldTimeFsm.Fsm.HasEvent(REFRESH_WORLD_TIME_EVENT))
				{
					FsmEvent mpRefreshWorldTimeEvent = _worldTimeFsm.Fsm.GetEvent(REFRESH_WORLD_TIME_EVENT);
					PlayMakerUtils.AddNewGlobalTransition(_worldTimeFsm, mpRefreshWorldTimeEvent, "State 1");
				}

				// Make sure world time is up-to-date with cache.
				WorldTime = _worldTimeCached;
			}
			else if (Utils.IsGameObjectHierarchyMatching(gameObject, "mailbox_bottom_player/Name"))
			{
				SetupMailbox(gameObject);
			}
			else if (gameObject.name == "TRAFFIC")
			{
				_trafficManager.Setup(gameObject);
			}
			else if (gameObject.name == "STORE")
			{
				_shopManager.Setup(gameObject);
			}
			else if (gameObject.name == "BOAT")
			{
				ObjectSyncComponent osc = gameObject.transform.FindChild("GFX/Colliders/Collider").gameObject.AddComponent<ObjectSyncComponent>();
				osc.Setup(ObjectSyncManager.ObjectTypes.Boat, ObjectSyncManager.AutomaticId);
			}

			// Garage doors.
			else if (gameObject.name == "GarageDoors")
			{
				ObjectSyncComponent oscLeft = gameObject.transform.FindChild("DoorLeft/Coll").gameObject.AddComponent<ObjectSyncComponent>();
				oscLeft.Setup(ObjectSyncManager.ObjectTypes.GarageDoor, ObjectSyncManager.AutomaticId);
				ObjectSyncComponent oscRight = gameObject.transform.FindChild("DoorRight/Coll").gameObject.AddComponent<ObjectSyncComponent>();
				oscRight.Setup(ObjectSyncManager.ObjectTypes.GarageDoor, ObjectSyncManager.AutomaticId);
			}
			// Old car shed doors.
			else if (gameObject.name == "Doors" && gameObject.transform.parent.name == "Shed")
			{
				PlayMakerFSM doorLeft = gameObject.transform.FindChild("DoorLeft/Mesh").gameObject.GetComponent<PlayMakerFSM>();
				EventHook.AddWithSync(doorLeft, "Open door");
				EventHook.AddWithSync(doorLeft, "Close door");
				PlayMakerFSM doorRight = gameObject.transform.FindChild("DoorRight/Mesh").gameObject.GetComponent<PlayMakerFSM>();
				EventHook.AddWithSync(doorRight, "Open door");
				EventHook.AddWithSync(doorRight, "Close door");
			}

			// Weather system.
			else if (gameObject.name == "Clouds" && gameObject.transform.parent.name == "CloudSystem")
			{
				ObjectSyncComponent osc = gameObject.AddComponent<ObjectSyncComponent>();
				osc.Setup(ObjectSyncManager.ObjectTypes.Weather, ObjectSyncManager.AutomaticId);
			}

			// Sewage well jobs.
			else if (gameObject.name.StartsWith("HouseShit"))
			{
				ObjectSyncComponent osc = gameObject.AddComponent<ObjectSyncComponent>();
				osc.Setup(ObjectSyncManager.ObjectTypes.SewageWell, ObjectSyncManager.AutomaticId);
			}

			// Phone.
			else if (gameObject.name == "Ring")
			{
				_phoneManager.Setup(gameObject);
			}
			// Map.
			else if (gameObject.name == "MAP" && gameObject.transform.FindChild("Darts"))
			{
				_mapManager.Setup(gameObject);
			}
		}

		/// <summary>
		/// Handle collected objects destroy.
		/// </summary>
		public void DestroyObjects()
		{
			_worldTimeFsm = null;
			_lastnameTextMesh = null;
			_lastnameFsm = null;
		}

		/// <summary>
		/// Handle destroy of game object.
		/// </summary>
		/// <param name="gameObject">The destroyed game object.</param>
		public void DestroyObject(GameObject gameObject)
		{
			if (_worldTimeFsm != null && _worldTimeFsm.gameObject == gameObject)
			{
				_worldTimeFsm = null;
			}
			else if (_lastnameFsm != null && _lastnameFsm.gameObject == gameObject)
			{
				_lastnameFsm = null;
				_lastnameTextMesh = null;
			}
		}

		/// <summary>
		/// Handle creation/load of new game object.
		/// </summary>
		/// <param name="gameObject">The new game object.</param>
		private void HandleNewObject(GameObject gameObject)
		{
			foreach (IGameObjectCollector collector in _gameObjectUsers)
			{
				collector.CollectGameObject(gameObject);
			}
		}

		/// <summary>
		/// Handle destroy of the given object.
		/// </summary>
		/// <param name="gameObject">Destroyed game object.</param>
		private void HandleObjectDestroy(GameObject gameObject)
		{
			// Iterate backwards so pickupable users will be notified before the database.

			for (int i = _gameObjectUsers.Count; i > 0; --i)
			{
				_gameObjectUsers[i - 1].DestroyObject(gameObject);
			}
		}

		/// <summary>
		/// Callback called when world is loaded.
		/// </summary>
		public void OnLoad()
		{
			// Register all game objects.

			GameObject[] gos = Resources.FindObjectsOfTypeAll<GameObject>();

			foreach (GameObject go in gos)
			{
				if (!_worldHashGenerated)
				{
					Transform transform = go.transform;
					while (transform != null)
					{
						_worldHash ^= Utils.StringJenkinsHash(transform.name);
						transform = transform.parent;
					}
				}

				HandleNewObject(go);
			}

			Logger.Log("World hash: " + _worldHash);
			_worldHashGenerated = true;

			// Check mandatory objects.

			Client.Assert(_worldTimeFsm != null, "No world time FSM found :(");
			Client.Assert(_lastnameFsm != null, "Mailbox FSM couldn't be found!");
			Client.Assert(_lastnameTextMesh != null, "Mailbox TextMesh couldn't be found!");

			// Notify different parts of the mod about the world load.

			if (GameCallbacks.onWorldLoad != null)
			{
				GameCallbacks.onWorldLoad();
			}
		}

		/// <summary>
		/// Callback called when world gets unloaded.
		/// </summary>
		public void OnUnload()
		{
			// Iterate backwards so pickupable users will be notified before the database.

			for (int i = _gameObjectUsers.Count; i > 0; --i)
			{
				_gameObjectUsers[i - 1].DestroyObjects();
			}

			if (GameCallbacks.onWorldUnload != null)
			{
				GameCallbacks.onWorldUnload();
			}

			_player = null;
		}

		/// <summary>
		/// Update game world state.
		/// </summary>
		public void Update()
		{
			if (_player == null)
			{
				GameObject playerGo = GameObject.Find("PLAYER");

				if (playerGo != null)
				{
					_player = new GamePlayer(playerGo);

					if (GameCallbacks.onLocalPlayerCreated != null)
					{
						GameCallbacks.onLocalPlayerCreated();
					}
				}
			}
		}

		/// <summary>
		/// List of vehicle gameobject names.
		/// </summary>
		private static readonly string[] VehicleGoNames = {
			"JONNEZ ES(Clone)", "HAYOSIKO(1500kg, 250)", "SATSUMA(557kg, 248)",
			"RCO_RUSCKO12(270)", "KEKMET(350-400psi)", "FLATBED", "FERNDALE(1630kg)", "GIFU(750/450psi)"
		};

		public void UpdateImgui()
		{
			// noop
		}

		/// <summary>
		/// Spawns pickupable.
		/// </summary>
		/// <param name="prefabId">Pickupable prefab id.</param>
		/// <param name="position">The spawn position.</param>
		/// <param name="rotation">The spawn rotation.</param>
		/// <param name="objectId">The ObjectID of the object.</param>
		/// <returns>Spawned pickupable game object.</returns>
		public GameObject SpawnPickupable(int prefabId, Vector3 position, Quaternion rotation, int objectId)
		{
			GamePickupableDatabase.PrefabDesc prefabDescriptor = _gamePickupableDatabase.GetPickupablePrefab(prefabId);
			Client.Assert(prefabDescriptor != null, $"Unable to find pickupable prefab {prefabId}");
			return prefabDescriptor.Spawn(position, rotation);
		}
	}
}
