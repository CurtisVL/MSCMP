using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MSCMP.UI;

namespace MSCMP.Network {
	class NetManager {
		private const int MAX_PLAYERS = 16;
		private static int PROTOCOL_VERSION = 2;
		private const uint PROTOCOL_ID = 0x6d73636d;

		private Steamworks.Callback<Steamworks.GameLobbyJoinRequested_t> gameLobbyJoinRequestedCallback = null;
		private Steamworks.Callback<Steamworks.P2PSessionRequest_t> p2pSessionRequestCallback = null;
		private Steamworks.Callback<Steamworks.P2PSessionConnectFail_t> p2pConnectFailCallback = null;
		private Steamworks.CallResult<Steamworks.LobbyCreated_t> lobbyCreatedCallResult = null;
		private Steamworks.CallResult<Steamworks.LobbyEnter_t> lobbyEnterCallResult = null;
		private Steamworks.CallResult<Steamworks.LobbyMatchList_t> lobbyListResult = null;
		private Steamworks.CallResult<Steamworks.LobbyDataUpdate_t> lobbyDataResult = null;
		public enum Mode {
			None,
			Host,
			Player
		}

		public enum State {
			Idle,
			CreatingLobby,
			LoadingGameWorld,
			Playing
		}

		private State state = State.Idle;
		private Mode mode = Mode.None;

		public bool IsHost {
			get { return mode == Mode.Host; }
		}
		public bool IsPlayer {
			get { return mode == Mode.Player; }
		}
		public bool IsOnline {
			get { return mode != Mode.None; }
		}
		public bool IsPlaying {
			get { return state == State.Playing;  }
		}
		private Steamworks.CSteamID currentLobbyId = Steamworks.CSteamID.Nil;
		
		//private NetPlayer[] players = new NetPlayer[MAX_PLAYERS];
		/// <summary>
		/// Stores player's ID, as well as NetPlayer.
		/// </summary>
		public Dictionary<int, NetPlayer> players = new Dictionary<int, NetPlayer>();

		/// <summary>
		/// The interval between sending individual heartbeat.
		/// </summary>
		const float HEARTBEAT_INTERVAL = 5.0f;

		/// <summary>
		/// Timeout time of the connection.
		/// </summary>
		const float TIMEOUT_TIME = 60.0f; // This was raised as too many people had issues with joining onn slow PCs. A better solution will be implemented in the future.

		/// <summary>
		/// How many seconds left before sending next heartbeat?
		/// </summary>
		float timeToSendHeartbeat = 0.0f;

		/// <summary>
		/// How many seconds passed since last heart beat was received.
		/// </summary>
		float timeSinceLastHeartbeat = 0.0f;

		/// <summary>
		/// The value of the clock on the remote players' computer.
		/// </summary>
		ulong remoteClock = 0;

		/// <summary>
		/// Current ping value.
		/// </summary>
		uint ping = 0;

		/// <summary>
		/// The time when network manager was created in UTC.
		/// </summary>
		DateTime netManagerCreationTime;

		/// <summary>
		/// Network world.
		/// </summary>
		NetWorld netWorld = null;

		/// <summary>
		/// The network message handler.
		/// </summary>
		NetMessageHandler netMessageHandler = null;

		/// <summary>
		/// Get net manager's message handler.
		/// </summary>
		public NetMessageHandler MessageHandler {
			get { return netMessageHandler;  }
		}

		public static NetManager Instance;

		/// <summary>
		/// Network statistics object.
		/// </summary>
		NetStatistics statistics;

		/// <summary>
		/// The time the connection was started in UTC.
		/// </summary>
		DateTime? connectionStartedTime;

		/// <summary>
		/// SteamID of host, used when connecting to a lobby.
		/// </summary>
		Steamworks.CSteamID hostSteamID;

		/// <summary>
		/// Local player ID of this client.
		/// </summary>
		int localPlayerID = -1;

		/// <summary>
		/// Get ticks since connection started.
		/// </summary>
		public ulong TicksSinceConnectionStarted {
			get {
				Client.Assert(connectionStartedTime != null, "Attempting to get ticks since connection started, but connection started time is null! (Not connected?)");
				return (ulong)((DateTime.UtcNow - this.connectionStartedTime)?.Ticks);
			}
		}

