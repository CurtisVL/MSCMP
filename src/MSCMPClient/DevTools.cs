using UnityEngine;
using System.Text;
using System.IO;
using System;
using System.Diagnostics;

namespace MSCMP {
#if !PUBLIC_RELEASE
	/// <summary>
	/// Development tools.
	/// </summary>
	static class DevTools {

		static bool devView = false;

		static bool displayClosestObjectNames = false;
		static bool airBreak = false;

		public static bool netStats = false;
		public static bool displayPlayerDebug = false;

		/// <summary>
		/// Game object representing local player.
		/// </summary>
		static GameObject localPlayer = null;

		const float DEV_MENU_BUTTON_WIDTH = 150.0f;
		const float TITLE_SECTION_WIDTH = 50.0f;
		static Rect devMenuButtonsRect = new Rect(5, 0.0f, DEV_MENU_BUTTON_WIDTH, 25.0f);

		//Debug Model variables
		static GameObject ourDebugPlayer;
		static Animation characterAnimationComponent;
		static bool controlDebugPlayer = false;

		public static void OnGUI() {
			if (displayClosestObjectNames) {
				DrawClosestObjectNames();
			}

			if (!devView) {
				return;
			}


			devMenuButtonsRect.x = 5.0f;
			devMenuButtonsRect.y = 0.0f;

			NewSection("Toggles:");
			Checkbox("Net stats", ref netStats);
			Checkbox("Net stats - players dbg", ref displayPlayerDebug);
			Checkbox("Display object names", ref displayClosestObjectNames);
			Checkbox("AirBreak", ref airBreak);
			Checkbox("Control Debug Model", ref controlDebugPlayer);

			NewSection("Actions:");

			if (Action("Dump world")) {
				DumpWorld(Application.loadedLevelName);
			}

			if (Action("Dump local player")) {
				DumpLocalPlayer();
			}
		}

		static void NewSection(string title) {
			devMenuButtonsRect.x = 5.0f;
			devMenuButtonsRect.y += 25.0f;

			GUI.color = Color.white;
			GUI.Label(devMenuButtonsRect, title);
			devMenuButtonsRect.x += TITLE_SECTION_WIDTH;
		}

		static void Checkbox(string name, ref bool state) {
			GUI.color = state ? Color.green : Color.white;
			if (GUI.Button(devMenuButtonsRect, name)) {
				state = !state;
			}
			devMenuButtonsRect.x += DEV_MENU_BUTTON_WIDTH;
		}

		static bool Action(string name) {
			GUI.color = Color.white;
			bool execute = GUI.Button(devMenuButtonsRect, name);
			devMenuButtonsRect.x += DEV_MENU_BUTTON_WIDTH;
			return execute;
		}

		static void DrawClosestObjectNames() {
			foreach (GameObject go in GameObject.FindObjectsOfType<GameObject>()) {
				if (localPlayer) {
					if ((go.transform.position - localPlayer.transform.position).sqrMagnitude > 10) {
						continue;
					}
				}

				Vector3 pos = Camera.main.WorldToScreenPoint(go.transform.position);
				if (pos.z < 0.0f) {
					continue;
				}


				GUI.Label(new Rect(pos.x, Screen.height - pos.y, 500, 20), go.name);
			}
		}

		public static void Update() {
			if (localPlayer == null) {
				localPlayer = GameObject.Find("PLAYER");
			}
			else {
				UpdatePlayer();
			}

			if (Input.GetKeyDown(KeyCode.F3)) {
				devView = !devView;
			}

			if (ourDebugPlayer != null && localPlayer != null) {
				CheckBlendedAnimationStates();

				float aimLook = localPlayer.transform.FindChild("Pivot/Camera/FPSCamera").transform.rotation.eulerAngles.x;
				Transform spine = ourDebugPlayer.transform.FindChild("pelvis/spine_mid/shoulders/head");
				spine.rotation *= Quaternion.Euler(0, 0, -aimLook);

				if (controlDebugPlayer) {
					if (Input.GetKeyDown(KeyCode.Keypad7)) characterAnimationComponent.CrossFade("Idle");
					if (Input.GetKeyDown(KeyCode.Keypad8)) characterAnimationComponent.CrossFade("Walk");

					if (Input.GetKeyDown(KeyCode.Keypad4)) {
						if (!IsPlayingAnim("Drunk")) characterAnimationComponent.CrossFade("Drunk");
						else characterAnimationComponent.Blend("Drunk", 0);
					}

					if (Input.GetKeyDown(KeyCode.Keypad5)) {
						if (!IsPlayingAnim("Jump")) characterAnimationComponent.CrossFade("Jump");
						else characterAnimationComponent.Blend("Jump", 0);
					}

					if (Input.GetKeyDown(KeyCode.Keypad1)) {
						if (IsPlayingAnim("Lean")) PlayActionAnim("Lean", false);
						else PlayActionAnim("Lean", true);
					}

					if (Input.GetKeyDown(KeyCode.Keypad2)) {
						if (IsPlayingAnim("Finger")) PlayActionAnim("Finger", false);

						if (IsPlayingAnim("Hitchhike")) PlayActionAnim("Hitchhike", false);
						else PlayActionAnim("Hitchhike", true);
					}

					if (Input.GetKeyDown(KeyCode.Keypad3)) {
						if (IsPlayingAnim("Hitchhike")) PlayActionAnim("Hitchhike", false);

						if (IsPlayingAnim("Finger")) PlayActionAnim("Finger", false);
						else PlayActionAnim("Finger", true);
					}
				}
			}
		}

