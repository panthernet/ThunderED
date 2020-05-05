using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dasync.Collections;
using Discord.Commands;
using ThunderED.Helpers;
using ThunderED.Modules;
using ThunderED.Modules.OnDemand;
using ThunderED.Modules.Sub;
using HRMModule = ThunderED.Modules.HRMModule;

namespace ThunderED.Classes
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public static partial class TickManager
    {
        private static bool _running;
        private static DateTime _asyncNow = DateTime.Now;
       // private static DateTime _asyncToday = DateTime.Today;
       // private static DateTime _lastTickDate = DateTime.Now;

        private static readonly List<AppModuleBase> Modules = new List<AppModuleBase>();
        private static readonly List<AppModuleBase> OnDemandModules = new List<AppModuleBase>();

        public static T GetModule<T>()
        {
            var type = typeof(T);
            var o = Modules.FirstOrDefault(a => a.GetType() == type) ?? OnDemandModules.FirstOrDefault(a => a.GetType() == type);
            return (T)(object)o;
        }

        private static async Task LoadModules()
        {          
            Modules.Clear();
            OnDemandModules.Clear();

            //sub modules - core modules that are called in each tick and can supply other modules with some data
            Modules.Add(new ContinuousCheckModule());

            if (SettingsManager.Settings.Config.ModuleLiveKillFeed)
                Modules.Add(new ZKillLiveFeedModule());

            if (SettingsManager.Settings.Config.ModuleWebServer)
                Modules.Add(new WebServerModule());


            //dynamic modules - called in each tick

            if (SettingsManager.Settings.Config.ModuleAuthCheck)
                Modules.Add(new AuthCheckModule());

           // if (SettingsManager.Settings.Config.ModuleReliableKillFeed)
           //     Modules.Add(new ReliableKillModule());

            if (SettingsManager.Settings.Config.ModuleNotificationFeed)
                Modules.Add(new NotificationModule());

            if (SettingsManager.Settings.Config.ModuleJabber)
                Modules.Add(new JabberModule());

            /*if (SettingsManager.Settings.Config.ModuleFleetup)
                Modules.Add(new FleetUpModule());*/

            if (SettingsManager.Settings.Config.ModuleTimers)
                Modules.Add(new TimersModule());

            if (SettingsManager.Settings.Config.ModuleMail)
                Modules.Add(new MailModule());

            if(SettingsManager.Settings.Config.ModuleIRC)
                Modules.Add(new IRCModule());

            if(SettingsManager.Settings.Config.ModuleTelegram)
                Modules.Add(new TelegramModule());
            
            if(SettingsManager.Settings.Config.ModuleIncursionNotify)
                Modules.Add(new IncursionNotifyModule());

            if(SettingsManager.Settings.Config.ModuleNullsecCampaign)
                Modules.Add(new NullCampaignModule());

            if (SettingsManager.Settings.Config.ModuleContractNotifications)
                Modules.Add(new ContractNotificationsModule());

            if(SettingsManager.Settings.Config.ModuleSovTracker)
                Modules.Add(new SovTrackerModule());

            if(SettingsManager.Settings.Config.ModuleIndustrialJobs)
                Modules.Add(new IndustrialJobsModule());

            if (SettingsManager.Settings.Config.ModuleHRM)
            {
                Modules.Add(new HRMModule());
                foreach (var id in SettingsManager.Settings.HRMModule.GetEnabledGroups().SelectMany(a => a.Value.UsersAccessList).Distinct())
                    await APIHelper.ESIAPI.RemoveAllCharacterDataFromCache(id);
            }

            //on demand modules - only could be pinged by other modules or commands
            if (SettingsManager.Settings.Config.ModuleLiveKillFeed)
                OnDemandModules.Add(new LiveKillFeedModule());

           // if (SettingsManager.Settings.Config.ModuleRadiusKillFeed)
           //     OnDemandModules.Add(new RadiusKillFeedModule());

            if (SettingsManager.Settings.Config.ModuleChatRelay)
                OnDemandModules.Add(new ChatRelayModule());

            if (SettingsManager.Settings.CommandsConfig.EnableShipsCommand)
                OnDemandModules.Add(new ShipsModule());

            if (SettingsManager.Settings.Config.ModuleWebConfigEditor)
                OnDemandModules.Add(new WebSettingsModule());
            if (SettingsManager.Settings.CommandsConfig.EnableRoleManagementCommands)
                OnDemandModules.Add(new DiscordRolesManagementModule());

            if (SettingsManager.Settings.Config.ModuleSystemLogFeeder)
                OnDemandModules.Add(new SystemLogFeeder());

            //IMPORTANT - web auth is the last module - to be the last for 404 handling
            if (SettingsManager.Settings.Config.ModuleAuthWeb)
                Modules.Add(new WebAuthModule());

            await Modules.ParallelForEachAsync(async a => await a.Initialize(),  SettingsManager.MaxConcurrentThreads);
            await OnDemandModules.ParallelForEachAsync(async a => await a.Initialize(),  SettingsManager.MaxConcurrentThreads);

            //subscriptions
            if (SettingsManager.Settings.Config.ModuleIRC)
                APIHelper.DiscordAPI.SubscribeRelay(GetModule<IRCModule>());
            if (SettingsManager.Settings.Config.ModuleTelegram)
                APIHelper.DiscordAPI.SubscribeRelay(GetModule<TelegramModule>());

        }

        private static bool _isModulesLoaded;

        public static async void Tick(object stateInfo)
        {
            if (_running || !APIHelper.IsDiscordAvailable) return;

            _running = true;
            _asyncNow = DateTime.Now;

            try
            {
                if (!_isModulesLoaded)
                {
                    await LoadModules();
                    _isModulesLoaded = true;
                }

                #region ONLINE CHECK
                if ((_asyncNow - _lastOnlineCheck).TotalSeconds > 5)
                {
                    _lastOnlineCheck = _asyncNow;
                    var onlineType = await APIHelper.ESIAPI.IsServerOnlineEx("General");
                    IsESIUnreachable = onlineType == -1;

                    if (onlineType != 1)
                    {
                        if (IsConnected)
                        {
                            if(onlineType == 0)
                                await LogHelper.LogWarning("EVE server is offline!", LogCat.ESI);
                            if(onlineType == -1)
                                await LogHelper.LogWarning("EVE ESI API is unreachable!", LogCat.ESI);
                            await LogHelper.LogWarning("Waiting for connection....", LogCat.ESI);
                        }
                        IsNoConnection = true;
                    }
                    else
                    {
                        if (IsNoConnection)
                        {
                            IsNoConnection = false;
                            await LogHelper.LogWarning("EVE server is ONLINE or connection has been restored!", LogCat.ESI, true, false);
                        }
                    }
                }
                #endregion

                await ContinuousCheckModule.OneSec_TQStatusPost(_asyncNow);

                if(IsNoConnection || IsESIUnreachable) return;

                await Modules.ParallelForEachAsync(async module =>
                {
                    await module.RunInternal(null);
                }, SettingsManager.MaxConcurrentThreads);
                
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.Tick);
            }
            finally
            {
                _running = false;
            }

        }

        private static DateTime _lastOnlineCheck;

        public static bool IsNoConnection { get; private set; }
        public static bool IsConnected => !IsNoConnection;
        public static bool IsESIUnreachable { get; private set; }


        public static Task RunModule(Type type, ICommandContext context, object prm = null)
        {
            var module = Modules.FirstOrDefault(a => a.GetType() == type);
            module?.RunInternal(prm ?? context);
            return Task.CompletedTask;
        }

        public static void InvalidateModules()
        {
            WebServerModule.ModuleConnectors.Clear();
            WebServerModule.WebModuleConnectors.Clear();
            ZKillLiveFeedModule.Queryables.Clear();
            Modules.ForEach(a=> a.Cleanup());
            _isModulesLoaded = false;
        }

        public static bool AllModulesReadyToClose()
        {
            return Modules.All(a => !a.IsRunning && !a.IsRequestRunning);
        }
    }
}
