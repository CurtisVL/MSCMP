using MSCMP.Utilities;
using UnityEngine;

namespace MSCMP.Network
{
	/// <summary>
	/// Handles network statistics.
	/// </summary>
	internal class NetStatistics
	{
		private const int HISTORY_SIZE = 100;
		private readonly long[] _bytesReceivedHistory = new long[HISTORY_SIZE];
		private readonly long[] _bytesSentHistory = new long[HISTORY_SIZE];
		private long _maxBytesReceivedInHistory;
		private long _maxBytesSentInHistory;

		private int _packetsSendTotal;
		private int _packetsReceivedTotal;

		private int _packetsSendLastFrame;
		private int _packetsReceivedLastFrame;

		private int _packetsSendCurrentFrame;
		private int _packetsReceivedCurrentFrame;

		private long _bytesSentTotal;
		private long _bytesReceivedTotal;

		private long _bytesSentLastFrame;
		private long _bytesReceivedLastFrame;

		private long _bytesSentCurrentFrame;
		private long _bytesReceivedCurrentFrame;

		/// <summary>
		/// Network manager owning this object.
		/// </summary>
		private readonly NetManager _netManager;

		/// <summary>
		/// Line material used to draw the graph.
		/// </summary>
		private Material _lineMaterial;


		public NetStatistics(NetManager netManager)
		{
			_netManager = netManager;
		}

		private void SetupLineMaterial()
		{
			if (_lineMaterial != null) { return; }

			// Setup graph lines material.

			Shader shader = Shader.Find("GUI/Text Shader"); // Text shader is sufficient for this case.
			Client.Assert(shader != null, "Shader not found!");
			_lineMaterial = new Material(shader);
			Client.Assert(_lineMaterial != null, "Failed to setup material!");
		}

		/// <summary>
		/// Resets all frame statistics.
		/// </summary>
		public void NewFrame()
		{
			_packetsSendLastFrame = _packetsSendCurrentFrame;
			_packetsReceivedLastFrame = _packetsReceivedCurrentFrame;

			_packetsSendCurrentFrame = 0;
			_packetsReceivedCurrentFrame = 0;

			_bytesSentLastFrame = _bytesSentCurrentFrame;
			_bytesReceivedLastFrame = _bytesReceivedCurrentFrame;

			_maxBytesReceivedInHistory = _bytesSentCurrentFrame;
			_maxBytesSentInHistory = _bytesReceivedCurrentFrame;
			for (int i = 0; i < HISTORY_SIZE - 1; ++i)
			{
				_bytesSentHistory[i] = _bytesSentHistory[i + 1];
				if (_maxBytesSentInHistory < _bytesSentHistory[i])
				{
					_maxBytesSentInHistory = _bytesSentHistory[i];
				}
				_bytesReceivedHistory[i] = _bytesReceivedHistory[i + 1];
				if (_maxBytesReceivedInHistory < _bytesReceivedHistory[i])
				{
					_maxBytesReceivedInHistory = _bytesReceivedHistory[i];
				}
			}

			_bytesSentHistory[HISTORY_SIZE - 1] = _bytesSentCurrentFrame;
			_bytesReceivedHistory[HISTORY_SIZE - 1] = _bytesReceivedCurrentFrame;

			_bytesSentCurrentFrame = 0;
			_bytesReceivedCurrentFrame = 0;
		}

		/// <summary>
		/// Records new send message.
		/// </summary>
		/// <param name="messageId">The received message id.</param>
		/// <param name="bytes">Received bytes.</param>
		public void RecordSendMessage(int messageId, long bytes)
		{
			_bytesSentCurrentFrame += bytes;
			_bytesSentTotal += bytes;

			_packetsSendCurrentFrame++;
			_packetsSendTotal++;
		}

		/// <summary>
		/// Records new received message.
		/// </summary>
		/// <param name="messageId">The received message id.</param>
		/// <param name="bytes">Received bytes.</param>
		public void RecordReceivedMessage(int messageId, long bytes)
		{
			_bytesReceivedCurrentFrame += bytes;
			_bytesReceivedTotal += bytes;

			_packetsReceivedCurrentFrame++;
			_packetsReceivedTotal++;
		}

		/// <summary>
		/// Draws statistic label.
		/// </summary>
		/// <remarks>GUI color after this call may not be white!</remarks>
		/// <param name="name">Name of the statistic.</param>
		/// <param name="value">The statistic value.</param>
		/// <param name="critical">The critical statistic value to highlight. (if -1 there is no critical value)</param>
		/// <param name="bytes">Is the stat representing bytes?</param>
		private void DrawStatHelper(ref Rect rct, string name, long value, int critical = -1, bool bytes = false)
		{
			GUI.color = Color.white;
			GUI.Label(rct, name);

			bool isCriticalValue = critical != -1 && value >= critical;
			GUI.color = isCriticalValue ? Color.red : Color.white;

			rct.x += rct.width;
			if (bytes)
			{
				GUI.Label(rct, FormatBytes(value));
			}
			else
			{
				GUI.Label(rct, value.ToString());
			}

			rct.x -= rct.width;
			rct.y += rct.height;
		}

