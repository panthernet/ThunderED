using System.Collections.Generic;
using ThunderED.Classes.IRC;

namespace ThunderED.Classes
{
    public class ThunderSettings: SettingsBase<ThunderSettings>
    {
        public ConfigSettings Config;
        public WebServerModuleSettings WebServerModule;
        public WebAuthModuleSettings WebAuthModule;
        public ChatRelayModuleSettings ChatRelayModule;
        public IncursionNotificationModuleSettings IncursionNotificationModule;
        public IRCModuleSettings IrcModule;
        public NotificationFeedSettings NotificationFeedModule;
        public NullCampaignModuleSettings NullCampaignModule;
        public TelegramModuleSettings TelegramModule;
        public MailModuleSettings MailModule;
        public TimersModuleSettings TimersModule;
        public RadiusKillFeedModuleSettings RadiusKillFeedModule;
        public StatsModuleSettings StatsModule;
        public LiveKillFeedModuleSettings LiveKillFeedModule;
        public ResourcesSettings Resources;
        public FleetupModuleSettings FleetupModule;
        public JabberModuleSettings JabberModule;
    }

    public class JabberModuleSettings
    {
        public string Domain;
        public string Username;
        public string Password;
        public bool Filter;
        public bool Debug;
        public ulong DefChan;
        public Dictionary<string, ulong> Filters = new Dictionary<string, ulong>();
        public string Prepend = "@here";
    }


    public class FleetupModuleSettings
    {
        public string UserId;
        public string APICode;
        public string AppKey;
        public string GroupID;
        public ulong Channel;
        public bool Announce_Post;
        public List<int> Announce = new List<int>();
    }

    public class ResourcesSettings
    {
        public string ImgCitLowPower;
        public string ImgCitUnderAttack;
        public string ImgCitAnchoring;
        public string ImgCitDestroyed;
        public string ImgCitLostShield;
        public string ImgCitLostArmor;
        public string ImgCitOnline;
        public string ImgCitFuelAlert;
        public string ImgCitServicesOffline;
        public string ImgLowFWStand;
        public string ImgMoonComplete;
        public string ImgWarAssist;
        public string ImgWarDeclared;
        public string ImgWarInvalidate;
        public string ImgWarSurrender;
        public string ImgTimerAlert;
        public string ImgMail;
        public string ImgIncursion;
    }

    public class LiveKillFeedModuleSettings
    {
        public bool EnableCache;
        public long BigKill;
        public ulong BigKillChannel;
        public Dictionary<string, KillFeedGroup> GroupsConfig = new Dictionary<string, KillFeedGroup>();
    }

    public class KillFeedGroup
    {
        public ulong DiscordChannel;
        public int CorpID;
        public int AllianceID;
        public long MinimumValue;
        public long MinimumLossValue;
        public long BigKillValue;
        public ulong BigKillChannel;
        public bool BigKillSendToGeneralToo;
    }

    public class StatsModuleSettings
    {
        public ulong AutoDailyStatsChannel;
        public int AutoDailyStatsDefaultCorp;
        public int AutodailyStatsDefaultAlliance;
    }

    public class RadiusKillFeedModuleSettings
    {
        public bool EnableCache;
        public Dictionary<string, RadiusGroup> GroupsConfig = new Dictionary<string, RadiusGroup>();
    }

    public class RadiusGroup
    {
        public int Radius;
        public ulong RadiusChannel;
        public int RadiusSystemId;
        public int RadiusConstellationId;
        public int RadiusRegionId;
        public long MinimumValue;
    }

    public class TimersModuleSettings
    {
        public bool AutoAddTimerForReinforceNotifications = true;
        public int AuthTimeoutInMinutes = 10;
        public List<int> Announces = new List<int>();
        public ulong AnnounceChannel;
        public bool GrantEditRolesToDiscordAdmins = true;
        public Dictionary<string, TimersAccessGroup> AccessList = new Dictionary<string, TimersAccessGroup>();
        public Dictionary<string, TimersAccessGroup> EditList = new Dictionary<string, TimersAccessGroup>();
    }

    public class TimersAccessGroup
    {
        public bool IsAlliance;
        public bool IsCharacter;
        public int Id;
    }

    public class MailModuleSettings
    {
        public int CheckIntervalInMinutes = 2;
        public Dictionary<string, MailAuthGroup> AuthGroups = new Dictionary<string, MailAuthGroup>();
    }

