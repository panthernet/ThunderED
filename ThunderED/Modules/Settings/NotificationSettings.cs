using System.Collections.Generic;
using ThunderED.Classes;

namespace ThunderED.Modules.Settings
{
    public class NotificationSettings: SettingsBase<NotificationSettings>
    {
        public NotificationSettingsInternal NotificationFeedModule;
        public NotificationSettingsInternal Core => NotificationFeedModule;

        public class NotificationSettingsInternal
        {
            public int CheckIntervalInMinutes = 2;
            public Dictionary<string, NotificationSettingsGroup> Groups = new Dictionary<string, NotificationSettingsGroup>();
        }

        public class NotificationSettingsGroup
        {
            public int CharacterID;
            public ulong DefaultDiscordChannelID;
            public Dictionary<string, NotificationSettingsFilter> Filters = new Dictionary<string, NotificationSettingsFilter>();
        }

        public class NotificationSettingsFilter
        {
            public List<string> Notifications = new List<string>();
            public ulong ChannelID;
            public List<string> Mentions = new List<string>();
        }
    }
}
