using System;
using System.IO;
using System.Threading;
using System.Web;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED
{
    internal class Program
    {
        private static Timer _timer;
        public const string VERSION = "1.0.4";

        private static void Main(string[] args)
        {
            //load settings
            SettingsManager.Prepare();
            LogHelper.LogInfo($"ThunderED v{VERSION} is running!").GetAwaiter().GetResult();
            //load database provider
            var result = SQLiteHelper.LoadProvider();
            if (!string.IsNullOrEmpty(result))
            {
                Console.BackgroundColor = ConsoleColor.Red;
                Console.WriteLine(result);
                Console.ReadKey();
                return;
            }
            //update config settings
            if (SettingsManager.GetBool("config", "moduleNotificationFeed"))
                SettingsManager.NextNotificationCheck = DateTime.Parse(SQLiteHelper.SQLiteDataQuery("cacheData", "data", "name", "nextNotificationCheck").GetAwaiter().GetResult());
            //load language
            LM.Load().GetAwaiter().GetResult();
            //load APIs
            APIHelper.Prepare().GetAwaiter().GetResult();
            //Load modules
            TickManager.LoadModules();
            //initiate core timer
            _timer = new Timer(TickManager.Tick, new AutoResetEvent(true), 100, 100);

            while (true)
            {
                var command = Console.ReadLine();
                switch (command?.Split(" ")[0])
                {
                    case "quit":
                        Console.WriteLine("Quitting...");
                        _timer.Dispose();
                        APIHelper.DiscordAPI.Stop();
                        return;
                    case "getnurl":
                        var cb = HttpUtility.UrlEncode(SettingsManager.Get("auth", "callbackUrl"));
                        var client_id = SettingsManager.Get("auth", "ccpAppClientId");
                        var url =
                            $"https://login.eveonline.com/oauth/authorize/?response_type=code&redirect_uri={cb}&client_id={client_id}&scope=esi-characters.read_notifications.v1&state=9";
                        File.WriteAllText(Path.Combine(SettingsManager.RootDirectory, "logs", "getnurl.txt"), $"{url}{Environment.NewLine}");
                        var text =
                            $"Notification feeder URL:{Environment.NewLine}{url} {Environment.NewLine}Get this url from 'logs/getnurl.txt' file!";
                        Console.WriteLine(text);
                        break;
                    case "flushn":
                        Console.WriteLine("Flushing all notifications DB list");
                        SQLiteHelper.RunCommand("delete from notificationsList").GetAwaiter().GetResult();
                        break;
                    case "flushcache":
                        Console.WriteLine("Flushing all cache from DB");
                        SQLiteHelper.RunCommand("delete from cache").GetAwaiter().GetResult();
                        break;
                    case "help":
                        Console.WriteLine("List of available commands:");
                        Console.WriteLine(" quit    - quit app");
                        Console.WriteLine(" flushn  - flush all notification IDs from database");
                        Console.WriteLine(" getnurl - display notification auth url");
                        Console.WriteLine(" flushcache - flush all cache from database");
                        break;
                }
                Thread.Sleep(10);
            }
        }
    }
}
