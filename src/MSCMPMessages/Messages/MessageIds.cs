namespace MSCMPMessages.Messages {
	/// <summary>
	/// Network ids.
	/// </summary>
	/// <remarks>
	/// When adding new message ids add them at the bottom of the enum to keep protocol backward compatibility.
	/// </remarks>
	public enum MessageIds {
		Handshake,
		Heartbeat,
		HeartbeatResponse,
		Disconnect,
		PlayerSync,
		VehicleState,
		OpenDoors,
		FullWorldSync,
		AskForWorldState,
		VehicleEnter,
		VehicleLeave,
		PickupableSpawn,
		PickupableDestroy,
		PickupableActivate,
		PickupableSetPosition,
		WorldPeriodicalUpdate,
		LightSwitch,
		AnimSync,
		VehicleSwitch,
		ObjectSync,
		ObjectSyncResponse,
		ObjectOwnerSync,
		EventHookSync,
		RequestObjectSync,
		Phone,
		DartSync,
		ConnectedPlayers,
		PlayerJoin,
		PlayerLeave,
	}
}
