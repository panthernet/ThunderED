using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace THDWebServer.Classes
{
    [Serializable]
    internal class WebSocketClient
    {
        public WebSocket Socket { get; set; }
        public Queue<SocketMessage> Queue { get; } = new Queue<SocketMessage>();
        public TaskCompletionSource<bool> Finish { get; set; }
        public string Code { get; set; }
    }
}