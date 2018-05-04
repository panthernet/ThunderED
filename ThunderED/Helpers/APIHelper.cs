using System.Threading.Tasks;
using ThunderED.API;

namespace ThunderED.Helpers
{
    public static class APIHelper
    {
        public static DiscordAPI DiscordAPI { get; private set; }
        public static ESIAPI ESIAPI { get; private set; }
        public static ZKillAPI ZKillAPI { get; private set; }
        public static FleetUpAPI FleetUpAPI { get; private set; }

        public static async Task Prepare()
        {
            DiscordAPI = new DiscordAPI();
            ESIAPI = new ESIAPI();
            ZKillAPI = new ZKillAPI();
            FleetUpAPI = new FleetUpAPI();

            await DiscordAPI.Start();
        }

        public static void PurgeCache()
        {
            ESIAPI.PurgeCache();
            DiscordAPI.PurgeCache();
            ZKillAPI.PurgeCache();
        }

        public static void ResetCache()
        {
            ESIAPI.ResetCache();
            DiscordAPI.ResetCache();
            ZKillAPI.ResetCache();
        }
    }
}
