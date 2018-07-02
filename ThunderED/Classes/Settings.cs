#if EDITOR
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace TED_ConfigEditor.Classes
#else
using System.Collections.Generic;

namespace ThunderED.Classes
#endif
{
    public class ThunderSettings: SettingsBase<ThunderSettings>
    {
        public ConfigSettings Config { get; set; } = new ConfigSettings();
        public WebServerModuleSettings WebServerModule { get; set; }
        public WebAuthModuleSettings WebAuthModule { get; set; }
        public ChatRelayModuleSettings ChatRelayModule { get; set; }
        public IncursionNotificationModuleSettings IncursionNotificationModule { get; set; }
        public IRCModuleSettings IrcModule { get; set; }
        public NotificationFeedSettings NotificationFeedModule { get; set; }
        public NullCampaignModuleSettings NullCampaignModule { get; set; }
        public TelegramModuleSettings TelegramModule { get; set; }
        public MailModuleSettings MailModule { get; set; }
        public TimersModuleSettings TimersModule { get; set; }
        public RadiusKillFeedModuleSettings RadiusKillFeedModule { get; set; }
        public StatsModuleSettings StatsModule { get; set; }
        public LiveKillFeedModuleSettings LiveKillFeedModule { get; set; }
        public ResourcesSettings Resources { get; set; }
        public FleetupModuleSettings FleetupModule { get; set; }
        public JabberModuleSettings JabberModule { get; set; }
    }

    public class JabberModuleSettings
    {
        public string Domain { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool Filter { get; set; }
        public bool Debug { get; set; }
        public ulong DefChan { get; set; }
#if EDITOR
        public ObservableDictionary<string, ulong> Filters { get; set; } = new ObservableDictionary<string, ulong>();
#else
        public Dictionary<string, ulong> Filters { get; set; } = new Dictionary<string, ulong>();
#endif
        public string Prepend { get; set; } = "@here";
    }


    public class FleetupModuleSettings
    {
        [Comment("FleetUp user ID")]
        [Required]
        public string UserId { get; set; }
        [Comment("FleetUp API code")]
        [Required]
        public string APICode { get; set; }
        [Comment("FleetUp application key")]
        [Required]
        public string AppKey { get; set; }
        [Required]
        public string GroupID { get; set; }
        [Required]
        public ulong Channel { get; set; }
        public bool Announce_Post { get; set; }
#if EDITOR
        public ObservableCollection<int> Announce { get; set; } = new ObservableCollection<int>();
#else
        public List<int> Announce { get; set; } = new List<int>();
#endif
    }

    public class ResourcesSettings
    {
        public string ImgCitLowPower { get; set; }
        public string ImgCitUnderAttack { get; set; }
        public string ImgCitAnchoring { get; set; }
        public string ImgCitDestroyed { get; set; }
        public string ImgCitLostShield { get; set; }
        public string ImgCitLostArmor { get; set; }
        public string ImgCitOnline { get; set; }
        public string ImgCitFuelAlert { get; set; }
        public string ImgCitServicesOffline { get; set; }
        public string ImgLowFWStand { get; set; }
        public string ImgMoonComplete { get; set; }
        public string ImgWarAssist { get; set; }
        public string ImgWarDeclared { get; set; }
        public string ImgWarInvalidate { get; set; }
        public string ImgWarSurrender { get; set; }
        public string ImgTimerAlert { get; set; }
        public string ImgMail { get; set; }
        public string ImgIncursion { get; set; }
    }

    public class LiveKillFeedModuleSettings
    {
        public bool EnableCache { get; set; }
        public long BigKill { get; set; }
        public ulong BigKillChannel { get; set; }
#if EDITOR
        public ObservableDictionary<string, KillFeedGroup> GroupsConfig { get; set; } = new ObservableDictionary<string, KillFeedGroup>();
#else
        public Dictionary<string, KillFeedGroup> GroupsConfig { get; set; } = new Dictionary<string, KillFeedGroup>();
#endif
    }

    public class KillFeedGroup
    {
        public ulong DiscordChannel { get; set; }
        public int CorpID { get; set; }
        public int AllianceID { get; set; }
        public long MinimumValue { get; set; }
        public long MinimumLossValue { get; set; }
        public long BigKillValue { get; set; }
        public ulong BigKillChannel { get; set; }
        public bool BigKillSendToGeneralToo { get; set; }
    }

    public class StatsModuleSettings
    {
        public ulong AutoDailyStatsChannel { get; set; }
        public int AutoDailyStatsDefaultCorp { get; set; }
        public int AutodailyStatsDefaultAlliance { get; set; }
    }

    public class RadiusKillFeedModuleSettings
    {
        public bool EnableCache { get; set; }
#if EDITOR
        public ObservableDictionary<string, RadiusGroup> GroupsConfig { get; set; } = new ObservableDictionary<string, RadiusGroup>();
#else
        public Dictionary<string, RadiusGroup> GroupsConfig { get; set; } = new Dictionary<string, RadiusGroup>();
#endif
    }

    public class RadiusGroup
    {
        public int Radius { get; set; }
        public ulong RadiusChannel { get; set; }
        public int RadiusSystemId { get; set; }
        public int RadiusConstellationId { get; set; }
        public int RadiusRegionId { get; set; }
        public long MinimumValue { get; set; }
    }

    public class TimersModuleSettings
    {
        [Comment("Automatically add timer event upon receiving structure reinforce notification (if notifications feed module is enabled)")]
        public bool AutoAddTimerForReinforceNotifications { get; set; } = true;
        [Comment("Web session timeout in minutes")]
        public int AuthTimeoutInMinutes { get; set; } = 10;
#if EDITOR
        [Comment("List of numeric values representing the time in minutes to send timer reminder message to discord when specified amount of minutes is left before the timer ends")]
        public ObservableCollection<int> Announces { get; set; } = new ObservableCollection<int>();
#else
        public List<int> Announces { get; set; } = new List<int>();
#endif
        [Comment("Discord channel ID for announce messages")]
        [Required]
        public ulong AnnounceChannel { get; set; }
        [Comment("Auto grant editor roles to Discord admins based on the config section")]
        public bool GrantEditRolesToDiscordAdmins { get; set; } = true;
#if EDITOR
        [Comment("List of entities which has view access to the timers page")]
        public ObservableDictionary<string, TimersAccessGroup> AccessList { get; set; } = new ObservableDictionary<string, TimersAccessGroup>();
        [Comment("List of entities which has edit access on the timers page")]
        public ObservableDictionary<string, TimersAccessGroup> EditList { get; set; } = new ObservableDictionary<string, TimersAccessGroup>();
#else
        public Dictionary<string, TimersAccessGroup> AccessList { get; set; } = new Dictionary<string, TimersAccessGroup>();
        public Dictionary<string, TimersAccessGroup> EditList { get; set; } = new Dictionary<string, TimersAccessGroup>();
#endif
    }

    public class TimersAccessGroup
    {
        [Comment("Is this an alliance or corporation ID")]
        public bool IsAlliance { get; set; }
        [Comment("Is this a character ID. Has priority over **isAlliance** value")]
        public bool IsCharacter { get; set; }
        [Comment("Is this a corporation ID")]
        public bool IsCorporation { get; set; }
        [Comment("Numeric ID value of the entity")]
        public int Id { get; set; }
    }

    public class MailModuleSettings
    {
        [Comment("Interval in minutes to check for new mail")]
        [Required]
        public int CheckIntervalInMinutes { get; set; } = 2;
#if EDITOR
        [Comment("Character groups allowed to auth as mail feeders")]
        [Required]
        public ObservableDictionary<string, MailAuthGroup> AuthGroups { get; set; } = new ObservableDictionary<string, MailAuthGroup>();
#else
        public Dictionary<string, MailAuthGroup> AuthGroups { get; set; } = new Dictionary<string, MailAuthGroup>();
#endif
    }

    public class MailAuthGroup
    {
        [Comment("EVE Online character ID")]
        [Required]
        public int Id { get; set; }

        [Comment("Include private mail to this feed")]
        public bool IncludePrivateMail { get; set; }
#if EDITOR
        [Comment("List of in game EVE mail label names which will be used to mark and fetch mails")]
        public ObservableCollection<string> Labels { get; set; } = new ObservableCollection<string>();
        [Comment("List of 'FROM' character IDs to filter incoming mail")]
        public ObservableCollection<int> Senders { get; set; } = new ObservableCollection<int>();
#else
        public List<string> Labels { get; set; } = new List<string>();
        public List<int> Senders { get; set; } = new List<int>();
#endif
        [Comment("Numeric Discord channel ID to post mail feed")]
        [Required]
        public ulong Channel { get; set; }
    }

    public class TelegramModuleSettings
    {
        public string Token { get; set; }
#if EDITOR
        public ObservableCollection<TelegramRelay> RelayChannels { get; set; } = new ObservableCollection<TelegramRelay>();
#else
        public List<TelegramRelay> RelayChannels { get; set; } = new List<TelegramRelay>();
#endif
    }

    public class TelegramRelay
    {
        public long Telegram { get; set; }
        public ulong Discord { get; set; }
#if EDITOR
        public ObservableCollection<string> DiscordFilters { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DiscordFiltersStartsWith { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> TelegramFilters { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> TelegramFiltersStartsWith { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> TelegramUsers { get; set; } = new ObservableCollection<string>();
#else
        public List<string> DiscordFilters { get; set; } = new List<string>();
        public List<string> DiscordFiltersStartsWith { get; set; } = new List<string>();
        public List<string> TelegramFilters { get; set; } = new List<string>();
        public List<string> TelegramFiltersStartsWith { get; set; } = new List<string>();
        public List<string> TelegramUsers { get; set; } = new List<string>();
        public bool RelayFromDiscordBotOnly { get; set; }
#endif
    }

    public class NullCampaignModuleSettings
    {
        public int CheckIntervalInMinutes { get; set; } = 1;
#if EDITOR
        public ObservableDictionary<string, NullCampaignGroup> Groups { get; set; } = new ObservableDictionary<string, NullCampaignGroup>(); 
#else
        public Dictionary<string, NullCampaignGroup> Groups { get; set; } = new Dictionary<string, NullCampaignGroup>(); 
#endif
    }

    public class NullCampaignGroup
    {
#if EDITOR
        public ObservableCollection<int> Regions { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<int> Constellations { get; set; } = new ObservableCollection<int>();
#else
        public List<int> Regions { get; set; } = new List<int>();
        public List<int> Constellations { get; set; } = new List<int>();
#endif
        public ulong DiscordChannelId { get; set; }
    }

    public class NotificationFeedSettings
    {
        public int CheckIntervalInMinutes { get; set; } = 2;
#if EDITOR
        public ObservableDictionary<string, NotificationSettingsGroup> Groups { get; set; } = new ObservableDictionary<string, NotificationSettingsGroup>();
#else
        public Dictionary<string, NotificationSettingsGroup> Groups { get; set; } = new Dictionary<string, NotificationSettingsGroup>();
#endif
    }

    public class NotificationSettingsGroup
    {
        public int CharacterID { get; set; }
        public ulong DefaultDiscordChannelID { get; set; }
        public int FetchLastNotifDays { get; set; }
#if EDITOR
        public ObservableDictionary<string, NotificationSettingsFilter> Filters { get; set; } = new ObservableDictionary<string, NotificationSettingsFilter>();
#else
        public Dictionary<string, NotificationSettingsFilter> Filters { get; set; } = new Dictionary<string, NotificationSettingsFilter>();
#endif
    }

    public class NotificationSettingsFilter
    {
        public ulong ChannelID { get; set; }
        public string DefaultMention { get; set; } = "@everyone";
#if EDITOR
        public ObservableCollection<string> Notifications { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<int> CharMentions { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<string> RoleMentions { get; set; } = new ObservableCollection<string>();
#else
        public List<string> Notifications { get; set; } = new List<string>();
        public List<int> CharMentions { get; set; } = new List<int>();
        public List<string> RoleMentions { get; set; } = new List<string>();
#endif
    }

    public class IRCModuleSettings
    {
        public string Server { get; set; } = "chat.freenode.net";
        public int Port { get; set; } = 6667;
        public bool UseSSL { get; set; } = false;
        public string Password { get; set; }
        public string Nickname { get; set; } = "DefaultUser-TH";
        public string Nickname2 { get; set; }
        public string Username { get; set; } = "username";
        public string Realname { get; set; } = "realname";
        public bool Invisible { get; set; } = true;
        public bool AutoReconnect { get; set; } = true;
        public int AutoReconnectDelay { get; set; } = 5000;
        public bool AutoRejoinOnKick { get; set; }
        public string QuitReason { get; set; } = "Leaving";
        public bool SuppressMOTD { get; set; } = false;
        public bool SuppressPing { get; set; } = false;
#if EDITOR
        public ObservableCollection<string> ConnectCommands { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<IRCRelayItem> RelayChannels { get; set; } = new ObservableCollection<IRCRelayItem>();
#else
        public List<string> ConnectCommands { get; set; } = new List<string>();
        public List<IRCRelayItem> RelayChannels { get; set; } = new List<IRCRelayItem>();
#endif
        public bool AutoJoinWaitIdentify { get; set; }   
    }

    public class IRCRelayItem
    {
        public string IRC { get; set; }
        public ulong Discord { get; set; }
#if EDITOR
        public ObservableCollection<string> DiscordFilters { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> DiscordFiltersStartsWith { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> IRCFilters { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> IRCFiltersStartsWith { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> IRCUsers { get; set; } = new ObservableCollection<string>();
#else
        public List<string> DiscordFilters { get; set; } = new List<string>();
        public List<string> DiscordFiltersStartsWith { get; set; } = new List<string>();
        public List<string> IRCFilters { get; set; } = new List<string>();
        public List<string> IRCFiltersStartsWith { get; set; } = new List<string>();
        public List<string> IRCUsers { get; set; } = new List<string>();
#endif
        public bool RelayFromDiscordBotOnly { get; set; }
    }

    public class IncursionNotificationModuleSettings
    {
        public ulong DiscordChannelId { get; set; }
#if EDITOR
        public ObservableCollection<int> Regions { get; set; } = new ObservableCollection<int>();
        public ObservableCollection<int> Constellations { get; set; } = new ObservableCollection<int>();
#else
        public List<int> Regions { get; set; } = new List<int>();
        public List<int> Constellations { get; set; } = new List<int>();
#endif
        public bool ReportIncursionStatusAfterDT { get; set; }
    }

    public class ChatRelayChannel
    {
        public string EVEChannelName { get; set; }
        public ulong DiscordChannelId { get; set; }
        public string Code { get; set; }
    }

    public class ChatRelayModuleSettings
    {
#if EDITOR
        public ObservableCollection<ChatRelayChannel> RelayChannels {get; set; } = new ObservableCollection<ChatRelayChannel>();
#else
        public List<ChatRelayChannel> RelayChannels {get; set; } = new List<ChatRelayChannel>();
#endif
    }

    public class ConfigSettings: IValidatable
    {
        [Comment("Discord bot token value")]
        [Required]
        public string BotDiscordToken { get; set; }
        [Comment("The name of the bot to display in Discord")]
        [Required]
        public string BotDiscordName { get; set; }
        [Comment("This string will be displayed in Discord under the bots name")]
        public string BotDiscordGame { get; set; }
        [Comment("Single symbol which will represent bot command")]
        [Required]
        public string BotDiscordCommandPrefix { get; set; } = "!";
        [Comment("Numeric ID value of your Discord group (guild)")]
        [Required]
        public ulong DiscordGuildId { get; set; }
#if EDITOR
        public ObservableCollection<string> DiscordAdminRoles { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<ulong> ComForbiddenChannels { get; set; } = new ObservableCollection<ulong>();
#else
        [Comment("At least one role name from Discord with admin privilegies")]
        [Required]
        public List<string> DiscordAdminRoles { get; set; } = new List<string>();
        [Comment("The list of numeric channel IDs in which bot commands will be ignored")]
        public List<ulong> ComForbiddenChannels { get; set; } = new List<ulong>();
#endif
        [Comment("Interface and message language. Note that console text and logs will always be in english. You can add your own translation files un Languages directory.")]
        public string Language { get; set; } = "en-US";
        [Comment("Specifies if queries and results from ESI should be received only in english or using the language settings")]
        public bool UseEnglishESIOnly { get; set; } = true;

        public bool ModuleWebServer { get; set; } = false;
        public bool ModuleAuthCheck { get; set; } = false;
        public bool ModuleAuthWeb { get; set; } = false;
        public bool ModuleCharCorp { get; set; } = false;
        public bool ModuleLiveKillFeed { get; set; } = false;
        public bool ModuleRadiusKillFeed { get; set; } = false;
        public bool ModuleReliableKillFeed { get; set; } = false;
        public bool ModulePriceCheck { get; set; } = false;
        public bool ModuleTime { get; set; } = false;
        public bool ModuleFleetup { get; set; } = false;
        public bool ModuleJabber { get; set; } = false;
        public bool ModuleMOTD { get; set; } = false;
        public bool ModuleNotificationFeed { get; set; } = false;
        public bool ModuleStats { get; set; } = false;
        public bool ModuleTimers { get; set; } = false;
        public bool ModuleMail { get; set; } = false;
        public bool ModuleIRC { get; set; } = false;
        public bool ModuleTelegram { get; set; } = false;
        public bool ModuleChatRelay { get; set; } = false;
        public bool ModuleIncursionNotify { get; set; } = false;

        [Comment("Optional ZKill RedisQ queue name to fetch kills from. Could be any text value but make sure it is not simple and is quite unique")]
        public string ZkillLiveFeedRedisqID { get; set; }
        public string TimeFormat { get; set; } = "dd.MM.yyyy HH:mm:ss";
        public string ShortTimeFormat { get; set; } = "dd.MM.yyyy HH:mm";
        [Comment("Display welcome message with authentication offer to all new users joining your Discord group hallway")]
        public bool WelcomeMessage { get; set; } = true;
        [Comment("Time interval in minutes to purge all outdated cache")]
        public int CachePurgeInterval { get; set; } = 30;
        [Comment("Memory usage limit in Mb. If app reaches that limit it will try to free some memory")]
        public int MemoryUsageLimitMb { get; set; } = 100;
        [Comment("Log all the app messages by specified severity and above (Values: Info, Error, Critical)")]
        public string LogSeverity { get; set; } = "Info";
        [Comment("FALSE by default. Set to TRUE if you want to log all raw notifications data the bot will fetch. This is needed to catch notifications which the bot could not yet process. Send me acquired data to add notifications you will like to be processed by the bot")]
        public bool LogNewNotifications { get; set; } = true;
        [Comment("Database provider. Default value is 'sqlite'")]
        public string DatabaseProvider { get; set; } = "sqlite";
        [Comment("Number of web-request retries before treating it as failed")]
        public int RequestRetries { get; set; } = 3;
        [Comment("The path to a database file. Default value is 'edb.db'")]
        public string DatabaseFile { get; set; } = "edb.db";

#if EDITOR
        public string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(BotDiscordToken):
                        return string.IsNullOrEmpty(BotDiscordToken) ? Extensions.ERR_MSG_VALUEEMPTY : null;
                }

                return null;
            }
        }

        public string Error => "!!!!";

        public string Validate()
        {
            var sb = new StringBuilder();
            GetType().GetProperties().Where(a=> a.CanRead && a.CanWrite).ToList().ForEach(property =>
            {
                var val = this[property.Name];
                if (!string.IsNullOrEmpty(val))
                {
                    sb.Append(val);
                    sb.Append("\n");
                }
            });
            return sb.ToString();
        }

#endif
    }

    public class WebServerModuleSettings
    {
        [Comment("Text IP address or domain name which the bot will use to listen for connections. \nIf the machine the bot running on have direct access to the internet then it should be equal\n to **webExternalIP** overwise it is the intrAnet address of your machine")]
        [Required]
        public string WebListenIP { get; set; }
        [Comment("Numeric port value")]
        [Required]
        public int WebListenPort { get; set; }
        [Comment("Text IP address or domain name which is used to receive connections from the internet")]
        [Required]
        public string WebExternalIP { get; set; }
        [Comment("Numeric port value")]
        [Required]
        public int WebExternalPort { get; set; }
        [Comment("Discord group invitation url")]
        public string DiscordUrl { get; set; }
        [Comment("Text client ID from the CCP application")]
        [Required]
        public string CcpAppClientId { get; set; }
        [Comment("Text client code from the CCP application")]
        [Required]
        public string CcpAppSecret { get; set; }
    }

    public class WebAuthModuleSettings
    {
        public int AuthCheckIntervalMinutes { get; set; } = 30;
        public ulong AuthReportChannel { get; set; }
        public bool EnforceCorpTickers { get; set; }
        public bool EnforceCharName { get; set; }
#if EDITOR
        public ObservableCollection<string> ExemptDiscordRoles { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<ulong> ComAuthChannels { get; set; } = new ObservableCollection<ulong>();
        public ObservableDictionary<string, WebAuthGroup> AuthGroups { get; set; } = new ObservableDictionary<string, WebAuthGroup>();
#else
        public List<string> ExemptDiscordRoles { get; set; } = new List<string>();
        public List<ulong> ComAuthChannels { get; set; } = new List<ulong>();
        public Dictionary<string, WebAuthGroup> AuthGroups { get; set; } = new Dictionary<string, WebAuthGroup>();
#endif
    }

    public class WebAuthGroup
    {
        public int CorpID { get; set; }
        public int AllianceID { get; set; }
#if EDITOR
        public ObservableCollection<string> MemberRoles { get; set; } = new ObservableCollection<string>();
#else
        public List<string> MemberRoles { get; set; } = new List<string>();
#endif
    }
}