    public class MailAuthGroup
    {
        public int Id;
        public List<string> Labels = new List<string>();
        public bool IncludePrivateMail;
        public List<int> Senders = new List<int>();
        public ulong Channel;
    }

    public class TelegramModuleSettings
    {
        public string Token;
        public List<TelegramRelay> RelayChannels = new List<TelegramRelay>();
    }

    public class TelegramRelay
    {
        public long Telegram;
        public ulong Discord;
        public List<string> DiscordFilters = new List<string>();
        public List<string> DiscordFiltersStartsWith = new List<string>();
        public List<string> TelegramFilters = new List<string>();
        public List<string> TelegramFiltersStartsWith = new List<string>();
        public List<string> TelegramUsers = new List<string>();
        public bool RelayFromDiscordBotOnly;
    }

    public class NullCampaignModuleSettings
    {
        public int CheckIntervalInMinutes = 1;
        public Dictionary<string, NullCampaignGroup> Groups = new Dictionary<string, NullCampaignGroup>(); 
    }

    public class NullCampaignGroup
    {
        public List<int> Regions = new List<int>();
        public List<int> Constellations = new List<int>();
        public ulong DiscordChannelId;
    }

    public class NotificationFeedSettings
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
        public List<int> CharMentions = new List<int>();
        public List<string> RoleMentions = new List<string>();
    }

    public class IRCModuleSettings
    {
        public string Server { get; set; } = "chat.freenode.net";
        public int Port { get; set; } = IRC.IRC.DEFAULT_PORT;
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
        public bool AutoResponse { get; set; }
        public List<AutoResponseInfo> AutoResponseList { get; set; } = new List<AutoResponseInfo>();
        public int AutoResponseDelay { get; set; } = 10000;
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
        public ulong DiscordChannelId;
        public List<int> Regions = new List<int>();
        public List<int> Constellations = new List<int>();
        public bool ReportIncursionStatusAfterDT;
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
        public string BotDiscordToken;
        public string BotDiscordName;
        public string BotDiscordGame;
        public string BotDiscordCommandPrefix = "!";
        public ulong DiscordGuildId;
        public List<string> DiscordAdminRoles = new List<string>();
        public List<ulong> ComForbiddenChannels = new List<ulong>();
        public string Language = "en-US";
        public bool UseEnglishESIOnly = true;

        public bool ModuleWebServer = false;
        public bool ModuleAuthCheck = false;
        public bool ModuleAuthWeb = false;
        public bool ModuleCharCorp = false;
        public bool ModuleLiveKillFeed = false;
        public bool ModuleRadiusKillFeed = false;
        public bool ModuleReliableKillFeed = false;
        public bool ModulePriceCheck = false;
        public bool ModuleTime = false;
        public bool ModuleFleetup = false;
        public bool ModuleJabber = false;
        public bool ModuleMOTD = false;
        public bool ModuleNotificationFeed = false;
        public bool ModuleStats = false;
        public bool ModuleTimers = false;
        public bool ModuleMail = false;
        public bool ModuleIRC = false;
        public bool ModuleTelegram = false;
        public bool ModuleChatRelay = false;
        public bool ModuleIncursionNotify = false;

        public string ZkillLiveFeedRedisqID;
        public string TimeFormat = "dd.MM.yyyy HH:mm:ss";
        public string ShortTimeFormat = "dd.MM.yyyy HH:mm";
        public bool WelcomeMessage = true;
        public int CachePurgeInterval = 30;
        public int MemoryUsageLimitMb = 100;
        public string LogSeverity = "Info";
        public bool LogNewNotifications = true;
        public string DatabaseProvider = "sqlite";
        public int RequestRetries = 3;
        public string DatabaseFile = "edb.db";
    }

    public class WebServerModuleSettings
    {
        public string WebListenIP;
        public int WebListenPort;
        public string WebExternalIP;
        public int WebExternalPort;
        public string DiscordUrl;
        public string CcpAppClientId;
        public string CcpAppSecret;
    }

    public class WebAuthModuleSettings
    {
        public int AuthCheckIntervalMinutes = 30;
        public List<string> ExemptDiscordRoles = new List<string>();
        public ulong AuthReportChannel;
        public List<ulong> ComAuthChannels = new List<ulong>();
        public bool EnforceCorpTickers;
        public bool EnforceCharName;
        public Dictionary<string, WebAuthGroup> AuthGroups = new Dictionary<string, WebAuthGroup>();
    }

    public class WebAuthGroup
    {
        public int CorpID;
        public int AllianceID;
        public List<string> MemberRoles;
    }
}
