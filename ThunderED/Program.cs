using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED
{
    internal partial class Program
    {
        private static Timer _timer;

        private static async Task Main(string[] args)
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            // var ssss = new List<JsonZKill.ZkillOnly>().Count(a => a.killmail_id == 0);

            if (!File.Exists(SettingsManager.FileSettingsPath))
            {
                await LogHelper.LogError("Please make sure you have settings.json file in bot folder! Create it and fill with correct settings.");
                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Please make sure you have settings.json file in bot folder! Create it and fill with correct settings.");
                    Console.ReadKey();
                }
                catch
                {
                    // ignored
                }

                return;
            }

            //load settings
            var result = await SettingsManager.Prepare();
            if (!string.IsNullOrEmpty(result))
            {
                await LogHelper.LogError(result);
                try
                {
                    Console.ReadKey();
                }
                catch
                {
                    // ignored
                }

                return;
            }

            APIHelper.Prepare();
            await LogHelper.LogInfo($"ThunderED v{VERSION} is running!").ConfigureAwait(false);
            //load database provider
            var rs = await SQLHelper.LoadProvider();
            if (!string.IsNullOrEmpty(rs))
            {
                await LogHelper.LogError(result);
                try
                {
                    Console.ReadKey();
                }
                catch
                {
                    // ignored
                }

                return;
            }

            //load language
            await LM.Load();
            //load injected settings
            await SettingsManager.UpdateInjectedSettings();
            //load APIs
            await APIHelper.DiscordAPI.Start();

            while (!APIHelper.DiscordAPI.IsAvailable)
            {
                await Task.Delay(10);
            }

            if (APIHelper.DiscordAPI.GetGuild() == null)
            {
                await LogHelper.LogError("[CRITICAL] DiscordGuildId - Discord guild not found!");
                try
                {
                    Console.ReadKey();
                }
                catch
                {
                    // ignored
                }

                return;
            }

            //initiate core timer
            _timer = new Timer(TickManager.Tick, new AutoResetEvent(true), 100, 100);

            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = false;
                _timer?.Dispose();
                APIHelper.DiscordAPI.Stop();
            };

            AppDomain.CurrentDomain.UnhandledException += async (sender, eventArgs) =>
            {
                await LogHelper.LogEx($"[UNHANDLED EXCEPTION]", (Exception)eventArgs.ExceptionObject);
                await LogHelper.LogWarning($"Consider restarting the service...");
            };

            while (true)
            {
                if (!SettingsManager.Settings.Config.RunAsServiceCompatibility)
                {
                    var command = Console.ReadLine();
                    var arr = command?.Split(" ");
                    if ((arr?.Length ?? 0) == 0) continue;
                    switch (arr[0])
                    {
                        case "quit":
                            Console.WriteLine("Quitting...");
                            _timer.Dispose();
                            APIHelper.DiscordAPI.Stop();
                            return;
                        case "flushn":
                            Console.WriteLine("Flushing all notifications DB list");
                            await SQLHelper.RunCommand("delete from notificationsList");
                            break;
                        case "flushcache":
                            Console.WriteLine("Flushing all cache from DB");
                            await SQLHelper.RunCommand("delete from cache");
                            break;
                        case "help":
                            Console.WriteLine("List of available commands:");
                            Console.WriteLine(" quit    - quit app");
                            Console.WriteLine(" flushn  - flush all notification IDs from database");
                            Console.WriteLine(" getnurl - display notification auth url");
                            Console.WriteLine(" flushcache - flush all cache from database");
                            Console.WriteLine(" token [ID] - refresh and display EVE character token from database");
                            break;
                        case "token":
                            if (arr.Length == 1) continue;
                            if (!long.TryParse(arr[1], out var id))
                                continue;
                            var rToken = await SQLHelper.GetRefreshTokenDefault(id);
                            Console.WriteLine(await APIHelper.ESIAPI
                                .RefreshToken(rToken, SettingsManager.Settings.WebServerModule.CcpAppClientId, SettingsManager.Settings.WebServerModule.CcpAppSecret));
                            break;
                    }

                    await Task.Delay(10);
                }
                else await Task.Delay(500);
            }
        }
    }
}
