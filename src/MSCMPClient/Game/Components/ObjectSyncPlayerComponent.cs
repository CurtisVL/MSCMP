using UnityEngine;

namespace MSCMP.Game.Components {
	/// <summary>
	/// Attached to player, uses radius around player to determine object sync frequency.
	/// </summary>
	internal class ObjectSyncPlayerComponent : MonoBehaviour {
		/// <summary>
		/// Called on object entering trigger.
		/// </summary>
		/// <param name="other"></param>
		private void OnTriggerEnter(Collider other) {
			ObjectSyncComponent syncComponent = other.GetComponent<ObjectSyncComponent>();
			syncComponent?.SendEnterSync();
		}

		/// <summary>
		/// Called on object exiting trigger.
		/// </summary>
		/// <param name="other"></param>
		private void OnTriggerExit(Collider other) {
			ObjectSyncComponent syncComponent = other.GetComponent<ObjectSyncComponent>();
			syncComponent?.SendExitSync();
		}
	}
}
