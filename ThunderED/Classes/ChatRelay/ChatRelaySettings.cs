using System.Collections.Generic;

namespace ThunderED.Classes.ChatRelay
{
    public class ChatRelayChannel
    {
        public string EVEChannelName { get; set; }
        public ulong DiscordChannelId { get; set; }
        public string Code { get; set; }
    }

    public class ChatRelaySettingsInternal
    {
        public List<ChatRelayChannel> RelayChannels {get; set; } = new List<ChatRelayChannel>();
    }

    public class ChatRelaySettings: SettingsBase<ChatRelaySettings>
    {
        public ChatRelaySettingsInternal ChatRelayModule { get; set; }
    }
}
