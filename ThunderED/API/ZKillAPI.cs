using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.ZKill;
using ThunderED.Zkb;

namespace ThunderED.API
{
    public class ZKillAPI: CacheBase
    {
        private readonly HttpClient _zKillhttpClient = new HttpClient();
        private static IRequestHandler RequestHandler { get; set; }

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
    }
}
