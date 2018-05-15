using UnityEngine;
using System.Threading.Tasks;

namespace MSCMP.Game.Components {
	/// <summary>
	/// Attached to player, uses radius around player to determine object sync frequency.
	/// </summary>
	class ObjectSyncPlayerComponent : MonoBehaviour {
		/// <summary>
		/// Ran on script start.
		/// </summary>
		void Start() {

		}

		/// <summary>
		/// Called on object entering trigger.
		/// </summary>
		/// <param name="other"></param>
		void OnTriggerEnter(Collider other) {
			if (other.transform.name == "CarColliderAI" && other.gameObject.GetComponent<ObjectSyncComponent>() == null) { // Temporary way to get NPC vehicles.
				Logger.Log($"Found a new AI vehicle! Object name: {other.transform.parent.name}");
				other.gameObject.AddComponent<ObjectSyncComponent>().SendToRemote();
			}
			ObjectSyncComponent syncComponent = other.GetComponent<ObjectSyncComponent>();
			if (syncComponent != null) {
				Task t = new Task(syncComponent.SendEnterSync);
				t.Start();
			}
		}

		/// <summary>
		/// Called on object exiting trigger.
		/// </summary>
		/// <param name="other"></param>
		void OnTriggerExit(Collider other) {
			ObjectSyncComponent syncComponent = other.GetComponent<ObjectSyncComponent>();
			if (syncComponent != null) {
				Task t = new Task(syncComponent.SendExitSync);
				t.Start();
			}
		}

		/// <summary>
		/// Called each frame.
		/// </summary>
		void Update() {
			if (Input.GetKeyDown(KeyCode.F11)) {
				ObjectSyncManager.Instance.PrintDebug();
			}
		}
	}
}
