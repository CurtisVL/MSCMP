using UnityEngine;

namespace MSCMP.Network
{
	/// <summary>
	/// Network object using for synchronization of pickupable.
	/// </summary>
	internal class NetPickupable
	{
		/// <summary>
		/// Invalid id of the pickupable.
		/// </summary>
		public const ushort INVALID_ID = ushort.MaxValue;

		/// <summary>
		/// The network id of the pickupable.
		/// </summary>
		private readonly ushort _netId;

		/// <summary>
		/// Network id of this pickupable.
		/// </summary>
		public ushort NetId => _netId;

		/// <summary>
		/// The game object representing pickupable.
		/// </summary>
		private GameObject _gameObject;

		/// <summary>
		/// Constructor.
		/// </summary>
		/// <param name="netId">The network id of the pickupable.</param>
		/// <param name="go">The game object representing pickupable.</param>
		public NetPickupable(ushort netId, GameObject go)
		{
			_netId = netId;
			_gameObject = go;
		}
	}
}
