namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.PlayerLeave)]
	class PlayerLeaveMessage {
		int playerID;
		string reason;
	}
}
