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
        public const string VERSION = "1.0.9";

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
