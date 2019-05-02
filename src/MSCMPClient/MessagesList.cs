using UnityEngine;

namespace MSCMP
{
	public enum MessageSeverity
	{
		Info,
		Error
	}

	/// <summary>
	/// Basic message hud.
	/// </summary>
	internal static class MessagesList
	{
		private const int MESSAGES_COUNT = 5;
		private static readonly Color[] _colors = new Color[MESSAGES_COUNT];
		private static readonly string[] _messages = new string[MESSAGES_COUNT];

		/// <summary>
		/// Add message to the hud.
		/// </summary>
		/// <param name="message"></param>
		/// <param name="severity"></param>
		public static void AddMessage(string message, MessageSeverity severity)
		{

			for (int i = 1; i < MESSAGES_COUNT; ++i)
			{
				_colors[i - 1] = _colors[i];
				_messages[i - 1] = _messages[i];
			}

			_messages[MESSAGES_COUNT - 1] = message;
			Color color = Color.white;
			switch (severity)
			{
				case MessageSeverity.Info:
					color = Color.white;
					break;

				case MessageSeverity.Error:
					color = Color.red;
					break;
			}

			_colors[MESSAGES_COUNT - 1] = color;
		}

		/// <summary>
		/// Clear chat.
		/// </summary>
		public static void ClearMessages()
		{
			for (int i = 0; i < MESSAGES_COUNT; ++i)
			{
				_messages[i] = "";
			}
		}


		/// <summary>
		/// Draw message list.
		/// </summary>

		public static void Draw()
		{
			float x = 10.0f;
			float y = Screen.height / 2.0f;
			const float lineWidth = 500;
			const float lineHeight = 20;
			for (int i = 0; i < MESSAGES_COUNT; ++i)
			{
				if (_messages[i] != null && _messages[i].Length > 0)
				{
					GUI.color = Color.black;
					GUI.Label(new Rect(x + 1, y + 1, lineWidth, lineHeight), _messages[i]);

					GUI.color = _colors[i];
					GUI.Label(new Rect(x, y, lineWidth, lineHeight), _messages[i]);
				}
				y += lineHeight;
			}
		}
	}
}
