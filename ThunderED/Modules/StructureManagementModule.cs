using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

                if (!HasAuthAccess(rChar))
                {
                    await LogHelper.LogWarning($"Unauthorized feed request from {characterId}", Category);
                    var r = WebQueryResult.BadRequestToSystemAuth;
                    r.Message1 = LM.Get("authTokenBodyFail");
                    return r;
                }

                await DbHelper.UpdateToken(result[1], numericCharId, TokenEnum.Structures);
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
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();

            return HasAccess(id, corpId, allianceId, module.ParsedAuthAccessMembersLists);
        }

        /// <summary>
        /// Security check for access
        /// </summary>
        public static bool HasViewAccess(in JsonClasses.CharacterData data)
        {
            if (data == null) return false;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();
            return HasAccess(data.character_id, data.corporation_id, data.alliance_id ?? 0, module.ParsedViewAccessMembersLists);
        }

        /// <summary>
        /// Security check for access
        /// </summary>
        public static bool HasViewAccess(WebAuthUserData data)
        {
            if (data == null) return false;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();
            return HasAccess(data.Id, data.CorpId, data.AllianceId, module.ParsedViewAccessMembersLists);
        }

        #endregion

        #region General access functions

        private static bool HasAccess(long id, long corpId, long allianceId, Dictionary<string, List<long>> dic)
        {
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            return dic["character"].Contains(id) || dic["corporation"].Contains(corpId) || (allianceId > 0 && dic["alliance"].Contains(corpId));
        }

        private static bool HasAccess(long id, long corpId, long allianceId, Dictionary<string, Dictionary<string, List<long>>> dic, out string groupName)
        {
            groupName = null;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;

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

        public static bool HasManageAccess(in JsonClasses.CharacterData data, out string groupName)
        {
            groupName = null;
            if (data == null) return false;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();
            return HasAccess(data.character_id, data.corporation_id, data.alliance_id ?? 0, module.ParsedManageAccessMembersLists, out groupName);
        }

        public static bool HasManageAccess(WebAuthUserData data, out string groupName)
        {
            groupName = null;
            if (data == null) return false;
            if (!SettingsManager.Settings.Config.ModuleStructureManagement) return false;
            var module = TickManager.GetModule<StructureManagementModule>();
            return HasAccess(data.Id, data.CorpId, data.AllianceId, module.ParsedManageAccessMembersLists, out groupName);
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

        public async Task<Tuple<List<string>, List<ThdStructureInfo>>> GetStructures(MiningComplexAccessGroup accessGroup, WebAuthUserData user)
        {
            var tokens = await DbHelper.GetTokens(TokenEnum.Structures);
            var result = new List<ThdStructureInfo>();
            var corps = new List<string>();
            var processedCorps = new List<long>();

            foreach (var token in tokens)
            {
                var r = await APIHelper.ESIAPI.RefreshToken(token.Token, Settings.WebServerModule.CcpAppClientId,
                    Settings.WebServerModule.CcpAppSecret);
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
}
