using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public partial class WebAuthModule: AppModuleBase
    {
        private DateTime _lastTimersCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthWeb;
        private static int _standsInterval;
        private static DateTime _lastStandsUpdateDate = DateTime.MinValue;

        protected readonly Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>> ParsedMembersLists = new Dictionary<string, Dictionary<string, Dictionary<string, List<long>>>>();

        public static WebAuthModule Instance { get; protected set; }

        public WebAuthModule()
        {
            LogHelper.LogModule("Initializing WebAuth module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, Auth);
            Instance = this;
        }

        public override async Task Initialize()
        {
            //check entities
            foreach (var (groupName, group) in Settings.WebAuthModule.AuthGroups)
            {
                var keys = group.AllowedMembers.GetDupeKeys();
                if (keys.Any())
                    await LogHelper.LogWarning(
                        $"Group {groupName} contains duplicate member entries {string.Join(',', keys)}! Set unique names to avoid inconsistency during auth checks!", Category);
                await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(group.AllowedMembers.Values.SelectMany(a => a.DiscordRoles).Distinct().ToList(), Category);
                await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(group.ManualAssignmentRoles, Category);
                await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(group.AuthRoles, Category);
                if (group.StandingsAuth != null)
                {
                    await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(group.StandingsAuth.StandingFilters.Values.SelectMany(a=> a.DiscordRoles).Distinct().ToList(), Category);
                }
            }

            //parse data
            foreach (var (key, value) in Settings.WebAuthModule.AuthGroups)
            {
                var aGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                foreach (var (fKey, fValue) in value.AllowedMembers)
                {
                    var aData = await ParseMemberDataArray(fValue.Entities.Where(a => (a is long i && i != 0) || (a is string s && s != string.Empty)).ToList());
                    aGroupDic.Add(fKey, aData);
                }
                ParsedMembersLists.Add(key, aGroupDic);
            }
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

                if ((DateTime.Now - _lastTimersCheck).TotalMinutes < 5 ) return;
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
            var list = await SQLHelper.GetOutdatedAwaitingAuthUsers();
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
                    if (Settings.Config.ModuleHRM && Settings.HRMModule.UseDumpForMembers)
                    {
                        user.SetStateDumpster();
                        await SQLHelper.SaveAuthUser(user);
                    }
                    else await SQLHelper.DeleteAuthDataByCharId(user.CharacterId);
                }
            }
        }

        public static async Task<AuthRoleEntity> GetCorpEntityById(Dictionary<string, WebAuthGroup> groups, long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups;
            foreach (var (groupName, group) in groups)
            {
                if (group.StandingsAuth == null)
                {
                    foreach (var (entityName, entity) in group.AllowedMembers)
                    {
                        var list = Instance.GetTier2CorporationIds(Instance.ParsedMembersLists, groupName, entityName);
                        if (list.Contains(id))
                            return entity;
                    }
                }
                else
                {
                    return await GetEntityForStandingsAuth(group, id, 1);
                }
            }

            return null;
        }

        public static async Task<AuthRoleEntity> GetCharEntityById(Dictionary<string, WebAuthGroup> groups, long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups;
            foreach (var (groupName, group) in groups)
            {
                if (group.StandingsAuth == null)
                {
                    foreach (var (entityName, entity) in group.AllowedMembers)
                    {
                        var list = Instance.GetTier2CharacterIds(Instance.ParsedMembersLists, groupName, entityName);
                        if (list.Contains(id))
                            return entity;
                    }
                }
                else
                {
                    return await GetEntityForStandingsAuth(group, id, 0);
                }
            }

            return null;
        }

        public static async Task<AuthRoleEntity> GetAllyEntityById(Dictionary<string, WebAuthGroup> groups,long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups;
            foreach (var (groupName, group) in groups)
            {
                if (group.StandingsAuth == null)
                {
                    foreach (var (entityName, entity) in group.AllowedMembers)
                    {
                        var list = Instance.GetTier2AllianceIds(Instance.ParsedMembersLists, groupName, entityName);
                        if (list.Contains(id))
                            return entity;
                    }
                }
                else
                {
                    return await GetEntityForStandingsAuth(group, id, 2);
                }
            }

            return null;
        }

        public static async Task<AuthRoleEntity> GetAllyEntityById(KeyValuePair<string, WebAuthGroup> group, long id)
        {
            var (key, value) = @group;
            return await GetAllyEntityById(new Dictionary<string, WebAuthGroup> {{key, value}}, id);
        }

        public static async Task<AuthRoleEntity> GetCorpEntityById(KeyValuePair<string, WebAuthGroup> group, long id)
        {
            var (key, value) = @group;
            return await GetCorpEntityById(new Dictionary<string, WebAuthGroup> {{key, value}}, id);
        }

        public static async Task<AuthRoleEntity> GetCharEntityById(KeyValuePair<string, WebAuthGroup> group, long id)
        {
            var (key, value) = @group;
            return await GetCharEntityById(new Dictionary<string, WebAuthGroup> {{key, value}}, id);
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
                if (s != null) return new AuthRoleEntity {Entities = new List<object> {id}, DiscordRoles = s.DiscordRoles};

                s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "le").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c <= b)));
                if (s != null) return new AuthRoleEntity {Entities = new List<object>  {id}, DiscordRoles = s.DiscordRoles};
                s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "ge").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c >= b)));
                if (s != null) return new AuthRoleEntity {Entities = new List<object>  {id}, DiscordRoles = s.DiscordRoles};

                s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "lt").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c < b)));
                if (s != null) return new AuthRoleEntity {Entities = new List<object>  {id}, DiscordRoles = s.DiscordRoles};
                s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "gt").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c > b)));
                if (s != null) return new AuthRoleEntity {Entities = new List<object>  {id}, DiscordRoles = s.DiscordRoles};


            }

            return null;
        }

        public static async Task<WebAuthResult> GetAuthGroupByCorpId(Dictionary<string, WebAuthGroup> groups, long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups;
            var eResult = await GetCorpEntityById(groups, id);
            var result = eResult == null ? null : groups.FirstOrDefault(a => a.Value.AllowedMembers.ContainsValue(eResult)).Value;
            if (result != null) return new WebAuthResult {Group = result, RoleEntity = eResult};

            foreach (var (groupName, group) in groups.Where(a=> a.Value.StandingsAuth != null))
            {
                var res = await GetEntityForStandingsAuth(group, id, 1);
                if (res != null) return new WebAuthResult {Group = group, RoleEntity = res };
            }

            return null;
        }

        public static async Task<WebAuthResult> GetAuthGroupByCharacterId(Dictionary<string, WebAuthGroup> groups, long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups;
            var eResult = await GetCharEntityById(groups, id);
            var result = eResult == null ? null : groups.FirstOrDefault(a => a.Value.AllowedMembers.ContainsValue(eResult)).Value;
            if (result != null) return new WebAuthResult {Group = result, RoleEntity = eResult};

            foreach (var (groupName, group) in groups.Where(a=> a.Value.StandingsAuth != null))
            {
                var res = await GetEntityForStandingsAuth(group, id, 0);
                if (res != null) return new WebAuthResult {Group = group, RoleEntity = res };
            }

            return null;
        }


        public static async Task<WebAuthResult> GetAuthGroupByAllyId(Dictionary<string, WebAuthGroup>groups, long id)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups;
            var eResult = await GetAllyEntityById(groups, id);
            var result = eResult == null ? null : groups.FirstOrDefault(a => a.Value.AllowedMembers.ContainsValue(eResult)).Value;
            if (result != null) return new WebAuthResult {Group = result, RoleEntity = eResult};

            foreach (var (groupName, group) in groups.Where(a=> a.Value.StandingsAuth != null))
            {
                var res = await GetEntityForStandingsAuth(group, id, 2);
                if (res != null) return new WebAuthResult {Group = group, RoleEntity = res };
            }

            return null;
        }

        public async Task ProcessPreliminaryApplicant(AuthUserEntity user, ICommandContext context = null)
        {
            try
            {
                var group = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == user.GroupName);
                if (group.Value == null)
                {
                    await LogHelper.LogWarning($"Group {user.GroupName} not found for character {user.Data.CharacterName} awaiting auth...");
                    return;
                }

                var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, user.CharacterId, true);
                if (rChar == null) return;

                if (user.Data.CorporationId != rChar.corporation_id || user.Data.AllianceId != rChar.alliance_id)
                {
                    await user.UpdateData(rChar);
                    await SQLHelper.SaveAuthUser(user);
                }

                var longCorpId = rChar.corporation_id;
                var longAllyId = rChar.alliance_id ?? 0;
                if (await GetCharEntityById(group, user.CharacterId) != null || await GetCorpEntityById(group, longCorpId) != null || (rChar.alliance_id > 0 && await GetAllyEntityById(group, longAllyId) != null))
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
                    await AuthUser(context, user.RegCode, user.DiscordId, false);
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
                var list = await SQLHelper.GetAuthUsersWithPerms((int)UserStatusEnum.Awaiting);
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
            var port = Settings.WebServerModule.WebExternalPort;


            if (request.HttpMethod != HttpMethod.Get.ToString())
                return false;
            try
            {
                if (request.Url.LocalPath == "/auth" || request.Url.LocalPath == $"{extPort}/auth"|| request.Url.LocalPath == $"{port}/auth")
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    
                    if (prms.Length == 0 || prms[0].Split('=').Length == 0 || string.IsNullOrEmpty(prms[0]))
                    {
                        await WebServerModule.WriteResponce(WebServerModule.Get404Page(), response);
                        return true;
                    }

                    var groupName = HttpUtility.UrlDecode(prms[0].Split('=')[1]);//string.IsNullOrEmpty(Settings.WebAuthModule.DefaultAuthGroup) || !Settings.WebAuthModule.AuthGroups.ContainsKey(Settings.WebAuthModule.DefaultAuthGroup) ? Settings.WebAuthModule.AuthGroups.Keys.FirstOrDefault() : Settings.WebAuthModule.DefaultAuthGroup;
                    if (!Settings.WebAuthModule.AuthGroups.ContainsKey(groupName) && !DEF_NOGROUP_NAME.Equals(groupName)&& !DEF_ALTREGGROUP_NAME.Equals(groupName))
                    {
                        await WebServerModule.WriteResponce(WebServerModule.Get404Page(), response);
                        return true;
                    }

                    if (!Settings.WebAuthModule.AuthGroups.ContainsKey(groupName) && DEF_NOGROUP_NAME.Equals(groupName))
                    {
                        var url = WebServerModule.GetAuthUrlOneButton();
                        await response.RedirectAsync(new Uri(url));
                    }
                    else if (!Settings.WebAuthModule.AuthGroups.ContainsKey(groupName) && DEF_ALTREGGROUP_NAME.Equals(groupName))
                    {

                        var url = WebServerModule.GetAuthUrlAltRegButton();
                        var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{authUrl}", url)
                            .Replace("{authButtonDiscordText}", LM.Get("authAltRegTemplateHeader"))
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{header}", LM.Get("authAltRegTemplateHeader")).Replace("{body}", LM.Get("authAltRegBody")).Replace("{backText}", LM.Get("backText"));
                        await WebServerModule.WriteResponce(text, response);

                        //await response.RedirectAsync(new Uri(url));
                    }
                    else
                    {
                        var grp = Settings.WebAuthModule.AuthGroups[groupName];
                        var url = grp.MustHaveGroupName || (Settings.WebAuthModule.UseOneAuthButton && grp.ExcludeFromOneButtonMode)
                            ? WebServerModule.GetCustomAuthUrl(grp.ESICustomAuthRoles, groupName)
                            : WebServerModule.GetAuthUrl();

                        var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{authUrl}", url)
                            .Replace("{authButtonDiscordText}", LM.Get("authButtonDiscordText"))
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{body}", LM.Get("authTemplateInv")).Replace("{backText}", LM.Get("backText"));
                        await WebServerModule.WriteResponce(text, response);
                    }

                    return true;
                }

                if ((request.Url.LocalPath == "/callback" || request.Url.LocalPath == $"{extPort}/callback" || request.Url.LocalPath == $"{port}/callback")
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
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{header}", LM.Get("authTemplateHeader"))
                            .Replace("{backUrl}", WebServerModule.GetAuthLobbyUrl())
                            .Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    var characterID = result[0];
                    var numericCharId = Convert.ToInt64(characterID);

                    if (string.IsNullOrEmpty(characterID))
                    {
                        await LogHelper.LogWarning("Bad or outdated stand auth request!");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{message}", LM.Get("authTokenBadRequest"))
                            .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    if (Settings.WebAuthModule.AuthGroups.Values.All(g => g.StandingsAuth == null ||  !g.StandingsAuth.CharacterIDs.Contains(numericCharId)))
                    {
                        await LogHelper.LogWarning($"Unathorized auth stands feed request from {characterID}");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
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
                        .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                        .Replace("{body2}", LM.Get("authStandsTokenRcv", rChar.name))
                        .Replace("{body}", LM.Get("authTokenRcv")).Replace("{header}", LM.Get("authStandsTokenHeader")).Replace("{backText}", LM.Get("backText")), response);
                    return true;
                }
                
                if ((request.Url.LocalPath == "/callback" || request.Url.LocalPath == $"{extPort}/callback" || request.Url.LocalPath == $"{port}/callback")
                    && (!request.Url.Query.Contains("&state=") || request.Url.Query.Contains("&state=x") || request.Url.Query.Contains("&state=oneButton") || request.Url.Query.Contains("&state=altReg") ))
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
                        var prms = QueryHelpers.ParseQuery(request.Url.Query);
                       // var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms.ContainsKey("code") ? prms["code"].LastOrDefault() : null;
                        var state = prms.ContainsKey("state") ? prms["state"].LastOrDefault() : null;
                        var mainCharId = 0L;
                        if (state?.Contains('|') ?? false)
                        {
                            var lst = state.Split('|');
                            state = lst[0];
                            long.TryParse(lst[1], out mainCharId);
                        }

                        var inputGroupName = state?.Length > 1 ? HttpUtility.UrlDecode(state.Substring(1, state.Length - 1)) : null;
                        var inputGroup = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key.Equals(inputGroupName, StringComparison.OrdinalIgnoreCase)).Value;
                        var autoSearchGroup = inputGroup == null && (state?.Equals("oneButton") ?? false);
                        var altCharReg = inputGroup == null && (state?.Equals("altReg") ?? false);

                        var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                        if (result == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                .Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{backUrl}", WebServerModule.GetAuthLobbyUrl())
                                .Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var characterID = result?[0];

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);
                        if (rChar == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                .Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{backUrl}", WebServerModule.GetAuthLobbyUrl())
                                .Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var longCharacterId = Convert.ToInt64(characterID);

                        var corpID = rChar?.corporation_id ?? 0;
                        var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar?.corporation_id, true);
                        if (rCorp == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                .Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{backUrl}", WebServerModule.GetAuthLobbyUrl())
                                .Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var allianceID = rCorp?.alliance_id ?? 0;

                        var cFoundList = new List<long>();
                        var groupName = string.Empty;
                        WebAuthGroup group = null;

                        //alt character registration check
                        if (altCharReg)
                        {
                            var user = await SQLHelper.GetAuthUserByCharacterId(longCharacterId);
                            //do not allow to bind alt to another alt
                            if (user == null || !user.IsAuthed || user.MainCharacterId.HasValue || user.DiscordId == 0)
                            {
                                await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage(LM.Get("authAltRegTemplateHeader"), LM.Get("authAltRegMainNotFound"), WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}", Category);
                                return true;
                            }

                            var pair = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value.BindToMainCharacter);
                            if (pair.Value == null)
                            {
                                await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage(LM.Get("authAltRegTemplateHeader"), LM.Get("authAltRegGroupNotFound"), WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}", Category);
                                return true;
                            }

                            group = pair.Value;
                            groupName = pair.Key;
                            var url = group.ESICustomAuthRoles.Any()
                                ? WebServerModule.GetCustomAuthUrl(group.ESICustomAuthRoles, user.GroupName, longCharacterId)
                                : WebServerModule.GetAuthUrl(groupName, longCharacterId);
                            await response.RedirectAsync(new Uri(url));

                            return true;
                        }

                        if (mainCharId > 0)
                        {
                            var refreshToken = result[1];
                            var altCharId = longCharacterId;
                            var user = await SQLHelper.GetAuthUserByCharacterId(mainCharId);
                            //do not allow to bind alt to another alt
                            if (user == null || !user.IsAuthed || user.MainCharacterId.HasValue || user.DiscordId == 0)
                            {
                                await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage(LM.Get("authAltRegTemplateHeader"), LM.Get("authAltRegMainNotFound"), WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}", Category);
                                return true;
                            }
                            var pair = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value.BindToMainCharacter);
                            if (pair.Value == null)
                            {
                                await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage(LM.Get("authAltRegTemplateHeader"), LM.Get("authAltRegGroupNotFound"), WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}", Category);
                                return true;
                            }

                            group = pair.Value;
                            groupName = pair.Key;

                            var altUser = await AuthUserEntity.CreateAlt(altCharId, refreshToken, group, groupName, mainCharId);
                            await SQLHelper.SaveAuthUser(altUser);

                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2)
                                    .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                    .Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                    .Replace("{image}", image)
                                    .Replace("{uid}", null)
                                    .Replace("{header}", LM.Get("authAltRegTemplateHeader"))
                                    .Replace("{body}", LM.Get("authAltRegAccepted", rChar.name, user.Data.CharacterName))
                                    .Replace("{body3}", null)
                                    .Replace("{body2}", null)
                                    .Replace("{backText}", LM.Get("backText")),
                                response);

                            return true;
                        }


                        //PreliminaryAuthMode
                        if (inputGroup != null && inputGroup.PreliminaryAuthMode)
                        {
                            group = inputGroup;
                            if (string.IsNullOrEmpty(result[1]))
                            {
                                await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("Auth Module", LM.Get("authNoTokenReceived"), WebServerModule.GetAuthPageUrl()), response);
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

                            //has custom ESI roles or name specified
                            if (inputGroup != null)
                            {
                                group = inputGroup;
                                var groupPair = new Dictionary<string, WebAuthGroup> {{inputGroupName, inputGroup}};

                                //guest check
                                if (group.AllowedMembers.Values.All(b => !b.Entities.Any() || b.Entities.All(c => c.ToString().All(char.IsDigit) && (long)c == 0)))
                                {
                                    add = true;
                                    groupName = inputGroupName;
                                    cFoundList.Add(rChar.corporation_id);
                                }
                                else
                                {
                                    if (await GetCorpEntityById(groupPair, longCorpId) != null || (allianceID != 0 && await GetAllyEntityById(groupPair, longAllyId) != null) ||
                                        await GetCharEntityById(groupPair, longCharacterId) != null)
                                    {
                                        groupName = inputGroupName;
                                        add = true;
                                        cFoundList.Add(rChar.corporation_id);
                                    }
                                }
                            }
                            else
                            {
                                var searchFor = autoSearchGroup
                                    ? Settings.WebAuthModule.AuthGroups.Where(a=> !a.Value.ExcludeFromOneButtonMode && !a.Value.BindToMainCharacter)
                                    : Settings.WebAuthModule.AuthGroups.Where(a => !a.Value.ESICustomAuthRoles.Any() && !a.Value.PreliminaryAuthMode && !a.Value.BindToMainCharacter);
                                //general auth
                                foreach (var grp in searchFor)
                                {
                                    if (await GetCorpEntityById(grp, longCorpId) != null || (allianceID != 0 && await GetAllyEntityById(grp, longAllyId) != null) || await GetCharEntityById(grp, longCharacterId) != null)
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
                                    var grp = Settings.WebAuthModule.AuthGroups.Where(a=> !a.Value.BindToMainCharacter).FirstOrDefault(a => a.Value.StandingsAuth == null &&
                                        a.Value.AllowedMembers.Values.All(b => b.Entities.All(c=> c.ToString().All(char.IsDigit) && (long)c== 0)));
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
                            if (autoSearchGroup && group.ESICustomAuthRoles.Any()) //for one button with ESI - had to auth twice
                            {
                                await response.RedirectAsync(new Uri(WebServerModule.GetCustomAuthUrl(group.ESICustomAuthRoles, groupName)));
                                return true;
                            }

                            //cleanup prev auth
                            await SQLHelper.DeleteAuthDataByCharId(Convert.ToInt64(characterID));
                            var refreshToken = result[1];

                            var uid = GetUniqID();
                            var authUser = new AuthUserEntity
                            {
                                CharacterId = Convert.ToInt64(characterID),
                                Data = { Permissions = group.ESICustomAuthRoles.Count > 0 ? string.Join(',', group.ESICustomAuthRoles) : null },
                                DiscordId = 0,
                                RefreshToken = refreshToken,
                                GroupName = groupName,
                                AuthState = inputGroup != null && inputGroup.PreliminaryAuthMode ? 0 : 1,
                                RegCode = uid,
                                CreateDate = DateTime.Now
                            };
                            await authUser.UpdateData();
                            await SQLHelper.SaveAuthUser(authUser);

                            if (!group.PreliminaryAuthMode)
                            {
                                await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2).Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                        .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                        .Replace("{image}", image)
                                        .Replace("{uid}",  $"!auth {uid}").Replace("{header}", LM.Get("authTemplateHeader"))
                                        .Replace("{body}", LM.Get("authTemplateSucc1", rChar.name))
                                        .Replace("{body2}", LM.Get("authTemplateSucc2")).Replace("{body3}", LM.Get("authTemplateSucc3"))
                                        .Replace("{backText}", LM.Get("backText")),
                                    response);
                                if (SettingsManager.Settings.WebAuthModule.AuthReportChannel != 0)
                                    await APIHelper.DiscordAPI.SendMessageAsync(SettingsManager.Settings.WebAuthModule.AuthReportChannel, $"{group.DefaultMention} {LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName)}").ConfigureAwait(false);
                                await LogHelper.LogWarning(LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName), LogCat.AuthWeb);
                            }
                            else
                            {
                                if (!group.SkipDiscordAuthPage)
                                    await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2)
                                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                            .Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                            .Replace("{image}", image)
                                            .Replace("{uid}", $"!auth confirm {uid}")
                                            .Replace("{header}", LM.Get("authTemplateHeader"))
                                            .Replace("{body}", LM.Get("authTemplateManualAccept", rChar.name))
                                            .Replace("{body3}", LM.Get("authTemplateManualAccept3"))
                                            .Replace("{body2}", LM.Get("authTemplateManualAccept2"))
                                            .Replace("{backUrl}", WebServerModule.GetWebSiteUrl())
                                            .Replace("{backText}", LM.Get("backText")),
                                        response);
                                else 
                                    await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2)
                                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                            .Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                            .Replace("{image}", image)
                                            .Replace("{uid}", null)
                                            .Replace("{header}", LM.Get("authTemplateHeaderShort"))
                                            .Replace("{body}", LM.Get("authTemplateManualAccept", rChar.name))
                                            .Replace("{body3}", LM.Get("authTemplateManualAccept3"))
                                            .Replace("{body2}", null)
                                            .Replace("{backText}", LM.Get("backText")
                                            .Replace("{backUrl}", WebServerModule.GetWebSiteUrl())),
                                        response);

                            }
                        }
                        else
                        {
                            var message = LM.Get("authNonAlly");
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                                .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                .Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{backText}", LM.Get("backText"))
                                .Replace("{backUrl}", WebServerModule.GetAuthLobbyUrl())
                                .Replace("{body}", ""), response);
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

        public const string DEF_NOGROUP_NAME = "-0-";
        public const string DEF_ALTREGGROUP_NAME = "-1-";

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
                var authUser = !string.IsNullOrEmpty(remainder) ? await SQLHelper.GetAuthUserByRegCode(remainder) : await SQLHelper.GetAuthUserByDiscordId(discordId);
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

                if (authUser.Data.PermissionsList.Any())
                {
                    var token = await APIHelper.ESIAPI.RefreshToken(authUser.RefreshToken, SettingsManager.Settings.WebServerModule.CcpAppClientId,
                        SettingsManager.Settings.WebServerModule.CcpAppSecret);
                    //delete char if token is invalid
                    if (string.IsNullOrEmpty(token))
                    {
                        await SQLHelper.DeleteAuthDataByCharId(authUser.CharacterId);
                        return;
                    }
                }
               

                //check if we fit some group
                var result = await GetRoleGroup(authUser.CharacterId, discordId, isManualAuth, authUser.RefreshToken);
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

                    //remove all prevoius users associated with discordID or charID
                    List<long> altCharIds = null;
                    if (discordId > 0)
                    {
                        altCharIds = await SQLHelper.DeleteAuthDataByDiscordId(discordId);
                        await SQLHelper.DeleteAuthDataByCharId(authUser.CharacterId);
                    }

                    authUser.CharacterId = authUser.CharacterId;
                    authUser.DiscordId = discordId;
                    authUser.GroupName = groupName;
                    authUser.SetStateAuthed();
                    authUser.RegCode = null;

                    await authUser.UpdateData(group.ESICustomAuthRoles.Count > 0 ? string.Join(',', group.ESICustomAuthRoles) : null);

                    await SQLHelper.SaveAuthUser(authUser);
                    if(altCharIds?.Any() ?? false)
                        altCharIds.ForEach(async a=> await SQLHelper.UpdateMainCharacter(a, authUser.CharacterId));

                    //run roles assignment
                    await UpdateUserRoles(discordId, SettingsManager.Settings.WebAuthModule.ExemptDiscordRoles,
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
