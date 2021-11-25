using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;
using ThunderED.Thd;

namespace ThunderED.Modules
{
    public partial class WebAuthModule
    {
        private async Task WebPartInitialization()
        {
            if (WebServerModule.WebModuleConnectors.ContainsKey(Reason))
                WebServerModule.WebModuleConnectors.Remove(Reason);
            WebServerModule.WebModuleConnectors.Add(Reason, ProcessAuth);
            await Task.CompletedTask;
        }

        private async Task<WebQueryResult> ProcessAuth(string query, CallbackTypeEnum callbackType, string inputIp, WebAuthUserData webUserData)
        {
            if (!Settings.Config.ModuleAuthWeb)
                return WebQueryResult.False;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return WebQueryResult.EsiFailure;

            try
            {
                RunningRequestCount++;
                var prms = query.TrimStart('?').Split('&');
                if (prms.Length == 0 || prms[0].Split('=').Length == 0 || string.IsNullOrEmpty(prms[0]))
                    return WebQueryResult.BadRequestToGeneralAuth;

                if (callbackType == CallbackTypeEnum.Auth)
                {
                    var grps = Settings.WebAuthModule.GetEnabledAuthGroups();
                    var groupName = HttpUtility.UrlDecode(prms[0].Split('=')[1]);

                    if (!grps.Keys.ContainsCaseInsensitive(groupName) && !DEF_NOGROUP_NAME.Equals(groupName) &&
                        !DEF_ALTREGGROUP_NAME.Equals(groupName))
                        return WebQueryResult.BadRequestToGeneralAuth;

                    if (!grps.Keys.ContainsCaseInsensitive(groupName) && DEF_NOGROUP_NAME.Equals(groupName))
                    {
                        var redirect = WebQueryResult.RedirectUrl;
                        redirect.AddUrl(ServerPaths.GetAuthUrlOneButton(inputIp));
                        return redirect;
                    }
                    else if (!grps.Keys.ContainsCaseInsensitive(groupName) && DEF_ALTREGGROUP_NAME.Equals(groupName))
                    {
                        var redirect = WebQueryResult.RedirectUrl;
                        redirect.AddUrl("/altreg");
                        return redirect;
                    }
                    else
                    {
                        var grp = GetGroupByName(groupName).Value;
                        var url = grp.MustHaveGroupName ||
                                  (Settings.WebAuthModule.UseOneAuthButton && grp.ExcludeFromOneButtonMode)
                            ? ServerPaths.GetCustomAuthUrl(inputIp, grp.ESICustomAuthRoles, groupName)
                            : ServerPaths.GetAuthUrl(inputIp);
                        var result = WebQueryResult.RedirectUrl;
                        result.AddValue("url", url);
                        return result;
                    }
                }
                else if (query.Contains("&state=userauth"))
                {
                    var res = WebQueryResult.RedirectUrl;
                    res.AddUrl($"{ServerPaths.GetUserAuthCallbackUrl()}{query}");
                    return res;
                }
                else if(query.Contains("&state=authst"))
                {
                    var code = prms[0].Split('=')[1];

                    var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId,
                        Settings.WebServerModule.CcpAppSecret);
                    if (result == null)
                        return WebQueryResult.EsiFailure;

                    var characterId = result[0];
                    var numericCharId = Convert.ToInt64(characterId);

                    if (string.IsNullOrEmpty(characterId))
                    {
                        await LogHelper.LogWarning("Bad or outdated stand auth request!");
                        return WebQueryResult.BadRequestToGeneralAuth;
                    }

                    var grps = Settings.WebAuthModule.GetEnabledAuthGroups();

                    if (grps.Values.All(g =>
                        g.StandingsAuth == null || !g.StandingsAuth.CharacterIDs.Contains(numericCharId)))
                    {
                        await LogHelper.LogWarning($"Unauthorized auth stands feed request from {characterId}");
                        var r = WebQueryResult.BadRequestToGeneralAuth;
                        r.Message1 = LM.Get("authTokenBodyFail");
                        r.Message2 = LM.Get("authTokenInvalid");
                        return r;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);

                    await SQLHelper.DeleteAuthStands(numericCharId);
                    var data = new AuthStandsEntity { CharacterID = numericCharId, Token = result[1] };

                    var tq = await APIHelper.ESIAPI.RefreshToken(data.Token, Settings.WebServerModule.CcpAppClientId,
                        Settings.WebServerModule.CcpAppSecret, $"From {Category} | Char ID: {characterId}");
                    var token = tq.Result;

                    if (!tq.Data.IsFailed)
                        await RefreshStandings(data, token);
                    await SQLHelper.SaveAuthStands(data);

                    await LogHelper.LogInfo($"Auth stands feed added for character: {characterId}({rChar.name})",
                        LogCat.AuthWeb);

                    var result2 = WebQueryResult.GeneralAuthSuccess;
                    result2.Message1 = LM.Get("authTokenRcv");
                    result2.Message2 = LM.Get("authStandsTokenRcv", rChar.name);
                    return result2;
                }
                else if (!query.Contains("&state=") || query.Contains("&state=x") ||
                         query.Contains("&state=oneButton") || query.Contains("&state=altReg"))
                {
                    var add = false;

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        var prms2 = QueryHelpers.ParseQuery(query);
                        var code = prms2.ContainsKey("code") ? prms2["code"].LastOrDefault() : null;
                        var state = prms2.ContainsKey("state") ? prms2["state"].LastOrDefault() : null;
                        var mainCharId = 0L;
                        string rawIp = null;
                        if (state?.Contains('|') ?? false)
                        {
                            var lst = state.Split('|');
                            state = lst[0];
                            long.TryParse(lst[1], out mainCharId);
                            rawIp = lst.LastOrDefault();
                            rawIp = rawIp?.Split(':')
                                .FirstOrDefault(a => !string.IsNullOrEmpty(a) && char.IsDigit(a[0]));
                        }

                        mainCharId = webUserData?.Id ?? 0;

                        var inputGroupName = state?.Length > 1
                            ? HttpUtility.UrlDecode(state.Substring(1, state.Length - 1))
                            : null;
                        var inputGroup = GetGroupByName(inputGroupName).Value;
                        var autoSearchGroup = inputGroup == null && (state?.Equals("oneButton") ?? false);
                        var altCharReg = inputGroup == null && (state?.Equals("altReg") ?? false);

                        var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId,
                            Settings.WebServerModule.CcpAppSecret);
                        if (result == null)
                            return WebQueryResult.EsiFailure;

                        var characterId = result?[0];

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                        if (rChar == null)
                            return WebQueryResult.EsiFailure;

                        var longCharacterId = Convert.ToInt64(characterId);

                        var corpID = rChar?.corporation_id ?? 0;
                        var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar?.corporation_id, true);
                        if (rCorp == null)
                            return WebQueryResult.EsiFailure;

