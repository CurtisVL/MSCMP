namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.Phone)]
	class PhoneMessage {
		string topic;
		int timesToRing;
	}
}
