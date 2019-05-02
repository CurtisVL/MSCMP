namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.ConnectedPlayers)]
	class ConnectedPlayersMessage {
		int[] playerIDs;
		ulong[] steamIDs;
	}
}
