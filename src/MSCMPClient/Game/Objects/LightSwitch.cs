using HutongGames.PlayMaker;
using UnityEngine;

namespace MSCMP.Game.Objects
{

	/// <summary>
	/// Light switch wrapper
	/// </summary>
	internal class LightSwitch
	{
		private readonly GameObject _go;
		public GameObject GameObject => _go;

		private readonly PlayMakerFSM _fsm;

		//Get switch status
		public bool SwitchStatus => _fsm.FsmVariables.FindFsmBool("Switch").Value;

		/// <summary>
		/// Position of the switch in world.
		/// </summary>
		public Vector3 Position => _go.transform.position;

		public delegate void OnLightSwitchUseEvent(GameObject lswitch, bool turnOn);

		public OnLightSwitchUseEvent OnLightSwitchUse;

		private const string EVENT_NAME = "MPSWITCH";

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="gameObject">Game object of the light switch to represent by this wrapper.</param>
		public LightSwitch(GameObject gameObject)
		{
			_go = gameObject;

			_fsm = Utils.GetPlaymakerScriptByName(_go, "Use");
			if (_fsm.Fsm.HasEvent(EVENT_NAME))
			{
				//Already hooked
				Logger.Log($"Light switch {_go.name} is already hooked!");
			}
			else
			{
				FsmEvent mpEventOn = _fsm.Fsm.GetEvent(EVENT_NAME);
				PlayMakerUtils.AddNewGlobalTransition(_fsm, mpEventOn, "Switch");
				PlayMakerUtils.AddNewAction(_fsm.Fsm.GetState("Switch"), new OnLightSwitchUseAction(this));
			}
		}

		/// <summary>
		/// PlayMaker state action executed when a light switch is used
		/// </summary>
		private class OnLightSwitchUseAction : FsmStateAction
		{
			private readonly LightSwitch _lightSwitch;

			public OnLightSwitchUseAction(LightSwitch theLightSwitch)
			{
				_lightSwitch = theLightSwitch;
			}

			public override void OnEnter()
			{
				Finish();
				Logger.Debug($"Light switch set to: {!_lightSwitch.SwitchStatus}");

				// If use was triggered from our custom event we do not send it.
				if (State.Fsm.LastTransition.EventName == EVENT_NAME)
				{
					return;
				}

				_lightSwitch.OnLightSwitchUse(_lightSwitch._go, !_lightSwitch.SwitchStatus);
			}
		}

		/// <summary>
		/// Toggles light switch
		/// </summary>
		/// <param name="on">On/Off</param>
		public void TurnOn(bool on)
		{
			Logger.Debug($"Toggled light switch, on: {on}");
			if (SwitchStatus != on)
			{
				_fsm.SendEvent(EVENT_NAME);
			}
		}
	}
}
