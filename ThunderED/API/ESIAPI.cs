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
            _language = SettingsManager.GetBool("config", "useEnglishESIOnly") ? "en-us" : (SettingsManager.Get("config", "language")?.ToLower() ?? "en-us");
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

        internal async Task<JsonClasses.Type_id> GetTypeId(string reason, object id, bool forceUpdate = false)
        {
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


        internal async Task<JsonClasses.StructureData> GetStructureData(string reason, string id, string token)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.StructureData>($"https://esi.tech.ccp.is/latest/universe/structures/{id}/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        internal async Task<JsonClasses.ConstellationData> GetConstellationData(string reason, object id)
        {
            return await GetEntry<JsonClasses.ConstellationData>($"https://esi.tech.ccp.is/latest/universe/constellations/{id}/?datasource=tranquility&language={_language}", reason, id, 180);
        }

        internal async Task<JsonClasses.RegionData> GetRegionData(string reason, object id)
        {
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

        internal async Task<JsonClasses.SystemName> GetSystemData(string reason, object id, bool forceUpdate = false, bool noCache = false)
        {
            return await GetEntry<JsonClasses.SystemName>($"https://esi.tech.ccp.is/latest/universe/systems/{id}/?datasource=tranquility&language={_language}", reason, id, 180,
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

        public async Task<List<JsonClasses.MailHeader>> GetMailHeaders(string reason, string id, string token, int lastMailId, List<int> labels, int[] senders)
        {
            var authHeader = $"Bearer {token}";
            var lastIdText = lastMailId == 0 ? null : $"&last_mail_id={lastMailId}";
            var mailLabels = labels == null || labels.Count == 0 ? null : $"&labels={string.Join("%2C", labels)}";

            var data = await APIHelper.RequestWrapper<List<JsonClasses.MailHeader>>(
                $"https://esi.tech.ccp.is/latest/characters/{id}/mail/?datasource=tranquility{lastIdText}{mailLabels}&language={_language}", reason, authHeader);
            return senders == null || senders.Length == 0 || data == null ? data : data.Where(a=> senders.Contains(a.from)).ToList();
        }

        public async Task<JsonClasses.Mail> GetMail(string reason, string id, string token, int mailId)
        {
            var authHeader = $"Bearer {token}";
            return await APIHelper.RequestWrapper<JsonClasses.Mail>($"https://esi.tech.ccp.is/latest/characters/{id}/mail/{mailId}/?datasource=tranquility&language={_language}", reason, authHeader);
        }

        public async Task<JsonClasses.MailLabelData> GetMailLabels(string reason, string id, string token)
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
            return ((await APIHelper.RequestWrapper<JsonClasses.ServerStatus>($"https://esi.tech.ccp.is/latest/status/?datasource=tranquility&language={_language}", reason))?.players ?? 0) > 20;
        }
    }
}
