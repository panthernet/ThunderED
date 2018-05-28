using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;
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
                    var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{callbackurl}", callbackurl).Replace("{client_id}", Settings.WebServerModule.CcpAppClientId)
                        .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{body}", LM.Get("authTemplateInv")).Replace("{backText}", LM.Get("backText"));
                    await response.WriteContentAsync(text);
                    return true;
                }

                if (request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php"
                    && !request.Url.Query.Contains("&state="))
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
                       // var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        var result = await GetCHaracterIdFromCode(code, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                        if (result == null)
                            esiFailure = true;
                        var characterID = result?[0];

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);

                        var foundList = new List<int>();
                        foreach (var group in Settings.WebAuthModule.AuthGroups.Values)
                        {
                            if (group.CorpID != 0)
                                foundList.Add(group.CorpID);
                            if (group.AllianceID != 0)
                                foundList.Add(group.AllianceID);
                        }

                        if (rChar == null)
                            esiFailure = true;
                        var corpID = rChar?.corporation_id ?? 0;

                        var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar?.corporation_id, true);
                        if (rCorp == null)
                            esiFailure = true;
                        var allianceID = rCorp?.alliance_id ?? 0;

                        if (corpID != 0 && foundList.Contains(corpID) || allianceID != 0 && foundList.Contains(allianceID) || foundList.Count == 0)
                            add = true;

                        if (!esiFailure && add)
                        {
                            await SQLHelper.SQLiteDataInsertOrUpdate("pendingUsers", new Dictionary<string, object>
                            {
                                {"characterID", characterID},
                                {"corporationID", corpID.ToString()},
                                {"allianceID", allianceID.ToString()},
                                {"authString", uid},
                                {"active", "1"},
                                {"groups", "[]"},
                                {"dateCreated", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}
                            });

                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");

                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth2).Replace("{url}", Settings.WebServerModule.DiscordUrl).Replace("{image}", image)
                                .Replace("{uid}", uid).Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{body}", string.Format(LM.Get("authTemplateSucc1"), rChar.name))
                                .Replace("{body2}", LM.Get("authTemplateSucc2")).Replace("{body3}", LM.Get("authTemplateSucc3")).Replace("{backText}", LM.Get("backText")));
                        }
                        else if (!esiFailure)
                        {
                            var message = "ERROR";
                            if (!add)
                                message = LM.Get("authNonAlly");
                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")));
                        }
                        else
                        {
                            var message = LM.Get("ESIFailure");
                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")));
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
                var responce = await SQLHelper.GetPendingUser(remainder);

                if (!responce.Any())
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel, LM.Get("authHasInvalidKey"), true).ConfigureAwait(false);
                }
                else switch (responce[0]["active"].ToString())
                {
                    case "0":
                        await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel,LM.Get("authHasInactiveKey"), true).ConfigureAwait(false);
                        break;
                    case "1":
                       // var authgroups = SettingsManager.GetSubList("auth", "authgroups");
                       // var corps = new Dictionary<string, string>();
                       // var alliance = new Dictionary<string, string>();

                        var foundList = new Dictionary<int, List<string>>();
                        foreach (var group in TickManager.GetModule<WebAuthModule>().Settings.WebAuthModule.AuthGroups.Values)
                        {
                            if (group.CorpID != 0)
                                foundList.Add(group.CorpID, group.MemberRoles);
                            if (group.AllianceID != 0)
                                foundList.Add(group.AllianceID, group.MemberRoles);
                        }

                        var characterID = responce[0]["characterID"].ToString();
                        var characterData = await APIHelper.ESIAPI.GetCharacterData("WebAuth", characterID, true);
                        if (characterData == null)
                            esiFailed = true;

                        var corporationData = await APIHelper.ESIAPI.GetCorporationData("WebAuth", characterData.corporation_id);
                        if (corporationData == null)
                            esiFailed = true;

                        var allianceID = characterData.alliance_id ?? 0;
                        var corpID = characterData.corporation_id;

                        bool enable = foundList.ContainsKey(corpID) || characterData.alliance_id != null && foundList.ContainsKey(allianceID) || foundList.Count == 0;

                        if (enable && !esiFailed)
                        {
                            await AuthGrantRoles(context, characterID, foundList, characterData, corporationData, remainder);
                        }
                        else
                        {
                            await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, LM.Get("ESIFailure")).ConfigureAwait(false);
                            await LogHelper.LogError("ESI Failure", LogCat.AuthWeb).ConfigureAwait(false);
                        }

                        break;
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Error: {ex.Message}", ex, LogCat.AuthWeb).ConfigureAwait(false);
            }
        }

        private static async Task AuthGrantRoles(ICommandContext context, string characterID, Dictionary<int, List<string>> foundList, JsonClasses.CharacterData characterData, JsonClasses.CorporationData corporationData, string remainder)
        {
            var rolesToAdd = new List<SocketRole>();

            var allianceID = characterData.alliance_id ?? 0;
            var corpID = characterData.corporation_id;

            var authSettings = TickManager.GetModule<WebAuthModule>()?.Settings.WebAuthModule;

            try
            {
                //Check for Corp roles
                if (foundList.ContainsKey(corpID))
                {
                    var cRoles = foundList[corpID];
                    cRoles.ForEach(a =>
                    {
                        var f = APIHelper.DiscordAPI.GetGuildRole(a);
                        if(f != null && !rolesToAdd.Contains(f))
                            rolesToAdd.Add(f);
                    });
                }

                //Check for Alliance roles
                if (foundList.ContainsKey(allianceID))
                {
                    var cRoles = foundList[allianceID];
                    cRoles.ForEach(a =>
                    {
                        var f = APIHelper.DiscordAPI.GetGuildRole(a);
                        if(f != null && !rolesToAdd.Contains(f))
                            rolesToAdd.Add(f);
                    });
                }

                var discordUser = APIHelper.DiscordAPI.GetUser(context.Message.Author.Id);

                if (authSettings.AuthReportChannel != 0)
                    await APIHelper.DiscordAPI.SendMessageAsync(authSettings.AuthReportChannel, string.Format(LM.Get("grantRolesMessage"), characterData.name))
                        .ConfigureAwait(false);
                await APIHelper.DiscordAPI.AssignRolesToUser(discordUser, rolesToAdd);

                var rolesString = new StringBuilder();
                foreach (var role in discordUser.Roles)
                {
                    if(role.Name.StartsWith("@everyone")) continue;
                    rolesString.Append(role.Name.Replace("\"", "&br;"));
                    rolesString.Append(",");
                }
                if(rolesString.Length > 0)
                    rolesString.Remove(rolesString.Length-1, 1);

                await SQLHelper.SQLiteDataUpdate("pendingUsers", "active", "0", "authString", remainder);

                await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, string.Format(LM.Get("msgAuthSuccess"), context.Message.Author.Mention, characterData.name));
                var eveName = characterData.name;
                var discordID = discordUser.Id;
                var addedOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                await SQLHelper.SQLiteDataInsertOrUpdate("authUsers", new Dictionary<string, object>
                {
                    {"eveName", eveName},
                    {"characterID", characterID},
                    {"discordID", discordID.ToString()},
                    {"role", rolesString.ToString()},
                    {"active", "yes"},
                    {"addedOn", addedOn}
                });
               
                if (authSettings.EnforceCorpTickers || authSettings.EnforceCharName)
                {
                    var nickname = "";
                    if (authSettings.EnforceCorpTickers)
                        nickname = $"[{corporationData.ticker}] ";
                    if (authSettings.EnforceCharName)
                        nickname += $"{eveName}";
                    else
                        nickname += $"{discordUser.Username}";

                    try
                    {
                        //will throw ex on admins
                        await discordUser.ModifyAsync(x => x.Nickname = nickname);
                    }
                    catch
                    {
                        //ignore
                    }

                    await APIHelper.DiscordAPI.Dupes(discordUser);
                }
            }

            catch (Exception ex)
            {
                await LogHelper.LogEx($"Failed adding Roles to User {characterData.name}, Reason: {ex.Message}", ex, LogCat.Discord);
            }
        }

    }
}
