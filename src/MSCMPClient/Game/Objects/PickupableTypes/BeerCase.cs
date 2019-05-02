using UnityEngine;
using Object = UnityEngine.Object;

namespace MSCMP.Game.Objects.PickupableTypes
{
	/// <summary>
	/// Hooks events related to beercases.
	/// </summary>
	internal class BeerCase
	{
		private readonly GameObject _beerCaseGo;
		private readonly Components.ObjectSyncComponent _osc;
		private readonly PlayMakerFSM _beerCaseFsm;

		//Get used bottles
		public int UsedBottles
		{
			get => _beerCaseFsm.FsmVariables.FindFsmInt("DestroyedBottles").Value;
			set => _beerCaseFsm.FsmVariables.FindFsmInt("DestroyedBottles").Value = value;
		}

		/// <summary>
		/// Constructor.
		/// </summary>
		public BeerCase(GameObject go)
		{
			_beerCaseGo = go;
			_osc = _beerCaseGo.GetComponent<Components.ObjectSyncComponent>();

			Client.Assert(_beerCaseFsm = Utils.GetPlaymakerScriptByName(go, "Use"), "Beer case FSM not found!");
			HookEvents();
		}

		/// <summary>
		/// Hook events.
		/// </summary>
		private void HookEvents()
		{
			EventHook.AddWithSync(_beerCaseFsm, "Remove bottle", () =>
			{
				if (_beerCaseFsm.Fsm.LastTransition.EventName == "MP_Remove bottle")
				{
					return true;
				}

				return false;
			});

			// Sync beer case bottle count with host.
			if (Network.NetManager.Instance.IsOnline && !Network.NetManager.Instance.IsHost)
			{
				_osc.RequestObjectSync();
			}
		}

		/// <summary>
		/// Removes random bottles from the beer case.
		/// </summary>
		/// <param name="count">Amount of bottles that should be remaining.</param>
		public void RemoveBottles(int count)
		{
			int i = 0;
			while (count > UsedBottles)
			{
				if (UsedBottles != 23)
				{
					GameObject bottle = _beerCaseGo.transform.GetChild(i).gameObject;
					i++;
					if (bottle != null)
					{
						Object.Destroy(bottle);
						UsedBottles++;
					}
					else
					{
						Logger.Error($"Failed to remove bottle! No bottle GameObjects found!");
					}
				}
				else
				{
					Logger.Error($"Failed to remove bottle! UsedBottles: {UsedBottles}");
				}
			}
		}
	}
}
