using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public class WebAuthModule: AppModuleBase
    {
        public override LogCat Category => LogCat.AuthWeb;

        public WebAuthModule()
        {
            WebServerModule.ModuleConnectors.Add(Reason, Auth);
        }

        public static async Task<string[]> GetCHaracterIdFromCode(string code, string clientID, string secret)
        {
            var result = await APIHelper.ESIAPI.GetAuthToken(code, clientID, secret);
            var accessToken = result[0];

            if (accessToken == null) return null;

            using (var authWebHttpClient = new HttpClient())
            {
                authWebHttpClient.DefaultRequestHeaders.Clear();
                authWebHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                var tokenresponse = await authWebHttpClient.GetAsync("https://login.eveonline.com/oauth/verify");
                var verifyString = await tokenresponse.Content.ReadAsStringAsync();
                if(JObject.Parse(verifyString)["error"]?.ToString() == "invalid_token")
                    return null;
                authWebHttpClient.DefaultRequestHeaders.Clear();
                return new[] {(string) JObject.Parse(verifyString)["CharacterID"], result[1]};
            }

        }

        public async Task<bool> Auth(HttpListenerRequestEventArgs context)
        {
            var esiFailure = false;
            var request = context.Request;
            var response = context.Response;

            var clientID = SettingsManager.Get("auth","ccpAppClientId");
            var secret = SettingsManager.Get("auth","ccpAppSecret");
            var url = SettingsManager.Get("auth","discordUrl");
            var extIp = SettingsManager.Get("webServerModule", "webExternalIP");
            var extPort = SettingsManager.Get("webServerModule", "webExternalPort");
            var port = SettingsManager.Get("webServerModule", "webListenPort");
            var callbackurl =  $"http://{extIp}:{extPort}/callback.php";


            if (request.HttpMethod != HttpMethod.Get.ToString())
                return false;
            try
            {
                if (request.Url.LocalPath == "/auth.php" || request.Url.LocalPath == $"{extPort}/auth.php"|| request.Url.LocalPath == $"{port}/auth.php")
                {
                    response.Headers.ContentEncoding.Add("utf-8");
                    response.Headers.ContentType.Add("text/html;charset=utf-8");
                    var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{callbackurl}", callbackurl).Replace("{client_id}", clientID)
                        .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{body}", LM.Get("authTemplateInv"));
                    await response.WriteContentAsync(text);
                    return true;
                }

                if (request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php"
                    && !request.Url.Query.Contains("&state=11"))
                {
                    var assembly = Assembly.GetEntryAssembly();
                    // var temp = assembly.GetManifestResourceNames();
                    var resource = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.Discord-01.png");
                    var buffer = new byte[resource.Length];
                    resource.Read(buffer, 0, Convert.ToInt32(resource.Length));
                    var image = Convert.ToBase64String(buffer);
                    var uid = GetUniqID();
                    var add = false;

                    if (!string.IsNullOrWhiteSpace(request.Url.Query))
                    {
                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms[0].Split('=')[1];
                        var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        var result = await GetCHaracterIdFromCode(code, clientID, secret);
                        if (result == null)
                            esiFailure = true;
                        var characterID = result?[0];

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                        if (state == "9") //refresh token fetch ops
                        {
                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                            if (string.IsNullOrEmpty(characterID))
                            {
                                await LogHelper.LogWarning("Bad or outdated notify feed request!");
                                await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                                    .Replace("{message}", LM.Get("authTokenBadRequest"))
                                    .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")));
                                return true;
                            }

                            if (SettingsManager.GetSubList("notifications", "keys").All(key => key["characterID"] != characterID))
                            {
                                await LogHelper.LogWarning($"Unathorized notify feed request from {characterID}");
                                await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail)
                                    .Replace("{message}", LM.Get("authTokenInvalid"))
                                    .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")));
                                return true;
                            }

                            await SQLiteHelper.SQLiteDataInsertOrUpdateTokens(result[1], characterID?.ToString(), null);
                            await LogHelper.LogInfo($"Notification feed added for character: {characterID}", LogCat.AuthWeb);
                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuthNotifySuccess)
                                .Replace("{body2}", string.Format(LM.Get("authTokenRcv2"), rChar.name))
                                .Replace("{body}", LM.Get("authTokenRcv")).Replace("{header}", LM.Get("authTokenHeader")));
                            return true;
                        }

                        var authgroups = SettingsManager.GetSubList("auth", "authgroups");
                        var corps = new Dictionary<string, string>();
                        var alliance = new Dictionary<string, string>();

                        string allianceID;
                        string corpID;
                        foreach (var config in authgroups)
                        {
                            var configChildren = config.GetChildren().ToList();

                            corpID = configChildren.FirstOrDefault(x => x.Key == "corpID")?.Value ?? "";
                            allianceID = configChildren.FirstOrDefault(x => x.Key == "allianceID")?.Value ?? "";
                            var memberRole = configChildren.FirstOrDefault(x => x.Key == "memberRole")?.Value ?? "";

                            if (Convert.ToInt32(corpID) != 0)
                                corps.Add(corpID, memberRole);
                            if (Convert.ToInt32(allianceID) != 0)
                                alliance.Add(allianceID, memberRole);
                        }

                        if (rChar == null)
                            esiFailure = true;
                        corpID = rChar?.corporation_id.ToString() ?? "0";

                        var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar?.corporation_id, true);
                        if (rCorp == null)
                            esiFailure = true;
                        allianceID = rCorp?.alliance_id.ToString() ?? "0";

                        if (corps.ContainsKey(corpID))
                            add = true;
                        else if (alliance.ContainsKey(allianceID))
                            add = true;
                        else if (corps.Count == 0 && alliance.Count == 0)
                            add = true;

                        if (!esiFailure && add)
                        {
                            await SQLiteHelper.SQLiteDataInsertOrUpdate("pendingUsers", new Dictionary<string, object>
                            {
                                {"characterID", characterID},
                                {"corporationID", corpID},
                                {"allianceID", allianceID},
                                {"authString", uid},
                                {"active", "1"},
                                {"dateCreated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}
                            });

                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");

                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth2).Replace("{url}", url).Replace("{image}", image)
                                .Replace("{uid}", uid).Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{body}", string.Format(LM.Get("authTemplateSucc1"), rChar.name))
                                .Replace("{body2}", LM.Get("authTemplateSucc2")).Replace("{body3}", LM.Get("authTemplateSucc3")));
                        }
                        else if (!esiFailure)
                        {
                            var message = "ERROR";
                            if (!add)
                                message = LM.Get("authNonAlly");
                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                                .Replace("{header}", LM.Get("authTemplateHeader")));
                        }
                        else
                        {
                            var message = LM.Get("ESIFailure");
                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                                .Replace("{header}", LM.Get("authTemplateHeader")));
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

        private static string GetUniqID()
        {
            var ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            double t = ts.TotalMilliseconds / 1000;

            int a = (int)Math.Floor(t);
            int b = (int)((t - Math.Floor(t)) * 1000000);

            return a.ToString("x8") + b.ToString("x5");
        }


        internal static async Task AuthUser(ICommandContext context, string remainder)
        {
            try
            {
                var esiFailed = false;
                var responce = await SQLiteHelper.GetPendingUser(remainder);

                if (!responce.Any())
                {
                    await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, $"{await APIHelper.DiscordAPI.GetMentionedUserString(context)} {LM.Get("authHasInvalidKey")}");
                }
                else switch (responce[0]["active"].ToString())
                {
                    case "0":
                        await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, $"{await APIHelper.DiscordAPI.GetMentionedUserString(context)} {LM.Get("authHasInactiveKey")}");
                        break;
                    case "1":
                        var authgroups = SettingsManager.GetSubList("auth", "authgroups");
                        var corps = new Dictionary<string, string>();
                        var alliance = new Dictionary<string, string>();

                        foreach (var config in authgroups)
                        {
                            var configChildren = config.GetChildren().ToList();

                            var corpID2 = configChildren.FirstOrDefault(x => x.Key == "corpID")?.Value ?? "";
                            var allianceID2 = configChildren.FirstOrDefault(x => x.Key == "allianceID")?.Value ?? "";
                            var memberRole = configChildren.FirstOrDefault(x => x.Key == "memberRole")?.Value ?? "";

                            if (Convert.ToInt32(corpID2) != 0)
                                corps.Add(corpID2, memberRole);
                            if (Convert.ToInt32(allianceID2) != 0)
                                alliance.Add(allianceID2, memberRole);
                        }

                        var characterID = responce[0]["characterID"].ToString();
                        var characterData = await APIHelper.ESIAPI.GetCharacterData("WebAuth", characterID, true);
                        if (characterData == null)
                            esiFailed = true;

                        var corporationData = await APIHelper.ESIAPI.GetCorporationData("WebAuth", characterData.corporation_id);
                        if (corporationData == null)
                            esiFailed = true;

                        var allianceID = characterData.alliance_id.ToString();
                        var corpID = characterData.corporation_id.ToString();

                        bool enable = corps.ContainsKey(corpID) || characterData.alliance_id != null && alliance.ContainsKey(allianceID) || (corps.Count == 0 && alliance.Count == 0);

                        if (enable && !esiFailed)
                        {
                            await APIHelper.DiscordAPI.AuthGrantRoles(context, characterID, corps, alliance, characterData, corporationData, remainder);
                        }
                        else
                        {
                            await context.Channel.SendMessageAsync(LM.Get("ESIFailure"));
                            await LogHelper.LogError("ESI Failure", LogCat.AuthWeb);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Error: {ex.Message}", ex, LogCat.AuthWeb);
            }
        }
    }
}
