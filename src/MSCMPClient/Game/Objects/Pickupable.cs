using MSCMP.Game.Objects.PickupableTypes;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMP.Game.Objects
{
	internal class Pickupable 
		: ISyncedObject
	{
		private readonly GameObject _gameObject;
		private readonly Rigidbody _rigidbody;

		private bool _holdingObject;

		public enum SubType
		{
			Consumable,
			ShoppingBag,
			BeerCase,
		}

		private readonly SubType _objectType;

		private readonly BeerCase _beerCaseSubType;
		private int _usedBottlesLast;

		/// <summary>
		/// Constructor.
		/// </summary>
		public Pickupable(GameObject go)
		{
			_gameObject = go;
			_rigidbody = go.GetComponent<Rigidbody>();

			// Determine pickupable subtype by GameObject name.
			if (_gameObject.name == "Sausage-Potatoes(Clone)")
			{
				new PubFood(_gameObject);
				return;
			}

			// Determines pickupable subtype by FSM contents.
			PlayMakerFSM[] fsms = go.GetComponents<PlayMakerFSM>();
			foreach (PlayMakerFSM fsm in fsms)
			{
				// Consumable.
				if (fsm.Fsm.GetState("Eat") != null || fsm.Fsm.GetState("Eat 2") != null)
				{
					new Consumable(_gameObject);
					_objectType = SubType.Consumable;
					break;
				}
				// Shopping bag.

				if (fsm.Fsm.GetState("Initiate") != null && fsm.Fsm.Name == "Open")
				{
					new ShoppingBag(_gameObject);
					_objectType = SubType.ShoppingBag;
					break;
				}
				// Beer case.
				if (fsm.Fsm.GetState("Remove bottle") != null)
				{
					_beerCaseSubType = new BeerCase(_gameObject);
					_objectType = SubType.BeerCase;
					break;
				}
			}
		}

		/// <summary>
		/// Specifics for syncing this object.
		/// </summary>
		/// <returns>What should be synced for this object.</returns>
		public ObjectSyncManager.Flags Flags()
		{
			return ObjectSyncManager.Flags.Full;
		}

		/// <summary>
		/// Get object's Transform.
		/// </summary>
		/// <returns>Object's Transform.</returns>
		public Transform ObjectTransform()
		{
			return _gameObject.transform;
		}

		/// <summary>
		/// Check is periodic sync of the object is enabled.
		/// </summary>
		/// <returns>Periodic sync enabled or disabled.</returns>
		public bool PeriodicSyncEnabled()
		{
			return false;
		}

		/// <summary>
		/// Determines if the object should be synced.
		/// </summary>
		/// <returns>True if object should be synced, false if it shouldn't.</returns>
		public bool CanSync()
		{
			if (_rigidbody == null)
			{
				Client.Assert(true, "Couldn't find Rigidbody on pickupable: " + _gameObject.name);
			}
			if (_rigidbody.velocity.sqrMagnitude >= 0.01f)
			{
				return true;
			}

			return false;
		}

		/// <summary>
		/// Called when a player enters range of an object.
		/// </summary>
		/// <returns>True if the player should try to take ownership of the object.</returns>
		public bool ShouldTakeOwnership()
		{
			return true;
		}

		/// <summary>
		/// Returns variables to be sent to the remote client.
		/// </summary>
		/// <returns>Variables to be sent to the remote client.</returns>
		public float[] ReturnSyncedVariables(bool sendAllVariables)
		{
			List<float> variables = new List<float>();
			if (_holdingObject)
			{
				variables.Add(1);
			}
			else
			{
				variables.Add(0);
			}

			// Beer case.
			if (_objectType == SubType.BeerCase)
			{
				if (_usedBottlesLast != _beerCaseSubType.UsedBottles || sendAllVariables)
				{
					_usedBottlesLast = _beerCaseSubType.UsedBottles;
					variables.Add(_beerCaseSubType.UsedBottles);
				}
			}

			return variables.ToArray();
		}

		/// <summary>
		/// Handle variables sent from the remote client.
		/// </summary>
		public void HandleSyncedVariables(float[] variables)
		{
			if (_rigidbody != null)
			{
				if (variables[0] == 1)
				{
					// Object is being held.
					_rigidbody.useGravity = false;
				}
				else
				{
					// Object is not being held.
					_rigidbody.useGravity = true;
				}

				if (variables.Length > 1)
				{
					// Beer case
					if (_objectType == SubType.BeerCase)
					{
						if (variables[1] != _beerCaseSubType.UsedBottles)
						{
							_beerCaseSubType.RemoveBottles((int)variables[1]);
						}
					}
				}
			}
		}

		/// <summary>
		/// Called when owner is set to the remote client.
		/// </summary>
		public void OwnerSetToRemote()
		{

		}

		/// <summary>
		/// Called when owner is removed.
		/// </summary>
		public void OwnerRemoved()
		{
			_rigidbody.useGravity = true;
		}

		/// <summary>
		/// Called when sync control is taken by force.
		/// </summary>
		public void SyncTakenByForce()
		{
			if (_holdingObject)
			{
				Logger.Log("Dropped object because remote player has taken control of it!");
				GamePlayer.Instance.DropStolenObject();
			}
		}

		/// <summary>
		/// Called when an object is constantly syncing. (Usually when a pickupable is picked up, or when a vehicle is being driven)
		/// </summary>
		/// <param name="newValue">If object is being constantly synced.</param>
		public void ConstantSyncChanged(bool newValue)
		{
			_holdingObject = newValue;
			if (!_holdingObject)
			{
				_rigidbody.useGravity = true;
			}
		}
	}
}
