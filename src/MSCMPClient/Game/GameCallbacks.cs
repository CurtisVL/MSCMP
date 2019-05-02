using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Various callbacks called on game actions.
	/// </summary>
	internal static class GameCallbacks
	{
		/// <summary>
		/// Delegate of callback called when local player pickups object.
		/// </summary>
		/// <param name="gameObj">Picked up game object.</param>
		public delegate void OnObjectPickupEvent(GameObject gameObj);

		/// <summary>
		/// Callback called when local player pickups object.
		/// </summary>
		public static OnObjectPickupEvent OnObjectPickup;

		/// <summary>
		/// Delegate of callback called when local player pickups object.
		/// </summary>
		/// <param name="drop">Was the object dropped or throwed?</param>
		public delegate void OnObjectReleaseEvent(bool drop);

		/// <summary>
		/// Callback called when local player releases object.
		/// </summary>
		public static OnObjectReleaseEvent OnObjectRelease;

		public delegate void OnLocalPlayerCreatedEvent();

#pragma warning disable CS0649 // Temporarily disable CS0649 warning as onLocalPlayerCreated is never used.
		/// <summary>
		/// Callback called when local player spawns.
		/// </summary>
		public static OnLocalPlayerCreatedEvent OnLocalPlayerCreated;
#pragma warning restore CS0649

		public delegate void OnWorldLoadEvent();

		/// <summary>
		/// Callback called when game world gets loaded.
		/// </summary>
		public static OnWorldLoadEvent OnWorldLoad;

		public delegate void OnWorldUnloadEvent();

		/// <summary>
		/// Callback called when game world gets unloaded.
		/// </summary>
		public static OnWorldUnloadEvent OnWorldUnload;

		/// <summary>
		/// Delegate of the callback called when PlayMaker creates new object.
		/// </summary>
		/// <param name="instance">The instance of the new object.</param>
		/// <param name="prefab">The prefab used to instantiate this object.</param>
		public delegate void OnPlayMakerObjectCreateEvent(GameObject instance, GameObject prefab);

		/// <summary>
		/// Callback called when PlayMaker creates new object.
		/// </summary>
		public static OnPlayMakerObjectCreateEvent OnPlayMakerObjectCreate = null;

		/// <summary>
		/// Delegate of the callback called when PlayMaker destroys object.
		/// </summary>
		/// <param name="instance">The instance of the object that will be destroyed.</param>
		public delegate void OnPlayMakerObjectDestroyEvent(GameObject instance);

		/// <summary>
		/// Callback called when PlayMaker destroys object.
		/// </summary>
		public static OnPlayMakerObjectDestroyEvent OnPlayMakerObjectDestroy = null;

		/// <summary>
		/// Delegate of the callback called when PlayMaker activates game object.
		/// </summary>
		/// <param name="instance">The instance of the object that will be activated/deactivated.</param>
		/// <param name="activate">Is the object activating?</param>
		public delegate void OnPlayMakerObjectActivateEvent(GameObject instance, bool activate);

		/// <summary>
		/// Callback called when PlayMaker activates object.
		/// </summary>
		public static OnPlayMakerObjectActivateEvent OnPlayMakerObjectActivate = null;


		/// <summary>
		/// Delegate of the callback called when PlayMaker sets position of the game object.
		/// </summary>
		/// <param name="instance">The instance of the object this event is about.</param>
		/// <param name="position">The position to set.</param>
		/// <param name="space">The position space.</param>
		public delegate void OnPlayMakerSetPositionEvent(GameObject instance, Vector3 newPosition, Space space);

		/// <summary>
		/// Callback called when PlayMaker sets position of an object.
		/// </summary>
		public static OnPlayMakerSetPositionEvent OnPlayMakerSetPosition = null;
	}
}
