using System;
using UnityEngine;
using HutongGames.PlayMaker;

namespace MSCMP.Game
{
	/// <summary>
	/// Manages sync of jobs/states related to the uncle.
	/// </summary>
	class UncleManager : IGameObjectCollector
	{
		public static UncleManager Instance;

		private GameObject uncleGameObject;
		private PlayMakerFSM uncleFSM;

		public int UncleStage {
			get {
				return uncleFSM.FsmVariables.GetFsmInt("UncleStage").Value;
			}
			set {
				uncleFSM.FsmVariables.GetFsmInt("UncleStage").Value = value;
			}
		}

		public float UncleTime {
			get {
				return uncleFSM.FsmVariables.GetFsmFloat("UncleTime").Value;
			}
			set {
				uncleFSM.FsmVariables.GetFsmFloat("UncleTime").Value = value;
			}
		}

		public bool UncleHome {
			get {
				return uncleFSM.FsmVariables.GetFsmBool("UncleHome").Value;
			}
			set {
				uncleFSM.FsmVariables.GetFsmBool("UncleHome").Value = value;
			}
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		public UncleManager() {
			Instance = this;
		}

		/// <summary>
		/// Find uncle GameObjects.
		/// </summary>
		/// <param name="obj">Uncle related GameObject.</param>
		public void CollectGameObject(GameObject obj) {
			// Main uncle GameObject.
			if (obj.name == "UNCLE" && obj.transform.parent.name == "YARD") {
				uncleGameObject = obj;
				uncleFSM = Utils.GetPlaymakerScriptByName(uncleGameObject, "States");
				// Uncle related.
				EventHook.AddWithSync(uncleFSM, "State 1");
				EventHook.AddWithSync(uncleFSM, "Uncle no license");
				EventHook.AddWithSync(uncleFSM, "Truck sold");
				EventHook.AddWithSync(uncleFSM, "Truck sold 2");
				EventHook.AddWithSync(uncleFSM, "Uncle drunk");
			}
			// Uncle front door.
			if (obj.name == "Door" && obj.transform.parent.Find("UncleDoorHandle")) {
				PlayMakerFSM doorFSM = Utils.GetPlaymakerScriptByName(obj, "Use");
				EventHook.AddWithSync(doorFSM, "Open door");
				EventHook.AddWithSync(doorFSM, "Close door");
			}
		}

		/// <summary>
		/// On object destroyed. (Not used for uncle)
		/// </summary>
		public void DestroyObject(GameObject obj) {

		}

		/// <summary>
		/// On all objects destroyed. (Not used for uncle)
		/// </summary>
		public void DestroyObjects() {

		}
	}
}
