using MSCMP.UI;
using System;
using System.Collections.Generic;
using System.IO;
using MSCMP.Network.Messages;
using Steamworks;
using UnityEngine;

namespace MSCMP.Network
{
	internal class NetManager
	{
		private const int MAX_PLAYERS = 16;
		private static int _protocolVersion = 2;
		private const uint PROTOCOL_ID = 0x6d73636d;

		private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
		private Callback<P2PSessionRequest_t> _p2PSessionRequestCallback;
		private Callback<P2PSessionConnectFail_t> _p2PConnectFailCallback;
		private readonly CallResult<LobbyCreated_t> _lobbyCreatedCallResult;
		private readonly CallResult<LobbyEnter_t> _lobbyEnterCallResult;
		private readonly CallResult<LobbyMatchList_t> _lobbyListResult;
		private CallResult<LobbyDataUpdate_t> _lobbyDataResult;
		public enum Mode
		{
			None,
			Host,
			Player
		}

		public enum State
		{
			Idle,
			CreatingLobby,
			LoadingGameWorld,
			Playing
		}

		private State _state = State.Idle;
		private Mode _mode = Mode.None;

		public bool IsHost => _mode == Mode.Host;

		public bool IsPlayer => _mode == Mode.Player;

		public bool IsOnline => _mode != Mode.None;

		public bool IsPlaying => _state == State.Playing;
		private CSteamID _currentLobbyId = CSteamID.Nil;

		//private NetPlayer[] players = new NetPlayer[MAX_PLAYERS];
		/// <summary>
		/// Stores player's ID, as well as NetPlayer.
		/// </summary>
		public readonly Dictionary<int, NetPlayer> Players = new Dictionary<int, NetPlayer>();

		/// <summary>
		/// The interval between sending individual heartbeat.
		/// </summary>
		private const float HEARTBEAT_INTERVAL = 5.0f;

		/// <summary>
		/// Timeout time of the connection.
		/// </summary>
		private const float TIMEOUT_TIME = 60.0f; // This was raised as too many people had issues with joining onn slow PCs. A better solution will be implemented in the future.

		/// <summary>
		/// How many seconds left before sending next heartbeat?
		/// </summary>
		private float _timeToSendHeartbeat;

		/// <summary>
		/// How many seconds passed since last heart beat was received.
		/// </summary>
		private float _timeSinceLastHeartbeat;

		/// <summary>
		/// The value of the clock on the remote players' computer.
		/// </summary>
		private ulong _remoteClock;

		/// <summary>
		/// Current ping value.
		/// </summary>
		private uint _ping;

		/// <summary>
		/// The time when network manager was created in UTC.
		/// </summary>
		private readonly DateTime _netManagerCreationTime;

		/// <summary>
		/// Network world.
		/// </summary>
		private readonly NetWorld _netWorld;

		/// <summary>
		/// The network message handler.
		/// </summary>
		private readonly NetMessageHandler _netMessageHandler;

		/// <summary>
		/// Get net manager's message handler.
		/// </summary>
		public NetMessageHandler MessageHandler => _netMessageHandler;

		public static NetManager Instance;

		/// <summary>
		/// Network statistics object.
		/// </summary>
		private readonly NetStatistics _statistics;

		/// <summary>
		/// The time the connection was started in UTC.
		/// </summary>
		private DateTime? _connectionStartedTime;

		/// <summary>
		/// SteamID of host, used when connecting to a lobby.
		/// </summary>
		private CSteamID _hostSteamId;

		/// <summary>
		/// Local player ID of this client.
		/// </summary>
		private int _localPlayerId = -1;

		/// <summary>
		/// Get ticks since connection started.
		/// </summary>
		public ulong TicksSinceConnectionStarted
		{
			get
			{
				Client.Assert(_connectionStartedTime != null, "Attempting to get ticks since connection started, but connection started time is null! (Not connected?)");
				return (ulong)(DateTime.UtcNow - _connectionStartedTime)?.Ticks;
			}
		}

		/// <summary>
		/// Lobby IDs found from current lobbies request.
		/// </summary>
		private readonly List<CSteamID> _lobbyIDs = new List<CSteamID>();

