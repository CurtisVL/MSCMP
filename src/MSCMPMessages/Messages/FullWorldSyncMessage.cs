﻿namespace MSCMPMessages.Messages {

	class DoorsInitMessage {
		bool open;
		Vector3Message position;
	}

	class VehicleInitMessage {
		byte id;
		TransformMessage transform;
	}

	[NetMessageDesc(MessageIds.FullWorldSync)]
	class FullWorldSyncMessage {
		int							day;
		float						dayTime;
		DoorsInitMessage[]			doors;
		VehicleInitMessage[]		vehicles;
		PickupableSpawnMessage[]	pickupables;
		LightSwitchMessage[]		lights;
	}
}
