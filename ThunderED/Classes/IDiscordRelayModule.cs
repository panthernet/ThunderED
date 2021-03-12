using System;

namespace ThunderED.Classes
{
    public interface IDiscordRelayModule
    {
        event Action<string, ulong> RelayMessage;
    }
}