using HutongGames.PlayMaker;
using UnityEngine;

namespace MSCMP.Game.Objects {

	/// <summary>
	/// Light switch wrapper
	/// </summary>
	class LightSwitch {
		GameObject go = null;
		public GameObject GameObject { get { return go; } }

		PlayMakerFSM fsm = null;
		LightSwitchManager.SwitchTypes type = LightSwitchManager.SwitchTypes.LightSwitch;

		//Get switch status
		public bool SwitchStatus {
			get {
				if (type == LightSwitchManager.SwitchTypes.LightSwitch) {
					return fsm.FsmVariables.FindFsmBool("Switch").Value;
				}
				else if (type == LightSwitchManager.SwitchTypes.TiemoSwitch) {
					return fsm.FsmVariables.FindFsmBool("SwitchOn").Value;
				}
				else {
					return false;
				}
			}
		}

		/// <summary>
		/// Position of the switch in world.
		/// </summary>
		public Vector3 Position {
			get {
				return go.transform.position;
			}
		}

		public delegate void OnLightSwitchUse(GameObject lswitch, bool turnOn);

		public OnLightSwitchUse onLightSwitchUse;

		private const string EVENT_NAME = "MPSWITCH";

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="gameObject">Game object of the light switch to represent by this wrapper.</param>
		public LightSwitch(GameObject gameObject, LightSwitchManager.SwitchTypes switchType) {
			go = gameObject;
			type = switchType;

			fsm = Utils.GetPlaymakerScriptByName(go, "Use");
			if (fsm.Fsm.HasEvent(EVENT_NAME)) {
				//Already hooked
				Logger.Log($"Light switch {go.name} is already hooked!");
			}
			else {
				if (type == LightSwitchManager.SwitchTypes.LightSwitch) {
					FsmEvent mpEventOn = fsm.Fsm.GetEvent(EVENT_NAME);
					PlayMakerUtils.AddNewGlobalTransition(fsm, mpEventOn, "Switch");
					PlayMakerUtils.AddNewAction(fsm.Fsm.GetState("Switch"), new OnLightSwitchUseAction(this));
				}
				else if (type == LightSwitchManager.SwitchTypes.TiemoSwitch) {
					FsmEvent mpEventOn = fsm.Fsm.GetEvent(EVENT_NAME);
					PlayMakerUtils.AddNewGlobalTransition(fsm, mpEventOn, "Position");
					PlayMakerUtils.AddNewAction(fsm.Fsm.GetState("Position"), new OnLightSwitchUseAction(this));
				}
			}
		}

		/// <summary>
		/// PlayMaker state action executed when a light switch is used
		/// </summary>
		private class OnLightSwitchUseAction : FsmStateAction {
			private LightSwitch lightSwitch;

			public OnLightSwitchUseAction(LightSwitch theLightSwitch) {
				lightSwitch = theLightSwitch;
			}

			public override void OnEnter() {
				Finish();
				Logger.Debug($"Switch set to: {!lightSwitch.SwitchStatus}");

				// If use was triggered from our custom event we do not send it.
				if (State.Fsm.LastTransition.EventName == EVENT_NAME) {
					return;
				}

				lightSwitch.onLightSwitchUse(lightSwitch.go, !lightSwitch.SwitchStatus);
			}
		}

		/// <summary>
		/// Toggles light switch
		/// </summary>
		/// <param name="on">On/Off</param>
		public void TurnOn(bool on) {
			Logger.Debug($"Toggled light switch, on: {on}");
			if (SwitchStatus != on) {
				fsm.SendEvent(EVENT_NAME);
			}
		}
	}
}
