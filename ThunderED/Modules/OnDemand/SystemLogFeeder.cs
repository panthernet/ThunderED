using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules.OnDemand
{
    public class SystemLogFeeder: AppModuleBase
    {
        public override LogCat Category => LogCat.SLF;
        private static readonly Timer Timer;
        private static bool _isEnabled;
        private static readonly object Locker = new object();
        static SystemLogFeeder()
        {
            _isEnabled = true;
            Timer = new Timer(5000) {AutoReset = true};
            Timer.Elapsed += (sender, e) =>
            {
                //_isEnabled = true;
                lock (Locker)
                {
                    if (Package.Count > 0)
                    {
                        SendMessage().GetAwaiter().GetResult();
                    }
                }
            };
            Timer.Start();
        }

        private static readonly ConcurrentBag<string> Package = new ConcurrentBag<string>();

        private static async Task SendMessage()
        {
            var message = string.Join(Environment.NewLine, Package.ToArray());
            await APIHelper.DiscordAPI.SendMessageAsync(SettingsManager.Settings.SystemLogFeederModule.DiscordChannelId, message);
            Package.Clear();

        }

        public static async Task FeedMessage(string message, bool isCritical)
        {
            try
            {
                if(!_isEnabled) return;
                if (APIHelper.DiscordAPI == null || !APIHelper.DiscordAPI.IsAvailable || !SettingsManager.Settings.Config.ModuleSystemLogFeeder || SettingsManager.Settings.SystemLogFeederModule.DiscordChannelId == 0) return;

                if(!isCritical && SettingsManager.Settings.SystemLogFeederModule.OnlyErrors) return;
                if (message.Contains($"{SettingsManager.Settings.SystemLogFeederModule.DiscordChannelId}"))
                {
                    _isEnabled = false;
                    Timer.Start();
                }

                if (Package.ToArray().Sum(a => a.Length) + message.Length >= 2000)
                    await SendMessage();
                Package.Add(message);
            }
            catch
            {
                //ignore
            }
        }
    }
}
