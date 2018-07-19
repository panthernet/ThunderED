using System;
using System.Collections.Async;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ByteSizeLib;
using Discord.Commands;
using ThunderED.Helpers;
using ThunderED.Modules;
using ThunderED.Modules.OnDemand;
using ThunderED.Modules.Static;
using ThunderED.Modules.Sub;

namespace ThunderED.Classes
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public static partial class TickManager
    {
        private static bool _running;
        private static DateTime _asyncNow = DateTime.Now;
        private static DateTime _asyncToday = DateTime.Today;
        private static DateTime _lastTickDate = DateTime.Now;

        private static readonly List<AppModuleBase> Modules = new List<AppModuleBase>();
        private static readonly List<AppModuleBase> OnDemandModules = new List<AppModuleBase>();

        public static T GetModule<T>()
        {
            var type = typeof(T);
            var o = Modules.FirstOrDefault(a => a.GetType() == type) ?? OnDemandModules.FirstOrDefault(a => a.GetType() == type);
            return (T)(object)o;
        }

        public static void LoadModules()
        {          
            Modules.Clear();
            OnDemandModules.Clear();

            //sub modules - core modules that are called in each tick and can supply other modules with some data
            Modules.Add(new ZKillLiveFeedModule());
            Modules.Add(new WebServerModule());
            Modules.Add(new ContinuousCheckModule());

            //dynamic modules - called in each tick

            if (SettingsManager.Settings.Config.ModuleAuthCheck)
                Modules.Add(new AuthCheckModule());

           // if (SettingsManager.Settings.Config.ModuleReliableKillFeed)
           //     Modules.Add(new ReliableKillModule());

            if (SettingsManager.Settings.Config.ModuleNotificationFeed)
                Modules.Add(new NotificationModule());

            if (SettingsManager.Settings.Config.ModuleJabber)
                Modules.Add(new JabberModule());

            if (SettingsManager.Settings.Config.ModuleFleetup)
                Modules.Add(new FleetUpModule());

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

            //on demand modules - only could be pinged by other modules
            if (SettingsManager.Settings.Config.ModuleLiveKillFeed)
                OnDemandModules.Add(new LiveKillFeedModule());

            if (SettingsManager.Settings.Config.ModuleRadiusKillFeed)
                OnDemandModules.Add(new RadiusKillFeedModule());

            if (SettingsManager.Settings.Config.ModuleChatRelay)
                OnDemandModules.Add(new ChatRelayModule());

                //IMPORTANT - web auth is the last module - to be the last for 404 handling
            if (SettingsManager.Settings.Config.ModuleAuthWeb)
                OnDemandModules.Add(new WebAuthModule());


            //subscriptions
            if (SettingsManager.Settings.Config.ModuleIRC)
                APIHelper.DiscordAPI.SubscribeRelay(GetModule<IRCModule>());
            if (SettingsManager.Settings.Config.ModuleTelegram)
                APIHelper.DiscordAPI.SubscribeRelay(GetModule<TelegramModule>());

        }

        public static async void Tick(object stateInfo)
        {
            try
            {
                if (!_running && APIHelper.DiscordAPI.IsAvailable)
                {
                    _running = true;
                    await Async_Tick(stateInfo);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex,LogCat.Tick );
            }
        }

        private static DateTime _lastOnlineCheck;

        public static bool IsNoConnection { get; private set; }
        public static bool IsConnected => !IsNoConnection;

        private static async Task Async_Tick(object stateInfo)
        {
            _asyncNow = DateTime.Now;
            _asyncToday = DateTime.Today;

            try
            {
                if ((_asyncNow - _lastOnlineCheck).TotalSeconds > 5)
                {
                    _lastOnlineCheck = _asyncNow;
                    if (!await APIHelper.ESIAPI.IsServerOnline("General"))
                    {
                        if (!IsNoConnection)
                        {
                            await LogHelper.LogWarning("EVE server is offline or there is a connection problem!", LogCat.ESI);
                            await LogHelper.LogWarning("Waiting for connection....", LogCat.ESI);
                        }
                        IsNoConnection = true;
                        return;
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

                if(IsNoConnection) return;

                Parallel.ForEach(Modules, module => module.Run(null));
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

        public static Task RunModule(Type type, ICommandContext context, object prm = null)
        {
            var module = Modules.FirstOrDefault(a => a.GetType() == type);
            module?.Run( prm ?? context);
            return Task.CompletedTask;
        }
    }
}
