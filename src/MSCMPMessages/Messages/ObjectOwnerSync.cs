namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.ObjectOwnerSync)]
	class ObjectOwnerSync {
		int objectID;
		int ownerPlayerID;
	}
}
