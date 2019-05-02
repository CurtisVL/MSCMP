using MSCMP.Game.Components;
using UnityEngine;

namespace MSCMP.Game.Objects
{
	/// <summary>
	/// Class representing local player object.
	/// </summary>
	internal class GamePlayer
	{
		private readonly PlayMakerFSM _pickupFsm;

		private GameObject _pickedUpGameObject;

		/// <summary>
		/// Get game object representing player.
		/// </summary>
		public GameObject Object { get; }

		/// <summary>
		/// Get object player has picked up.
		/// </summary>
		public GameObject PickedUpObject => _pickedUpGameObject;

		/// <summary>
		/// Instance.
		/// </summary>
		public static GamePlayer Instance;


		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="gameObject">The game object to pickup.</param>
		public GamePlayer(GameObject gameObject)
		{
			Object = gameObject;
			Instance = this;

			_pickupFsm = Utils.GetPlaymakerScriptByName(gameObject, "PickUp");

			if (_pickupFsm != null)
			{
				// Pickup events
				EventHook.Add(_pickupFsm, "Part picked", () =>
				{
					PickupObject();
					return false;
				});
				EventHook.Add(_pickupFsm, "Item picked", () =>
				{
					PickupObject();
					return false;
				});

				// Throw event
				EventHook.Add(_pickupFsm, "Throw part", () =>
				{
					ThrowObject();
					return false;
				});

				// Drop event
				EventHook.Add(_pickupFsm, "Drop part", () =>
				{
					DropObject();
					return false;
				});
			}

			GameObject trigger = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			trigger.transform.localScale = new Vector3(100, 100, 100);
			trigger.GetComponent<SphereCollider>().isTrigger = true;
			UnityEngine.Object.Destroy(trigger.GetComponent<MeshRenderer>());

			trigger.transform.position = gameObject.transform.position;
			trigger.transform.parent = gameObject.transform;
			ObjectSyncPlayerComponent ospc = trigger.AddComponent<ObjectSyncPlayerComponent>();
		}


		/// <summary>
		/// Handle pickup of the object.
		/// </summary>
		private void PickupObject()
		{
			_pickedUpGameObject = _pickupFsm.Fsm.GetFsmGameObject("PickedObject").Value;
			ObjectSyncComponent osc = _pickedUpGameObject.GetComponent<ObjectSyncComponent>();
			osc.TakeSyncControl();
			osc.SendConstantSync(true);

			Logger.Log("Picked up object: " + _pickedUpGameObject);
		}

		/// <summary>
		/// Handle throw of the object.
		/// </summary>
		private void ThrowObject()
		{
			Logger.Log("Threw object: " + _pickedUpGameObject);
			_pickedUpGameObject.GetComponent<ObjectSyncComponent>().SendConstantSync(false);
			_pickedUpGameObject = null;
		}

		/// <summary>
		/// Handle drop of the object.
		/// </summary>
		private void DropObject()
		{
			Logger.Log("Dropped object: " + _pickedUpGameObject);
			_pickedUpGameObject.GetComponent<ObjectSyncComponent>().SendConstantSync(false);
			_pickedUpGameObject = null;
		}

		/// <summary>
		/// Drops object when it has been stolen from the player.
		/// </summary>
		public void DropStolenObject()
		{
			_pickupFsm.SendEvent("MP_Drop part");
		}
	}
}
