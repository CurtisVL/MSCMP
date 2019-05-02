using MSCMP.Game;
using MSCMP.Network;
using MSCMP.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP
{
	/// <summary>
	/// Main multiplayer mode controller component.
	/// </summary>
	internal class MpController
		: MonoBehaviour
	{
		public static MpController Instance;

		/// <summary>
		/// Object managing whole networking.
		/// </summary>
		private NetManager _netManager;

		/// <summary>
		/// Name of the currently loaded level.
		/// </summary>
		private string _currentLevelName = "";

		/// <summary>
		/// Current scroll value of the invite panel.
		/// </summary>
		private Vector2 _friendsScrollViewPos;

		/// <summary>
		/// The mod logo texture.
		/// </summary>
		private Texture2D _modLogo;

		/// <summary>
		/// Game world manager object.
		/// </summary>
		private readonly GameWorld _gameWorld = new GameWorld();

		/// <summary>
		/// Console object.
		/// </summary>
		private readonly UI.Console _console = new UI.Console();

		private MpController()
		{
			Instance = this;
		}

		~MpController()
		{
			Instance = null;
		}

		private void Start()
		{
			Steamworks.SteamAPI.Init();

			DontDestroyOnLoad(gameObject);

			_netManager = new NetManager();

			_modLogo = Client.LoadAsset<Texture2D>("Assets/Textures/MSCMPLogo.png");

			ImguiUtils.Setup();

#if !PUBLIC_RELEASE
			// Skip splash screen in development builds.
			Application.LoadLevel("MainMenu");

			DevTools.OnInit();
#endif
		}

		/// <summary>
		/// Callback called when unity loads new event.
		/// </summary>
		/// <param name="newLevelName"></param>
		private void OnLevelSwitch(string newLevelName)
		{
			if (_currentLevelName == "GAME")
			{
				_gameWorld.OnUnload();
			}

			if (newLevelName == "GAME")
			{
				_gameWorld.OnLoad();
				_netManager.OnGameWorldLoad();
				return;
			}

			// When leaving game to main menu disconenct from the session.

			if (_currentLevelName == "GAME" && newLevelName == "MainMenu")
			{
				if (_netManager.IsOnline)
				{
					_netManager.Disconnect();
				}
			}
		}

		/// <summary>
		/// Updates IMGUI of the multiplayer.
		/// </summary>
		private void OnGui()
		{
			if (_netManager.IsOnline)
			{
				_netManager.DrawNameTags();
			}

			GUI.color = Color.white;
			GUI.Label(new Rect(2, Screen.height - 18, 500, 20), "MSCMP " + Client.GetModDisplayVersion());

			GUI.color = new Color(1.0f, 1.0f, 1.0f, 0.25f);
			GUI.DrawTexture(new Rect(2, Screen.height - 80, 76, 66), _modLogo);

			// Draw online state.

			if (_netManager.IsOnline)
			{
				GUI.color = Color.green;
				GUI.Label(new Rect(2, 2, 500, 20), "ONLINE " + (_netManager.IsHost ? "HOST" : "PLAYER"));
			}
			else
			{
				GUI.color = Color.red;
				GUI.Label(new Rect(2, 2, 500, 20), "OFFLINE");
			}

			MessagesList.Draw();

			// Friends widget.

			if (ShouldSeeInvitePanel())
			{
				UpdateInvitePanel();
			}

#if !PUBLIC_RELEASE
			DevTools.OnGui();

			if (DevTools.NetStats)
			{
				_netManager.DrawDebugGui();
			}

			_gameWorld.UpdateImgui();
#endif

			_console.Draw();
		}

		/// <summary>
		/// The interval between each friend list updates from steam in seconds.
		/// </summary>
		private const float FRIENDLIST_UPDATE_INTERVAL = 10.0f;

		/// <summary>
		/// Time left to next friend update.
		/// </summary>
		private float _timeToUpdateFriendList;

		private struct FriendEntry
		{
			public Steamworks.CSteamID SteamId;
			public string Name;
			public bool PlayingMsc;
		}

		/// <summary>
		/// Time in seconds player can have between invite.
		/// </summary>
		private const float INVITE_COOLDOWN = 10.0f;

		/// <summary>
		/// Current invite cooldown value.
		/// </summary>
		private float _inviteCooldown;

		private readonly List<FriendEntry> _onlineFriends = new List<FriendEntry>();

		/// <summary>
		/// Steam id of the recently invited friend.
		/// </summary>
		private Steamworks.CSteamID _invitedFriendSteamId;

		/// <summary>
		/// Check if invite panel is visible.
		/// </summary>
		/// <returns>true if invite panel is visible false otherwise</returns>
		private bool IsInvitePanelVisible()
		{
			return PlayMakerGlobals.Instance.Variables.FindFsmBool("PlayerInMenu").Value;
		}

		/// <summary>
		/// Update friend list.
		/// </summary>
		private void UpdateFriendList()
		{
			if (_inviteCooldown > 0.0f)
			{
				_inviteCooldown -= Time.deltaTime;
			}
			else
			{
				// Reset invited friend steam id.

				_invitedFriendSteamId.Clear();
			}

			_timeToUpdateFriendList -= Time.deltaTime;
			if (_timeToUpdateFriendList > 0.0f)
			{
				return;
			}

			_onlineFriends.Clear();

			Steamworks.EFriendFlags friendFlags = Steamworks.EFriendFlags.k_EFriendFlagImmediate;
			int friendsCount = Steamworks.SteamFriends.GetFriendCount(friendFlags);

			for (int i = 0; i < friendsCount; ++i)
			{
				Steamworks.CSteamID friendSteamId = Steamworks.SteamFriends.GetFriendByIndex(i, friendFlags);

				if (Steamworks.SteamFriends.GetFriendPersonaState(friendSteamId) == Steamworks.EPersonaState.k_EPersonaStateOffline)
				{
					continue;
				}
				
				FriendEntry friend = new FriendEntry();
				friend.SteamId = friendSteamId;
				friend.Name = Steamworks.SteamFriends.GetFriendPersonaName(friendSteamId);

				Steamworks.FriendGameInfo_t gameInfo;
				Steamworks.SteamFriends.GetFriendGamePlayed(friendSteamId, out gameInfo);
				friend.PlayingMsc = gameInfo.m_gameID.AppID() == Client.GameAppId;

				if (friend.PlayingMsc)
				{
					_onlineFriends.Insert(0, friend);
				}
				else
				{
					_onlineFriends.Add(friend);
				}
			}

			_timeToUpdateFriendList = FRIENDLIST_UPDATE_INTERVAL;
		}

		/// <summary>
		/// Should player see invite panel?
		/// </summary>
		/// <returns>true if invite panel should be visible, false otherwise</returns>
		private bool ShouldSeeInvitePanel()
		{
			return _netManager.IsHost && !_netManager.IsNetworkPlayerConnected();
		}

		/// <summary>
		/// Updates invite panel IMGUI.
		/// </summary>
		private void UpdateInvitePanel()
		{
			if (!IsInvitePanelVisible())
			{
				GUI.color = Color.white;
				GUI.Label(new Rect(0, Screen.height - 100, 200.0f, 20.0f), "[ESCAPE] - Invite friend");
				return;
			}

			const float invitePanelHeight = 400.0f;
			const float invitePanelWidth = 300.0f;
			const float rowHeight = 20.0f;
			Rect invitePanelRect = new Rect(Screen.width - invitePanelWidth - 10.0f, Screen.height / 2 - invitePanelHeight / 2, invitePanelWidth, 20.0f);

			// Draw header

			GUI.color = new Color(1.0f, 0.5f, 0.0f, 0.8f);
			ImguiUtils.DrawPlainColorRect(invitePanelRect);

			GUI.color = Color.white;
			invitePanelRect.x += 2.0f;
			GUI.Label(invitePanelRect, "Invite friend");
			invitePanelRect.x -= 2.0f;

			// Draw contents

			invitePanelRect.y += 21.0f;
			invitePanelRect.height = invitePanelHeight;

			GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.8f);
			ImguiUtils.DrawPlainColorRect(invitePanelRect);

			GUI.color = new Color(1.0f, 0.5f, 0.0f, 0.8f);
			int onlineFriendsCount = _onlineFriends.Count;

			invitePanelRect.height -= 2.0f;

			if (onlineFriendsCount == 0)
			{
				GUI.color = Color.white;

				TextAnchor previousAlignment = GUI.skin.label.alignment;
				GUI.skin.label.alignment = TextAnchor.MiddleCenter;
				bool playerIsOffline = Steamworks.SteamFriends.GetPersonaState() == Steamworks.EPersonaState.k_EPersonaStateOffline;
				if (playerIsOffline)
				{
					GUI.Label(invitePanelRect, "You cannot invite friends while in steam offline mode.\n\nSwitch back your steam status to online to be able to invite players.");
				}
				else
				{
					GUI.Label(invitePanelRect, "You don't have any friends online.");
				}
				GUI.skin.label.alignment = previousAlignment;


				return;
			}


			_friendsScrollViewPos = GUI.BeginScrollView(invitePanelRect, _friendsScrollViewPos, new Rect(0, 0, invitePanelWidth - 20.0f, 20.0f * onlineFriendsCount));

			int firstVisibleFriendId = (int)(_friendsScrollViewPos.y / rowHeight);
			int maxVisibleFriends = (int)(invitePanelHeight / rowHeight);
			int lastIndex = firstVisibleFriendId + maxVisibleFriends + 1;
			if (lastIndex > onlineFriendsCount)
			{
				lastIndex = onlineFriendsCount;
			}
			for (int i = firstVisibleFriendId; i < lastIndex; ++i)
			{
				FriendEntry friend = _onlineFriends[i];
				if (friend.PlayingMsc)
				{
					GUI.color = Color.green;
				}
				else
				{
					GUI.color = Color.white;
				}

				Rect friendRect = new Rect(2, 1 + rowHeight * i, 200.0f, rowHeight);

				GUI.Label(friendRect, friend.Name);

				friendRect.x += 180.0f;
				friendRect.width = 100.0f;

				Steamworks.CSteamID friendSteamId = friend.SteamId;

				if (_invitedFriendSteamId == friendSteamId)
				{
					GUI.Label(friendRect, $"INVITED! ({_inviteCooldown:F1}s)");
					continue;
				}

				if (_inviteCooldown > 0.0f)
				{
					continue;
				}

				if (GUI.Button(friendRect, "Invite"))
				{
					if (_netManager.InviteToMyLobby(friendSteamId))
					{
						_invitedFriendSteamId = friendSteamId;
						_inviteCooldown = INVITE_COOLDOWN;
					}
					else
					{
						UI.Mpgui.Instance.ShowMessageBox("Failed to invite friend due to steam error.");
					}

				}
			}

			GUI.EndScrollView();
		}

		private void OnLevelWasLoaded(int level)
		{
			string loadedLevelName = Application.loadedLevelName;
			OnLevelSwitch(loadedLevelName);
			_currentLevelName = loadedLevelName;
		}

		/// <summary>
		/// Update multiplayer state.
		/// </summary>
		private void LateUpdate()
		{
			Utils.CallSafe("Update", () =>
			{
				Steamworks.SteamAPI.RunCallbacks();

				if (IsInvitePanelVisible())
				{
					UpdateFriendList();
				}

				_gameWorld.Update();
				_netManager.Update();


				// Development stuff.
#if !PUBLIC_RELEASE
				DevTools.Update();
#endif
			});
		}

		/// <summary>
		/// Wrapper around unitys load level method to call OnLevelSwitch even if level is the same.
		/// </summary>
		/// <param name="levelName">The name of the level to load.</param>
		public void LoadLevel(string levelName)
		{
			Application.LoadLevel(levelName);
		}

		/// <summary>
		/// Can this client instance use save?
		/// </summary>
		public bool CanUseSave => !_netManager.IsOnline || _netManager.IsHost;
	}
}