                        var allianceID = rCorp?.alliance_id ?? 0;

                        var cFoundList = new List<long>();
                        var groupName = string.Empty;
                        WebAuthGroup group = null;

                        var grps = Settings.WebAuthModule.GetEnabledAuthGroups();

                        if (altCharReg && mainCharId > 0)
                        {
                            var refreshToken = result[1];
                            var altCharId = longCharacterId;
                            var user = await DbHelper.GetAuthUser(mainCharId, true);
                            //do not allow to bind alt to another alt
                            if (user == null || user.AuthState != (int)UserStatusEnum.Authed || user.MainCharacterId.HasValue || user.DiscordId == 0)
                            {
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterId} grp: {inputGroupName}", Category);
                                var result2 = WebQueryResult.BadRequestToGeneralAuth;
                                result2.Message1 = LM.Get("authAltRegTemplateHeader");
                                result2.Message2 = LM.Get("authAltRegMainNotFound");
                                return result2;
                            }

                            var pair = grps.FirstOrDefault(a => a.Value.BindToMainCharacter);
                            if (pair.Value == null)
                            {
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterId} grp: {inputGroupName}",  Category);
                                var result2 = WebQueryResult.BadRequestToGeneralAuth;
                                result2.Message1 = LM.Get("authAltRegTemplateHeader");
                                result2.Message2 = LM.Get("authAltRegGroupNotFound");
                                return result2;
                            }

                            group = pair.Value;
                            groupName = pair.Key;

                            var altUser = await user.CreateAlt(altCharId, group, groupName,
                                mainCharId);
                            altUser.Ip = rawIp;
                            //remove old reg if any
                            await DbHelper.DeleteAuthUser(altCharId);
                            //save alt
                            await DbHelper.SaveAuthUser(altUser, refreshToken);

                            if (Settings.WebAuthModule.AuthReportChannel > 0)
                                await APIHelper.DiscordAPI.SendMessageAsync(Settings.WebAuthModule.AuthReportChannel,
                                    LM.Get("authReportCreateAlt", altUser.DataView.CharacterName, user.DataView.CharacterName));

