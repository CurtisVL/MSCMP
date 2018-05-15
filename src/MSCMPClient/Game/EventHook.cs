using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;

namespace MSCMP.Game {
	class EventHook {
		/// <summary>
		/// Adds a PlayMaker Event hook.
		/// </summary>
		/// <param name="fsm">The PlayMakerFSM that contains the event to hook.</param>
		/// <param name="eventName">The name of the event to hook.</param>
		/// <param name="task">The</param>
		public static void Add(PlayMakerFSM fsm, string eventName, Action action) {
			FsmState state = fsm.Fsm.GetState(eventName);
			PlayMakerUtils.AddNewAction(state, new CustomAction(action));
			FsmEvent mpEvent = fsm.Fsm.GetEvent("MP_" + eventName);
			PlayMakerUtils.AddNewGlobalTransition(fsm, mpEvent, eventName);
		}

		/// <summary>
		/// Adds a PlayMaker Event hook and syncs event with remote clients.
		/// </summary>
		/// <param name="fsm">The PlayMakerFSM that contains the event to hook.</param>
		/// <param name="eventName">The name of the event to hook.</param>
		/// <param name="task">The</param>
		public static void AddWithSync(PlayMakerFSM fsm, string eventName, Action action) {
			// To Do
			// Must send MP version of event to client. ("MP_ACC", etc.)
		}

		/// <summary>
		/// Runs the specified event.
		/// </summary>
		/// <param name="fsm">The PlayMakerFSM that contains the event to run.</param>
		/// <param name="eventName">The name of the event to run. (Must be MP version of event)</param>
		private static void RunEvent(PlayMakerFSM fsm, string eventName) {
			Logger.Log($"Ran event from remote: {eventName}");
			fsm.SendEvent(eventName);
		}

		/// <summary>
		/// Action used when adding event hooks.
		/// </summary>
		public class CustomAction : FsmStateAction {
			private Action action;

			public CustomAction(Action a) {
				action = a;
			}

			public override void OnEnter() {
				action();

				Finish();
			}
		}
	}
}
