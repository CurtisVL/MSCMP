using System.Collections.Generic;
using UnityEngine;
using MSCMP.Game.Objects;
using MSCMP.Game;

namespace MSCMP.Game {
	/// <summary>
	/// Class managing state of the beercases in game.
	/// </summary>
	class BeerCaseManager : IGameObjectCollector {
		/// <summary>
		/// Singleton of the beercase manager.
		/// </summary>
		public static BeerCaseManager Instance = null;

		/// <summary>
		/// List of the beercases.
		/// </summary>
		public List<BeerCase> beercases = new List<BeerCase>();

		/// <summary>
		/// Amount of bottles in a full beercase.
		/// </summary>
		public int FullCaseBottles = 24;

		public delegate void OnBottleConsumed(GameObject beer);

		/// <summary>
		/// Callback called when player consumes a beer bottle
		/// </summary>
		public OnBottleConsumed onBottleConsumed;

		public BeerCaseManager() {
			Instance = this;
		}

		~BeerCaseManager() {
			Instance = null;
		}

		/// <summary>
		/// Collect all beer cases.
		/// </summary>
		public void CollectGameObject(GameObject gameObject) {
			var metaData = gameObject.GetComponent<Game.Components.PickupableMetaDataComponent>();
			if (metaData == null) {
				return;
			}

			if (metaData.PrefabDescriptor.type == GamePickupableDatabase.PrefabType.BeerCase) {
				AddBeerCase(gameObject);
			}
		}

		/// <summary>
		/// Destroy all references to collected objects.
		/// </summary>
		public void DestroyObjects() {
			beercases.Clear();
		}


		/// <summary>
		/// Adds beercase by GameObject
		/// </summary>
		/// <param name="beerGO">BeerCase GameObject.</param>
		public void AddBeerCase(GameObject beerGO) {
			bool isDuplicate = false;

			foreach (BeerCase beer in beercases) {
				GameObject beerGameObject = beer.GetGameObject;
				if (beerGameObject == beerGO) {
					Logger.Debug($"Duplicate beercase rejected: {beerGameObject.name}");
					isDuplicate = true;
				}
			}

			if (isDuplicate == false) {
				BeerCase beer = new BeerCase(beerGO);
				beercases.Add(beer);

				beer.onConsumedBeer = (beerObj) => {
					onBottleConsumed(beer.GetGameObject);
				};
			}
		}

		/// <summary>
		/// Find beercases from GameObject
		/// </summary>
		/// <param name="name">BeerCase.</param>
		/// <returns></returns>
		public BeerCase FindBeerCase(GameObject beerGO) {
			foreach (var beer in beercases) {
				if (beer.GetGameObject == beerGO) {
					Logger.Debug($"Found beercase! {beer.GetGameObject.name}");
					return beer;
				}
			}
			return null;
		}

		/// <summary>
		/// Sets bottle count of beercase.
		/// </summary>
		/// <param name="beerGO"></param>
		/// <param name="bottleCount"></param>
		public void SetBottleCount(GameObject beerGO, int bottleCount) {
			BeerCase beer = FindBeerCase(beerGO);
			if (beer != null) {
				beer.RemoveBottles(BeerCaseManager.Instance.FullCaseBottles - bottleCount);
			}
			else {
				Logger.Log("SetBottleCount: Beercase not found!");
			}
		}
	}
}
