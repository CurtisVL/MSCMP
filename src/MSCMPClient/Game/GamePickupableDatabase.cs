using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using MSCMP.Game.Components;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Database containing prefabs of all pickupables.
	/// </summary>
	internal class GamePickupableDatabase
		: IGameObjectCollector
	{
		private static GamePickupableDatabase _instance;
		public static GamePickupableDatabase Instance => _instance;

		/// <summary>
		/// All instances of gameobject pickupables.
		/// </summary>
		private readonly Dictionary<int, GameObject> _pickupables = new Dictionary<int, GameObject>();

		/// <summary>
		/// Getter for pickupables.
		/// </summary>
		public Dictionary<int, GameObject> Pickupables => _pickupables;

		public GamePickupableDatabase()
		{
			_instance = this;

			GameCallbacks.onPlayMakerObjectCreate += (instance, prefab) =>
			{
				PrefabDesc descriptor = GetPrefabDesc(prefab);
				if (descriptor == null) return;

				PickupableMetaDataComponent metaDataComponent = instance.AddComponent<PickupableMetaDataComponent>();
				metaDataComponent.PrefabId = descriptor.Id;

				Logger.Log($"Pickupable has been spawned. ({instance.name})");
			};
		}
		~GamePickupableDatabase()
		{
			_instance = null;
		}

		/// <summary>
		/// Pickupable prefab descriptor.
		/// </summary>
		public class PrefabDesc
		{
			/// <summary>
			/// The unique id of the prefab.
			/// </summary>
			public int Id;

			/// <summary>
			/// Prefab game object.
			/// </summary>
			public GameObject GameObject;

			/// <summary>
			/// Spawn new instance of the given pickupable at given world position.
			/// </summary>
			/// <param name="position">The position where to spawn pickupable at.</param>
			/// <param name="rotation">The rotation to apply on spawned pickupable.</param>
			/// <returns>Newly spawned pickupable game object.</returns>
			public GameObject Spawn(Vector3 position, Quaternion rotation)
			{
				// HACK: Jonnez is already spawned and there can be only one of it.
				// TODO: Get rid of it, it's ugly hack. Perhaps JONNEZ should behave like pickupable.
				if (GameObject.name.StartsWith("JONNEZ ES"))
				{
					return GameObject.Find("JONNEZ ES(Clone)");
				}

				GameObject pickupable = (GameObject)Object.Instantiate(GameObject, position, rotation);
				pickupable.SetActive(true);
				pickupable.transform.SetParent(null);

				// Disable loading code on all spawned pickupables.
				PlayMakerFSM fsm = Utils.GetPlaymakerScriptByName(pickupable, "Use");
				if (fsm != null)
				{
					FsmState loadState = fsm.Fsm.GetState("Load");
					if (loadState != null)
					{
						SendEvent action = new SendEvent
						{
							eventTarget = new FsmEventTarget
							{
								excludeSelf = false,
								target = FsmEventTarget.EventTarget.Self
							},
							sendEvent = fsm.Fsm.GetEvent("FINISHED")
						};
						PlayMakerUtils.AddNewAction(loadState, action);

						Logger.Log("Installed skip load hack for prefab " + pickupable.name);
					}
					else
					{
						Logger.Log("Failed to find state on " + pickupable.name);
					}

				}

				return pickupable;
			}
		}

		/// <summary>
		/// List containing prefabs.
		/// </summary>
		private readonly List<PrefabDesc> _prefabs = new List<PrefabDesc>();

		/// <summary>
		/// Rebuild pickupables database.
		/// </summary>
		public void CollectGameObject(GameObject gameObject)
		{
			if (!IsPickupable(gameObject))
			{
				return;
			}

			int prefabId = _prefabs.Count;
			PickupableMetaDataComponent metaDataComponent = gameObject.AddComponent<PickupableMetaDataComponent>();
			metaDataComponent.PrefabId = prefabId;

			PrefabDesc desc = new PrefabDesc
			{
				GameObject = gameObject,
				Id = prefabId
			};

			// Activate game object if it's not active to make sure we can access all play maker fsm.
			bool wasActive = desc.GameObject.activeSelf;
			if (!wasActive)
			{
				desc.GameObject.SetActive(true);
			}

			// Add ObjectSyncComponent.
			if (desc.GameObject.GetComponent<ObjectSyncComponent>() == null)
			{
				desc.GameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.Pickupable, -1);
			}
			else
			{
				Object.Destroy(desc.GameObject.GetComponent<ObjectSyncComponent>());
				desc.GameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.Pickupable, -1);
			}

			// Deactivate game object back if needed.
			if (!wasActive)
			{
				desc.GameObject.SetActive(false);
			}

			_prefabs.Add(desc);

			if (Network.NetWorld.DisplayObjectRegisteringDebug)
			{
				Logger.Debug($"Registered new prefab {gameObject.name} ({gameObject.GetInstanceID()}) into pickupable database. (Prefab ID: {prefabId})");
			}
		}

		/// <summary>
		/// Handle collected objects destroy.
		/// </summary>
		public void DestroyObjects()
		{
			_prefabs.Clear();
		}

		/// <summary>
		/// Handle destroy of game object.
		/// </summary>
		/// <param name="gameObject">The destroyed game object.</param>
		public void DestroyObject(GameObject gameObject)
		{
			if (!IsPickupable(gameObject))
			{
				return;
			}

			PrefabDesc prefab = GetPrefabDesc(gameObject);
			if (prefab != null)
			{
				Logger.Debug($"Deleting prefab descriptor - {gameObject.name}.");

				// Cannot use Remove() because GetPickupablePrefab() depends on indices to stay untouched.
				_prefabs[prefab.Id] = null;
			}
		}

		/// <summary>
		/// Get pickupable prefab by it's id.
		/// </summary>
		/// <param name="prefabId">The id of the prefab to get.</param>
		/// <returns>The pickupable prefab descriptor.</returns>
		public PrefabDesc GetPickupablePrefab(int prefabId)
		{
			if (prefabId < _prefabs.Count)
			{
				return _prefabs[prefabId];
			}
			return null;
		}

		/// <summary>
		/// Get prefab descriptor by prefab game object.
		/// </summary>
		/// <param name="prefab">The prefab game object.</param>
		/// <returns>Prefab descriptor if given prefab is valid.</returns>
		public PrefabDesc GetPrefabDesc(GameObject prefab)
		{
			foreach (PrefabDesc desc in _prefabs)
			{
				if (desc != null && desc.GameObject == prefab)
				{
					return desc;
				}
			}
			return null;
		}

		/// <summary>
		/// Check if given game object is pickupable.
		/// </summary>
		/// <param name="gameObject">The game object to check.</param>
		/// <returns>true if given game object is pickupable, false otherwise</returns>
		public static bool IsPickupable(GameObject gameObject)
		{
			if (!gameObject.CompareTag("PART") && !gameObject.CompareTag("ITEM"))
			{
				return false;
			}

			if (!gameObject.GetComponent<Rigidbody>())
			{
				return false;
			}
			return true;
		}
	}
}
