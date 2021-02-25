using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.API;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Json;

namespace ThunderED.Helpers
{
    public static class APIHelper
    {
        public static DiscordAPI DiscordAPI { get; private set; }
        public static ESIAPI ESIAPI { get; private set; }
        public static ZKillAPI ZKillAPI { get; private set; }
        public static FleetUpAPI FleetUpAPI { get; private set; }

        public static string GetItemTypeUrl(object id)
        {
            return $"https://everef.net/type/{id}";
        }

        private static CancellationTokenSource _token;

        public static bool IsDiscordAvailable => DiscordAPI != null && DiscordAPI.IsAvailable;


        public static void StopServices()
        {
            _token.Cancel();
        }

        private static void RunDiscordThread()
        {
            var thread = new Thread(async () =>
            {
                try
                {
                    DiscordAPI = new DiscordAPI();

                    while (!_token.IsCancellationRequested)
                    {
                        await Task.Delay(10);
                    }

                    DiscordAPI?.Stop();
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx("Discord Thread", ex, LogCat.Discord);
                    DiscordAPI?.Stop();
                    if(!_token.IsCancellationRequested)
                        RunDiscordThread();
                }
            });
            thread.Start();
        }

        public static void Prepare()
        {
            _token = new CancellationTokenSource();
            RunDiscordThread();
            ESIAPI = new ESIAPI();
            ZKillAPI = new ZKillAPI();
            FleetUpAPI = new FleetUpAPI();
        }

        public static void PurgeCache()
        {
            ESIAPI.PurgeCache();
            DiscordAPI?.PurgeCache();
            ZKillAPI.PurgeCache();
        }