		/// <summary>
		/// Lobby IDs found from current lobbies request.
		/// </summary>
		List<Steamworks.CSteamID> lobbyIDs = new List<Steamworks.CSteamID>();

		/// <summary>
		/// Lobby names from current lobbies request.
		/// </summary>
		List<string> lobbyNames = new List<string>();

		/// <summary>
		/// Lobby owner SteamIDs.
		/// </summary>
		List<Steamworks.CSteamID> lobbyOwners = new List<Steamworks.CSteamID>();

		public NetManager() {
			Instance = this;
			statistics = new NetStatistics(this);
			netManagerCreationTime = DateTime.UtcNow;
			netMessageHandler = new NetMessageHandler(this);
			netWorld = new NetWorld(this);

			// Hopefully this will fix people playing with different mod versions!
			string versionFull = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			int buildNumber = Convert.ToInt32(versionFull.Substring(versionFull.LastIndexOf('.') + 1));
			PROTOCOL_VERSION = buildNumber;

			p2pSessionRequestCallback = Steamworks.Callback<Steamworks.P2PSessionRequest_t>.Create(OnP2PSessionRequest);
			p2pConnectFailCallback = Steamworks.Callback<Steamworks.P2PSessionConnectFail_t>.Create(OnP2PConnectFail);
			gameLobbyJoinRequestedCallback = Steamworks.Callback<Steamworks.GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
			lobbyCreatedCallResult = new Steamworks.CallResult<Steamworks.LobbyCreated_t>(OnLobbyCreated);
			lobbyEnterCallResult = new Steamworks.CallResult<Steamworks.LobbyEnter_t>(OnLobbyEnter);
			lobbyListResult = new Steamworks.CallResult<Steamworks.LobbyMatchList_t>(OnLobbyList);
			lobbyDataResult = new Steamworks.CallResult<Steamworks.LobbyDataUpdate_t>(OnGetLobbyInfo);

			RegisterProtocolMessagesHandlers();

			RequestLobbies();
		}

		/// <summary>
		/// Handle steam networking P2P connect fail callback.
		/// </summary>
		/// <param name="result">The callback result.</param>
		void OnP2PConnectFail(Steamworks.P2PSessionConnectFail_t result) {
			Logger.Error($"P2P Connection failed, session error: {Utils.P2PSessionErrorToString((Steamworks.EP2PSessionError)result.m_eP2PSessionError)}, remote: {result.m_steamIDRemote}");
		}

		/// <summary>
		/// Handle steam networking P2P session request callback.
		/// </summary>
		/// <param name="result">The callback result.</param>
		void OnP2PSessionRequest(Steamworks.P2PSessionRequest_t result) {
			if (Steamworks.SteamNetworking.AcceptP2PSessionWithUser(result.m_steamIDRemote)) {
				Logger.Debug($"Accepted p2p session with {result.m_steamIDRemote}");
			}
			else {
				Logger.Error($"Failed to accept P2P session with {result.m_steamIDRemote}");
			}
		}

		/// <summary>
		/// Handle result of create lobby operation.
		/// </summary>
		/// <param name="result">The operation result.</param>
		/// <param name="ioFailure">Did IO failure happen?</param>
		void OnLobbyCreated(Steamworks.LobbyCreated_t result, bool ioFailure) {
			if (result.m_eResult != Steamworks.EResult.k_EResultOK || ioFailure) {
				Logger.Debug($"Failed to create lobby. (result: {result.m_eResult}, io failure: {ioFailure})");

				MPGUI.Instance.ShowMessageBox($"Failed to create lobby due to steam error.\n{result.m_eResult}/{ioFailure}", () => {
					MPController.Instance.LoadLevel("MainMenu");
				});
				return;
			}

			Logger.Debug($"Lobby has been created, lobby id: {result.m_ulSteamIDLobby}");
			MessagesList.AddMessage("Session started.", MessageSeverity.Info);

			// Setup local player.
			players.Add(0, new NetLocalPlayer(this, netWorld, Steamworks.SteamUser.GetSteamID()));
			localPlayerID = 0;

			mode = Mode.Host;
			state = State.Playing;
			currentLobbyId = new Steamworks.CSteamID(result.m_ulSteamIDLobby);
		}

