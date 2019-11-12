#if EDITOR
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Classes.Enums;

namespace TED_ConfigEditor.Classes
#else
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using ThunderED.Classes.Enums;

namespace ThunderED.Classes
#endif
{
    public class ThunderSettings: SettingsBase<ThunderSettings>
    {
        [ConfigEntryName("")]
        public ConfigSettings Config { get; set; } = new ConfigSettings();
        [ConfigEntryName("")]
        [StaticConfigEntry]
        public CommandsConfigSettings CommandsConfig { get; set; } = new CommandsConfigSettings();

        [ConfigEntryName("moduleWebServer")]
        public WebServerModuleSettings WebServerModule { get; set; } = new WebServerModuleSettings();
        [ConfigEntryName("moduleAuthWeb")]
        public WebAuthModuleSettings WebAuthModule { get; set; } = new WebAuthModuleSettings();
        [ConfigEntryName("moduleChatRelay")]
        public ChatRelayModuleSettings ChatRelayModule { get; set; } = new ChatRelayModuleSettings();
        [ConfigEntryName("moduleIncursionNotify")]
        public IncursionNotificationModuleSettings IncursionNotificationModule { get; set; } = new IncursionNotificationModuleSettings();
        [ConfigEntryName("moduleIRC")]
        public IRCModuleSettings IrcModule { get; set; } = new IRCModuleSettings();
        [ConfigEntryName("moduleNotificationFeed")]
        public NotificationFeedSettings NotificationFeedModule { get; set; } = new NotificationFeedSettings();
        [ConfigEntryName("moduleNullsecCampaign")]
        public NullCampaignModuleSettings NullCampaignModule { get; set; } = new NullCampaignModuleSettings();
        [ConfigEntryName("moduleTelegram")]
        public TelegramModuleSettings TelegramModule { get; set; } = new TelegramModuleSettings();
        [ConfigEntryName("moduleMail")]
        public MailModuleSettings MailModule { get; set; } = new MailModuleSettings();
        [ConfigEntryName("moduleTimers")]
        public TimersModuleSettings TimersModule { get; set; } = new TimersModuleSettings();
        [ConfigEntryName("moduleLiveKillFeed")]
        public LiveKillFeedModuleSettings LiveKillFeedModule { get; set; } = new LiveKillFeedModuleSettings();
        [ConfigEntryName("")]
        [StaticConfigEntry]
        public ResourcesSettings Resources { get; set; } = new ResourcesSettings();
        //[ConfigEntryName("moduleFleetup")]
        //public FleetupModuleSettings FleetupModule { get; set; } = new FleetupModuleSettings();
        [ConfigEntryName("moduleJabber")]
        public JabberModuleSettings JabberModule { get; set; } = new JabberModuleSettings();
        [ConfigEntryName("moduleHRM")]
        public HRMModuleSettings HRMModule { get; set; } = new HRMModuleSettings();
        [ConfigEntryName("ModuleSystemLogFeeder")]
        public SystemLogFeederSettings SystemLogFeederModule { get; set; } = new SystemLogFeederSettings();
        [ConfigEntryName("ModuleStats")]
        public StatsModuleSettings StatsModule { get; set; } = new StatsModuleSettings();

        [ConfigEntryName("Database")]
        [StaticConfigEntry]
        public Database Database { get; set; } = new Database();

        [ConfigEntryName("")]
        [StaticConfigEntry]
        public ContinousCheckModuleSettings ContinousCheckModule { get; set; } = new ContinousCheckModuleSettings();

        [ConfigEntryName("ModuleContractNotifications")]
        public ContractNotificationsModuleSettings ContractNotificationsModule { get; set; } = new ContractNotificationsModuleSettings();

        [ConfigEntryName("ModuleSovIndexTracker")]
        public SovTrackerModuleSettings SovTrackerModule { get; set; } = new SovTrackerModuleSettings();

        [ConfigEntryName("ModuleWebConfigEditor")]
        public WebConfigEditorModuleSettings WebConfigEditorModule { get; set; } = new WebConfigEditorModuleSettings();

        [ConfigEntryName("")]
        [StaticConfigEntry]
        public ZKBSettingsModuleSettings ZKBSettingsModule { get; set; } = new ZKBSettingsModuleSettings();

        [ConfigEntryName("ModuleIndustrialJobs")]
        public IndustrialJobsModuleSettings IndustrialJobsModule { get; set; } = new IndustrialJobsModuleSettings();

#if EDITOR
        public string Validate(List<string> usedModules)
        {
            var sb = new StringBuilder();

            if ((Config.ModuleNotificationFeed || Config.ModuleAuthWeb || Config.ModuleMail || Config.ModuleTimers || Config.ModuleContractNotifications || Config.ModuleHRM || Config.ModuleIndustrialJobs || Config.ModuleWebConfigEditor) && !Config.ModuleWebServer)
            {
                sb.AppendLine("General Config Settings");
                sb.AppendLine("ModuleWebServer must be enabled if you plan to use Notifications, WebAuth, Mail, Timers, ContractsFeed, HRM, IndustryFeed or WebConfigEditor modules!\n");
            }

            // var checkList = usedModules?.Select(a => a.ToString()).ToList() ?? new List<string>();
            foreach (var info in GetType().GetProperties().Where(a=> typeof(IValidatable).IsAssignableFrom(a.PropertyType)))
            {
                if(info.Name != "Config" && !usedModules.Contains(info.GetValue(this).GetAttributeValue<ConfigEntryNameAttribute>("Name"))) continue;

                var p = info.GetValue(this);
                var result = (string)info.PropertyType.GetMethod("Validate").Invoke(p, new object[] { false });
                if (!string.IsNullOrEmpty(result))
                {
                    sb.Append(result);
                    sb.Append("\n\n");
                }
            }

            return sb.ToString();
        }

        public void BeforeEditorSave()
        {
            foreach (var value in WebAuthModule.AuthGroups.Values)
            {
                value.OnEditorSave();
            }
        }
#endif
    }

    public class IndustrialJobsModuleSettings
    {
        public int CheckIntervalInMinutes { get; set; } = 5;

#if EDITOR
        public ObservableDictionary<string, IndustrialJobGroup> Groups { get; set; } = new  ObservableDictionary<string, IndustrialJobGroup>();
#else
        public Dictionary<string, IndustrialJobGroup> Groups { get; set; } = new  Dictionary<string, IndustrialJobGroup>();
#endif
    }

    public class IndustrialJobGroup
    {
        public string ButtonText { get; set; }

#if EDITOR
        public ObservableCollection<object> CharacterEntities { get; set; } = new ObservableCollection<object>();
        public ObservableCollection<ulong> DiscordChannels { get; set; } = new ObservableCollection<ulong>();
        public ObservableDictionary<string, IndustryJobFilter> Filters { get; set; } = new ObservableDictionary<string, IndustryJobFilter>();
#else
        public List<object> CharacterEntities { get; set; } = new  List<object>();
        public List<ulong> DiscordChannels { get; set; } = new  List<ulong>();
        public Dictionary<string, IndustryJobFilter> Filters { get; set; } = new Dictionary<string, IndustryJobFilter>();
#endif
    }

    public class IndustryJobFilter
    {
        public bool FeedPersonalJobs { get; set; } = true;
        public bool FeedCorporateJobs { get; set; } = true;
        public bool FeedStartingJobs { get; set; } = true;
        public bool FeedCancelledJobs { get; set; } = true;
        public bool FeedReadyJobs { get; set; } = true;
        public bool FeedDeliveredJobs { get; set; } = true;
        public bool FeedPausedJobs { get; set; } = true;
        public bool FeedRevertedJobs { get; set; } = true;

        public bool FeedResearchJobs { get; set; } = true;
        public bool FeedCopyingJobs { get; set; } = true;
        public bool FeedInventionJobs { get; set; } = true;
        public bool FeedReactionJobs { get; set; } = true;
        public bool FeedBuildJobs { get; set; } = true;

#if EDITOR
        public ObservableCollection<ulong> DiscordChannels { get; set; } = new ObservableCollection<ulong>();
#else
        public List<ulong> DiscordChannels { get; set; } = new  List<ulong>();
#endif
    }

    public class ZKBSettingsModuleSettings
    {
        [Comment("Optional ZKill RedisQ queue name to fetch kills from. Could be any text value but make sure it is not simple and is quite unique")]
        public string ZkillLiveFeedRedisqID { get; set; }
        public bool UseSocketsForZKillboard { get; set; } = true;
        public string ZKillboardWebSocketUrl { get; set; } = "wss://zkillboard.com:2096";
        [Comment("Try avoid duplicate killmails across all radius and live kill feeds")]
        public bool AvoidDupesAcrossAllFeeds { get; set; } = false;
    }

    public class WebConfigEditorModuleSettings
    {
#if EDITOR
        public ObservableDictionary<string, WCEAccessFilter> AccessList { get; set; } = new  ObservableDictionary<string, WCEAccessFilter>();
#else
        public Dictionary<string, WCEAccessFilter> AccessList { get; set; } = new  Dictionary<string, WCEAccessFilter>();
#endif
        public int SessionTimeoutInMinutes { get; set; } = 10;

    }

    public class WCEAccessFilter
    {
#if EDITOR
        public ObservableCollection<object> AllowedEntities { get; set; } = new ObservableCollection<object>();
        public ObservableCollection<string> AllowedDiscordRoles { get; set; } = new ObservableCollection<string>();
#else
        public List<object> AllowedEntities { get; set; } = new List<object>();
        public List<string> AllowedDiscordRoles { get; set; } = new List<string>();
#endif
        public bool CanEditSimplifiedAuth { get; set; } = true;
        public bool CanEditTimers { get; set; } = true;
    }

    public class CommandsConfigSettings
    {
#region Ships commands
#if EDITOR
        public ObservableCollection<string> ShipsCommandDiscordRoles { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<ulong> ShipsCommandDiscordChannels { get; set; } = new ObservableCollection<ulong>();
#else
        public List<string> ShipsCommandDiscordRoles { get; set; } = new List<string>();
        public List<ulong> ShipsCommandDiscordChannels { get; set; } = new List<ulong>();
#endif
        [Comment("Enable !ships command")]
        public bool EnableShipsCommand { get; set; } = false;
#endregion

#region Roles commands
        [Comment("Enable !addrole, !remrole, !listroles commands")]
        public bool EnableRoleManagementCommands { get; set; } = false;
#if EDITOR
        public ObservableCollection<string> RolesCommandDiscordRoles { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> RolesCommandAllowedRoles { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<ulong> RolesCommandDiscordChannels { get; set; } = new ObservableCollection<ulong>();
#else
        public List<string> RolesCommandDiscordRoles { get; set; } = new List<string>();
        public List<string> RolesCommandAllowedRoles { get; set; } = new List<string>();
        public List<ulong> RolesCommandDiscordChannels { get; set; } = new List<ulong>();
#endif
#endregion

    }

    public class SovTrackerModuleSettings: ValidatableSettings
    {
        [Required]
        [Comment("Check interval in minutes")]
        public int CheckIntervalInMinutes { get; set; } = 1;

#if EDITOR
        public ObservableDictionary<string,  SovTrackerGroup> Groups { get; set; } = new ObservableDictionary<string, SovTrackerGroup>();
#else
        public Dictionary<string, SovTrackerGroup> Groups { get; set; } = new Dictionary<string, SovTrackerGroup>();        
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(CheckIntervalInMinutes):
                        return CheckIntervalInMinutes == 0? Compose(nameof(CheckIntervalInMinutes), "CheckIntervalInMinutes must be greater than 0!") : null;
                    case nameof(Groups):
                        return !Groups.Any() ? Compose(nameof(Groups), "There must be some groups!") : null;
                }

                return null;
            }
        }
#endif
    }

    public class SovTrackerGroup
    {
#if EDITOR
        public ObservableCollection<long> Regions { get; set; } = new ObservableCollection<long>();
        public ObservableCollection<long> Constellations { get; set; } = new ObservableCollection<long>();
        public ObservableCollection<long> Systems { get; set; } = new ObservableCollection<long>();
        public ObservableCollection<long> HolderAlliances { get; set; } = new ObservableCollection<long>();
        [Comment("The list of Discord mentions to use for this notifications, like @everyone")]
        public ObservableCollection<string> DiscordMentions { get; set; } = new ObservableCollection<string>();
#else
        public List<long> Regions { get; set; } = new List<long>();
        public List<long> Constellations { get; set; } = new List<long>();
        public List<long> Systems { get; set; } = new List<long>();
        public List<long> HolderAlliances { get; set; } = new List<long>();
        public List<string> DiscordMentions { get; set; } = new List<string>();
#endif
        public double WarningThresholdValue { get; set; } = 1;
        public ulong DiscordChannelId { get; set; }
        public bool TrackADMIndexChanges { get; set; } = true;
        public bool TrackTCUHolderChanges { get; set; } = true;
        public bool TrackIHUBHolderChanges { get; set; } = true;
    }

    public class StandingGroup: ValidatableSettings
    {
#if EDITOR
        public ObservableCollection<double> Standings { get; set; } = new ObservableCollection<double>();
        public ObservableCollection<string> DiscordRoles { get; set; } = new ObservableCollection<string>();
#else
        public List<double> Standings { get; set; } = new List<double>();
        public List<string> DiscordRoles { get; set; } = new List<string>();
#endif
        public string Modifier { get; set; } = "eq";
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Standings):
                        return !Standings.Any() ? Compose(nameof(Standings), "Standings must be specified!") : null;
                    case nameof(Modifier):
                        return !string.IsNullOrEmpty(Modifier)? Compose(nameof(Modifier), "Modifier must be specified!") : null;
                }
                return null;
            }
        }
