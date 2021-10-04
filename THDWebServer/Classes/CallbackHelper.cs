using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.ProtectedBrowserStorage;
using THDWebServer.Authentication;
using ThunderED;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Modules;

namespace THDWebServer.Classes
{
    public static class CallbackHelper
    {
        public static async Task ProcessCallbackReply(NavigationManager manager, ProtectedSessionStorage store,
            CustomAuthenticationStateProvider auth, string request, string ip, CallbackTypeEnum cType)
        {
            try
            {
                await LogHelper.LogInfo($"Processing callback...", LogCat.WebServer);

                WebQueryResult result;
                if (request.Contains("&state=userauth"))
                {
                    //got global auth request
                    var prms = request.TrimStart('?').Split('&');
                    if (prms.Length == 0 || prms[0].Split('=').Length == 0 || string.IsNullOrEmpty(prms[0]))
                        result = WebQueryResult.BadRequestToGeneralAuth;
                    else
                    {
                        var code = prms[0].Split('=')[1];
                        var r = await WebAuthModule.GetCharacterIdFromCode(code,
                            SettingsManager.Settings.WebServerModule.CcpAppClientId,
                            SettingsManager.Settings.WebServerModule.CcpAppSecret);
                        if (r == null)
                            result = WebQueryResult.EsiFailure;
                        else
                        {
                            var charId = Convert.ToInt64(r[0]);
                            if (string.IsNullOrEmpty(r[0]))
                            {
                                await LogHelper.LogWarning("Bad or outdated user auth request!");
                                result = WebQueryResult.BadRequestToRoot;
                            }
                            else
                            {
                                var authUser = await DbHelper.GetAuthUser(charId);
                                if (authUser == null)
                                {
                                    result = WebQueryResult.BadRequestToRoot;
                                    result.Message1 = LM.Get("webUserIsNotAuthenticated");
                                }
                                else
                                {
                                    await auth.SaveAuth(authUser);
                                    result = new WebQueryResult(WebQueryResultEnum.RedirectUrl, "/", null, true);
                                }
                            }
                        }
                    }
                }
                else
                {
                    var data = await store.GetAsync<WebAuthUserData>("user");

                    result = await ExternalAccess.ProcessCallback(request, cType, ip, data);
                }

                if (result.HasMessage)
                {
                    await store.SafeSet("message1", result.Message1);
                    await store.SafeSet("message2", result.Message2);
                    await store.SafeSet("message3", result.Message3);
                }

                var url = result.Values.ContainsKey("url") ? result.Values["url"].ToString() : null;
                var returnUrl = result.Values.ContainsKey("returnUrl") ? result.Values["returnUrl"].ToString() : null;
                if (!string.IsNullOrEmpty(returnUrl))
                    await store.SafeSet("returnUrl", returnUrl);


                switch (result.Result)
                {
                    case WebQueryResultEnum.EsiFailure:
                    case WebQueryResultEnum.BadFeedRequest:
                    case WebQueryResultEnum.UnauthorizedFeedRequest:
                    case WebQueryResultEnum.RedirectUrl:
                    case WebQueryResultEnum.GeneralAuthFailure:
                        manager.NavigateTo(url, result.ForceRedirect);
                        return;
                    case WebQueryResultEnum.GeneralAuthSuccess:
                    case WebQueryResultEnum.BadRequest:
                    case WebQueryResultEnum.FeedSuccess:
                        await store.SafeSet(result.Values.FirstOrDefault());
                        manager.NavigateTo(url, result.ForceRedirect);
                        return;
                    case WebQueryResultEnum.False:
                        return;
                    default:
                        manager.NavigateTo("/badrq", result.ForceRedirect);
                        return;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex, LogCat.AuthWeb);
                manager.NavigateTo("/badrq", true);
            }
        }
    }
}
