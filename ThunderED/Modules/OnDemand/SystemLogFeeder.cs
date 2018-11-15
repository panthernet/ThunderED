using System;
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

        static SystemLogFeeder()
        {
            Timer = new Timer(5000) {AutoReset = false};
            Timer.Elapsed += (sender, e) => { _isEnabled = true; };
            Timer.Start();
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

                await APIHelper.DiscordAPI.SendMessageAsync(SettingsManager.Settings.SystemLogFeederModule.DiscordChannelId, message);
            }
            catch
            {
                //ignore
            }
        }
    }
}
