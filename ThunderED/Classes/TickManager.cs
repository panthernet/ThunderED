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
        private static DateTime _memoryCheckTime = DateTime.Now;
        private static DateTime _lastTickDate = DateTime.Now;
        private static DateTime _lastCacheCheckDate = DateTime.Now;

        private static readonly List<AppModuleBase> Modules = new List<AppModuleBase>();
        private static readonly List<AppModuleBase> OnDemandModules = new List<AppModuleBase>();

        public static T GetModule<T>()
        {
            return (T)(object)Modules.FirstOrDefault(a => a.GetType() == typeof(T));
        }

        public static void LoadModules()
        {          
            Modules.Clear();
            OnDemandModules.Clear();

            //sub modules - core modules that are called in each tick and can supply other modules with some data
            Modules.Add(new ZKillLiveFeedModule());
            Modules.Add(new WebServerModule());

            //dynamic modules - called in each tick

            if (SettingsManager.GetBool("config","moduleAuthCheck"))
                Modules.Add(new AuthCheckModule());

            if (SettingsManager.GetBool("config","moduleReliableKillFeed"))
                Modules.Add(new ReliableKillModule());

            if (SettingsManager.GetBool("config","moduleNotificationFeed"))
                Modules.Add(new NotificationModule());

            if (SettingsManager.GetBool("config","moduleJabber"))
                Modules.Add(new JabberModule());

            if (SettingsManager.GetBool("config","moduleFleetup"))
                Modules.Add(new FleetUpModule());

            if (SettingsManager.GetBool("config","moduleTimers"))
                Modules.Add(new TimersModule());

            if (SettingsManager.GetBool("config","moduleMail"))
                Modules.Add(new MailModule());

            if(SettingsManager.GetBool("config", "moduleIRC"))
                Modules.Add(new IRCModule());

            if(SettingsManager.GetBool("config", "moduleTelegram"))
                Modules.Add(new TelegramModule());

            //on demand modules - only could be pinged by other modules
            if (SettingsManager.GetBool("config","moduleLiveKillFeed"))
                OnDemandModules.Add(new LiveKillFeedModule());

            if (SettingsManager.GetBool("config","moduleRadiusKillFeed"))
                OnDemandModules.Add(new RadiusKillFeedModule());

            if (SettingsManager.GetBool("config","moduleAuthWeb"))
                OnDemandModules.Add(new WebAuthModule());

            //subscriptions
            if (SettingsManager.GetBool("config", "moduleIRC"))
                APIHelper.DiscordAPI.SubscribeRelay(GetModule<IRCModule>());
            if (SettingsManager.GetBool("config", "moduleTelegram"))
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

        private static int _cacheInterval;

        private static async Task Async_Tick(object stateInfo)
        {
            _asyncNow = DateTime.Now;
            _asyncToday = DateTime.Today;

            try
            {
                //cache handling
                if ((_asyncNow - _memoryCheckTime).TotalMinutes >= 30)
                {
                    var mem = SettingsManager.GetInt("config", "memoryUsageLimitMb");
                    if (mem > 0)
                    {
                        _memoryCheckTime = _asyncNow;
                        var size = ByteSize.FromBytes(Process.GetCurrentProcess().WorkingSet64);
                        if (size.MegaBytes > mem)
                        {
                           // APIHelper.ResetCache();
                            GC.Collect();
                        }
                    }
                }

                //display day stats on day change
                if (_lastTickDate.Date != _asyncToday.Date)
                {
                    _lastTickDate = _asyncToday;
                    await LogHelper.LogInfo("Running auto day stats post...", LogCat.Tick);
                    await StatsModule.Stats(null, "newday");
                }

                //purge unused cache from memory
                _cacheInterval = _cacheInterval != 0? _cacheInterval : SettingsManager.GetInt("config", "cachePurgeInterval");
                if ((_asyncNow - _lastCacheCheckDate).TotalMinutes >= _cacheInterval)
                {
                    await LogHelper.LogInfo("Running cache purge...", LogCat.Tick);

                    _lastCacheCheckDate = _asyncNow;
                    APIHelper.PurgeCache();
                }

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