		/// <summary>
		/// Draws text label.
		/// </summary>
		/// <remarks>GUI color after this call may not be white!</remarks>
		/// <param name="name">Name of the statistic.</param>
		/// <param name="text">The text value.</param>
		private void DrawTextHelper(ref Rect rct, string name, string text)
		{
			GUI.color = Color.white;
			GUI.Label(rct, name);
			rct.x += rct.width;
			GUI.Label(rct, text);

			rct.x -= rct.width;
			rct.y += rct.height;
		}

		/// <summary>
		/// Draw line using GL.
		/// </summary>
		/// <param name="start">Line start position.</param>
		/// <param name="end">Line end position.</param>
		/// <param name="color">Line color.</param>
		private void DrawLineHelper(Vector2 start, Vector2 end, Color color)
		{
			GL.Color(color);
			GL.Vertex3(start.x, Screen.height - start.y, 0.0f);
			GL.Vertex3(end.x, Screen.height - end.y, 0.0f);
		}

		/// <summary>
		/// Draw network graph.
		/// </summary>
		/// <param name="drawRect">Rectangle where graph should drawn.</param>
		private void DrawGraph(Rect drawRect)
		{
			SetupLineMaterial();

			_lineMaterial.SetPass(0);
			GL.PushMatrix();
			GL.LoadPixelMatrix();
			GL.Begin(GL.LINES);

			// draw graph boundaries

			DrawLineHelper(new Vector2(drawRect.x, drawRect.y), new Vector2(drawRect.x + drawRect.width, drawRect.y), Color.gray);
			DrawLineHelper(new Vector2(drawRect.x, drawRect.y), new Vector2(drawRect.x, drawRect.y + drawRect.height), Color.gray);
			DrawLineHelper(new Vector2(drawRect.x + drawRect.width, drawRect.y), new Vector2(drawRect.x + drawRect.width, drawRect.y + drawRect.height), Color.gray);
			DrawLineHelper(new Vector2(drawRect.x, drawRect.y + drawRect.height), new Vector2(drawRect.x + drawRect.width, drawRect.y + drawRect.height), Color.gray);

			float stepWidth = drawRect.width / HISTORY_SIZE;

			for (int i = 0; i < HISTORY_SIZE; ++i)
			{
				// draw send

				long previousHistoryValue = i > 0 ? _bytesSentHistory[i - 1] : 0;
				float previousY = drawRect.y + drawRect.height * Mathf.Clamp01(1.0f - (float)previousHistoryValue / Mathf.Max(1, _maxBytesSentInHistory));
				Vector2 start = new Vector2(drawRect.x + stepWidth * Mathf.Max(i - 1, 0), previousY);
				float currentY = drawRect.y + drawRect.height * Mathf.Clamp01(1.0f - (float)_bytesSentHistory[i] / Mathf.Max(1, _maxBytesSentInHistory));
				Vector2 end = new Vector2(drawRect.x + stepWidth * i, currentY);
				DrawLineHelper(start, end, Color.red);

				// draw receive

				previousHistoryValue = i > 0 ? _bytesReceivedHistory[i - 1] : 0;
				previousY = drawRect.y + drawRect.height * Mathf.Clamp01(1.0f - (float)previousHistoryValue / Mathf.Max(1, _maxBytesReceivedInHistory));
				start = new Vector2(drawRect.x + stepWidth * Mathf.Max(i - 1, 0), previousY);
				currentY = drawRect.y + drawRect.height * Mathf.Clamp01(1.0f - (float)_bytesReceivedHistory[i] / Mathf.Max(1, _maxBytesReceivedInHistory));
				end = new Vector2(drawRect.x + stepWidth * i, currentY);
				DrawLineHelper(start, end, Color.green);
			}

			GL.End();
			GL.PopMatrix();
		}

		/// <summary>
		/// Helper used to format bytes.
		/// </summary>
		/// <param name="bytes">The bytes.</param>
		/// <returns>Formatted bytes string.</returns>
		private string FormatBytes(long bytes)
		{
			if (bytes >= 1024 * 1024)
			{
				float mb = (float)bytes / (1024 * 1024);
				return mb.ToString("0.00") + " MB";
			}

			if (bytes >= 1024)
			{
				float kb = (float)bytes / 1024;
				return kb.ToString("0.00") + " KB";
			}
			return bytes + " B";
		}

