using System.Collections.Generic;
using ThunderED.Classes;

namespace ThunderED.Modules.Settings
{

    public class IncursionNotifySettings: SettingsBase<IncursionNotifySettings>
    {
        public IncursionNotifySettingsInternal Core => IncursionNotificationModule;
        public IncursionNotifySettingsInternal IncursionNotificationModule;
    }

    public class IncursionNotifySettingsInternal
    {
        public ulong DiscordChannelId;
        public List<int> Regions = new List<int>();
        public List<int> Constellations = new List<int>();
        public bool ReportIncursionStatusAfterDT;
    }
}
