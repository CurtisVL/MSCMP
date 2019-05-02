using MSCMP.Game;
using MSCMP.Game.Components;
using MSCMP.Game.Objects;
using MSCMP.Network.Messages;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSCMP.Network
{
	internal class NetWorld
	{
		/// <summary>
		/// If we should log object registering debug messages.
		/// </summary>
		public static bool DisplayObjectRegisteringDebug = false;

		/// <summary>
		/// Maximum count of the supported vehicles.
		/// </summary>
		public const int MAX_VEHICLES = byte.MaxValue;

		/// <summary>
		/// Maximum count of the supported pickupables.
		/// </summary>
		public const int MAX_PICKUPABLES = ushort.MaxValue;

		/// <summary>
		/// Net manager owning this world.
		/// </summary>
		private readonly NetManager _netManager;

		/// <summary>
		/// Interval between each periodical update in seconds.
		/// </summary>
		private const float PERIODICAL_UPDATE_INTERVAL = 10.0f;

		/// <summary>
		/// Time left to send periodical message.
		/// </summary>
		private float _timeToSendPeriodicalUpdate;

		/// <summary>
		/// If the player is still handling FullWorldSync.
		/// </summary>
		public bool PlayerIsLoading = true;

		/// <summary>
		/// Instance.
		/// </summary>
		public static NetWorld Instance;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="netManager">Network manager owning this network world.</param>
		public NetWorld(NetManager netManager)
		{
			_netManager = netManager;
			Instance = this;

			GameCallbacks.onWorldUnload += OnGameWorldUnload;
			GameCallbacks.onWorldLoad += OnGameWorldLoad;
			GameCallbacks.onPlayMakerObjectCreate += (instance, prefab) =>
			{
				if (!GamePickupableDatabase.IsPickupable(instance))
				{
					return;
				}

				PickupableMetaDataComponent metaData = prefab.GetComponent<PickupableMetaDataComponent>();
				Client.Assert(metaData != null, "Tried to spawn pickupable that has no meta data assigned.");

				PickupableSpawnMessage msg = new PickupableSpawnMessage
				{
					prefabId = metaData.PrefabId,
					transform =
					{
						position = Utils.GameVec3ToNet(instance.transform.position),
						rotation = Utils.GameQuatToNet(instance.transform.rotation)
					},
					active = instance.activeSelf
				};

				// Setup sync component on object.
				Client.Assert(instance.GetComponent<ObjectSyncComponent>(), $"Object created but no ObjectSyncComponent could be found! Object name: {instance.name}");
				ObjectSyncComponent osc = instance.GetComponent<ObjectSyncComponent>();
				msg.id = osc.Setup(osc.ObjectType, ObjectSyncManager.AutomaticId);

				// Determine if object should be spawned on remote client.
				// (Helps to avoid duplicate objects spawning)
				bool sendToRemote = false;
				if (NetManager.Instance.IsHost)
				{
					Logger.Debug("Sending new object data to client!");
					sendToRemote = true;
				}
				else
				{
					// This is a hack to workout beer bottles not spawning on the remote client due to items only spawning on the host.
					// This will be replaced in the future.
					if (instance.name.StartsWith("BottleBeerFly"))
					{
						sendToRemote = true;
					}
				}

				if (sendToRemote)
				{
					netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
					Logger.Debug("Sending new object data to client!");
				}
			};

			GameCallbacks.onPlayMakerObjectActivate += (instance, activate) =>
			{
				if (PlayerIsLoading)
				{
					return;
				}

				if (activate == instance.activeSelf)
				{
					return;
				}

				if (!GamePickupableDatabase.IsPickupable(instance))
				{
					return;
				}

				ObjectSyncComponent pickupable = GetPickupableByGameObject(instance);
				if (pickupable == null)
				{
					return;
				}

				if (activate)
				{
					PickupableMetaDataComponent metaData = pickupable.gameObject.GetComponent<PickupableMetaDataComponent>();

					PickupableSpawnMessage msg = new PickupableSpawnMessage
					{
						id = pickupable.ObjectId,
						prefabId = metaData.PrefabId,
						transform =
						{
							position = Utils.GameVec3ToNet(instance.transform.position),
							rotation = Utils.GameQuatToNet(instance.transform.rotation)
						}
					};
					netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
				}
				else
				{
					PickupableActivateMessage msg = new PickupableActivateMessage
					{
						id = pickupable.ObjectId,
						activate = false
					};
					netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
				}
			};

			GameCallbacks.onPlayMakerObjectDestroy += instance =>
			{
				if (!GamePickupableDatabase.IsPickupable(instance))
				{
					return;
				}

				ObjectSyncComponent pickupable = GetPickupableByGameObject(instance);
				if (pickupable == null)
				{
					Logger.Debug($"Pickupable {instance.name} has been destroyed however it is not registered, skipping removal.");
					return;
				}

				HandlePickupableDestroy(instance);
			};

			GameCallbacks.onPlayMakerSetPosition += (gameObject, position, space) =>
			{
				if (!GamePickupableDatabase.IsPickupable(gameObject))
				{
					return;
				}

				ObjectSyncComponent pickupable = GetPickupableByGameObject(gameObject);
				if (pickupable == null)
				{
					return;
				}


				if (space == Space.Self)
				{
					position += gameObject.transform.position;
				}

				PickupableSetPositionMessage msg = new PickupableSetPositionMessage
				{
					id = pickupable.ObjectId,
					position = Utils.GameVec3ToNet(position)
				};
				netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			};

			RegisterNetworkMessagesHandlers(netManager.MessageHandler);
		}

		/// <summary>
		/// Register world related network message handlers.
		/// </summary>
		/// <param name="netMessageHandler">The network message handler to register messages to.</param>
		private void RegisterNetworkMessagesHandlers(NetMessageHandler netMessageHandler)
		{
			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, PickupableSetPositionMessage msg) =>
			{
				Client.Assert(ObjectSyncManager.GetObjectById(msg.id), $"Tried to move pickupable that is not spawned {msg.id}.");
				GameObject gameObject = ObjectSyncManager.GetObjectById(msg.id);
				gameObject.transform.position = Utils.NetVec3ToGame(msg.position);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, PickupableActivateMessage msg) =>
			{
				GameObject gameObject = null;
				if (ObjectSyncManager.GetObjectById(msg.id))
				{
					gameObject = ObjectSyncManager.GetObjectById(msg.id);
				}
				Client.Assert(gameObject != null, "Tried to activate a pickupable but it's not spawned! Does any connected client or you have other mods beside MSC:MP installed? Try uninstalling them!");

				gameObject?.SetActive(msg.activate);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, PickupableSpawnMessage msg) =>
			{
				SpawnPickupable(msg);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, PickupableDestroyMessage msg) =>
			{
				if (!ObjectSyncManager.GetSyncComponentById(msg.id))
				{
					return;
				}

				Object.Destroy(ObjectSyncManager.GetObjectById(msg.id));
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, WorldPeriodicalUpdateMessage msg) =>
			{
				// Game reports 'next hour' - we want to have transition so correct it.
				GameWorld.Instance.WorldTime = msg.sunClock - 2.0f;
				GameWorld.Instance.WorldDay = msg.worldDay;
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, PlayerSyncMessage msg) =>
			{
				NetPlayer player = _netManager.GetPlayer(sender);
				if (player == null)
				{
					Logger.Error($"Received synchronization packet from {sender} but there is not player registered using this id.");
					return;
				}

				player.HandleSynchronize(msg);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, AnimSyncMessage msg) =>
			{
				NetPlayer player = _netManager.GetPlayer(sender);
				if (player == null)
				{
					Logger.Error($"Received animation synchronization packet from {sender} but there is not player registered using this id.");
					return;
				}

				player.HandleAnimSynchronize(msg);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, OpenDoorsMessage msg) =>
			{
				NetPlayer player = _netManager.GetPlayer(sender);
				if (player == null)
				{
					Logger.Error($"Received OpenDoorsMessage however there is no matching player {sender}! (open: {msg.open}");
					return;
				}

				GameDoor doors = GameDoorsManager.Instance.FindGameDoors(Utils.NetVec3ToGame(msg.position));
				if (doors == null)
				{
					Logger.Error($"Player tried to open door, however, the door could not be found!");
					return;
				}
				doors.Open(msg.open);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, FullWorldSyncMessage msg) =>
			{
				NetPlayer player = _netManager.GetPlayer(sender);

				// This one should never happen - if happens there is something done miserably wrong.
				Client.Assert(player != null, $"There is no player matching given steam id {sender}.");

				// Handle full world state synchronization.
				HandleFullWorldSync(msg);

				// Spawn host character.
				if (player != null)
				{
					player.Spawn();

					// Set player state.

					player.Teleport(Utils.NetVec3ToGame(msg.spawnPosition), Utils.NetQuatToGame(msg.spawnRotation));

					if (msg.pickedUpObject != NetPickupable.INVALID_ID)
					{
						player.PickupObject(msg.pickedUpObject);
					}
				}

				// World is loaded! Notify network manager about that.
				_netManager.OnNetworkWorldLoaded();
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, AskForWorldStateMessage msg) =>
			{
				FullWorldSyncMessage msgF = new FullWorldSyncMessage();
				WriteFullWorldSync(msgF);
				_netManager.SendMessage(_netManager.GetPlayer(sender), msgF, Steamworks.EP2PSend.k_EP2PSendReliable);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, VehicleEnterMessage msg) =>
			{
				NetPlayer player = _netManager.GetPlayer(sender);
				if (player == null)
				{
					Logger.Error($"Steam user of id {sender} send message however there is no active player matching this id.");
					return;
				}

				ObjectSyncComponent vehicle = ObjectSyncManager.Instance.ObjectIDs[msg.objectID];
				if (vehicle == null)
				{
					Logger.Error("Player " + player.SteamId + " tried to enter vehicle with Object ID " + msg.objectID + " but there is no vehicle with such id.");
					return;
				}

				player.EnterVehicle(vehicle, msg.passenger);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, VehicleLeaveMessage msg) =>
			{
				NetPlayer player = _netManager.GetPlayer(sender);
				if (player == null)
				{
					Logger.Error($"Steam user of id {sender} send message however there is no active player matching this id.");
					return;
				}
				player.LeaveVehicle();
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, VehicleSwitchMessage msg) =>
			{
				float newValueFloat = -1;

				PlayerVehicle vehicle = ObjectSyncManager.Instance.ObjectIDs[msg.objectID].GetObjectSubtype() as PlayerVehicle;
				if (vehicle == null)
				{
					Logger.Debug("Remote player tried to change a switch in vehicle " + msg.objectID + " but there is no vehicle with such id.");
					return;
				}

				if (msg.HasSwitchValueFloat)
				{
					newValueFloat = msg.SwitchValueFloat;
				}

				vehicle.SetVehicleSwitch((PlayerVehicle.SwitchIDs)msg.switchID, msg.switchValue, newValueFloat);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, LightSwitchMessage msg) =>
			{
				LightSwitch light = LightSwitchManager.Instance.FindLightSwitch(Utils.NetVec3ToGame(msg.pos));
				light.TurnOn(msg.toggle);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, ObjectSyncMessage msg) =>
			{
				ObjectSyncComponent osc;
				ObjectSyncManager.SyncTypes type = (ObjectSyncManager.SyncTypes)msg.SyncType;
				if (ObjectSyncManager.GetObjectById(msg.objectID))
				{
					osc = ObjectSyncManager.GetSyncComponentById(msg.objectID);
				}
				else
				{
					Logger.Error($"Specified object is not yet added to the ObjectID's Dictionary! (Object ID: {msg.objectID})");
					return;
				}

				// This *should* never happen, but apparently it's possible.
				Client.Assert(osc != null, $"Object Sync Component wasn't found for object with ID {msg.objectID}, however, the object had a dictionary entry!");

				// Host controls who owns an object.
				if (NetManager.Instance.IsHost)
				{
					// Set owner on host.
					if (type == ObjectSyncManager.SyncTypes.SetOwner)
					{
						ObjectSyncManager.SetOwnerHandler(msg, osc, sender);
					}
					// Remove owner on host.
					if (type == ObjectSyncManager.SyncTypes.RemoveOwner)
					{
						if (osc.Owner == _netManager.GetLocalPlayer())
						{
							ObjectSyncManager.RemoveOwnerHandler(msg, osc, sender);
						}
					}
					// Sync taken by force on host.
					if (type == ObjectSyncManager.SyncTypes.ForceSetOwner)
					{
						ObjectSyncManager.SyncTakenByForceHandler(msg, osc, sender);
					}
				}

				// Set ownership info on clients.
				else
				{
					NetPlayer player = _netManager.GetPlayerByPlayerId(msg.OwnerPlayerID);
					// Set owner.
					if (type == ObjectSyncManager.SyncTypes.SetOwner)
					{
						if (osc.Owner != _netManager.GetLocalPlayer())
						{
							osc.OwnerSetToRemote(player);
						}
						osc.Owner = player;
					}
					// Remove owner.
					else if (type == ObjectSyncManager.SyncTypes.RemoveOwner)
					{
						if (osc.Owner != _netManager.GetLocalPlayer())
						{
							osc.OwnerRemoved();
						}
						osc.Owner = null;
					}
					// Force set owner.
					else if (type == ObjectSyncManager.SyncTypes.ForceSetOwner)
					{
						if (osc.Owner != _netManager.GetLocalPlayer())
						{
							osc.SyncTakenByForce();
							osc.SyncEnabled = false;
						}
						osc.Owner = player;
						if (osc.Owner == _netManager.GetLocalPlayer())
						{
							osc.SyncEnabled = true;
						}
					}
				}

				// Set object's position and variables.
				if (osc.Owner == _netManager.GetPlayer(sender) || type == ObjectSyncManager.SyncTypes.PeriodicSync)
				{
					// Send synced variables, or variables only sync in some cases.
					if (msg.HasSyncedVariables)
					{
						osc.HandleSyncedVariables(msg.SyncedVariables);
					}

					// Full sync.
					if (msg.HasPosition && msg.HasRotation)
					{
						osc.SetPositionAndRotation(Utils.NetVec3ToGame(msg.Position), Utils.NetQuatToGame(msg.Rotation));
					}
					// Position only sync.
					else if (msg.HasPosition)
					{
						Quaternion zero = new Quaternion(0, 0, 0, 0);
						osc.SetPositionAndRotation(Utils.NetVec3ToGame(msg.Position), zero);
					}
					// Rotation only sync.
					else if (msg.HasRotation)
					{
						osc.SetPositionAndRotation(Vector3.zero, Utils.NetQuatToGame(msg.Rotation));
					}
				}
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, ObjectSyncResponseMessage msg) =>
			{
				ObjectSyncComponent osc = ObjectSyncManager.GetSyncComponentById(msg.objectID);
				if (msg.accepted)
				{
					osc.SyncEnabled = true;
					osc.Owner = NetManager.Instance.GetLocalPlayer();
				}
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, ObjectSyncRequestMessage msg) =>
			{
				Client.Assert(ObjectSyncManager.GetObjectById(msg.objectID), $"Remote client tried to request object sync of an unknown object, remote ObjectID was: {msg.objectID}");

				ObjectSyncComponent osc = ObjectSyncManager.GetSyncComponentById(msg.objectID);
				osc.SendObjectSync(ObjectSyncManager.SyncTypes.GenericSync, true, true);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, EventHookSyncMessage msg) =>
			{
				if (msg.request)
				{
					EventHook.SendSync(msg.fsmID);
					return;
				}

				if (msg.HasFsmEventName)
				{
					EventHook.HandleEventSync(msg.fsmID, msg.fsmEventID, msg.FsmEventName);
				}
				else
				{
					EventHook.HandleEventSync(msg.fsmID, msg.fsmEventID);
				}
			});
		}

		/// <summary>
		/// Update net world.
		/// </summary>
		public void Update()
		{

			if (_netManager.IsPlayer || !_netManager.IsNetworkPlayerConnected())
			{
				return;
			}

			_timeToSendPeriodicalUpdate -= Time.deltaTime;

			if (_timeToSendPeriodicalUpdate <= 0.0f)
			{
				WorldPeriodicalUpdateMessage message = new WorldPeriodicalUpdateMessage
				{
					sunClock = (byte)GameWorld.Instance.WorldTime,
					worldDay = (byte)GameWorld.Instance.WorldDay
				};
				_netManager.BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendReliable);

				_timeToSendPeriodicalUpdate = PERIODICAL_UPDATE_INTERVAL;
			}

		}

		/// <summary>
		/// Called when game world gets loaded.
		/// </summary>
		public void OnGameWorldLoad()
		{

		}


		/// <summary>
		/// Called when game world gets unloaded.
		/// </summary>
		private void OnGameWorldUnload()
		{
			ObjectSyncManager.Instance.ObjectIDs.Clear();
		}

		/// <summary>
		/// Write full world synchronization message.
		/// </summary>
		/// <param name="msg">The message to write to.</param>
		public void WriteFullWorldSync(FullWorldSyncMessage msg)
		{
			Logger.Debug("Writing full world synchronization message.");
			Stopwatch watch = Stopwatch.StartNew();

			// 'Player is loading' is only applicable for remote client.
			PlayerIsLoading = false;

			// Write time
			GameWorld gameWorld = GameWorld.Instance;
			msg.dayTime = gameWorld.WorldTime;
			msg.day = gameWorld.WorldDay;

			// Write mailbox name
			msg.mailboxName = gameWorld.PlayerLastName;

			// Write doors
			List<GameDoor> doors = GameDoorsManager.Instance.Doors;
			int doorsCount = doors.Count;
			msg.doors = new DoorsInitMessage[doorsCount];

			Logger.Debug($"Writing state of {doorsCount} doors.");
			for (int i = 0; i < doorsCount; ++i)
			{
				DoorsInitMessage doorMsg = new DoorsInitMessage();
				GameDoor door = doors[i];
				doorMsg.position = Utils.GameVec3ToNet(door.Position);
				doorMsg.open = door.IsOpen;
				msg.doors[i] = doorMsg;
			}

			// Write light switches.
			List<LightSwitch> lights = LightSwitchManager.Instance.LightSwitches;
			int lightCount = lights.Count;
			msg.lights = new LightSwitchMessage[lightCount];

			Logger.Debug($"Writing light switches state of {lightCount}");
			for (int i = 0; i < lightCount; i++)
			{
				LightSwitchMessage lightMsg = new LightSwitchMessage();
				LightSwitch light = lights[i];
				lightMsg.pos = Utils.GameVec3ToNet(light.Position);
				lightMsg.toggle = light.SwitchStatus;
				msg.lights[i] = lightMsg;
			}

			// Write connected players.
			msg.connectedPlayers = new ConnectedPlayersMessage();
			int[] playerIDs = new int[_netManager.Players.Count];
			ulong[] steamIDs = new ulong[_netManager.Players.Count];
			int index2 = 0;
			foreach (KeyValuePair<int, NetPlayer> connectedPlayer in _netManager.Players)
			{
				playerIDs[index2] = connectedPlayer.Key;
				steamIDs[index2] = connectedPlayer.Value.SteamId.m_SteamID;
				index2++;
			}
			msg.connectedPlayers.playerIDs = playerIDs;
			msg.connectedPlayers.steamIDs = steamIDs;

			// Write objects. (Pickupables, Player vehicles, AI vehicles)
			List<PickupableSpawnMessage> pickupableMessages = new List<PickupableSpawnMessage>();
			Logger.Debug($"Writing state of {ObjectSyncManager.Instance.ObjectIDs.Count} objects");
			List<float> data = new List<float>();
			foreach (KeyValuePair<int, ObjectSyncComponent> kv in ObjectSyncManager.Instance.ObjectIDs)
			{
				ObjectSyncComponent osc = kv.Value;
				if (osc == null)
				{
					continue;
				}
				if (osc.ObjectType != ObjectSyncManager.ObjectTypes.Pickupable)
				{
					continue;
				}
				bool wasActive = true;
				if (!osc.gameObject.activeSelf)
				{
					wasActive = false;
					osc.gameObject.SetActive(true);
				}
				if (DisplayObjectRegisteringDebug)
				{
					Logger.Debug($"Writing object: {osc.gameObject.name}");
				}

				PickupableSpawnMessage pickupableMsg = new PickupableSpawnMessage();

				PickupableMetaDataComponent metaData = osc.gameObject.GetComponent<PickupableMetaDataComponent>();
				Client.Assert(metaData != null && metaData.PrefabDescriptor != null, $"Object with broken meta data -- {osc.gameObject.name}.");

				pickupableMsg.prefabId = metaData.PrefabId;

				Transform transform = osc.gameObject.transform;
				pickupableMsg.transform.position = Utils.GameVec3ToNet(transform.position);
				pickupableMsg.transform.rotation = Utils.GameQuatToNet(transform.rotation);

				pickupableMsg.active = osc.gameObject.activeSelf;

				// ObjectID
				pickupableMsg.id = osc.gameObject.GetComponent<ObjectSyncComponent>().ObjectId;

				if (data.Count != 0)
				{
					pickupableMsg.Data = data.ToArray();
				}
				if (!wasActive)
				{
					osc.gameObject.SetActive(false);
				}
				pickupableMessages.Add(pickupableMsg);
			}

			// Object owners.
			int objectsCount = ObjectSyncManager.Instance.ObjectIDs.Count;
			msg.objectOwners = new ObjectOwnerSync[objectsCount];

			Dictionary<NetPlayer, int> playersReversed = new Dictionary<NetPlayer, int>();
			foreach (KeyValuePair<int, NetPlayer> player in _netManager.Players)
			{
				playersReversed.Add(player.Value, player.Key);
			}

			Logger.Debug($"Writing owners of {objectsCount} objects!");
			int index = 0;
			foreach (KeyValuePair<int, ObjectSyncComponent> objectId in ObjectSyncManager.Instance.ObjectIDs)
			{
				ObjectOwnerSync objectMsg = new ObjectOwnerSync();
				objectMsg.objectID = objectId.Key;
				if (objectId.Value.Owner != null)
				{
					objectMsg.ownerPlayerID = playersReversed[objectId.Value.Owner];
				}
				else
				{
					objectMsg.ownerPlayerID = -1;
				}
				msg.objectOwners[index] = objectMsg;
				index++;
			}

			msg.pickupables = pickupableMessages.ToArray();

			_netManager.GetLocalPlayer().WriteSpawnState(msg);

			watch.Stop();
			Logger.Debug("World state has been written. Took " + watch.ElapsedMilliseconds + "ms");
		}


		/// <summary>
		/// Handle full world sync message.
		/// </summary>
		/// <param name="msg">The message to handle.</param>

		public void HandleFullWorldSync(FullWorldSyncMessage msg)
		{
			Logger.Debug("Handling full world synchronization message.");
			Stopwatch watch = Stopwatch.StartNew();

			// Read time
			GameWorld gameWorld = GameWorld.Instance;
			gameWorld.WorldTime = msg.dayTime;
			gameWorld.WorldDay = msg.day;

			// Read mailbox name
			gameWorld.PlayerLastName = msg.mailboxName;

			// Doors.
			foreach (DoorsInitMessage door in msg.doors)
			{
				Vector3 position = Utils.NetVec3ToGame(door.position);
				GameDoor doors = GameDoorsManager.Instance.FindGameDoors(position);
				Client.Assert(doors != null, $"Unable to find doors at: {position}.");
				if (doors.IsOpen != door.open)
				{
					doors.Open(door.open);
				}
			}

			// Lights.
			foreach (LightSwitchMessage light in msg.lights)
			{
				Vector3 position = Utils.NetVec3ToGame(light.pos);
				LightSwitch lights = LightSwitchManager.Instance.FindLightSwitch(position);
				Client.Assert(lights != null, $"Unable to find light switch at: {position}.");
				if (lights.SwitchStatus != light.toggle)
				{
					lights.TurnOn(light.toggle);
				}
			}

			// Pickupables
			foreach (PickupableSpawnMessage pickupableMsg in msg.pickupables)
			{
				SpawnPickupable(pickupableMsg);
			}

			// Remove spawned (and active) pickupables that we did not get info about.
			foreach (KeyValuePair<int, GameObject> kv in GamePickupableDatabase.Instance.Pickupables)
			{
				if (kv.Value.GetComponent<ObjectSyncComponent>() == null)
				{
					Object.Destroy(kv.Value);
				}
			}

			// Connected players.
			int i = 0;
			foreach (int newPlayerId in msg.connectedPlayers.playerIDs)
			{
				Steamworks.CSteamID localSteamId = Steamworks.SteamUser.GetSteamID();
				Steamworks.CSteamID newPlayerSteamId = new Steamworks.CSteamID(msg.connectedPlayers.steamIDs[i]);
				// If player is not host or local player, setup new player.
				if (newPlayerSteamId != _netManager.GetHostPlayer().SteamId && newPlayerSteamId != localSteamId)
				{
					_netManager.Players.Add(newPlayerId, new NetPlayer(_netManager, this, newPlayerSteamId));
					_netManager.Players[newPlayerId].Spawn();
					Logger.Debug("Setup new player at ID: " + newPlayerId);
				}
				i++;
			}

			// Object owners.
			foreach (ObjectOwnerSync syncMsg in msg.objectOwners)
			{
				if (syncMsg.ownerPlayerID != -1)
				{
					ObjectSyncManager.Instance.ObjectIDs[syncMsg.objectID].Owner = _netManager.GetPlayerByPlayerId(syncMsg.ownerPlayerID);
				}
			}

			GamePickupableDatabase.Instance.Pickupables.Clear();
			PlayerIsLoading = false;

			watch.Stop();
			Logger.Debug("Full world synchronization message has been handled. Took " + watch.ElapsedMilliseconds + "ms");
		}

		/// <summary>
		/// Ask host for full world sync.
		/// </summary>
		public void AskForFullWorldSync()
		{
			AskForWorldStateMessage msg = new AskForWorldStateMessage();
			_netManager.SendMessage(_netManager.GetHostPlayer(), msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

#if !PUBLIC_RELEASE
		/// <summary>
		/// Update debug imgui.
		/// </summary>
		public void UpdateImgui()
		{
			// noop
		}
#endif

		/// <summary>
		/// Get pickupable game object from object id.
		/// </summary>
		/// <param name="objectId">Object id of the pickupable.</param>
		/// <returns>Game object representing the given pickupable or null if there is no pickupable matching this network id.</returns>
		public GameObject GetPickupableGameObject(int objectId)
		{
			if (ObjectSyncManager.Instance.ObjectIDs.ContainsKey(objectId))
			{
				return ObjectSyncManager.Instance.ObjectIDs[objectId].gameObject;
			}
			return null;
		}

		/// <summary>
		/// Get pickupable object id from game object.
		/// </summary>
		/// <param name="go">Game object to get object id for.</param>
		/// <returns>The object id of pickupable or invalid id of the pickupable if no object ID is found for given game object.</returns>
		public ObjectSyncComponent GetPickupableByGameObject(GameObject go)
		{
			foreach (KeyValuePair<int, ObjectSyncComponent> osc in ObjectSyncManager.Instance.ObjectIDs)
			{
				if (osc.Value.GetGameObject() == null)
				{
					Logger.Error($"Found a broken ObjectID entry (Couldn't return GameObject) whilst trying to get pickupable by GameObject, ID: {osc.Value.ObjectId}");
					continue;
				}

				if (osc.Value.GetGameObject() == go)
				{
					return osc.Value;
				}
			}
			Logger.Error("GetPickupableByGameObject: Couldn't find GameObject!");
			return null;
		}

		/// <summary>
		/// Get pickupable object ID from game object.
		/// </summary>
		/// <param name="go">Game object to get object ID for.</param>
		/// <returns>The object ID of pickupable or invalid ID of the pickupable if no object ID is found for given game object.</returns>
		public int GetPickupableObjectId(GameObject go)
		{
			foreach (KeyValuePair<int, ObjectSyncComponent> osc in ObjectSyncManager.Instance.ObjectIDs)
			{
				if (osc.Value.gameObject == go)
				{
					return osc.Value.ObjectId;
				}
			}
			return NetPickupable.INVALID_ID;
		}

		/// <summary>
		/// Handle destroy of pickupable game object.
		/// </summary>
		/// <param name="pickupable">The destroyed pickupable.</param>
		public void HandlePickupableDestroy(GameObject pickupable)
		{
			ObjectSyncComponent osc = GetPickupableByGameObject(pickupable);
			if (osc != null)
			{
				PickupableDestroyMessage msg = new PickupableDestroyMessage();
				msg.id = osc.ObjectId;
				_netManager.BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);

				Logger.Debug($"Handle pickupable destroy {pickupable.name}, Object ID: {osc.ObjectId}");
			}
			else
			{
				Logger.Debug($"Unhandled pickupable has been destroyed {pickupable.name}");
				Logger.Debug(Environment.StackTrace);
			}
		}

		/// <summary>
		/// Spawn pickupable from network message.
		/// </summary>
		/// <param name="msg">The message containing info about pickupable to spawn.</param>
		public void SpawnPickupable(PickupableSpawnMessage msg)
		{
			Vector3 position = Utils.NetVec3ToGame(msg.transform.position);
			Quaternion rotation = Utils.NetQuatToGame(msg.transform.rotation);

			if (ObjectSyncManager.Instance.ObjectIDs.ContainsKey(msg.id))
			{
				ObjectSyncComponent osc = ObjectSyncManager.Instance.ObjectIDs[msg.id];
				// Ignore spawn requests for items that are already spawned.
				if (osc.ObjectId == msg.id)
				{
					return;
				}
				GameObject gameObject = osc.gameObject;
				GamePickupableDatabase.PrefabDesc desc = GamePickupableDatabase.Instance.GetPickupablePrefab(msg.prefabId);
				if (gameObject != null)
				{
					PickupableMetaDataComponent metaData = gameObject.GetComponent<PickupableMetaDataComponent>();
					// Incorrect prefab found.
					if (msg.prefabId != metaData.PrefabId)
					{
						bool resolved = false;
						foreach (KeyValuePair<int, ObjectSyncComponent> go in ObjectSyncManager.Instance.ObjectIDs)
						{
							if (go.Value.gameObject.GetComponent<PickupableMetaDataComponent>().PrefabId == msg.prefabId)
							{
								gameObject = go.Value.gameObject;
								Logger.Debug("Prefab mismatch was resolved.");
								resolved = true;
								break;
							}
						}
						if (!resolved)
						{
							Client.Assert(true, "Prefab ID mismatch couldn't be resolved!");
						}
					}
					gameObject.SetActive(msg.active);
					gameObject.transform.position = position;
					gameObject.transform.rotation = rotation;

					if (gameObject.GetComponent<ObjectSyncComponent>() != null)
					{
						gameObject.GetComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.Pickupable, msg.id);
					}
					else
					{
						gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.Pickupable, msg.id);
					}
					return;
				}

				DestroyPickupableLocal(msg.id);
			}

			GameObject pickupable = GameWorld.Instance.SpawnPickupable(msg.prefabId, position, rotation, msg.id);
			if (pickupable.GetComponent<ObjectSyncComponent>() != null)
			{
				Object.Destroy(pickupable.GetComponent<ObjectSyncComponent>());
			}
			pickupable.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.Pickupable, msg.id);
		}

		/// <summary>
		/// Destroy given pickupable from the game without sending destroy message to players.
		/// </summary>
		/// <param name="id">The object ID of the pickupable to destroy.</param>
		private void DestroyPickupableLocal(int id)
		{
			if (!ObjectSyncManager.Instance.ObjectIDs.ContainsKey(id)) return;
			
			GameObject gameObject = ObjectSyncManager.Instance.ObjectIDs[id].gameObject;
			if (gameObject != null)
			{
				Object.Destroy(gameObject);
			}
		}
	}
}
