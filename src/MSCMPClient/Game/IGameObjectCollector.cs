using UnityEngine;

namespace MSCMP.Game {
	interface IGameObjectCollector {

		/// <summary>
		/// Called when there is a new game object that can be collected.
		/// </summary>
		/// <param name="gameObject">The game object that can be collected.</param>
		void CollectGameObject(GameObject gameObject);

		/// <summary>
		/// Called when all collected objects are destroyed.
		/// </summary>
		void DestroyObjects();
	}
}
