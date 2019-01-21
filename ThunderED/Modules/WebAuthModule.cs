using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public class WebAuthModule: AppModuleBase
    {
        private DateTime _lastTimersCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthWeb;
        private static int _standsInterval;
        private static DateTime _lastStandsUpdateDate = DateTime.MinValue;

        public WebAuthModule()
        {
            LogHelper.LogModule("Inititalizing WebAuth module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, Auth);
        }

        public override async Task Run(object prm)
        {
            if (!Settings.Config.ModuleAuthWeb) return;

            if(IsRunning) return;
            IsRunning = true;
            try
            {
                _standsInterval = _standsInterval != 0? _standsInterval : SettingsManager.Settings.WebAuthModule.StandingsRefreshIntervalInMinutes;
                if ((DateTime.Now - _lastStandsUpdateDate).TotalMinutes >= _standsInterval)
                {
                    _lastStandsUpdateDate = DateTime.Now;
                    await LogHelper.LogInfo("Running standings update...", Category);
                    try
                    {
                        var sb = new StringBuilder();
                        foreach (var numericCharId in Settings.WebAuthModule.AuthGroups.Values.Where(a => a.StandingsAuth != null).SelectMany(a => a.StandingsAuth.CharacterIDs)
                            .Distinct())
                        {
                            var st = await SQLHelper.LoadAuthStands(numericCharId);
                            if (st == null) return;
                            var token = await APIHelper.ESIAPI.RefreshToken(st.Token, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);

                            await RefreshStandings(st, token);
                            await SQLHelper.DeleteAuthStands(numericCharId);
                            await SQLHelper.SaveAuthStands(st);
                            sb.Append($"{numericCharId},");
                        }

                        if (sb.Length > 0)
                        {
                            sb.Remove(sb.Length - 1, 1);
                            await LogHelper.LogInfo($"Standings updated for: {sb}", Category);
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx("Stands Feed Update", ex, Category);
                    }
                }

                if ((DateTime.Now - _lastTimersCheck).TotalMinutes < 1 ) return;
                _lastTimersCheck = DateTime.Now;

                await ProcessPreliminaryAppilicants();
                await ClearOutdatedApplicants();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task ClearOutdatedApplicants()
        {
            var list = await SQLHelper.GetOutdatedAuthUsers();
            foreach (var user in list)
            {
                var group = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == user.GroupName).Value;
                if (group == null)
                {
                    await SQLHelper.DeleteAuthDataByCharId(user.CharacterId);
                    continue;
                }

                if (group.AppInvalidationInHours > 0 && (DateTime.Now - user.CreateDate).TotalHours >= group.AppInvalidationInHours)
                {
                    await SQLHelper.DeleteAuthDataByCharId(user.CharacterId);
                }
            }
        }

        public async Task ProcessPreliminaryApplicant(string remainder)
        {
            var data = await SQLHelper.GetAuthUserByRegCode(remainder);
            if (data == null) return;
            await ProcessPreliminaryApplicant(data);
        }

        public static async Task<AuthRoleEntity> GetCorpEntityById(List<WebAuthGroup> groups,long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups.Values.ToList();
            foreach (var authGroup in groups)
            {
                if (authGroup.StandingsAuth == null)
                {
                    foreach (var entity in authGroup.AllowedCorporations.Values)
                    {
                        if (entity.Id.Contains(id))
                            return entity;
                    }
                }
                else
                {
                    return await GetEntityForStandingsAuth(authGroup, id, 1);
                }
            }

            return null;
        }

        public static async Task<AuthRoleEntity> GetCharEntityById(List<WebAuthGroup> groups, long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups.Values.ToList();
            foreach (var authGroup in groups)
            {
                if (authGroup.StandingsAuth == null)
                {
                    foreach (var entity in authGroup.AllowedCharacters.Values)
                    {
                        if (entity.Id.Contains(id))
                            return entity;
                    }
                }
                else
                {
                    return await GetEntityForStandingsAuth(authGroup, id, 0);
                }
            }

            return null;
        }

        public static async Task<AuthRoleEntity> GetAllyEntityById(List<WebAuthGroup> groups,long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups.Values.ToList();
            foreach (var authGroup in groups)
            {
                if (authGroup.StandingsAuth == null)
                {
                    foreach (var entity in authGroup.AllowedAlliances.Values)
                    {
                        if (entity.Id.Contains(id))
                            return entity;
                    }
                }
                else
                {
                    return await GetEntityForStandingsAuth(authGroup, id, 2);
                }
            }

            return null;
        }

        public static async Task<AuthRoleEntity> GetAllyEntityById(WebAuthGroup group, long id)
        {
            if (group.StandingsAuth == null)
            {
                foreach (var entity in group.AllowedAlliances.Values)
                {
                    if (entity.Id.Contains(id))
                        return entity;
                }
            }
            else
            {
                return await GetEntityForStandingsAuth(group, id, 2);

            }

            return null;
        }

        public static async Task<AuthRoleEntity> GetCorpEntityById(WebAuthGroup group, long id)
        {
            if (group.StandingsAuth == null)
            {
                foreach (var entity in group.AllowedCorporations.Values)
                {
                    if (entity.Id.Contains(id))
                        return entity;
                }
            }
            else
            {
                return await GetEntityForStandingsAuth(group, id, 1);
            }

            return null;
        }

        public static async Task<AuthRoleEntity> GetCharEntityById(WebAuthGroup group, long id)
        {
            if (group.StandingsAuth == null)
            {
                foreach (var entity in group.AllowedCharacters.Values)
                {
                    if (entity.Id.Contains(id))
                        return entity;
                }
            }
            else
            {
                return await GetEntityForStandingsAuth(group, id, 0);
            }

            return null;
        }

        private static async Task<AuthRoleEntity> GetEntityForStandingsAuth(WebAuthGroup group, long id, int type) //0 personal, 1 corp, 2 ally, 3 faction
        {
            foreach (var characterID in group.StandingsAuth.CharacterIDs)
            {
                string typeName;
                switch (type)
                {
                    case 0:
                        typeName = "character";
                        break;
                    case 1:
                        typeName = "corporation";
                        break;
                    case 2:
                        typeName = "alliance";
                        break;
                    default:
                        return null;
                }

                var standings = await SQLHelper.LoadAuthStands(characterID);
                if (standings == null) return null;

                var st = new List<JsonClasses.Contact>();
                st.AddRange(group.StandingsAuth.UseCharacterStandings && standings.PersonalStands != null ? standings.PersonalStands : new List<JsonClasses.Contact>());
                st.AddRange(group.StandingsAuth.UseCorporationStandings && standings.CorpStands != null ? standings.CorpStands : new List<JsonClasses.Contact>());
                st.AddRange(group.StandingsAuth.UseAllianceStandings && standings.AllianceStands != null? standings.AllianceStands : new List<JsonClasses.Contact>());

                var stands = st.Where(a => a.contact_type == typeName && a.contact_id == id).Select(a=> a.standing).Distinct();
                var s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "eq").FirstOrDefault(a => a.Standings.Any(b => stands.Contains(b)));
                if (s != null) return new AuthRoleEntity {Id = new List<long> {id}, DiscordRoles = s.DiscordRoles};

                s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "le").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c <= b)));
                if (s != null) return new AuthRoleEntity {Id = new List<long> {id}, DiscordRoles = s.DiscordRoles};
                s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "ge").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c >= b)));
                if (s != null) return new AuthRoleEntity {Id = new List<long> {id}, DiscordRoles = s.DiscordRoles};

                s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "lt").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c < b)));
                if (s != null) return new AuthRoleEntity {Id = new List<long> {id}, DiscordRoles = s.DiscordRoles};
                s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "gt").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c > b)));
                if (s != null) return new AuthRoleEntity {Id = new List<long> {id}, DiscordRoles = s.DiscordRoles};


            }

            return null;
        }

        public static async Task<WebAuthResult> GetAuthGroupByCorpId(List<WebAuthGroup> groups, long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups.Values.ToList();
            var result = groups.FirstOrDefault(a => a.AllowedCorporations.Values.Any(b=> b.Id.Contains(id)));
            if (result != null) return new WebAuthResult {Group = result, RoleEntity = await GetCorpEntityById(groups, id)};

            foreach (var authGroup in groups.Where(a=> a.StandingsAuth != null))
            {
                var res = await GetEntityForStandingsAuth(authGroup, id, 1);
                if (res != null) return new WebAuthResult {Group = authGroup, RoleEntity = res };
            }

            return null;
        }

        public static async Task<WebAuthResult> GetAuthGroupByCharacterId(List<WebAuthGroup> groups, long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups.Values.ToList();
            var result = groups.FirstOrDefault(a => a.AllowedCharacters.Values.Any(b=> b.Id.Contains(id)));
            if (result != null) return new WebAuthResult {Group = result, RoleEntity = await GetCharEntityById(groups, id)};

            foreach (var authGroup in groups.Where(a=> a.StandingsAuth != null))
            {
                var res = await GetEntityForStandingsAuth(authGroup, id, 0);
                if (res != null) return new WebAuthResult {Group = authGroup, RoleEntity = res };
            }

            return null;
        }


        public static async Task<WebAuthResult> GetAuthGroupByAllyId(List<WebAuthGroup> groups, long id)
        {
            if (id == 0) return null;
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups.Values.ToList();
            var result = groups.FirstOrDefault(a => a.AllowedAlliances.Values.Any(b=> b.Id.Contains(id)));
            if (result != null) return new WebAuthResult {Group = result, RoleEntity = await GetAllyEntityById(groups, id)};

            foreach (var authGroup in groups.Where(a=> a.StandingsAuth != null))
            {
                var res = await GetEntityForStandingsAuth(authGroup, id, 2);
                if (res != null) return new WebAuthResult {Group = authGroup, RoleEntity = res };
            }

            return null;
        }

        public async Task ProcessPreliminaryApplicant(AuthUserEntity user)
        {
            try
            {
                var group = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == user.GroupName);
                if (group.Value == null)
                {
                    await LogHelper.LogWarning($"Group {user.GroupName} not found for character {user.Data.CharacterName} awaiting auth...");
                    return;
                }

                var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, user.GroupName, true);
                if (rChar == null) return;

                var longCorpId = rChar.corporation_id;
                var longAllyId = rChar.alliance_id ?? 0;
                if (await GetCharEntityById(group.Value, user.CharacterId) != null || await GetCorpEntityById(group.Value, longCorpId) != null || (rChar.alliance_id > 0 && await GetAllyEntityById(group.Value, longAllyId) != null))
                {
                    if (group.Value == null)
                    {
                        await LogHelper.LogWarning($"Unable to auth {user.Data.CharacterName}({user.CharacterId}) as its auth group {user.GroupName} do not exist in the settings file!",
                            Category);
                        if (Settings.WebAuthModule.AuthReportChannel != 0)
                            await APIHelper.DiscordAPI.SendMessageAsync(Settings.WebAuthModule.AuthReportChannel,
                                $"{group.Value.DefaultMention} {LM.Get("authUnableToProcessUserGroup", user.Data.CharacterName, user.CharacterId, user.GroupName)}");
                    }

                    //auth
                    await AuthUser(null, user.RegCode, user.DiscordId, false);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Auth check for {user?.Data.CharacterName}", ex, Category);
            }
        }


        private async Task ProcessPreliminaryAppilicants()
        {
            try
            {
                await LogHelper.LogModule("Running preliminary auth check...", Category);
                var list = await SQLHelper.GetAuthUsersWithPerms(1);
                foreach (var data in list.Where(a => a.DiscordId != 0))
                {
                    await ProcessPreliminaryApplicant(data);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
        }


        public static async Task<string[]> GetCharacterIdFromCode(string code, string clientID, string secret)
        {
            var result = await APIHelper.ESIAPI.GetAuthToken(code, clientID, secret);
            var accessToken = result[0];

            if (accessToken == null) return null;

            using (var authWebHttpClient = new HttpClient())
            {
                authWebHttpClient.DefaultRequestHeaders.Clear();
                authWebHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                using (var tokenresponse = await authWebHttpClient.GetAsync("https://login.eveonline.com/oauth/verify"))
                {
                    var verifyString = await tokenresponse.Content.ReadAsStringAsync();
                    if (JObject.Parse(verifyString)["error"]?.ToString() == "invalid_token")
                        return null;
                    authWebHttpClient.DefaultRequestHeaders.Clear();
                    return new[] {(string) JObject.Parse(verifyString)["CharacterID"], result[1]};
                }
            }

        }

        public async Task<bool> Auth(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleAuthWeb) return false;

            var request = context.Request;
            var response = context.Response;

            var extIp = Settings.WebServerModule.WebExternalIP;
            var extPort = Settings.WebServerModule.WebExternalPort;
            var port = Settings.WebServerModule.WebListenPort;


            if (request.HttpMethod != HttpMethod.Get.ToString())
                return false;
            try
            {
                if (request.Url.LocalPath == "/auth.php" || request.Url.LocalPath == $"{extPort}/auth.php"|| request.Url.LocalPath == $"{port}/auth.php")
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    
                    if (prms.Length == 0 || prms[0].Split('=').Length == 0 || string.IsNullOrEmpty(prms[0]))
                    {
                        await WebServerModule.WriteResponce(WebServerModule.Get404Page(), response);
                        return true;
                    }

                    var groupName = HttpUtility.UrlDecode(prms[0].Split('=')[1]);//string.IsNullOrEmpty(Settings.WebAuthModule.DefaultAuthGroup) || !Settings.WebAuthModule.AuthGroups.ContainsKey(Settings.WebAuthModule.DefaultAuthGroup) ? Settings.WebAuthModule.AuthGroups.Keys.FirstOrDefault() : Settings.WebAuthModule.DefaultAuthGroup;
                    if (!Settings.WebAuthModule.AuthGroups.ContainsKey(groupName))
                    {
                        await WebServerModule.WriteResponce(WebServerModule.Get404Page(), response);
                        return true;
                    }

                    var grp = Settings.WebAuthModule.AuthGroups[groupName];
                    var url = grp.MustHaveGroupName ? WebServerModule.GetCustomAuthUrl(grp.ESICustomAuthRoles, groupName) : WebServerModule.GetAuthUrl();

                    var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{authUrl}", url)
                        .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{body}", LM.Get("authTemplateInv")).Replace("{backText}", LM.Get("backText"));
                    await WebServerModule.WriteResponce(text, response);
                    return true;
                }

                if ((request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                    && request.Url.Query.Contains("&state=authst"))
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    var code = prms[0].Split('=')[1];
                   // var groupInput = prms.FirstOrDefault(a => a.StartsWith("authst"));
                    //groupInput = groupInput?.Substring(5, groupInput.Length - 5);

                    var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                    if (result == null)
                    {
                        var message = LM.Get("ESIFailure");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                            .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    var characterID = result[0];
                    var numericCharId = Convert.ToInt64(characterID);

                    if (string.IsNullOrEmpty(characterID))
                    {
                        await LogHelper.LogWarning("Bad or outdated stand auth request!");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{message}", LM.Get("authTokenBadRequest"))
                            .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    if (Settings.WebAuthModule.AuthGroups.Values.All(g => g.StandingsAuth == null ||  !g.StandingsAuth.CharacterIDs.Contains(numericCharId)))
                    {
                        await LogHelper.LogWarning($"Unathorized auth stands feed request from {characterID}");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{message}", LM.Get("authTokenInvalid"))
                            .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                    await SQLHelper.DeleteAuthStands(numericCharId);
                    var data = new AuthStandsEntity {CharacterID = numericCharId, Token = result[1]};

                    var token = await APIHelper.ESIAPI.RefreshToken(data.Token, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);

                    await RefreshStandings(data, token);
                    await SQLHelper.SaveAuthStands(data);
                    
                    await LogHelper.LogInfo($"Auth stands feed added for character: {characterID}({rChar.name})", LogCat.AuthWeb);
                    //TODO better screen?
                    await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifySuccess)
                        .Replace("{body2}", LM.Get("authStandsTokenRcv", rChar.name))
                        .Replace("{body}", LM.Get("authTokenRcv")).Replace("{header}", LM.Get("authStandsTokenHeader")).Replace("{backText}", LM.Get("backText")), response);
                    return true;
                }
                
                if ((request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                    && (!request.Url.Query.Contains("&state=") || request.Url.Query.Contains("&state=x")))
                {
                    var assembly = Assembly.GetEntryAssembly();
                    // var temp = assembly.GetManifestResourceNames();
                    var resource = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Discord-01.png");
                    var buffer = new byte[resource.Length];
                    resource.Read(buffer, 0, Convert.ToInt32(resource.Length));
                    var image = Convert.ToBase64String(buffer);
                    var add = false;

                    if (!string.IsNullOrWhiteSpace(request.Url.Query))
                    {
                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms[0].Split('=')[1];
                       // var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        var x = prms.Last().Split('=');
                        var inputGroupName = x.Length > 1 ? HttpUtility.UrlDecode(x[1].Substring(1, x[1].Length - 1)) : null;
                        var inputGroup = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key.Equals(inputGroupName, StringComparison.OrdinalIgnoreCase)).Value;

                        var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                        if (result == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var characterID = result?[0];

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);
                        if (rChar == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var corpID = rChar?.corporation_id ?? 0;
                        var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar?.corporation_id, true);
                        if (rCorp == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var allianceID = rCorp?.alliance_id ?? 0;

                        var cFoundList = new List<long>();
                        var groupName = string.Empty;
                        WebAuthGroup group = null;

                        //PreliminaryAuthMode
                        if (inputGroup != null && inputGroup.PreliminaryAuthMode)
                        {
                            group = inputGroup;
                            if (string.IsNullOrEmpty(result[1]))
                            {
                                await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("Auth Module", LM.Get("authNoTokenReceived")), response);
                                await LogHelper.LogWarning($"Invalid named group auth attempt (missing token) from charID: {characterID} grp: {inputGroupName}", Category);
                                return true;
                            }
                            cFoundList.Add(corpID); //fake reg ;)
                            groupName = inputGroupName;
                            add = true;
                        }
                        else //normal auth
                        {
                            var longCorpId = rChar.corporation_id;
                            var longAllyId = allianceID;

                            //has custom ESI roles
                            if (inputGroup != null)
                            {
                                group = inputGroup;
                                if (await GetCorpEntityById(group, longCorpId) != null || (allianceID != 0 && await GetAllyEntityById(group, longAllyId) != null))
                                {
                                    groupName = inputGroupName;
                                    add = true;
                                    cFoundList.Add(rChar.corporation_id);
                                }
                            }
                            else
                            {
                                //general auth
                                foreach (var grp in Settings.WebAuthModule.AuthGroups.Where(a=> !a.Value.ESICustomAuthRoles.Any() && !a.Value.PreliminaryAuthMode))
                                {
                                    if (await GetCorpEntityById(grp.Value, longCorpId) != null || (allianceID != 0 && await GetAllyEntityById(grp.Value, longAllyId) != null))
                                    {
                                        cFoundList.Add(rChar.corporation_id);
                                        groupName = grp.Key;
                                        group = grp.Value;
                                        add = true;
                                        break;
                                    }
                                }

                                //for guest mode during general auth
                                if (!add)
                                {
                                    var grp = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a =>
                                        a.Value.AllowedAlliances.Values.All(b => b.Id.All(c=> c== 0)) && a.Value.AllowedCorporations.Values.All(b => b.Id.All(c=> c == 0)));
                                    if (grp.Value != null)
                                    {
                                        add = true;
                                        groupName = grp.Key;
                                        group = grp.Value;
                                        cFoundList.Add(rChar.corporation_id);
                                    }
                                }

                            }
                        }

                        if (add)
                        {
                            //cleanup prev auth
                            await SQLHelper.DeleteAuthDataByCharId(Convert.ToInt64(characterID));
                            var refreshToken = result[1];

                            var uid = GetUniqID();
                            var authUser = new AuthUserEntity
                            {
                                CharacterId = Convert.ToInt64(characterID),
                                Data = {CharacterName = rChar.name, Permissions = group.ESICustomAuthRoles.Count > 0 ? string.Join(',', group.ESICustomAuthRoles) : null },
                                DiscordId = 0,
                                RefreshToken = refreshToken,
                                GroupName = groupName,
                                AuthState = inputGroup != null && inputGroup.PreliminaryAuthMode ? 0 : 1,
                                RegCode = uid,
                                CreateDate = DateTime.Now
                            };
                            authUser.Data.CorporationId = rChar.corporation_id;
                            authUser.Data.CorporationName = rCorp.name;
                            authUser.Data.CorporationTicker = rCorp.ticker;
                            authUser.Data.AllianceId = rChar.alliance_id ?? 0;
                            if (authUser.Data.AllianceId > 0)
                            {
                                var rAlliance = await APIHelper.ESIAPI.GetAllianceData(Reason, rChar.alliance_id, true);
                                authUser.Data.AllianceName = rAlliance.name;
                                authUser.Data.AllianceTicker = rAlliance.ticker;
                            }
                            await SQLHelper.SaveAuthUser(authUser);

                            if (!group.PreliminaryAuthMode)
                            {
                                await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2).Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                        .Replace("{image}", image)
                                        .Replace("{uid}",  $"!auth {uid}").Replace("{header}", LM.Get("authTemplateHeader"))
                                        .Replace("{body}", LM.Get("authTemplateSucc1", rChar.name))
                                        .Replace("{body2}", LM.Get("authTemplateSucc2")).Replace("{body3}", LM.Get("authTemplateSucc3")).Replace("{backText}", LM.Get("backText")),
                                    response);
                                if (SettingsManager.Settings.WebAuthModule.AuthReportChannel != 0)
                                    await APIHelper.DiscordAPI.SendMessageAsync(SettingsManager.Settings.WebAuthModule.AuthReportChannel, $"{group.DefaultMention} {LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName)}").ConfigureAwait(false);
                                await LogHelper.LogWarning(LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName), LogCat.AuthWeb);
                            }
                            else
                            {
                                await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2)
                                        .Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                        .Replace("{image}", image)
                                        .Replace("{uid}", $"!auth confirm {uid}")
                                        .Replace("{header}", LM.Get("authTemplateHeader"))
                                        .Replace("{body}", LM.Get("authTemplateManualAccept", rChar.name))
                                        .Replace("{body3}", LM.Get("authTemplateManualAccept3"))
                                        .Replace("{body2}", LM.Get("authTemplateManualAccept2"))
                                        .Replace("{backText}", LM.Get("backText")),
                                    response);

                            }
                        }
                        else
                        {
                            var message = LM.Get("authNonAlly");
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")).Replace("{body}", ""), response);
                        }


                        return true;
                    }
                }
            }

            catch (Exception ex)
            {
                await LogHelper.LogEx($"Error: {ex.Message}", ex, Category);
            }

            return false;
        }

        private async Task RefreshStandings(AuthStandsEntity data, string token)
        {
            data.PersonalStands = await APIHelper.ESIAPI.GetCharacterContacts(Reason, data.CharacterID, token);
            var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, data.CharacterID, true);
            data.CorpStands = await APIHelper.ESIAPI.GetCorpContacts(Reason, rChar?.corporation_id ?? 0, token);
            data.AllianceStands = await APIHelper.ESIAPI.GetAllianceContacts(Reason, rChar?.alliance_id ?? 0, token);
        }

        public static string GetUniqID()
        {
            //ensure we have some letters
            string result;
            do
            {
                var ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
                double t = ts.TotalMilliseconds / 1000;

                int a = (int)Math.Floor(t);
                int b = (int)((t - Math.Floor(t)) * 1000000);

                result = a.ToString("x8") + b.ToString("x5");
            } while (result.All(char.IsDigit));

            return result;
        }


        internal static async Task AuthUser(ICommandContext context, string remainder, ulong discordId, bool isManualAuth)
        {
            JsonClasses.CharacterData characterData = null;
            try
            {
                discordId = discordId > 0 ? discordId : context.Message.Author.Id;

                //check pending user validity
                var authUser = isManualAuth ? await SQLHelper.GetAuthUserByRegCode(remainder) : await SQLHelper.GetAuthUserByDiscordId(discordId);
                if (authUser == null)
                {
                    if(context != null)
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel, LM.Get("authHasInvalidKey", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true).ConfigureAwait(false);
                    return;
                }
                if(authUser.IsAuthed || string.IsNullOrEmpty(authUser.RegCode))
                {
                    if(context != null)
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel,LM.Get("authHasInactiveKey", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true).ConfigureAwait(false);
                    return;
                }
               

                //check if we fit some group
                var result = await APIHelper.DiscordAPI.GetRoleGroup(authUser.CharacterId, discordId, isManualAuth);
                var groupName = result?.GroupName;
                //pass auth
                if (!string.IsNullOrEmpty(groupName))
                {
                    var group = SettingsManager.Settings.WebAuthModule.AuthGroups[groupName];
                    var channel = context?.Channel?.Id ?? SettingsManager.Settings.WebAuthModule.AuthReportChannel;
                    characterData = await APIHelper.ESIAPI.GetCharacterData("Auth", authUser.CharacterId);
                    
                    //report to discord
                    var reportChannel = SettingsManager.Settings.WebAuthModule.AuthReportChannel;
                    if (reportChannel != 0)
                    {
                        var mention = group.DefaultMention;
                        if (group.PreliminaryAuthMode)
                            await APIHelper.DiscordAPI.SendMessageAsync(reportChannel, $"{mention} {LM.Get("grantRolesPrelMessage", characterData.name, groupName)}")
                                .ConfigureAwait(false);
                        else
                            await APIHelper.DiscordAPI.SendMessageAsync(reportChannel, $"{mention} {LM.Get("grantRolesMessage", characterData.name)}")
                                .ConfigureAwait(false);
                    }
                    await LogHelper.LogInfo($"Granting roles to {characterData.name} {(group.PreliminaryAuthMode ? $"[AUTO-AUTH from {groupName}]" : $"[MANUAL-AUTH {groupName}]")}", LogCat.AuthCheck);

                    //disable pending user
                    await SQLHelper.SetAuthUsersRegCode(authUser.CharacterId, null);
                   
                    //insert new authUsers


                    //remove all prevoius users associated with discordID or charID
                    if (discordId > 0)
                    {
                        await SQLHelper.DeleteAuthUsers(discordId);
                        await SQLHelper.DeleteAuthDataByCharId(authUser.CharacterId);
                    }

                    authUser.CharacterId = authUser.CharacterId;
                    authUser.Data.CharacterName = characterData.name;
                    authUser.Data.Permissions = group.ESICustomAuthRoles.Count > 0 ? string.Join(',', group.ESICustomAuthRoles) : null;
                    authUser.DiscordId = discordId;
                    authUser.GroupName = groupName;
                    authUser.AuthState = 2;

                    if (authUser.Data.CorporationId == 0)
                    {
                        var rCorp = await APIHelper.ESIAPI.GetCorporationData("AuthUser", characterData.corporation_id, true);
                        authUser.Data.CorporationId = characterData.corporation_id;
                        authUser.Data.CorporationName = rCorp.name;
                        authUser.Data.CorporationTicker = rCorp.ticker;
                        authUser.Data.AllianceId = characterData.alliance_id ?? 0;
                        if (authUser.Data.AllianceId > 0)
                        {
                            var rAlliance = await APIHelper.ESIAPI.GetAllianceData("AuthUser", characterData.alliance_id, true);
                            authUser.Data.AllianceName = rAlliance.name;
                            authUser.Data.AllianceTicker = rAlliance.ticker;
                        }
                    }

                    await SQLHelper.SaveAuthUser(authUser);

                    //run roles assignment
                    await APIHelper.DiscordAPI.UpdateUserRoles(discordId, SettingsManager.Settings.WebAuthModule.ExemptDiscordRoles,
                        SettingsManager.Settings.WebAuthModule.AuthCheckIgnoreRoles, isManualAuth);

                    //notify about success
                    if(channel != 0)
                        await APIHelper.DiscordAPI.SendMessageAsync(channel, LM.Get("msgAuthSuccess", APIHelper.DiscordAPI.GetUserMention(discordId), characterData.name));
                }
                else
                {
                    await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, "Unable to accept user as he don't fit into auth group access criteria!").ConfigureAwait(false);
                    await LogHelper.LogError("ESI Failure or No Access", LogCat.AuthWeb);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Failed adding Roles to User {characterData?.name}, Reason: {ex.Message}", ex, LogCat.AuthCheck);
            }
        }
    }
}
