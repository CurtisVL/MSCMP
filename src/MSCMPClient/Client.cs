﻿using System.IO;
using System.Reflection;
using System.Diagnostics;
using UnityEngine;
using System;
using System.Runtime.CompilerServices;

namespace MSCMP
{
	/// <summary>
	/// Main class of the mod.
	/// </summary>
	public class Client {

		/// <summary>
		/// Asset bundle containing multiplayer mod content.
		/// </summary>
		static AssetBundle assetBundle = null;

		/// <summary>
		/// The my summer car game app id.
		/// </summary>
		public static readonly Steamworks.AppId_t GAME_APP_ID = new Steamworks.AppId_t(516750);

		/// <summary>
		/// Starts the mod. Called from Injector.
		/// </summary>
		public static void Start() {
			string logPath = GetPath("clientLog.txt");
			if (!Logger.SetupLogger(logPath)) {
				FatalError($"Cannot setup logger. Log file path: {logPath}");
				return;
			}

			Logger.SetAutoFlush(true);

			Game.Hooks.PlayMakerActionHooks.Install();

			string assetBundlePath = GetPath("../../data/mpdata");
			if (!File.Exists(assetBundlePath)) {
				FatalError("Cannot find mpdata asset bundle.");
				return;
			}

			assetBundle = AssetBundle.CreateFromFile(assetBundlePath);

			var go = new GameObject("Multiplayer GUI Controller");
			go.AddComponent<UI.MPGUI>();

			go = new GameObject("Multiplayer Controller");
			go.AddComponent<MPController>();
		}

		/// <summary>
		/// Gets absolute path for the specified file relative to mod installation folder.
		/// </summary>
		/// <param name="file">The file to get path for.</param>
		/// <returns>Absolute path for the specified file relative to mod instalation folder.</returns>
		public static string GetPath(string file) {
			return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + @"\" + file;
		}

		/// <summary>
		/// Loads asset from multiplayer mod asset bundle.
		/// </summary>
		/// <typeparam name="T">The type of the asset to load.</typeparam>
		/// <param name="name">The name of the asset to load.</param>
		/// <returns>Loaded asset.</returns>
		public static T LoadAsset<T>(string name) where T : UnityEngine.Object {
			return assetBundle.LoadAsset<T>(name);
		}

		/// <summary>
		/// Call this when fatal error occurs. This will print error into the log and close the game.
		/// </summary>
		/// <param name="message">The message to print to console.</param>
		public static void FatalError(string message) {
			Logger.Log(message);
			Logger.Log(Environment.StackTrace);
			ShowMessageBox(message, "MSCMP - Fatal error");

#if DEBUG
			if (Debugger.IsAttached) {
				throw new Exception(message);
			}
			else {
#endif
				Process.GetCurrentProcess().Kill();
#if DEBUG
			}
#endif
		}

		/// <summary>
		/// Standard assertion. If given condition is not true then prints message to the log and closes game.
		/// </summary>
		/// <param name="condition">Condition to chec.</param>
		/// <param name="message">The message to print to console.</param>
		public static void Assert(bool condition, string message) {
			if (condition) {
				return;
			}
			Logger.Log("[ASSERTION FAILED]");
			FatalError(message);
		}

		/// <summary>
		/// Shows system message box to the user. Should be used only during initialization when no ui can be shown in game.
		/// </summary>
		/// <param name="message">The message to show.</param>
		/// <param name="title">The title of the message box.</param>
		[MethodImplAttribute(MethodImplOptions.InternalCall)]
		extern static void ShowMessageBox(string message, string title);

		/// <summary>
		/// The current mod development stage.
		/// </summary>
		public const string MOD_DEVELOPMENT_STAGE = "Pre-Alpha";

		/// <summary>
		/// Get display version of the mod.
		/// </summary>
		/// <returns></returns>
		public static string GetMODDisplayVersion() {
			string version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			version += " " + MOD_DEVELOPMENT_STAGE;
			return version;
		}
	}
}
