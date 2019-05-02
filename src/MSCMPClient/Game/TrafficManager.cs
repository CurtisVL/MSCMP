using UnityEngine;

namespace MSCMP.Game
{
	/// <summary>
	/// Manages traffic related triggers and provides waypoints for traffic navigation.
	/// </summary>
	internal class TrafficManager
	{
		private GameObject _traffic;
		private static GameObject _routes;

		/// <summary>
		/// Possible routes for AI vehicles to use.
		/// </summary>
		private enum Routes
		{
			BusRoute,
			DirtRoad,
			Highway,
			HomeRoad,
			RoadRace,
			Trackfield,
			Village
		}

		/// <summary>
		/// Setup the traffic manager.
		/// </summary>
		public void Setup(GameObject trafficGo)
		{
			_traffic = trafficGo;
			_routes = _traffic.transform.FindChild("Routes").gameObject;

			GameObject triggerManager = _traffic.transform.FindChild("TriggerManager").gameObject;

			PlayMakerFSM[] fsms = triggerManager.GetComponentsInChildren<PlayMakerFSM>();
			foreach (PlayMakerFSM fsm in fsms)
			{
				EventHook.SyncAllEvents(fsm);
			}
		}

		/// <summary>
		/// Get the GameObject of a waypoint.
		/// </summary>
		/// <param name="waypoint">Waypoint's name, as an int.</param>
		/// <param name="route"></param>
		/// <returns>Waypoint GameObject.</returns>
		public static GameObject GetWaypoint(float waypoint, int route)
		{
			GameObject waypointGo = _routes.transform.FindChild(((Routes)route).ToString()).FindChild("" + waypoint).gameObject;

			if (waypointGo == null)
			{
				Logger.Log($"Couldn't find waypoint, waypoint: {waypoint}, route: {route}");
			}

			return waypointGo;
		}
	}
}
