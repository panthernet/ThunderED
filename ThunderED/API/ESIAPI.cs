using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.PriceChecks;

namespace ThunderED.API
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public class ESIAPI: CacheBase
    {
        private readonly string _language;

        public ESIAPI()
        {
            _language = SettingsManager.Settings.Config.UseEnglishESIOnly ? "en-us" : SettingsManager.Settings.Config.Language?.ToLower() ?? "en-us";
        }

        public async Task<List<JsonFuzz.FuzzPrice>> GetFuzzPrice(string reason, List<long> ids)
        {
            var result = await APIHelper.RequestWrapper<Dictionary<string, JsonFuzz.FuzzItems>>($"https://market.fuzzwork.co.uk/aggregates/?station=60003760&types={string.Join(",", ids)}", reason);
            return result.Select(a=> new JsonFuzz.FuzzPrice{ Id = Convert.ToInt64(a.Key), Sell = a.Value.sell.min, Buy = a.Value.buy.max }).ToList();
        }

        public async Task RemoveAllCharacterDataFromCache(object id)
        {
            if(id == null) return;
            var user = await GetCharacterData("ESIAPI", id);
            if(user == null) return;
            await RemoveDbCache("CharacterData", id);
            await RemoveCorporationFromCache(user.corporation_id);
            if (user.alliance_id.HasValue)
                await RemoveAllianceFromCache(user.alliance_id.Value);
        }


        public async Task RemoveCharacterFromCache(object id)
        {
            await RemoveDbCache("CharacterData", id);
        }

        public async Task RemoveCorporationFromCache(object id)
        {
            await RemoveDbCache("CorporationData", id);
        }

        public async Task RemoveAllianceFromCache(object id)
        {
            await RemoveDbCache("AllianceData", id);
        }

        public async Task<JsonClasses.CharacterData> GetCharacterData(string reason, object id, bool forceUpdate = false, bool noCache = false, bool isAggressive = false)
        {
            JsonClasses.CharacterData result;
            if (isAggressive)
                result = await GetAgressiveESIEntry<JsonClasses.CharacterData>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/?datasource=tranquility&language={_language}", reason, id, 10);
            else
                result = await GetEntry<JsonClasses.CharacterData>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/?datasource=tranquility&language={_language}", reason, id, 1,
                    forceUpdate, noCache);

            if (result != null)
                result.character_id = Convert.ToInt64(id);
            return result;
        }

        public async Task<JsonClasses.CorporationData> GetCorporationData(string reason, object id, bool forceUpdate = false, bool noCache = false, bool isAggressive = false)
        {
            if (id == null) return null;
            if(isAggressive)
                return await GetAgressiveESIEntry<JsonClasses.CorporationData>($"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{id}/?datasource=tranquility&language={_language}", reason, id,10);
            return await GetEntry<JsonClasses.CorporationData>($"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{id}/?datasource=tranquility&language={_language}", reason, id, 1,
                forceUpdate, noCache);
        }

        public async Task<JsonClasses.AllianceData> GetAllianceData(string reason, object id, bool forceUpdate = false, bool noCache = false, bool isAggressive = false)
        {
            if (id == null) return null;
            if(isAggressive)
                return await GetAgressiveESIEntry<JsonClasses.AllianceData>($"{SettingsManager.Settings.Config.ESIAddress}latest/alliances/{id}/?datasource=tranquility&language={_language}", reason, id, 10);
            return await GetEntry<JsonClasses.AllianceData>($"{SettingsManager.Settings.Config.ESIAddress}latest/alliances/{id}/?datasource=tranquility&language={_language}", reason, id, 1,
                forceUpdate, noCache);
        }

       /* public async Task<JsonClasses.AffiliationData> GetAffiliationeData(string reason, object id, bool forceUpdate = false, bool noCache = false, bool isAggressive = false)
        {
            if (id == null) return null;
            if (isAggressive)
                return await GetAgressiveESIEntry<JsonClasses.AllianceData>($"{SettingsManager.Settings.Config.ESIAddress}latest/alliances/{id}/?datasource=tranquility&language={_language}", reason, id, 10);
            return await GetEntry<JsonClasses.AllianceData>($"{SettingsManager.Settings.Config.ESIAddress}latest/alliances/{id}/?datasource=tranquility&language={_language}", reason, id, 1,
                forceUpdate, noCache);
        }*/

        public async Task<object> GetMemberEntityProperty(string reason, object id, string propertyName)
        {
            var ch = await GetCharacterData(reason, id) ?? (object)await GetCorporationData(reason, id) ?? await GetAllianceData(reason, id);
            return ch?.GetType().GetProperty(propertyName)?.GetValue(ch);
        }

        public async Task<JsonClasses.FactionData> GetFactionData(string reason, long id)
        {
            var factions = await APIHelper.RequestWrapper<List<JsonClasses.FactionData>>($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/factions/?datasource=tranquility&language={_language}", reason);
            return factions?.FirstOrDefault(a => a.faction_id == id);
        }

        public async Task<List<JsonClasses.CorporationHistoryEntry>> GetCharCorpHistory(string reason, object charId)
        {
            return await APIHelper.RequestWrapper<List<JsonClasses.CorporationHistoryEntry>>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{charId}/corporationhistory/?datasource=tranquility&language={_language}", reason);
        }

        public async Task<JsonClasses.Type_id> GetTypeId(string reason, object id, bool forceUpdate = false)
        {
            
            var data = await SQLHelper.GetTypeId(Convert.ToInt64(id));
            if (data != null)
                return data;

            var result =  await GetEntry<JsonClasses.Type_id>($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/types/{id}/?datasource=tranquility&language={_language}", reason, id, 30,
                forceUpdate);
            return result;
        }

        public async Task<JsonClasses.SystemIDSearch> GetRadiusSystems(string reason, object id)
        {
            if (id == null || id.ToString() == "0") return null;
            return await APIHelper.RequestWrapper<JsonClasses.SystemIDSearch>($"{SettingsManager.Settings.Config.ESIAddress}latest/search/?categories=solar_system&datasource=tranquility&search={id}&strict=true", reason);
        }

        public async Task<string> GetRawRoute(string reason, object firstId, object secondId)
        {
            if (firstId == null || secondId == null || firstId.ToString() == "0" || secondId.ToString() == "0") return null;
            return await APIHelper.RequestWrapper<string>($"{SettingsManager.Settings.Config.ESIAddress}latest/route/{firstId}/{secondId}/?datasource=tranquility&flag=shortest", reason);
        }

        public async Task<ESIQueryResult<List<JsonClasses.Notification>>> GetNotifications(string reason, object userId, string token, string etag)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.Notification>>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{userId}/notifications/?datasource=tranquility&language={_language}", reason, authHeader, etag);
        }

        public async Task<JsonClasses.Planet> GetPlanet(string reason, object planetId)
        {
            return await APIHelper.RequestWrapper<JsonClasses.Planet>($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/planets/{planetId}/?datasource=tranquility&language={_language}", reason);
        }

        public async Task<JsonClasses.ESIKill> GetKillmail(string reason, object killId, object killHash)
        {
            return await APIHelper.RequestWrapper<JsonClasses.ESIKill>($"{SettingsManager.Settings.Config.ESIAddress}latest/killmails/{killId}/{killHash}/?datasource=tranquility&language={_language}", reason);
        }


        public async Task<JsonClasses.StructureData> GetUniverseStructureData(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await GetEntry<JsonClasses.StructureData>($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/structures/{id}/?datasource=tranquility&language={_language}", reason, id, 1, false, false, authHeader);
        }

        public async Task<JsonClasses.StationData> GetStationData(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await GetEntry<JsonClasses.StationData>($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/stations/{id}/?datasource=tranquility&language={_language}", reason, id, 30, false, false, authHeader);
        }

        public async Task<JsonClasses.ConstellationData> GetConstellationData(string reason, object id)
        {
            var data = await SQLHelper.GetConstellationById(Convert.ToInt64(id));
            if (data != null)
                return data;

            return await GetEntry<JsonClasses.ConstellationData>($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/constellations/{id}/?datasource=tranquility&language={_language}", reason, id, 180);
        }

        public async Task<JsonClasses.RegionData> GetRegionData(string reason, object id)
        {
            var data = await SQLHelper.GetRegionById(Convert.ToInt64(id));
            if (data != null)
                return data;

            return await GetEntry<JsonClasses.RegionData>($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/regions/{id}/?datasource=tranquility&language={_language}", reason, id, 180);
        }


        public async Task<JsonClasses.CharacterID> SearchCharacterId(string reason, string name)
        {
            name = HttpUtility.UrlEncode(name);
            return await APIHelper.RequestWrapper<JsonClasses.CharacterID>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/search/?categories=character&datasource=tranquility&search={name}&strict=true", reason);
        }

        public async Task<bool> OpenContractIngame(string reason, long contractId, string token)
        {
           // var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(characterId);

           // var token = await RefreshToken(authUserEntity.RefreshToken, SettingsManager.Settings.WebServerModule.CcpAppClientId, SettingsManager.Settings.WebServerModule.CcpAppSecret);
            var authHeader = $"Bearer {token}";
            var values = new Dictionary<string, string> {{"contract_id", $"{contractId}"}, {"datasource","tranquility"}};
            var content = new FormUrlEncodedContent(values);

            return await APIHelper.PostWrapper($"{SettingsManager.Settings.Config.ESIAddress}latest/ui/openwindow/contract/?contract_id={contractId}&datasource=tranquility", content, reason, authHeader);
        }

        public async Task<JsonClasses.CorpIDLookup> SearchCorporationId(string reason, string name)
        {
            name = HttpUtility.UrlEncode(name);
            return await APIHelper.RequestWrapper<JsonClasses.CorpIDLookup>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/search/?categories=corporation&datasource=tranquility&search={name}&strict=true", reason);
        }

        public async Task<JsonClasses.AllianceIDLookup> SearchAllianceId(string reason, string name)
        {
            name = HttpUtility.UrlEncode(name);
            return await APIHelper.RequestWrapper<JsonClasses.AllianceIDLookup>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/search/?categories=alliance&datasource=tranquility&search={name}&strict=true", reason);
        }

        public async Task<ESIQueryResult<List<JsonClasses.FWSystemStat>>> GetFWSystemStats(string reason, string etag)
        {
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.FWSystemStat>>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/fw/systems/?datasource=tranquility&language={_language}", reason, null, etag);
        }

        public async Task<JsonClasses.SystemName> GetSystemData(string reason, object id, bool forceUpdate = false, bool noCache = false)
        {
            var system = await SQLHelper.GetSystemById(Convert.ToInt64(id));
            if (system != null)
                return system;

            return await GetEntry<JsonClasses.SystemName>($"{SettingsManager.Settings.Config.ESIAddress}dev/universe/systems/{id}/?datasource=tranquility&language={_language}", reason, id, 180,
                forceUpdate, noCache);
        }

        public void Auth(string client, string key)
        {
            var ssoClient = new HttpClient();
            ssoClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<ESIQueryResult<string>> RefreshToken(string refreshToken, string clientId, string secret, string notes = null)
        { 
            var result = new ESIQueryResult<string>();
            try
            {
                using (var ssoClient = new HttpClient())
                {
                    ssoClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
                    ssoClient.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret))}");

                    var values = new Dictionary<string, string> {{"grant_type", "refresh_token"}, {"refresh_token", $"{refreshToken}"}};
                    var content = new FormUrlEncodedContent(values);
                    using (var responseMessage = await ssoClient.PostAsync("https://login.eveonline.com/oauth/token", content))
                    {
                        var raw = await responseMessage.Content.ReadAsStringAsync();
                        if (!responseMessage.IsSuccessStatusCode)
                        {
                            if (raw.StartsWith("{\"error\""))
                            {
                                await LogHelper.LogWarning($"[TOKEN] Request failure: {raw}\n{notes}", LogCat.ESI);
                                result.Data.ErrorCode = -99;
                                result.Data.Message = "Valid ESI request error";
                            }
                            else
                            {
                                result.Data.ErrorCode = (int)responseMessage.StatusCode;
                                result.Data.Message = responseMessage.StatusCode.ToString();
                            }
                            return result;
                        }

                        result.Result = (string)JObject.Parse(raw)["access_token"];
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"{nameof(RefreshToken)} {notes}", ex, LogCat.ESI);
                result.Data.ErrorCode = -1;
                result.Data.Message = "Unexpected exception";
                return result;
            }
        }


        public async Task<string[]> GetAuthToken(string code, string clientID, string secret)
        {
            try
            {
                using (var ssoClient = new HttpClient())
                {
                    ssoClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
                    var values = new Dictionary<string, string> {{"grant_type", "authorization_code"}, {"code", $"{code}"}};

                    ssoClient.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(clientID + ":" + secret))}");
                    var content = new FormUrlEncodedContent(values);

                    using (var tokenresponse = await ssoClient.PostAsync("https://login.eveonline.com/oauth/token", content))
                    {
                        var responseString = await tokenresponse.Content.ReadAsStringAsync();
                        return new[] {(string) JObject.Parse(responseString)["access_token"], (string) JObject.Parse(responseString)["refresh_token"]};
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("GetAuthToken", ex, LogCat.ESI);
                return null;
            }
        }

        #region Private methods
        

 
        
        private async Task<T> GetEntry<T>(string url, string reason, object id, int days, bool forceUpdate = false, bool noCache = false, string authHeader = null) 
            where T : class
        {
            if (id == null || id.ToString() == "0") return null;
            var data = await GetFromDbCache<T>(id, days);
            if(data == null || forceUpdate)
            {
                data = await APIHelper.RequestWrapper<T>(url, reason, authHeader);
                if(data != null && !noCache)
                    await UpdateDbCache(data, id, days);
            }
            return data;
        }


        private async Task<T> GetAgressiveESIEntry<T>(string url, string reason, object id, int retries)
            where T : class
        {
            if (id == null || id.ToString() == "0") return null;
            var data = await GetFromDbCache<T>(id, 1);
            if (data == null)
            {
                var result = await APIHelper.AggressiveESIRequestWrapper<T>(url, reason, retries);
                if (!result.Data.IsFailed)
                {
                    data = result.Result;
                    if (data != null)
                        await UpdateDbCache(data, id, 1);
                }
            }
            return data;
        }

        #endregion

        #region Cache
        /// <summary>
        /// Purge all outdated cache
        /// </summary>
        internal override async void PurgeCache()
        {
            await SQLHelper.PurgeCache();
        }

        /// <summary>
        /// Clear all cache by type. Everything if null.
        /// </summary>
        /// <param name="type">Cahce type</param>
        internal override async void ResetCache(string type = null)
        {
            await SQLHelper.DeleteCache(type);
            await SettingsManager.UpdateSettings();
            await SimplifiedAuth.UpdateInjectedSettings();
        }
        #endregion

        public async Task<ESIQueryResult<List<JsonClasses.MailHeader>>> GetMailHeaders(string reason, object id, string token, long lastMailId, string etag)
        {
           // if (senders.Count == 0 && labels.Count == 0 && mailListsIds.Count == 0) return null;
            var authHeader = $"Bearer {token}";
            var lastIdText = lastMailId == 0 ? null : $"&last_mail_id={lastMailId}";
            //var mailLabels = labels == null || labels.Count == 0 ? null : $"&labels={string.Join("%2C", labels)}";

            var data = await APIHelper.ESIRequestWrapper<List<JsonClasses.MailHeader>>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/mail/?datasource=tranquility{lastIdText}&language={_language}", reason, authHeader, etag);
            return data;
        }

        public async Task<List<JsonClasses.MailList>> GetMailLists(string reason, long id, string token)
        {
            if (id == 0) return new List<JsonClasses.MailList>();
            var authHeader = $"Bearer {token}";

            var data = await APIHelper.RequestWrapper<List<JsonClasses.MailList>>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/mail/lists/?datasource=tranquility&language={_language}", reason, authHeader);

            return data ?? new List<JsonClasses.MailList>();
        }

        public async Task<decimal> GetCharacterWalletBalance(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            var result = await APIHelper.RequestWrapper<string>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/wallet/?datasource=tranquility", reason, authHeader);
            return string.IsNullOrEmpty(result) ? 0 : Convert.ToDecimal(result.Substring(0,  result.IndexOfAny(new []{',','.'})));
        }

        public async Task<List<JsonClasses.WalletJournalEntry>> GetCharacterWalletJournal(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.WalletJournalEntry>>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/wallet/journal/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        
        public async Task<List<JsonClasses.WalletTransactionEntry>> GetCharacterWalletTransactions(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.WalletTransactionEntry>>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/wallet/transactions/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<List<JsonClasses.WalletJournalEntry>> GetCharacterJournalTransactions(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.WalletJournalEntry>>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/wallet/journal/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<List<JsonClasses.CharYearlyStatsEntry>> GetCharYearlyStats(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.CharYearlyStatsEntry>>(
                $"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/stats/?datasource=tranquility&language={_language}", reason, authHeader);
        }
        

        public async Task<JsonClasses.Mail> GetMail(string reason, object id, string token, long mailId)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.Mail>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/mail/{mailId}/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.MailLabelData> GetMailLabels(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.MailLabelData>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/mail/labels/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.IncursionData[]> GetIncursions(string reason)
        {
            return await APIHelper.RequestWrapper<JsonClasses.IncursionData[]>($"{SettingsManager.Settings.Config.ESIAddress}latest/incursions/?datasource=tranquility&language={_language}", reason);
        }

        public async Task<bool> IsServerOnline(string reason)
        {
            var status = await GetServerStatus(reason);
            if (status?.Data == null || status.Data.IsNoConnection) return false;
            return status.Result.players > 20;
        }

        public async Task<int> IsServerOnlineEx(string reason)
        {
            try
            {
                var res = await GetServerStatus(reason);
                if (res.Data.IsFailed || res.Data.IsNotValid || res.Data.IsNoConnection)
                {
                    if (DateTime.UtcNow.Hour == 11 && DateTime.UtcNow.Minute <= 30)
                        return 0;
                    return -1; //esi down
                }

                return res.Result.players > 20 ? 1 : 0;
            }
            catch
            {
                return -1;
            }
        }

        public async Task<ESIQueryResult<JsonClasses.ServerStatus>> GetServerStatus(string reason)
        {
            return await APIHelper.ESIRequestWrapper<JsonClasses.ServerStatus>($"{SettingsManager.Settings.Config.ESIAddress}latest/status/?datasource=tranquility&language={_language}", reason, null, null, false, true);
        }

        public async Task<ESIQueryResult<List<JsonClasses.NullCampaignItem>>> GetNullCampaigns(string reason, string etag)
        {
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.NullCampaignItem>>($"{SettingsManager.Settings.Config.ESIAddress}latest/sovereignty/campaigns/?datasource=tranquility&language={_language}", reason, null, etag);
        }

        public async Task<ESIQueryResult<List<JsonClasses.FWStats>>> GetFWStats(string reason, string etag)
        {
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.FWStats>>($"{SettingsManager.Settings.Config.ESIAddress}latest/fw/stats/?datasource=tranquility&language={_language}", reason, null, etag);

        }

        public async Task<ESIQueryResult<List<JsonClasses.Contract>>>  GetCharacterContracts(string reason, object id, string token, string etag)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.Contract>>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/contracts/?datasource=tranquility&language={_language}", reason, authHeader, etag);
        }

        public async Task<ESIQueryResult<List<JsonClasses.Contract>>> GetCorpContracts(string reason, object id, string token, string etag)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.Contract>>($"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{id}/contracts/?datasource=tranquility&language={_language}", reason, authHeader, etag);
        }

        public async Task<List<JsonClasses.ContractItem>> GetCharacterContractItems(string reason, object charId, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.ContractItem>>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{charId}/contracts/{id}/items/?datasource=tranquility&language={_language}", reason, authHeader);

        }

        public async Task<List<JsonClasses.ContractItem>> GetPublicContractItems(string reason, object id)
        {
            return await APIHelper.RequestWrapper<List<JsonClasses.ContractItem>>($"{SettingsManager.Settings.Config.ESIAddress}latest/contracts/public/items/{id}/?datasource=tranquility&language={_language}", reason);

        }


        public async Task<List<JsonClasses.ContractItem>> GetCorpContractItems(string reason, object corpId, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.ContractItem>>($"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{corpId}/contracts/{id}/items/?datasource=tranquility&language={_language}", reason, authHeader);

        }


        public async Task<ESIQueryResult<List<JsonClasses.Contact>>> GetCharacterContacts(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.Contact>>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/contacts/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<ESIQueryResult<List<JsonClasses.Contact>>> GetCorpContacts(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.Contact>>($"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{id}/contacts/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<ESIQueryResult<List<JsonClasses.Contact>>> GetAllianceContacts(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.Contact>>($"{SettingsManager.Settings.Config.ESIAddress}latest/alliances/{id}/contacts/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.SkillsData> GetCharSkills(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.SkillsData>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/skills/?datasource=tranquility&language={_language}", reason, authHeader);
        }


        public async Task<string> GetCorporationIcons(string reason, long id, int size)
        {
            var res =  await APIHelper.RequestWrapper<JsonClasses.CorpIconsData>($"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{id}/icons/?datasource=tranquility&language={_language}", reason);
            if (res == null)
                return null;

            switch (size)
            {
                case 64:
                    return res.px64x64;
                case 128:
                    return res.px128x128;
                case 256:
                    return res.px256x256;
                default:
                    return null;
            }
        }

        public async Task<List<long>> GetNpcCorps(string reason)
        {
            return await APIHelper.RequestWrapper<List<long>>($"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/npccorps/?datasource=tranquility&language={_language}", reason);
        }

        public async Task<List<JsonClasses.StandingData>> GetcharacterStandings(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.StandingData>>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/standings/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.CharacterLocation> GetCharacterLocation(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.CharacterLocation>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/location/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.CharacterShip> GetCharacterShipType(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.CharacterShip>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/ship/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<List<JsonClasses.SovStructureData>> GetSovStructuresData(string reason)
        {
            return await APIHelper.RequestWrapper<List<JsonClasses.SovStructureData>>($"{SettingsManager.Settings.Config.ESIAddress}latest/sovereignty/structures/?datasource=tranquility&language={_language}", reason);

        }

        public async Task<JsonClasses.SearchResult> SearchLocationEntity(string reason, string value)
        {
            var searchValue = HttpUtility.UrlEncode(value);
            return await APIHelper.RequestWrapper<JsonClasses.SearchResult>($"{SettingsManager.Settings.Config.ESIAddress}latest/search/?categories=constellation,region,solar_system&datasource=tranquility&search={searchValue}&strict=true", reason);
        }

        public async Task<JsonClasses.SearchResult> SearchTypeEntity(string reason, string value)
        {
            var searchValue = HttpUtility.UrlEncode(value);
            return await APIHelper.RequestWrapper<JsonClasses.SearchResult>($"{SettingsManager.Settings.Config.ESIAddress}latest/search/?categories=inventory_type&datasource=tranquility&language={_language}&search={searchValue}&strict=true", reason);
        }


        public async Task<JsonClasses.SearchResult> SearchMemberEntity(string reason, string value, bool isAggressive = false)
        {
            var searchValue = HttpUtility.UrlEncode(value);
            if(isAggressive)
                return (await APIHelper.AggressiveESIRequestWrapper<JsonClasses.SearchResult>($"{SettingsManager.Settings.Config.ESIAddress}latest/search/?categories=alliance,character,corporation&datasource=tranquility&search={searchValue}&strict=true", reason,10))?.Result;
            return await APIHelper.RequestWrapper<JsonClasses.SearchResult>($"{SettingsManager.Settings.Config.ESIAddress}latest/search/?categories=alliance,character,corporation&datasource=tranquility&search={searchValue}&strict=true", reason);
        }

        public async Task<JsonClasses.MoonData> GetMoon(string reason, object id)
        {
            return await APIHelper.RequestWrapper<JsonClasses.MoonData>($"{SettingsManager.Settings.Config.ESIAddress}latest/universe/moons/{id}/?datasource=tranquility&language={_language}", reason);

        }

        public async Task<ESIQueryResult<List<JsonClasses.IndustryJob>>> GetCorpIndustryJobs(string reason, object id, string token, string etag)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.IndustryJob>>($"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{id}/industry/jobs/?datasource=tranquility&include_completed=true&language={_language}", reason, authHeader);
        }

        public async Task<ESIQueryResult<List<JsonClasses.IndustryJob>>> GetCharacterIndustryJobs(string reason, object id, string token, string etag)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.ESIRequestWrapper<List<JsonClasses.IndustryJob>>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/industry/jobs/?datasource=tranquility&include_completed=true&language={_language}", reason, authHeader);
        }

        public async Task<List<JsonClasses.CharacterTitle>> GetCharacterTitles(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.CharacterTitle>>($"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/titles/?datasource=tranquility&include_completed=true&language={_language}", reason, authHeader);
        }

        public async Task<List<MiningExtractionJson>> GetCorpMiningExtractions(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            var page = 1;
            var list = new List<MiningExtractionJson>();
            while (true)
            {
                var result = await APIHelper.RequestWrapper<List<MiningExtractionJson>>(
                    $"{SettingsManager.Settings.Config.ESIAddress}latest/corporation/{id}/mining/extractions/?datasource=tranquility&page={page}&language={_language}",
                    reason, authHeader);
                if (result == null || !result.Any())
                    break;
                list.AddRange(result);
                if (result.Count < 1000)
                    break;
                page++;
            }

            return list;
        }

        public async Task<List<MiningLedgerJson>> GetCorpMiningLedgers(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            var page = 1;
            var list = new List<MiningLedgerJson>();
            while (true)
            {
                var result = await APIHelper.RequestWrapper<List<MiningLedgerJson>>(
                    $"{SettingsManager.Settings.Config.ESIAddress}latest/corporation/{id}/mining/observers/?datasource=tranquility&page={page}&language={_language}",
                    reason, authHeader);
                if (result == null || !result.Any())
                    break;
                list.AddRange(result);
                if (result.Count < 1000)
                    break;
                page++;
            }

            return list;
        }

        public async Task<List<MiningLedgerEntryJson>> GetCorpMiningLedgerEntries(string reason, long corporationId, long observerId, string token)
        {
            var authHeader = $"Bearer {token}";
            var page = 1;
            var list = new List<MiningLedgerEntryJson>();
            while (true)
            {
                var result = await APIHelper.RequestWrapper<List<MiningLedgerEntryJson>>(
                    $"{SettingsManager.Settings.Config.ESIAddress}latest/corporation/{corporationId}/mining/observers/{observerId}/?datasource=tranquility&page={page}&language={_language}",
                    reason, authHeader);
                if (result == null || !result.Any())
                    break;
                list.AddRange(result);
                if (result.Count < 1000)
                    break;
                page++;
            }

            return list;
        }

        public async Task<List<CorporationStructureJson>> GetCorpStructures(string reason, object corporationId, string token)
        {
            var authHeader = $"Bearer {token}";
            var page = 1;
            var list = new List<CorporationStructureJson>();
            while (true)
            {
                var result = await APIHelper.RequestWrapper<List<CorporationStructureJson>>(
                    $"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{corporationId}/structures/?datasource=tranquility&page={page}&language={_language}",
                    reason, authHeader);
                if (result == null || !result.Any())
                    break;
                list.AddRange(result);
                if (result.Count < 1000)
                    break;
                page++;
            }

            return list;
        }

        public bool IsNpcCharacter(object id)
        {
            if (id == null) return false;
            if (long.TryParse(id.ToString(), out var result))
            {
                return result >= 1000000 && result <= 4000000;
            }

            return false;
        }

        public bool IsNpcCorporation(object id)
        {
            if (id == null) return false;
            if (long.TryParse(id.ToString(), out var result))
            {
                return result >= 1000000 && result <= 2000000;
            }

            return false;
        }

        public async Task<ESIQueryResult<List<AssetData>>> GetCharacterAssets(object id, string token, string etag = null, bool onePage = false)
        {
            var authHeader = $"Bearer {token}";
            var page = 1;
            var list = new List<AssetData>();
            while (true)
            {
                var result = await APIHelper.ESIRequestWrapper<List<AssetData>>(
                    $"{SettingsManager.Settings.Config.ESIAddress}latest/characters/{id}/assets/?datasource=tranquility&page={page}&language={_language}",
                    authHeader, etag);
                if (result?.Result == null)
                    break;
                etag = result.Data.ETag;
                list.AddRange(result.Result);
                if (result.Result.Count < 1000)
                    break;
                page++;
                if (onePage)
                    break;
            }

            return new ESIQueryResult<List<AssetData>>
            {
                Data = new QueryData { ETag = etag },
                Result = list
            };
        }

        public async Task<ESIQueryResult<List<AssetData>>> GetCorpAssets(object id, string token, string etag = null, bool onePage = false)
        {
            var authHeader = $"Bearer {token}";
            var page = 1;
            var list = new List<AssetData>();
            while (true)
            {
                var result = await APIHelper.ESIRequestWrapper<List<AssetData>>(
                    $"{SettingsManager.Settings.Config.ESIAddress}latest/corporations/{id}/assets/?datasource=tranquility&page={page}&language={_language}",
                    authHeader, etag);
                if (result?.Result == null)
                    break;
                etag = result.Data.ETag;
                list.AddRange(result.Result);
                if (result.Result.Count < 1000)
                    break;
                page++;
                if (onePage)
                    break;
            }

            return new ESIQueryResult<List<AssetData>>
            {
                Data = new QueryData { ETag = etag },
                Result = list
            };
        }

        //public async Task<ESIQueryResult<List<>>>

    }
}
