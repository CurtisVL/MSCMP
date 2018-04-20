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
		public GameObject DriversSeat = null;

		GameObject player = null;
		GameObject trigger = null;

		CapsuleCollider playerCollider = null;

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

			// Set seat position and size based on vehicle
			trigger = this.gameObject;

			// Van
			if (VehicleType.StartsWith("HAYOSIKO")) {
				trigger.transform.position = new Vector3(DriversSeat.transform.position.x + 0.7f, DriversSeat.transform.position.y - 0.1f, DriversSeat.transform.position.z);
				trigger.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
			}

			// Truck
			if (VehicleType.StartsWith("GIFU")) {
				trigger.transform.position = new Vector3(DriversSeat.transform.position.x - 0.1f, DriversSeat.transform.position.y - 0.08f, DriversSeat.transform.position.z - 1.35f);
				trigger.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
			}

			// Old car
			if (VehicleType.StartsWith("RCO_RUSCKO")) {
				trigger.transform.position = new Vector3(DriversSeat.transform.position.x + 0.7f, DriversSeat.transform.position.y, DriversSeat.transform.position.z);
				trigger.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
			}

			// Satsuma
			if (VehicleType.StartsWith("SATSUMA")) {
				trigger.transform.position = new Vector3(DriversSeat.transform.position.x - 0.6f, DriversSeat.transform.position.y - 0.25f, DriversSeat.transform.position.z);
				trigger.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
			}

			// Disables the cube mesh render
			//trigger.GetComponentInChildren<MeshRenderer>().enabled = false;
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

				if (playerCollider == null) {
					playerCollider = player.GetComponentInChildren<CapsuleCollider>();
					playerCollider.enabled = false;
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

			if (Input.GetKeyDown(KeyCode.Return) == true) {
				// Enter seat
				if (isSitting == false && canSit == true) {
					isSitting = true;

					player.transform.parent = this.gameObject.transform;
					motor.enabled = false;

					showGUI = false;
					iconsFsm.Fsm.GetFsmBool("GUIpassenger").Value = false;
					textFsm.Fsm.GetFsmString("GUIinteraction").Value = "";

					onEnter();
				}
				// Leave seat
				else if (isSitting == true) {
					isSitting = false;

					player.transform.parent = null;
					
					// Resets player rotation on leaving the seat
					Quaternion currentRotation = player.transform.rotation;
					currentRotation = new Quaternion(currentRotation.x, 0, currentRotation.z, currentRotation.w);

					motor.enabled = true;

					showGUI = true;

					onLeave();
				}
			}
		}
	}
}