		/// <summary>
		/// Draw network statistics.
		/// </summary>
		public void Draw()
		{
			GUI.color = Color.white;
			const int windowWidth = 300;
			const int windowHeight = 600;
			Rect statsWindowRect = new Rect(Screen.width - windowWidth - 10, Screen.height - windowHeight - 10, windowWidth, windowHeight);
			GUI.Window(666, statsWindowRect, window =>
			{

				// Draw traffic graph title.

				Rect rct = new Rect(10, 20, 200, 25);
				GUI.Label(rct, $"Traffic graph (last {HISTORY_SIZE} frames):");
				rct.y += 25;

				Rect graphRect = new Rect(rct.x, rct.y, windowWidth - 20, 100);

				// Draw graph background.

				GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.35f);
				ImguiUtils.DrawPlainColorRect(graphRect);

				// Draw the graph itself.

				graphRect.x += statsWindowRect.x;
				graphRect.y += statsWindowRect.y;
				DrawGraph(graphRect);

				GUI.color = Color.white;
				rct.y += 5;
				rct.x += 5;
				ImguiUtils.DrawSmallLabel($"{FormatBytes(_maxBytesSentInHistory)} sent/frame", rct, Color.red, true);
				rct.y += 12;

				ImguiUtils.DrawSmallLabel($"{FormatBytes(_maxBytesReceivedInHistory)} recv/frame", rct, Color.green, true);
				rct.y -= 12 - 5;
				rct.x -= 5;

				rct.y += graphRect.height;

				rct.height = 20;

				// Draw separator

				GUI.color = Color.black;
				ImguiUtils.DrawPlainColorRect(new Rect(0, rct.y, windowWidth, 2));
				rct.y += 2;

				// Draw stats background

				GUI.color = new Color(0.0f, 0.0f, 0.0f, 0.5f);
				ImguiUtils.DrawPlainColorRect(new Rect(0, rct.y, windowWidth, windowHeight - rct.y));

				// Draw statistics

				DrawStatHelper(ref rct, "packetsSendTotal", _packetsSendTotal);
				DrawStatHelper(ref rct, "packetsReceivedTotal", _packetsReceivedTotal);
				DrawStatHelper(ref rct, "packetsSendLastFrame", _packetsSendLastFrame, 1000);
				DrawStatHelper(ref rct, "packetsReceivedLastFrame", _packetsReceivedLastFrame, 1000);
				DrawStatHelper(ref rct, "packetsSendCurrentFrame", _packetsSendCurrentFrame, 1000);
				DrawStatHelper(ref rct, "packetsReceivedCurrentFrame", _packetsReceivedCurrentFrame, 1000);
				DrawStatHelper(ref rct, "bytesSendTotal", _bytesSentTotal, -1, true);
				DrawStatHelper(ref rct, "bytesReceivedTotal", _bytesReceivedTotal, -1, true);
				DrawStatHelper(ref rct, "bytesSendLastFrame", _bytesSentLastFrame, 1000, true);
				DrawStatHelper(ref rct, "bytesReceivedLastFrame", _bytesReceivedLastFrame, 1000, true);
				DrawStatHelper(ref rct, "bytesSendCurrentFrame", _bytesSentCurrentFrame, 1000, true);
				DrawStatHelper(ref rct, "bytesReceivedCurrentFrame", _bytesReceivedCurrentFrame, 1000, true);

				// Draw separator

				rct.y += 2;
				GUI.color = Color.black;
				ImguiUtils.DrawPlainColorRect(new Rect(0, rct.y, windowWidth, 2));
				rct.y += 2;

				// Draw P2P session state.

				DrawTextHelper(ref rct, "Steam session state:", "");

				Steamworks.P2PSessionState_t sessionState = new Steamworks.P2PSessionState_t();
				if (_netManager.GetP2PSessionState(out sessionState))
				{
					DrawTextHelper(ref rct, "Is Connecting", sessionState.m_bConnecting.ToString());
					DrawTextHelper(ref rct, "Is connection active", sessionState.m_bConnectionActive == 0 ? "no" : "yes");
					DrawTextHelper(ref rct, "Using relay?", sessionState.m_bConnectionActive == 0 ? "no" : "yes");
					DrawTextHelper(ref rct, "Session error", Utils.P2PSessionErrorToString((Steamworks.EP2PSessionError)sessionState.m_eP2PSessionError));
					DrawTextHelper(ref rct, "Bytes queued for send", FormatBytes(sessionState.m_nBytesQueuedForSend));
					DrawTextHelper(ref rct, "Packets queued for send", sessionState.m_nPacketsQueuedForSend.ToString());
					uint uip = sessionState.m_nRemoteIP;
					string ip = $"{(uip >> 24) & 0xff}.{(uip >> 16) & 0xff}.{(uip >> 8) & 0xff}.{uip & 0xff}";
					DrawTextHelper(ref rct, "Remote ip", ip);
					DrawTextHelper(ref rct, "Remote port", sessionState.m_nRemotePort.ToString());
				}
				else
				{
					DrawTextHelper(ref rct, "Session inactive.", "");
				}
			}, "Network statistics");
		}
	}
}