		public static void UpdatePlayer() {
			// Testing New Model
			if (Input.GetKeyDown(KeyCode.KeypadMultiply)) {
				bool useNewClothes = ourDebugPlayer == null;

				ourDebugPlayer = LoadCustomCharacter(localPlayer);
				if (useNewClothes) {
					ApplyCustomTexture(ourDebugPlayer, MaterialId.Face, "MPchar_face02.dds");
					ApplyCustomTexture(ourDebugPlayer, MaterialId.Shirt, "MPchar_shirt05.dds");
					ApplyCustomTexture(ourDebugPlayer, MaterialId.Pants, "MPchar_pants02.dds");
				}
			}

			if (airBreak) {
				// Pseudo AirBrk
				if (Input.GetKey(KeyCode.KeypadPlus)) {
					localPlayer.transform.position = localPlayer.transform.position + Vector3.up * 5.0f;
				}
				if (Input.GetKey(KeyCode.KeypadMinus)) {
					localPlayer.transform.position = localPlayer.transform.position - Vector3.up * 5.0f;
				}
				if (Input.GetKey(KeyCode.Keypad8)) {
					localPlayer.transform.position = localPlayer.transform.position + localPlayer.transform.rotation * Vector3.forward * 5.0f;
				}
				if (Input.GetKey(KeyCode.Keypad2)) {
					localPlayer.transform.position = localPlayer.transform.position - localPlayer.transform.rotation * Vector3.forward * 5.0f;
				}
				if (Input.GetKey(KeyCode.Keypad4)) {
					localPlayer.transform.position = localPlayer.transform.position - localPlayer.transform.rotation * Vector3.right * 5.0f;
				}
				if (Input.GetKey(KeyCode.Keypad6)) {
					localPlayer.transform.position = localPlayer.transform.position + localPlayer.transform.rotation * Vector3.right * 5.0f;
				}
			}
		}

		static void PlayActionAnim(string animName, bool play) {
			if (play) {
				characterAnimationComponent[animName].wrapMode = WrapMode.ClampForever;
				characterAnimationComponent[animName].speed = 1;
				characterAnimationComponent[animName].enabled = true;
				characterAnimationComponent[animName].weight = 1.0f;
			}
			else {
				characterAnimationComponent[animName].wrapMode = WrapMode.Once;
				if (characterAnimationComponent[animName].time > characterAnimationComponent[animName].length) {
					characterAnimationComponent[animName].time = characterAnimationComponent[animName].length;
				}
				characterAnimationComponent[animName].speed = -1;
				characterAnimationComponent[animName].weight = 1.0f;
			}
		}

		static bool IsPlayingAnim(string animName) {
			if (characterAnimationComponent == null) return false;
			if (characterAnimationComponent[animName].enabled == true) return true;

			return false;
		}

		static void CheckBlendedAnimationStates() {
			if (characterAnimationComponent["Jump"].time != 0.0f && characterAnimationComponent["Jump"].weight == 0.0f) {
				characterAnimationComponent["Jump"].enabled = false;
				characterAnimationComponent["Jump"].time = 0;
			}

			if (characterAnimationComponent["Drunk"].time != 0.0f && characterAnimationComponent["Drunk"].weight == 0.0f)
			{
				characterAnimationComponent["Drunk"].enabled = false;
				characterAnimationComponent["Drunk"].time = 0;
			}
		}

