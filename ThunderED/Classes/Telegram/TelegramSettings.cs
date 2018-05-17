using System.Collections.Generic;

namespace ThunderED.Classes.Telegram
{
    public class TelegramSettings: SettingsBase<TelegramSettings>
    {
        public TelegramSettingsInternal TelegramModule { get; set; }
    }

    public class TelegramSettingsInternal
    {
        public string Token { get; set; }

        public List<TelegramRelay> RelayChannels { get; set; } = new List<TelegramRelay>();
    }

    public class TelegramRelay
    {
        public long Telegram { get; set; }
        public ulong Discord { get; set; }
        public List<string> DiscordFilters { get; set; } = new List<string>();
        public List<string> TelegramFilters { get; set; } = new List<string>();
    }
}
