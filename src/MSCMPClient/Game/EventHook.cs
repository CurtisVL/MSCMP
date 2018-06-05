using System;
using System.Collections.Generic;
using HutongGames.PlayMaker;

namespace MSCMP.Game {
	/// <summary>
	/// Removes the need to have lots of FSMStateActions by having single methods for adding and syncing FSM Events.
	/// </summary>
	class EventHook {
		public Dictionary<int, PlayMakerFSM> fsms = new Dictionary<int, PlayMakerFSM>();
		public Dictionary<int, string> fsmEvents = new Dictionary<int, string>();

		public static EventHook Instance = null;

		/// <summary>
		/// Constructor.
		/// </summary>
		public EventHook() {
			Instance = this;
		}

		/// <summary>
		/// Adds a PlayMaker Event hook.
		/// </summary>
		/// <param name="fsm">The PlayMakerFSM that contains the event to hook.</param>
		/// <param name="eventName">The name of the event to hook.</param>
		/// <param name="action">The action to perform on event firing.</param>
		/// <param name="actionOnExit">Should action be put ran on exitting instead of entering event?</param>
		public static void Add(PlayMakerFSM fsm, string eventName, Func<bool> action, bool actionOnExit = false) {
			if (fsm == null) {
				Logger.Log("EventHook: Add failed. (FSM is null!)");
			}
			else {
				FsmState state = fsm.Fsm.GetState(eventName);
				if (state != null) {
					PlayMakerUtils.AddNewAction(state, new CustomAction(action, eventName, actionOnExit));
					FsmEvent mpEvent = fsm.Fsm.GetEvent("MP_" + eventName);
					PlayMakerUtils.AddNewGlobalTransition(fsm, mpEvent, eventName);
				}
			}
		}

		/// <summary>
		/// Adds a PlayMaker Event hook and syncs event with remote clients.
		/// </summary>
		/// <param name="fsm">The PlayMakerFSM that contains the event to hook.</param>
		/// <param name="eventName">The name of the event to hook.</param>
		/// <param name="action">Optional action to run. (Action runs before duplicate check!)</param>
		public static void AddWithSync(PlayMakerFSM fsm, string eventName, Func<bool> action = null) {
			if (fsm == null) {
				Logger.Log("EventHook: AddWithSync failed. (FSM is null!)");
			}
			else {
				FsmState state = fsm.Fsm.GetState(eventName);
				if (state != null) {
					Instance.fsms.Add(Instance.fsms.Count + 1, fsm);
					Instance.fsmEvents.Add(Instance.fsmEvents.Count + 1, eventName);

					PlayMakerUtils.AddNewAction(state, new CustomActionSync(Instance.fsms.Count, Instance.fsmEvents.Count, action));
					FsmEvent mpEvent = fsm.Fsm.GetEvent("MP_" + eventName);
					PlayMakerUtils.AddNewGlobalTransition(fsm, mpEvent, eventName);
				}
			}
		}

		/// <summary>
		/// Runs the specified event.
		/// </summary>
		/// <param name="fsmID">FSM ID.</param>
		/// <param name="fsmEventID">FSM Event ID.</param>
		public static void HandleEventSync(int fsmID, int fsmEventID) {
			Logger.Log($"Ran event from remote: {"MP_" + Instance.fsmEvents[fsmEventID]}");
			Instance.fsms[fsmID].SendEvent("MP_" + Instance.fsmEvents[fsmEventID]);
		}

		/// <summary>
		/// Action used when adding event hooks.
		/// </summary>
		public class CustomAction : FsmStateAction {
			private Func<bool> action;
			private string eventName;
			private bool actionOnExit;

			public CustomAction(Func<bool> a, string eName, bool onExit) {
				action = a;
				eventName = eName;
				actionOnExit = onExit;
			}

			public bool RunAction(Func<bool> action) {
				return action();
			}

			public override void OnEnter() {
				if (actionOnExit == false) {
					if (RunAction(action) == true) {
						return;
					}
				}

				Finish();
			}

			public override void OnExit() {
				if (actionOnExit == true) {
					if (RunAction(action) == true) {
						return;
					}
				}

				Finish();
			}
		}

		/// <summary>
		/// Action used when adding sycned event hooks.
		/// </summary>
		public class CustomActionSync : FsmStateAction {
			private int fsmID;
			private int fsmEventID;
			private Func<bool> action;

			public CustomActionSync(int id, int eventID, Func<bool> a) {
				fsmID = id;
				fsmEventID = eventID;
				action = a;
			}
			
			public bool RunAction(Func<bool> action) {
				return action();
			}

			public override void OnEnter() {
				if (action != null) {
					if (RunAction(action) == true) {
						return;
					}
				}

				if (Instance.fsms[fsmID].Fsm.LastTransition.EventName == "MP_" + Instance.fsmEvents[fsmEventID]) {
					return;
				}

				Network.NetLocalPlayer.Instance.SendEventHookSync(fsmID, fsmEventID);
				Logger.Log($"Sending sync for event {"MP_" + Instance.fsmEvents[fsmEventID]}");

				Finish();
			}
		}
	}
}
