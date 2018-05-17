using System;

namespace ThunderED.Modules
{
    public interface IDiscordRelayModule
    {
        event Action<string, ulong> RelayMessage;
    }
}