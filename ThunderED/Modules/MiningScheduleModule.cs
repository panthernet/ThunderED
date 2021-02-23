using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderED.API;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.Sub;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public class MiningScheduleModule: AppModuleBase
    {
        public override LogCat Category => LogCat.MiningSchedule;

        protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedFeedMembersLists = new Dictionary<string, Dictionary<string, List<long>>>();
        protected readonly Dictionary<string, Dictionary<string, List<long>>> ParsedAccessMembersLists = new Dictionary<string, Dictionary<string, List<long>>>();

        public override async Task Initialize()
        {
            if (WebServerModule.WebModuleConnectors.ContainsKey(Reason))
                WebServerModule.WebModuleConnectors.Remove(Reason);
            WebServerModule.WebModuleConnectors.Add(Reason, ProcessRequest);

            ParsedFeedMembersLists.Clear();
            ParsedAccessMembersLists.Clear();

            var data = new Dictionary<string, List<object>> { { "general", Settings.MiningScheduleModule.AccessEntities } };
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, ParsedAccessMembersLists);
            data = new Dictionary<string, List<object>> { { "general", Settings.MiningScheduleModule.FeedEntities } };
            await ParseMixedDataArray(data, MixedParseModeEnum.Member, ParsedFeedMembersLists);
        }

        public override Task Run(object prm)
        {
            return base.Run(prm);
        }

        public async Task<WebQueryResult> ProcessRequest(string query, CallbackTypeEnum type, string ip, WebAuthUserData data)
        {
            if (!Settings.Config.ModuleMiningSchedule)
                return WebQueryResult.False;

            try
            {
                RunningRequestCount++;
                if (!query.Contains("&state=ms"))
                    return WebQueryResult.False;

                var prms = query.TrimStart('?').Split('&');
                var code = prms[0].Split('=')[1];

                var result = await WebAuthModule.GetCharacterIdFromCode(code,
                    Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                if (result == null)
                    return WebQueryResult.EsiFailure;

                var characterId = result[0];
                var numericCharId = Convert.ToInt64(characterId);

                if (string.IsNullOrEmpty(characterId) || string.IsNullOrEmpty(result[1]))
                {
                    await LogHelper.LogWarning("Bad or outdated feed request!", Category);
                    var r = WebQueryResult.BadRequestToSystemAuth;
                    r.Message1 = LM.Get("authTokenBodyFail");
                    r.Message2 = LM.Get("authTokenBadRequest");
                    return r;
                }

                var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                if(rChar == null)
                    return WebQueryResult.EsiFailure;

                if (!HasAuthAccess(rChar))
                {
                    await LogHelper.LogWarning($"Unauthorized feed request from {characterId}", Category);
                    var r = WebQueryResult.BadRequestToSystemAuth;
                    r.Message1 = LM.Get("authTokenBodyFail");
                    return r;
                }

                await DbHelper.UpdateToken(result[1], numericCharId, TokenEnum.MiningSchedule);
                await LogHelper.LogInfo($"Feed added for character: {characterId}", Category);

                var res = WebQueryResult.FeedAuthSuccess;
                res.Message1 = LM.Get("msAuthSuccessHeader");
                res.Message2 = LM.Get("msAuthSuccessBody");
                return res;

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                return WebQueryResult.False;
            }
            finally
            {
                RunningRequestCount--;
            }

        }

        public async Task<WebMiningExtractionResult> GetExtractions()
        {
            var result = new WebMiningExtractionResult();
            var tokens = await DbHelper.GetTokens(TokenEnum.MiningSchedule);

            var processedCorps = new List<long>();

            foreach (var token in tokens)
            {
                var r = await APIHelper.ESIAPI.RefreshToken(token.Token, Settings.WebServerModule.CcpAppClientId,
                    Settings.WebServerModule.CcpAppSecret);
                if (r == null || r.Data.IsFailed)
                {
                    await LogHelper.LogWarning($"Failed to refresh mining token from {token.CharacterId}");
                    if (r?.Data.IsNotValid ?? false)
                    {
                        await DbHelper.DeleteToken(token.CharacterId, TokenEnum.MiningSchedule);
                        await LogHelper.LogWarning($"Mining token from {token.CharacterId} is no longer valid and will be deleted!");
                    }
                    continue;
                }

                var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, token.CharacterId, true);
                if (rChar == null)
                {
                    await LogHelper.LogWarning($"Failed to refresh character {token.CharacterId}");
                    continue;
                }
                var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar.corporation_id);
                if (corp == null)
                {
                    await LogHelper.LogWarning($"Failed to refresh corp {rChar.corporation_id}");
                    continue;
                }
                if(processedCorps.Contains(rChar.corporation_id))
                    continue;

                processedCorps.Add(rChar.corporation_id);
                result.Corporations.Add(corp.name);

                var extr = await APIHelper.ESIAPI.GetCorpMiningExtractions(Reason, rChar.corporation_id, r.Result);
                var innerList = new List<WebMiningExtraction>();
               // var structures = await APIHelper.ESIAPI.GetCorpStructures(Reason, rChar.corporation_id, token.Token);

                foreach (var e in extr)
                {
                    var structure =
                        await APIHelper.ESIAPI.GetUniverseStructureData(Reason, e.structure_id, r.Result);

                    //var moon = await APIHelper.ESIAPI.GetMoon(Reason, e.moon_id);
                    var item = new WebMiningExtraction
                    {
                        ExtractionStartTime = e.extraction_start_time.ToEveTime(),
                        ChunkArrivalTime = e.chunk_arrival_time.ToEveTime(),
                        NaturalDecayTime = e.natural_decay_time.ToEveTime(),
                        //MoonId = e.moon_id,
                        StructureId = e.structure_id,
                        StructureName = structure?.name ?? LM.Get("Unknown"),
                        //MoonName = moon?.name ?? LM.Get("Unknown"),
                        CorporationName = corp.name,
                        
                    };
                    item.Remains = item.ChunkArrivalTime.GetRemains(LM.Get("timerRemains"));

                    var notify = await DbHelper.GetMiningNotification(e.structure_id, item.NaturalDecayTime);
                    if (notify != null)
                    {
                        item.OreComposition = notify.OreComposition;
                        item.Operator = notify.Operator;
                    }

                    innerList.Add(item);
                }
                result.Extractions.AddRange(innerList);
            }

            result.Corporations = result.Corporations.OrderBy(a => a).ToList();
            result.Extractions = result.Extractions.OrderBy(a => a.ChunkArrivalTime).ToList();
            return result;
        }

        public class WebMiningExtractionResult
        {
            public List<WebMiningExtraction> Extractions { get; set; } = new List<WebMiningExtraction>();
            public List<string> Corporations { get; set; } = new List<string>();
        }

        public static async Task UpdateNotificationFromFeed(string composition, long structureId, DateTime date, string op)
        {
            if(!SettingsManager.Settings.Config.ModuleMiningSchedule) return;

            await DbHelper.UpdateMiningNotification(new ThdMiningNotification
            {
                CitadelId = structureId, Operator = op, OreComposition = composition, Date = date
            });

        }

        #region Access checks

        public static bool HasViewAccess(in JsonClasses.CharacterData data)
        {
            if (data == null) return false;
            return HasViewAccess(data.character_id, data.corporation_id, data.alliance_id ?? 0);
        }

        public static bool HasViewAccess(WebAuthUserData data)
        {
            if (data == null) return false;
            return HasViewAccess(data.Id, data.CorpId, data.AllianceId);
        }

        private static bool HasViewAccess(long id, long corpId, long allianceId)
        {
            if (!SettingsManager.Settings.Config.ModuleMiningSchedule) return false;
            var module = TickManager.GetModule<MiningScheduleModule>();

            return module.GetAccessAllCharacterIds().Contains(id) ||
                   module.GetAccessAllCorporationIds().Contains(corpId) || (allianceId > 0 &&
                                                                            module.GetAccessAllAllianceIds().Contains(allianceId));
        }

        public List<long> GetAccessAllCharacterIds()
        {
            return ParsedAccessMembersLists.Where(a => a.Value.ContainsKey("character")).SelectMany(a => a.Value["character"]).Distinct().Where(a => a > 0).ToList();
        }
        public List<long> GetAccessAllCorporationIds()
        {
            return ParsedAccessMembersLists.Where(a => a.Value.ContainsKey("corporation")).SelectMany(a => a.Value["corporation"]).Distinct().Where(a => a > 0).ToList();
        }

        public List<long> GetAccessAllAllianceIds()
        {
            return ParsedAccessMembersLists.Where(a => a.Value.ContainsKey("alliance")).SelectMany(a => a.Value["alliance"]).Distinct().Where(a => a > 0).ToList();
        }
        #endregion

        #region Feed/Auth checks

        public static bool HasAuthAccess(in JsonClasses.CharacterData data)
        {
            if (data == null) return false;
            return HasAuthAccess(data.character_id, data.corporation_id, data.alliance_id ?? 0);
        }

        public static bool HasAuthAccess(WebAuthUserData data)
        {
            if (data == null) return false;
            return HasAuthAccess(data.Id, data.CorpId, data.AllianceId);
        }

        private static bool HasAuthAccess(long id, long corpId, long allianceId)
        {
            if (!SettingsManager.Settings.Config.ModuleMiningSchedule) return false;
            var module = TickManager.GetModule<MiningScheduleModule>();

            return module.GetFeedAllCharacterIds().Contains(id) ||
                   module.GetFeedAllCorporationIds().Contains(corpId) || (allianceId > 0 &&
                                                                            module.GetFeedAllAllianceIds().Contains(allianceId));
        }

        public List<long> GetFeedAllCharacterIds()
        {
            return ParsedFeedMembersLists.Where(a => a.Value.ContainsKey("character")).SelectMany(a => a.Value["character"]).Distinct().Where(a => a > 0).ToList();
        }
        public List<long> GetFeedAllCorporationIds()
        {
            return ParsedFeedMembersLists.Where(a => a.Value.ContainsKey("corporation")).SelectMany(a => a.Value["corporation"]).Distinct().Where(a => a > 0).ToList();
        }

        public List<long> GetFeedAllAllianceIds()
        {
            return ParsedFeedMembersLists.Where(a => a.Value.ContainsKey("alliance")).SelectMany(a => a.Value["alliance"]).Distinct().Where(a => a > 0).ToList();
        }
        #endregion
    }
}