		/// <summary>
		/// Lobby names from current lobbies request.
		/// </summary>
		private readonly List<string> _lobbyNames = new List<string>();

		/// <summary>
		/// Lobby owner SteamIDs.
		/// </summary>
		private readonly List<CSteamID> _lobbyOwners = new List<CSteamID>();

		public NetManager()
		{
			Instance = this;
			_statistics = new NetStatistics(this);
			_netManagerCreationTime = DateTime.UtcNow;
			_netMessageHandler = new NetMessageHandler(this);
			_netWorld = new NetWorld(this);

			// Hopefully this will fix people playing with different mod versions!
			string versionFull = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			int buildNumber = Convert.ToInt32(versionFull.Substring(versionFull.LastIndexOf('.') + 1));
			_protocolVersion = 1;

			_p2PSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
			_p2PConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PConnectFail);
			_gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
			_lobbyCreatedCallResult = new CallResult<LobbyCreated_t>(OnLobbyCreated);
			_lobbyEnterCallResult = new CallResult<LobbyEnter_t>(OnLobbyEnter);
			_lobbyListResult = new CallResult<LobbyMatchList_t>(OnLobbyList);
			_lobbyDataResult = new CallResult<LobbyDataUpdate_t>(OnGetLobbyInfo);

			RegisterProtocolMessagesHandlers();

			RequestLobbies();
		}

		/// <summary>
		/// Handle steam networking P2P connect fail callback.
		/// </summary>
		/// <param name="result">The callback result.</param>
		private void OnP2PConnectFail(P2PSessionConnectFail_t result)
		{
			Logger.Error($"P2P Connection failed, session error: {Utils.P2PSessionErrorToString((EP2PSessionError)result.m_eP2PSessionError)}, remote: {result.m_steamIDRemote}");
		}

