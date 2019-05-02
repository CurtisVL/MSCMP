using HutongGames.PlayMaker;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSCMP
{
	/// <summary>
	/// Class containing PlayMaker utils.
	/// </summary>
	internal static class PlayMakerUtils
	{
		/// <summary>
		/// Add new global transition from the given event to the state name to the given PlayMaker FSM.
		/// </summary>
		/// <param name="fsm">The PlayMaker FSM to add global transition for.</param>
		/// <param name="ev">The event triggering the transition.</param>
		/// <param name="stateName">The state this transition activates.</param>
		public static void AddNewGlobalTransition(PlayMakerFSM fsm, FsmEvent ev, string stateName)
		{
			FsmTransition[] oldTransitions = fsm.FsmGlobalTransitions;
			List<FsmTransition> temp = oldTransitions.ToList();

			FsmTransition transition = new FsmTransition
			{
				FsmEvent = ev, ToState = stateName
			};
			temp.Add(transition);

			fsm.Fsm.GlobalTransitions = temp.ToArray();
		}
		
		/// <summary>
		/// Add new action into play maker state.
		/// </summary>
		/// <param name="state">The state to add action to.</param>
		/// <param name="action">The action to add.</param>
		public static void AddNewAction(FsmState state, FsmStateAction action)
		{
			FsmStateAction[] oldActions = state.Actions;
			List<FsmStateAction> temp = new List<FsmStateAction>
			{
				action
			};
			temp.AddRange(oldActions);
			state.Actions = temp.ToArray();
		}

		/// <summary>
		/// Removes an event and global transition from an fsm
		/// </summary>
		/// <param name="fsm">The FSM you want to delete it from</param>
		/// <param name="eventName">The event(and global transition) name</param>
		public static void RemoveEvent(PlayMakerFSM fsm, string eventName)
		{
			FsmTransition[] oldTransitions = fsm.FsmGlobalTransitions;
			List<FsmTransition> temp = new List<FsmTransition>();
			foreach (FsmTransition t in oldTransitions)
			{
				if (t.EventName != eventName) temp.Add(t);
			}
			fsm.Fsm.GlobalTransitions = temp.ToArray();

			FsmEvent[] oldEvents = fsm.Fsm.Events;
			fsm.Fsm.Events = oldEvents.Where(t => t.Name != eventName).ToArray();
		}

		/// <summary>
		/// Set a gameObject's state
		/// </summary>
		/// <param name="gameObject">The gameObject you want to set</param>
		/// <param name="fsmName">The FSM that contains the state</param>
		/// <param name="state">The name of the state</param>
		public static void SetToState(GameObject gameObject, string fsmName, string state)
		{
			string hookedEventName = state + "-MSCMP";
			PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(gameObject, fsmName);

			FsmEvent ourEvent = fsm.Fsm.GetEvent(hookedEventName);
			AddNewGlobalTransition(fsm, ourEvent, state);

			fsm.SendEvent(hookedEventName);
			RemoveEvent(fsm, hookedEventName);
		}
	}
}
