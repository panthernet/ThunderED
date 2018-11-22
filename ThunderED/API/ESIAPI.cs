using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.API
{
    /// <summary>
    /// Use partial class to implement additional methods
    /// </summary>
    public partial class ESIAPI: CacheBase
    {
        private readonly string _language;

        public ESIAPI()
        {
            _language = SettingsManager.Settings.Config.UseEnglishESIOnly ? "en-us" : SettingsManager.Settings.Config.Language?.ToLower() ?? "en-us";
        }

        internal async Task<JsonClasses.CharacterData> GetCharacterData(string reason, object id, bool forceUpdate = false, bool noCache = false)
        {
            return await GetEntry<JsonClasses.CharacterData>($"https://esi.tech.ccp.is/latest/characters/{id}/?datasource=tranquility&language={_language}", reason, id, 1,
                forceUpdate, noCache);
        }

        internal async Task<JsonClasses.CorporationData> GetCorporationData(string reason, object id, bool forceUpdate = false, bool noCache = false)
        {
            return await GetEntry<JsonClasses.CorporationData>($"https://esi.tech.ccp.is/latest/corporations/{id}/?datasource=tranquility&language={_language}", reason, id, 1,
                forceUpdate, noCache);
        }

        internal async Task<JsonClasses.AllianceData> GetAllianceData(string reason, object id, bool forceUpdate = false, bool noCache = false)
        {
            return await GetEntry<JsonClasses.AllianceData>($"https://esi.tech.ccp.is/latest/alliances/{id}/?datasource=tranquility&language={_language}", reason, id, 1,
                forceUpdate, noCache);
        }

        internal async Task<JsonClasses.FactionData> GetFactionData(string reason, long id)
        {
            var factions = await APIHelper.RequestWrapper<List<JsonClasses.FactionData>>($"https://esi.tech.ccp.is/latest/universe/factions/?datasource=tranquility&language={_language}", reason);
            return factions?.FirstOrDefault(a => a.faction_id == id);
        }

        internal async Task<List<JsonClasses.CorporationHistoryEntry>> GetCharCorpHistory(string reason, object charId)
        {
            return await APIHelper.RequestWrapper<List<JsonClasses.CorporationHistoryEntry>>($"https://esi.tech.ccp.is/latest/characters/{charId}/corporationhistory/?datasource=tranquility&language={_language}", reason);
        }

        internal async Task<JsonClasses.Type_id> GetTypeId(string reason, object id, bool forceUpdate = false)
        {
            
            var data = await SQLHelper.GetTypeId(Convert.ToInt64(id));
            if (data != null)
                return data;

            return await GetEntry<JsonClasses.Type_id>($"https://esi.tech.ccp.is/latest/universe/types/{id}/?datasource=tranquility&language={_language}", reason, id, 30,
                forceUpdate);
        }

        internal async Task<JsonClasses.SystemIDSearch> GetRadiusSystems(string reason, object id)
        {
            if (id == null || id.ToString() == "0") return null;
            return await APIHelper.RequestWrapper<JsonClasses.SystemIDSearch>($"https://esi.tech.ccp.is/latest/search/?categories=solar_system&datasource=tranquility&language={_language}&search={id}&strict=true", reason);
        }

        internal async Task<string> GetRawRoute(string reason, object firstId, object secondId)
        {
            if (firstId == null || secondId == null || firstId.ToString() == "0" || secondId.ToString() == "0") return null;
            return await APIHelper.RequestWrapper<string>($"https://esi.tech.ccp.is/latest/route/{firstId}/{secondId}/?datasource=tranquility&flag=shortest", reason);
        }

        internal async Task<List<JsonClasses.Notification>> GetNotifications(string reason, string userId, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.Notification>>($"https://esi.tech.ccp.is/latest/characters/{userId}/notifications/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        internal async Task<JsonClasses.Planet> GetPlanet(string reason, string planetId)
        {
            return await APIHelper.RequestWrapper<JsonClasses.Planet>($"https://esi.tech.ccp.is/latest/universe/planets/{planetId}/?datasource=tranquility&language={_language}", reason);
        }

        internal async Task<JsonClasses.ESIKill> GetKillmail(string reason, object killId, object killHash)
        {
            return await APIHelper.RequestWrapper<JsonClasses.ESIKill>($"https://esi.tech.ccp.is/latest/killmails/{killId}/{killHash}/?datasource=tranquility&language={_language}", reason);
        }


        internal async Task<JsonClasses.StructureData> GetStructureData(string reason, string id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.StructureData>($"https://esi.tech.ccp.is/latest/universe/structures/{id}/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        internal async Task<JsonClasses.ConstellationData> GetConstellationData(string reason, object id)
        {
            var data = await SQLHelper.GetConstellationById(Convert.ToInt64(id));
            if (data != null)
                return data;

            return await GetEntry<JsonClasses.ConstellationData>($"https://esi.tech.ccp.is/latest/universe/constellations/{id}/?datasource=tranquility&language={_language}", reason, id, 180);
        }

        internal async Task<JsonClasses.RegionData> GetRegionData(string reason, object id)
        {
            var data = await SQLHelper.GetRegionById(Convert.ToInt64(id));
            if (data != null)
                return data;

            return await GetEntry<JsonClasses.RegionData>($"https://esi.tech.ccp.is/latest/universe/regions/{id}/?datasource=tranquility&language={_language}", reason, id, 180);
        }


        internal async Task<JsonClasses.CharacterID> SearchCharacterId(string reason, string name)
        {
            name = HttpUtility.UrlEncode(name);
            return await APIHelper.RequestWrapper<JsonClasses.CharacterID>(
                $"https://esi.tech.ccp.is/latest/search/?categories=character&datasource=tranquility&language={_language}&search={name}&strict=true", reason);
        }

        internal async Task<JsonClasses.CorpIDLookup> SearchCorporationId(string reason, string name)
        {
            name = HttpUtility.UrlEncode(name);
            return await APIHelper.RequestWrapper<JsonClasses.CorpIDLookup>(
                $"https://esi.tech.ccp.is/latest/search/?categories=corporation&datasource=tranquility&language={_language}&search={name}&strict=true", reason);
        }

        internal async Task<JsonClasses.AllianceIDLookup> SearchAllianceId(string reason, string name)
        {
            name = HttpUtility.UrlEncode(name);
            return await APIHelper.RequestWrapper<JsonClasses.AllianceIDLookup>(
                $"https://esi.tech.ccp.is/latest/search/?categories=alliance&datasource=tranquility&language={_language}&search={name}&strict=true", reason);
        }

        internal async Task<List<JsonClasses.FWSystemStat>> GetFWSystemStats(string reason)
        {
            return await APIHelper.RequestWrapper<List<JsonClasses.FWSystemStat>>(
                $"https://esi.tech.ccp.is/latest/fw/systems/?datasource=tranquility&language={_language}", reason);
        }

        internal async Task<JsonClasses.SystemName> GetSystemData(string reason, object id, bool forceUpdate = false, bool noCache = false)
        {
            var system = await SQLHelper.GetSystemById(Convert.ToInt64(id));
            if (system != null)
                return system;

            return await GetEntry<JsonClasses.SystemName>($"https://esi.tech.ccp.is/dev/universe/systems/{id}/?datasource=tranquility&language={_language}", reason, id, 180,
                forceUpdate, noCache);
        }

        public void Auth(string client, string key)
        {
            var ssoClient = new HttpClient();
            ssoClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> RefreshToken(string refreshToken, string clientId, string secret)
        {
            try
            {
                using (var ssoClient = new HttpClient())
                {
                    ssoClient.DefaultRequestHeaders.Add("User-Agent", SettingsManager.DefaultUserAgent);
                    ssoClient.DefaultRequestHeaders.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes(clientId + ":" + secret))}");

                    var values = new Dictionary<string, string> {{"grant_type", "refresh_token"}, {"refresh_token", $"{refreshToken}"}};
                    var content = new FormUrlEncodedContent(values);
                    var tokenresponse = await ssoClient.PostAsync("https://login.eveonline.com/oauth/token", content);
                    var responseString = await tokenresponse.Content.ReadAsStringAsync();
                    return (string) JObject.Parse(responseString)["access_token"];
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("RefreshToken", ex, LogCat.ESI);
                return null;
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

                    var tokenresponse = await ssoClient.PostAsync("https://login.eveonline.com/oauth/token", content);
                    var responseString = await tokenresponse.Content.ReadAsStringAsync();

                    return new [] {(string) JObject.Parse(responseString)["access_token"], (string) JObject.Parse(responseString)["refresh_token"] };
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("GetAuthToken", ex, LogCat.ESI);
                return null;
            }
        }

        #region Private methods
        

 
        
        private async Task<T> GetEntry<T>(string url, string reason, object id, int days, bool forceUpdate = false, bool noCache = false) 
            where T : class
        {
            if (id == null || id.ToString() == "0") return null;
            var data = await GetFromDbCache<T>(id, days);
            if(data == null || forceUpdate)
            {
                data = await APIHelper.RequestWrapper<T>(url, reason);
                if(data != null && !noCache)
                    await UpdateDbCache(data, id, days);
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
            await SQLHelper.SQLiteDataPurgeCache();
        }

        /// <summary>
        /// Clear all cache by type. Everything if null.
        /// </summary>
        /// <param name="type">Cahce type</param>
        internal override async void ResetCache(string type = null)
        {
            if (string.IsNullOrEmpty(type))
                await SQLHelper.SQLiteDataDelete("cache");
            else await SQLHelper.SQLiteDataDelete("cache", "type", type);
            SettingsManager.UpdateSettings();
        }
        #endregion

        public async Task<List<JsonClasses.MailHeader>> GetMailHeaders(string reason, string id, string token, long lastMailId)
        {
           // if (senders.Count == 0 && labels.Count == 0 && mailListsIds.Count == 0) return null;
            var authHeader = $"Bearer {token}";
            var lastIdText = lastMailId == 0 ? null : $"&last_mail_id={lastMailId}";
            //var mailLabels = labels == null || labels.Count == 0 ? null : $"&labels={string.Join("%2C", labels)}";

            var data = await APIHelper.RequestWrapper<List<JsonClasses.MailHeader>>(
                $"https://esi.tech.ccp.is/latest/characters/{id}/mail/?datasource=tranquility{lastIdText}&language={_language}", reason, authHeader);
            return data;
        }

        public async Task<List<JsonClasses.MailList>> GetMailLists(string reason, long id, string token)
        {
            if (id == 0) return new List<JsonClasses.MailList>();
            var authHeader = $"Bearer {token}";

            var data = await APIHelper.RequestWrapper<List<JsonClasses.MailList>>(
                $"https://esi.tech.ccp.is/latest/characters/{id}/mail/lists/?datasource=tranquility&language={_language}", reason, authHeader);

            return data ?? new List<JsonClasses.MailList>();
        }

        public async Task<double> GetCharacterWalletBalance(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            var result = await APIHelper.RequestWrapper<string>(
                $"https://esi.tech.ccp.is/latest/characters/{id}/wallet/?datasource=tranquility", reason, authHeader);
            return string.IsNullOrEmpty(result) ? 0 : Convert.ToDouble(result);
        }

        public async Task<List<JsonClasses.WalletJournalEntry>> GetCharacterWalletJournal(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.WalletJournalEntry>>(
                $"https://esi.tech.ccp.is/latest/characters/{id}/wallet/journal/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        
        public async Task<List<JsonClasses.WalletTransactionEntry>> GetCharacterWalletTransactions(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.WalletTransactionEntry>>(
                $"https://esi.tech.ccp.is/latest/characters/{id}/wallet/transactions/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<List<JsonClasses.WalletJournalEntry>> GetCharacterJournalTransactions(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.WalletJournalEntry>>(
                $"https://esi.tech.ccp.is/latest/characters/{id}/wallet/journal/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<List<JsonClasses.CharYearlyStatsEntry>> GetCharYearlyStats(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.CharYearlyStatsEntry>>(
                $"https://esi.tech.ccp.is/latest/characters/{id}/stats/?datasource=tranquility&language={_language}", reason, authHeader);
        }
        

        public async Task<JsonClasses.Mail> GetMail(string reason, object id, string token, long mailId)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.Mail>($"https://esi.tech.ccp.is/latest/characters/{id}/mail/{mailId}/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.MailLabelData> GetMailLabels(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.MailLabelData>($"https://esi.tech.ccp.is/latest/characters/{id}/mail/labels/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.IncursionData[]> GetIncursions(string reason)
        {
            return await APIHelper.RequestWrapper<JsonClasses.IncursionData[]>($"https://esi.tech.ccp.is/latest/incursions/?datasource=tranquility&language={_language}", reason);
        }

        public async Task<bool> IsServerOnline(string reason)
        {
            return ((await GetServerStatus(reason))?.players ?? 0) > 20;
        }

        public async Task<JsonClasses.ServerStatus> GetServerStatus(string reason)
        {
            return await APIHelper.RequestWrapper<JsonClasses.ServerStatus>($"https://esi.tech.ccp.is/latest/status/?datasource=tranquility&language={_language}", reason);
        }

        public async Task<List<JsonClasses.NullCampaignItem>> GetNullCampaigns(string reason)
        {
            return new List<JsonClasses.NullCampaignItem>(await APIHelper.RequestWrapper<JsonClasses.NullCampaignItem[]>($"https://esi.tech.ccp.is/latest/sovereignty/campaigns/?datasource=tranquility&language={_language}", reason));
        }

        public async Task<List<JsonClasses.FWStats>> GetFWStats(string reason)
        {
            return await APIHelper.RequestWrapper<List<JsonClasses.FWStats>>($"https://esi.tech.ccp.is/latest/fw/stats/?datasource=tranquility&language={_language}", reason);

        }

        public async Task<List<JsonClasses.Contract>> GetCharacterContracts(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.Contract>>($"https://esi.tech.ccp.is/latest/characters/{id}/contracts/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<List<JsonClasses.Contact>> GetCharacterContacts(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<List<JsonClasses.Contact>>($"https://esi.tech.ccp.is/latest/characters/{id}/contacts/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.SkillsData> GetCharSkills(string reason, object id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.SkillsData>($"https://esi.tech.ccp.is/latest/characters/{id}/skills/?datasource=tranquility&language={_language}", reason, authHeader);
        }


    }
}
