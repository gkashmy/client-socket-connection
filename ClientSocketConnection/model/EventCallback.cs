using System;
using System.Collections.Generic;
using System.Text;

namespace ClientSocketConnection.model
{
    public class EventCallback
    {
        public class SocketEventCallback
        {
            public int type { get; set; }
            public int eventIndex { get; set; }
        }
    }
}