		static GameObject LoadCustomCharacter(GameObject localPlayer) {
			GameObject loadedModel = Client.LoadAsset<GameObject>("Assets/MPPlayerModel/MPPlayerModel.fbx");
			GameObject ourCustomPlayer = (GameObject)GameObject.Instantiate((GameObject)loadedModel);

			characterAnimationComponent = ourCustomPlayer.GetComponent<Animation>();
			characterAnimationComponent["Jump"].layer = 1;
			characterAnimationComponent["Drunk"].layer = 2;
			characterAnimationComponent["Drunk"].blendMode = AnimationBlendMode.Additive;
			characterAnimationComponent["Lean"].layer = 3;
			characterAnimationComponent["Lean"].blendMode = AnimationBlendMode.Additive;
			characterAnimationComponent["Finger"].layer = 3;
			characterAnimationComponent["Finger"].blendMode = AnimationBlendMode.Additive;
			characterAnimationComponent["Hitchhike"].layer = 3;
			characterAnimationComponent["Hitchhike"].blendMode = AnimationBlendMode.Additive;

			ourCustomPlayer.transform.position = localPlayer.transform.position + Vector3.up * 0.60f + localPlayer.transform.rotation * Vector3.forward * 1.0f;
			ourCustomPlayer.transform.rotation = localPlayer.transform.rotation * Quaternion.Euler(0, 180, 0);

			return ourCustomPlayer;
		}

		static bool ApplyCustomColorTextures(GameObject gameObject) {
			Renderer objectRenderer = ourDebugPlayer.GetComponentInChildren<Renderer>();
			if (objectRenderer == null) return false;
			//Logger.Log("Total Materials: " + testRenderer.materials.Length);

			StreamReader SettingsFile = new StreamReader(Client.GetPath("myCharacter.ini"));
			string line = null;
			for(int i=0; i<=2;i++) {
				if ((line = SettingsFile.ReadLine()) == null) break;
				string[] LineData = line.Split('|');

				float red = Convert.ToSingle(LineData[0]);
				float green = Convert.ToSingle(LineData[1]);
				float blue = Convert.ToSingle(LineData[2]);

				Color OurColor = new Color(red, green, blue);
				objectRenderer.materials[i].color = OurColor;
			}

			SettingsFile.Close();
			return true;
		}

		enum MaterialId {
			Face,
			Shirt,
			Pants
		}
		static string[] MaterialNames = new string[] {
			"MPchar_face03",
			"MPchar_shirt06",
			"MPchar_pants03"
		};

		/// <summary>
		/// Convert material id to it's name.
		/// </summary>
		/// <param name="material">The id of the material.</param>
		/// <returns>Name of the material.</returns>
		static string GetCharacterMaterialName(MaterialId material) {
			return MaterialNames[(int)material];
		}

		static bool ApplyCustomTexture(GameObject gameObject, MaterialId material, string newTexture) {
			Renderer objectRenderer = ourDebugPlayer.GetComponentInChildren<Renderer>();
			if (objectRenderer == null) return false;

			for (int i = 0; i < objectRenderer.materials.Length; i++) {
				string addon = " (Instance)";
				string materialName = GetCharacterMaterialName(material) + addon;
				if (materialName != objectRenderer.materials[i].name) continue;

				Texture2D texture = (Texture2D)Client.LoadAsset<Texture2D>("Assets/MPPlayerModel/Textures/" + newTexture);
				objectRenderer.materials[i].mainTexture = texture;
				break;
			}
			return true;
		}

		static void DumpLocalPlayer() {
			StringBuilder builder = new StringBuilder();
			Utils.PrintTransformTree(localPlayer.transform, 0, (int level, string text) => {
				for (int i = 0; i < level; ++i) builder.Append("    ");
				builder.Append(text + "\n");
			});
			System.IO.File.WriteAllText(Client.GetPath("localPlayer.txt"), builder.ToString());
		}

		public static void DumpWorld(string levelName) {
			Utils.CallSafe("DUmpWorld", ()=> {
				Development.WorldDumper worldDumper = new Development.WorldDumper();
				string dumpFolder = Client.GetPath($"HTMLWorldDump\\{levelName}");
				Directory.Delete(dumpFolder, true);
				Directory.CreateDirectory(dumpFolder);

				var watch = Stopwatch.StartNew();

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

		public static void DumpObject(Transform obj, string file) {
			StringBuilder bldr = new StringBuilder();
			Utils.PrintTransformTree(obj, 0, (int level, string text) => {
				for (int i = 0; i < level; ++i) bldr.Append("    ");
				bldr.Append(text + "\n");
			});

			System.IO.File.WriteAllText(file, bldr.ToString());
		}
	}
#endif
}
