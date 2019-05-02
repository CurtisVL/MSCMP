using HutongGames.PlayMaker;
using System;
using System.Collections.Generic;

namespace MSCMP.Game
{
	/// <summary>
	/// Contains methods for hooking PlayMakerFSMs and syncing them with other clients.
	/// </summary>
	internal class EventHook
	{
		public readonly Dictionary<int, PlayMakerFSM> Fsms = new Dictionary<int, PlayMakerFSM>();
		public readonly Dictionary<int, string> FsmEvents = new Dictionary<int, string>();

		public static EventHook Instance;

		/// <summary>
		/// Constructor.
		/// </summary>
		public EventHook()
		{
			Instance = this;
		}

		/// <summary>
		/// Adds a PlayMaker Event hook.
		/// </summary>
		/// <param name="fsm">The PlayMakerFSM that contains the event to hook.</param>
		/// <param name="eventName">The name of the event to hook.</param>
		/// <param name="action">The action to perform on event firing.</param>
		/// <param name="actionOnExit">Should action be put ran on exitting instead of entering event?</param>
		public static void Add(PlayMakerFSM fsm, string eventName, Func<bool> action, bool actionOnExit = false)
		{
			if (fsm == null)
			{
				Client.Assert(true, "EventHook Add: Failed to hook event. (FSM is null)");
			}
			else
			{
				FsmState state = fsm.Fsm.GetState(eventName);
				if (state != null)
				{
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
		public static void AddWithSync(PlayMakerFSM fsm, string eventName, Func<bool> action = null)
		{
			if (fsm == null)
			{
				Client.Assert(true, "EventHook AddWithSync: Failed to hook event. (FSM is null)");
			}
			else
			{
				FsmState state = fsm.Fsm.GetState(eventName);
				if (state != null)
				{
					bool duplicate = false;
					int fsmId = Instance.FsmEvents.Count + 1;
					if (Instance.Fsms.ContainsValue(fsm))
					{
						duplicate = true;
						foreach (KeyValuePair<int, PlayMakerFSM> entry in Instance.Fsms)
						{
							if (entry.Value == fsm)
							{
								fsmId = entry.Key;
								break;
							}
						}
					}
					int eventId = Instance.FsmEvents.Count + 1;
					Instance.Fsms.Add(Instance.Fsms.Count + 1, fsm);
					Instance.FsmEvents.Add(Instance.FsmEvents.Count + 1, eventName);

					PlayMakerUtils.AddNewAction(state, new CustomActionSync(Instance.Fsms.Count, Instance.FsmEvents.Count, action));
					FsmEvent mpEvent = fsm.Fsm.GetEvent("MP_" + eventName);
					PlayMakerUtils.AddNewGlobalTransition(fsm, mpEvent, eventName);

					// Sync with host
					if (!Network.NetManager.Instance.IsHost && Network.NetManager.Instance.IsOnline && duplicate == false)
					{
						Network.NetLocalPlayer.Instance.RequestEventHookSync(fsmId);
					}
				}
			}
		}

		/// <summary>
		/// Runs the specified event.
		/// </summary>
		/// <param name="fsmId">FSM ID.</param>
		/// <param name="fsmEventId">FSM Event ID.</param>
		public static void HandleEventSync(int fsmId, int fsmEventId, string fsmEventName = "none")
		{
			try
			{
				if (fsmEventId == -1)
				{
					Instance.Fsms[fsmId].SendEvent("MP_" + fsmEventName);
				}
				else
				{
					Instance.Fsms[fsmId].SendEvent("MP_" + Instance.FsmEvents[fsmEventId]);
				}
			}
			catch
			{
				Client.Assert(true, $"Handle event sync failed! FSM not found at ID: {fsmId} - Ensure both players are using a new save created on the same version of My Summer Car. Any installed mods could also cause this error.");
			}
		}

		/// <summary>
		/// Responds to requests to sync an FSM event on the remote client.
		/// </summary>
		/// <param name="fsmId">FSM ID.</param>
		public static void SendSync(int fsmId)
		{
			try
			{
				PlayMakerFSM fsm = Instance.Fsms[fsmId];
				string currentState = fsm.Fsm.ActiveStateName;
				if (currentState != "" || currentState != null)
				{
					Network.NetLocalPlayer.Instance.SendEventHookSync(fsmId, -1, currentState);
				}
			}
			catch
			{
				Logger.Debug($"Sync was request for an event, but the FSM wasn't found on this client!");
			}
		}

		/// <summary>
		/// Sync all events within a given FSM.
		/// </summary>
		/// <param name="fsm">FSM to sync Events of.</param>
		/// <param name="action">Optional action, default will only run events for the sync owner, or host is no one owns the object.</param>
		public static void SyncAllEvents(PlayMakerFSM fsm, Func<bool> action = null)
		{
			if (fsm == null)
			{
				Client.Assert(true, "EventHook SyncAllEvents: Failed to hook event. (FSM is null)");
				return;
			}
			FsmState[] states = fsm.FsmStates;

			foreach (FsmState state in states)
			{
				AddWithSync(fsm, state.Name, () =>
				{
					if (action != null)
					{
						return action();
					}

					return false;
				});
			}
		}

		/// <summary>
		/// Action used when adding event hooks.
		/// </summary>
		public class CustomAction : FsmStateAction
		{
			private readonly Func<bool> _action;
			private string _eventName;
			private readonly bool _actionOnExit;

			public CustomAction(Func<bool> theAction, string theEventName, bool onExit)
			{
				_action = theAction;
				_eventName = theEventName;
				_actionOnExit = onExit;
			}

			public bool RunAction(Func<bool> action)
			{
				return action();
			}

			public override void OnEnter()
			{
				if (_actionOnExit == false)
				{
					if (RunAction(_action))
					{
						return;
					}
				}

				Finish();
			}

			public override void OnExit()
			{
				if (_actionOnExit)
				{
					if (RunAction(_action))
					{
						return;
					}
				}

				Finish();
			}
		}

		/// <summary>
		/// Action used when adding sycned event hooks.
		/// </summary>
		public class CustomActionSync : FsmStateAction
		{
			private readonly int _fsmId;
			private readonly int _fsmEventId;
			private readonly Func<bool> _action;

			public CustomActionSync(int theFsmId, int theEventId, Func<bool> theAction)
			{
				_fsmId = theFsmId;
				_fsmEventId = theEventId;
				_action = theAction;
			}

			public bool RunAction(Func<bool> action)
			{
				return action();
			}

			public override void OnEnter()
			{
				if (_action != null)
				{
					if (RunAction(_action))
					{
						return;
					}
				}

				if (Instance.Fsms[_fsmId].Fsm.LastTransition.EventName == "MP_" + Instance.FsmEvents[_fsmEventId])
				{
					return;
				}

				Network.NetLocalPlayer.Instance.SendEventHookSync(_fsmId, _fsmEventId);

				Finish();
			}
		}
	}
}
