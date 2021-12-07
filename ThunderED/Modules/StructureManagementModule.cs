using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.Sub;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public class StructureManagementModule: AppModuleBase
    {
        public override LogCat Category => LogCat.StructureManagement;

        protected Dictionary<string, List<long>> ParsedAuthAccessMembersLists = new Dictionary<string, List<long>>();
        protected Dictionary<string, List<long>> ParsedViewAccessMembersLists = new Dictionary<string, List<long>>();
        protected Dictionary<string, Dictionary<string, List<long>>> ParsedManageAccessMembersLists = new Dictionary<string, Dictionary<string, List<long>>>();
        private static readonly object UpdateLock = new object();

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Structure Management module...", Category);

            if (WebServerModule.WebModuleConnectors.ContainsKey(Reason))
                WebServerModule.WebModuleConnectors.Remove(Reason);
            WebServerModule.WebModuleConnectors.Add(Reason, ProcessRequest);

            ParsedAuthAccessMembersLists.Clear();

            //auth access
            ParsedAuthAccessMembersLists = await ParseMixedDataArray(Settings.StructureManagementModule.AuthAccessEntities, MixedParseModeEnum.Member);
            //view access
            ParsedViewAccessMembersLists = await ParseMixedDataArray(Settings.StructureManagementModule.ViewAccessEntities, MixedParseModeEnum.Member);
            lock (UpdateLock)
                ParsedManageAccessMembersLists.Clear();
            //parse data
            foreach (var (key, value) in Settings.StructureManagementModule.ComplexAccess)
            {
                var aData = await ParseMemberDataArray(value.Entities
                    .Where(a => (a is long i && i != 0) || (a is string s && s != string.Empty)).ToList());
                lock (UpdateLock)
                    ParsedManageAccessMembersLists.Add(key, aData);
            }
        }


        private DateTime? _lastCheck;
        private volatile bool _isRunning;

        public override async Task Run(object prm)
        {
          
            if(!Settings.Config.ModuleStructureManagement || _isRunning) return;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;
            _isRunning = true;
            try
            {
                if ((_lastCheck == null || DateTime.Now >= _lastCheck) && ParsedManageAccessMembersLists.Any())
                {
                    _lastCheck = DateTime.Now.AddMinutes(2);
                    var result = new List<NotifyItem>();
                    var processedCorps = new List<long>();
                    foreach (var token in await DbHelper.GetTokens(TokenEnum.Structures))
                    {
                        var r = await APIHelper.ESIAPI.GetAccessTokenWithScopes(token, new ESIScope().AddCorpStructure().AddUniverseStructure().Merge());
                        if (r == null || r.Data.IsFailed)
                        {
                            await LogHelper.LogWarning($"Failed to refresh structure token from {token.CharacterId}",
                                Category);
                            if (r?.Data.IsNotValid ?? false)
                            {
                                await DbHelper.DeleteToken(token.CharacterId, TokenEnum.Structures);
                                await LogHelper.LogWarning(
                                    $"Structures token from {token.CharacterId} is no longer valid and will be deleted!",
                                    Category);
                            }

                            continue;
                        }

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, token.CharacterId, true);
                        if (rChar == null)
                        {
                            await LogHelper.LogError($"Failed to refresh character {token.CharacterId}", Category);
                            continue;
                        }

                        var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar.corporation_id);
                        if (corp == null)
                        {
                            await LogHelper.LogError($"Failed to refresh corp {rChar.corporation_id}", Category);
                            continue;
                        }

                        if (processedCorps.Contains(rChar.corporation_id))
                            continue;
                        processedCorps.Add(rChar.corporation_id);

                        var cacheId = $"sm{rChar.corporation_id}";
                        var structures = await DbHelper.GetCache<List<CorporationStructureJson>>(cacheId, 5);
                        if (structures == null)
                        {
                            structures = await APIHelper.ESIAPI.GetCorpStructures(Reason, rChar.corporation_id,
                                r.Result);
                            if(structures != null)
                                await DbHelper.UpdateCache(cacheId, structures);
                        }
                        if (structures == null)
                        {
                            await LogHelper.LogError($"Unable to get the structures info from {rChar.name}");
                            continue;
                        }

                        var groups = new List<StructureAccessGroup>();
                        foreach (var (groupName, groupValue) in ParsedManageAccessMembersLists)
                        {
                            if (groupValue["character"].Contains(rChar.character_id) ||
                                groupValue["corporation"].Contains(rChar.corporation_id) ||
                                (rChar.alliance_id.HasValue &&
                                 groupValue["alliance"].Contains(rChar.alliance_id.Value)))
                            {
                                groups.Add(Settings.StructureManagementModule.ComplexAccess[groupName]);
                            }
                        }

                        if (groups.Any())
                        {
                            //select all unanchoring structures
                            var unanchoring = structures.Where(a => a.state != CorpStructureStateEnum.unanchored && a.unanchors_at.HasValue)
                                .ToList();
                            var unanchored = structures.Where(a =>
                                a.state == CorpStructureStateEnum.unanchored);

                            foreach (var @group in groups)
                            {
                                try
                                {
                                    //unanchor notifications
                                    if (group.CustomNotifications.UnanchoringHours.Any() &&
                                        group.CustomNotifications.UnanchoringDiscordChannelIds.Any() &&
                                        unanchoring.Any())
                                    {


                                        var announces = group.CustomNotifications.UnanchoringHours
                                            .OrderByDescending(a => a).ToList();
                                        if (announces.Count == 0) continue;

                                        foreach (var s in unanchored)
                                        {
                                            var lastNotify =
                                                await DbHelper.GetNotificationListEntry("structures",
                                                    s.structure_id);
                                            if(lastNotify != null && lastNotify.FilterName == "check")
                                                continue;
                                            await DbHelper.UpdateNotificationListEntry("structures", s.structure_id,
                                                "check");
                                            result.Add(new NotifyItem
                                            {
                                                Token = r.Result,
                                                Hours = 0,
                                                Structure = s,
                                                Channels = group.CustomNotifications.UnanchoringDiscordChannelIds
                                            });
                                        }

                                        foreach (var s in unanchoring)
                                        {
                                            var lastNotify =
                                                await DbHelper.GetNotificationListEntryDate("structures",
                                                    s.structure_id);
                                            //don;t have announces
                                            if (lastNotify.HasValue &&
                                                (s.unanchors_at.Value - lastNotify.Value).TotalHours < announces.Min())
                                                continue;
                                            //new announce
                                            var left = (s.unanchors_at.Value - DateTime.UtcNow).TotalHours;
                                            if (!lastNotify.HasValue)
                                            {
                                                //have some announces
                                                if (left <= announces.Max())
                                                {
                                                    var value = announces.Where(a => a <= left).OrderByDescending(a => a)
                                                        .FirstOrDefault();
                                                    value = value == 0 ? announces.Min() : value;
                                                    result.Add(new NotifyItem
                                                    {
                                                        Token = r.Result, Hours = value, Structure = s,
                                                        Channels = group.CustomNotifications.UnanchoringDiscordChannelIds
                                                    });
                                                    await DbHelper.UpdateNotificationListEntry("structures", s.structure_id);
                                                }
                                            }
                                            else
                                            {
                                                var sinceAnnounce =
                                                    (s.unanchors_at.Value - lastNotify.Value).TotalHours;
                                                //existing announce
                                                var aList = announces.Where(a => a < sinceAnnounce && a >= left)
                                                    .OrderByDescending(a => a).ToList();
                                                if (aList.Count == 0) return;

                                                result.Add(new NotifyItem
                                                {
                                                    Token = r.Result, Hours = aList.First(), Structure = s, Channels = group.CustomNotifications.UnanchoringDiscordChannelIds
                                                });
                                                await DbHelper.UpdateNotificationListEntry("structures", s.structure_id);
                                            }

                                        }

                                    }
                                }
                                catch (Exception ex)
                                {
                                    await LogHelper.LogEx(ex, Category);
                                }
                            }
                        }
                    }

                    foreach (var r in result)
                    {
                        var s = await APIHelper.ESIAPI.GetUniverseStructureData(Reason, r.Structure.structure_id,
                            r.Token);
                        foreach (var channel in r.Channels)
                        {
                            try
                            {
                                await APIHelper.DiscordAPI.SendMessageAsync(channel,
                                    LM.Get(r.Hours == 0 ? "smNotifyUnanchoredMessage" : "smNotifyUnanchoringMessage", s?.name, r.Hours));
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogEx(ex, Category);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, Category);
            }
            finally
            {
                _isRunning = false;
            }
        }


        public async Task<WebQueryResult> ProcessRequest(string query, CallbackTypeEnum type, string ip, WebAuthUserData data)
        {
            if (!Settings.Config.ModuleStructureManagement)
                return WebQueryResult.False;

            try
            {
                RunningRequestCount++;
                if (!query.Contains("&state=sm"))
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
                if (rChar == null)
                    return WebQueryResult.EsiFailure;

                if (!await HasAuthAccess(rChar))
                {
                    await LogHelper.LogWarning($"Unauthorized feed request from {characterId}", Category);
                    var r = WebQueryResult.BadRequestToSystemAuth;
                    r.Message1 = LM.Get("authTokenBodyFail");
                    return r;
                }

                var t = await DbHelper.UpdateToken(result[1], numericCharId, TokenEnum.Structures);
                var accessToken = (await APIHelper.ESIAPI.GetAccessToken(t))?.Result;
                if (!string.IsNullOrEmpty(accessToken))
                {
                    t.Scopes = APIHelper.ESIAPI.GetScopesFromToken(accessToken);
                    await DbHelper.UpdateToken(t.Token, t.CharacterId, t.Type, t.Scopes);
                }
                await LogHelper.LogInfo($"Feed added for character: {characterId}", Category);

                var res = WebQueryResult.FeedAuthSuccess;
                res.Message1 = LM.Get("smAuthSuccessHeader");
                res.Message2 = LM.Get("smAuthSuccessBody");
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

        #region Auth checks

        public static async Task<bool> HasAuthAccess(JsonClasses.CharacterData data)
        {
            if (data == null) return false;
            return await HasAuthAccess(data.character_id, data.corporation_id, data.alliance_id ?? 0);
        }

        public static async Task<bool> HasAuthAccess(WebAuthUserData data)
        {
            if (data == null) return false;
            return await HasAuthAccess(data.Id, data.CorpId, data.AllianceId);
        }

        private static async Task<bool> HasAuthAccess(long id, long corpId, long allianceId)
        {
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();

            if (HasAccess(id, corpId, allianceId, module.ParsedAuthAccessMembersLists))
                return true;

            var roles = await DiscordHelper.GetDiscordRoles(id);
            if (roles == null) return false;

            if (SettingsManager.Settings.StructureManagementModule.AuthAccessDiscordRoles != null &&
                roles.Intersect(SettingsManager.Settings.StructureManagementModule.AuthAccessDiscordRoles)
                    .Any())
                return true;
            return false;
        }

        /*/// <summary>
        /// Security check for access
        /// </summary>
        public static bool HasViewAccess(in JsonClasses.CharacterData data)
        {
            if (data == null) return false;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();
            return HasAccess(data.character_id, data.corporation_id, data.alliance_id ?? 0, module.ParsedViewAccessMembersLists);
        }*/

        /// <summary>
        /// Security check for access
        /// </summary>
        public static async Task<bool> HasViewAccess(WebAuthUserData data)
        {
            if (data == null || TickManager.IsNoConnection || TickManager.IsESIUnreachable) return false;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();
            if (HasAccess(data.Id, data.CorpId, data.AllianceId, module.ParsedViewAccessMembersLists))
                return true;

            var roles = await DiscordHelper.GetDiscordRoles(data.Id);
            if (roles == null) return false;

            if (SettingsManager.Settings.StructureManagementModule.ViewAccessDiscordRoles != null &&
                roles.Intersect(SettingsManager.Settings.StructureManagementModule.ViewAccessDiscordRoles)
                    .Any())
                return true;
            return false;
        }

        #endregion

        #region General access functions

        private static bool HasAccess(long id, long corpId, long allianceId, Dictionary<string, List<long>> dic)
        {
            if (!SettingsManager.Settings.Config.ModuleStructureManagement || !dic.Any() || TickManager.IsNoConnection || TickManager.IsESIUnreachable) return false;
            return dic["character"].Contains(id) || dic["corporation"].Contains(corpId) || (allianceId > 0 && dic["alliance"].Contains(corpId));
        }

        private static bool HasAccess(long id, long corpId, long allianceId, Dictionary<string, Dictionary<string, List<long>>> dic, out string groupName)
        {
            groupName = null;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement || TickManager.IsNoConnection || TickManager.IsESIUnreachable) return false;

            groupName = dic.FirstOrDefault(a => a.Value.FirstOrDefault(b => b.Key == "character" && b.Value.Contains(id)).Key != null).Key;
            if (!string.IsNullOrEmpty(groupName))
                return true;
            groupName = dic.FirstOrDefault(a => a.Value.FirstOrDefault(b => b.Key == "corporation" && b.Value.Contains(corpId)).Key != null).Key;
            if (!string.IsNullOrEmpty(groupName))
                return true;
            if (allianceId > 0)
            {
                groupName = dic.FirstOrDefault(a => a.Value.FirstOrDefault(b => b.Key == "alliance" && b.Value.Contains(allianceId)).Key != null).Key;
                if (!string.IsNullOrEmpty(groupName))
                    return true;
            }

            return false;
        }

        #endregion

        #region Manage access checks

        /*public static bool HasManageAccess(in JsonClasses.CharacterData data, out string groupName)
        {
            groupName = null;
            if (data == null) return false;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();
            return HasAccess(data.character_id, data.corporation_id, data.alliance_id ?? 0, module.ParsedManageAccessMembersLists, out groupName);
        }*/

        public static bool HasManageAccess(WebAuthUserData data, out string groupName)
        {
            groupName = null;
            if (data == null || TickManager.IsNoConnection || TickManager.IsESIUnreachable) return false;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();
            if (HasAccess(data.Id, data.CorpId, data.AllianceId, module.ParsedManageAccessMembersLists, out groupName))
                return true;

            var roles = DiscordHelper.GetDiscordRoles(data.Id).GetAwaiter().GetResult();
            if (roles == null) return false;

            foreach (var (key, value) in SettingsManager.Settings.MiningScheduleModule.Ledger.ComplexAccess)
                if (value.DiscordRoles != null &&
                    roles.Intersect(value.DiscordRoles).Any())
                {
                    groupName = key;
                    return true;
                }
            return false;
        }
        /*/// <summary>
        /// Global access flag to ledger operations
        /// </summary>
        public static bool HasCommonLedgerViewAccess(in JsonClasses.CharacterData data)
        {
            if (data == null) return false;
            return HasObserverLedgerViewAccess(data) || HasLedgerEditAccess(data, out _);
        }
        /// <summary>
        /// Global access flag to ledger operations
        /// </summary>
        public static bool HasCommonLedgerViewAccess(in WebAuthUserData data)
        {
            if (data == null) return false;
            return HasObserverLedgerViewAccess(data) || HasLedgerEditAccess(data, out _);
        }*/
        #endregion

        public async Task<Tuple<List<string>, List<ThdStructureInfo>>> GetStructures(StructureAccessGroup accessGroup, WebAuthUserData user)
        {
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return null;
            var tokens = await DbHelper.GetTokens(TokenEnum.Structures);
            var result = new List<ThdStructureInfo>();
            var corps = new List<string>();
            var processedCorps = new List<long>();

            foreach (var token in tokens)
            {
                var r = await APIHelper.ESIAPI.GetAccessTokenWithScopes(token, new ESIScope().AddCorpStructure().AddUniverseStructure().Merge());
                if (r == null || r.Data.IsFailed)
                {
                    await LogHelper.LogWarning($"Failed to refresh structure token from {token.CharacterId}",
                        Category);
                    if (r?.Data.IsNotValid??false)
                    {
                        await DbHelper.DeleteToken(token.CharacterId, TokenEnum.Structures);
                        await LogHelper.LogWarning(
                            $"Structures token from {token.CharacterId} is no longer valid and will be deleted!",
                            Category);
                    }
                    continue;
                }

                var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, token.CharacterId, true);
                if (rChar == null)
                {
                    await LogHelper.LogWarning($"Failed to refresh character {token.CharacterId}", Category);
                    continue;
                }

                var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar.corporation_id);
                if (corp == null)
                {
                    await LogHelper.LogWarning($"Failed to refresh corp {rChar.corporation_id}", Category);
                    continue;
                }
                if (processedCorps.Contains(rChar.corporation_id))
                    continue;
                //check access to only own corp
                if (accessGroup != null && accessGroup.CanManageOwnCorporation &&
                    rChar.corporation_id != user.CorpId)
                    continue;

                processedCorps.Add(rChar.corporation_id);
                corps.Add(corp.name);

                var list = await APIHelper.ESIAPI.GetCorpStructures(Reason, rChar.corporation_id, r.Result);

                foreach (var item in list)
                {
                    var structure =
                        await APIHelper.ESIAPI.GetUniverseStructureData(Reason, item.structure_id, r.Result);

                    //check structures from access list
                    if (accessGroup != null && !accessGroup.CanManageOwnCorporation &&
                        accessGroup.StructureNames.Any())
                    {
                        if (structure == null)
                        {
                            await LogHelper.LogWarning(
                                $"Has accessGroup for user {user.Name} and can't identify structure. It will be skipped.",
                                Category);
                            continue;
                        }

                        if (!accessGroup.StructureNames.Any(a =>
                            structure.name.StartsWith(a, StringComparison.OrdinalIgnoreCase)))
                            continue;
                    }


                    var left = item.fuel_expires.HasValue
                        ? item.fuel_expires.Value.GetRemains(LM.Get("timerRemains"))
                        : LM.Get("smNoFuel");

                    var state = LM.Get("smStructureStateUnknown");
                    switch (item.state)
                    {
                        case CorpStructureStateEnum.anchor_vulnerable:
                            state = LM.Get("smStructureStateAnchorV");
                            break;
                        case CorpStructureStateEnum.anchoring:
                            state = LM.Get("smStructureStateAnchoring");
                            break;
                        case CorpStructureStateEnum.armor_reinforce:
                            state = LM.Get("smStructureStateReinforceA");
                            break;
                        case CorpStructureStateEnum.armor_vulnerable:
                            state = LM.Get("smStructureStateVulnerableA");
                            break;
                        case CorpStructureStateEnum.deploy_vulnerable:
                            state = LM.Get("smStructureStateVulnerableDeploy");
                            break;
                        case CorpStructureStateEnum.fitting_invulnerable:
                            state = LM.Get("smStructureStateFittingInvul");
                            break;
                        case CorpStructureStateEnum.hull_reinforce:
                            state = LM.Get("smStructureStateReinforceH");
                            break;
                        case CorpStructureStateEnum.hull_vulnerable:
                            state = LM.Get("smStructureStateVulnerableH");
                            break;
                        case CorpStructureStateEnum.onlining_vulnerable:
                            state = LM.Get("smStructureStateVulnerableOnlining");
                            break;
                        case CorpStructureStateEnum.shield_vulnerable:
                            state = LM.Get("smStructureStateVulnerableS");
                            break;
                        case CorpStructureStateEnum.unanchored:
                            state = LM.Get("smStructureStateUnanchored");
                            break;
                    }

                    if (item.state_timer_end.HasValue)
                        state =
                            $"{state}<br>{LM.Get("smStateEndsIn")}: {item.state_timer_end.Value.GetRemains(LM.Get("timerRemains"))}";

                    result.Add(new ThdStructureInfo
                    {
                        StructureId = item.structure_id,
                        StructureTypeId = item.type_id,
                        StructureName = structure?.name ?? LM.Get("Unknown"),
                        CorporationId = rChar.corporation_id,
                        CorporationName = corp.name,
                        FuelTimeLeft = left,
                        FuelTime = item.fuel_expires,
                        State = state,
                        FeederId = token.CharacterId
                    });
                }

            }

            return new Tuple<List<string>, List<ThdStructureInfo>>(corps, result.OrderBy(a=>a.FuelTime).ToList());
        }
    }

    public class NotifyItem
    {
        public string Token { get; set; }
        public int Hours { get; set; }
        public CorporationStructureJson Structure { get; set; }
        public List<ulong> Channels { get; set; }
    }
}
