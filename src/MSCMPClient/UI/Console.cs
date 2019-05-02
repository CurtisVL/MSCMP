using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.UI
{
	/// <summary>
	/// Console ui element.
	/// </summary>
	internal class Console
	{
		/// <summary>
		/// The command delegate.
		/// </summary>
		/// <param name="args">The arguments - first one will be name of the command.</param>
		public delegate void CommandDelegate(string[] args);

		private static readonly Dictionary<string, CommandDelegate> Commands = new Dictionary<string, CommandDelegate>();

		/// <summary>
		/// Register new console command.
		/// </summary>
		/// <param name="command">Command to register.</param>
		/// <param name="commandDelegate">The command delegate.</param>
		public static void RegisterCommand(string command, CommandDelegate commandDelegate)
		{
			Commands.Add(command, commandDelegate);
		}

		/// <summary>
		/// Execute given command.
		/// </summary>
		/// <param name="command">The command to execute</param>
		/// <returns>true if command was executed, false otherwise</returns>
		public static bool ExecuteCommand(string command)
		{
			try
			{
				string[] args = command.Split(' ');
				if (args.Length == 0)
				{
					return false;
				}

				CommandDelegate commandDelegate = Commands[args[0]];
				if (commandDelegate != null)
				{
					commandDelegate.Invoke(args);
					return true;
				}
			}
			catch (Exception e)
			{
				Client.ConsoleMessage($"COMMAND ERROR: {e}");
				return true; //True, so it won't say Invalid Command
			}
			return false;
		}

		/// <summary>
		/// Is the console visible?
		/// </summary>
		private bool _isVisible;

		/// <summary>
		/// Should console input field be focused next frame?
		/// </summary>
		private bool _focusConsole;

		/// <summary>
		/// Current console input text.
		/// </summary>
		private string _inputText = "";

		/// <summary>
		/// List of all messages in console.
		/// </summary>
		private readonly List<string> _messages = new List<string>();

		/// <summary>
		/// The console singleton.
		/// </summary>
		private static Console _instance;

		/// <summary>
		/// Get currently active instance of console.
		/// </summary>
		public static Console Instance => _instance;

		/// <summary>
		/// Constructor.
		/// </summary>
		public Console()
		{
			_instance = this;
		}

		/// <summary>
		/// Destructor.
		/// </summary>
		~Console()
		{
			_instance = null;
		}

		/// <summary>
		/// The input history.
		/// </summary>
		private readonly List<string> _inputHistory = new List<string>();

		/// <summary>
		/// Current input history index, if -1 no input history entry is being used.
		/// </summary>
		private int _currentHistoryEntryIndex = -1;

		/// <summary>
		/// Handle the input user typed.
		/// </summary>
		private void HandleInput()
		{
			if (!ExecuteCommand(_inputText))
			{
				AddMessage($"ERROR: Unknown command {_inputText}.");
			}
			_inputHistory.Add(_inputText);
			_inputText = string.Empty;
			_currentHistoryEntryIndex = -1;
		}

		/// <summary>
		/// Current console rectangle.
		/// </summary>
		private Rect _consoleRect = new Rect(5, 5, 800, 400);

		/// <summary>
		/// The width of the console button.
		/// </summary>
		private const int BUTTON_WIDTH = 80;

		/// <summary>
		/// Draw console.
		/// </summary>
		public void Draw()
		{
			HandleEvent();

			if (!_isVisible)
			{
				return;
			}

			GUI.color = Color.white;
			_consoleRect = GUI.Window(69, _consoleRect, DrawConsole, "CONSOLE (Press ~ to hide)");
		}

		/// <summary>
		/// Handle input event.
		/// </summary>
		private void HandleEvent()
		{
			if (Event.current.rawType != EventType.KeyUp)
			{
				return;
			}

			switch (Event.current.keyCode)
			{
				case KeyCode.BackQuote:
					_isVisible = !_isVisible;
					if (_isVisible)
					{
						_focusConsole = true;
					}
					break;

				case KeyCode.Return:
					if (_isVisible)
					{
						HandleInput();
					}
					break;

				case KeyCode.UpArrow:
					if (_isVisible)
					{
						CycleThroughInputHistory(false);
					}
					break;

				case KeyCode.DownArrow:
					if (_isVisible)
					{
						CycleThroughInputHistory(true);
					}
					break;
			}
		}

		/// <summary>
		/// Cycles through input history.
		/// </summary>
		/// <param name="forward">Should cycle forward or backwards?</param>
		private void CycleThroughInputHistory(bool forward)
		{
			if (forward)
			{
				++_currentHistoryEntryIndex;
				if (_currentHistoryEntryIndex == _inputHistory.Count)
				{
					_currentHistoryEntryIndex = -1;
				}
			}
			else
			{
				if (_currentHistoryEntryIndex == -1)
				{
					if (_inputHistory.Count > 0)
					{
						_currentHistoryEntryIndex = _inputHistory.Count - 1;
					}
				}
				else
				{
					--_currentHistoryEntryIndex;
				}
			}

			if (_currentHistoryEntryIndex == -1)
			{
				_inputText = "";
			}
			else
			{
				_inputText = _inputHistory[_currentHistoryEntryIndex];
			}

			//Moving the cursor to the last character
			if (GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) is TextEditor editor)
			{
				editor.selectPos = _inputText.Length + 1;
				editor.pos = _inputText.Length + 1;
			}
		}

		/// <summary>
		/// Current console scroll position.
		/// </summary>
		private Vector2 _scrollPosition;

		/// <summary>
		/// Maximum visible messages.
		/// </summary>
		private const int MAX_VISIBLE_MESSAGES = 1000;

		/// <summary>
		/// The height of single message line.
		/// </summary>
		private const float MESSAGE_HEIGHT = 20.0f;

		private void DrawConsole(int windowId)
		{
			// Draw messages.
			Rect scrollViewRect = new Rect(10, 20, _consoleRect.width - 20, _consoleRect.height - 55);
			int visibleMessagesCount = Mathf.Min(_messages.Count, MAX_VISIBLE_MESSAGES);
			Rect viewRect = new Rect(0, 0, scrollViewRect.width - 50, visibleMessagesCount * MESSAGE_HEIGHT);

			Rect messageRect = viewRect;
			messageRect.height = MESSAGE_HEIGHT;
			messageRect.y = viewRect.height - MESSAGE_HEIGHT;

			_scrollPosition = GUI.BeginScrollView(scrollViewRect, _scrollPosition, viewRect);

			for (int i = _messages.Count; i > _messages.Count - visibleMessagesCount; --i)
			{
				GUI.Label(messageRect, _messages[i - 1]);
				messageRect.y -= messageRect.height;
			}

			GUI.EndScrollView();

			// Draw input field.

			int inputWidth = (int)(_consoleRect.width - BUTTON_WIDTH * 2 - 30 - 10);
			Rect inputRect = new Rect(10, _consoleRect.height - 30, inputWidth, 20);
			GUI.SetNextControlName("ConsoleTextField");
			_inputText = GUI.TextField(inputRect, _inputText);

			// Draw send button.

			inputRect.x += inputWidth + 10;
			inputRect.width = BUTTON_WIDTH;

			if (GUI.Button(inputRect, "SEND"))
			{
				HandleInput();
			}

			inputRect.x += BUTTON_WIDTH + 10;
			if (GUI.Button(inputRect, "CLEAR"))
			{
				Clear();
			}

			if (_focusConsole)
			{
				GUI.FocusControl("ConsoleTextField");
			}

			// Make console dragable.
			// Must be called as last otherwise different IMGUI calls will have broken state.

			GUI.DragWindow();
		}

		/// <summary>
		/// Add new message to the console.
		/// </summary>
		/// <param name="message">The message to add.</param>
		public void AddMessage(string message)
		{
			_scrollPosition.y += MESSAGE_HEIGHT;
			_messages.Add(message);
		}

		/// <summary>
		/// Clears console.
		/// </summary>
		public void Clear()
		{
			_messages.Clear();
			_scrollPosition.y = 0.0f;
		}
	}
}