		/// <summary>
		/// Handle steam networking P2P session request callback.
		/// </summary>
		/// <param name="result">The callback result.</param>
		private void OnP2PSessionRequest(P2PSessionRequest_t result)
		{
			if (SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote))
			{
				Logger.Debug($"Accepted p2p session with {result.m_steamIDRemote}");
			}
			else
			{
				Logger.Error($"Failed to accept P2P session with {result.m_steamIDRemote}");
			}
		}

		/// <summary>
		/// Handle result of create lobby operation.
		/// </summary>
		/// <param name="result">The operation result.</param>
		/// <param name="ioFailure">Did IO failure happen?</param>
		private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
		{
			if (result.m_eResult != EResult.k_EResultOK || ioFailure)
			{
				Logger.Debug($"Failed to create lobby. (result: {result.m_eResult}, io failure: {ioFailure})");

				Mpgui.Instance.ShowMessageBox($"Failed to create lobby due to steam error.\n{result.m_eResult}/{ioFailure}", () =>
				{
					MpController.Instance.LoadLevel("MainMenu");
				});
				return;
			}

			Logger.Debug($"Lobby has been created, lobby id: {result.m_ulSteamIDLobby}");
			MessagesList.AddMessage("Session started.", MessageSeverity.Info);

			// Setup local player.
			Players.Add(0, new NetLocalPlayer(this, _netWorld, SteamUser.GetSteamID()));
			_localPlayerId = 0;

			_mode = Mode.Host;
			_state = State.Playing;
			_currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
		}

		/// <summary>
		/// Handle result of join lobby operation.
		/// </summary>
		/// <param name="result">The operation result.</param>
		/// <param name="ioFailure">Did IO failure happen?</param>
		private void OnLobbyEnter(LobbyEnter_t result, bool ioFailure)
		{
			if (ioFailure || result.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
			{
				Logger.Error("Failed to join lobby. (reponse: {result.m_EChatRoomEnterResponse}, ioFailure: {ioFailure})");
				Mpgui.Instance.ShowMessageBox($"Failed to join lobby.\n(reponse: {result.m_EChatRoomEnterResponse}, ioFailure: {ioFailure})");
				return;
			}

			Logger.Debug("Entered lobby: " + result.m_ulSteamIDLobby);

			MessagesList.AddMessage("Entered lobby.", MessageSeverity.Info);

			// Setup host player.
			Players.Add(0, new NetPlayer(this, _netWorld, _hostSteamId));
			Logger.Debug("Setup host player as ID: 0");

			_mode = Mode.Player;
			_state = State.LoadingGameWorld;
			_currentLobbyId = new CSteamID(result.m_ulSteamIDLobby);

			ShowLoadingScreen(true);
			SendHandshake(Players[0]);
		}

		/// <summary>
		/// Register protocol related network messages handlers.
		/// </summary>
		private void RegisterProtocolMessagesHandlers()
		{
			_netMessageHandler.BindMessageHandler((CSteamID sender, HandshakeMessage msg) =>
			{
				HandleHandshake(sender, msg);
			});

			_netMessageHandler.BindMessageHandler((CSteamID sender, HeartbeatMessage msg) =>
			{
				HeartbeatResponseMessage message = new HeartbeatResponseMessage();
				message.clientClock = msg.clientClock;
				message.clock = GetNetworkClock();
				BroadcastMessage(message, EP2PSend.k_EP2PSendReliable);
			});

			_netMessageHandler.BindMessageHandler((CSteamID sender, HeartbeatResponseMessage msg) =>
			{
				_ping = (uint)(GetNetworkClock() - msg.clientClock);

				// TODO: Some smart lag compensation.
				_remoteClock = msg.clock;

				_timeSinceLastHeartbeat = 0.0f;
			});

			_netMessageHandler.BindMessageHandler((CSteamID sender, PhoneMessage msg) =>
			{
				Game.PhoneManager.Instance.PhoneCall(msg.topic, msg.timesToRing);
			});

			_netMessageHandler.BindMessageHandler((CSteamID sender, DartSyncMessage msg) =>
			{
				Game.MapManager.Instance.SyncDartsHandler(msg.darts);
			});

			_netMessageHandler.BindMessageHandler((CSteamID sender, DisconnectMessage msg) =>
			{
				HandleDisconnect(GetPlayerIdBySteamId(sender), false);
			});

			_netMessageHandler.BindMessageHandler((CSteamID sender, ConnectedPlayersMessage msg) =>
			{
				int i = 0;
				foreach (int newPlayerId in msg.playerIDs)
				{
					CSteamID localSteamId = SteamUser.GetSteamID();
					CSteamID newPlayerSteamId = new CSteamID(msg.steamIDs[i]);
					// If player is not host or local player, setup new player.
					if (newPlayerSteamId != _hostSteamId && newPlayerSteamId != localSteamId)
					{
						Players.Add(newPlayerId, new NetPlayer(this, _netWorld, newPlayerSteamId));
						Players[newPlayerId].Spawn();
						Logger.Debug("Setup new player at ID: " + newPlayerId);
					}
					i++;
				}
			});

			_netMessageHandler.BindMessageHandler((CSteamID sender, PlayerJoinMessage msg) =>
			{
				if (NetWorld.Instance.PlayerIsLoading)
				{
					if (msg.steamID == SteamUser.GetSteamID().m_SteamID && _localPlayerId == -1)
					{
						Players.Add(msg.playerID, new NetLocalPlayer(this, _netWorld, new CSteamID(msg.steamID)));
						_localPlayerId = msg.playerID;
						Logger.Debug("Setup local player as ID: " + msg.playerID);
					}
					else
					{
						Logger.Debug("Ignored connecting player as curent player is still loading!");
					}
					return;
				}
				CSteamID newPlayerSteamId = new CSteamID(msg.steamID);
				if (newPlayerSteamId != _hostSteamId && newPlayerSteamId != GetLocalPlayer().SteamId)
				{
					Players.Add(msg.playerID, new NetPlayer(this, _netWorld, newPlayerSteamId));
					Players[msg.playerID].Spawn();
					MessagesList.AddMessage($"Player {Players[msg.playerID].GetName()} joined.", MessageSeverity.Info);
					Logger.Debug("New player connected at ID: " + msg.playerID);
				}
			});

			_netMessageHandler.BindMessageHandler((CSteamID sender, PlayerLeaveMessage msg) =>
			{
				if (NetWorld.Instance.PlayerIsLoading)
				{
					Logger.Debug("Ignored connecting player as curent player is still loading!");
					return;
				}
				MessagesList.AddMessage($"Player {Players[msg.playerID].GetName()} disconnected. ({msg.reason})", MessageSeverity.Info);
				CleanupPlayer(msg.playerID);
			});
		}

		/// <summary>
		/// Show loading screen.
		/// </summary>
		/// <param name="show">Show or hide loading screen.</param>
		private void ShowLoadingScreen(bool show)
		{
			if (Application.loadedLevelName == "MainMenu")
			{
				// This is not that slow as you may think - seriously!

				GameObject[] gos = Resources.FindObjectsOfTypeAll<GameObject>();
				GameObject loadingScreen = null;
				foreach (GameObject go in gos)
				{
					if (go.transform.parent == null && go.name == "Loading")
					{
						loadingScreen = go;
						break;
					}
				}
				loadingScreen.SetActive(show);
			}
		}

		/// <summary>
		/// Get network clock with the milliseconds resolution. (time since network manager was created)
		/// </summary>
		/// <returns>Network clock time in miliseconds.</returns>
		public ulong GetNetworkClock()
		{
			return (ulong)(DateTime.UtcNow - _netManagerCreationTime).TotalMilliseconds;
		}

		/// <summary>
		/// Writes given network message into a given stream.
		/// </summary>
		/// <param name="message">The message to write.</param>
		/// <param name="stream">The stream to write message to.</param>
		/// <returns>true if message was written successfully, false otherwise</returns>
		private bool WriteMessage(INetMessage message, MemoryStream stream)
		{
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(PROTOCOL_ID);
			writer.Write((byte)message.MessageId);
			if (!message.Write(writer))
			{
				Client.FatalError("Failed to write network message " + message.MessageId);
				return false;
			}

			_statistics.RecordSendMessage(message.MessageId, stream.Length);
			return true;
		}

		/// <summary>
		/// Broadcasts message to connected players.
		/// </summary>
		/// <typeparam name="T">The type of the message to broadcast.</typeparam>
		/// <param name="message">The message to broadcast.</param>
		/// <param name="sendType">The send type.</param>
		/// <param name="channel">The channel used to deliver message.</param>
		/// <returns></returns>
		public bool BroadcastMessage<T>(T message, EP2PSend sendType, int channel = 0) where T : INetMessage
		{
			if (Players.Count == 0)
			{
				return false;
			}

			MemoryStream stream = new MemoryStream();
			if (!WriteMessage(message, stream))
			{
				return false;
			}

			foreach (KeyValuePair<int, NetPlayer> player in Players)
			{
				if (player.Value is NetLocalPlayer)
				{
					continue;
				}

				player.Value?.SendPacket(stream.GetBuffer(), sendType, channel);
			}
			return true;
		}

		/// <summary>
		/// Send message to given player.
		/// </summary>
		/// <typeparam name="T">The type of the message to broadcast.</typeparam>
		/// <param name="player">Player to who message should be send.</param>
		/// <param name="message">The message to broadcast.</param>
		/// <param name="sendType">The send type.</param>
		/// <param name="channel">The channel used to deliver message.</param>
		/// <returns>true if message was sent false otherwise</returns>
		public bool SendMessage<T>(NetPlayer player, T message, EP2PSend sendType, int channel = 0) where T : INetMessage
		{
			if (player == null)
			{
				return false;
			}

			MemoryStream stream = new MemoryStream();
			if (!WriteMessage(message, stream))
			{
				return false;
			}

			return player.SendPacket(stream.GetBuffer(), sendType, channel);
		}

		/// <summary>
		/// Callback called when client accepts lobby join request from other steam user.
		/// </summary>
		/// <param name="request">The request.</param>
		private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t request)
		{
			JoinLobby(request.m_steamIDLobby, request.m_steamIDFriend);
		}

		/// <summary>
		/// Join a lobby with the specified ID.
		/// </summary>
		/// <param name="lobbyId">Lobby ID.</param>
		/// <param name="hostId">host ID.</param>
		public void JoinLobby(CSteamID lobbyId, CSteamID hostId)
		{
			SteamAPICall_t apiCall = SteamMatchmaking.JoinLobby(lobbyId);
			if (apiCall == SteamAPICall_t.Invalid)
			{
				Logger.Error($"Unable to join lobby {lobbyId}. JoinLobby call failed.");
				Mpgui.Instance.ShowMessageBox($"Failed to join lobby.\nPlease try again later.");
				return;
			}

			Logger.Debug("Setup player.");

			_timeSinceLastHeartbeat = 0.0f;
			_hostSteamId = hostId;

			_lobbyEnterCallResult.Set(apiCall);
		}

		/// <summary>
		/// Setup lobby to host a game.
		/// </summary>
		/// <returns>true if lobby setup request was properly sent, false otherwise</returns>
		public bool SetupLobby()
		{
			Logger.Debug("Setting up lobby.");
			SteamAPICall_t apiCall = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MAX_PLAYERS);
			if (apiCall == SteamAPICall_t.Invalid)
			{
				Logger.Error("Unable to create lobby.");
				return false;
			}
			Logger.Debug("Waiting for lobby create reply..");
			_lobbyCreatedCallResult.Set(apiCall);
			return true;
		}

		/// <summary>
		/// Leave current lobby.
		/// </summary>
		private void LeaveLobby()
		{
			SteamMatchmaking.LeaveLobby(_currentLobbyId);
			_currentLobbyId = CSteamID.Nil;
			_mode = Mode.None;
			_state = State.Idle;
			Players[_localPlayerId].Dispose();
			Players.Clear();
			Logger.Log("Left lobby.");
		}

		/// <summary>
		/// Invite player with given id to the lobby.
		/// </summary>
		/// <param name="invitee">The steam id of the player to invite.</param>
		/// <returns>true if player was invited, false otherwise</returns>
		public bool InviteToMyLobby(CSteamID invitee)
		{
			if (!IsHost)
			{
				return false;
			}
			return SteamMatchmaking.InviteUserToLobby(_currentLobbyId, invitee);
		}

		/// <summary>
		/// Is another player connected and playing in the session?
		/// </summary>
		/// <returns>true if there is another player connected and playing in the session, false otherwise</returns>
		public bool IsNetworkPlayerConnected()
		{
			return Players.Count > 1;
		}

		/// <summary>
		/// Cleanup remote player.
		/// </summary>
		public void CleanupPlayer(int playerId)
		{
			if (Players[playerId] == null)
			{
				return;
			}
			SteamNetworking.CloseP2PSessionWithUser(Players[playerId].SteamId);
			Players[playerId].Dispose();
			Players.Remove(playerId);
		}


		/// <summary>
		/// Disconnect from the active multiplayer session.
		/// </summary>
		public void Disconnect()
		{
			SendMessage(GetHostPlayer(), new DisconnectMessage(), EP2PSend.k_EP2PSendReliable);
			_connectionStartedTime = null;
			LeaveLobby();
		}

		/// <summary>
		/// Handle disconnect of a remote player.
		/// </summary>
		/// <param name="timeout">Was the disconnect caused by timeout?</param>
		private void HandleDisconnect(int playerId, bool timeout)
		{
			ShowLoadingScreen(false);

			if (IsHost)
			{
				string reason = timeout ? "timeout" : "part";
				MessagesList.AddMessage($"Player {Players[playerId].GetName()} disconnected. ({reason})", MessageSeverity.Info);

				CleanupPlayer(playerId);

				PlayerLeaveMessage msg = new PlayerLeaveMessage();
				msg.playerID = playerId;
				msg.reason = reason;
				BroadcastMessage(msg, EP2PSend.k_EP2PSendReliable);
			}

			// Go to main menu if we are normal player - the session just closed.

			if (IsPlayer && Players[playerId].SteamId == SteamUser.GetSteamID())
			{
				LeaveLobby();
				MpController.Instance.LoadLevel("MainMenu");

				if (timeout)
				{
					Mpgui.Instance.ShowMessageBox("Session timed out.");
				}
				else
				{
					Mpgui.Instance.ShowMessageBox("Host closed the session.");
				}
			}
		}

		/// <summary>
		/// Update connection state.
		/// </summary>
		private void UpdateHeartbeat()
		{
			if (!IsNetworkPlayerConnected())
			{
				return;
			}

			foreach (KeyValuePair<int, NetPlayer> player in Players)
			{
				_timeSinceLastHeartbeat += Time.deltaTime;

				if (_timeSinceLastHeartbeat >= TIMEOUT_TIME)
				{
					HandleDisconnect(player.Key, true);
				}
				else
				{
					_timeToSendHeartbeat -= Time.deltaTime;
					if (_timeToSendHeartbeat <= 0.0f)
					{
						HeartbeatMessage message = new HeartbeatMessage();
						message.clientClock = GetNetworkClock();
						BroadcastMessage(message, EP2PSend.k_EP2PSendReliable);

						_timeToSendHeartbeat = HEARTBEAT_INTERVAL;
					}
				}
			}
		}


		/// <summary>
		/// Process incomming network messages.
		/// </summary>
		private void ProcessMessages()
		{
			while (SteamNetworking.IsP2PPacketAvailable(out uint size))
			{
				if (size == 0)
				{
					Logger.Log("Received empty p2p packet");
					continue;
				}

				// TODO: Pre allocate this buffer and reuse it here - we don't want garbage collector to go crazy with that.

				byte[] data = new byte[size];

				if (!SteamNetworking.ReadP2PPacket(data, size, out uint msgSize, out CSteamID senderSteamId))
				{
					Logger.Error("Failed to read p2p packet!");
					continue;
				}

				if (msgSize != size || msgSize == 0)
				{
					Logger.Error("Invalid packet size");
					continue;
				}

				MemoryStream stream = new MemoryStream(data);
				BinaryReader reader = new BinaryReader(stream);

				uint protocolId = reader.ReadUInt32();
				if (protocolId != PROTOCOL_ID)
				{
					Logger.Error("The received message was not sent by MSCMP network layer.");
					continue;
				}

				byte messageId = reader.ReadByte();
				_statistics.RecordReceivedMessage(messageId, size);
				_netMessageHandler.ProcessMessage(messageId, senderSteamId, reader);
			}
		}

		/// <summary>
		/// Update network manager state.
		/// </summary>
		public void Update()
		{
			_statistics.NewFrame();

			if (!IsOnline)
			{
				return;
			}

			_netWorld.Update();
			UpdateHeartbeat();
			ProcessMessages();
			if (Game.PhoneManager.Instance != null)
			{
				Game.PhoneManager.Instance.OnUpdate();
			}

#if !PUBLIC_RELEASE
			if (Input.GetKeyDown(KeyCode.F8) && Players[1] != null)
			{
				NetLocalPlayer localPlayer = GetLocalPlayer();
				localPlayer.Teleport(Players[1].GetPosition(), Players[1].GetRotation());
			}
#endif

			foreach (KeyValuePair<int, NetPlayer> player in Players)
			{
				player.Value?.Update();
			}
		}