#endif
    }

    public class ContractNotificationsModuleSettings: ValidatableSettings
    {
        [Required]
        [Comment("Check interval in minutes")]
        public int CheckIntervalInMinutes { get; set; } = 1;

        [Comment("Maximum number of last contracts to check")]
        public int MaxTrackingCount { get; set; } = 150;
#if EDITOR
        public ObservableDictionary<string, ContractNotifyGroup> Groups { get; set; } = new ObservableDictionary<string, ContractNotifyGroup>();
#else
        public Dictionary<string, ContractNotifyGroup> Groups { get; set; } = new Dictionary<string, ContractNotifyGroup>();        
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(CheckIntervalInMinutes):
                        return CheckIntervalInMinutes == 0? Compose(nameof(CheckIntervalInMinutes), "CheckIntervalInMinutes must be greater than 0!") : null;
                   // case nameof(Groups):
                   //     return Groups.Values.SelectMany(a=> a.CharacterIDs).Count() != Groups.Values.SelectMany(a=> a.CharacterIDs).Distinct().Count() ? Compose(nameof(Groups), "Groups must have unique character IDs!") : null;
                }

                return null;
            }
        }
#endif
    }

    public class ContractNotifyGroup: ValidatableSettings
    {
#if EDITOR
        public ObservableCollection<long> CharacterIDs { get; set; } = new ObservableCollection<long>();
        public ObservableDictionary<string, ContractNotifyFilter> Filters { get; set; } = new ObservableDictionary<string, ContractNotifyFilter>();
#else
        public List<long> CharacterIDs { get; set; } = new List<long>();
        public Dictionary<string, ContractNotifyFilter> Filters { get; set; } = new Dictionary<string, ContractNotifyFilter>();
#endif
        public bool FeedPersonalContracts { get; set; } = true;
        public bool FeedCorporateContracts { get; set; } = true;
        public string ButtonText { get; set; } = "Default Contracts Auth";
        [Comment("Do not process contract in other filters if it has been posted within a filter")]
        public bool StopOnFirstFilterMatch { get; set; } = false;
        public string DefaultMention { get; set; }

#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(CharacterIDs):
                        return CharacterIDs.Count == 0? Compose(nameof(CharacterIDs), "CharacterIDs must be set!") : null;
                }

                return null;
            }
        }
#endif
    }

    public class ContractNotifyFilter
    {
#if EDITOR
        [Comment("Availability filter. Values: public, personal, corporation, alliance")]
        public ObservableCollection<string> Availability { get; set; } = new ObservableCollection<string>();
        [Comment("Type filter. Values: unknown, item_exchange, auction, courier, loan")]
        public ObservableCollection<string> Types { get; set; } = new ObservableCollection<string>();
        [Comment("Status filter. Values: finished_issuer, finished_contractor, finished, cancelled, rejected, failed, deleted, reversed, in_progress, outstanding")]
        public ObservableCollection<string> Statuses { get; set; } = new ObservableCollection<string>();
#else        
        public List<string> Availability { get; set; } = new List<string>();
        public List<string> Types { get; set; } = new List<string>();
        public List<string> Statuses { get; set; } = new List<string>();
#endif
        public ulong DiscordChannelId { get; set; }
        public bool FeedIssuedBy { get; set; } = true;
        public bool FeedIssuedTo { get; set; } = true;
        [Comment("Show link to open contract ingame")]
        public bool ShowIngameOpen { get; set; }
        [Comment("Display only issuer/status details for contracts")]
        public bool ShowOnlyBasicDetails { get; set; }
        [Comment("Look for Discord ID in contract description and report to this user in PM instead of dedicated channel")]
        public bool RedirectByIdInDescription { get; set; }
        [Comment("Post message to Discord channel if it has been redirected by RedirectByIdInDescription param")]
        public bool PostToChannelIfRedirected { get; set; }
    }

    public class StatsModuleSettings
    {
        public bool EnableStatsCommand { get; set; } = true;

        [Comment("Enable daily rating mode if specified. Daily rating will display all entries in a summary message.")]
        public ulong RatingModeChannelId { get; set; }
#if EDITOR
        public ObservableDictionary<string, DailyStatsGroup> DailyStatsGroups { get; set; } = new  ObservableDictionary<string, DailyStatsGroup>();
#else
        public Dictionary<string, DailyStatsGroup> DailyStatsGroups { get; set; } = new  Dictionary<string, DailyStatsGroup>();
#endif

    }

    public class DailyStatsGroup: ValidatableSettings
    {
        [Comment("Numeric discord channel ID for auto posting daily stats upon new day")]
        public ulong DailyStatsChannel { get; set; }
        [Comment("Default numeric corporation ID to display stats for. Mutually exclusive with DailyStatsAlliance")]
        public long DailyStatsCorp { get; set; }
        [Comment("Default numeric alliance ID to display stats for. Mutually exclusive with DailyStatsCorp")]
        public long DailyStatsAlliance { get; set; }
        [Comment("Include this stats in rating summary if enabled")]
        public bool IncludeInRating { get; set; } = false;
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(DailyStatsChannel):
                        return DailyStatsChannel == 0? Compose(nameof(DailyStatsChannel), "DailyStatsChannel must be specified!") : null;
                    case nameof(DailyStatsCorp):
                        return DailyStatsCorp == 0 && DailyStatsAlliance == 0? Compose(nameof(DailyStatsAlliance), "DailyStatsCorp or DailyStatsAlliance must be specified!") : null;
                }

                return null;
            }
        }
