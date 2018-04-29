using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.ZKill;

namespace ThunderED.API
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public partial class ZKillAPI: CacheBase
    {
        private readonly HttpClient _zKillhttpClient = new HttpClient();

        public ZKillAPI()
        {
            _zKillhttpClient.Timeout = new TimeSpan(0, 0, 10);
            _zKillhttpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
        }

        internal async Task<JsonZKill.ZKillboard> GetRedisqResponce()
        {
            try
            {
                var redisqID = SettingsManager.Get("killFeed", "reDisqID");
                var resp = await (await _zKillhttpClient.GetAsync(string.IsNullOrEmpty(redisqID)
                    ? "https://redisq.zkillboard.com/listen.php?ttw=1"
                    : $"https://redisq.zkillboard.com/listen.php?ttw=1&queueID={redisqID}")).Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<JsonZKill.ZKillboard>(resp);
            }
            catch (TaskCanceledException)
            {
                //skip
                return null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("GetRedisqResponce", ex, LogCat.ZKill);
                return null;
            }
        }

        internal async Task<List<JsonZKill.Kill>> GetCharacterKills(object characterId)
        {
            var responce = await _zKillhttpClient.GetAsync($"https://zkillboard.com/api/kills/characterID/{characterId}/");
            return responce.IsSuccessStatusCode ? JsonConvert.DeserializeObject<List<JsonZKill.Kill>>(await responce.Content.ReadAsStringAsync()) : null;
        }

        internal async Task<JsonZKill.CharacterStats> GetCharacterStats(object characterId)
        {
            var responce = await _zKillhttpClient.GetAsync($"https://zkillboard.com/api/stats/characterID/{characterId}/");
            return responce.IsSuccessStatusCode ? JsonConvert.DeserializeObject<JsonZKill.CharacterStats>(await responce.Content.ReadAsStringAsync()) : null;
        }

        
        internal async Task<List<JsonZKill.Kill>> GetCharacterLosses(object characterId)
        {
            var responce = await _zKillhttpClient.GetAsync($"https://zkillboard.com/api/losses/characterID/{characterId}/");
            return responce.IsSuccessStatusCode ? JsonConvert.DeserializeObject<List<JsonZKill.Kill>>(await responce.Content.ReadAsStringAsync()) : null;
        }

        internal async Task<List<JsonZKill.LightKill>> GetLightEntityFeed(bool isAlliance, int id)
        {
            string content = null;
            try
            {
                var eText = isAlliance ? "allianceID" : "corpID";
                var responce = await _zKillhttpClient.GetAsync($"https://zkillboard.com/api/{eText}/{id}/no-items/no-attackers/orderDirection/asc/");
                if (responce.IsSuccessStatusCode)
                {
                    content = await responce.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<JsonZKill.LightKill>>(content);
                }

                return null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.ZKill);
                await LogHelper.LogInfo($"GetLightEntityFeed RESPONCE: {content}", LogCat.ZKill);
                return null;
            }
        }

        internal async Task<List<JsonZKill.ZkillOnly>> GetZKillOnlyFeed(bool isAlliance, int id)
        {
            string content = null;
            try
            {
                var eText = isAlliance ? "allianceID" : "corpID";
                var responce = await _zKillhttpClient.GetAsync($"https://zkillboard.com/api/{eText}/{id}/zkbOnly/orderDirection/desc/");
                if (responce.IsSuccessStatusCode)
                {
                    content = await responce.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<JsonZKill.ZkillOnly>>(content);
                }

                return null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.ZKill);
                await LogHelper.LogInfo($"GetZKillOnlyFeed RESPONCE: {content}", LogCat.ZKill);
                return null;
            }
        }

        internal async Task<JsonZKill.Kill> GetLightEntityKill(int killmailID)
        {
            string content = null;
            try
            {
                var responce = await _zKillhttpClient.GetAsync($"https://zkillboard.com/api/killID/{killmailID}/");
                if (responce.IsSuccessStatusCode)
                {
                    content = await responce.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<List<JsonZKill.Kill>>(content)?.FirstOrDefault();
                }

                return null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, LogCat.ZKill);
                await LogHelper.LogInfo($"GetLightEntityKill RESPONCE: {content}", LogCat.ZKill);
                return null;
            }
        }
    }
}