#if !PUBLIC_RELEASE
		/// <summary>
		/// Update network debug IMGUI.
		/// </summary>
		public void DrawDebugGui()
		{
			_statistics.Draw();
			_netWorld.UpdateImgui();
		}
#endif

		/// <summary>
		/// Draw player nametags.
		/// </summary>
		public void DrawNameTags()
		{
			if (Players.Count != 0)
			{
				foreach (KeyValuePair<int, NetPlayer> player in Players)
				{
					player.Value.DrawNametag(player.Key);
				}
			}
		}

		/// <summary>
		/// Reject remote player during connection phase.
		/// </summary>
		/// <param name="playerId"></param>
		/// <param name="reason">The rejection reason.</param>
		private void RejectPlayer(int playerId, string reason)
		{
			MessagesList.AddMessage($"Player {Players[playerId].GetName()} connection rejected. {reason}", MessageSeverity.Error);

			Logger.Error($"Player rejected. {reason}");
			SendHandshake(Players[playerId]);
			Players[playerId].Dispose();
			Players[playerId] = null;
		}

		/// <summary>
		/// Abort joinign the lobby during connection phase.
		/// </summary>
		/// <param name="reason">The abort reason.</param>
		private void AbortJoining(string reason)
		{
			string errorMessage = $"Failed to join lobby.\n{reason}";
			Mpgui.Instance.ShowMessageBox(errorMessage);
			Logger.Error(errorMessage);
			MpController.Instance.LoadLevel("MainMenu");
		}

		/// <summary>
		/// Process handshake message received from the given steam id.
		/// </summary>
		/// <param name="senderSteamId">The steam id of the sender.</param>
		/// <param name="msg">Hand shake message.</param>
		private void HandleHandshake(CSteamID senderSteamId, HandshakeMessage msg)
		{
			if (IsHost)
			{
				// Setup THE PLAYER

				_timeSinceLastHeartbeat = 0.0f;
				Players.Add(Players.Count, new NetPlayer(this, _netWorld, senderSteamId));
				int connectingPlayerId = Players.Count - 1;
				Logger.Log("Connecting player is now ID: " + (Players.Count - 1));

				// Check if version matches - if not ignore this player.

				if (msg.protocolVersion != _protocolVersion)
				{
					RejectPlayer(connectingPlayerId, $"Mod version mismatch.");
					return;
				}

				// Player can be spawned here safely. Host is already in game and all game objects are here.

				Players[connectingPlayerId].Spawn();
				SendHandshake(Players[connectingPlayerId]);

				MessagesList.AddMessage($"Player {Players[connectingPlayerId].GetName()} joined.", MessageSeverity.Info);

				Players[connectingPlayerId].HasHandshake = true;

				SendPlayerJoined(connectingPlayerId, Players[Players.Count - 1]);

			}
			else
			{
				// Check if protocol version matches.

				if (msg.protocolVersion != _protocolVersion)
				{
					string message;
					if (msg.protocolVersion > _protocolVersion)
					{
						message = "Host has newer version of the mod.";
					}
					else
					{
						message = "Host has older version of the mod.";
					}

					AbortJoining($"{message}\n(Your mod version: {_protocolVersion}, Host mod version: {msg.protocolVersion})");
					return;
				}

				// All is fine - load game world.

				MessagesList.AddMessage($"Connection established!", MessageSeverity.Info);

				MpController.Instance.LoadLevel("GAME");

				// Host will be spawned when game will be loaded and OnGameWorldLoad callback will be called.
			}

			_remoteClock = msg.clock;
			_connectionStartedTime = DateTime.UtcNow;
		}

		/// <summary>
		/// Sends handshake to the connected player.
		/// </summary>
		private void SendHandshake(NetPlayer player)
		{
			HandshakeMessage message = new HandshakeMessage();
			message.protocolVersion = _protocolVersion;
			message.clock = GetNetworkClock();
			SendMessage(player, message, EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Callback called when game world gets loaded.
		/// </summary>
		public void OnGameWorldLoad()
		{
			// If we are not online setup an lobby for players to connect.

			if (!IsOnline)
			{
				SetupLobby();
				return;
			}

			if (IsPlayer)
			{
				_netWorld.AskForFullWorldSync();
			}
		}

		/// <summary>
		/// Get local player object.
		/// </summary>
		/// <returns>Local player object.</returns>
		public NetLocalPlayer GetLocalPlayer()
		{
			return (NetLocalPlayer)Players[_localPlayerId];
		}

		/// <summary>
		/// Get network player object by steam id.
		/// </summary>
		/// <param name="steamId">The steam id used to find player for.</param>
		/// <returns>Network player object or null if there is not player matching given steam id.</returns>
		public NetPlayer GetPlayer(CSteamID steamId)
		{
			foreach (KeyValuePair<int, NetPlayer> player in Players)
			{
				if (player.Value?.SteamId == steamId)
				{
					return player.Value;
				}
			}
			return null;
		}


		/// <summary>
		/// Called after whole network world is loaded.
		/// </summary>
		public void OnNetworkWorldLoaded()
		{
			_state = State.Playing;
		}


		/// <summary>
		/// Get current p2p session state.
		/// </summary>
		/// <param name="sessionState">The session state.</param>
		/// <returns>true if session state is available, false otherwise</returns>
		public bool GetP2PSessionState(out P2PSessionState_t sessionState)
		{
			if (Players[1] == null)
			{
				sessionState = new P2PSessionState_t();
				return false;
			}
			return SteamNetworking.GetP2PSessionState(Players[1].SteamId, out sessionState);
		}

		/// <summary>
		/// Find player ID based on SteamID.
		/// </summary>
		/// <param name="steamId">Steam ID of player to find.</param>
		/// <returns></returns>
		public int GetPlayerIdBySteamId(CSteamID steamId)
		{
			foreach (KeyValuePair<int, NetPlayer> player in Players)
			{
				if (player.Value.SteamId == steamId)
				{
					return player.Key;
				}
			}
			return -1;
		}

		/// <summary>
		/// Send player joined message to connected players.
		/// </summary>
		/// <param name="player">Player who joined.</param>
		private void SendPlayerJoined(int playerId, NetPlayer player)
		{
			PlayerJoinMessage msg = new PlayerJoinMessage();
			msg.playerID = playerId;
			msg.steamID = player.SteamId.m_SteamID;
			BroadcastMessage(msg, EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Request current game lobbies.
		/// </summary>
		public void RequestLobbies()
		{
			Logger.Log("Trying to get list of available lobbies ...");
			SteamAPICall_t apiCall = SteamMatchmaking.RequestLobbyList();
			_lobbyListResult.Set(apiCall);
		}

		/// <summary>
		/// Lobby list result.
		/// </summary>
		/// <param name="result">Lobby list.</param>
		/// <param name="ioFailure"></param>
		private void OnLobbyList(LobbyMatchList_t result, bool ioFailure)
		{
			Logger.Log("Found " + result.m_nLobbiesMatching + " lobbies!");

			_lobbyIDs.Clear();
			_lobbyNames.Clear();
			for (int i = 0; i < result.m_nLobbiesMatching; i++)
			{
				CSteamID lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
				CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(lobbyId);
				if (lobbyOwner.m_SteamID != 0)
				{
					_lobbyIDs.Add(lobbyId);
					_lobbyOwners.Add(lobbyOwner);
					Logger.Log("Lobby ID of index " + i + ": " + lobbyId + " - Owner: " + lobbyOwner);
					bool success = SteamMatchmaking.RequestLobbyData(lobbyId);
					if (!success)
					{
						Logger.Error("Failed to get lobby info!");
					}

					// Temp until lobby data callback is fixed!
					int numPlayers = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
					_lobbyNames.Add("Lobby (" + numPlayers + "/16)");
				}
			}
			Mpgui.Instance.LobbyNames = _lobbyNames;
		}

		/// <summary>
		/// Lobby info result.
		/// </summary>
		/// <param name="result">Lobby info.</param>
		/// <param name="ioFailure"></param>
		private void OnGetLobbyInfo(LobbyDataUpdate_t result, bool ioFailure)
		{
			// This doesn't seem to call just yet!
			for (int i = 0; i < _lobbyIDs.Count; i++)
			{
				if (_lobbyIDs[i].m_SteamID == result.m_ulSteamIDLobby)
				{
					Logger.Log("Lobby " + i + " :: " + SteamMatchmaking.GetLobbyData((CSteamID)_lobbyIDs[i].m_SteamID, "name"));
					int numPlayers = SteamMatchmaking.GetNumLobbyMembers((CSteamID)_lobbyIDs[i]);
					_lobbyNames.Add("Unknown name (" + numPlayers + "/16)");
					return;
				}
			}
			Mpgui.Instance.LobbyNames = _lobbyNames;
		}

		/// <summary>
		/// Join lobby from UI buttons.
		/// </summary>
		/// <param name="index">Index of the button.</param>
		public void JoinLobbyFromUi(int index)
		{
			JoinLobby(_lobbyIDs[index], _lobbyOwners[index]);
		}

		/// <summary>
		/// Return the host NetPlayer.
		/// </summary>
		/// <returns>Host NetPlayer.</returns>
		public NetPlayer GetHostPlayer()
		{
			return Players[0];
		}

		/// <summary>
		/// Return NetPlayer of a player by their Player ID.
		/// </summary>
		/// <returns>NetPlayer of player.</returns>
		public NetPlayer GetPlayerByPlayerId(int playerId)
		{
			return Players[playerId];
		}
	}
}