#endif
    }


    public class HRMModuleSettings: ValidatableSettings
    {
#if EDITOR
        public ObservableDictionary<string, HRMAccessFilter> AccessList { get; set; } = new  ObservableDictionary<string, HRMAccessFilter>();
        public ObservableDictionary<string, SpyFilter> SpyFilters { get; set; } = new  ObservableDictionary<string, SpyFilter>();
#else
        public Dictionary<string, HRMAccessFilter> AccessList { get; set; } = new  Dictionary<string, HRMAccessFilter>();
        public Dictionary<string, SpyFilter> SpyFilters { get; set; } = new  Dictionary<string, SpyFilter>();
#endif
        [Comment("Authentication timeout in minutes")]
        public int AuthTimeoutInMinutes { get; set; } = 10;
        [Comment("Number of entries to display in tables")]
        public int TableEntriesPerPage { get; set; } = 10;

        [Comment("Number of skill entries to display in tables")]
        public int TableSkillEntriesPerPage { get; set; } = 20;

        [Comment("Mark users as dumped when they leaving your group or manually kicked. Display dumped users in a separate list.")]
        public bool UseDumpForMembers { get; set; } = true;

        [Comment("After this period in hours dumped member will be deleted entirely. Set 0 to disable.")]
        public int DumpInvalidationInHours { get; set; } = 240;

        [Comment("Optional Discord channel ID to feed new mail from users marked as spies")]
        public ulong DefaultSpiesMailFeedChannelId { get; set; }

        [Comment("Validate ESI tokens while loading character lists. Can significantly increase loading times for huge character lists.")]
        public bool ValidateTokensWhileLoading { get; set; } = true;
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(AccessList):
                        return AccessList.Count == 0 || !AccessList.Any(a=> a.Value.RolesAccessList.Any() || a.Value.UsersAccessList.Any())? Compose(nameof(AccessList), "Either UsersAccessList or RolesAccessList must be specified for at least one access list entry!") : null;
                    case nameof(AuthTimeoutInMinutes):
                        return AuthTimeoutInMinutes <= 0 ? Compose(nameof(AuthTimeoutInMinutes), "It is security unwise to set unlimited HR session") : null;
                    case nameof(TableEntriesPerPage):
                        return TableEntriesPerPage <= 0 ? Compose(nameof(TableEntriesPerPage), "Number of entries in a table must be greater than 0") : null;
                }

                return null;
            }
        }
