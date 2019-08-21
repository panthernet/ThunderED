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
                    _webSocket?.Dispose();
                    _webSocket = new PureWebSocket(SettingsManager.Settings.ZKBSettingsModule.ZKillboardWebSocketUrl, o);
                    _webSocket.OnMessage += WebSocket_OnMessage;
                    _webSocket.OnError += async (sender, exception) => { await LogHelper.LogEx("WebSocket.OnError", exception, LogCat.ZKill); };

                    if (!_webSocket.Connect())
                    {
                        _webSocket?.Dispose();
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

                        await LogHelper.LogInfo("ZKB feed core WebSocket connect successful!", LogCat.ZKill);
                    }

                }

                if (!_webMailsQueue.IsEmpty && _webMailsQueue.TryDequeue(out var km))
                    return km;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("GetSocketResponce", ex, LogCat.ZKill);
                _webSocket?.Dispose();
                _webSocket = null;
            }

            return null;
        }

        private async void WebSocket_OnMessage(object sender, string message)
        {
            try
            {
                var entry = JsonConvert.DeserializeObject<JsonZKill.Killmail>(message);
                _webMailsQueue.Enqueue(entry);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("_webSocket_OnData", ex, LogCat.ZKill);
            }
        }

        internal async Task<JsonZKill.ZKillboard> GetRedisqResponce()
        {
            var redisqID = SettingsManager.Settings.ZKBSettingsModule.ZkillLiveFeedRedisqID;
			var request = string.IsNullOrEmpty(redisqID)
                    ? "https://redisq.zkillboard.com/listen.php?ttw=1"
                    : $"https://redisq.zkillboard.com/listen.php?ttw=1&queueID={redisqID}";
			var data = await APIHelper.RequestWrapper<JsonZKill.ZKillboard>(request, _reason, null, null, true);
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
			var zKills = await APIHelper.RequestWrapper<List<JsonZKill.ZkillOnly>>($"https://zkillboard.com/api/kills/characterID/{characterId}/page/1/", _reason);
            var list = new ConcurrentBag<JsonClasses.ESIKill>();
            var q = zKills.Count > 20 ? zKills.TakeSmart(20) : zKills;

            await q.ParallelForEachAsync(async z =>
            {
                var kill = await APIHelper.ESIAPI.GetKillmail(_reason, z.killmail_id, z.zkb.hash);
                list.Add(kill);
            },  SettingsManager.MaxConcurrentThreads);

            return list.OrderByDescending(a=> a.killmail_id).ToList();
        }

        internal async Task<JsonZKill.CharacterStats> GetCharacterStats(object characterId)
        {
			return await APIHelper.RequestWrapper<JsonZKill.CharacterStats>($"https://zkillboard.com/api/stats/characterID/{characterId}/", _reason);
        }

        
        internal async Task<List<JsonClasses.ESIKill>> GetCharacterLosses(object characterId)
        {
			var zLosses =  await APIHelper.RequestWrapper<List<JsonZKill.ZkillOnly>>($"https://zkillboard.com/api/losses/characterID/{characterId}/page/1/", _reason);
            var list = new List<JsonClasses.ESIKill>();
            var q = zLosses.Count > 20 ? zLosses.TakeSmart(20) : zLosses;

            await q.ParallelForEachAsync(async z =>
            {
                var kill = await APIHelper.ESIAPI.GetKillmail(_reason, z.killmail_id, z.zkb.hash);
                list.Add(kill);
            },  SettingsManager.MaxConcurrentThreads);

            return list.OrderByDescending(a=> a.killmail_id).ToList();
        }

        internal async Task<List<JsonZKill.ZkillOnly>> GetZKillOnlyFeed(bool isAlliance, int id)
        {			
            var eText = isAlliance ? "allianceID" : "corporationID";
			return await APIHelper.RequestWrapper<List<JsonZKill.ZkillOnly>>($"https://zkillboard.com/api/{eText}/{id}/zkbOnly/orderDirection/desc/pastSeconds/3600/", _reason);
		
        }

        public class ZkillEntityStats
        {
            public long IskDestroyed;
            public long IskLost;
            public long PointsDestroyed;
            public long PointsLost;
            public int ShipsDestroyed;
            public int ShipsLost;
            public int SoloKills;
            public string MostSystems;
            public string EntityName;
        }

        public async Task<ZkillEntityStats> GetKillsLossesStats(long id, bool isAlliance, DateTime? from = null, DateTime? to = null, int lastSeconds = 0)
        {
            try
            {
                var txt = isAlliance ? "allianceID" : "corporationID";
                int maxPerPage = 200;
                var page = 1;
                var killsList = new List<JsonZKill.ZkillOnly>();
                var query = $"https://zkillboard.com/api/kills/{txt}/{id}/npc/0/page/{{0}}";
                var sysList = new List<long>();
                if (lastSeconds > 0)
                    query += $"/pastSeconds/{lastSeconds}/";
                else if (from.HasValue)
                {
                    from = from.Value.Subtract(TimeSpan.FromMinutes(from.Value.Minute));
                    query += $"/startTime/{from.Value:yyyyMMddHHmm}/";
                    to = to ?? DateTime.UtcNow;
                    to = to.Value.Subtract(TimeSpan.FromMinutes(to.Value.Minute));
                    query += $"endTime/{to.Value:yyyyMMddHHmm}/";
                }

                if (query.Last() != '/')
                    query += "/";

                while (true)
                {
                    var res = await APIHelper.RequestWrapper<List<JsonZKill.ZkillOnly>>(string.Format(query, page), _reason);

                    if (res != null)
                        killsList.AddRange(res);
                    if (res == null || res.Count == 0 || res.Count < maxPerPage) break;
                    page++;
                }

                var lossList = new List<JsonZKill.ZkillOnly>();
                query = $"https://zkillboard.com/api/losses/{txt}/{id}/npc/0/page/{{0}}";
                if (lastSeconds > 0)
                    query += $"/pastSeconds/{lastSeconds}/";
                else if (from.HasValue)
                {
                    from = from.Value.Subtract(TimeSpan.FromMinutes(from.Value.Minute));
                    query += $"/startTime/{from.Value:yyyyMMddHHmm}/";
                    to = to ?? DateTime.UtcNow;
                    to = to.Value.Subtract(TimeSpan.FromMinutes(to.Value.Minute));
                    query += $"endTime/{to.Value:yyyyMMddHHmm}/";
                }

                if (query.Last() != '/')
                    query += "/";
                page = 1;
                while (true)
                {

                    var res = await APIHelper.RequestWrapper<List<JsonZKill.ZkillOnly>>(string.Format(query, page), _reason);

                    if (res != null)
                        lossList.AddRange(res);
                    if (res == null || res.Count == 0 || res.Count < maxPerPage) break;
                    page++;
                }

                var idList = killsList.Select(a => a.zkb.locationID).ToList();
                idList.AddRange(lossList.Select(a=> a.zkb.locationID));
                var most = idList.Count > 0 ? idList.GroupBy(i=>i).OrderByDescending(grp=>grp.Count())
                    .Select(grp=>grp.Key).First() : 0;

                var system = most > 0 ? await APIHelper.ESIAPI.GetSystemData(LogCat.Stats.ToString(), most) : null;
                var pl = await APIHelper.ESIAPI.GetPlanet("", most);
                var name = system?.name ?? pl?.name;
                if (!string.IsNullOrEmpty(name))
                {
                    if (name.StartsWith("Stargate"))
                        name = name.Replace("Stargate", "").Replace("(", "").Replace(")", "").Trim();
                    else name = name.Split(' ').FirstOrDefault();
                }

                var r = new ZkillEntityStats
                {
                    IskDestroyed = (long) killsList.Sum(a => a.zkb.totalValue),
                    PointsDestroyed = killsList.Sum(a => a.zkb.points),
                    IskLost = (long) lossList.Sum(a => a.zkb.totalValue),
                    PointsLost = lossList.Sum(a => a.zkb.points),
                    ShipsDestroyed = killsList.Count,
                    ShipsLost = lossList.Count,
                    SoloKills = killsList.Count(a=> a.zkb.solo),
                    MostSystems = name

                };

                return r;

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(GetKillsLossesStats), ex, LogCat.Stats);
                return new ZkillEntityStats();
            }
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
