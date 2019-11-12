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
                        $"Group {groupName} contains duplicate `AllowedMembers` names {string.Join(',', keys)}! Set unique names to avoid inconsistency during auth checks!", Category);
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
                            var tq = await APIHelper.ESIAPI.RefreshToken(st.Token, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                            var token = tq.Result;

                            if (!tq.Data.IsFailed)
                            {
                                await RefreshStandings(st, token);
                                await SQLHelper.DeleteAuthStands(numericCharId);
                                await SQLHelper.SaveAuthStands(st);
                                sb.Append($"{numericCharId},");
                            }
                            else
                            {
                                await LogHelper.LogWarning($"Token fetch error while standings update! Skipping update. {tq.Data.ErrorCode} ({tq.Data.Message})", Category);
                                if(tq.Data.IsNotValid)
                                    await LogHelper.LogWarning($"Standings update token for character {numericCharId} is invalid or outdated. Please reauth!", Category);
                            }
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
                var group = GetGroupByName(user.GroupName).Value;
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
                        await LogHelper.LogInfo($"Moving outdated applicant {user.Data.CharacterName} to dumpster...");
                        await SQLHelper.SaveAuthUser(user);
                    }
                    else await SQLHelper.DeleteAuthDataByCharId(user.CharacterId);
                }
            }
        }


        public static async Task<WebAuthResult> GetAuthRoleEntityById(Dictionary<string, WebAuthGroup> groups, JsonClasses.CharacterData chData)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups;
            var result = new WebAuthResult();
            foreach (var (groupName, group) in groups)
            {
                if (group.StandingsAuth == null)
                {
                    var nameList = new List<string>();
                    foreach (var (entityName, entity) in group.AllowedMembers)
                    {
                        //found guest
                        if (!entity.Entities.Any())
                        {
                            await AuthInfoLog(chData, $"[GARE] Found guest group {groupName}! Return OK.", true);
                            result.RoleEntities.Add(entity);
                            result.Group = group;
                            result.GroupName = groupName;
                            return result;
                        }

                        await AuthInfoLog(chData, $"[GARE] Checking {groupName}|{entityName} - FSTOP: {group.StopSearchingOnFirstMatch} ...", true);
                        var data = Instance.GetTier2CharacterIds(Instance.ParsedMembersLists, groupName, entityName);
                        if (data.Contains(chData.character_id))
                            result.RoleEntities.Add(entity);
                        else
                        {
                            data = Instance.GetTier2CorporationIds(Instance.ParsedMembersLists, groupName, entityName);
                            if (data.Contains(chData.corporation_id))
                                result.RoleEntities.Add(entity);
                            else
                            {
                                data = Instance.GetTier2AllianceIds(Instance.ParsedMembersLists, groupName, entityName);
                                if (chData.alliance_id.HasValue && data.Contains(chData.alliance_id.Value))
                                    result.RoleEntities.Add(entity);
                            }
                        }

                        //return if we have a match and don't need to check all members
                        if (result.RoleEntities.Any())
                        {
                            nameList.Add(entityName);
                            if (group.StopSearchingOnFirstMatch)
                            {
                                await AuthInfoLog(chData, $"[GARE] Found match. Return OK.", true);
                                result.Group = group;
                                result.GroupName = groupName;
                                return result;
                            }
                        }
                    }

                    //return after all members has been checked
                    if (result.RoleEntities.Any())
                    {
                        await AuthInfoLog(chData, $"[GARE] Found matches from {string.Join(',', nameList)}. Return OK.", true);
                        result.Group = group;
                        result.GroupName = groupName;
                        return result;
                    }
                }
                else
                {
                    var r = await GetEntityForStandingsAuth(group, chData);
                    if (r.Any())
                    {
                        await AuthInfoLog(chData, $"[GARE] Found match. Return OK.", true);
                        result.Group = group;
                        result.GroupName = groupName;
                        result.RoleEntities = r;
                        return result;
                    }
                }
            }

            return result;
        }

        public static async Task<WebAuthResult> GetAuthRoleEntityById(KeyValuePair<string, WebAuthGroup> group, JsonClasses.CharacterData chData)
        {
            var (key, value) = @group;
            return await GetAuthRoleEntityById(new Dictionary<string, WebAuthGroup> {{key, value}}, chData);
        }

        private static async Task<List<AuthRoleEntity>> GetEntityForStandingsAuth(WebAuthGroup group, JsonClasses.CharacterData chData) //0 personal, 1 corp, 2 ally, 3 faction
        {
            var list = new List<AuthRoleEntity>();
            foreach (var characterID in group.StandingsAuth.CharacterIDs)
            {
                var standings = await SQLHelper.LoadAuthStands(characterID);
                if (standings == null) return list;

                await AuthInfoLog(chData, $"[GARE] Checking stands from {characterID}...", true);

                for (var typeNumber = 0; typeNumber < 3; typeNumber++)
                {
                    string typeName;
                    var id = 0L;
                    switch (typeNumber)
                    {
                        case 0:
                            typeName = "character";
                            id = chData.character_id;
                            break;
                        case 1:
                            typeName = "corporation";
                            id = chData.corporation_id;
                            break;
                        case 2:
                            typeName = "alliance";
                            id = chData.alliance_id ?? 0;
                            break;
                        default:
                            return list;
                    }

                    var st = new List<JsonClasses.Contact>();
                    st.AddRange(group.StandingsAuth.UseCharacterStandings && standings.PersonalStands != null ? standings.PersonalStands : new List<JsonClasses.Contact>());
                    st.AddRange(group.StandingsAuth.UseCorporationStandings && standings.CorpStands != null ? standings.CorpStands : new List<JsonClasses.Contact>());
                    st.AddRange(group.StandingsAuth.UseAllianceStandings && standings.AllianceStands != null? standings.AllianceStands : new List<JsonClasses.Contact>());
                    await AuthInfoLog(chData, $"[GARE] Total stands to check {st.Count}...", true);

                    var stands = st.Where(a => a.contact_type == typeName && a.contact_id == id).Select(a=> a.standing).Distinct();
                    var s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "eq").FirstOrDefault(a => a.Standings.Any(b => stands.Contains(b)));
                    if (s != null) 
                        list.Add(new AuthRoleEntity {Entities = new List<object> {id}, DiscordRoles = s.DiscordRoles});
                    if (group.StopSearchingOnFirstMatch)
                        return list;

                    s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "le").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c <= b)));
                    if (s != null) list.Add( new AuthRoleEntity {Entities = new List<object>  {id}, DiscordRoles = s.DiscordRoles});
                    if (group.StopSearchingOnFirstMatch)
                        return list;

                    s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "ge").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c >= b)));
                    if (s != null) list.Add(new AuthRoleEntity {Entities = new List<object>  {id}, DiscordRoles = s.DiscordRoles});
                    if (group.StopSearchingOnFirstMatch)
                        return list;
                    s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "lt").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c < b)));
                    if (s != null) list.Add(new AuthRoleEntity {Entities = new List<object>  {id}, DiscordRoles = s.DiscordRoles});
                    if (group.StopSearchingOnFirstMatch)
                        return list;
                    s = group.StandingsAuth.StandingFilters.Values.Where(a => a.Modifier == "gt").FirstOrDefault(a => a.Standings.Any(b => stands.Any(c => c > b)));
                    if (s != null) list.Add(new AuthRoleEntity {Entities = new List<object>  {id}, DiscordRoles = s.DiscordRoles});
                    if (group.StopSearchingOnFirstMatch)
                        return list;
                }
            }

            return list;
        }

        private static async Task<WebAuthResult> GetAuthGroupByCharacter(Dictionary<string, WebAuthGroup> groups, JsonClasses.CharacterData chData)
        {
            groups = groups ?? SettingsManager.Settings.WebAuthModule.AuthGroups;
            var result = await GetAuthRoleEntityById(groups, chData);
            return result.RoleEntities.Any() ? new WebAuthResult {GroupName = result.GroupName, Group = result.Group, RoleEntities = result.RoleEntities} : null;
        }
        
        public async Task ProcessPreliminaryApplicant(AuthUserEntity user, ICommandContext context = null)
        {
            try
            {
                var group = GetGroupByName(user.GroupName);
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

                // var longCorpId = rChar.corporation_id;
                //var longAllyId = rChar.alliance_id ?? 0;
                if ((await GetAuthRoleEntityById(group, rChar)).RoleEntities.Any())
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
                    await AuthUser(context, user.RegCode, user.DiscordId);
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

            var remoteAddress = HttpUtility.UrlEncode(Convert.ToBase64String(Encoding.UTF8.GetBytes($"{request.RemoteEndpoint.Address}:{request.RemoteEndpoint.Port}")));

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
                    if (!Settings.WebAuthModule.AuthGroups.Keys.ContainsCaseInsensitive(groupName) && !DEF_NOGROUP_NAME.Equals(groupName)&& !DEF_ALTREGGROUP_NAME.Equals(groupName))
                    {
                        await WebServerModule.WriteResponce(WebServerModule.Get404Page(), response);
                        return true;
                    }

                    if (!Settings.WebAuthModule.AuthGroups.Keys.ContainsCaseInsensitive(groupName) && DEF_NOGROUP_NAME.Equals(groupName))
                    {
                        var url = WebServerModule.GetAuthUrlOneButton(remoteAddress);
                        await response.RedirectAsync(new Uri(url));
                    }
                    else if (!Settings.WebAuthModule.AuthGroups.Keys.ContainsCaseInsensitive(groupName) && DEF_ALTREGGROUP_NAME.Equals(groupName))
                    {

                        var url = WebServerModule.GetAuthUrlAltRegButton(remoteAddress);
                        var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{authUrl}", url)
                            .Replace("{authButtonDiscordText}", LM.Get("authAltRegTemplateHeader"))
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{header}", LM.Get("authAltRegTemplateHeader")).Replace("{body}", LM.Get("authAltRegBody")).Replace("{backText}", LM.Get("backText"));
                        await WebServerModule.WriteResponce(text, response);

                        //await response.RedirectAsync(new Uri(url));
                    }
                    else
                    {
                        var grp = GetGroupByName(groupName).Value;
                        var url = grp.MustHaveGroupName || (Settings.WebAuthModule.UseOneAuthButton && grp.ExcludeFromOneButtonMode)
                            ? WebServerModule.GetCustomAuthUrl(remoteAddress, grp.ESICustomAuthRoles, groupName)
                            : WebServerModule.GetAuthUrl(remoteAddress);

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

                    if (Settings.WebAuthModule.AuthGroups.Values.All(g => g.StandingsAuth == null || !g.StandingsAuth.CharacterIDs.Contains(numericCharId)))
                    {
                        await LogHelper.LogWarning($"Unauthorized auth stands feed request from {characterID}");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{message}", LM.Get("authTokenInvalid"))
                            .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")).Replace("{backText}", LM.Get("backText")), response);
                        return true;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                    await SQLHelper.DeleteAuthStands(numericCharId);
                    var data = new AuthStandsEntity {CharacterID = numericCharId, Token = result[1]};

                    var tq = await APIHelper.ESIAPI.RefreshToken(data.Token, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                    var token = tq.Result;

                    if(!tq.Data.IsFailed)
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
                        string ip = null;
                        string rawIp = null;
                        if (state?.Contains('|') ?? false)
                        {
                            var lst = state.Split('|');
                            state = lst[0];
                            long.TryParse(lst[1], out mainCharId);
                            rawIp = lst.LastOrDefault();
                            ip = Encoding.UTF8.GetString(Convert.FromBase64String(HttpUtility.UrlDecode(lst.LastOrDefault())));
                        }

                        var inputGroupName = state?.Length > 1 ? HttpUtility.UrlDecode(state.Substring(1, state.Length - 1)) : null;
                        var inputGroup = GetGroupByName(inputGroupName).Value;
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
                                ? WebServerModule.GetCustomAuthUrl(rawIp, group.ESICustomAuthRoles, user.GroupName, longCharacterId)
                                : WebServerModule.GetAuthUrl(rawIp, groupName, longCharacterId);
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
                            altUser.Ip = ip;
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
                            //ordinary named group check
                            if (inputGroup != null)
                            {
                                group = inputGroup;
                                if ((await GetAuthRoleEntityById(new KeyValuePair<string, WebAuthGroup>(inputGroupName, inputGroup), rChar)).RoleEntities.Any())
                                {
                                    groupName = inputGroupName;
                                    add = true;
                                    cFoundList.Add(rChar.corporation_id);
                                }
                            }
                            else
                            {
                                //check all the shit if we fall here
                                if (Settings.WebAuthModule.UseOneAuthButton)
                                {
                                    var searchFor = autoSearchGroup
                                        ? Settings.WebAuthModule.AuthGroups.Where(a => !a.Value.ExcludeFromOneButtonMode && !a.Value.BindToMainCharacter)
                                        : Settings.WebAuthModule.AuthGroups.Where(a =>
                                            !a.Value.ESICustomAuthRoles.Any() && !a.Value.PreliminaryAuthMode && !a.Value.BindToMainCharacter);
                                    //general auth
                                    var gResult = await GetAuthRoleEntityById(searchFor.ToDictionary(a=> a.Key, a=> a.Value), rChar);
                                    if (gResult.RoleEntities.Any())
                                    {
                                        cFoundList.Add(rChar.corporation_id);
                                        groupName = gResult.GroupName;
                                        group = gResult.Group;
                                        add = true;
                                    }
                                }
                            }
                        }

                        if (add)
                        {
                            if (autoSearchGroup && group.ESICustomAuthRoles.Any()) //for one button with ESI - had to auth twice
                            {
                                await response.RedirectAsync(new Uri(WebServerModule.GetCustomAuthUrl(rawIp ?? remoteAddress, group.ESICustomAuthRoles, groupName)));
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
                                CreateDate = DateTime.Now,
                                Ip = ip
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
            var stands = await APIHelper.ESIAPI.GetCharacterContacts(Reason, data.CharacterID, token);
            data.PersonalStands = stands.Data.IsFailed ? data.PersonalStands : stands.Result;
            var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, data.CharacterID, true);
            if (rChar != null)
            {
                stands = await APIHelper.ESIAPI.GetCorpContacts(Reason, rChar.corporation_id, token);
                data.CorpStands = stands.Data.IsFailed ? data.CorpStands : stands.Result;
                if (rChar.alliance_id.HasValue)
                {
                    stands = await APIHelper.ESIAPI.GetAllianceContacts(Reason, rChar.alliance_id.Value, token);
                    data.AllianceStands = stands.Data.IsFailed ? data.AllianceStands : stands.Result;
                }
                else data.AllianceStands = new List<JsonClasses.Contact>();
            }
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

        private static async Task AuthInfoLog(string message, bool isOptional = false)
        {
            if(!isOptional || SettingsManager.Settings.WebAuthModule.EnableDetailedLogging)
                await LogHelper.LogInfo($"[CHK]: {message}", LogCat.AuthCheck);
        }

        private static async Task AuthInfoLog(object charId, string message, bool isOptional = false)
        {
            if(!isOptional || SettingsManager.Settings.WebAuthModule.EnableDetailedLogging)
                await LogHelper.LogInfo($"[CH{charId}]: {message}", LogCat.AuthCheck);
        }

        private static async Task AuthInfoLog(JsonClasses.CharacterData ch, string message, bool isOptional = false)
        {
            if(!isOptional || SettingsManager.Settings.WebAuthModule.EnableDetailedLogging)
                await LogHelper.LogInfo($"[{ch.character_id}|{ch.name}]: {message}", LogCat.AuthCheck);
        }
        private static async Task AuthInfoLog(AuthUserEntity ch, string message, bool isOptional = false)
        {
            if(!isOptional || SettingsManager.Settings.WebAuthModule.EnableDetailedLogging)
                await LogHelper.LogInfo($"[{ch.CharacterId}|{ch.Data.CharacterName}]: {message}", LogCat.AuthCheck);
        }

        private static async Task AuthWarningLog(object charId, string message, bool isOptional = false)
        {
            if(!isOptional || SettingsManager.Settings.WebAuthModule.EnableDetailedLogging)
                await LogHelper.LogWarning($"[CH{charId}]: {message}", LogCat.AuthCheck);
        }
        private static async Task AuthWarningLog(JsonClasses.CharacterData ch, string message, bool isOptional = false)
        {
            if(!isOptional || SettingsManager.Settings.WebAuthModule.EnableDetailedLogging)
                await LogHelper.LogWarning($"[{ch.character_id}|{ch.name}]: {message}", LogCat.AuthCheck);
        }

        private static async Task AuthWarningLog(AuthUserEntity ch, string message, bool isOptional = false)
        {
            if(!isOptional || SettingsManager.Settings.WebAuthModule.EnableDetailedLogging)
                await LogHelper.LogWarning($"[{ch.CharacterId}|{ch.Data.CharacterName}]: {message}", LogCat.AuthCheck);
        }


        internal static async Task AuthUser(ICommandContext context, string remainder, ulong discordId)
        {
            JsonClasses.CharacterData characterData = null;
            try
            {
                discordId = discordId > 0 ? discordId : context.Message.Author.Id;

                //check pending user validity
                var authUser = !string.IsNullOrEmpty(remainder) ? await SQLHelper.GetAuthUserByRegCode(remainder) : await SQLHelper.GetAuthUserByDiscordId(discordId);
                if (authUser == null)
                {
                    await AuthWarningLog(discordId, $"Failed to get authUser from `{remainder}` or by Discord ID");
                    if(context != null)
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel, LM.Get("authHasInvalidKey", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true).ConfigureAwait(false);
                    return;
                }
                if(authUser.IsAuthed || string.IsNullOrEmpty(authUser.RegCode))
                {
                    await AuthWarningLog(authUser, authUser.IsAuthed ? "User already authenticated" : "Specified reg code is empty");
                    if(context != null)
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel,LM.Get("authHasInactiveKey", SettingsManager.Settings.Config.BotDiscordCommandPrefix), true).ConfigureAwait(false);
                    return;
                }

                if (authUser.Data.PermissionsList.Any())
                {
                    var token = (await APIHelper.ESIAPI.RefreshToken(authUser.RefreshToken, SettingsManager.Settings.WebServerModule.CcpAppClientId,
                        SettingsManager.Settings.WebServerModule.CcpAppSecret))?.Result;
                    //delete char if token is invalid
                    if (string.IsNullOrEmpty(token))
                    {
                        //just reauth... if happens
                        await AuthWarningLog(authUser, $"Character has invalid token and will be deleted from DB.");
                        if(context != null)
                            await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel,LM.Get("authUnableToCompleteTryAgainLater"), true).ConfigureAwait(false);
                        await SQLHelper.DeleteAuthDataByCharId(authUser.CharacterId);
                        return;
                    }
                }
               
                characterData = await APIHelper.ESIAPI.GetCharacterData("Auth", authUser.CharacterId, true);

                //check if we fit some group
                var result = await GetRoleGroup(characterData, discordId, authUser.RefreshToken);
                if (result.IsConnectionError)
                {
                    await AuthWarningLog(authUser, $"Possible connection error while processing auth request(search for group)!");
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel,LM.Get("authUnableToCompleteTryAgainLater"), true).ConfigureAwait(false);
                    return;
                }

                await AuthInfoLog(authUser, $"GRPFETCH GROUP: {result.GroupName} ROLES: {(result.UpdatedRoles == null || !result.UpdatedRoles.Any() ? "null" : string.Join(',', result.UpdatedRoles?.Select(a=> a.Name.Replace("@", "_"))))} MANUAL: {(result.UpdatedRoles == null || !result.UpdatedRoles.Any() ? "null" : string.Join(',', result.ValidManualAssignmentRoles.Select(a=> a.Replace("@", "_"))))}", true);

                //var groupName = result?.GroupName;
                //pass auth
                if (!string.IsNullOrEmpty(result?.GroupName))
                {
                    var group = result.Group;
                    var channel = context?.Channel?.Id ?? SettingsManager.Settings.WebAuthModule.AuthReportChannel;

                    if (characterData == null)
                    {
                        await AuthWarningLog(authUser, $"Unable to get character {authUser.CharacterId} from ESI. Aborting auth.");
                        if(context != null)
                            await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel,LM.Get("authUnableToCompleteTryAgainLater"), true).ConfigureAwait(false);
                        //await SQLHelper.DeleteAuthDataByCharId(authUser.CharacterId);
                        return;
                    }
                    
                    //report to discord
                    var reportChannel = SettingsManager.Settings.WebAuthModule.AuthReportChannel;
                    if (reportChannel != 0)
                    {
                        var mention = group.DefaultMention;
                        if (group.PreliminaryAuthMode)
                            await APIHelper.DiscordAPI.SendMessageAsync(reportChannel, $"{mention} {LM.Get("grantRolesPrelMessage", characterData.name, result.GroupName)}")
                                .ConfigureAwait(false);
                        else
                            await APIHelper.DiscordAPI.SendMessageAsync(reportChannel, $"{mention} {LM.Get("grantRolesMessage", characterData.name)}")
                                .ConfigureAwait(false);
                    }

                    //remove all prevoius users associated with discordID or charID
                    List<long> altCharIds = null;
                    if (discordId > 0)
                    {
                        altCharIds = await SQLHelper.DeleteAuthDataByDiscordId(discordId);
                        await SQLHelper.DeleteAuthDataByCharId(authUser.CharacterId);
                    }

                    // authUser.CharacterId = authUser.CharacterId;
                    authUser.DiscordId = discordId > 0 ? discordId : authUser.DiscordId;
                    if (discordId == 0)
                        await AuthWarningLog(authUser, "Assigning 0 Discord ID to auth user?");
                    authUser.GroupName = result.GroupName;
                    authUser.SetStateAuthed();
                    authUser.RegCode = null;

                    await authUser.UpdateData(group.ESICustomAuthRoles.Count > 0 ? string.Join(',', group.ESICustomAuthRoles) : null);

                    await SQLHelper.SaveAuthUser(authUser);
                    if(altCharIds?.Any() ?? false)
                        altCharIds.ForEach(async a=> await SQLHelper.UpdateMainCharacter(a, authUser.CharacterId));

                    //run roles assignment
                    await AuthInfoLog(authUser, $"Running roles update for {characterData.name} {(group.PreliminaryAuthMode ? $"[AUTO-AUTH from {result.GroupName}]" : $"[MANUAL-AUTH {result.GroupName}]")}");

                    await UpdateUserRoles(discordId, SettingsManager.Settings.WebAuthModule.ExemptDiscordRoles,
                        SettingsManager.Settings.WebAuthModule.AuthCheckIgnoreRoles);

                    //notify about success
                    if(channel != 0)
                        await APIHelper.DiscordAPI.SendMessageAsync(channel, LM.Get("msgAuthSuccess", await APIHelper.DiscordAPI.GetUserMention(discordId), characterData.name));
                    await AuthInfoLog(authUser, $"Character {characterData.name} has been successfully authenticated");
                }
                else
                {
                    if(context != null)
                        await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, "Unable to accept user as he don't fit into auth group access criteria!").ConfigureAwait(false);
                    await LogHelper.LogError($"ESI Failure or No Access - auth group name not matching user data! DiscordID: {discordId}", LogCat.AuthWeb);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Failed to auth character {characterData?.name}, Reason: {ex.Message}", ex, LogCat.AuthCheck);
            }
        }

    }
}