#endif
    }

    public class SpyFilter
    {
#if EDITOR
        public ObservableCollection<string> CorporationNames { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> AllianceNames { get; set; } = new ObservableCollection<string>();
#else
        public List<string> CorporationNames { get; set; } = new List<string>();
        public List<string> AllianceNames { get; set; } = new List<string>();
#endif
        public ulong MailFeedChannelId { get; set; }
        [Comment("Display links to ship fits, etc. in EVE format after the mail message")]
        public bool DisplayMailDetailsSummary { get; set; } = true;

        [JsonIgnore]
        public Dictionary<string, long> CorpIds = new Dictionary<string, long>();
        [JsonIgnore]
        public Dictionary<string, long> AllianceIds = new Dictionary<string, long>();
    }

    public class HRMAccessFilter
    {
#if EDITOR
        public ObservableCollection<long> UsersAccessList { get; set; } = new ObservableCollection<long>();
        public ObservableCollection<string> RolesAccessList { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<string> AuthGroupNamesFilter { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<long> AuthAllianceIdFilter { get; set; } = new ObservableCollection<long>();
        public ObservableCollection<long> AuthCorporationIdFilter { get; set; } = new ObservableCollection<long>();
#else
        public List<long> UsersAccessList { get; set; } = new List<long>();
        public List<string> RolesAccessList { get; set; } = new List<string>();
        public List<string> AuthGroupNamesFilter { get; set; } = new List<string>();
        public List<long> AuthAllianceIdFilter { get; set; } = new List<long>();
        public List<long> AuthCorporationIdFilter { get; set; } = new List<long>();
#endif
        public bool ApplyGroupFilterToAwaitingUsers { get; set; } = false;
        public bool IsAwaitingUsersVisible { get; set; } = true;
        public bool IsDumpedUsersVisible { get; set; } = true;
        public bool IsAuthedUsersVisible { get; set; } = true;
        public bool IsAltUsersVisible { get; set; } = true;
        public bool IsSpyUsersVisible { get; set; } = true;
        public bool CanSearchMail { get; set; } = true;
        public bool CanKickUsers { get; set; } = true;
        public bool CanMoveToSpies { get; set; } = true;
        public bool CanInspectAuthedUsers { get; set; } = true;
        public bool CanInspectAwaitingUsers { get; set; } = true;
        public bool CanInspectDumpedUsers { get; set; } = true;
        public bool CanInspectSpyUsers { get; set; } = true;
        public bool CanInspectAltUsers { get; set; } = true;
        public bool CanRestoreDumped { get; set; } = true;

        public bool CanAccessUser(int authState)
        {
            var state = (UserStatusEnum) authState;
            return (CanInspectAuthedUsers && state == UserStatusEnum.Authed) ||
                   (CanInspectAwaitingUsers && (state == UserStatusEnum.Awaiting || state == UserStatusEnum.Initial)) ||
                   (CanInspectDumpedUsers && state == UserStatusEnum.Dumped) || (CanInspectSpyUsers && state == UserStatusEnum.Spying);
        }
    }

    public class ContinousCheckModuleSettings: ValidatableSettings
    {
        [Comment("Enable posting about TQ status into specified channels")]
        public bool EnableTQStatusPost { get; set; } = true;

        [Comment("Discord mention string to use for message")]
        public string TQStatusPostMention { get; set; } = "@here";
#if EDITOR
        public ObservableCollection<ulong> TQStatusPostChannels { get; set; } = new ObservableCollection<ulong>();
#else
        public List<ulong> TQStatusPostChannels { get; set; } = new List<ulong>();
#endif

#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                return null;
            }
        }
#endif
    }

    public class JabberModuleSettings: ValidatableSettings
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

#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Domain):
                        return string.IsNullOrEmpty(Domain)? Compose(nameof(Domain), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Username):
                        return string.IsNullOrEmpty(Username)? Compose(nameof(Username), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Password):
                        return string.IsNullOrEmpty(Password)? Compose(nameof(Password), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(DefChan):
                        return DefChan == 0? Compose(nameof(DefChan), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

   /* public class FleetupModuleSettings: ValidatableSettings
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
        [Comment("Default Discord mention for report")]
        public string DefaultMention { get; set; } = "@everyone";

        [Comment("Discord mention for the final event time report")]
        public string FinalTimeMention { get; set; } = "@here";
#if EDITOR
        public ObservableCollection<long> Announce { get; set; } = new ObservableCollection<long>();
#else
        public List<long> Announce { get; set; } = new List<long>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(UserId):
                        return string.IsNullOrEmpty(UserId)? Compose(nameof(UserId), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(APICode):
                        return string.IsNullOrEmpty(APICode)? Compose(nameof(APICode), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(AppKey):
                        return string.IsNullOrEmpty(AppKey)? Compose(nameof(AppKey), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(GroupID):
                        return string.IsNullOrEmpty(GroupID)? Compose(nameof(GroupID), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Channel):
                        return Channel == 0? Compose(nameof(Channel), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }*/

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
        public string ImgFactionCaldari { get; set; }
        public string ImgFactionGallente { get; set; }
        public string ImgFactionAmarr { get; set; }
        public string ImgFactionMinmatar { get; set; }
        public string ImgEntosisAlert { get; set; }
        public string ImgContract { get; set; }
        public string ImgContractDelete { get; set; }
        public string ImgNoLongerWarEligible { get; set; }
        public string ImgBecameWarEligible { get; set; }
        public string ImgWarInviteAccepted{ get; set; }
        public string ImgWarInviteRejected{ get; set; }
        public string ImgWarInviteSent{ get; set; }
        public string ImgAllMaintenanceBillMsg{ get; set; }
        public string ImgBillOutOfMoneyMsg{ get; set; }
        public string ImgAllianceCapitalChanged{ get; set; }
        public string ImgBountyPlacedAlliance{ get; set; }
        public string ImgCorpKicked{ get; set; }
        public string ImgCorpNewCEOMsg{ get; set; }
        public string ImgCorpTaxChangeMsg{ get; set; }
    }

    public class LiveKillFeedModuleSettings: ValidatableSettings
    {
        //[Comment("Enable or disable caching. If you're getting many global KMs it might be better to disable it to free database from large chunks of one time data")]
        //public bool EnableCache { get; set; }
        /*[Comment("Numeric value in ISK to consider the kill to be BIG enough")]
        public long BigKill { get; set; }
        [Comment("Numeric channel ID to post all GLOBAL big kills in EVE, 0 to skip")]
        public ulong BigKillChannel { get; set; }*/

        [Comment("Do not process KM in other groups if KM has been posted within a group")]
        public bool StopOnFirstGroupMatch { get; set; }

#if EDITOR
        public ObservableDictionary<string, KillFeedGroup> Groups { get; set; } = new ObservableDictionary<string, KillFeedGroup>();
#else
        public Dictionary<string, KillFeedGroup> Groups { get; set; } = new Dictionary<string, KillFeedGroup>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Groups):
                        return Groups.Count == 0? Compose(nameof(Groups), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class KillFeedGroup: ValidatableSettings
    {
        [Comment("Post group name into notification message")]
        public bool ShowGroupName { get; set; }
        [Comment("Feed PVP kills in this group")]
        public bool FeedPvpKills { get; set; } = true;
        [Comment("Feed PVE kills in this group")]
        public bool FeedPveKills { get; set; } = true;
        [Comment("Feed AWOX kills in this group")]
        public bool FeedAwoxKills { get; set; } = true;
        [Comment("Feed not AWOX kills in this group")]
        public bool FeedNotAwoxKills { get; set; } = true;
        [Comment("Feed SOLO kills in this group")]
        public bool FeedSoloKills { get; set; } = true;
        [Comment("Feed GROUP kills in this group")]
        public bool FeedGroupKills { get; set; } = true;
        [Comment("Bot will send message containing only ZKB url and Discord will unfurl it automatically")]
        public bool FeedUrlsOnly { get; set; } = false;
        [Comment("Do not process KM in other filters if KM has been posted within a filter")]
        public bool StopOnFirstFilterMatch { get; set; } = true;
        [Comment("Optional template file name from Templates/Messages folder")]
        public string MessageTemplateFileName { get; set; }

#if EDITOR
        [Comment("List of Discord channel IDs for KM feed within a group")]
        public ObservableCollection<ulong> DiscordChannels { get; set; } = new ObservableCollection<ulong>();
        [Comment("List of filtering rules to filter KMs for a group")]
        public ObservableDictionary<string, KillMailFilter> Filters { get; set; } = new ObservableDictionary<string, KillMailFilter>();
#else
        public List<ulong> DiscordChannels = new List<ulong>();
        public Dictionary<string, KillMailFilter> Filters = new Dictionary<string, KillMailFilter>();
#endif

#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(DiscordChannels):
                        return !DiscordChannels.Any() ? Compose(nameof(DiscordChannels), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class KillMailFilter
    {
        [Comment("If set to True - will display KM which fit filter criteria, otherwise will exclude KM which fit filter criteria")]
        public bool Inclusive { get; set; } = true;
        [Comment("If set to True in Inclusive mode - all filter criteria must match to feed KM, otherwise any match will do")]
        public bool AllMustMatch { get; set; } = true;
        public double MinimumKillValue { get; set; }
        public double MinimumLossValue { get; set; }
        public double MaximumKillValue { get; set; }
        public double MaximumLossValue { get; set; }

        [Comment("Numeric value of number of jumps around specified system. Leave 0 to disable radius feed. Has no effect on region or constellation.")]
        public int Radius { get; set; }
#if EDITOR
        [Comment("List of Discord channel IDs for KM feed within a filter")]
        public ObservableCollection<ulong> DiscordChannels { get; set; } = new ObservableCollection<ulong>();

        public ObservableCollection<object> ShipEntities { get; set; } = new ObservableCollection<object>();
        public ObservableCollection<object> VictimEntities { get; set; } = new ObservableCollection<object>();
        public ObservableCollection<object> AttackerEntities { get; set; } = new ObservableCollection<object>();
        public ObservableCollection<object> LocationEntities { get; set; } = new ObservableCollection<object>();
#else
        public List<object> ShipEntities = new List<object>();
        public List<object> VictimEntities = new List<object>();
        public List<object> AttackerEntities = new List<object>();
        public List<ulong> DiscordChannels = new List<ulong>();
        public List<object> LocationEntities = new List<object> ();
#endif
    }

    public class TimersModuleSettings: ValidatableSettings
    {
        [Comment("Automatically add timer event upon receiving structure reinforce notification (if notifications feed module is enabled)")]
        public bool AutoAddTimerForReinforceNotifications { get; set; } = true;
        [Comment("Web session timeout in minutes")]
        public int AuthTimeoutInMinutes { get; set; } = 10;
        [Comment("Link to a tiny url representation which is created manually and overrides standard URL for !turl command")]
        public string TinyUrl { get; set; }

        [Comment("Time format for new timers input CAPS SENSITIVE. Default: DD.MM.YYYY HH:mm")]
        public string TimeInputFormat { get; set; } = "DD.MM.YYYY HH:mm";
        [Comment("Optional Discord defult mention for timer report")]
        public string DefaultMention { get; set; }

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
        [Required]
        public ObservableDictionary<string, TimersAccessGroup> AccessList { get; set; } = new ObservableDictionary<string, TimersAccessGroup>();
        [Comment("List of entities which has edit access on the timers page")]
        [Required]
        public ObservableDictionary<string, TimersAccessGroup> EditList { get; set; } = new ObservableDictionary<string, TimersAccessGroup>();
#else
        public Dictionary<string, TimersAccessGroup> AccessList { get; set; } = new Dictionary<string, TimersAccessGroup>();
        public Dictionary<string, TimersAccessGroup> EditList { get; set; } = new Dictionary<string, TimersAccessGroup>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(AnnounceChannel):
                        return  AnnounceChannel == 0 ? Compose(nameof(AnnounceChannel), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(AccessList):
                        return AccessList.Count == 0 ? Compose(nameof(AccessList), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(EditList):
                        return EditList.Count == 0 ? Compose(nameof(EditList), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class TimersAccessGroup: ValidatableSettings
    {
#if EDITOR
        [Comment("Mixed list of character, corporation, alliance IDs or names for filtering")]
        public ObservableCollection<object> FilterEntities { get; set; } = new ObservableCollection<object>();
        public ObservableCollection<string> FilterDiscordRoles { get; set; } = new ObservableCollection<string>();
#else
        public List<object> FilterEntities { get; set; } = new List<object>();
        public List<string> FilterDiscordRoles { get; set; } = new List<string>();
#endif


#if EDITOR
        public override string this[string columnName]
        {
            get
            {

                return null;
            }
        }
#endif
    }

    public class MailModuleSettings: ValidatableSettings
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
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(CheckIntervalInMinutes):
                        return CheckIntervalInMinutes < 1 ? Compose(nameof(CheckIntervalInMinutes), "Value must be greater than 0 or the bot will blow up!") : null;
                    case nameof(AuthGroups):
                        return AuthGroups.Count == 0 ? Compose(nameof(AuthGroups), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class MailAuthGroup: ValidatableSettings
    {
        [Comment("Include private mail to this feed")]
        public bool IncludePrivateMail { get; set; }
#if EDITOR
        [Comment("EVE Online character ID")]
        [Required]
        public ObservableCollection<long> Id { get; set; } = new ObservableCollection<long>();
        public ObservableDictionary<string, MailAuthFilter> Filters { get; set; } = new ObservableDictionary<string, MailAuthFilter>();
#else
        public List<long> Id { get; set; } = new List<long>();
        public Dictionary<string, MailAuthFilter> Filters { get; set; } = new Dictionary<string, MailAuthFilter>();
#endif
        [Comment("Numeric Discord channel ID to post mail feed")]
        [Required]
        public ulong DefaultChannel { get; set; }

        [Comment("Optional Discord default mention for mail report")]
        public string DefaultMention { get; set; }


#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Id):
                        return !Id.Any() ? Compose(nameof(Id), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(DefaultChannel):
                        return DefaultChannel == 0 ? Compose(nameof(DefaultChannel), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class MailAuthFilter: ValidatableSettings
    {
#if EDITOR
        [Comment("List of in game EVE mail label names which will be used to mark and fetch mails")]
        public ObservableCollection<string> FilterLabels { get; set; } = new ObservableCollection<string>();
        [Comment("List of 'FROM' character IDs to filter incoming mail")]
        public ObservableCollection<long> FilterSenders { get; set; } = new ObservableCollection<long>();
        [Comment("List of EVE MailList names to filter incoming mail")]
        public ObservableCollection<string> FilterMailList { get; set; } = new ObservableCollection<string>();
#else
        public List<string> FilterLabels { get; set; } = new List<string>();
        public List<long> FilterSenders { get; set; } = new List<long>();
        public List<string> FilterMailList { get; set; } = new List<string>();
#endif
        [Comment("Optional numeric Discord channel ID to post filtered mail feed")]
        public ulong FeedChannel { get; set; }
        [Comment("Display links to ship fits, etc. in EVE format after the mail message")]
        public bool DisplayDetailsSummary { get; set; } = true;

#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(FilterLabels):
                        return FilterLabels.Count == 0 && FilterSenders.Count == 0 && FilterMailList.Count == 0 ? Compose(nameof(FilterSenders), "Labels, MailLists or Senders must be specified!") : null;
                }

                return null;
            }
        }
#endif
    }

    public class TelegramModuleSettings: ValidatableSettings
    {
        [Comment("Telegram bot token string obtained upon Telegram bot creation")]
        [Required]
        public string Token { get; set; }
        [Comment("Optional proxy IP for Telegram bot")]
        public string ProxyAddress { get; set; }
        [Comment("Optional proxy Port for Telegram bot")]
        public int ProxyPort { get; set; }
        [Comment("Optional proxy Username for Telegram bot")]
        public string ProxyUsername { get; set; }
        [Comment("Optional proxy Password  for Telegram bot")]
        public string ProxyPassword { get; set; }

#if EDITOR
        [Required]
        public ObservableCollection<TelegramRelay> RelayChannels { get; set; } = new ObservableCollection<TelegramRelay>();
#else
        public List<TelegramRelay> RelayChannels { get; set; } = new List<TelegramRelay>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Token):
                        return string.IsNullOrEmpty(Token)? Compose(nameof(Token), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class TelegramRelay: ValidatableSettings
    {
        [Comment("Telegram channel numeric ID")]
        [Required]
        public long Telegram { get; set; }
        [Comment("Discord channel numeric ID")]
        [Required]
        public ulong Discord { get; set; }
        [Comment("Relay only ThunderED bot messages from specified Discord channel")]
        public bool RelayFromDiscordBotOnly { get; set; }
#if EDITOR
        [Comment("Discord messages that contain these strings will be filtered from relay")]
        public ObservableCollection<string> DiscordFilters { get; set; } = new ObservableCollection<string>();
        [Comment("Discord messages that start with these strings will be filtered from relay")]
        public ObservableCollection<string> DiscordFiltersStartsWith { get; set; } = new ObservableCollection<string>();
        [Comment("Telegram messages that contain these strings will be filtered from relay")]
        public ObservableCollection<string> TelegramFilters { get; set; } = new ObservableCollection<string>();
        [Comment("Telegram messages that start with these strings will be filtered from relay")]
        public ObservableCollection<string> TelegramFiltersStartsWith { get; set; } = new ObservableCollection<string>();
        [Comment("Relay messages only from specified Telegram users. \nFirst check for Telegram @username then by First Name + Second Name")]
        public ObservableCollection<string> TelegramUsers { get; set; } = new ObservableCollection<string>();
        //[Comment("Relay messages only from specified Telegram nicknames. \nFirst check for Telegram @username then by First Name + Second Name")]
       // public ObservableCollection<string> TelegramNicks { get; set; } = new ObservableCollection<string>();
#else
        public List<string> DiscordFilters { get; set; } = new List<string>();
        public List<string> DiscordFiltersStartsWith { get; set; } = new List<string>();
        public List<string> TelegramFilters { get; set; } = new List<string>();
        public List<string> TelegramFiltersStartsWith { get; set; } = new List<string>();
        public List<string> TelegramUsers { get; set; } = new List<string>();
       // public List<string> TelegramNicks { get; set; } = new List<string>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Telegram):
                        return Telegram == 0 ? Compose(nameof(Telegram), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Discord):
                        return Discord == 0 ? Compose(nameof(Discord), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class NullCampaignModuleSettings: ValidatableSettings
    {
        public int CheckIntervalInMinutes { get; set; } = 1;
#if EDITOR
        public ObservableDictionary<string, NullCampaignGroup> Groups { get; set; } = new ObservableDictionary<string, NullCampaignGroup>(); 
#else
        public Dictionary<string, NullCampaignGroup> Groups { get; set; } = new Dictionary<string, NullCampaignGroup>(); 
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(CheckIntervalInMinutes):
                        return CheckIntervalInMinutes < 1 ? Compose(nameof(CheckIntervalInMinutes), "Value must be greater than 0 or the bot will blow up!") : null;
                    case nameof(Groups):
                        return Groups.Count == 0 ? Compose(nameof(Groups), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class NullCampaignGroup: ValidatableSettings
    {
#if EDITOR
        public ObservableCollection<long> Regions { get; set; } = new ObservableCollection<long>();
        public ObservableCollection<long> Constellations { get; set; } = new ObservableCollection<long>();
        [Comment("List of time marks in minutes before the event starts to send notifications. E.g. 15, 30 - will send notifications when 15 and 30 minutes left for event start.")]
        public ObservableCollection<int> Announces { get; set; } = new ObservableCollection<int>();
        [Comment("The list of Discord mentions to use for this notifications, default is @everyone")]
        public ObservableCollection<string> Mentions { get; set; } = new ObservableCollection<string>();
#else
        public List<long> Regions { get; set; } = new List<long>();
        public List<long> Constellations { get; set; } = new List<long>();
        public List<int> Announces { get; set; } = new List<int>();
        public List<string> Mentions { get; set; } = new List<string>();
#endif
        [Comment("Default mention to use for module notification messages")]
        public string DefaultMention { get; set; } = "@everyone";
        [Comment("Send notification message when new campaign has been discovered")]
        public bool ReportNewCampaign { get; set; } = true;
        [Comment("Discord numeric channel ID")]
        public ulong DiscordChannelId { get; set; }

#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(DiscordChannelId):
                        return DiscordChannelId == 0 ? Compose(nameof(DiscordChannelId), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Regions):
                        return Regions.Count == 0 && Constellations.Count == 0? Compose(nameof(Regions), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class NotificationFeedSettings: ValidatableSettings
    {
        [Comment("Time interval in minutes to run new notifications check. \nDue to natural delay in notifications on CCP side it is not wise to set it lower than 2 minutes")]
        public int CheckIntervalInMinutes { get; set; } = 2;
#if EDITOR
        [Comment("The list of character keys which will be authorized to share their notifications")]
        [Required]
        public ObservableDictionary<string, NotificationSettingsGroup> Groups { get; set; } = new ObservableDictionary<string, NotificationSettingsGroup>();
#else
        public Dictionary<string, NotificationSettingsGroup> Groups { get; set; } = new Dictionary<string, NotificationSettingsGroup>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(CheckIntervalInMinutes):
                        return CheckIntervalInMinutes < 1 ? Compose(nameof(CheckIntervalInMinutes), "Value must be greater than 0 or the bot will blow up!") : null;
                    case nameof(Groups):
                        return Groups.Count == 0 ? Compose(nameof(Groups), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class NotificationSettingsGroup: ValidatableSettings
    {
        [Comment("Numeric default Discord channel ID. All notification filters will use this channel to send messages by default")]
        [Required]
        public ulong DefaultDiscordChannelID { get; set; }
        [Comment("Numeric number of days to fetch old notifications for newly registered feeder. \n0 by default meaning no old notifications will be feeded")]
        public int FetchLastNotifDays { get; set; }

#if EDITOR
        [Comment("The list of filters to sort incoming notifications")]
        [Required]
        public ObservableDictionary<string, NotificationSettingsFilter> Filters { get; set; } = new ObservableDictionary<string, NotificationSettingsFilter>();
        [Comment("Numeric EVE character ID")]
        [Required]
        public ObservableCollection<long> CharacterID { get; set; } = new ObservableCollection<long>();

#else
        public Dictionary<string, NotificationSettingsFilter> Filters { get; set; } = new Dictionary<string, NotificationSettingsFilter>();
        public List<long> CharacterID { get; set; } = new List<long>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Filters):
                        return Filters.Count == 0 ? Compose(nameof(Filters), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(CharacterID):
                        return !CharacterID.Any() || CharacterID.All(a=> a == 0) ? Compose(nameof(CharacterID), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(DefaultDiscordChannelID):
                        return DefaultDiscordChannelID == 0 ? Compose(nameof(DefaultDiscordChannelID), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class NotificationSettingsFilter: ValidatableSettings
    {
        [Comment("Numeric Discord channel ID to redirect messages. Leave 0 to use group default channel")]
        public ulong ChannelID { get; set; }
        [Comment("Default Discord mention command to use for this group")]
        public string DefaultMention { get; set; } = "@everyone";
#if EDITOR
        [Comment("List of text notification types this filter has access to")]
        [Required]
        public ObservableCollection<string> Notifications { get; set; } = new ObservableCollection<string>();
        [Comment("List of numeric EVE CHARACTER IDs to mention them in the message. Characters must be authed on the server for this to work \nthus allowing to get their Discord IDs. Leave empty to use **@everyone** mention")]
        public ObservableCollection<long> CharMentions { get; set; } = new ObservableCollection<long>();
        [Comment("List of Discord role names to mention. Role must be configured in Discord to be mentionable")]
        public ObservableCollection<string> RoleMentions { get; set; } = new ObservableCollection<string>();
#else
        public List<string> Notifications { get; set; } = new List<string>();
        public List<long> CharMentions { get; set; } = new List<long>();
        public List<string> RoleMentions { get; set; } = new List<string>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Notifications):
                        return Notifications.Count == 0 ? Compose(nameof(Notifications), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class IRCModuleSettings: ValidatableSettings
    {
        [Comment("IRC server address")]
        [Required]
        public string Server { get; set; } = "chat.freenode.net";
        [Comment("IRC server port")]
        [Required]
        public int Port { get; set; } = 6667;
        public bool UseSSL { get; set; } = false;
        public string Password { get; set; }
        [Required]
        public string Nickname { get; set; } = "DefaultUser-TH";
        [Comment("Secondary IRC nickname in case the first one is in use")]
        public string Nickname2 { get; set; }
        [Required]
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
        [Comment("The list of IRC commands to perform upon successful chat login")]
        public ObservableCollection<string> ConnectCommands { get; set; } = new ObservableCollection<string>();
        [Comment("Groups of settings for message relay")]
        public ObservableCollection<IRCRelayItem> RelayChannels { get; set; } = new ObservableCollection<IRCRelayItem>();
#else
        public List<string> ConnectCommands { get; set; } = new List<string>();
        public List<IRCRelayItem> RelayChannels { get; set; } = new List<IRCRelayItem>();
#endif
        public bool AutoJoinWaitIdentify { get; set; }   
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(RelayChannels):
                        return RelayChannels.Count == 0 ? Compose(nameof(RelayChannels), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Server):
                        return string.IsNullOrEmpty(Server) ? Compose(nameof(Server), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Port):
                        return Port == 0 ? Compose(nameof(Port), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Nickname):
                        return string.IsNullOrEmpty(Nickname) ? Compose(nameof(Nickname), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Username):
                        return string.IsNullOrEmpty(Username) ? Compose(nameof(Username), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class IRCRelayItem: ValidatableSettings
    {
        [Comment("IRC channel name string prefixed with #")]
        [Required]
        public string IRC { get; set; }
        [Comment("Discord numeric channel ID")]
        [Required]
        public ulong Discord { get; set; }
        [Comment("Relay only ThunderED bot messages from specified Discord channel")]
        public bool RelayFromDiscordBotOnly { get; set; }
#if EDITOR
        [Comment("Discord messages that contain these strings will be filtered from relay")]
        public ObservableCollection<string> DiscordFilters { get; set; } = new ObservableCollection<string>();
        [Comment("Discord messages that start with these strings will be filtered from relay")]
        public ObservableCollection<string> DiscordFiltersStartsWith { get; set; } = new ObservableCollection<string>();
        [Comment("IRC messages that contain these strings will be filtered from relay")]
        public ObservableCollection<string> IRCFilters { get; set; } = new ObservableCollection<string>();
        [Comment("IRC messages that start with these strings will be filtered from relay")]
        public ObservableCollection<string> IRCFiltersStartsWith { get; set; } = new ObservableCollection<string>();
        [Comment("Relay messages only from specified IRC usernames")]
        public ObservableCollection<string> IRCUsers { get; set; } = new ObservableCollection<string>();
#else
        public List<string> DiscordFilters { get; set; } = new List<string>();
        public List<string> DiscordFiltersStartsWith { get; set; } = new List<string>();
        public List<string> IRCFilters { get; set; } = new List<string>();
        public List<string> IRCFiltersStartsWith { get; set; } = new List<string>();
        public List<string> IRCUsers { get; set; } = new List<string>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Discord):
                        return Discord == 0 ? Compose(nameof(Discord), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(IRC):
                        return string.IsNullOrEmpty(IRC) ? Compose(nameof(IRC), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class IncursionNotificationModuleSettings: ValidatableSettings
    {
        [Comment("Numeric Discord channel ID")]
        [Required]
        public ulong DiscordChannelId { get; set; }
        [Comment("Set to **True** if you want bot to post status update about existing incursions. \nSet to **False** to report only new incursions")]
        public bool ReportIncursionStatusAfterDT { get; set; }
        [Comment("Optional default mention for Incursions report. Default: @everyone")]
        public string DefaultMention { get; set; } = "@everyone";
#if EDITOR
        [Comment("List of numeric region IDs to filter incursions")]
        public ObservableCollection<long> Regions { get; set; } = new ObservableCollection<long>();
        [Comment("List of numeric constellation IDs to filter incursions")]
        public ObservableCollection<long> Constellations { get; set; } = new ObservableCollection<long>();
#else
        public List<long> Regions { get; set; } = new List<long>();
        public List<long> Constellations { get; set; } = new List<long>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(DiscordChannelId):
                        return DiscordChannelId == 0 ? Compose(nameof(DiscordChannelId), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Regions):
                        return Regions.Count == 0 && Constellations.Count == 0 ? Compose(nameof(Regions), "Regions or Constellations must be filled!") : null;
                }

                return null;
            }
        }
#endif
    }

    public class ChatRelayChannel: ValidatableSettings
    {
        [Comment("Name of the EVE chat channel")]
        [Required]
        public string EVEChannelName { get; set; }
        [Comment("Numeric Discord channel ID")]
        [Required]
        public ulong DiscordChannelId { get; set; }
        [Comment("Unique string code to identify this relay block. \nYou have to specify this code in TED_ChatRelay app settings too")]
        [Required]
        public string Code { get; set; }
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(DiscordChannelId):
                        return DiscordChannelId == 0 ? Compose(nameof(DiscordChannelId), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(EVEChannelName):
                        return string.IsNullOrEmpty(EVEChannelName) ? Compose(nameof(EVEChannelName), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(Code):
                        return string.IsNullOrEmpty(Code) ? Compose(nameof(Code), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class ChatRelayModuleSettings: ValidatableSettings
    {
#if EDITOR
        public ObservableCollection<ChatRelayChannel> RelayChannels {get; set; } = new ObservableCollection<ChatRelayChannel>();
#else
        public List<ChatRelayChannel> RelayChannels {get; set; } = new List<ChatRelayChannel>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(RelayChannels):
                        return RelayChannels.Count == 0 ? Compose(nameof(RelayChannels), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class Database
    {
        [Comment("Database provider. Values: sqlite, mysql. Default value is 'sqlite'")]
        public string DatabaseProvider { get; set; } = "sqlite";
        [Comment("The path to a database file for file-based providers like SQlite. Default value is 'edb.db'")]
        [Required]
        public string DatabaseFile { get; set; } = "edb.db";

        public string ServerAddress { get; set; }
        public int ServerPort { get; set; }
        public string DatabaseName { get; set; }
        public string UserId { get; set; }
        public string Password { get; set; }
        public string CustomConnectionString { get; set; }
        [Comment("How often the SQLite backups should be created. Has no effect if value set to 0 or DatabaseProvider is not sqlite. Default value is 8 hours.")]
        public int SqliteBackupFrequencyInHours { get; set; } = 8;
        [Comment("Maximum number of backup files. Oldest will be deleted. Minimum value is 2. Default value is 10.")]
        public int SqliteBackupMaxFiles { get; set; } = 10;
    }


    public class ConfigSettings: ValidatableSettings
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
        [Required]
        public ObservableCollection<string> DiscordAdminRoles { get; set; } = new ObservableCollection<string>();
        public ObservableCollection<ulong> ComForbiddenChannels { get; set; } = new ObservableCollection<ulong>();
#else
        [Comment("At least one role name from Discord with admin privilegies")]
        [Required]
        public List<string> DiscordAdminRoles { get; set; } = new List<string>();
        [Comment("The list of numeric channel IDs in which bot commands will be ignored")]
        public List<ulong> ComForbiddenChannels { get; set; } = new List<ulong>();
#endif
        [Comment("Interface and message language. Note that console text and logs will always be in english. You can add your own translation files in Languages directory.")]
        public string Language { get; set; } = "en-US";
        [Comment("Specifies if queries and results from ESI should be received only in english or using the language settings")]
        public bool UseEnglishESIOnly { get; set; } = true;

        public bool ModuleWebServer { get; set; } = false;
        [Comment("Enable auth check module which checks all authenticated users and strips roles if user has left your corp or alliance")]
        public bool ModuleAuthCheck { get; set; } = false;
        public bool ModuleAuthWeb { get; set; } = false;
        [Comment("Enable char and corp query module (!char and !corp commands)")]
        public bool ModuleCharCorp { get; set; } = false;
        public bool ModuleLiveKillFeed { get; set; } = false;
        // public bool ModuleReliableKillFeed { get; set; } = false;
        [Comment("Enable price check module (!pc, !jita etc. commands)")]
        public bool ModulePriceCheck { get; set; } = false;
        [Comment("Enable EVE time module (!time command)")]
        public bool ModuleTime { get; set; } = false;
        //public bool ModuleFleetup { get; set; } = false;
        public bool ModuleJabber { get; set; } = false;
        public bool ModuleMOTD { get; set; } = false;
        public bool ModuleNotificationFeed { get; set; } = false;
        public bool ModuleTimers { get; set; } = false;
        public bool ModuleMail { get; set; } = false;
        public bool ModuleIRC { get; set; } = false;
        public bool ModuleTelegram { get; set; } = false;
        public bool ModuleChatRelay { get; set; } = false;
        public bool ModuleIncursionNotify { get; set; } = false;
        public bool ModuleNullsecCampaign { get; set; } = false;
        public bool ModuleFWStats { get; set; } = true;
        public bool ModuleLPStock { get; set; } = true;
        public bool ModuleHRM { get; set; } = false;
        public bool ModuleSystemLogFeeder { get; set; } = false;
        public bool ModuleStats { get; set; } = true;
        public bool ModuleContractNotifications { get; set; } = false;
        public bool ModuleSovTracker { get; set; } = false;
        public bool ModuleWebConfigEditor { get; set; } = false;
        public bool ModuleIndustrialJobs { get; set; } = false;

        public string TimeFormat { get; set; } = "dd.MM.yyyy HH:mm:ss";
        public string ShortTimeFormat { get; set; } = "dd.MM.yyyy HH:mm";
        public string DateFormat { get; set; } = "dd.MM.yyyy";
        [Comment("Display welcome message with authentication offer to all new users joining your Discord group hallway")]
        public bool WelcomeMessage { get; set; } = true;
        [Comment("Welcome message Discord channel ID")]
        public ulong WelcomeMessageChannelId { get; set; }
        [Comment("Time interval in minutes to purge all outdated cache")]
        public int CachePurgeInterval { get; set; } = 30;
        [Comment("Memory usage limit in Mb. If app reaches that limit it will try to free some memory")]
        public int MemoryUsageLimitMb { get; set; } = 100;
        [Comment("Log all the app messages by specified severity and above (Values: Info, Error, Critical)")]
        public string LogSeverity { get; set; } = "Info";
        [Comment("Log all app output into a single file instead of many files each for its own module")]
        public bool UseSingleFileForLogging { get; set; }
        [Comment("FALSE by default. Set to TRUE if you want to log all raw notifications data the bot will fetch. This is needed to catch notifications which the bot could not yet process. Send me acquired data to add notifications you will like to be processed by the bot")]
        public bool LogNewNotifications { get; set; } = true;
        [Comment("Number of web-request retries before treating it as failed")]
        public int RequestRetries { get; set; } = 3;
        [Comment("Number of threads for concurrent operations. Default value is 4.")]
        public int ConcurrentThreadsCount { get; set; } = 4;

        public bool ExtendedESILogging { get; set; } = false;
        public string ESIAddress { get; set; } = "https://esi.evetech.net/";
        public bool UseHTTPS { get; set; } = false;
        public bool RunAsServiceCompatibility { get; set; } = false;
        public bool DisableLogIntoFiles { get; set; } = false;
        [Comment("Optional path to language files folder. Empty by default and will use default folder")]
        public string LanguageFilesFolder { get; set; }


#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(BotDiscordToken):
                        return string.IsNullOrEmpty(BotDiscordToken) ? Compose(nameof(BotDiscordToken), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(BotDiscordName):
                        return string.IsNullOrEmpty(BotDiscordName) ? Compose(nameof(BotDiscordName), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(BotDiscordGame):
                        return string.IsNullOrEmpty(BotDiscordGame) ? Compose(nameof(BotDiscordGame), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(BotDiscordCommandPrefix):
                        return string.IsNullOrEmpty(BotDiscordCommandPrefix) ? Compose(nameof(BotDiscordCommandPrefix), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(DiscordGuildId):
                        return DiscordGuildId == 0 ? Compose(nameof(DiscordGuildId), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(DiscordAdminRoles):
                        return DiscordAdminRoles.Count == 0 ? Compose(nameof(DiscordAdminRoles), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    //case nameof(DatabaseFile):
                    //    return string.IsNullOrEmpty(DatabaseFile) ? Compose(nameof(DatabaseFile), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class SystemLogFeederSettings : ValidatableSettings
    {
        [Comment("Discord channel ID")]
        [Required]
        public ulong DiscordChannelId { get; set; }

        [Comment("Log severity: Info, Module, Warning, Error, Critical")]
        public string LogSeverity { get; set; } = "Info";

        [Comment("Message send interval to Discord in msec")]
        public int SendInterval { get; set; } = 5000;

#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(DiscordChannelId):
                        return DiscordChannelId == 0 ? Compose(nameof(DiscordChannelId), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }
                return null;
            }
        }
#endif
    }

    public class WebServerModuleSettings: ValidatableSettings
    {
        /*  [Comment("Text IP address or domain name which the bot will use to listen for connections. \nIf the machine the bot running on have direct access to the internet then it should be equal\n to **webExternalIP** overwise it is the intrAnet address of your machine")]
          [Required]
          public string WebListenIP { get; set; }
          [Comment("Numeric port value")]
          [Required]
          public int WebListenPort { get; set; }*/
        [Comment("Use the port in URLs")]
        [Required]
        public bool UsePortInUrl { get; set; } = true;
        [Comment("Text IP address or domain name which is used to receive connections from the internet")]
        public string WebExternalIP { get; set; }
        [Comment("Numeric port value")]
        [Required]
        public int WebExternalPort { get; set; }
        [Comment("Port for reporting bot status. Will return status on query or nothing if bot is not running. Return values: OK, NO_ESI, NO_CONNECTION, NO_DISCORD")]
        public int ServerStatusPort { get; set; }

        [Comment("Status report will act as the bot isn't running if Discord connection is not available (do not respond to queries)")]
        public bool NoStatusResponseOnDiscordDisconnection { get; set; } = true;
        [Comment("Discord group invitation url")]
        public string DiscordUrl { get; set; }
        [Comment("Text client ID from the CCP application")]
        [Required]
        public string CcpAppClientId { get; set; }
        [Comment("Text client code from the CCP application")]
        [Required]
        public string CcpAppSecret { get; set; }

#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                   /* case nameof(WebListenIP):
                        return string.IsNullOrEmpty(WebListenIP) ? Compose(nameof(WebListenIP), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(WebListenPort):
                        return WebListenPort == 0 ? Compose(nameof(WebListenPort), Extensions.ERR_MSG_VALUEEMPTY) : null;
                  */  case nameof(WebExternalIP):
                        return string.IsNullOrEmpty(WebExternalIP) ? Compose(nameof(WebExternalIP), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(WebExternalPort):
                        return WebExternalPort == 0 ? Compose(nameof(WebExternalPort), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(CcpAppClientId):
                        return string.IsNullOrEmpty(CcpAppClientId) ? Compose(nameof(CcpAppClientId), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(CcpAppSecret):
                        return string.IsNullOrEmpty(CcpAppSecret) ? Compose(nameof(CcpAppSecret), Extensions.ERR_MSG_VALUEEMPTY) : null;
                }

                return null;
            }
        }
#endif
    }

    public class WebAuthModuleSettings: ValidatableSettings
    {
        [Comment("Numeric time interval in minutes to run auth checks of existing users")]
        [Required]
        public int AuthCheckIntervalMinutes { get; set; } = 60;

        [Comment("Take this amount of users per auth check pass. Auth delay is 2 min so it's N users per 2 minutes. If check interval is 60 min then it is max 60/2*100=3000 users per hour can be checked.")]
        public int AuthTakeNumberOfUsersPerPass { get; set; } = 100;
        
        [Comment("Numeric time interval in minutes to run standings update for feed users")]
        public int StandingsRefreshIntervalInMinutes { get; set; } = 60;

        [Comment("Numeric ID of the Discord channel to report bot auth actions. Preferably for admins only. Leave 0 to disable")]
        public ulong AuthReportChannel { get; set; }
        [Comment("Automatically assign corp tickers to users")]
        public bool EnforceCorpTickers { get; set; }
        [Comment("Automatically assign alliance tickers to users")]
        public bool EnforceAllianceTickers { get; set; }
        [Comment("Automatically assign alliance ticker to user or corp ticker if not in alliance")]
        public bool EnforceSingleTickerPerUser { get; set; }
        [Comment("Automatically assign character names to users (setup Discord group to disallow name change also)")]
        public bool EnforceCharName { get; set; }
        [Comment("Default group to use for auth url display")]
        public string DefaultAuthGroup { get; set; }
        [Comment("By default each auth group have own auth button. With this option on there wll be only one button and auth will search for first group with matching condition automatically.")]
        public bool UseOneAuthButton { get; set; } = false;
        public bool EnableDetailedLogging { get; set; }

        [Comment("Auto clear !auth commands text from discord channels to reduce clutter")]
        public bool AutoClearAuthCommandsFromDiscord { get; set; }
        [Comment("Check Discord users that do not have authentication")]
        public bool AuthCheckUnregisteredDiscordUsers { get; set; } = true;

#if EDITOR
        [Comment("The list of Discord role names which will not be checked for authentication (admins etc.)")]
        public ObservableCollection<string> ExemptDiscordRoles { get; set; } = new ObservableCollection<string>();
        [Comment("The list of Discord roles which will not be stripped if character is authed. This will allow you to add custom roles manually.")]
        public ObservableCollection<string> AuthCheckIgnoreRoles { get; set; } = new ObservableCollection<string>();
        [Comment("The list of channels where !auth command is allowed")]
        [Required]
        public ObservableCollection<ulong> ComAuthChannels { get; set; } = new ObservableCollection<ulong>();
        [Comment("The list of groups to filter auth requests")]
        [Required]
        public ObservableDictionary<string, WebAuthGroup> AuthGroups { get; set; } = new ObservableDictionary<string, WebAuthGroup>();        
#else
        public List<string> ExemptDiscordRoles { get; set; } = new List<string>();
        public List<string> AuthCheckIgnoreRoles { get; set; } = new List<string>();
        public List<ulong> ComAuthChannels { get; set; } = new List<ulong>();
        public Dictionary<string, WebAuthGroup> AuthGroups { get; set; } = new Dictionary<string, WebAuthGroup>();
#endif
#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(AuthCheckIntervalMinutes):
                        return AuthCheckIntervalMinutes < 1 ? Compose(nameof(AuthCheckIntervalMinutes), "Value must be greater than 0!") : null;
                    case nameof(ComAuthChannels):
                        return ComAuthChannels.Count == 0 ? Compose(nameof(ComAuthChannels), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(AuthGroups):
                        return AuthGroups.Count == 0 ? Compose(nameof(AuthGroups), Extensions.ERR_MSG_VALUEEMPTY) : null;
                    case nameof(DefaultAuthGroup):
                        return !string.IsNullOrEmpty(DefaultAuthGroup) && AuthGroups.All(a => a.Key != DefaultAuthGroup) ? Compose(nameof(DefaultAuthGroup), "DefaultAuthGroup do not equal to any of the AuthGroups names!") : null;
                }

                return null;
            }
        }
#endif
    }

    public class WebAuthGroup: ValidatableSettings
    {

#if EDITOR
        [Comment("Dictionary containing member identification and the list of Discord roles which to assign on successful auth")]
        public ObservableDictionary<string, AuthRoleEntity> AllowedMembers { get; set; } = new ObservableDictionary<string, AuthRoleEntity>();

        [Comment("The list of exact Discord role names authorized to manually approve applicants")]
        public ObservableCollection<string> AuthRoles { get; set; } = new ObservableCollection<string>();

        [Comment("Text Discord roles list which can be added manually to a user and will persist while he passes auth check for this group. Stripped when he is not in a specified corp/ally anymore.")]
        public ObservableCollection<string> ManualAssignmentRoles { get; set; } = new ObservableCollection<string>();
        [Comment("List of linked group names for additional auth checks. If user passes auth from one of this groups it will be moved there. ")]
        public ObservableCollection<string> UpgradeGroupNames { get; set; } = new ObservableCollection<string>();
        [Comment("List of linked group names for additional auth checks. If user group has been invalidated he will pass a check for all groups mentioned in this list. ")]
        public ObservableCollection<string> DowngradeGroupNames { get; set; } = new ObservableCollection<string>();
        
#else
        public List<string> AuthRoles { get; set; } = new List<string>();
        public Dictionary<string, AuthRoleEntity> AllowedMembers { get; set; } = new Dictionary<string, AuthRoleEntity>();
        public List<string> ManualAssignmentRoles { get; set; } = new List<string>();
        public List<string> UpgradeGroupNames { get; set; } = new List<string>();
        public List<string> DowngradeGroupNames { get; set; } = new List<string>();
#endif
        [Comment("Remove user authentication if supplied ESI token has become invalid")]
        public bool RemoveAuthIfTokenIsInvalid { get;set; }

        [Comment("Remove token form user authentication data if supplied ESI token has become invalid")]
        public bool RemoveTokenIfTokenIsInvalid { get; set; }

        [Comment("Enable auth mode that will only search roles until first criteria match. Otherwise it wil search and add roles from all matching filters within this group")]
        public bool StopSearchingOnFirstMatch { get; set; }

        [Comment("Hide this group button but allow to participate in upgrades")]
        public bool Hidden { get; set; }

        [Comment("Enable auth to require manual acceptance from authorized members")]
        public bool PreliminaryAuthMode { get; set; }

        [Comment("Enable automatic applications invalidation and cleanup in specified amount of hours")]
        public int AppInvalidationInHours { get; set; } = 48;

        [Comment("Optional text to display in a web-server group auth button if separate button is generated for this group")]
        public string CustomButtonText { get; set; }
        [Comment("Optional default Discord mention for auth report")]
        public string DefaultMention { get; set; }

        [Comment("Optional data for standings auth. Will be auto removed if no CharacterIDs are specified otherwise will make this group to work using standings.")]
        public StandingsAuthGroupExtension StandingsAuth { get; set; } = null;

        [Comment("Skip Discord auth page display after CCP auth (when you want to get ESI token without actual auth)")]
        public bool SkipDiscordAuthPage { get; set; } = false;

        [Comment("Bind authenticating character to main character which is already exist")]
        public bool BindToMainCharacter { get; set; } = false;


        [Comment("Optional switch to exclude this group from OneButtonMode if corresponding option is enabled on a section level")]
        public bool ExcludeFromOneButtonMode { get; set; } = false;

        public bool IsEmpty()
        {
            return !AllowedMembers.Any() && StandingsAuth == null;
        }
#if EDITOR
        [Comment("The list of ESI access role names to check on auth")]
        public ObservableCollection<string> ESICustomAuthRoles { get; set; } = new ObservableCollection<string>();
#else
        public List<string> ESICustomAuthRoles { get; set; } = new List<string>();

        [JsonIgnore]
        public bool MustHaveGroupName => true; //ESICustomAuthRoles.Any() || StandingsAuth != null;
#endif
#if EDITOR

        public override void OnEditorSave()
        {
            if (StandingsAuth != null && !StandingsAuth.CharacterIDs.Any())
                StandingsAuth = null;
        }


        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(PreliminaryAuthMode):
                        return ESICustomAuthRoles.Count == 0 ? Compose(nameof(ESICustomAuthRoles), "ESICustomAuthRoles must contain at least one value when PreliminaryAuthMode is true!") : null;
                    case nameof(ESICustomAuthRoles):
                        var wrong = ESICustomAuthRoles.Where(a => !SettingsManager.ESIScopes.Contains(a)).ToList();
                        return wrong.Count > 0 ? Compose(nameof(ESICustomAuthRoles), $"ESICustomAuthRoles contains unidentified ESI scopes: {string.Join(", ", wrong)}") : null;
                }

                return null;
            }
        }
#endif
    }

    public class StandingsAuthGroupExtension
    {
        
#if EDITOR
        public ObservableCollection<long> CharacterIDs { get; set; } = new ObservableCollection<long>();
        public ObservableDictionary<string, StandingGroup> StandingFilters { get; set; } = new ObservableDictionary<string, StandingGroup>();

#else
        public List<long> CharacterIDs { get; set; } = new List<long>();
        public Dictionary<string, StandingGroup> StandingFilters { get; set; } = new Dictionary<string, StandingGroup>();

#endif

        public string WebAdminButtonText { get; set; } = "Standings Admin Auth";
        public bool UseCharacterStandings { get; set; } = true;
        public bool UseCorporationStandings { get; set; } = false;
        public bool UseAllianceStandings { get; set; } = false;
    }

    public class AuthRoleEntity: ValidatableSettings
    {


#if EDITOR
        [Comment("The list of Discord role names to assign after successful auth")]
        public ObservableCollection<string> DiscordRoles { get; set; } = new ObservableCollection<string>();
        [Comment("List of names and Ids of entities (char/corp/alliance)")]
        [Required]
        public ObservableCollection<object> Entities { get; set; } = new ObservableCollection<object>();
        [Comment("Optional titles list to assign Discord roles")]
        public ObservableDictionary<string, TitleAuthGroup> Titles { get; set; } = new ObservableDictionary<string, TitleAuthGroup>();
#else
        public List<string> DiscordRoles { get; set; } = new List<string>();
        public List<object> Entities { get; set; } = new List<object>();
        public Dictionary<string, TitleAuthGroup> Titles { get; set; } = new Dictionary<string, TitleAuthGroup>();
#endif


#if EDITOR
        public override string this[string columnName]
        {
            get
            {
                switch (columnName)
                {
                    case nameof(Entities):
                        return Entities.Count == 0  ? Compose(nameof(Entities), "You must set at least one entity!") : null;
                }

                return null;
            }
        }
#endif
    }

    public class TitleAuthGroup
    {
#if EDITOR
        [Comment("List of text title names")]
        public ObservableCollection<string> TitleNames { get; set; } = new ObservableCollection<string>();
        [Comment("List of text Discord role names to assign for selected titles")]
        public ObservableCollection<string> DiscordRoles { get; set; } = new ObservableCollection<string>();
#else
        public List<string> TitleNames { get; set; } = new List<string>();
        public List<string> DiscordRoles { get; set; } = new List<string>();
#endif
    }
}
