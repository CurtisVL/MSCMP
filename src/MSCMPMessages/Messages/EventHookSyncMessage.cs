﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MSCMPMessages.Messages {
	[NetMessageDesc(MessageIds.EventHookSync)]
	class EventHookSyncMessage {

		int fsmID;
		int fsmEventID;
	}
}
