using UnityEngine;

namespace MSCMP.Game.Objects {
	/// <summary>
	/// Handles passenger seats.
	/// 
	/// This method appears to be easier than modifying current passenger seats of vehicles.
	/// Current passenger seats link into far too many other events that are not required in MP.
	/// </summary>
	/// <param name="other"></param>
	public class PassengerSeat : MonoBehaviour {
		public string VehicleType = null;
		bool canSit = false;
		bool isSitting = false;
		GameObject player = null;

		public void PrintDebug() {
			Logger.Log($"Passanger seat added, vehicle: {VehicleType}");
		}

		void OnTriggerEnter(Collider other) {
			if (other.gameObject.name == "PLAYER") {
				Logger.Log($"Triggered entered by local player!");
				canSit = true;
				player = other.gameObject;
			}
		}

		void OnTriggerExit(Collider other) {
			if (other.gameObject.name == "PLAYER") {
				Logger.Log($"Triggered exited by local player!");
				canSit = false;
				player = null;
			}
		}
		
		void Update() {
			if (canSit == true && Input.GetKeyDown(KeyCode.Return) == true) {
				isSitting = !isSitting;
				Logger.Log($"Player sat in passanger seat: {isSitting}");
				if(isSitting == true) {
					Logger.Log("Player sat in the seat!");
					player.transform.SetParent(this.transform);
				}
				else {
					Logger.Log("Player has left the seat!");
					player.transform.SetParent(null);
				}
			}
		}
	}
}
