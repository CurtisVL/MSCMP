using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.ObjectSyncResponse)]
	class ObjectSyncResponseMessage {

		int objectID;
		bool accepted;
	}
}
