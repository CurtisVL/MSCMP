using HutongGames.PlayMaker;
using MSCMP.Network.Messages;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MSCMP
{
	/// <summary>
	/// Various utilities.
	/// </summary>
	internal static class Utils
	{
		public const int LAYER_DEFAULT = 1 << 0;
		public const int LAYER_TRANSPARENT_FX = 1 << 1;
		public const int LAYER_IGNORE_RAYCAST = 1 << 2;
		public const int LAYER_WATER = 1 << 4;
		public const int LAYER_UI = 1 << 5;

		/// <summary>
		/// Delegate used to print tree of the objects.
		/// </summary>
		/// <param name="level">The level - can be used to generate identation.</param>
		/// <param name="data">The line data.</param>
		public delegate void PrintInfo(int level, string data);

		/// <summary>
		/// Print details about the given object.
		/// </summary>
		/// <param name="level">The level of the print.</param>
		/// <param name="obj">The base typed object contaning action.</param>
		/// <param name="print">The delegate to call to print value.</param>
		private static void PrintObjectFields(int level, object obj, PrintInfo print)
		{
			if (obj == null)
			{
				return;
			}

			if (level > 10)
			{
				print(level + 1, "Out of depth limit.");
				return;
			}

			Type type = obj.GetType();
			FieldInfo[] fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);

			foreach (FieldInfo fieldInfo in fieldInfos)
			{
				object val = fieldInfo.GetValue(obj);
				Type fieldType = fieldInfo.FieldType;

				if (val == null)
				{
					print(level, fieldType.FullName + " " + fieldInfo.Name + " = null");
					continue;
				}

				string additionalString = "";

				if (val is NamedVariable variable)
				{
					additionalString += $" [Named variable: {variable.Name}]";
				}

				print(level, fieldType.FullName + " " + fieldInfo.Name + " = " + val + additionalString);

				if (fieldType.IsClass && (fieldType.Namespace == null || !fieldType.Namespace.StartsWith("System")))
				{
					PrintObjectFields(level + 1, val, print);
				}
			}
		}

		/// <summary>
		/// Helper getting named variable valeu as string.
		/// </summary>
		/// <param name="var"></param>
		/// <returns></returns>
		private static string GetNamedVariableValueAsString(NamedVariable var)
		{
			if (var == null) return "null";

			object value = null;
			if (var is FsmBool fsmBool) { value = fsmBool.Value; }
			if (var is FsmColor color) { value = color.Value; }
			if (var is FsmFloat fsmFloat) { value = fsmFloat.Value; }
			if (var is FsmGameObject gameObject) { value = gameObject.Value; }
			if (var is FsmInt fsmInt) { value = fsmInt.Value; }
			if (var is FsmMaterial material) { value = material.Value; }
			if (var is FsmObject fsmObject) { value = fsmObject.Value; }
			if (var is FsmQuaternion quaternion) { value = quaternion.Value; }
			if (var is FsmRect rect) { value = rect.Value; }
			if (var is FsmString fsmString) { value = fsmString.Value; }
			if (var is FsmTexture texture) { value = texture.Value; }
			if (var is FsmVector2 vector2) { value = vector2.Value; }
			if (var is FsmVector3 vector3) { value = vector3.Value; }

			return value == null 
				? "null" 
				: value.ToString();
		}

		/// <summary>
		/// Prints play maker fsm component details.
		/// </summary>
		/// <param name="pmfsm">The component to print detals for.</param>
		/// <param name="level">The level of print.</param>
		/// <param name="print">The method used to print the details.</param>
		private static void PrintPlaymakerFsmComponent(PlayMakerFSM pmfsm, int level, PrintInfo print)
		{
			// Make sure FSM is initialized.
			pmfsm.Fsm.Init(pmfsm);

			print(level, $"PMFSM Name: {pmfsm.FsmName}");
			print(level, $"Active state: {pmfsm.ActiveStateName}");
			print(level, $"Initialized: {pmfsm.Fsm.Initialized}");

			Logger.Log("EVENTS");
			FsmEvent[] events = pmfsm.FsmEvents;
			foreach (FsmEvent fsmEvent in events)
			{
				if (fsmEvent == null)
				{
					print(level, "Null event!");
					continue;
				}
				print(level, $"Event Name: {fsmEvent.Name} ({fsmEvent.Path})");
			}

			Logger.Log("GT");
			foreach (FsmTransition fsmTransition in pmfsm.FsmGlobalTransitions)
			{
				if (fsmTransition == null)
				{
					print(level, "Null global transition!");
					continue;
				}
				print(level, "Global transition: " + fsmTransition.EventName + " > " + fsmTransition.ToState);
			}

			Logger.Log("STATES");
			FsmState[] states = pmfsm.FsmStates;
			foreach (FsmState fsmState in states)
			{
				if (fsmState == null)
				{
					print(level, "Null state!");
					continue;
				}
				Logger.Log("PRE TRANS");

				print(level, $"State Name: {fsmState.Name} (fsm: {fsmState.Fsm}, go: {fsmState.Fsm.GameObject})");
				foreach (FsmTransition fsmTransition in fsmState.Transitions)
				{
					if (fsmTransition == null)
					{
						print(level + 1, "Null transition!");
						continue;
					}


					print(level + 1, "Transition: " + fsmTransition.EventName + " > " + fsmTransition.ToState);
				}

				Logger.Log("POST TRANS");
				Logger.Log("PRE ACTIONS");
				foreach (FsmStateAction fsmStateAction in fsmState.Actions)
				{
					if (fsmStateAction == null)
					{
						print(level + 1, "Null action!");
						continue;
					}

					print(level + 1, "Action Name: " + fsmStateAction.Name + " (" + fsmStateAction.GetType().FullName + ")");
					PrintObjectFields(level + 2, fsmStateAction, print);
				}
				Logger.Log("POST ACTIONS");
			}

			Logger.Log("VARIABLES");
			print(level, "Variables:");
			NamedVariable[] variables = pmfsm.FsmVariables.GetAllNamedVariables();
			foreach (NamedVariable var in variables)
			{
				print(level + 1, $"{var.Name} = {GetNamedVariableValueAsString(var)}");
			}
		}

		/// <summary>
		/// Prints unity Transform components.
		/// </summary>
		/// <param name="trans">The transform object to print components of.</param>
		/// <param name="level">The level of print.</param>
		/// <param name="print">The delegate to call to print value.</param>
		private static void PrintTransformComponents(Transform trans, int level, PrintInfo print)
		{
			Component[] components = trans.GetComponents<Component>();
			foreach (Component component in components)
			{
				print(level + 1, "C " + component.GetType().FullName + " [" + component.tag + "]");

				switch (component)
				{
					case PlayMakerFSM fsm:
						try
						{
							PrintPlaymakerFsmComponent(fsm, level + 2, print);
						}
						catch (Exception e)
						{
							Logger.Log("XXX");
							Logger.Log(e.StackTrace);
						}

						break;

					case Animation animation:
					{
						Animation anim = animation;
						foreach (AnimationState state in anim)
						{
							print(level + 2, "Animation state: " + state.name);
						}

						break;
					}
				}
			}
		}

		/// <summary>
		/// Prints unity Transform children.
		/// </summary>
		/// <param name="trans">The transform object to print children of.</param>
		/// <param name="level">The level of print.</param>
		/// <param name="print">The delegate to call to print value.</param>
		private static void PrintTransformChildren(Transform trans, int level, PrintInfo print)
		{
			for (int i = 0; i < trans.childCount; ++i)
			{
				Transform child = trans.GetChild(i);
				PrintTransformTree(child, level + 1, print);
			}
		}

		/// <summary>
		/// Prints unity Transform tree starting from trans.
		/// </summary>
		/// <param name="trans">The transform object to start print of the tree.</param>
		/// <param name="level">The level of print. When starting printing it should be 0.</param>
		/// <param name="print">The delegate to call to print value.</param>
		public static void PrintTransformTree(Transform trans, int level, PrintInfo print)
		{
			if (trans == null)
			{
				return;
			}

			print(level, $"> {trans.name} [{trans.tag}, {(trans.gameObject.activeSelf ? "active" : "inactive")}, {trans.gameObject.GetInstanceID()}]");

			PrintTransformComponents(trans, level, print);
			PrintTransformChildren(trans, level, print);
		}

		/// <summary>
		/// Get PlayMaker finite-state-matching from the game objects tree starting from game object.
		/// </summary>
		/// <param name="go">The game object to start searching at.</param>
		/// <param name="name">The name of finite-state-machine to find.</param>
		/// <returns>Finite state machine matching the name or null if no such state machine is found.</returns>
		public static PlayMakerFSM GetPlaymakerScriptByName(GameObject go, string name)
		{
			PlayMakerFSM[] fsms = go.GetComponentsInChildren<PlayMakerFSM>();
			return fsms.FirstOrDefault(fsm => fsm.FsmName == name);
		}

		/// <summary>
		/// Convert game representation of vector into network message.
		/// </summary>
		/// <param name="v3">Vector to convert.</param>
		/// <returns>Vector network message.</returns>
		public static Vector3Message GameVec3ToNet(Vector3 v3)
		{
			Vector3Message msg = new Vector3Message
			{
				x = v3.x,
				y = v3.y,
				z = v3.z
			};
			return msg;
		}

		/// <summary>
		/// Convert network message containing vector into game representation of vector.
		/// </summary>
		/// <param name="msg">The message to convert.</param>
		/// <returns>Converted vector.</returns>
		public static Vector3 NetVec3ToGame(Vector3Message msg)
		{
			Vector3 vec = new Vector3
			{
				x = msg.x,
				y = msg.y,
				z = msg.z
			};
			return vec;
		}

		/// <summary>
		/// Convert game representation of quaternion into network message.
		/// </summary>
		/// <param name="q">Quaternion to convert.</param>
		/// <returns>Quaternion network message.</returns>
		public static QuaternionMessage GameQuatToNet(Quaternion q)
		{
			QuaternionMessage msg = new QuaternionMessage
			{
				w = q.w,
				x = q.x,
				y = q.y,
				z = q.z
			};
			return msg;
		}

		/// <summary>
		/// Convert network message containing quaternion into game representation of quaternion.
		/// </summary>
		/// <param name="msg">The message to convert.</param>
		/// <returns>Converted quaternion.</returns>
		public static Quaternion NetQuatToGame(QuaternionMessage msg)
		{
			Quaternion q = new Quaternion
			{
				w = msg.w,
				x = msg.x,
				y = msg.y,
				z = msg.z
			};
			return q;
		}

		/// <summary>
		/// Delegate contaning safe call code.
		/// </summary>
		public delegate void SafeCall();

		/// <summary>
		/// Perform safe call catching all exceptions that could happen within it's scope.
		/// </summary>
		/// <param name="name">The name of the safe call scope.</param>
		/// <param name="call">The code to execute.</param>
		public static void CallSafe(string name, SafeCall call)
		{
#if DEBUG
			if (System.Diagnostics.Debugger.IsAttached)
			{
				call();
				return;
			}
#endif

			try
			{
				call();
			}
			catch (Exception e)
			{
				Client.FatalError("Safe call " + name + " failed.\n" + e.Message + "\n" + e.StackTrace);
			}
		}

		/// <summary>
		/// Calculate jenkins hash of the given string.
		/// </summary>
		/// <param name="str">The string to calculate jenkins hash of.</param>
		/// <returns>The jenkins hash of the given string.</returns>
		public static int StringJenkinsHash(string str)
		{
			int i = 0;
			int hash = 0;
			while (i != str.Length)
			{
				hash += str[i++];
				hash += hash << 10;
				hash ^= hash >> 6;
			}
			hash += hash << 3;
			hash ^= hash >> 11;
			hash += hash << 15;
			return hash;
		}

		/// <summary>
		/// Check if hierarchy of the given game object matches.
		/// </summary>
		/// <param name="obj">The game object.</param>
		/// <param name="hierarchy">The hierarchy pattern to check.</param>
		/// <returns>true if hierarchy is matching, false otherwise</returns>
		public static bool IsGameObjectHierarchyMatching(GameObject obj, string hierarchy)
		{
			Transform current = obj.transform;
			string[] names = hierarchy.Split('/');
			for (int i = names.Length; i > 0; --i)
			{
				if (current == null)
				{
					return false;
				}

				if (names[i - 1] == "*")
				{
					continue;
				}

				if (current.name != names[i - 1])
				{
					return false;
				}

				current = current.parent;
			}
			return true;
		}


		/// <summary>
		/// Convert p2p session error to string.
		/// </summary>
		/// <param name="sessionError">The session error.</param>
		/// <returns>Session error string.</returns>
		public static string P2PSessionErrorToString(Steamworks.EP2PSessionError sessionError)
		{
			switch (sessionError)
			{
				case Steamworks.EP2PSessionError.k_EP2PSessionErrorNone: return "none";
				case Steamworks.EP2PSessionError.k_EP2PSessionErrorNotRunningApp: return "not running app";
				case Steamworks.EP2PSessionError.k_EP2PSessionErrorNoRightsToApp: return "no rights to app";
				case Steamworks.EP2PSessionError.k_EP2PSessionErrorDestinationNotLoggedIn: return "user not logged in";
				case Steamworks.EP2PSessionError.k_EP2PSessionErrorTimeout: return "timeout";
				default: return "unknown";
			}
		}
	}
}
