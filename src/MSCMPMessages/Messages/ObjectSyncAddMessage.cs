using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.ObjectSyncAdd)]
	class ObjectSyncAddMessage {

		int objectID;
		string objectName;

		Vector3Message pos;
		QuaternionMessage rot;
	}
}
