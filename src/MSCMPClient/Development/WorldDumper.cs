using UnityEngine;
using MSCMP.Utilities;
using System.Collections.Generic;
using System;
using System.IO;
using System.Reflection;
using HutongGames.PlayMaker;

namespace MSCMP.Development {
	/// <summary>
	/// HTML world dumper class.
	/// </summary>
	class WorldDumper {

		delegate void DumpObjectDelegate(UnityEngine.Object obj, HTMLWriter writer, WorldDumper dumper, string folder);

		Dictionary<Type, DumpObjectDelegate> dumpMethods = new Dictionary<Type, DumpObjectDelegate>();

		static string BuildFileName(UnityEngine.Object obj) {
			var invalids = System.IO.Path.GetInvalidFileNameChars();
			var santitizedName = String.Join("_", obj.name.Split(invalids, StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
			return $"{santitizedName}-{obj.GetInstanceID()}.html";
		}


		private static void PrintValue(object obj, object value, HTMLWriter writer) {
			if (value == null) {
				writer.WriteValue("null");
				return;
			}

			if (value is FsmEvent && obj is FsmStateAction) {
				var fsmEvent = ((FsmEvent)value);
				var action = (FsmStateAction)obj;
				var state = action.State;
				var fsm = action.Fsm;

				writer.WriteValue(fsmEvent.Name+" ");

				foreach (var transition in fsm.GlobalTransitions) {
					if (transition.FsmEvent == fsmEvent) {
						writer.Link($"#{transition.ToState}", $"({transition.ToState})");
					}
				}

				foreach (var transition in state.Transitions) {
					if (transition.FsmEvent == fsmEvent) {
						writer.Link($"#{transition.ToState}", $"({transition.ToState})");
					}
				}
			}
			else {
				writer.WriteValue(value.ToString());
			}

			if (value is NamedVariable) {
				string variableName = ((NamedVariable)value).Name;
				if (variableName.Length > 0) {
					writer.WriteValue($" <div class=\"variable_ref\">{variableName}</div>");
				}
			}
		}

		private static void PrintObjectFields(object obj, HTMLWriter writer) {
			if (obj == null) {
				return;
			}

			writer.StartTag("table", "class=\"fields_table\"");

			Type type = obj.GetType();
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy);

			foreach (var fi in fields) {
				writer.StartTag("tr");
				{
					var fieldType = fi.FieldType;

					writer.StartTag("td", "width=\"50px\"");
					{
						writer.StartTag("div", "class=\"field_type\"");
						writer.WriteValue(fieldType.Name);
						writer.EndTag();
					}
					writer.EndTag();

					writer.StartTag("td", "width=\"100px\"");
					{
						writer.WriteValue(fi.Name);
					}
					writer.EndTag();

					writer.StartTag("td", "width=\"300px\"");
					{
						writer.StartTag("div", "class=\"field_value\"");
						{
							var value = fi.GetValue(obj);
							PrintValue(obj, value, writer);
						}
						writer.EndTag();
					}
					writer.EndTag();
				}
				writer.EndTag();
			}
			writer.EndTag();
		}

		static string GetComponentName(Component component) {
			if (component is PlayMakerFSM) {
				Fsm fsm = ((PlayMakerFSM)component).Fsm;
				if (fsm != null) {
					return fsm.Name;
				}
			}
			return component.name;
		}


		public WorldDumper() {
			RegisterNewDumper<TextAsset>((UnityEngine.Object obj, HTMLWriter writer, WorldDumper dumper, string folder) => {
				TextAsset textAsset = (TextAsset)obj;

				writer.OneLiner("h2", $"Bytes ({textAsset.bytes.Length}):");

				writer.OneLiner("b", "Bytes as string:");
				writer.NewLine();
				writer.OneLiner("textarea", System.Text.Encoding.UTF8.GetString(textAsset.bytes), "rows=\"20\" cols=\"100\" disabled");

				writer.OneLiner("h2", $"Text ({textAsset.text.Length}):");
				writer.OneLiner("textarea", textAsset.text, "rows=\"20\" cols=\"100\" disabled");
			});

			RegisterNewDumper<UnityEngine.Object>((UnityEngine.Object obj, HTMLWriter writer, WorldDumper dumper, string folder) => {
				writer.WriteValue($"THIS IS JUST UnityEngine.Object DUMP! The object type is - {obj.GetType().FullName}");
			});

			RegisterNewDumper<PlayMakerFSM>((UnityEngine.Object obj, HTMLWriter writer, WorldDumper dumper, string folder) => {
				PlayMakerFSM component = (PlayMakerFSM)obj;

				var fsm = component.Fsm;
				if (fsm == null) {
					writer.WriteValue("FSM IS INVALID!");
					return;
				}

				writer.StartTag("h2");
				writer.WriteValue($"Name: {fsm.Name}");
				writer.EndTag();

				writer.WriteValue($"<b>Active state:</b> {fsm.ActiveStateName}<br/>");


				foreach (var state in fsm.States) {
					writer.StartTag("div", "class=\"fsm_state " + ((fsm.ActiveState == state) ? "fsm_active_state" : "") + $"\" id=\"{state.Name}\"");
					{
						writer.OneLiner("div", state.Name, "class=\"fsm_state_name\"");

						FsmTransition finishedTransition = null;
						if (state.Transitions.Length > 0) {
							writer.OneLiner("b", "Transitions:");

							writer.StartTag("table", "class=\"transition_table\"");
							writer.StartTag("tr");
							{
								writer.OneLiner("td", "Event");
								writer.OneLiner("td", "State");
							}
							writer.EndTag();


							foreach (var transition in state.Transitions) {
								writer.StartTag("tr");
								{
									writer.OneLiner("td", transition.EventName);
									writer.StartTag("td");
									writer.Link($"#{transition.ToState}", transition.ToState);
									writer.EndTag();
								}
								writer.EndTag();

								if (transition.EventName == "FINISHED") {
									finishedTransition = transition;
								}
							}
							writer.EndTag();
						}

						writer.StartTag("div", "class=\"state_phase\"");
						writer.WriteValue("START");
						writer.EndTag();

						writer.OneLiner("div", "", "class=\"arrow down center_arrow\"");
						foreach (var action in state.Actions) {
							if (action == null) {
								writer.OneLiner("div", "NULL ACTION!", "class=\"error\"");
								writer.OneLiner("div", "", "class=\"arrow down center_arrow\"");
								continue;
							}

							writer.StartTag("div", $"class=\"fsm_action\"");
							{
								writer.OneLiner("div", action.GetType().Name, $"class=\"fsm_action_name\" title=\"{action.GetType().FullName}\"");

								PrintObjectFields(action, writer);
							}
							writer.EndTag();
							writer.OneLiner("div", "", "class=\"arrow down center_arrow\"");
						}

						writer.StartTag("div", "class=\"state_phase\"");
						writer.WriteValue("END");
						if (finishedTransition != null) {
							writer.WriteValue(" - Jump to: ");
							writer.Link($"#{finishedTransition.ToState}", finishedTransition.ToState);
						}
						writer.EndTag();
					}
					writer.EndTag();
				}
			});

			RegisterNewDumper<Component>((UnityEngine.Object obj, HTMLWriter writer, WorldDumper dumper, string folder) => {
				Component component = (Component)obj;

				writer.WriteValue("COMPONENT!");
			});

			RegisterNewDumper<GameObject>((UnityEngine.Object obj, HTMLWriter writer, WorldDumper dumper, string folder) => {
				GameObject go = (GameObject)obj;

				Component[] components = go.GetComponents<Component>();

				writer.StartTag("h2");
				writer.WriteValue($"Components ({components.Length}):");
				writer.EndTag();

				writer.StartTag("ol");
				foreach (var component in components) {
					writer.StartTag("li");
					{
						string componentFile = dumper.DumpObject(component, $"{folder}/{go.name}/Components", writer.FileName);
						writer.Link(componentFile, $"{GetComponentName(component)} - {component.GetType().Name}");
					}
					writer.EndTag();
				}
				writer.EndTag();


				writer.StartTag("h2");
				writer.WriteValue($"Children ({go.transform.childCount}):");
				writer.EndTag();

				writer.StartTag("ol");

				for (int i = 0; i < go.transform.childCount; ++i) {
					GameObject childrenGo = go.transform.GetChild(i).gameObject;
					writer.StartTag("li");
					{
						string childrenFile = dumper.DumpObject(childrenGo, $"{folder}/{go.name}/Children", writer.FileName);
						writer.Link(childrenFile, childrenGo.name);
					}
					writer.EndTag();
				}
				writer.EndTag();
			});
		}

		void RegisterNewDumper<T>(DumpObjectDelegate dumpDelegate) where T : UnityEngine.Object {
			dumpMethods.Add(typeof(T), dumpDelegate);
		}


		/// <summary>
		/// Dump world to the given folder.
		/// </summary>
		/// <param name="folder">The folder without trailing slash to dump world to - must exists.</param>
		public void Dump(string folder) {
			UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll<UnityEngine.Object>();

			HTMLWriter.WriteDocument($"{folder}/index.html", "INDEX", (HTMLWriter writer) => {

				writer.StartTag("table");
				writer.StartTag("tr");
				{
					writer.OneLiner("td", "#");
					writer.OneLiner("td", "Name");
					writer.OneLiner("td", "Type");
				}
				writer.EndTag();

				int index = 1;
				foreach (var obj in objects) {
					if (CanSkipObject(obj)) {
						continue;
					}

					writer.StartTag("tr");
					{
						writer.OneLiner("td", index.ToString());

						string objectFile = DumpObject(obj, folder, writer.FileName);

						writer.StartTag("td");
						{
							writer.Link(objectFile, ((obj.name.Trim().Length > 0) ? obj.name : "NO NAME"));
						}
						writer.EndTag();

						writer.StartTag("td");
						{
							writer.WriteValue(obj.GetType().Name);
						}
						writer.EndTag();
					}
					writer.EndTag();
					++index;

					System.GC.Collect();
				}
				writer.EndTag();

				WriteStyles(writer);
			});
		}

		private void WriteStyles(HTMLWriter writer) {
			writer.StartTag("style", "type=\"text/css\"");
			{
				writer.WriteValue("body { background: #202020; color: #b3b3b3;  font-family: sans-serif; }");
				writer.WriteValue("a { color: #9e9e9e; }");
				writer.WriteValue(".field_type { background: #418dff; color: #000; text-align: center; border: 1px solid #203e6b; padding: 3px; }");
				writer.WriteValue(".field_value { background: #000; color: #fff; border: 1px solid black; padding: 3px;  }");
				writer.WriteValue(".fields_table { font-size: 10px; margin: 5px; }");

				writer.WriteValue(".fsm_state { background: #252525; color: #a9a9a9; border: 1px solid #cecece; margin: 10px; width: 600px; }");
				writer.WriteValue(".fsm_active_state { border: 2px solid gold; }");
				writer.WriteValue(".fsm_state_name { background: #101010; padding: 10px; }");
				writer.WriteValue(".error { background: #a90000; }");
				writer.WriteValue(".fsm_action { background: #383838; border: 1px solid #000000; margin: 5px; border-radius: 5px; }");
				writer.WriteValue(".fsm_action_name { padding: 10px; font-size: 14px; font-weight: bold; border-bottom: 1px solid black; }");

				writer.WriteValue(".variable_ref { background: #ffb441; color: #000; font-size: 9px; display: inline-block; border-radius: 3px; border: 1px solid #6b4b1a; }");

				writer.WriteValue(".transition_table { margin: 2px; font-size: 12px; }");

				writer.WriteValue(".state_phase { background: #383838; border: 1px solid #000000; margin: 5px; border-radius: 5px; font-size: 18px; text-align: center; }");

				writer.WriteValue(".arrow { border: solid #909090; border-width: 0 2px 2px 0; display: inline-block; padding: 10px; width: 1px; }");

				writer.WriteValue(".right { transform: rotate(-45deg); -webkit-transform: rotate(-45deg); }");
				writer.WriteValue(".left { transform: rotate(135deg); -webkit-transform: rotate(135deg); }");
				writer.WriteValue(".up { transform: rotate(-135deg); -webkit-transform: rotate(-135deg); }");
				writer.WriteValue(".down { transform: rotate(45deg); -webkit-transform: rotate(45deg); }");

				writer.WriteValue(".center_arrow { margin-left: 289px; margin-top: -10px; margin-bottom: 2px; }");
			}
			writer.EndTag();
		}


		bool CanSkipObject(UnityEngine.Object obj) {
			/*if (obj is Transform) {
				if (((Transform)obj).parent != null) {
					return true;
				}
				return false;
			}
			else*/ if (obj is GameObject) {
				if (((GameObject)obj).transform.parent != null) {
					return true;
				}
				return false;
			}

			// Skip rest of the objects.
			return true;
		}


		string DumpObject(UnityEngine.Object obj, string folder, string previousFile = "") {
			Directory.CreateDirectory(folder);
			DumpObjectDelegate bestDumpDelegate = null;

			foreach (var kv in dumpMethods) {
				if (kv.Key.GetHashCode() == obj.GetType().GetHashCode()) {
					bestDumpDelegate = kv.Value;
					break;
				}

				if (obj.GetType().IsSubclassOf(kv.Key)) {
					bestDumpDelegate = kv.Value;
				}
			}

			try {
				if (bestDumpDelegate != null) {
					string fileName = $"{folder}/{BuildFileName(obj)}";
					HTMLWriter.WriteDocument(fileName, $"{obj.name} - {obj.GetType().FullName}", (HTMLWriter writer) => {
						writer.Link(previousFile, "< GO BACK");
						writer.NewLine();
						writer.OneLiner("h1", obj.name);
						writer.OneLiner("b", $"Type: {obj.GetType().FullName}");
						writer.ShortTag("hr");

						bestDumpDelegate.Invoke(obj, writer, this, folder);

						WriteStyles(writer);
					});
					return fileName;
				}
			}
			catch (Exception e) {
				Logger.Error("Failed to dump object!");
				Logger.Error(e.Message);
				Logger.Error(e.StackTrace);
				return $"#exception_{e.Message}";
			}
			return "#failed_to_generate_file";
		}
	}
}
