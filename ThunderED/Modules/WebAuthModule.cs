using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
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
        public const string DEF_NOGROUP_NAME = "-0-";
        public const string DEF_ALTREGGROUP_NAME = "-1-";

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

        private static readonly object UpdateLock = new object();

        public override async Task Initialize()
        {
            await WebPartInitialization();
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

            lock (UpdateLock)
                ParsedMembersLists.Clear();

            //parse data
            foreach (var (key, value) in Settings.WebAuthModule.GetEnabledAuthGroups())
            {
                var aGroupDic = new Dictionary<string, Dictionary<string, List<long>>>();
                foreach (var (fKey, fValue) in value.AllowedMembers)
                {
                    var aData = await ParseMemberDataArray(fValue.Entities
                        .Where(a => (a is long i && i != 0) || (a is string s && s != string.Empty)).ToList());
                    aGroupDic.Add(fKey, aData);
                }

                lock (UpdateLock)
                    ParsedMembersLists.Add(key, aGroupDic);
            }

            //we can't proceed if there was unresolved entities as this will lead to role stripping failures
            if (IsEntityInitFailed)
            {
                await LogHelper.LogError("WebAuth module has been suspended due to errors in resolving specified char/corp/alliance entities! Please actualize auth config or restart the bot if this is an ESI issue.", Category);
                if (Settings.WebAuthModule.AuthReportChannel > 0)
                    await APIHelper.DiscordAPI.SendMessageAsync(Settings.WebAuthModule.AuthReportChannel,
                        LM.Get("initialEntityParseError"));

            }

        }

        public override async Task Run(object prm)
        {
            if (!Settings.Config.ModuleAuthWeb || IsEntityInitFailed) return;

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
                        foreach (var numericCharId in Settings.WebAuthModule.GetEnabledAuthGroups().Values.Where(a => a.StandingsAuth != null).SelectMany(a => a.StandingsAuth.CharacterIDs)
                            .Distinct())
                        {
                            var st = await SQLHelper.LoadAuthStands(numericCharId);
                            if (st == null) return;
                            var tq = await APIHelper.ESIAPI.RefreshToken(st.Token, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret
                                , $"From {Category} | Char ID: {st.CharacterID}");
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
            groups = groups ?? SettingsManager.Settings.WebAuthModule.GetEnabledAuthGroups();
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
                        List<long> data;
                        lock (UpdateLock)
                            data = Instance.GetTier2CharacterIds(Instance.ParsedMembersLists, groupName, entityName);
                        if (data.Contains(chData.character_id))
                            result.RoleEntities.Add(entity);
                        else
                        {
                            lock (UpdateLock)
                                data = Instance.GetTier2CorporationIds(Instance.ParsedMembersLists, groupName, entityName);
                            if (data.Contains(chData.corporation_id))
                                result.RoleEntities.Add(entity);
                            else
                            {
                                lock (UpdateLock)
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
                            else
                            {
                                await AuthInfoLog(chData, $"[GARE] Found pre-match in {groupName}|{entityName}. Continue search...", true);
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
                if (standings == null) continue;

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
            groups = groups ?? SettingsManager.Settings.WebAuthModule.GetEnabledAuthGroups();
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
                        SettingsManager.Settings.WebServerModule.CcpAppSecret, $"From WebAuth | Char ID: {authUser.CharacterId} | Char name: {authUser.Data.CharacterName}"))?.Result;
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