		/// <summary>
		/// Handle result of join lobby operation.
		/// </summary>
		/// <param name="result">The operation result.</param>
		/// <param name="ioFailure">Did IO failure happen?</param>
		void OnLobbyEnter(Steamworks.LobbyEnter_t result, bool ioFailure) {
			if (ioFailure || result.m_EChatRoomEnterResponse != (uint)Steamworks.EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess) {
				Logger.Error("Failed to join lobby. (reponse: {result.m_EChatRoomEnterResponse}, ioFailure: {ioFailure})");
				MPGUI.Instance.ShowMessageBox($"Failed to join lobby.\n(reponse: {result.m_EChatRoomEnterResponse}, ioFailure: {ioFailure})");
				return;
			}

			Logger.Debug("Entered lobby: " + result.m_ulSteamIDLobby);

			MessagesList.AddMessage("Entered lobby.", MessageSeverity.Info);

			// Setup host player.
			players.Add(0, new NetPlayer(this, netWorld, hostSteamID));
			Logger.Debug("Setup host player as ID: 0");

			mode = Mode.Player;
			state = State.LoadingGameWorld;
			currentLobbyId = new Steamworks.CSteamID(result.m_ulSteamIDLobby);

			ShowLoadingScreen(true);
			SendHandshake(players[0]);
		}

