using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSCMP
{
#if !PUBLIC_RELEASE
	/// <summary>
	/// Development tools.
	/// </summary>
	internal static class DevTools
	{
		private static bool _devView;

		private static bool _displayClosestObjectNames;
		private static bool _airBreak;

		public static bool NetStats;
		public static bool DisplayPlayerDebug;

		/// <summary>
		/// Game object representing local player.
		/// </summary>
		private static GameObject _localPlayer;

		private static GameObject _playerCamera;

		private const float DEV_MENU_BUTTON_WIDTH = 150.0f;
		private const float TITLE_SECTION_WIDTH = 50.0f;
		private static Rect _devMenuButtonsRect = new Rect(5, 0.0f, DEV_MENU_BUTTON_WIDTH, 25.0f);

		/// <summary>
		/// List of spots to be used in /gotospot. List contains each spot's name, their XYZ Position, and W as Rotation
		/// </summary>
		private static readonly Dictionary<string, Vector4> Spots = new Dictionary<string, Vector4>()
		{
			{ "Home", new Vector4(-10.0f, -0.3f, 7.6f, 180) },
			{ "Island", new Vector4(-851.5f, -2.9f, 516.6f, 163) },
			{ "Teemo", new Vector4(-1546.3f, 3.2f, 1176.8f, 354) },
			{ "Fleetari", new Vector4(1550.0f, 4.8f, 734.3f, 62) },
			{ "Pigman", new Vector4(-171.1f, -3.9f, 1024.9f, 133) },
			{ "Jokkeold", new Vector4(1939.4f, 7.1f, -222.5f, 126) },
			{ "Jokkenew", new Vector4(-1285.6f, 0.2f, 1088.3f, 210) },
			{ "Drag", new Vector4(-1312.6f, 2.2f, -937.6f, 99) }
		};

		public static void OnInit()
		{
			//List of commands
			UI.Console.RegisterCommand("help", args =>
			{
				Client.ConsoleMessage($"Available Commands:");
				Client.ConsoleMessage($"   gotospot [spotName]");
				Client.ConsoleMessage($"   gethere [gameObjectName]");
				Client.ConsoleMessage($"   goto [gameObjectName]");
				Client.ConsoleMessage($"   gotoobj [ObjectID]");
				Client.ConsoleMessage($"   gotoxyz [x] [y] [z] [Optional: Rotation]");
				Client.ConsoleMessage($"   savepos");
			});

			//Teleports yourself to the given spot
			UI.Console.RegisterCommand("gotospot", args =>
			{
				if (args.Length == 1)
				{
					string availableSpots = "";
					foreach (KeyValuePair<string, Vector4> spot in Spots) availableSpots += spot.Key + " ";

					Client.ConsoleMessage($"Valid Spots: {availableSpots}.");
					return;
				}

				if (_localPlayer == null)
				{
					Client.ConsoleMessage("ERROR: Couldn't find local player.");
					return;
				}

				string ourSpot = args[1].ToLower();
				foreach (KeyValuePair<string, Vector4> spot in Spots)
				{
					if (spot.Key.ToLower() != ourSpot)
						continue;

					SetPosition(spot.Value.x, spot.Value.y, spot.Value.z, spot.Value.w);
					Client.ConsoleMessage($"Teleported to {spot}!");
					return;
				}

				Client.ConsoleMessage($"{args[1]} is an invalid spot. Type 'gotospot' with no parameters to see all the available spots!");
			});

			//Teleports a game object to you
			UI.Console.RegisterCommand("gethere", args =>
			{
				if (args.Length == 1)
				{
					Client.ConsoleMessage($"ERROR: Invalid syntax. Use 'gethere [gameObjectName]'.");
					return;
				}

				if (_localPlayer == null)
				{
					Client.ConsoleMessage("ERROR: Couldn't find local player.");
					return;
				}

				string ourObjectName = string.Join(" ", args.Skip(1).ToArray());
				GameObject ourObject = GameObject.Find(ourObjectName);
				if (ourObject == null)
				{
					Client.ConsoleMessage($"ERROR: Couldn't find {ourObjectName}.");
					return;
				}

				ourObject.transform.rotation = _localPlayer.transform.rotation;
				ourObject.transform.position = _localPlayer.transform.position + _localPlayer.transform.rotation * Vector3.forward * 5.0f;
				Client.ConsoleMessage($"Teleported {ourObjectName} to you!");
			});

			//Teleports yourself to a game object
			UI.Console.RegisterCommand("goto", args =>
			{
				if (args.Length == 1)
				{
					Client.ConsoleMessage($"ERROR: Invalid syntax. Use 'goto [gameObjectName]'.");
					return;
				}

				if (_localPlayer == null)
				{
					Client.ConsoleMessage("ERROR: Couldn't find local player.");
					return;
				}

				string ourObjectName = string.Join(" ", args.Skip(1).ToArray());
				GameObject ourObject = GameObject.Find(ourObjectName);
				if (ourObject == null)
				{
					Client.ConsoleMessage($"ERROR: Couldn't find {ourObjectName}.");
					return;
				}

				_localPlayer.transform.position = ourObject.transform.position + Vector3.up * 2.0f;
				Client.ConsoleMessage($"Teleported to {ourObjectName} !");
			});

			// Teleports yourself to a GameObject based on ObjectID
			UI.Console.RegisterCommand("gotoobj", args =>
			{
				if (args.Length == 1)
				{
					Client.ConsoleMessage($"ERROR: Invalid syntax. Use 'gotoobj [ObjectID]'.");
					return;
				}

				if (_localPlayer == null)
				{
					Client.ConsoleMessage("ERROR: Couldn't find local player.");
					return;
				}

				int ourId = Convert.ToInt32(string.Join(" ", args.Skip(1).ToArray()));
				GameObject ourObject = Game.ObjectSyncManager.Instance.ObjectIDs[ourId].gameObject;
				if (ourObject == null)
				{
					Client.ConsoleMessage($"ERROR: Couldn't find GameObject at ID: {ourId}");
					return;
				}

				_localPlayer.transform.position = ourObject.transform.position + Vector3.up * 2.0f;
				Client.ConsoleMessage($"Teleported to {ourObject.name}");
			});

			//Teleports yourself to the specific coordinates
			UI.Console.RegisterCommand("gotoxyz", args =>
			{
				if (args.Length < 4)
				{
					Client.ConsoleMessage($"ERROR: Invalid syntax. Use 'gotoxyz [x] [y] [z] [Optional: Rotation]'.");
					return;
				}

				if (_localPlayer == null)
				{
					Client.ConsoleMessage("ERROR: Couldn't find local player.");
					return;
				}

				_localPlayer.transform.position = new Vector3(float.Parse(args[1]), float.Parse(args[2]), float.Parse(args[3]));

				if (args.Length == 5) _localPlayer.transform.eulerAngles = new Vector3(0, float.Parse(args[4]), 0);
				Client.ConsoleMessage($"Teleported to {args[1]}, {args[2]}, {args[3]}!");
			});

			//Logs your position and rotation
			UI.Console.RegisterCommand("savepos", args =>
			{
				if (_localPlayer == null)
				{
					Client.ConsoleMessage("ERROR: Couldn't find local player.");
					return;
				}

				Logger.Log("Saved Position:");
				Logger.Log($"Pos: {_localPlayer.transform.position}");
				Logger.Log($"Rot: {_localPlayer.transform.eulerAngles}");
				Client.ConsoleMessage("Saved your position!");
			});
		}

		public static void OnGui()
		{
			if (_displayClosestObjectNames)
			{
				DrawClosestObjectNames();
			}

			if (!_devView)
			{
				return;
			}

			_devMenuButtonsRect.x = 5.0f;
			_devMenuButtonsRect.y = 0.0f;

			NewSection("Toggles:");
			Checkbox("Net stats", ref NetStats);
			Checkbox("Net stats - players dbg", ref DisplayPlayerDebug);
			Checkbox("Display object names", ref _displayClosestObjectNames);
			Checkbox("AirBreak", ref _airBreak);

			NewSection("Actions:");

			if (Action("Dump world"))
			{
				DumpWorld(Application.loadedLevelName);
			}

			if (Action("Dump local player"))
			{
				DumpLocalPlayer();
			}
		}

		private static void NewSection(string title)
		{
			_devMenuButtonsRect.x = 5.0f;
			_devMenuButtonsRect.y += 25.0f;

			GUI.color = Color.white;
			GUI.Label(_devMenuButtonsRect, title);
			_devMenuButtonsRect.x += TITLE_SECTION_WIDTH;
		}

		private static void Checkbox(string name, ref bool state)
		{
			GUI.color = state ? Color.green : Color.white;
			if (GUI.Button(_devMenuButtonsRect, name))
			{
				state = !state;
			}
			_devMenuButtonsRect.x += DEV_MENU_BUTTON_WIDTH;
		}

		private static bool Action(string name)
		{
			GUI.color = Color.white;
			bool execute = GUI.Button(_devMenuButtonsRect, name);
			_devMenuButtonsRect.x += DEV_MENU_BUTTON_WIDTH;
			return execute;
		}

		private static void DrawClosestObjectNames()
		{
			foreach (GameObject gameObject in Object.FindObjectsOfType<GameObject>())
			{
				if (_localPlayer)
				{
					if ((gameObject.transform.position - _localPlayer.transform.position).sqrMagnitude > 10)
					{
						continue;
					}
				}

				Vector3 pos = Camera.main.WorldToScreenPoint(gameObject.transform.position);
				if (pos.z < 0.0f)
				{
					continue;
				}

				GUI.Label(new Rect(pos.x, Screen.height - pos.y, 500, 20), gameObject.name);
			}
		}

		public static void Update()
		{
			if (_localPlayer == null)
			{
				_localPlayer = GameObject.Find("PLAYER");
				_playerCamera = GameObject.Find("FPSCamera");
			}
			else
			{
				UpdatePlayer();
			}

			if (Input.GetKeyDown(KeyCode.F3))
			{
				_devView = !_devView;
			}
		}

		public static void UpdatePlayer()
		{
			// Pseudo AirBrk
			if (_airBreak)
			{
				float speed = 3.0f;

				if (Input.GetKey(KeyCode.Mouse0)) speed += 2.5f;
				if (Input.GetKey(KeyCode.Mouse1)) speed -= 2.5f;

				if (Input.GetKeyDown(KeyCode.Keypad5))
				{
					_localPlayer.GetComponent<CharacterController>().enabled = !_localPlayer.GetComponent<CharacterController>().enabled;
				}
				if (Input.GetKey(KeyCode.KeypadPlus))
				{
					_localPlayer.transform.position += Vector3.up * speed;
				}
				if (Input.GetKey(KeyCode.KeypadMinus))
				{
					_localPlayer.transform.position -= Vector3.up * speed;
				}
				if (Input.GetKey(KeyCode.Keypad8))
				{
					//localPlayer.transform.position = localPlayer.transform.position + localPlayer.transform.rotation * Vector3.forward * speed;
					_localPlayer.transform.position += _playerCamera.transform.rotation * Vector3.forward * speed;
				}
				if (Input.GetKey(KeyCode.Keypad2))
				{
					//localPlayer.transform.position = localPlayer.transform.position - localPlayer.transform.rotation * Vector3.forward * speed;
					_localPlayer.transform.position -= _playerCamera.transform.rotation * Vector3.forward * speed;
				}
				if (Input.GetKey(KeyCode.Keypad4))
				{
					_localPlayer.transform.position -= _localPlayer.transform.rotation * Vector3.right * speed;
				}
				if (Input.GetKey(KeyCode.Keypad6))
				{
					_localPlayer.transform.position += _localPlayer.transform.rotation * Vector3.right * speed;
				}
			}
		}

		private static void DumpLocalPlayer()
		{
			StringBuilder builder = new StringBuilder();
			Utils.PrintTransformTree(_localPlayer.transform, 0, (level, text) =>
			{

				for (int i = 0; i < level; ++i) builder.Append("    ");
				builder.Append(text + "\n");
			});
			File.WriteAllText(Client.GetPath("localPlayer.txt"), builder.ToString());
		}

		public static void DumpWorld(string levelName)
		{
			Utils.CallSafe("DUmpWorld", () =>
			{
				Development.WorldDumper worldDumper = new Development.WorldDumper();
				string dumpFolder = Client.GetPath($"HTMLWorldDump\\{levelName}");
				if (Directory.Exists(dumpFolder))
				{
					Directory.Delete(dumpFolder, true);
				}

				Directory.CreateDirectory(dumpFolder);

				Stopwatch watch = Stopwatch.StartNew();

				worldDumper.Dump(dumpFolder);

				Logger.Log($"World dump finished - took {watch.ElapsedMilliseconds} ms");
			});

			/*GameObject[] gos = gos = GameObject.FindObjectsOfType<GameObject>();

			string path = Client.GetPath($"WorldDump\\{levelName}");
			Directory.CreateDirectory(path);

			StringBuilder builder = new StringBuilder();
			int index = 0;
			foreach (GameObject go in gos) {
				Transform trans = go.GetComponent<Transform>();
				if (trans == null || trans.parent != null) continue;

				string SanitizedName = go.name;
				SanitizedName = SanitizedName.Replace("/", "");
				string dumpFilePath = path + "\\" + SanitizedName + ".txt";
				try {
					DumpObject(trans, dumpFilePath);
				}
				catch (Exception e) {
					Logger.Log("Unable to dump objects: " + SanitizedName + "\n");
					Logger.Log(e.Message + "\n");
				}

				builder.Append(go.name + " (" + SanitizedName + "), Trans: " + trans.position.ToString() + "\n");
				++index;
			}

			System.IO.File.WriteAllText(path + "\\dumpLog.txt", builder.ToString());*/
		}

		public static void DumpObject(Transform obj, string file)
		{
			StringBuilder bldr = new StringBuilder();
			Utils.PrintTransformTree(obj, 0, (level, text) =>
			{
				for (int i = 0; i < level; ++i) bldr.Append("    ");
				bldr.Append(text + "\n");
			});

			File.WriteAllText(file, bldr.ToString());
		}

		private static void SetPosition(float x, float y, float z, float rot = -1)
		{
			_localPlayer.transform.position = new Vector3(x, y, z);
			if (rot != -1) _localPlayer.transform.eulerAngles = new Vector3(0, rot, 0);
		}
	}
#endif
}
