using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ByteSizeLib;
using Discord.Commands;
using ThunderED.Helpers;
using ThunderED.Modules;
using ThunderED.Modules.Static;

namespace ThunderED.Classes
{
    public static class TickManager
    {
        private static bool _running;
        private static DateTime _asyncNow = DateTime.Now;
        private static DateTime _asyncToday = DateTime.Today;
        private static DateTime _memoryCheckTime = DateTime.Now;
        private static DateTime _lastTickDate = DateTime.Now;
        private static DateTime _lastCacheCheckDate = DateTime.Now;

        private static readonly List<AppModuleBase> Modules = new List<AppModuleBase>();

        public static void LoadModules()
        {          
            Modules.Clear();

            //auth module
            if (SettingsManager.GetBool("config","moduleAuthWeb"))
                Modules.Add(new WebAuthModule());

            //auth check
            if (SettingsManager.GetBool("config","moduleAuthCheck"))
                Modules.Add(new AuthCheckModule());

            if (SettingsManager.GetBool("config","moduleKillFeed"))
                Modules.Add(new KillModule());

            if (SettingsManager.GetBool("config","moduleNotificationFeed"))
                Modules.Add(new NotificationModule());

            if (SettingsManager.GetBool("config","moduleJabber"))
                Modules.Add(new JabberModule());
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

                Modules.ForEach(module => module.Run(null));

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