		/// <summary>
		/// Register protocol related network messages handlers.
		/// </summary>
		void RegisterProtocolMessagesHandlers() {
			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.HandshakeMessage msg) => {
				HandleHandshake(sender, msg);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.HeartbeatMessage msg) => {
				var message = new Messages.HeartbeatResponseMessage();
				message.clientClock = msg.clientClock;
				message.clock = GetNetworkClock();
				BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendReliable);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.HeartbeatResponseMessage msg) => {
				ping = (uint)(GetNetworkClock() - msg.clientClock);

				// TODO: Some smart lag compensation.
				remoteClock = msg.clock;

				timeSinceLastHeartbeat = 0.0f;
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.PhoneMessage msg) => {
				Game.PhoneManager.Instance.PhoneCall(msg.topic, msg.timesToRing);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.DartSyncMessage msg) => {
				Game.MapManager.Instance.SyncDartsHandler(msg.darts);
			});
			
			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.DisconnectMessage msg) => {
				HandleDisconnect(GetPlayerIDBySteamID(sender), false);
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.ConnectedPlayersMessage msg) => {
				int i = 0;
				foreach (int newPlayerID in msg.playerIDs) {
					Steamworks.CSteamID localSteamID = Steamworks.SteamUser.GetSteamID();
					Steamworks.CSteamID newPlayerSteamID = new Steamworks.CSteamID(msg.steamIDs[i]);
					// If player is not host or local player, setup new player.
					if (newPlayerSteamID != hostSteamID && newPlayerSteamID != localSteamID) {
						players.Add(newPlayerID, new NetPlayer(this, netWorld, newPlayerSteamID));
						players[newPlayerID].Spawn();
						Logger.Debug("Setup new player at ID: " + newPlayerID);
					}
					i++;
				}
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.PlayerJoinMessage msg) => {
				if (NetWorld.Instance.playerIsLoading) {
					if (msg.steamID == Steamworks.SteamUser.GetSteamID().m_SteamID && localPlayerID == -1) {
						players.Add(msg.playerID, new NetLocalPlayer(this, netWorld, new Steamworks.CSteamID(msg.steamID)));
						localPlayerID = msg.playerID;
						Logger.Debug("Setup local player as ID: " + msg.playerID);
					}
					else {
						Logger.Debug("Ignored connecting player as curent player is still loading!");
					}
					return;
				}
				Steamworks.CSteamID newPlayerSteamID = new Steamworks.CSteamID(msg.steamID);
				if (newPlayerSteamID != hostSteamID && newPlayerSteamID != GetLocalPlayer().SteamId) {
					players.Add(msg.playerID, new NetPlayer(this, netWorld, newPlayerSteamID));
					players[msg.playerID].Spawn();
					MessagesList.AddMessage($"Player {players[msg.playerID].GetName()} joined.", MessageSeverity.Info);
					Logger.Debug("New player connected at ID: " + msg.playerID);
				}
			});

			netMessageHandler.BindMessageHandler((Steamworks.CSteamID sender, Messages.PlayerLeaveMessage msg) => {
				if (NetWorld.Instance.playerIsLoading) {
					Logger.Debug("Ignored connecting player as curent player is still loading!");
					return;
				}
				MessagesList.AddMessage($"Player {players[msg.playerID].GetName()} disconnected. ({msg.reason})", MessageSeverity.Info);
				CleanupPlayer(msg.playerID);
			});
		}

		/// <summary>
		/// Show loading screen.
		/// </summary>
		/// <param name="show">Show or hide loading screen.</param>
		private void ShowLoadingScreen(bool show) {
			if (Application.loadedLevelName == "MainMenu") {
				// This is not that slow as you may think - seriously!

				GameObject []gos = Resources.FindObjectsOfTypeAll<GameObject>();
				GameObject loadingScreen = null;
				foreach (GameObject go in gos) {
					if (go.transform.parent == null && go.name == "Loading") {
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
		public ulong GetNetworkClock() {
			return (ulong)((DateTime.UtcNow - this.netManagerCreationTime).TotalMilliseconds);
		}

		/// <summary>
		/// Writes given network message into a given stream.
		/// </summary>
		/// <param name="message">The message to write.</param>
		/// <param name="stream">The stream to write message to.</param>
		/// <returns>true if message was written successfully, false otherwise</returns>
		private bool WriteMessage(INetMessage message, MemoryStream stream) {
			BinaryWriter writer = new BinaryWriter(stream);

			writer.Write(PROTOCOL_ID);
			writer.Write((byte)message.MessageId);
			if (!message.Write(writer)) {
				Client.FatalError("Failed to write network message " + message.MessageId);
				return false;
			}

			statistics.RecordSendMessage(message.MessageId, stream.Length);
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
		public bool BroadcastMessage<T>(T message, Steamworks.EP2PSend sendType, int channel = 0) where T : INetMessage {
			if (players.Count == 0) {
				return false;
			}

			MemoryStream stream = new MemoryStream();
			if (!WriteMessage(message, stream)) {
				return false;
			}

			foreach (var player in players) {
				if (player.Value is NetLocalPlayer) {
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
		public bool SendMessage<T>(NetPlayer player, T message, Steamworks.EP2PSend sendType, int channel = 0) where T : INetMessage {
			if (player == null) {
				return false;
			}

			MemoryStream stream = new MemoryStream();
			if (!WriteMessage(message, stream)) {
				return false;
			}

			return player.SendPacket(stream.GetBuffer(), sendType, channel);
		}

		/// <summary>
		/// Callback called when client accepts lobby join request from other steam user.
		/// </summary>
		/// <param name="request">The request.</param>
		private void OnGameLobbyJoinRequested(Steamworks.GameLobbyJoinRequested_t request) {
			JoinLobby(request.m_steamIDLobby, request.m_steamIDFriend);
		}

		/// <summary>
		/// Join a lobby with the specified ID.
		/// </summary>
		/// <param name="lobbyID">Lobby ID.</param>
		/// <param name="hostID">host ID.</param>
		public void JoinLobby(Steamworks.CSteamID lobbyID, Steamworks.CSteamID hostID) {
			Steamworks.SteamAPICall_t apiCall = Steamworks.SteamMatchmaking.JoinLobby(lobbyID);
			if (apiCall == Steamworks.SteamAPICall_t.Invalid) {
				Logger.Error($"Unable to join lobby {lobbyID}. JoinLobby call failed.");
				MPGUI.Instance.ShowMessageBox($"Failed to join lobby.\nPlease try again later.");
				return;
			}

			Logger.Debug("Setup player.");

			timeSinceLastHeartbeat = 0.0f;
			hostSteamID = hostID;

			lobbyEnterCallResult.Set(apiCall);
		}
	
		/// <summary>
		/// Setup lobby to host a game.
		/// </summary>
		/// <returns>true if lobby setup request was properly sent, false otherwise</returns>
		public bool SetupLobby() {
			Logger.Debug("Setting up lobby.");
			Steamworks.SteamAPICall_t apiCall = Steamworks.SteamMatchmaking.CreateLobby(Steamworks.ELobbyType.k_ELobbyTypeFriendsOnly, MAX_PLAYERS);
			if (apiCall == Steamworks.SteamAPICall_t.Invalid) {
				Logger.Error("Unable to create lobby.");
				return false;
			}
			Logger.Debug("Waiting for lobby create reply..");
			lobbyCreatedCallResult.Set(apiCall);
			return true;
		}

		/// <summary>
		/// Leave current lobby.
		/// </summary>
		private void LeaveLobby() {
			Steamworks.SteamMatchmaking.LeaveLobby(currentLobbyId);
			currentLobbyId = Steamworks.CSteamID.Nil;
			mode = Mode.None;
			state = State.Idle;
			players[localPlayerID].Dispose();
			players.Clear();
			Logger.Log("Left lobby.");
		}

		/// <summary>
		/// Invite player with given id to the lobby.
		/// </summary>
		/// <param name="invitee">The steam id of the player to invite.</param>
		/// <returns>true if player was invited, false otherwise</returns>
		public bool InviteToMyLobby(Steamworks.CSteamID invitee) {
			if (!IsHost) {
				return false;
			}
			return Steamworks.SteamMatchmaking.InviteUserToLobby(currentLobbyId, invitee);
		}

		/// <summary>
		/// Is another player connected and playing in the session?
		/// </summary>
		/// <returns>true if there is another player connected and playing in the session, false otherwise</returns>
		public bool IsNetworkPlayerConnected() {
			return players.Count > 1;
		}

		/// <summary>
		/// Cleanup remote player.
		/// </summary>
		public void CleanupPlayer(int playerID) {
			if (players[playerID] == null) {
				return;
			}
			Steamworks.SteamNetworking.CloseP2PSessionWithUser(players[playerID].SteamId);
			players[playerID].Dispose();
			players.Remove(playerID);
		}


		/// <summary>
		/// Disconnect from the active multiplayer session.
		/// </summary>
		public void Disconnect() {
			SendMessage(GetHostPlayer(), new Messages.DisconnectMessage(), Steamworks.EP2PSend.k_EP2PSendReliable);
			connectionStartedTime = null;
			LeaveLobby();
		}

		/// <summary>
		/// Handle disconnect of a remote player.
		/// </summary>
		/// <param name="timeout">Was the disconnect caused by timeout?</param>
		private void HandleDisconnect(int playerID, bool timeout) {
			ShowLoadingScreen(false);

			if (IsHost) {
				string reason = timeout ? "timeout" : "part";
				MessagesList.AddMessage($"Player {players[playerID].GetName()} disconnected. ({reason})", MessageSeverity.Info);

				CleanupPlayer(playerID);

				Messages.PlayerLeaveMessage msg = new Messages.PlayerLeaveMessage();
				msg.playerID = playerID;
				msg.reason = reason;
				BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
			}

			// Go to main menu if we are normal player - the session just closed.

			if (IsPlayer && players[playerID].SteamId == Steamworks.SteamUser.GetSteamID()) {
				LeaveLobby();
				MPController.Instance.LoadLevel("MainMenu");

				if (timeout) {
					MPGUI.Instance.ShowMessageBox("Session timed out.");
				}
				else {
					MPGUI.Instance.ShowMessageBox("Host closed the session.");
				}
			}
		}

		/// <summary>
		/// Update connection state.
		/// </summary>
		private void UpdateHeartbeat() {
			if (!IsNetworkPlayerConnected()) {
				return;
			}

			foreach (var player in players) {
				timeSinceLastHeartbeat += Time.deltaTime;

				if (timeSinceLastHeartbeat >= TIMEOUT_TIME) {
					HandleDisconnect(player.Key, true);
				}
				else {
					timeToSendHeartbeat -= Time.deltaTime;
					if (timeToSendHeartbeat <= 0.0f) {
						Messages.HeartbeatMessage message = new Messages.HeartbeatMessage();
						message.clientClock = GetNetworkClock();
						BroadcastMessage(message, Steamworks.EP2PSend.k_EP2PSendReliable);

						timeToSendHeartbeat = HEARTBEAT_INTERVAL;
					}
				}
			}
		}


		/// <summary>
		/// Process incomming network messages.
		/// </summary>
		private void ProcessMessages() {
			uint size = 0;
			while (Steamworks.SteamNetworking.IsP2PPacketAvailable(out size)) {
				if (size == 0) {
					Logger.Log("Received empty p2p packet");
					continue;
				}

				// TODO: Pre allocate this buffer and reuse it here - we don't want garbage collector to go crazy with that.

				byte[] data = new byte[size];

				uint msgSize = 0;
				Steamworks.CSteamID senderSteamId = Steamworks.CSteamID.Nil;
				if (!Steamworks.SteamNetworking.ReadP2PPacket(data, size, out msgSize, out senderSteamId)) {
					Logger.Error("Failed to read p2p packet!");
					continue;
				}

				if (msgSize != size || msgSize == 0) {
					Logger.Error("Invalid packet size");
					continue;
				}

				MemoryStream stream = new MemoryStream(data);
				BinaryReader reader = new BinaryReader(stream);

				uint protocolId = reader.ReadUInt32();
				if (protocolId != PROTOCOL_ID) {
					Logger.Error("The received message was not sent by MSCMP network layer.");
					continue;
				}

				byte messageId = reader.ReadByte();
				statistics.RecordReceivedMessage(messageId, size);
				netMessageHandler.ProcessMessage(messageId, senderSteamId, reader);
			}
		}

		/// <summary>
		/// Update network manager state.
		/// </summary>
		public void Update() {
			statistics.NewFrame();

			if (!IsOnline) {
				return;
			}

			netWorld.Update();
			UpdateHeartbeat();
			ProcessMessages();
			if (Game.PhoneManager.Instance != null) {
				Game.PhoneManager.Instance.OnUpdate();
			}

#if !PUBLIC_RELEASE
			if (Input.GetKeyDown(KeyCode.F8) && players[1] != null) {
				var localPlayer = GetLocalPlayer();
				localPlayer.Teleport(players[1].GetPosition(), players[1].GetRotation());
			}
#endif

			foreach (var player in players) {
				player.Value?.Update();
			}
		}

#if !PUBLIC_RELEASE
		/// <summary>
		/// Update network debug IMGUI.
		/// </summary>
		public void DrawDebugGUI() {
			statistics.Draw();
			netWorld.UpdateIMGUI();
		}
#endif

		/// <summary>
		/// Draw player nametags.
		/// </summary>
		public void DrawNameTags() {
			if (players.Count != 0) {
				foreach (var player in players) {
					player.Value.DrawNametag(player.Key);
				}
			}
		}

		/// <summary>
		/// Reject remote player during connection phase.
		/// </summary>
		/// <param name="reason">The rejection reason.</param>
		void RejectPlayer(int playerID, string reason) {
			MessagesList.AddMessage($"Player {players[playerID].GetName()} connection rejected. {reason}", MessageSeverity.Error);

			Logger.Error($"Player rejected. {reason}");
			SendHandshake(players[playerID]);
			players[playerID].Dispose();
			players[playerID] = null;
		}

		/// <summary>
		/// Abort joinign the lobby during connection phase.
		/// </summary>
		/// <param name="reason">The abort reason.</param>
		void AbortJoining(string reason) {
			string errorMessage = $"Failed to join lobby.\n{reason}";
			MPGUI.Instance.ShowMessageBox(errorMessage);
			Logger.Error(errorMessage);
			MPController.Instance.LoadLevel("MainMenu");
		}

		/// <summary>
		/// Process handshake message received from the given steam id.
		/// </summary>
		/// <param name="senderSteamId">The steam id of the sender.</param>
		/// <param name="msg">Hand shake message.</param>
		private void HandleHandshake(Steamworks.CSteamID senderSteamId, Messages.HandshakeMessage msg) {
			if (IsHost) {
				// Setup THE PLAYER

				timeSinceLastHeartbeat = 0.0f;
				players.Add(players.Count, new NetPlayer(this, netWorld, senderSteamId));
				int connectingPlayerID = players.Count - 1;
				Logger.Log("Connecting player is now ID: " + (players.Count - 1));

				// Check if version matches - if not ignore this player.

				if (msg.protocolVersion != PROTOCOL_VERSION) {
					RejectPlayer(connectingPlayerID, $"Mod version mismatch.");
					return;
				}

				// Player can be spawned here safely. Host is already in game and all game objects are here.

				players[connectingPlayerID].Spawn();
				SendHandshake(players[connectingPlayerID]);

				MessagesList.AddMessage($"Player {players[connectingPlayerID].GetName()} joined.", MessageSeverity.Info);

				players[connectingPlayerID].hasHandshake = true;

				SendPlayerJoined(connectingPlayerID, players[players.Count - 1]);

			}
			else {
				// Check if protocol version matches.

				if (msg.protocolVersion != PROTOCOL_VERSION) {
					string message;
					if (msg.protocolVersion > PROTOCOL_VERSION) {
						message = "Host has newer version of the mod.";
					}
					else {
						message = "Host has older version of the mod.";
					}

					AbortJoining($"{message}\n(Your mod version: {PROTOCOL_VERSION}, Host mod version: {msg.protocolVersion})");
					return;
				}

				// All is fine - load game world.

				MessagesList.AddMessage($"Connection established!", MessageSeverity.Info);

				MPController.Instance.LoadLevel("GAME");

				// Host will be spawned when game will be loaded and OnGameWorldLoad callback will be called.
			}

			remoteClock = msg.clock;
			connectionStartedTime = DateTime.UtcNow;
		}

		/// <summary>
		/// Sends handshake to the connected player.
		/// </summary>
		private void SendHandshake(NetPlayer player) {
			Messages.HandshakeMessage message = new Messages.HandshakeMessage();
			message.protocolVersion		= PROTOCOL_VERSION;
			message.clock				= GetNetworkClock();
			SendMessage(player, message, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Callback called when game world gets loaded.
		/// </summary>
		public void OnGameWorldLoad() {
			// If we are not online setup an lobby for players to connect.

			if (!IsOnline) {
				SetupLobby();
				return;
			}

			if (IsPlayer) {
				netWorld.AskForFullWorldSync();
			}
		}

		/// <summary>
		/// Get local player object.
		/// </summary>
		/// <returns>Local player object.</returns>
		public NetLocalPlayer GetLocalPlayer() {
			return (NetLocalPlayer)players[localPlayerID];
		}

		/// <summary>
		/// Get network player object by steam id.
		/// </summary>
		/// <param name="steamId">The steam id used to find player for.</param>
		/// <returns>Network player object or null if there is not player matching given steam id.</returns>
		public NetPlayer GetPlayer(Steamworks.CSteamID steamId) {
			foreach (var player in players) {
				if (player.Value?.SteamId == steamId) {
					return player.Value;
				}
			}
			return null;
		}


		/// <summary>
		/// Called after whole network world is loaded.
		/// </summary>
		public void OnNetworkWorldLoaded() {
			state = State.Playing;
		}


		/// <summary>
		/// Get current p2p session state.
		/// </summary>
		/// <param name="sessionState">The session state.</param>
		/// <returns>true if session state is available, false otherwise</returns>
		public bool GetP2PSessionState(out Steamworks.P2PSessionState_t sessionState) {
			if (players[1] == null) {
				sessionState = new Steamworks.P2PSessionState_t();
				return false;
			}
			return Steamworks.SteamNetworking.GetP2PSessionState(players[1].SteamId, out sessionState);
		}

		/// <summary>
		/// Find player ID based on SteamID.
		/// </summary>
		/// <param name="steamID">Steam ID of player to find.</param>
		/// <returns></returns>
		public int GetPlayerIDBySteamID(Steamworks.CSteamID steamID) {
			foreach (var player in players) {
				if (player.Value.SteamId == steamID) {
					return player.Key;
				}
			}
			return -1;
		}

		/// <summary>
		/// Send player joined message to connected players.
		/// </summary>
		/// <param name="player">Player who joined.</param>
		private void SendPlayerJoined(int playerID, NetPlayer player) {
			Messages.PlayerJoinMessage msg = new Messages.PlayerJoinMessage();
			msg.playerID = playerID;
			msg.steamID = player.SteamId.m_SteamID;
			BroadcastMessage(msg, Steamworks.EP2PSend.k_EP2PSendReliable);
		}

		/// <summary>
		/// Request current game lobbies.
		/// </summary>
		public void RequestLobbies() {
			Logger.Log("Trying to get list of available lobbies ...");
			Steamworks.SteamAPICall_t apiCall = Steamworks.SteamMatchmaking.RequestLobbyList();
			lobbyListResult.Set(apiCall);
		}

		/// <summary>
		/// Lobby list result.
		/// </summary>
		/// <param name="result">Lobby list.</param>
		void OnLobbyList(Steamworks.LobbyMatchList_t result, bool ioFailure) {
			Logger.Log("Found " + result.m_nLobbiesMatching + " lobbies!");

			lobbyIDs.Clear();
			lobbyNames.Clear();
			for (int i = 0; i < result.m_nLobbiesMatching; i++) {
				Steamworks.CSteamID lobbyID = Steamworks.SteamMatchmaking.GetLobbyByIndex(i);
				Steamworks.CSteamID lobbyOwner = Steamworks.SteamMatchmaking.GetLobbyOwner(lobbyID);
				if (lobbyOwner.m_SteamID != 0) {
					lobbyIDs.Add(lobbyID);
					lobbyOwners.Add(lobbyOwner);
					Logger.Log("Lobby ID of index " + i + ": " + lobbyID + " - Owner: " + lobbyOwner);
					bool success = Steamworks.SteamMatchmaking.RequestLobbyData(lobbyID);
					if (!success) {
						Logger.Error("Failed to get lobby info!");
					}

					// Temp until lobby data callback is fixed!
					int numPlayers = Steamworks.SteamMatchmaking.GetNumLobbyMembers(lobbyID);
					lobbyNames.Add("Lobby (" + numPlayers + "/16)");
				}
			}
			MPGUI.Instance.LobbyNames = lobbyNames;
		}

		/// <summary>
		/// Lobby info result.
		/// </summary>
		/// <param name="result">Lobby info.</param>
		void OnGetLobbyInfo(Steamworks.LobbyDataUpdate_t result, bool ioFailure) {
			// This doesn't seem to call just yet!
			for (int i = 0; i < lobbyIDs.Count; i++) {
				if (lobbyIDs[i].m_SteamID == result.m_ulSteamIDLobby) {
					Logger.Log("Lobby " + i + " :: " + Steamworks.SteamMatchmaking.GetLobbyData((Steamworks.CSteamID)lobbyIDs[i].m_SteamID, "name"));
					int numPlayers = Steamworks.SteamMatchmaking.GetNumLobbyMembers((Steamworks.CSteamID)lobbyIDs[i]);
					lobbyNames.Add("Unknown name (" + numPlayers + "/16)");
					return;
				}
			}
			MPGUI.Instance.LobbyNames = lobbyNames;
		}

		/// <summary>
		/// Join lobby from UI buttons.
		/// </summary>
		/// <param name="index">Index of the button.</param>
		public void JoinLobbyFromUI(int index) {
			JoinLobby(lobbyIDs[index], lobbyOwners[index]);
		}

		/// <summary>
		/// Return the host NetPlayer.
		/// </summary>
		/// <returns>Host NetPlayer.</returns>
		public NetPlayer GetHostPlayer() {
			return players[0];
		}

		/// <summary>
		/// Return NetPlayer of a player by their Player ID.
		/// </summary>
		/// <returns>NetPlayer of player.</returns>
		public NetPlayer GetPlayerByPlayerID(int playerID) {
			return players[playerID];
		}
	}
}