        public static void ResetCache()
        {
            ESIAPI.ResetCache();
            DiscordAPI?.ResetCache();
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
                    await WaitReq.WaitIfNeeded(i);
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

                        using (var responseMessage = await httpClient.GetAsync(request))
                        {
                            result.Data.ETag = responseMessage.Headers.FirstOrDefault(a => a.Key == "ETag").Value?.FirstOrDefault().Trim('"');

                            raw = await responseMessage.Content.ReadAsStringAsync();
                            if (!responseMessage.IsSuccessStatusCode)
                            {
                                if (responseMessage.StatusCode == HttpStatusCode.NotFound && !request.Contains("/route/"))
                                    await LogHelper.LogWarning($"Query address is invalid: {request}", LogCat.ESI);
                                if (responseMessage.StatusCode == HttpStatusCode.Forbidden)
                                    await LogHelper.LogWarning($"Query address is forbidden: {request}", LogCat.ESIWarnings);
                                result.Data.ErrorCode = (int)responseMessage.StatusCode;
                                result.Data.Message = raw;
                                if (responseMessage.StatusCode != HttpStatusCode.NotModified && responseMessage.StatusCode != HttpStatusCode.NotFound && responseMessage.StatusCode != HttpStatusCode.Forbidden &&
                                    (responseMessage.StatusCode != HttpStatusCode.BadGateway && responseMessage.StatusCode != HttpStatusCode.GatewayTimeout) && !silent)
                                    await LogHelper.LogError($"[try: {i}][{reason}] Potential {responseMessage.StatusCode} request failure: {request}", LogCat.ESI, false);
                                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                                    return result;

                                var errParsed = JsonConvert.DeserializeObject<JsonClasses.ESIError>(raw);
                                if (errParsed != null)
                                {
                                    if(errParsed.timeout > 0)
                                        WaitReq.Update(errParsed.timeout);
                                    if(SettingsManager.Settings.Config.ExtendedESILogging)
                                        await LogHelper.LogError($"[{reason}][CODE:{result.Data.ErrorCode}] Request failure: {request}\nMessage: {errParsed.error}", LogCat.ESI, false);
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
                    result.Data.Message = "Task has been cancelled";
                    result.Data.ErrorCode = 1;
                    return result;
                }
                catch (Exception ex)
                {
                    if (TickManager.IsNoConnection && request.StartsWith(SettingsManager.Settings.Config.ESIAddress))
                    {
                        result.Data.Message = "No connection";
                        result.Data.IsNoConnection = true;
                        return result;
                    }

                    if (!silent)
                    {
                        await LogHelper.LogEx(request, ex, LogCat.ESI);
                        await LogHelper.LogInfo($"[try: {i}][{reason}]{Environment.NewLine}REQUEST: {request}{Environment.NewLine}RESPONSE: {raw}", LogCat.ESI);
                    }
                }
            }

            return result;
        }

        public static async Task<ESIQueryResult<T>> AggressiveESIRequestWrapper<T>(string request, string reason, int retCount, string auth = null, string etag = null)
            where T : class
        {
            string raw = null;
            retCount = retCount == 0 ? 1 : retCount;

            var result = new ESIQueryResult<T>();

            for (var i = 0; i < retCount; i++)
            {
                try
                {
                    await WaitReq.WaitIfNeeded(i);
                    var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
                    using (var httpClient = new HttpClient(handler))
                    {
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
                        httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
                        httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
                        if (!string.IsNullOrEmpty(auth))
                            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", auth);
                        if (!string.IsNullOrEmpty(etag))
                            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("if-none-match", etag);

                        var ct = new CancellationTokenSource(10000);
                        using (var responseMessage = await httpClient.GetAsync(request, ct.Token))
                        {
                            result.Data.ETag = responseMessage.Headers.FirstOrDefault(a => a.Key == "ETag").Value?.FirstOrDefault().Trim('"');

                            raw = await responseMessage.Content.ReadAsStringAsync();
                            if (!responseMessage.IsSuccessStatusCode)
                            {
                                if (responseMessage.StatusCode == HttpStatusCode.NotFound && !request.Contains("/route/"))
                                    await LogHelper.LogWarning($"Query address is invalid: {request}", LogCat.ESI);
                                if (responseMessage.StatusCode == HttpStatusCode.Forbidden)
                                    await LogHelper.LogWarning($"Query address is forbidden: {request}", LogCat.ESIWarnings);
                                result.Data.ErrorCode = (int)responseMessage.StatusCode;
                                result.Data.Message = raw;
                                if (responseMessage.StatusCode != HttpStatusCode.NotModified && responseMessage.StatusCode != HttpStatusCode.NotFound && responseMessage.StatusCode != HttpStatusCode.Forbidden &&
                                    (responseMessage.StatusCode != HttpStatusCode.BadGateway && responseMessage.StatusCode != HttpStatusCode.GatewayTimeout))
                                    await LogHelper.LogError($"[try: {i}][{reason}] AGG Potential {responseMessage.StatusCode} request failure: {request}", LogCat.ESI, false);
                                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                                    return result;

                                var errParsed = JsonConvert.DeserializeObject<JsonClasses.ESIError>(raw);
                                if (errParsed != null)
                                {
                                    if (errParsed.timeout > 0)
                                    {
                                        WaitReq.Update(errParsed.timeout);
                                        continue; //got a timeout, should retry
                                    }

                                    if (SettingsManager.Settings.Config.ExtendedESILogging)
                                        await LogHelper.LogError($"[{reason}] AGG Request failure: {request}\nMessage: {errParsed.error}", LogCat.ESI, false);
                                    //get out, error is known
                                    return result;
                                }
                                else
                                {
                                    await LogHelper.LogError($"[{reason}] RAW failure: {request}\nMessage: {raw}", LogCat.ESI, false);
                                    continue;
                                }
                            }

                            if (typeof(T) == typeof(string))
                            {
                                result.Result = (T)(object)raw;
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
                                await LogHelper.LogError($"[try: {i}][{reason}] AGG Deserialized to null!{Environment.NewLine}Request: {request}", LogCat.ESI, false);
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
                    result.Data.Message = "Task has been cancelled";
                    result.Data.ErrorCode = 1;
                    return result;
                }
                catch (Exception ex)
                {
                    if (TickManager.IsNoConnection && request.StartsWith(SettingsManager.Settings.Config.ESIAddress))
                    {
                        result.Data.Message = "No connection";
                        result.Data.IsNoConnection = true;
                        return result;
                    }

                    await LogHelper.LogEx(request, ex, LogCat.ESI);
                    await LogHelper.LogInfo($"[try: {i}][{reason}]AGG {Environment.NewLine}REQUEST: {request}{Environment.NewLine}RESPONSE: {raw}", LogCat.ESI);
                }
            }

            return result;
        }

        private static readonly WaitRequestData WaitReq = new WaitRequestData();

        public static async Task<T> RequestWrapper<T>(string request, string reason, string auth = null, string eToken = null, bool noRetries = false, bool silent = false, string encoding = null)
            where T : class
        {
            string raw = null;
            var retCount = SettingsManager.Settings.Config.RequestRetries;
            retCount = retCount == 0 || noRetries ? 1 : retCount;
            for (int i = 0; i < retCount; i++)
            {
                try
                {
                    await WaitReq.WaitIfNeeded(i);
                    var handler = new HttpClientHandler {AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate};
                    using (var httpClient = new HttpClient(handler))
                    {
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
                        if(encoding == null)
                            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");                        
                        else if(!string.IsNullOrEmpty(encoding))
                            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", encoding);
                        if (!string.IsNullOrEmpty(auth))
                            httpClient.DefaultRequestHeaders.Add("Authorization", auth);
                        if(!string.IsNullOrEmpty(eToken))
                            httpClient.DefaultRequestHeaders.Add("Etoken", eToken);

                        var ct = new CancellationTokenSource(5000);

                        using (var responseMessage = await httpClient.GetAsync(request, ct.Token))
                        {
                            raw = await responseMessage.Content.ReadAsStringAsync();
                            if (responseMessage.StatusCode == HttpStatusCode.NotFound && !request.Contains("/route/"))
                                await LogHelper.LogWarning($"Query address is invalid: {request}", LogCat.ESIWarnings);
                            if (responseMessage.StatusCode == HttpStatusCode.Forbidden)
                                await LogHelper.LogWarning($"Query address is forbidden: {request}", LogCat.ESIWarnings);
                            if (responseMessage.Content.Headers.ContentEncoding.Any(a=> "br".Equals(a, StringComparison.OrdinalIgnoreCase)))
                            {
                                using (var b = new BrotliStream(await responseMessage.Content.ReadAsStreamAsync(), CompressionMode.Decompress, true))
                                {
                                    using (var s = new StreamReader(b))
                                        raw = await s.ReadToEndAsync();
                                }
                            }
                            if (!responseMessage.IsSuccessStatusCode)
                            {
                                if (responseMessage.StatusCode == HttpStatusCode.NotModified)
                                    return null;
                                if (responseMessage.StatusCode != HttpStatusCode.NotFound && responseMessage.StatusCode != HttpStatusCode.Forbidden &&
                                    (responseMessage.StatusCode != HttpStatusCode.BadGateway && responseMessage.StatusCode != HttpStatusCode.GatewayTimeout) && !silent)
                                    await LogHelper.LogError($"[try: {i}][{reason}] Potential {responseMessage.StatusCode} request failure: {request}", LogCat.ESI, false);
                                var errParsed = JsonConvert.DeserializeObject<JsonClasses.ESIError>(raw);
                                if (errParsed != null)
                                {
                                    if(errParsed.timeout > 0)
                                        WaitReq.Update(errParsed.timeout);
                                    if(SettingsManager.Settings.Config.ExtendedESILogging)
                                        await LogHelper.LogError($"[{reason}] Request failure: {request}\nMessage: {errParsed.error}", LogCat.ESI, false);
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
                    if (TickManager.IsNoConnection && request.StartsWith(SettingsManager.Settings.Config.ESIAddress))
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

                        var ct = new CancellationTokenSource(5000);
                        using (var responceMessage = await httpClient.PostAsync(request, content, ct.Token))
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
                    if (TickManager.IsNoConnection && request.StartsWith(SettingsManager.Settings.Config.ESIAddress))
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

    internal class WaitRequestData
    {
        private DateTime _from = DateTime.Now;
        private volatile int _waitSeconds;
        private readonly object _locker = new object();

        public void Update(int seconds)
        {
            lock (_locker)
            {
                _from = DateTime.Now;
                _waitSeconds = seconds;
            }
        }

        public async Task WaitIfNeeded(int retryNum)
        {
            var value = GetWaitInMsec();
            if (value > 0)
                await Task.Delay(value * 1000);
            else if(retryNum > 0) 
                await Task.Delay(100);
        }

        private int GetWaitInMsec()
        {
            if (_waitSeconds == 0) return 0;
            lock (_locker)
            {
                var i = (DateTime.Now - _from).Seconds;
                if (i >= _waitSeconds)
                {
                    _waitSeconds = 0;
                    return 0;
                }

                return _waitSeconds - i;
            }
        }
    }
}