                            var success = WebQueryResult.GeneralAuthSuccess;
                            success.Message1 = LM.Get("authAltRegTemplateHeader");
                            success.Message2 = LM.Get("authAltRegAccepted", rChar.name, user.DataView.CharacterName);
                            return success;
                        }


                        //PreliminaryAuthMode
                        if (inputGroup != null && inputGroup.PreliminaryAuthMode)
                        {
                            group = inputGroup;
                            if (string.IsNullOrEmpty(result[1]))
                            {
                                await LogHelper.LogWarning($"Invalid named group auth attempt (missing token) from charID: {characterId} grp: {inputGroupName}", Category);
                                var result2 = WebQueryResult.BadRequestToGeneralAuth;
                                result2.Message1 = LM.Get("authTemplateHeader");
                                result2.Message2 = LM.Get("authNoTokenReceived");
                                return result2;
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
                                if ((await GetAuthRoleEntityById(
                                        new KeyValuePair<string, WebAuthGroup>(inputGroupName, inputGroup), rChar))
                                    .RoleEntities.Any())
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
                                        ? grps.Where(a =>
                                            !a.Value.ExcludeFromOneButtonMode && !a.Value.BindToMainCharacter)
                                        : grps.Where(a =>
                                            !a.Value.ESICustomAuthRoles.Any() && !a.Value.PreliminaryAuthMode &&
                                            !a.Value.BindToMainCharacter);
                                    //general auth
                                    var gResult =
                                        await GetAuthRoleEntityById(searchFor.ToDictionary(a => a.Key, a => a.Value),
                                            rChar);
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
                            if (autoSearchGroup && group.ESICustomAuthRoles.Any()) 
                                //for one button with ESI - had to auth twice
                            {
                                var redirect = WebQueryResult.RedirectUrl;
                                redirect.AddValue("url", ServerPaths.GetCustomAuthUrl(rawIp, group.ESICustomAuthRoles, groupName));
                                return redirect;
                            }

                            //cleanup prev auth
                            await DbHelper.DeleteAuthUser(Convert.ToInt64(characterId));
                            var refreshToken = result[1];

                            var uid = GetUniqID();
                            var authUser = new ThdAuthUser()
                            {
                                CharacterId = Convert.ToInt64(characterId),
                                DataView = 
                                {
                                    Permissions = group.ESICustomAuthRoles.Count > 0
                                        ? string.Join(',', group.ESICustomAuthRoles)
                                        : null
                                },
                                DiscordId = 0,
                                GroupName = groupName,
                                AuthState = inputGroup != null && inputGroup.PreliminaryAuthMode ? 0 : 1,
                                RegCode = uid,
                                CreateDate = DateTime.Now,
                                Ip = rawIp
                            };
                            await authUser.UpdateData();
                            await DbHelper.SaveAuthUser(authUser, refreshToken);

                            if (group.SkipDiscordAuthPage)
                            {
                                var success = WebQueryResult.GeneralAuthSuccess;
                                success.Message1 = LM.Get("authTemplateHeaderShort");
                                success.Message2 = LM.Get("authTemplateManualAccept", rChar.name);
                                return success;
                            }

                            if (!group.PreliminaryAuthMode)
                            {
                                if (SettingsManager.Settings.WebAuthModule.AuthReportChannel != 0)
                                    await APIHelper.DiscordAPI
                                        .SendMessageAsync(SettingsManager.Settings.WebAuthModule.AuthReportChannel,
                                            $"{group.DefaultMention} {LM.Get("authManualAcceptMessage", rChar.name, characterId, groupName)}")
                                        .ConfigureAwait(false);
                                await LogHelper.LogWarning(LM.Get("authManualAcceptMessage", rChar.name, characterId, groupName), LogCat.AuthWeb);
                                var success = WebQueryResult.GeneralAuthSuccess;
                                success.Message1 = LM.Get("authTemplateHeader");
                                success.Message2 = LM.Get("authTemplateSucc1", rChar.name);
                                success.Message3 = $"{LM.Get("authTemplateSucc2")}<br><b>{Settings.Config.BotDiscordCommandPrefix}auth {uid}</b>";
                                return success;
                            }
                            else
                            {
                                var success = WebQueryResult.GeneralAuthSuccess;
                                success.Message1 = LM.Get("authTemplateHeader");
                                success.Message2 = LM.Get("authTemplateManualAccept", rChar.name);
                                success.Message3 =
                                    $"{LM.Get("authTemplateManualAccept2")}<br><b>{Settings.Config.BotDiscordCommandPrefix}auth confirm {uid}</b>";
                                return success;
                            }
                        }
                        else
                        {
                            var success = WebQueryResult.BadRequestToGeneralAuth;
                            success.Message1 = LM.Get("authTemplateHeader");
                            success.Message2 = LM.Get("authNonAlly");
                            return success;
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Error: {ex.Message}", ex, Category);
            }
            finally
            {
                RunningRequestCount--;
            }

            return WebQueryResult.False;
        }

        public static bool HasAuthAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleAuthWeb) return false;
            return SettingsManager.Settings.WebAuthModule.GetEnabledAuthGroups()
                .Where(a => a.Value.StandingsAuth != null).SelectMany(a => a.Value.StandingsAuth.CharacterIDs)
                .Contains(id);
        }
    }
}
