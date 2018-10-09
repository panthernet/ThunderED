using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.ZKill;

namespace ThunderED.API
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public partial class ZKillAPI: CacheBase
    {
        private readonly string _reason = LogCat.ZKill.ToString();
        //private readonly HttpClient _zKillhttpClient = new HttpClient();

        /*public ZKillAPI()
        {
            _zKillhttpClient.Timeout = new TimeSpan(0, 0, 10);
            _zKillhttpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
        }*/

        internal async Task<JsonZKill.ZKillboard> GetRedisqResponce()
        {
            var redisqID = SettingsManager.Settings.Config.ZkillLiveFeedRedisqID;
			var request = string.IsNullOrEmpty(redisqID)
                    ? "https://redisq.zkillboard.com/listen.php?ttw=1"
                    : $"https://redisq.zkillboard.com/listen.php?ttw=1&queueID={redisqID}";
			var data = await APIHelper.RequestWrapper<JsonZKill.ZKillboard>(request, _reason, null, true);
            if (data?.package == null)
            {
                await Task.Delay(3000);
                //skip
            }
				//await LogHelper.LogError("[GetRedisqResponce] Null data!", LogCat.ZKill, false);
			return data;
        }

        internal async Task<List<JsonClasses.ESIKill>> GetCharacterKills(object characterId)
        {
			var zKills = await APIHelper.RequestWrapper<List<JsonZKill.ZkillOnly>>($"https://zkillboard.com/api/kills/characterID/{characterId}/", _reason);
            var list = new ConcurrentBag<JsonClasses.ESIKill>();
            var q = zKills.Count > 20 ? zKills.TakeLast(20) : zKills;

            await q.ParallelForEachAsync(async z =>
            {
                var kill = await APIHelper.ESIAPI.GetKillmail(_reason, z.killmail_id, z.zkb.hash);
                list.Add(kill);
            }, 5);

            return list.ToList();
        }

        internal async Task<JsonZKill.CharacterStats> GetCharacterStats(object characterId)
        {
			return await APIHelper.RequestWrapper<JsonZKill.CharacterStats>($"https://zkillboard.com/api/stats/characterID/{characterId}/", _reason);
        }

        
        internal async Task<List<JsonClasses.ESIKill>> GetCharacterLosses(object characterId)
        {
			var zLosses =  await APIHelper.RequestWrapper<List<JsonZKill.ZkillOnly>>($"https://zkillboard.com/api/losses/characterID/{characterId}/", _reason);
            var list = new List<JsonClasses.ESIKill>();
            var q = zLosses.Count > 20 ? zLosses.TakeLast(20) : zLosses;

            await q.ParallelForEachAsync(async z =>
            {
                var kill = await APIHelper.ESIAPI.GetKillmail(_reason, z.killmail_id, z.zkb.hash);
                list.Add(kill);
            }, 5);

            return list.ToList();
        }

        internal async Task<List<JsonZKill.ZkillOnly>> GetZKillOnlyFeed(bool isAlliance, int id)
        {			
            var eText = isAlliance ? "allianceID" : "corporationID";
			return await APIHelper.RequestWrapper<List<JsonZKill.ZkillOnly>>($"https://zkillboard.com/api/{eText}/{id}/zkbOnly/orderDirection/desc/pastSeconds/3600/", _reason);
		
        }

    }
}
