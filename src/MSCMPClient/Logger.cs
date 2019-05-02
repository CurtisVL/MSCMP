using System.IO;

namespace MSCMP
{
	internal static class Logger
	{
		/// <summary>
		/// The file used for logging.
		/// </summary>
		private static StreamWriter _logFile;
		
		/// <summary>
		/// Setup logger.
		/// </summary>
		/// <param name="logPath">The path where log file should be created</param>
		/// <returns></returns>
		public static bool SetupLogger(string logPath)
		{
			try
			{
				_logFile = new StreamWriter(logPath, false);
			}
			catch
			{
				// Unfortunately there is no place where we could send the failure.
				return false;
			}
			return _logFile != null;
		}

		/// <summary>
		/// Set auto flush? (Remember! This is not good for FPS as each write to log is automatically flushing the log file!)
		/// </summary>
		/// <param name="autoFlush"></param>
		public static void SetAutoFlush(bool autoFlush)
		{
			if (_logFile != null)
			{
				_logFile.AutoFlush = autoFlush;
			}
		}

		/// <summary>
		/// Force flush of the log file.
		/// </summary>
		public static void ForceFlush()
		{
			_logFile?.Flush();
		}

		/// <summary>
		/// Write log message.
		/// </summary>
		/// <param name="message">Message to write.</param>
		public static void Log(string message)
		{
			_logFile?.WriteLine(message);
			Client.ConsoleMessage(message);
		}

		/// <summary>
		/// Write warning log message.
		/// </summary>
		/// <param name="message">Message to write.</param>
		public static void Warning(string message)
		{
			Log("[WARN] " + message);
		}

		/// <summary>
		/// Write error log message.
		/// </summary>
		/// <param name="message">Message to write.</param>
		public static void Error(string message)
		{
			Log("[ERROR] " + message);
		}

		/// <summary>
		/// Write debug message.
		/// </summary>
		/// <param name="message">Message to write.</param>
		public static void Debug(string message)
		{
#if !PUBLIC_RELEASE
			Log("[DEBUG] " + message);
#endif
		}
	}
}
