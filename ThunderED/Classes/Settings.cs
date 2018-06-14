using System.Collections.Generic;

namespace ThunderED.Classes
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
        public Dictionary<string, ulong> Filters { get; set; } = new Dictionary<string, ulong>();
        public string Prepend { get; set; } = "@here";
    }


    public class FleetupModuleSettings
    {
        public string UserId { get; set; }
        public string APICode { get; set; }
        public string AppKey { get; set; }
        public string GroupID { get; set; }
        public ulong Channel { get; set; }
        public bool Announce_Post { get; set; }
        public List<int> Announce { get; set; } = new List<int>();
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
        public Dictionary<string, KillFeedGroup> GroupsConfig { get; set; } = new Dictionary<string, KillFeedGroup>();
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
        public Dictionary<string, RadiusGroup> GroupsConfig { get; set; } = new Dictionary<string, RadiusGroup>();
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
        public bool AutoAddTimerForReinforceNotifications { get; set; } = true;
        public int AuthTimeoutInMinutes { get; set; } = 10;
        public List<int> Announces { get; set; } = new List<int>();
        public ulong AnnounceChannel { get; set; }
        public bool GrantEditRolesToDiscordAdmins { get; set; } = true;
        public Dictionary<string, TimersAccessGroup> AccessList { get; set; } = new Dictionary<string, TimersAccessGroup>();
        public Dictionary<string, TimersAccessGroup> EditList { get; set; } = new Dictionary<string, TimersAccessGroup>();
    }

    public class TimersAccessGroup
    {
        public bool IsAlliance { get; set; }
        public bool IsCharacter { get; set; }
        public bool IsCorporation { get; set; }
        public int Id { get; set; }
    }

    public class MailModuleSettings
    {
        public int CheckIntervalInMinutes { get; set; } = 2;
        public Dictionary<string, MailAuthGroup> AuthGroups { get; set; } = new Dictionary<string, MailAuthGroup>();
    }

    public class MailAuthGroup
    {
        public int Id { get; set; }
        public List<string> Labels { get; set; } = new List<string>();
        public bool IncludePrivateMail { get; set; }
        public List<int> Senders { get; set; } = new List<int>();
        public ulong Channel { get; set; }
    }

    public class TelegramModuleSettings
    {
        public string Token { get; set; }
        public List<TelegramRelay> RelayChannels { get; set; } = new List<TelegramRelay>();
    }

    public class TelegramRelay
    {
        public long Telegram { get; set; }
        public ulong Discord { get; set; }
        public List<string> DiscordFilters { get; set; } = new List<string>();
        public List<string> DiscordFiltersStartsWith { get; set; } = new List<string>();
        public List<string> TelegramFilters { get; set; } = new List<string>();
        public List<string> TelegramFiltersStartsWith { get; set; } = new List<string>();
        public List<string> TelegramUsers { get; set; } = new List<string>();
        public bool RelayFromDiscordBotOnly { get; set; }
    }

    public class NullCampaignModuleSettings
    {
        public int CheckIntervalInMinutes { get; set; } = 1;
        public Dictionary<string, NullCampaignGroup> Groups { get; set; } = new Dictionary<string, NullCampaignGroup>(); 
    }

    public class NullCampaignGroup
    {
        public List<int> Regions { get; set; } = new List<int>();
        public List<int> Constellations { get; set; } = new List<int>();
        public ulong DiscordChannelId { get; set; }
    }

    public class NotificationFeedSettings
    {
        public int CheckIntervalInMinutes { get; set; } = 2;
        public Dictionary<string, NotificationSettingsGroup> Groups { get; set; } = new Dictionary<string, NotificationSettingsGroup>();
    }

    public class NotificationSettingsGroup
    {
        public int CharacterID { get; set; }
        public ulong DefaultDiscordChannelID { get; set; }
        public Dictionary<string, NotificationSettingsFilter> Filters { get; set; } = new Dictionary<string, NotificationSettingsFilter>();
    }

    public class NotificationSettingsFilter
    {
        public List<string> Notifications { get; set; } = new List<string>();
        public ulong ChannelID { get; set; }
        public string DefaultMention { get; set; } = "@everyone";
        public List<int> CharMentions { get; set; } = new List<int>();
        public List<string> RoleMentions { get; set; } = new List<string>();
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
        public List<string> ConnectCommands { get; set; } = new List<string>();
        public List<IRCRelayItem> RelayChannels { get; set; } = new List<IRCRelayItem>();
        public bool AutoJoinWaitIdentify { get; set; }   
    }

    public class IRCRelayItem
    {
        public string IRC { get; set; }
        public ulong Discord { get; set; }
        public List<string> DiscordFilters { get; set; } = new List<string>();
        public List<string> DiscordFiltersStartsWith { get; set; } = new List<string>();
        public List<string> IRCFilters { get; set; } = new List<string>();
        public List<string> IRCFiltersStartsWith { get; set; } = new List<string>();
        public List<string> IRCUsers { get; set; } = new List<string>();
        public bool RelayFromDiscordBotOnly { get; set; }
    }

    public class IncursionNotificationModuleSettings
    {
        public ulong DiscordChannelId { get; set; }
        public List<int> Regions { get; set; } = new List<int>();
        public List<int> Constellations { get; set; } = new List<int>();
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
        public List<ChatRelayChannel> RelayChannels {get; set; } = new List<ChatRelayChannel>();
    }

    public class ConfigSettings
    {
        public string BotDiscordToken { get; set; }
        public string BotDiscordName { get; set; }
        public string BotDiscordGame { get; set; }
        public string BotDiscordCommandPrefix { get; set; } = "!";
        public ulong DiscordGuildId { get; set; }
        public List<string> DiscordAdminRoles { get; set; } = new List<string>();
        public List<ulong> ComForbiddenChannels { get; set; } = new List<ulong>();
        public string Language { get; set; } = "en-US";
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

        public string ZkillLiveFeedRedisqID { get; set; }
        public string TimeFormat { get; set; } = "dd.MM.yyyy HH:mm:ss";
        public string ShortTimeFormat { get; set; } = "dd.MM.yyyy HH:mm";
        public bool WelcomeMessage { get; set; } = true;
        public int CachePurgeInterval { get; set; } = 30;
        public int MemoryUsageLimitMb { get; set; } = 100;
        public string LogSeverity { get; set; } = "Info";
        public bool LogNewNotifications { get; set; } = true;
        public string DatabaseProvider { get; set; } = "sqlite";
        public int RequestRetries { get; set; } = 3;
        public string DatabaseFile { get; set; } = "edb.db";
    }

    public class WebServerModuleSettings
    {
        public string WebListenIP { get; set; }
        public int WebListenPort { get; set; }
        public string WebExternalIP { get; set; }
        public int WebExternalPort { get; set; }
        public string DiscordUrl { get; set; }
        public string CcpAppClientId { get; set; }
        public string CcpAppSecret { get; set; }
    }

    public class WebAuthModuleSettings
    {
        public int AuthCheckIntervalMinutes { get; set; } = 30;
        public List<string> ExemptDiscordRoles { get; set; } = new List<string>();
        public ulong AuthReportChannel { get; set; }
        public List<ulong> ComAuthChannels { get; set; } = new List<ulong>();
        public bool EnforceCorpTickers { get; set; }
        public bool EnforceCharName { get; set; }
        public Dictionary<string, WebAuthGroup> AuthGroups { get; set; } = new Dictionary<string, WebAuthGroup>();
    }

    public class WebAuthGroup
    {
        public int CorpID { get; set; }
        public int AllianceID { get; set; }
        public List<string> MemberRoles { get; set; } = new List<string>();
    }
}
