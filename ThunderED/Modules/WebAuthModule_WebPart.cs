using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.WebUtilities;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
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
                   /* var code = prms[0].Split('=')[1];
                    var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId,
                        Settings.WebServerModule.CcpAppSecret);
                    if (result == null)
                        return WebQueryResult.EsiFailure;
                    var charId = Convert.ToInt64(result[0]);
                    if (string.IsNullOrEmpty(result[0]))
                    {
                        await LogHelper.LogWarning("Bad or outdated user auth request!");
                        return WebQueryResult.BadRequestToRoot;
                    }

                    var authUser = await SQLHelper.GetAuthUserByCharacterId(charId);
                    if (authUser == null)
                    {
                        var r = WebQueryResult.BadRequestToRoot;
                        r.Message1 = LM.Get("webUserIsNotAuthenticated");
                        return r;
                    }
                    */

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

                    var characterID = result[0];
                    var numericCharId = Convert.ToInt64(characterID);

                    if (string.IsNullOrEmpty(characterID))
                    {
                        await LogHelper.LogWarning("Bad or outdated stand auth request!");
                        return WebQueryResult.BadRequestToGeneralAuth;
                    }

                    var grps = Settings.WebAuthModule.GetEnabledAuthGroups();

                    if (grps.Values.All(g =>
                        g.StandingsAuth == null || !g.StandingsAuth.CharacterIDs.Contains(numericCharId)))
                    {
                        await LogHelper.LogWarning($"Unauthorized auth stands feed request from {characterID}");
                        var r = WebQueryResult.BadRequestToGeneralAuth;
                        r.Message1 = LM.Get("authTokenBodyFail");
                        r.Message2 = LM.Get("authTokenInvalid");
                        return r;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                    await SQLHelper.DeleteAuthStands(numericCharId);
                    var data = new AuthStandsEntity { CharacterID = numericCharId, Token = result[1] };

                    var tq = await APIHelper.ESIAPI.RefreshToken(data.Token, Settings.WebServerModule.CcpAppClientId,
                        Settings.WebServerModule.CcpAppSecret, $"From {Category} | Char ID: {characterID}");
                    var token = tq.Result;

                    if (!tq.Data.IsFailed)
                        await RefreshStandings(data, token);
                    await SQLHelper.SaveAuthStands(data);

                    await LogHelper.LogInfo($"Auth stands feed added for character: {characterID}({rChar.name})",
                        LogCat.AuthWeb);

                    var result2 = WebQueryResult.GeneralAuthSuccess;
                    result2.Message1 = LM.Get("authTokenRcv");
                    result2.Message2 = LM.Get("authStandsTokenRcv", rChar.name);
                    return result2;
                }
                else if (!query.Contains("&state=") || query.Contains("&state=x") ||
                         query.Contains("&state=oneButton") || query.Contains("&state=altReg"))
                {
                   /* var assembly = Assembly.GetEntryAssembly();
                    // var temp = assembly.GetManifestResourceNames();
                    var resource = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Discord-01.png");
                    var buffer = new byte[resource.Length];
                    resource.Read(buffer, 0, Convert.ToInt32(resource.Length));
                    var image = Convert.ToBase64String(buffer);*/
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

                        var characterID = result?[0];

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);
                        if (rChar == null)
                            return WebQueryResult.EsiFailure;

                        var longCharacterId = Convert.ToInt64(characterID);

                        var corpID = rChar?.corporation_id ?? 0;
                        var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar?.corporation_id, true);
                        if (rCorp == null)
                            return WebQueryResult.EsiFailure;

                        var allianceID = rCorp?.alliance_id ?? 0;

                        var cFoundList = new List<long>();
                        var groupName = string.Empty;
                        WebAuthGroup group = null;

                        var grps = Settings.WebAuthModule.GetEnabledAuthGroups();

                        //alt character registration check
                        if (altCharReg)
                        {
                            var user = await DbHelper.GetAuthUser(longCharacterId, true);
                            //do not allow to bind alt to another alt
                            if (user == null || user.AuthState != (int)UserStatusEnum.Authed || user.MainCharacterId.HasValue || user.DiscordId == 0)
                            {
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}", Category);
                                var result2 = WebQueryResult.BadRequestToGeneralAuth;
                                result2.Message1 = LM.Get("authAltRegTemplateHeader");
                                result2.Message2 = LM.Get("authAltRegMainNotFound");
                                return result2;
                            }

                            var pair = grps.FirstOrDefault(a => a.Value.BindToMainCharacter);
                            if (pair.Value == null)
                            {
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}", Category);
                                var result2 = WebQueryResult.BadRequestToGeneralAuth;
                                result2.Message1 = LM.Get("authAltRegTemplateHeader");
                                result2.Message2 = LM.Get("authAltRegGroupNotFound");
                                return result2;
                            }

                            group = pair.Value;
                            groupName = pair.Key;
                            var url = group.ESICustomAuthRoles.Any()
                                ? ServerPaths.GetCustomAuthUrl(rawIp, group.ESICustomAuthRoles, user.GroupName,
                                    longCharacterId)
                                : ServerPaths.GetAuthUrl(rawIp, groupName, longCharacterId);

                            var redirect = WebQueryResult.RedirectUrl;
                            redirect.AddValue("url", url);
                            return redirect;
                        }

                        if (mainCharId > 0)
                        {
                            var refreshToken = result[1];
                            var altCharId = longCharacterId;
                            var user = await DbHelper.GetAuthUser(mainCharId, true);
                            //do not allow to bind alt to another alt
                            if (user == null || user.AuthState != (int)UserStatusEnum.Authed || user.MainCharacterId.HasValue || user.DiscordId == 0)
                            {
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}", Category);
                                var result2 = WebQueryResult.BadRequestToGeneralAuth;
                                result2.Message1 = LM.Get("authAltRegTemplateHeader");
                                result2.Message2 = LM.Get("authAltRegMainNotFound");
                                return result2;
                            }

                            var pair = grps.FirstOrDefault(a => a.Value.BindToMainCharacter);
                            if (pair.Value == null)
                            {
                                await LogHelper.LogWarning($"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}",  Category);
                                var result2 = WebQueryResult.BadRequestToGeneralAuth;
                                result2.Message1 = LM.Get("authAltRegTemplateHeader");
                                result2.Message2 = LM.Get("authAltRegGroupNotFound");
                                return result2;
                            }

                            group = pair.Value;
                            groupName = pair.Key;

                            var altUser = await user.CreateAlt(altCharId, refreshToken, group, groupName,
                                mainCharId);
                            altUser.Ip = rawIp;
                            await DbHelper.SaveAuthUser(altUser);



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
                                await LogHelper.LogWarning($"Invalid named group auth attempt (missing token) from charID: {characterID} grp: {inputGroupName}", Category);
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
                            await DbHelper.DeleteAuthUser(Convert.ToInt64(characterID));
                            var refreshToken = result[1];

                            var uid = GetUniqID();
                            var authUser = new ThdAuthUser()
                            {
                                CharacterId = Convert.ToInt64(characterID),
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
                                            $"{group.DefaultMention} {LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName)}")
                                        .ConfigureAwait(false);
                                await LogHelper.LogWarning(LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName), LogCat.AuthWeb);
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

        #region OLD AUTH
        /*public async Task<bool> Auth(HttpListenerRequestEventArgs context)
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
                RunningRequestCount++;
                if (request.Url.LocalPath == "/auth" || request.Url.LocalPath == $"{extPort}/auth" ||
                    request.Url.LocalPath == $"{port}/auth")
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');

                    if (prms.Length == 0 || prms[0].Split('=').Length == 0 || string.IsNullOrEmpty(prms[0]))
                    {
                        await WebServerModule.WriteResponce(WebServerModule.Get404Page(), response);
                        return true;
                    }

                    var grps = Settings.WebAuthModule.GetEnabledAuthGroups();

                    var
                        groupName = HttpUtility.UrlDecode(
                            prms[0].Split('=')[
                                1]); //string.IsNullOrEmpty(Settings.WebAuthModule.DefaultAuthGroup) || !Settings.WebAuthModule.AuthGroups.ContainsKey(Settings.WebAuthModule.DefaultAuthGroup) ? Settings.WebAuthModule.AuthGroups.Keys.FirstOrDefault() : Settings.WebAuthModule.DefaultAuthGroup;
                    if (!grps.Keys.ContainsCaseInsensitive(groupName) && !DEF_NOGROUP_NAME.Equals(groupName) &&
                        !DEF_ALTREGGROUP_NAME.Equals(groupName))
                    {
                        await WebServerModule.WriteResponce(WebServerModule.Get404Page(), response);
                        return true;
                    }

                    if (!grps.Keys.ContainsCaseInsensitive(groupName) && DEF_NOGROUP_NAME.Equals(groupName))
                    {
                        var url = WebServerModule.GetAuthUrlOneButton(remoteAddress);
                        await response.RedirectAsync(new Uri(url));
                    }
                    else if (!grps.Keys.ContainsCaseInsensitive(groupName) && DEF_ALTREGGROUP_NAME.Equals(groupName))
                    {

                        var url = WebServerModule.GetAuthUrlAltRegButton(remoteAddress);
                        var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{authUrl}", url)
                            .Replace("{authButtonDiscordText}", LM.Get("authAltRegTemplateHeader"))
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{header}", LM.Get("authAltRegTemplateHeader"))
                            .Replace("{body}", LM.Get("authAltRegBody")).Replace("{backText}", LM.Get("backText"));
                        await WebServerModule.WriteResponce(text, response);

                        //await response.RedirectAsync(new Uri(url));
                    }
                    else
                    {
                        var grp = GetGroupByName(groupName).Value;
                        var url = grp.MustHaveGroupName ||
                                  (Settings.WebAuthModule.UseOneAuthButton && grp.ExcludeFromOneButtonMode)
                            ? WebServerModule.GetCustomAuthUrl(remoteAddress, grp.ESICustomAuthRoles, groupName)
                            : WebServerModule.GetAuthUrl(remoteAddress);

                        var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{authUrl}", url)
                            .Replace("{authButtonDiscordText}", Settings.WebAuthModule.AuthButtonDiscordText)
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                            .Replace("{header}", LM.Get("authTemplateHeader"))
                            .Replace("{body}", LM.Get("authTemplateInv")).Replace("{backText}", LM.Get("backText"));
                        await WebServerModule.WriteResponce(text, response);
                    }

                    return true;
                }

                if ((request.Url.LocalPath == "/callback" || request.Url.LocalPath == $"{extPort}/callback" ||
                     request.Url.LocalPath == $"{port}/callback")
                    && request.Url.Query.Contains("&state=authst"))
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    var code = prms[0].Split('=')[1];
                    // var groupInput = prms.FirstOrDefault(a => a.StartsWith("authst"));
                    //groupInput = groupInput?.Substring(5, groupInput.Length - 5);

                    var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId,
                        Settings.WebServerModule.CcpAppSecret);
                    if (result == null)
                    {
                        var message = LM.Get("ESIFailure");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3)
                            .Replace("{message}", message)
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
                                .Replace("{header}", LM.Get("authTokenHeader"))
                                .Replace("{body}", LM.Get("authTokenBodyFail"))
                                .Replace("{backText}", LM.Get("backText")),
                            response);
                        return true;
                    }

                    var grps = Settings.WebAuthModule.GetEnabledAuthGroups();

                    if (grps.Values.All(g =>
                        g.StandingsAuth == null || !g.StandingsAuth.CharacterIDs.Contains(numericCharId)))
                    {
                        await LogHelper.LogWarning($"Unauthorized auth stands feed request from {characterID}");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                                .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                .Replace("{message}", LM.Get("authTokenInvalid"))
                                .Replace("{header}", LM.Get("authTokenHeader"))
                                .Replace("{body}", LM.Get("authTokenBodyFail"))
                                .Replace("{backText}", LM.Get("backText")),
                            response);
                        return true;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                    await SQLHelper.DeleteAuthStands(numericCharId);
                    var data = new AuthStandsEntity { CharacterID = numericCharId, Token = result[1] };

                    var tq = await APIHelper.ESIAPI.RefreshToken(data.Token, Settings.WebServerModule.CcpAppClientId,
                        Settings.WebServerModule.CcpAppSecret, $"From {Category} | Char ID: {characterID}");
                    var token = tq.Result;

                    if (!tq.Data.IsFailed)
                        await RefreshStandings(data, token);
                    await SQLHelper.SaveAuthStands(data);

                    await LogHelper.LogInfo($"Auth stands feed added for character: {characterID}({rChar.name})",
                        LogCat.AuthWeb);
                    //TODO better screen?
                    await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifySuccess)
                        .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                        .Replace("{body2}", LM.Get("authStandsTokenRcv", rChar.name))
                        .Replace("{body}", LM.Get("authTokenRcv")).Replace("{header}", LM.Get("authStandsTokenHeader"))
                        .Replace("{backText}", LM.Get("backText")), response);
                    return true;
                }

                if ((request.Url.LocalPath == "/callback" || request.Url.LocalPath == $"{extPort}/callback" ||
                     request.Url.LocalPath == $"{port}/callback")
                    && (!request.Url.Query.Contains("&state=") || request.Url.Query.Contains("&state=x") ||
                        request.Url.Query.Contains("&state=oneButton") || request.Url.Query.Contains("&state=altReg")))
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
                            ip = Encoding.UTF8.GetString(
                                Convert.FromBase64String(HttpUtility.UrlDecode(lst.LastOrDefault())));
                        }

                        var inputGroupName = state?.Length > 1
                            ? HttpUtility.UrlDecode(state.Substring(1, state.Length - 1))
                            : null;
                        var inputGroup = GetGroupByName(inputGroupName).Value;
                        var autoSearchGroup = inputGroup == null && (state?.Equals("oneButton") ?? false);
                        var altCharReg = inputGroup == null && (state?.Equals("altReg") ?? false);

                        var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId,
                            Settings.WebServerModule.CcpAppSecret);
                        if (result == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3)
                                .Replace("{message}", LM.Get("ESIFailure"))
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
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3)
                                .Replace("{message}", LM.Get("ESIFailure"))
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
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3)
                                .Replace("{message}", LM.Get("ESIFailure"))
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

                        var grps = Settings.WebAuthModule.GetEnabledAuthGroups();

                        //alt character registration check
                        if (altCharReg)
                        {
                            var user = await DbHelper.GetAuthUser(longCharacterId, true);
                            //do not allow to bind alt to another alt
                            if (user == null || user.AuthState != (int)UserStatusEnum.Authed || user.MainCharacterId.HasValue || user.DiscordId == 0)
                            {
                                await WebServerModule.WriteResponce(
                                    WebServerModule.GetAccessDeniedPage(LM.Get("authAltRegTemplateHeader"),
                                        LM.Get("authAltRegMainNotFound"), WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning(
                                    $"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}",
                                    Category);
                                return true;
                            }

                            var pair = grps.FirstOrDefault(a => a.Value.BindToMainCharacter);
                            if (pair.Value == null)
                            {
                                await WebServerModule.WriteResponce(
                                    WebServerModule.GetAccessDeniedPage(LM.Get("authAltRegTemplateHeader"),
                                        LM.Get("authAltRegGroupNotFound"), WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning(
                                    $"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}",
                                    Category);
                                return true;
                            }

                            group = pair.Value;
                            groupName = pair.Key;
                            var url = group.ESICustomAuthRoles.Any()
                                ? WebServerModule.GetCustomAuthUrl(rawIp, group.ESICustomAuthRoles, user.GroupName,
                                    longCharacterId)
                                : WebServerModule.GetAuthUrl(rawIp, groupName, longCharacterId);
                            await response.RedirectAsync(new Uri(url));

                            return true;
                        }

                        if (mainCharId > 0)
                        {
                            var refreshToken = result[1];
                            var altCharId = longCharacterId;
                            var user = await DbHelper.GetAuthUser(mainCharId, true);
                            //do not allow to bind alt to another alt
                            if (user == null || user.AuthState != (int)UserStatusEnum.Authed || user.MainCharacterId.HasValue || user.DiscordId == 0)
                            {
                                await WebServerModule.WriteResponce(
                                    WebServerModule.GetAccessDeniedPage(LM.Get("authAltRegTemplateHeader"),
                                        LM.Get("authAltRegMainNotFound"), WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning(
                                    $"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}",
                                    Category);
                                return true;
                            }

                            var pair = grps.FirstOrDefault(a => a.Value.BindToMainCharacter);
                            if (pair.Value == null)
                            {
                                await WebServerModule.WriteResponce(
                                    WebServerModule.GetAccessDeniedPage(LM.Get("authAltRegTemplateHeader"),
                                        LM.Get("authAltRegGroupNotFound"), WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning(
                                    $"{LM.Get("authAltRegMainNotFound")} {characterID} grp: {inputGroupName}",
                                    Category);
                                return true;
                            }

                            group = pair.Value;
                            groupName = pair.Key;

                            var altUser = await user.CreateAlt(altCharId, refreshToken, group, groupName,
                                mainCharId);
                            altUser.Ip = ip;
                            await DbHelper.SaveAuthUser(altUser);

                            if (Settings.WebAuthModule.AuthReportChannel > 0)
                                await APIHelper.DiscordAPI.SendMessageAsync(Settings.WebAuthModule.AuthReportChannel,
                                    LM.Get("authReportCreateAlt", altUser.DataView.CharacterName, user.DataView.CharacterName));

                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2)
                                    .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                    .Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                    .Replace("{image}", image)
                                    .Replace("{uid}", null)
                                    .Replace("{header}", LM.Get("authAltRegTemplateHeader"))
                                    .Replace("{body}",
                                        LM.Get("authAltRegAccepted", rChar.name, user.DataView.CharacterName))
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
                                await WebServerModule.WriteResponce(
                                    WebServerModule.GetAccessDeniedPage("Auth Module", LM.Get("authNoTokenReceived"),
                                        WebServerModule.GetAuthPageUrl()), response);
                                await LogHelper.LogWarning(
                                    $"Invalid named group auth attempt (missing token) from charID: {characterID} grp: {inputGroupName}",
                                    Category);
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
                            if (autoSearchGroup && group.ESICustomAuthRoles.Any()
                            ) //for one button with ESI - had to auth twice
                            {
                                await response.RedirectAsync(new Uri(
                                    WebServerModule.GetCustomAuthUrl(rawIp ?? remoteAddress, group.ESICustomAuthRoles,
                                        groupName)));
                                return true;
                            }

                            //cleanup prev auth
                            await DbHelper.DeleteAuthUser(Convert.ToInt64(characterID));
                            var refreshToken = result[1];

                            var uid = GetUniqID();
                            var authUser = new ThdAuthUser()
                            {
                                CharacterId = Convert.ToInt64(characterID),
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
                                Ip = ip
                            };
                            await authUser.UpdateData();
                            await DbHelper.SaveAuthUser(authUser, refreshToken);

                            if (!group.PreliminaryAuthMode)
                            {
                                await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2)
                                        .Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                        .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                        .Replace("{image}", image)
                                        .Replace("{uid}", $"{Settings.Config.BotDiscordCommandPrefix}auth {uid}")
                                        .Replace("{header}", LM.Get("authTemplateHeader"))
                                        .Replace("{body}", LM.Get("authTemplateSucc1", rChar.name))
                                        .Replace("{body2}", LM.Get("authTemplateSucc2"))
                                        .Replace("{body3}", LM.Get("authTemplateSucc3"))
                                        .Replace("{backText}", LM.Get("backText")),
                                    response);
                                if (SettingsManager.Settings.WebAuthModule.AuthReportChannel != 0)
                                    await APIHelper.DiscordAPI
                                        .SendMessageAsync(SettingsManager.Settings.WebAuthModule.AuthReportChannel,
                                            $"{group.DefaultMention} {LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName)}")
                                        .ConfigureAwait(false);
                                await LogHelper.LogWarning(
                                    LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName),
                                    LogCat.AuthWeb);
                            }
                            else
                            {
                                if (!group.SkipDiscordAuthPage)
                                    await WebServerModule.WriteResponce(File
                                            .ReadAllText(SettingsManager.FileTemplateAuth2)
                                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                            .Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                            .Replace("{image}", image)
                                            .Replace("{uid}", $"{Settings.Config.BotDiscordCommandPrefix}auth confirm {uid}")
                                            .Replace("{header}", LM.Get("authTemplateHeader"))
                                            .Replace("{body}", LM.Get("authTemplateManualAccept", rChar.name))
                                            .Replace("{body3}", LM.Get("authTemplateManualAccept3"))
                                            .Replace("{body2}", LM.Get("authTemplateManualAccept2"))
                                            .Replace("{backUrl}", WebServerModule.GetWebSiteUrl())
                                            .Replace("{backText}", LM.Get("backText")),
                                        response);
                                else
                                    await WebServerModule.WriteResponce(File
                                            .ReadAllText(SettingsManager.FileTemplateAuth2)
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
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3)
                                .Replace("{message}", message)
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
            finally
            {
                RunningRequestCount--;
            }

            return false;
        }*/
        #endregion

        public static bool HasAuthAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleAuthWeb) return false;
            return SettingsManager.Settings.WebAuthModule.GetEnabledAuthGroups()
                .Where(a => a.Value.StandingsAuth != null).SelectMany(a => a.Value.StandingsAuth.CharacterIDs)
                .Contains(id);
        }
    }
}
