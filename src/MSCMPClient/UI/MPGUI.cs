using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MSCMP.UI {
	/// <summary>
	/// Multiplayer mod GUI manager component.
	/// </summary>
	class MPGUI : MonoBehaviour {

		/// <summary>
		/// Instance of the GUI.
		/// </summary>
		public static MPGUI Instance = null;

		/// <summary>
		/// Message box handler.
		/// </summary>
		private Handlers.MessageBoxHandler messageBoxHandler = null;

		/// <summary>
		/// List of lobbies.
		/// </summary>
		public List<string> LobbyNames = new List<string>();

		public MPGUI() {
			Instance = this;
		}


		/// <summary>
		/// Setup UI.
		/// </summary>
		void Start() {
			Utils.CallSafe("UISetup", () => {
				var canvasPrefab = Client.LoadAsset<GameObject>("Assets/UI/UICanvas.prefab");

				var canvas = Instantiate(canvasPrefab);
				canvas.transform.SetParent(transform, false);

				var messageBox = canvas.transform.FindChild("MessageBox").gameObject;
				messageBoxHandler = messageBox.AddComponent<Handlers.MessageBoxHandler>();

				DontDestroyOnLoad(gameObject);
				DontDestroyOnLoad(canvas);
				DontDestroyOnLoad(messageBox);
			});
		}

		~MPGUI() {
			Instance = null;
		}


		private int cursorCounter = 0;

		/// <summary>
		/// Show cursor.
		/// </summary>
		/// <param name="show">Should cursor be shown?</param>
		public void ShowCursor(bool show) {
			if (show) {
				++cursorCounter;
				if (!Cursor.visible) {
					Cursor.visible = true;
				}
			}
			else {
				Client.Assert(cursorCounter > 0, "Tried to hide cursor too many times.");
				if (--cursorCounter == 0) {
					if (Application.loadedLevelName == "GAME") {
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
		public bool ShowMessageBox(string text, Handlers.MessageBoxHandler.OnClose onClose = null) {
			return messageBoxHandler.Show(text, onClose);
		}

		/// <summary>
		/// Lobby selection UI.
		/// </summary>
		void OnGUI() {
			if (Application.loadedLevelName == "MainMenu") {
				GUI.Label(new Rect(10, 50, 250, 20), "Dedicated servers:");

				int i = 75;
				int lobbyIndex = 0;
				foreach (string lobby in LobbyNames) {
					if (GUI.Button(new Rect(10, i, 250, 40), lobby)) {
						Network.NetManager.Instance.JoinLobbyFromUI(lobbyIndex);
					}
					i += 45;
					lobbyIndex++;
				}

				if (LobbyNames.Count == 0) {
					if (GUI.Button(new Rect(10, i, 250, 40), "No dedicated servers found")) {

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
