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
		private static readonly Color[] Colors = new Color[MESSAGES_COUNT];
		private static readonly string[] Messages = new string[MESSAGES_COUNT];

		/// <summary>
		/// Add message to the hud.
		/// </summary>
		/// <param name="message"></param>
		/// <param name="severity"></param>
		public static void AddMessage(string message, MessageSeverity severity)
		{

			for (int i = 1; i < MESSAGES_COUNT; ++i)
			{
				Colors[i - 1] = Colors[i];
				Messages[i - 1] = Messages[i];
			}

			Messages[MESSAGES_COUNT - 1] = message;
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

			Colors[MESSAGES_COUNT - 1] = color;
		}

		/// <summary>
		/// Clear chat.
		/// </summary>
		public static void ClearMessages()
		{
			for (int i = 0; i < MESSAGES_COUNT; ++i)
			{
				Messages[i] = "";
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
				if (Messages[i] != null && Messages[i].Length > 0)
				{
					GUI.color = Color.black;
					GUI.Label(new Rect(x + 1, y + 1, lineWidth, lineHeight), Messages[i]);

					GUI.color = Colors[i];
					GUI.Label(new Rect(x, y, lineWidth, lineHeight), Messages[i]);
				}
				y += lineHeight;
			}
		}
	}
}
