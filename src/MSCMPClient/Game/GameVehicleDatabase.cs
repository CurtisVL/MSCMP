using MSCMP.Game.Components;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Class handling the adding and removing of vehicles in the game.
	/// </summary>
	internal class GameVehicleDatabase
		: IGameObjectCollector
	{

		/// <summary>
		/// Singleton of the vehicle manager.
		/// </summary>
		public static GameVehicleDatabase Instance;

		/// <summary>
		/// List of AI vehicles and an ID to reference them by.
		/// </summary>
		public readonly Dictionary<int, GameObject> VehiclesAi = new Dictionary<int, GameObject>();

		/// <summary>
		/// List of Player vehicles and an ID to reference them by.
		/// </summary>
		public readonly Dictionary<int, GameObject> VehiclesPlayer = new Dictionary<int, GameObject>();

		public GameVehicleDatabase()
		{
			Instance = this;
		}

		~GameVehicleDatabase()
		{
			Instance = null;
		}

		/// <summary>
		/// Handle destroy of game object.
		/// </summary>
		/// <param name="gameObject">The destroyed game object.</param>
		public void DestroyObject(GameObject gameObject)
		{
			VehiclesAi.Clear();
		}

		/// <summary>
		/// Destroy all references to collected objects.
		/// </summary>
		public void DestroyObjects()
		{
			VehiclesAi.Clear();
		}

		/// <summary>
		/// Registers given gameObject as a vehicle if it's a vehicle.
		/// </summary>
		/// <param name="gameObject">The game object to check and eventually register.</param>
		public void CollectGameObject(GameObject gameObject)
		{
			// Player vehicles
			if (gameObject.name == "Colliders" && gameObject.transform.FindChild("CarCollider") != null || gameObject.name == "Colliders" && gameObject.transform.FindChild("Coll") != null || gameObject.name == "Colliders" && gameObject.transform.FindChild("Collider") != null)
			{
				// Boat gets confused and ends up being collected here.
				if (gameObject.transform.parent.name == "GFX")
				{
					return;
				}

				if (VehiclesPlayer.ContainsValue(gameObject))
				{
					Logger.Debug($"Duplicate Player vehicle prefab '{gameObject.name}' rejected");
				}
				else
				{
					VehiclesPlayer.Add(VehiclesPlayer.Count + 1, gameObject);
					Logger.Debug($"Registered Player vehicle prefab '{gameObject.transform.parent.name}' (Player Vehicle ID: {VehiclesPlayer.Count})");

					GameObject carCollider;
					if (gameObject.transform.FindChild("CarCollider") == null)
					{
						// Truck.
						if (gameObject.transform.FindChild("Coll"))
						{
							carCollider = gameObject.transform.FindChild("Coll").gameObject;
						}
						// Tractor.
						else
						{
							carCollider = gameObject.transform.FindChild("Collider").gameObject;
						}
					}
					// Basically everything else.
					else
					{
						carCollider = gameObject.transform.FindChild("CarCollider").gameObject;
					}
					carCollider.gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.PlayerVehicle, ObjectSyncManager.AutomaticId);
				}
			}

			if (gameObject.transform.FindChild("CarColliderAI") != null)
			{
				if (VehiclesAi.ContainsValue(gameObject))
				{
					Logger.Debug($"Duplicate AI vehicle prefab '{gameObject.name}' rejected");
				}
				else
				{
					VehiclesAi.Add(VehiclesAi.Count + 1, gameObject);
					if (Network.NetWorld.DisplayObjectRegisteringDebug)
					{
						Logger.Debug($"Registered AI vehicle prefab '{gameObject.name}' (AI Vehicle ID: {VehiclesAi.Count})");
					}
					GameObject carCollider = gameObject.transform.FindChild("CarColliderAI").gameObject;
					carCollider.gameObject.AddComponent<ObjectSyncComponent>().Setup(ObjectSyncManager.ObjectTypes.AiVehicle, ObjectSyncManager.AutomaticId);
				}
			}
		}
	}
}
