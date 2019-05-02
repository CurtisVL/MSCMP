namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.ObjectSync)]
	class ObjectSyncMessage {

		int objectID;

		[Optional]
		Vector3Message position;

		[Optional]
		QuaternionMessage rotation;

		[Optional]
		int syncType;

		[Optional]
		float[] syncedVariables;

		[Optional]
		int ownerPlayerID;
	}
}
