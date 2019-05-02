namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.PlayerJoin)]
	class PlayerJoinMessage {
		int playerID;
		ulong steamID;
	}
}
