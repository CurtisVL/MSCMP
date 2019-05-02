using System.Collections.Generic;
using System.IO;

namespace MSCMP.Network
{
	/// <summary>
	/// Network message handler.
	/// </summary>
	internal class NetMessageHandler
	{
		private delegate void HandleMessageLowLevel(Steamworks.CSteamID sender, BinaryReader reader);
		private readonly Dictionary<byte, HandleMessageLowLevel> _messageHandlers = new Dictionary<byte, HandleMessageLowLevel>();

		/// <summary>
		/// Delegate type for network messages handler.
		/// </summary>
		/// <typeparam name="T">The type of the network message.</typeparam>
		/// <param name="sender">Steam id that sent us the message.</param>
		/// <param name="message">The deserialized image.</param>
		public delegate void MessageHandler<T>(Steamworks.CSteamID sender, T message);

		/// <summary>
		/// Network manager owning this handler.
		/// </summary>
		private NetManager _netManager;

		public NetMessageHandler(NetManager theNetManager)
		{
			_netManager = theNetManager;
		}

		/// <summary>
		/// Binds handler for the given message. (There can be only one handler per message)
		/// </summary>
		/// <typeparam name="T">The type of message to register handler for.</typeparam>
		/// <param name="handler">The handler lambda.</param>
		public void BindMessageHandler<T>(MessageHandler<T> handler) 
			where T : INetMessage, new()
		{
			T message = new T();

			_messageHandlers.Add(message.MessageId, (sender, reader) =>
			{
				if (!message.Read(reader))
				{
					Logger.Log("Failed to read network message " + message.MessageId + " received from " + sender.ToString());
					return;
				}
				handler(sender, message);
			});
		}

		/// <summary>
		/// Process incoming network message.
		/// </summary>
		/// <param name="messageId">The id of the message.</param>
		/// <param name="senderSteamId">Steamid of the sender client.</param>
		/// <param name="reader">The binary reader contaning message data.</param>
		public void ProcessMessage(byte messageId, Steamworks.CSteamID senderSteamId, BinaryReader reader)
		{
			if (_messageHandlers.ContainsKey(messageId))
			{
				_messageHandlers[messageId](senderSteamId, reader);
			}
		}
	}
}
