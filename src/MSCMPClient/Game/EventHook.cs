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
		/// <param name="duplicateCheck">Check if event is running twice.</param>
		public static void Add(PlayMakerFSM fsm, string eventName, Action action, bool duplicateCheck) {
			if (fsm == null) {
				Logger.Log("EventHook: Add failed. (FSM is null!)");
			}
			else {
				FsmState state = fsm.Fsm.GetState(eventName);
				if (state != null) {
					PlayMakerUtils.AddNewAction(state, new CustomAction(action, eventName, duplicateCheck));
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
		/// <param name="syncedVariable">Optional variable to sync event by. Must be contained within the same as the one that is being synced.</param>
		public static void AddWithSync(PlayMakerFSM fsm, string eventName) {
			if (fsm == null) {
				Logger.Log("EventHook: AddWithSync failed. (FSM is null!)");
			}
			else {
				FsmState state = fsm.Fsm.GetState(eventName);
				if (state != null) {
					EventHook.Instance.fsms.Add(EventHook.Instance.fsms.Count + 1, fsm);
					EventHook.Instance.fsmEvents.Add(EventHook.Instance.fsmEvents.Count + 1, eventName);

					PlayMakerUtils.AddNewAction(state, new CustomActionSync(EventHook.Instance.fsms.Count, EventHook.Instance.fsmEvents.Count));
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
			Logger.Log($"Ran event from remote: {"MP_" + EventHook.Instance.fsmEvents[fsmEventID]}");
			EventHook.Instance.fsms[fsmID].SendEvent("MP_" + EventHook.Instance.fsmEvents[fsmEventID]);
		}

		/// <summary>
		/// Action used when adding event hooks.
		/// </summary>
		public class CustomAction : FsmStateAction {
			private Action action;
			private string eventName;
			private bool duplicateCheck;

			public CustomAction(Action a, string eName, bool dCheck) {
				action = a;
				eventName = eName;
				duplicateCheck = dCheck;
			}

			public override void OnEnter() {
				if (this.Fsm.LastTransition.EventName == "MP_" + eventName && duplicateCheck) {
					return;
				}

				action();

				Finish();
			}
		}

		/// <summary>
		/// Action used when adding sycned event hooks.
		/// </summary>
		public class CustomActionSync : FsmStateAction {
			private int fsmID;
			private int fsmEventID;

			public CustomActionSync(int id, int eventID) {
				fsmID = id;
				fsmEventID = eventID;
			}

			public override void OnEnter() {
				if (EventHook.Instance.fsms[fsmID].Fsm.LastTransition.EventName == "MP_" + EventHook.Instance.fsmEvents[fsmEventID]) {
					return;
				}

				Network.NetLocalPlayer.Instance.SendEventHookSync(fsmID, fsmEventID);
				Logger.Log($"Sending sync for event {"MP_" + EventHook.Instance.fsmEvents[fsmEventID]}");

				Finish();
			}
		}
	}
}
