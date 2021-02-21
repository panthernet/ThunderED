using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public partial class NotificationModule
    {
        private async Task WebPartInitialization()
        {
            if (WebServerModule.WebModuleConnectors.ContainsKey(Reason))
                WebServerModule.WebModuleConnectors.Remove(Reason); 
            WebServerModule.WebModuleConnectors.Add(Reason, ProcessRequest);
            await Task.CompletedTask;
        }

        public async Task<WebQueryResult> ProcessRequest(string query, CallbackTypeEnum type, string ip, WebAuthUserData data)
        {
            if (!Settings.Config.ModuleNotificationFeed)
                return WebQueryResult.False;

            try
            {
                RunningRequestCount++;
                if (query.Contains("&state=9"))
                {
                    //var prms = QueryHelpers.ParseQuery(query);
                    var prms = query.TrimStart('?').Split('&');
                    var code = prms[0].Split('=')[1];

                    var result = await WebAuthModule.GetCharacterIdFromCode(code,
                        Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                    if (result == null)
                        return WebQueryResult.EsiFailure;

                    var characterId = result[0];
                    var numericCharId = Convert.ToInt64(characterId);

                    if (string.IsNullOrEmpty(characterId))
                    {
                        await LogHelper.LogWarning("Bad or outdated notify feed request!", Category);
                        var r = WebQueryResult.BadRequestToSystemAuth;
                        r.Message1 = LM.Get("authTokenBodyFail");
                        r.Message2 = LM.Get("authTokenBadRequest");
                        return r;
                    }

                    if (!TickManager.GetModule<NotificationModule>().IsValidCharacter(numericCharId))
                    {
                        await LogHelper.LogWarning($"Unauthorized notify feed request from {characterId}", Category);
                        var r = WebQueryResult.BadRequestToSystemAuth;
                        r.Message1 = LM.Get("authTokenBodyFail");
                        return r;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);

                    await SQLHelper.InsertOrUpdateTokens(result[1] ?? "", characterId, null, null);
                    await LogHelper.LogInfo($"Notification feed added for character: {characterId}", Category);

                    var res = WebQueryResult.FeedAuthSuccess;
                    res.Message1 = LM.Get("authTokenRcv");
                    res.Message2 = LM.Get("authTokenRcv2", rChar.name);
                    return res;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                RunningRequestCount--;
            }

            return WebQueryResult.False;
        }


        #region OLD AUTH

        public async Task<bool> Auth(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleNotificationFeed) return false;

            var request = context.Request;
            var response = context.Response;
            var extPort = Settings.WebServerModule.WebExternalPort;
            var port = Settings.WebServerModule.WebExternalPort;
            try
            {
                RunningRequestCount++;
                if (request.HttpMethod != HttpMethod.Get.ToString())
                    return false;
                if ((request.Url.LocalPath == "/callback" || request.Url.LocalPath == $"{extPort}/callback" ||
                     request.Url.LocalPath == $"{port}/callback")
                    && request.Url.Query.Contains("&state=9"))
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    var code = prms[0].Split('=')[1];
                    // var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                    var result = await WebAuthModule.GetCharacterIdFromCode(code,
                        Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
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
                        await LogHelper.LogWarning("Bad or outdated notify feed request!");
                        await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                                .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                                .Replace("{message}", LM.Get("authTokenBadRequest"))
                                .Replace("{header}", LM.Get("authTokenHeader"))
                                .Replace("{body}", LM.Get("authTokenBodyFail"))
                                .Replace("{backText}", LM.Get("backText")),
                            response);
                        return true;
                    }

                    if (!TickManager.GetModule<NotificationModule>().IsValidCharacter(numericCharId))
                    {
                        await LogHelper.LogWarning($"Unathorized notify feed request from {characterID}");
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

                    await SQLHelper.InsertOrUpdateTokens(result[1] ?? "", characterID, null, "");
                    await LogHelper.LogInfo($"Notification feed added for character: {characterID}", LogCat.AuthWeb);
                    await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuthNotifySuccess)
                        .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(false))
                        .Replace("{body2}", LM.Get("authTokenRcv2", rChar.name))
                        .Replace("{body}", LM.Get("authTokenRcv")).Replace("{header}", LM.Get("authTokenHeader"))
                        .Replace("{backText}", LM.Get("backText")), response);
                    return true;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                RunningRequestCount--;
            }

            return false;
        }

        #endregion

        public static bool HasAuthAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleNotificationFeed) return false;
            return TickManager.GetModule<NotificationModule>()?.GetAllParsedCharacters().Contains(id) ?? false;
        }
    }
}
