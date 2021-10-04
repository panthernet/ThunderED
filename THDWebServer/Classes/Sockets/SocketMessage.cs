using System;

namespace THDWebServer.Classes
{
    [Serializable]
    internal class SocketMessage
    {
        public string Code;
        public SocketMessageEnum Type;
        public string Json;
    }
}