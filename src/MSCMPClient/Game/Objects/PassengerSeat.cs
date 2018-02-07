using UnityEngine;
using HutongGames.PlayMaker;

namespace MSCMP.Game.Objects {
	/// <summary>
	/// Handles passenger seats.
	/// 
	/// This method appears to be easier than modifying current passenger seats of vehicles.
	/// </summary>
	/// <param name="other"></param>
	public class PassengerSeat : MonoBehaviour {
		public string VehicleType = null;

		GameObject player = null;
		GameObject trigger = null;

		bool canSit = false;
		bool isSitting = false;
		bool showGUI = false;

		CharacterMotor motor = null;

		GameObject guiGameObject = null;
		PlayMakerFSM iconsFsm = null;
		PlayMakerFSM textFsm = null;

		public delegate void OnEnter();
		public delegate void OnLeave();
		public OnEnter onEnter = () => {
			Logger.Log("On enter passenger seat");
		};
		public OnLeave onLeave = () => {
			Logger.Log("On leave passenger seat");
		};

		/// <summary>
		/// Initialise passenger seat
		/// </summary>
		void Start() {
			Logger.Debug($"Passenger seat added, vehicle: {VehicleType}");

			guiGameObject = GameObject.Find("GUI");

			PlayMakerFSM[] fsms = guiGameObject.GetComponentsInChildren<PlayMakerFSM>();
			foreach (PlayMakerFSM fsm in fsms) {
				if (fsm.FsmName == "Logic") {
					iconsFsm = fsm;
					continue;
				}
				else if (fsm.FsmName == "SetText" && fsm.gameObject.name == "Interaction") {
					textFsm = fsm;
					continue;
				}
				if (iconsFsm != null && textFsm != null) {
					break;
				}
			}

			trigger = this.gameObject;

			trigger.GetComponentInChildren<MeshRenderer>().enabled = false;
		}

		/// <summary>
		/// Triggered on entering the passenger seat.
		/// </summary>
		/// <param name="other"></param>
		void OnTriggerEnter(Collider other) {
			if (other.gameObject.name == "PLAYER") {
				canSit = true;

				if (isSitting == false) {
					showGUI = true;
				}

				if (player == null) {
					player = other.gameObject;
					motor = player.GetComponentInChildren<CharacterMotor>();
				}
			}
		}

		/// <summary>
		/// Triggered on leaving the passenger seat.
		/// </summary>
		/// <param name="other"></param>
		void OnTriggerExit(Collider other) {
			if (other.gameObject.name == "PLAYER") {
				canSit = false;

				showGUI = false;
				iconsFsm.Fsm.GetFsmBool("GUIpassenger").Value = false;
				textFsm.Fsm.GetFsmString("GUIinteraction").Value = "";
			}
		}

		/// <summary>
		/// Called every frame.
		/// </summary>
		void Update() {
			if (showGUI == true) {
				// Yep, this needs to be called on update. Thanks MSC.
				textFsm.Fsm.GetFsmString("GUIinteraction").Value = "ENTER PASSENGER MODE";
				iconsFsm.Fsm.GetFsmBool("GUIpassenger").Value = true;
			}

			if (canSit == true && Input.GetKeyDown(KeyCode.Return) == true) {
				isSitting = !isSitting;
				if (isSitting == true) {
					player.transform.parent = this.gameObject.transform;
					motor.enabled = false;

					showGUI = false;
					iconsFsm.Fsm.GetFsmBool("GUIpassenger").Value = false;
					textFsm.Fsm.GetFsmString("GUIinteraction").Value = "";

					onEnter();
				}
				else {
					player.transform.parent = null;
					motor.enabled = true;

					showGUI = true;

					onLeave();
				}
			}
		}
	}
}
