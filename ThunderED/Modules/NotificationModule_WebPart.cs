using System;
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

                    await DbHelper.UpdateToken(result[1], numericCharId, TokenEnum.Notification);
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

        public static bool HasAuthAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleNotificationFeed) return false;
            return TickManager.GetModule<NotificationModule>()?.GetAllParsedCharacters().Contains(id) ?? false;
        }
    }
}
