using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.ObjectSync)]
	class ObjectSyncMessage {

		int objectID;

		Vector3Message position;
		QuaternionMessage rotation;

		[Optional]
		int owner;
	}
}
