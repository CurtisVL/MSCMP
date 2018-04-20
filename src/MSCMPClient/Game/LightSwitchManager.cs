using System.Collections.Generic;
using UnityEngine;
using MSCMP.Game.Objects;

namespace MSCMP.Game {
	/// <summary>
	/// Class managing state of the light switches in game.
	/// </summary>
	class LightSwitchManager {
		/// <summary>
		/// Singleton of the light switch manager.
		/// </summary>
		public static LightSwitchManager Instance = null;

		/// <summary>
		/// List of light switches.
		/// </summary>
		public List<LightSwitch> lightSwitches = new List<LightSwitch>();

		public delegate void OnLightSwitchUsed(GameObject lightSwitch, bool turnedOn);

		/// <summary>
		/// Callback called when player used a light switch
		/// </summary>
		public OnLightSwitchUsed onLightSwitchUsed;

		public enum SwitchTypes {
			LightSwitch,
			TiemoSwitch
		}

		public LightSwitchManager() {
			Instance = this;
		}

		~LightSwitchManager() {
			Instance = null;
		}

		/// <summary>
		/// Builds light switch list on world load.
		/// </summary>
		public void OnWorldLoad() {
			lightSwitches.Clear();
			GameObject[] gos = GameObject.FindObjectsOfType<GameObject>();

			//Register all light switches in game.
			foreach (var go in gos) {
				if (go.name.StartsWith("switch_")) {
					AddLightSwitch(go);
				}
			}
		}

		/// <summary>
		/// Adds light switches by GameObject
		/// </summary>
		/// <param name="lightGO">LightSwitch GameObject.</param>
		public void AddLightSwitch(GameObject lightGO) {
			PlayMakerFSM playMakerFsm = Utils.GetPlaymakerScriptByName(lightGO, "Use");
			SwitchTypes switchType = SwitchTypes.LightSwitch;
			if (playMakerFsm == null) {
				return;
			}

			bool isValid = false;
			// Normal light switch
			if (playMakerFsm.FsmVariables.FindFsmBool("Switch") != null) {
				isValid = true;
			}
			else if (playMakerFsm.FsmVariables.FindFsmBool("SwitchOn") != null) {
				isValid = true;
				switchType = SwitchTypes.TiemoSwitch;
			}

			if (isValid) {
				LightSwitch light = new LightSwitch(lightGO, switchType);
				lightSwitches.Add(light);
				Logger.Log($"Registered new switch: {lightGO.name} Type: {switchType}");

				light.onLightSwitchUse = (lightObj, turnedOn) => {
					onLightSwitchUsed(lightGO, !light.SwitchStatus);
				};
			}
		}

		/// <summary>
		/// Find light switch from position
		/// </summary>
		/// <param name="name">Light switch position.</param>
		/// <returns></returns>
		public LightSwitch FindLightSwitch(Vector3 pos) {
			foreach (LightSwitch light in lightSwitches) {
				if ((Vector3.Distance(light.Position, pos) < 0.1f)) {
					return light;
				}
			}
			return null;
		}
	}
}
