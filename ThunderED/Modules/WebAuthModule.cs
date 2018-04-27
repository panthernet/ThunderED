using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord.Commands;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class WebAuthModule: AppModuleBase
    {
        internal static System.Net.Http.HttpListener Listener;
        public override LogCat Category => LogCat.AuthWeb;

        public async Task Auth()
        {
            if (Listener == null || !Listener.IsListening)
            {
                var callbackurl = SettingsManager.Get("auth", "callbackUrl");
                var clientID = SettingsManager.Get("auth","ccpAppClientId");
                var secret = SettingsManager.Get("auth","ccpAppSecret");
                var url = SettingsManager.Get("auth","discordUrl");
                var port = SettingsManager.GetInt("auth", "webAuthListenPort");

                await LogHelper.LogInfo("Starting AuthWeb Server", Category);
                Listener = new System.Net.Http.HttpListener(IPAddress.Parse(SettingsManager.Get("auth", "webAuthListenIP")), port);
                Listener.Request += async (sender, context) =>
                {
                    var esiFailure = false;
                    var request = context.Request;
                    var response = context.Response;
                    try
                    {
                        if (request.HttpMethod == HttpMethod.Get.ToString())
                        {
                            if (request.Url.LocalPath == "/" || request.Url.LocalPath == $"{port}/")
                            {
                                //response.Headers.Add("Content-Type", "text/html;charset=windows-1252");
                                response.Headers.ContentEncoding.Add("utf-8");
                                response.Headers.ContentType.Add("text/html;charset=utf-8");
                                var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{callbackurl}", callbackurl).Replace("{client_id}", clientID)
                                    .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{body}", LM.Get("authTemplateInv"));
                                await response.WriteContentAsync(text);
                            }
                            else if (request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                            {
                                try
                                {
                                    var authWebHttpClient = new HttpClient();
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

                                        var result = await APIHelper.ESIAPI.GetAuthToken(code, clientID, secret);
                                        var accessToken = result[0];

                                        if (accessToken == null)
                                            esiFailure = true;
                                        authWebHttpClient.DefaultRequestHeaders.Clear();
                                        authWebHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                                        var tokenresponse = await authWebHttpClient.GetAsync("https://login.eveonline.com/oauth/verify");
                                        var verifyString = await tokenresponse.Content.ReadAsStringAsync();
                                        authWebHttpClient.DefaultRequestHeaders.Clear();

                                        
                                        var characterID = (string)JObject.Parse(verifyString)["CharacterID"];
                                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                                        if (state == "9") //refresh token fetch ops
                                        {
                                            if (string.IsNullOrEmpty(characterID))
                                            {
                                                await LogHelper.LogWarning("Bad or outdated notify feed request!");
                                                await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail).Replace("{message}", LM.Get("authTokenBadRequest"))
                                                    .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")));
                                                return;
                                            }

                                            if (SettingsManager.GetSubList("notifications", "keys").All(key => key["characterID"] != characterID))
                                            {
                                                await LogHelper.LogWarning($"Unathorized notify feed request from {characterID}");
                                                await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuthNotifyFail).Replace("{message}", LM.Get("authTokenInvalid"))
                                                    .Replace("{header}", LM.Get("authTokenHeader")).Replace("{body}", LM.Get("authTokenBodyFail")));
                                                return;
                                            }

                                            await SQLiteHelper.SQLiteDataInsertOrUpdateTokens(result[1], characterID?.ToString());
                                            response.Headers.ContentEncoding.Add("utf-8");
                                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                                            await LogHelper.LogInfo($"Notification feed added for character: {characterID}", LogCat.AuthWeb);
                                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuthNotifySuccess).Replace("{body2}", string.Format(LM.Get("authTokenRcv2"), rChar.name))
                                                .Replace("{body}", LM.Get("authTokenRcv")).Replace("{header}", LM.Get("authTokenHeader")));
                                            return;
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
                                            var corpMemberRole = configChildren.FirstOrDefault(x => x.Key == "corpMemberRole")?.Value ?? "";
                                            var allianceMemberRole = configChildren.FirstOrDefault(x => x.Key == "allianceMemberRole")?.Value ?? "";

                                            if (Convert.ToInt32(corpID) != 0)
                                                corps.Add(corpID, corpMemberRole);
                                            if (Convert.ToInt32(allianceID) != 0)
                                                alliance.Add(allianceID, allianceMemberRole);
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

                                        if (!esiFailure && add && (string) JObject.Parse(verifyString)["error"] != "invalid_token")
                                        {
                                            await SQLiteHelper.InsertPendingUser(characterID?.ToString(), corpID?.ToString(), allianceID?.ToString(), uid, "1", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                                            response.Headers.ContentEncoding.Add("utf-8");
                                            response.Headers.ContentType.Add("text/html;charset=utf-8");

                                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth2).Replace("{url}", url).Replace("{image}", image)
                                                .Replace("{uid}", uid).Replace("{header}", LM.Get("authTemplateHeader")).Replace("{body}", string.Format(LM.Get("authTemplateSucc1"),rChar.name))
                                                .Replace("{body2}", LM.Get("authTemplateSucc2")).Replace("{body3}", LM.Get("authTemplateSucc3")));
                                        }
                                        else if (!esiFailure)
                                        {
                                            var message = "ERROR";
                                            if (!add)
                                                message = LM.Get("authNonAlly");
                                            response.Headers.ContentEncoding.Add("utf-8");
                                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message).Replace("{header}", LM.Get("authTemplateHeader")));
                                        }
                                        else
                                        {
                                            var message = LM.Get("ESIFailure");
                                            response.Headers.ContentEncoding.Add("utf-8");
                                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message).Replace("{header}", LM.Get("authTemplateHeader")));
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    await LogHelper.LogEx($"Error: {ex.Message}", ex, Category);
                                }
                            }
                        }
                        else
                        {
                            response.MethodNotAllowed();
                        }
                    }
                    finally
                    {
                        response.Close();
                    }
                };
                Listener.Start();
            }
        }

        private static string GetUniqID()
        {
            var ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
            double t = ts.TotalMilliseconds / 1000;

            int a = (int)Math.Floor(t);
            int b = (int)((t - Math.Floor(t)) * 1000000);

            return a.ToString("x8") + b.ToString("x5");
        }


        public override async Task Run(object prm)
        {
            await Auth();
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
                            var corpMemberRole = configChildren.FirstOrDefault(x => x.Key == "corpMemberRole")?.Value ?? "";
                            var allianceMemberRole = configChildren.FirstOrDefault(x => x.Key == "allianceMemberRole")?.Value ?? "";

                            if (Convert.ToInt32(corpID2) != 0)
                                corps.Add(corpID2, corpMemberRole);
                            if (Convert.ToInt32(allianceID2) != 0)
                                alliance.Add(allianceID2, allianceMemberRole);
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

                        bool enable = corps.ContainsKey(corpID) || characterData.alliance_id != null && alliance.ContainsKey(allianceID);

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
