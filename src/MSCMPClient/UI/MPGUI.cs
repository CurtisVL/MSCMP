using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.UI
{
	/// <summary>
	/// Multiplayer mod GUI manager component.
	/// </summary>
	internal class Mpgui
		: MonoBehaviour
	{
		/// <summary>
		/// Instance of the GUI.
		/// </summary>
		public static Mpgui Instance;

		/// <summary>
		/// Message box handler.
		/// </summary>
		private Handlers.MessageBoxHandler _messageBoxHandler;

		/// <summary>
		/// List of lobbies.
		/// </summary>
		public List<string> LobbyNames = new List<string>();

		public Mpgui()
		{
			Instance = this;
		}
		
		/// <summary>
		/// Setup UI.
		/// </summary>
		private void Start()
		{
			Utils.CallSafe("UISetup", () =>
			{
				GameObject canvasPrefab = Client.LoadAsset<GameObject>("Assets/UI/UICanvas.prefab");

				GameObject canvas = Instantiate(canvasPrefab);
				canvas.transform.SetParent(transform, false);

				GameObject messageBox = canvas.transform.FindChild("MessageBox").gameObject;
				_messageBoxHandler = messageBox.AddComponent<Handlers.MessageBoxHandler>();

				DontDestroyOnLoad(gameObject);
				DontDestroyOnLoad(canvas);
				DontDestroyOnLoad(messageBox);
			});
		}

		~Mpgui()
		{
			Instance = null;
		}


		private int _cursorCounter;

		/// <summary>
		/// Show cursor.
		/// </summary>
		/// <param name="show">Should cursor be shown?</param>
		public void ShowCursor(bool show)
		{
			if (show)
			{
				++_cursorCounter;
				if (!Cursor.visible)
				{
					Cursor.visible = true;
				}
			}
			else
			{
				Client.Assert(_cursorCounter > 0, "Tried to hide cursor too many times.");
				if (--_cursorCounter == 0)
				{
					if (Application.loadedLevelName == "GAME")
					{
						// Only hide cursor if we are in game.
						Cursor.visible = false;
					}
				}
			}
		}

		/// <summary>
		/// Show message box. (There can be maximum of one message box)
		/// </summary>
		/// <param name="text">The text to show.</param>
		/// <param name="onClose">The callback to call when OK button is pressed.</param>
		/// <returns>true if message box was shown false if there is already some message box and this one could not be showed.</returns>
		public bool ShowMessageBox(string text, Handlers.MessageBoxHandler.OnClose onClose = null)
		{
			return _messageBoxHandler.Show(text, onClose);
		}

		/// <summary>
		/// Lobby selection UI.
		/// </summary>
		private void OnGui()
		{
			if (Application.loadedLevelName == "MainMenu")
			{
				GUI.Label(new Rect(10, 50, 250, 20), "Dedicated servers:");

				int i = 75;
				int lobbyIndex = 0;
				foreach (string lobby in LobbyNames)
				{
					if (GUI.Button(new Rect(10, i, 250, 40), lobby))
					{
						Network.NetManager.Instance.JoinLobbyFromUi(lobbyIndex);
					}
					i += 45;
					lobbyIndex++;
				}

				if (LobbyNames.Count == 0)
				{
					if (GUI.Button(new Rect(10, i, 250, 40), "No dedicated servers found"))
					{

					}
				}

				/*
				if (GUI.Button(new Rect(10, i, 250, 40), "Refresh list")) {
					Network.NetManager.Instance.RequestLobbies();
				}
				*/
			}
		}
	}
}
