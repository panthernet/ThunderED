using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.API;
using ThunderED.Classes;

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


        public static async Task<T> RequestWrapper<T>(string request, string reason, string auth = null, bool noRetries = false, bool silent = false)
            where T : class
        {
            string raw = null;
            var retCount = SettingsManager.Settings.Config.RequestRetries;
            retCount = retCount == 0 || noRetries ? 1 : retCount;
            for (int i = 0; i < retCount; i++)
            {
                try
                {
                    var handler = new HttpClientHandler {AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate};
                    using (var httpClient = new HttpClient(handler))
                    {
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
                        httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
                        if (!string.IsNullOrEmpty(auth))
                            httpClient.DefaultRequestHeaders.Add("Authorization", auth);

                        using (var responceMessage = await httpClient.GetAsync(request))
                        {
                            raw = await responceMessage.Content.ReadAsStringAsync();
                            if (!responceMessage.IsSuccessStatusCode)
                            {
                                if (responceMessage.StatusCode != HttpStatusCode.NotFound && responceMessage.StatusCode != HttpStatusCode.Forbidden &&
                                    (responceMessage.StatusCode != HttpStatusCode.BadGateway && responceMessage.StatusCode != HttpStatusCode.GatewayTimeout) && !silent)
                                    await LogHelper.LogError($"[try: {i}][{reason}] Potential {responceMessage.StatusCode} request failure: {request}", LogCat.ESI, false);
                                if (raw.StartsWith("{\"error\""))
                                {
                                    await LogHelper.LogError($"[{reason}] Request failure: {request}\n{raw}", LogCat.ESI, false);
                                    return null;
                                }
                                continue;
                            }

                            if (typeof(T) == typeof(string))
                                return (T) (object) raw;

                            if (!typeof(T).IsClass)
                                return null;

                            var data = JsonConvert.DeserializeObject<T>(raw);
                            if (data == null)
                                await LogHelper.LogError($"[try: {i}][{reason}] Deserialized to null!{Environment.NewLine}Request: {request}", LogCat.ESI, false);
                            else return data;
                        }
                    }
                }
                catch (TaskCanceledException)
                {
                    //skip, probably due to timeout
                }
                catch (Exception ex)
                {
                    if (TickManager.IsNoConnection && request.StartsWith("https://esi.tech.ccp.is"))
                        return null;

                    if (!silent)
                    {
                        await LogHelper.LogEx(request, ex, LogCat.ESI);
                        await LogHelper.LogInfo($"[try: {i}][{reason}]{Environment.NewLine}REQUEST: {request}{Environment.NewLine}RESPONCE: {raw}", LogCat.ESI);
                    }
                }
            }

            return null;
        }
    }
}
