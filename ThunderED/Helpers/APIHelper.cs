using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.API;
using ThunderED.Classes;
using ThunderED.Classes.Entities;

namespace ThunderED.Helpers
{
    public static class APIHelper
    {
        public static DiscordAPI DiscordAPI { get; private set; }
        public static ESIAPI ESIAPI { get; private set; }
        public static ZKillAPI ZKillAPI { get; private set; }
        public static FleetUpAPI FleetUpAPI { get; private set; }

        public static void Prepare()
        {
            DiscordAPI = new DiscordAPI();
            ESIAPI = new ESIAPI();
            ZKillAPI = new ZKillAPI();
            FleetUpAPI = new FleetUpAPI();
        }

        public static async Task StartDiscord()
        {
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

        public static async Task<ESIQueryResult<T>> ESIRequestWrapper<T>(string request, string reason, string auth = null, string etag = null, bool noRetries = false, bool silent = false)
            where T : class
        {
            string raw = null;
            var retCount = SettingsManager.Settings.Config.RequestRetries;
            retCount = retCount == 0 || noRetries ? 1 : retCount;

            var result = new ESIQueryResult<T>();

            for (int i = 0; i < retCount; i++)
            {
                try
                {
                    var handler = new HttpClientHandler {AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate};
                    using (var httpClient = new HttpClient(handler))
                    {
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
                        httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                        httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                        if (!string.IsNullOrEmpty(auth))
                            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth);
                        if(!string.IsNullOrEmpty(etag))
                            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("if-none-match", etag);

                        using (var responceMessage = await httpClient.GetAsync(request))
                        {
                            result.Data.ETag = responceMessage.Headers.FirstOrDefault(a => a.Key == "ETag").Value?.FirstOrDefault().Trim('"');

                            raw = await responceMessage.Content.ReadAsStringAsync();
                            if (!responceMessage.IsSuccessStatusCode)
                            {
                                result.Data.ErrorCode = (int)responceMessage.StatusCode;
                                result.Data.Message = raw;
                                if (responceMessage.StatusCode != HttpStatusCode.NotModified && responceMessage.StatusCode != HttpStatusCode.NotFound && responceMessage.StatusCode != HttpStatusCode.Forbidden &&
                                    (responceMessage.StatusCode != HttpStatusCode.BadGateway && responceMessage.StatusCode != HttpStatusCode.GatewayTimeout) && !silent)
                                    await LogHelper.LogError($"[try: {i}][{reason}] Potential {responceMessage.StatusCode} request failure: {request}", LogCat.ESI, false);
                                if (responceMessage.StatusCode == HttpStatusCode.NotModified)
                                    return result;

                                if (raw.StartsWith("{\"error\""))
                                {
                                    if(SettingsManager.Settings.Config.ExtendedESILogging)
                                        await LogHelper.LogError($"[{reason}] Request failure: {request}\n{raw}", LogCat.ESI, false);
                                    return result;
                                }
                                continue;
                            }

                            if (typeof(T) == typeof(string))
                            {
                                result.Result = (T) (object) raw;
                                return result;
                            }

                            if (!typeof(T).IsClass)
                            {
                                result.Data.Message = "Is not a class T!";
                                return result;
                            }

                            var data = JsonConvert.DeserializeObject<T>(raw);
                            if (data == null)
                            {
                                result.Data.Message = "Failed to deserialize!";
                                result.Data.ErrorCode = -100;
                                await LogHelper.LogError($"[try: {i}][{reason}] Deserialized to null!{Environment.NewLine}Request: {request}", LogCat.ESI, false);
                            }
                            else
                            {
                                result.Result = data;
                                return result;
                            }
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
                    {
                        result.Data.Message = "No connection";
                        result.Data.IsNoConnection = true;
                        return result;
                    }

                    if (!silent)
                    {
                        await LogHelper.LogEx(request, ex, LogCat.ESI);
                        await LogHelper.LogInfo($"[try: {i}][{reason}]{Environment.NewLine}REQUEST: {request}{Environment.NewLine}RESPONCE: {raw}", LogCat.ESI);
                    }
                }
            }

            return result;
        }

        public static async Task<T> RequestWrapper<T>(string request, string reason, string auth = null, string etoken = null, bool noRetries = false, bool silent = false)
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
                        httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                        if (!string.IsNullOrEmpty(auth))
                            httpClient.DefaultRequestHeaders.Add("Authorization", auth);
                        if(!string.IsNullOrEmpty(etoken))
                            httpClient.DefaultRequestHeaders.Add("Etoken", etoken);

                        using (var responceMessage = await httpClient.GetAsync(request))
                        {
                            raw = await responceMessage.Content.ReadAsStringAsync();
                            if (!responceMessage.IsSuccessStatusCode)
                            {
                                if (responceMessage.StatusCode != HttpStatusCode.NotModified && responceMessage.StatusCode != HttpStatusCode.NotFound && responceMessage.StatusCode != HttpStatusCode.Forbidden &&
                                    (responceMessage.StatusCode != HttpStatusCode.BadGateway && responceMessage.StatusCode != HttpStatusCode.GatewayTimeout) && !silent)
                                    await LogHelper.LogError($"[try: {i}][{reason}] Potential {responceMessage.StatusCode} request failure: {request}", LogCat.ESI, false);
                                if (responceMessage.StatusCode == HttpStatusCode.NotModified)
                                    return null;
                                if (raw.StartsWith("{\"error\""))
                                {
                                    if(SettingsManager.Settings.Config.ExtendedESILogging)
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

        public static async Task<bool> PostWrapper(string request, FormUrlEncodedContent content, string reason, string auth, bool noRetries = false, bool silent = false)
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
                        if (!string.IsNullOrEmpty(auth))
                            httpClient.DefaultRequestHeaders.Add("Authorization", auth);
                        httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                        httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

                        using (var responceMessage = await httpClient.PostAsync(request, content))
                        {
                            raw = await responceMessage.Content.ReadAsStringAsync();
                            if (!responceMessage.IsSuccessStatusCode)
                            {
                                if (responceMessage.StatusCode != HttpStatusCode.NotFound && responceMessage.StatusCode != HttpStatusCode.Forbidden &&
                                    (responceMessage.StatusCode != HttpStatusCode.BadGateway && responceMessage.StatusCode != HttpStatusCode.GatewayTimeout) && !silent)
                                    await LogHelper.LogError($"[try: {i}][{reason}] Potential {responceMessage.StatusCode} request failure: {request}", LogCat.ESI, false);

                                if (raw.StartsWith("{\"error\""))
                                {
                                    if(SettingsManager.Settings.Config.ExtendedESILogging)
                                        await LogHelper.LogError($"[{reason}] Request failure: {request}\n{raw}", LogCat.ESI, false);
                                    return false;
                                }
                                continue;
                            }

                            return true;
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
                        return false;

                    if (!silent)
                    {
                        await LogHelper.LogEx(request, ex, LogCat.ESI);
                        await LogHelper.LogInfo($"[try: {i}][{reason}]{Environment.NewLine}REQUEST: {request}{Environment.NewLine}RESPONCE: {raw}", LogCat.ESI);
                    }
                }

            }
            return false;
        }
    }
}
