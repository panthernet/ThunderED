using System;
using System.Collections.Async;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;
using Discord.Net.WebSockets;
using Newtonsoft.Json;
using PureWebSockets;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.ZKill;

namespace ThunderED.API
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public partial class ZKillAPI: CacheBase, IDisposable
    {
        private readonly string _reason = LogCat.ZKill.ToString();
        //private readonly HttpClient _zKillhttpClient = new HttpClient();

        /*public ZKillAPI()
        {
            _zKillhttpClient.Timeout = new TimeSpan(0, 0, 10);
            _zKillhttpClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
        }*/

        private PureWebSocket _webSocket;

        private readonly ConcurrentQueue<JsonZKill.Killmail> _webMailsQueue = new ConcurrentQueue<JsonZKill.Killmail>();

        internal async Task<JsonZKill.Killmail> GetSocketResponce()
        {
            try
            {


                if (_webSocket == null || _webSocket.State != WebSocketState.Open)
                {
                    if (_webSocket?.State == WebSocketState.Connecting) return null;
                    var o = new PureWebSocketOptions();
                    _webSocket = new PureWebSocket("wss://zkillboard.com:2096", o);
                    _webSocket.OnMessage += _webSocket_OnMessage;

                    if (!_webSocket.Connect())
                    {
                        _webSocket.Dispose();
                        _webSocket = null;
                        return null;
                    }
                    else
                    {
                        if (!_webSocket.Send("{\"action\":\"sub\",\"channel\":\"killstream\"}"))
                        {
                            _webSocket?.Dispose();
                            _webSocket = null;
                            return null;
                        }
                    }

                }

                if (!_webMailsQueue.IsEmpty && _webMailsQueue.TryDequeue(out var km))
                    return km;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("GetSocketResponce", ex, LogCat.ZKill);

            }

            return null;
        }

        private async void _webSocket_OnMessage(string data)
        {
            try
            {
                var entry = JsonConvert.DeserializeObject<JsonZKill.Killmail>(data);
                _webMailsQueue.Enqueue(entry);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("_webSocket_OnData", ex, LogCat.ZKill);
            }
        }

        internal async Task<JsonZKill.ZKillboard> GetRedisqResponce()
        {
            var redisqID = SettingsManager.Settings.Config.ZkillLiveFeedRedisqID;
			var request = string.IsNullOrEmpty(redisqID)
                    ? "https://redisq.zkillboard.com/listen.php?ttw=1"
                    : $"https://redisq.zkillboard.com/listen.php?ttw=1&queueID={redisqID}";
			var data = await APIHelper.RequestWrapper<JsonZKill.ZKillboard>(request, _reason, null, true);
            if (data?.package == null)
            {
                await Task.Delay(1500);
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

        public async Task<JsonZKill.CorpStats> GetCorporationData(long id, bool nosupers)
        {
            if (nosupers)
            {
                var res =  await APIHelper.RequestWrapper<JsonZKill.CorpStatsNoSupers>($"https://zkillboard.com/api/stats/corporationID/{id}/", _reason);
                return new JsonZKill.CorpStats
                {
                    hasSupers = false,
                    topLists = res.topLists,
                    shipsDestroyed = res.shipsDestroyed,
                    allTimeSum = res.allTimeSum,
                    dangerRatio = res.dangerRatio,
                    gangRatio = res.gangRatio,
                    id = res.id,
                    info = res.info,
                    iskDestroyed = res.iskDestroyed,
                    iskLost = res.iskLost,
                    shipsLost = res.shipsLost,
                    soloKills = res.soloKills,
                    soloLosses = res.soloLosses,
                    topIskKillIDs = res.topIskKillIDs
                };
            }
            return await APIHelper.RequestWrapper<JsonZKill.CorpStats>($"https://zkillboard.com/api/stats/corporationID/{id}/", _reason);
        }

        public async Task<JsonZKill.CorpStats> GetLiteCorporationData(long id)
        {
            var res =  await APIHelper.RequestWrapper<JsonZKill.CorpStatsLite>($"https://zkillboard.com/api/stats/corporationID/{id}/", _reason);
            return new JsonZKill.CorpStats { hasSupers = res.hasSupers};
        }

        public void Dispose()
        {
            _webSocket?.Dispose();
        }
    }
}
