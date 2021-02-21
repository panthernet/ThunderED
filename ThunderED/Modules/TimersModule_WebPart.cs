using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml.Schema;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Json.Internal;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public partial class TimersModule
    {
        private async Task WebPartInitialization()
        {

            await Task.CompletedTask;
        }

        #region OLD

        private async Task<bool> OnDisplayTimers(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleTimers) return false;

            var request = context.Request;
            var response = context.Response;

            try
            {
                RunningRequestCount++;
                var extPort = Settings.WebServerModule.WebExternalPort;
                var port = Settings.WebServerModule.WebExternalPort;

                if (request.HttpMethod == HttpMethod.Get.ToString())
                {
                    if (request.Url.LocalPath == "/callback" || request.Url.LocalPath == $"{extPort}/callback" ||
                        request.Url.LocalPath == $"{port}/callback")
                    {
                        var clientID = Settings.WebServerModule.CcpAppClientId;
                        var secret = Settings.WebServerModule.CcpAppSecret;

                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms[0].Split('=')[1];
                        var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        if (state != "11") return false;

                        //state = 11 && have code
                        var result = await WebAuthModule.GetCharacterIdFromCode(code, clientID, secret);
                        if (result == null)
                        {
                            //TODO invalid auth
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }

                        var characterId = Convert.ToInt64(result[0]);

                        await SQLHelper.UpdateTimersAuth(characterId);
                        //redirect to timers
                        var iid = Convert.ToBase64String(Encoding.UTF8.GetBytes(characterId.ToString()));
                        iid = HttpUtility.UrlEncode(iid);
                        var url = $"{WebServerModule.GetTimersURL()}?data=0&id={iid}&state=11";
                        await response.RedirectAsync(new Uri(url));
                        return true;
                    }

                    if (request.Url.LocalPath == "/timers" || request.Url.LocalPath == $"{extPort}/timers" ||
                        request.Url.LocalPath == $"{port}/timers")
                    {
                        if (string.IsNullOrWhiteSpace(request.Url.Query))
                        {
                            //redirect to auth
                            await response.RedirectAsync(new Uri(WebServerModule.GetTimersAuthURL()));
                            return true;
                        }

                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        if (prms.Length != 3)
                        {
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }

                        var data = prms[0].Split('=')[1];
                        var inputId = prms[1].Split('=')[1];
                        var state = prms[2].Split('=')[1];
                        if (state != "11")
                        {
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }

                        var characterId =
                            Convert.ToInt64(
                                Encoding.UTF8.GetString(Convert.FromBase64String(HttpUtility.UrlDecode(inputId))));

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                        if (rChar == null)
                        {
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }

                        //have charId - had to check it
                        //check in db
                        var timeout = Settings.TimersModule.AuthTimeoutInMinutes;
                        if (timeout != 0)
                        {
                            var result = await SQLHelper.GetTimersAuthTime(characterId);
                            if (result == null || (DateTime.Now - DateTime.Parse(result)).TotalMinutes > timeout)
                            {
                                //redirect to auth
                                await response.RedirectAsync(new Uri(WebServerModule.GetTimersAuthURL()));
                                return true;
                            }
                        }

                        var checkResult = await CheckAccess(characterId, rChar);
                        if (!checkResult[0])
                        {
                            await WebServerModule.WriteResponce(
                                WebServerModule.GetAccessDeniedPage("Timers Module", LM.Get("accessDenied"),
                                    WebServerModule.GetWebSiteUrl()), response);
                            return true;
                        }

                        if (checkResult[1] && data.StartsWith("delete"))
                        {
                            data = data.Substring(6, data.Length - 6);
                            await SQLHelper.DeleteTimer(Convert.ToInt64(data));
                            var x = HttpUtility.ParseQueryString(request.Url.Query);
                            x.Set("data", "0");
                            await response.RedirectAsync(new Uri($"{request.Url.ToString().Split('?')[0]}?{x}"));
                            return true;
                        }

                        await WriteCorrectResponce(response, checkResult[1], characterId);
                        return true;
                    }
                }
                else if (request.HttpMethod == HttpMethod.Post.ToString())
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    if (prms.Length != 3)
                    {
                        //await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return false;
                    }

                    var inputId = prms[1].Split('=')[1];
                    var state = prms[2].Split('=')[1];
                    if (state != "11")
                    {
                        // await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return false;
                    }

                    var characterId =
                        Convert.ToInt64(
                            Encoding.UTF8.GetString(Convert.FromBase64String(HttpUtility.UrlDecode(inputId))));

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                    if (rChar == null)
                    {
                        // await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return false;
                    }

                    var checkResult = await CheckAccess(characterId, rChar);
                    if (!checkResult[0])
                        return true;

                    var data = await request.ReadContentAsStringAsync();

                    if (checkResult[1] && data != null)
                    {
                        if (data.StartsWith("delete"))
                        {
                            data = data.Substring(6, data.Length - 6);
                            await SQLHelper.DeleteTimer(Convert.ToInt64(data));
                        }
                        else
                        {
                            TimerItem entry = null;
                            try
                            {
                                entry = JsonConvert.DeserializeObject<TimerItem>(data);
                            }
                            catch
                            {
                                //ignore
                            }

                            if (entry == null)
                            {
                                await response.WriteContentAsync(LM.Get("invalidInputData"));
                                return true;
                            }

                            var iDate = entry.GetDateTime();
                            if (iDate == null)
                            {
                                await response.WriteContentAsync(LM.Get("invalidTimeFormat"));
                                return true;
                            }

                            if (iDate < DateTime.Now)
                            {
                                await response.WriteContentAsync(LM.Get("passedTimeValue"));
                                return true;
                            }

                            //save
                            entry.timerChar = rChar.name;
                            await SQLHelper.UpdateTimer(entry);
                        }

                        return true;
                    }
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

        public static bool HasWebAccess(long id, long corpId, long allianceId)
        {
            if (!SettingsManager.Settings.Config.ModuleTimers) return false;
            var module = TickManager.GetModule<TimersModule>();
            if (module == null) return false;
            return module.GetAllCharacterIds().Contains(id) || module.GetAllCorporationIds().Contains(corpId) ||
                   module.GetAllAllianceIds().Contains(allianceId);
        }

        public static bool HasWebEditorAccess(in long userId, in long corpId, in long allyId)
        {
            //todo discord roles check
            if (!SettingsManager.Settings.Config.ModuleTimers) return false;
            var module = TickManager.GetModule<TimersModule>();
            return module.GetAllParsedCharacters(module.ParsedEditLists).Contains(userId) ||
                   module.GetAllParsedCorporations(module.ParsedEditLists).Contains(corpId) ||
                   module.GetAllParsedAlliances(module.ParsedEditLists).Contains(allyId);
        }

        public static async Task<string> SaveTimer(WebTimerData data, WebAuthUserData user)
        {
            try
            {
                if (user == null || user.Id == 0) return null;

                var rChar = await APIHelper.ESIAPI.GetCharacterData(LogCat.Timers.ToString(), user.Id, true);
                if (rChar == null)
                    return LM.Get("webAuthenticationExpired");

                var module = TickManager.GetModule<TimersModule>();
                var checkResult = await module.CheckAccess(user.Id, rChar);
                if (!checkResult[0] || !checkResult[1])
                    return LM.Get("webAuthenticationExpired");

                var timer = data.FromWebTimerData(data, user);

                var iDate = timer.GetDateTime();
                if (iDate == null)
                    return LM.Get("invalidTimeFormat");

                if (iDate < DateTime.UtcNow)
                    return LM.Get("passedTimeValue");

                await SQLHelper.UpdateTimer(timer);
                return null;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(SaveTimer), ex, LogCat.Timers);
                return LM.Get("webFatalError");
            }
        }

        public static async Task<string> SaveTimerRf(WebTimerDataRf data, WebAuthUserData user)
        {
            data.PushDate();
            return await SaveTimer(data, user);
        }
    }
}
