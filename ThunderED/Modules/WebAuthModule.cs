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

namespace ThunderED.Modules
{
    public class WebAuthModule: AppModuleBase
    {
        private DateTime _lastTimersCheck = DateTime.MinValue;
        public override LogCat Category => LogCat.AuthWeb;

        public WebAuthModule()
        {
            LogHelper.LogModule("Inititalizing WebAuth module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, Auth);
        }

        public override async Task Run(object prm)
        {
            if (!Settings.Config.ModuleAuthWeb) return;

            if(IsRunning) return;
            IsRunning = true;
            try
            {
                if ((DateTime.Now - _lastTimersCheck).TotalMinutes < 1 ) return;
                _lastTimersCheck = DateTime.Now;

                await ProcessPreliminaryAppilicants();
                await ClearOutdatedApplicants();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private async Task ClearOutdatedApplicants()
        {
            var list = await SQLHelper.GetPendingUsers();
            foreach (var user in list)
            {
                var tokenEntry = await SQLHelper.UserTokensGetEntry(user.CharacterId);
                if(tokenEntry == null || tokenEntry.AuthState == 2) continue;
                var group = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == tokenEntry.GroupName).Value;
                if (group == null)
                {
                    await SQLHelper.SQLiteDataDelete("pendingUsers", "characterID", user.CharacterId.ToString());
                    await SQLHelper.SQLiteDataDelete("userTokens", "characterID", user.CharacterId.ToString());
                    continue;
                }

                if (group.AppInvalidationInHours > 0 && (DateTime.Now - user.CreateDate).TotalHours >= group.AppInvalidationInHours)
                {
                    await SQLHelper.SQLiteDataDelete("pendingUsers", "characterID", user.CharacterId.ToString());
                    await SQLHelper.SQLiteDataDelete("userTokens", "characterID", user.CharacterId.ToString());
                }
            }
        }

        private async Task ProcessPreliminaryAppilicants()
        {
            try
            {
                await LogHelper.LogModule("Running preliminary auth check...", Category);
                var list = await SQLHelper.UserTokensGetConfirmedDataList();
                foreach (var data in list.Where(a => Convert.ToUInt64(a[2]) != 0))
                {
                    string characterName = null;
                    try
                    {
                        var characterId = Convert.ToInt32(data[0]);
                        characterName = data[1].ToString();
                        var discordId = Convert.ToUInt64(data[2]);
                        var groupName = data[3].ToString();

                        var group = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == groupName);
                        if (group.Value == null)
                        {
                            await LogHelper.LogWarning($"Group {groupName} not found for character {characterName} awaiting auth...");
                            continue;
                        }

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                        if (rChar == null) return;

                        if (group.Value.CorpIDList.Contains(rChar.corporation_id) || (rChar.alliance_id > 0 && group.Value.AllianceIDList.Contains(rChar.alliance_id.Value)))
                        {
                            if (group.Value == null)
                            {
                                await LogHelper.LogWarning($"Unable to auth {characterName}({characterId}) as its auth group {groupName} do not exist in the settings file!",
                                    Category);
                                if (Settings.WebAuthModule.AuthReportChannel != 0)
                                    await APIHelper.DiscordAPI.SendMessageAsync(Settings.WebAuthModule.AuthReportChannel,
                                        $"{group.Value.DefaultMention} {LM.Get("authUnableToProcessUserGroup", characterName, characterId, groupName)}");
                            }

                            //auth
                            var code = await SQLHelper.PendingUsersGetCode(characterId);
                            var roles = new Dictionary<int, List<string>>
                            {
                                {rChar.corporation_id, group.Value.MemberRoles}
                            };

                            var corp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar.corporation_id, true);

                            if (await AuthGrantRoles(0, characterId.ToString(), roles, rChar, corp, code, discordId, true, groupName))
                                await SQLHelper.UserTokensSetAuthState(characterId, 2);

                        }
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx($"Auth check for {characterName}", ex, Category);
                    }

                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
        }

        public static async Task<string[]> GetCharacterIdFromCode(string code, string clientID, string secret)
        {
            var result = await APIHelper.ESIAPI.GetAuthToken(code, clientID, secret);
            var accessToken = result[0];

            if (accessToken == null) return null;

            using (var authWebHttpClient = new HttpClient())
            {
                authWebHttpClient.DefaultRequestHeaders.Clear();
                authWebHttpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
                using (var tokenresponse = await authWebHttpClient.GetAsync("https://login.eveonline.com/oauth/verify"))
                {
                    var verifyString = await tokenresponse.Content.ReadAsStringAsync();
                    if (JObject.Parse(verifyString)["error"]?.ToString() == "invalid_token")
                        return null;
                    authWebHttpClient.DefaultRequestHeaders.Clear();
                    return new[] {(string) JObject.Parse(verifyString)["CharacterID"], result[1]};
                }
            }

        }

        public async Task<bool> Auth(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleAuthWeb) return false;

            var esiFailure = false;
            var request = context.Request;
            var response = context.Response;

            var extIp = Settings.WebServerModule.WebExternalIP;
            var extPort = Settings.WebServerModule.WebExternalPort;
            var port = Settings.WebServerModule.WebListenPort;
            var callbackurl =  $"http://{extIp}:{extPort}/callback.php";


            if (request.HttpMethod != HttpMethod.Get.ToString())
                return false;
            try
            {
                if (request.Url.LocalPath == "/auth.php" || request.Url.LocalPath == $"{extPort}/auth.php"|| request.Url.LocalPath == $"{port}/auth.php")
                {
                    var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{callbackurl}", callbackurl).Replace("{client_id}", Settings.WebServerModule.CcpAppClientId)
                        .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{body}", LM.Get("authTemplateInv")).Replace("{backText}", LM.Get("backText"));
                    await WebServerModule.WriteResponce(text, response);
                    return true;
                }

                if ((request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                    && (!request.Url.Query.Contains("&state=") || request.Url.Query.Contains("&state=x")))
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
                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        var code = prms[0].Split('=')[1];
                       // var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                        var x = prms.Last().Split('=');
                        var inputGroupName = x.Length > 1 ? x[1].Substring(1, x[1].Length - 1) : null;
                        var inputGroup = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key.Equals(inputGroupName, StringComparison.OrdinalIgnoreCase)).Value;

                        var result = await GetCharacterIdFromCode(code, Settings.WebServerModule.CcpAppClientId, Settings.WebServerModule.CcpAppSecret);
                        if (result == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var characterID = result?[0];

                        var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterID, true);
                        if (rChar == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var corpID = rChar?.corporation_id ?? 0;
                        var rCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, rChar?.corporation_id, true);
                        if (rCorp == null)
                        {
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", LM.Get("ESIFailure"))
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")), response);
                            return true;
                        }

                        var allianceID = rCorp?.alliance_id ?? 0;

                        var cFoundList = new List<int>();
                        var groupName = string.Empty;
                        WebAuthGroup group = null;
                        var groupPermissions = new List<string>();

                        //PreliminaryAuthMode
                        if (inputGroup != null && inputGroup.PreliminaryAuthMode)
                        {
                            group = inputGroup;
                            if (string.IsNullOrEmpty(result[1]))
                            {
                                await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("Auth Module", LM.Get("authNoTokenReceived")), response);
                                await LogHelper.LogWarning($"Invalid named group auth attempt (missing token) from charID: {characterID} grp: {inputGroupName}", Category);
                                return true;
                            }
                            cFoundList.Add(corpID); //fake reg ;)
                            groupName = inputGroupName;
                            groupPermissions = new List<string>(group.ESICustomAuthRoles);
                            add = true;
                        }
                        else //normal auth
                        {
                            //has custom ESI roles
                            if (inputGroup != null)
                            {
                                group = inputGroup;
                                if (group.CorpIDList.Contains(rChar.corporation_id) || (allianceID != 0 && group.AllianceIDList.Contains(allianceID)))
                                {
                                    groupName = inputGroupName;
                                    groupPermissions = new List<string>(group.ESICustomAuthRoles);

                                    add = true;
                                    cFoundList.Add(rChar.corporation_id);
                                }
                            }
                            else
                            {
                                //general auth
                                foreach (var grp in Settings.WebAuthModule.AuthGroups.Where(a=> !a.Value.ESICustomAuthRoles.Any() && !a.Value.PreliminaryAuthMode))
                                {
                                    if (grp.Value.CorpIDList.Contains(rChar.corporation_id) || (allianceID != 0 && grp.Value.AllianceIDList.Contains(allianceID)))
                                    {
                                        cFoundList.Add(rChar.corporation_id);
                                        groupName = grp.Key;
                                        group = grp.Value;
                                        add = true;
                                        break;
                                    }
                                }
                            }
                        }

                        if (add)
                        {
                            if (!string.IsNullOrEmpty(result[1]))
                            {
                                await SQLHelper.SQLiteDataInsertOrUpdate("userTokens", new Dictionary<string, object>
                                {
                                    {"characterName", rChar.name},
                                    {"characterID", Convert.ToInt32(characterID)},
                                    {"discordUserId", 0},
                                    {"refreshToken", result[1]},
                                    {"groupName", groupName},
                                    {"permissions", string.Join(',', groupPermissions)},
                                    {"authState", inputGroup != null && inputGroup.PreliminaryAuthMode ? 0 : 1}
                                });
                            }

                            var uid = GetUniqID();
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

                            if (!group.PreliminaryAuthMode)
                            {
                                await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2).Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                        .Replace("{image}", image)
                                        .Replace("{uid}",  $"!auth {uid}").Replace("{header}", LM.Get("authTemplateHeader"))
                                        .Replace("{body}", LM.Get("authTemplateSucc1", rChar.name))
                                        .Replace("{body2}", LM.Get("authTemplateSucc2")).Replace("{body3}", LM.Get("authTemplateSucc3")).Replace("{backText}", LM.Get("backText")),
                                    response);
                                if (SettingsManager.Settings.WebAuthModule.AuthReportChannel != 0)
                                    await APIHelper.DiscordAPI.SendMessageAsync(SettingsManager.Settings.WebAuthModule.AuthReportChannel, $"{group.DefaultMention} {LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName)}").ConfigureAwait(false);
                                await LogHelper.LogWarning(LM.Get("authManualAcceptMessage", rChar.name, characterID, groupName), LogCat.AuthWeb);
                            }
                            else
                            {
                                await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth2)
                                        .Replace("{url}", Settings.WebServerModule.DiscordUrl)
                                        .Replace("{image}", image)
                                        .Replace("{uid}", $"!auth confirm {uid}")
                                        .Replace("{header}", LM.Get("authTemplateHeader"))
                                        .Replace("{body}", LM.Get("authTemplateManualAccept", rChar.name))
                                        .Replace("{body3}", LM.Get("authTemplateManualAccept3"))
                                        .Replace("{body2}", LM.Get("authTemplateManualAccept2"))
                                        .Replace("{backText}", LM.Get("backText")),
                                    response);

                            }
                        }
                        else
                        {
                            var message = LM.Get("authNonAlly");
                            await WebServerModule.WriteResponce(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", message)
                                .Replace("{header}", LM.Get("authTemplateHeader")).Replace("{backText}", LM.Get("backText")).Replace("{body}", ""), response);
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

        public static string GetUniqID()
        {
            //ensure we have some letters
            string result;
            do
            {
                var ts = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0));
                double t = ts.TotalMilliseconds / 1000;

                int a = (int)Math.Floor(t);
                int b = (int)((t - Math.Floor(t)) * 1000000);

                result = a.ToString("x8") + b.ToString("x5");
            } while (result.All(char.IsDigit));

            return result;
        }


        internal static async Task AuthUser(ICommandContext context, string remainder, ulong discordId)
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
                        var groupName = string.Empty;
                        foreach (var group in TickManager.GetModule<WebAuthModule>().Settings.WebAuthModule.AuthGroups)
                        {
                            if (group.Value.CorpIDList.Count > 0)
                            {
                                foreach (var c in group.Value.CorpIDList.Where(a => !foundList.ContainsKey(a)))
                                    foundList.Add(c, group.Value.MemberRoles);
                               // groupName = group.Key;
                            }

                            if (group.Value.AllianceIDList.Count > 0)
                            {
                                foreach (var c in group.Value.AllianceIDList.Where(a => !foundList.ContainsKey(a)))
                                    foundList.Add(c, group.Value.MemberRoles);                             
                               // groupName = group.Key;
                            }
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

                        if (!enable)
                        {
                            var grp = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Value.CorpIDList.Count == 0 && a.Value.AllianceIDList.Count == 0);
                            if (grp.Value != null)
                            {
                                enable = true;
                                groupName = grp.Key;
                            }
                        }

                        if (enable && !esiFailed)
                        {
                            var ch = context?.Channel?.Id ?? SettingsManager.Settings.WebAuthModule.AuthReportChannel;
                            await AuthGrantRoles(ch, characterID, foundList, characterData, corporationData, remainder, discordId == 0 ? context.Message.Author.Id : discordId );

                            var chId = Convert.ToInt32(characterID);
                            if (await SQLHelper.IsEntryExists("userTokens", new Dictionary<string, object> {{"characterID", chId}}))
                            {
                                var discordUser = APIHelper.DiscordAPI.GetUser(discordId == 0 ? context.Message.Author.Id : discordId);
                               // await SQLHelper.SQLiteDataUpdate("userTokens", "groupName", groupName, "characterID", chId);
                                await SQLHelper.SQLiteDataUpdate("userTokens", "discordUserId", discordUser.Id, "characterID", chId);
                                await SQLHelper.SQLiteDataUpdate("userTokens", "authState", 2, "characterID", chId);
                            }
                        }
                        else
                        {
                            await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, LM.Get("ESIFailure")).ConfigureAwait(false);
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

        private static async Task<bool> AuthGrantRoles(ulong channelId, string characterID, Dictionary<int, List<string>> foundList, JsonClasses.CharacterData characterData, 
            JsonClasses.CorporationData corporationData, string remainder, ulong discordId, bool isPreliminary = false, string authGroupName = null)
        {
            var rolesToAdd = new List<SocketRole>();

            var allianceID = characterData.alliance_id ?? 0;
            var corpID = characterData.corporation_id;

            var authSettings = TickManager.GetModule<WebAuthModule>()?.Settings.WebAuthModule;
            var missedRoles = new List<string>();
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
                        else missedRoles.Add(a);
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
                        else missedRoles.Add(a);
                    });
                }

                var discordUser = APIHelper.DiscordAPI.GetUser(discordId);

                if (authSettings.AuthReportChannel != 0)
                {
                    var mention = SettingsManager.Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == authGroupName).Value?.DefaultMention;
                    if (isPreliminary)
                        await APIHelper.DiscordAPI.SendMessageAsync(authSettings.AuthReportChannel, $"{mention} {LM.Get("grantRolesPrelMessage", characterData.name, authGroupName)}")
                            .ConfigureAwait(false);
                    else
                        await APIHelper.DiscordAPI.SendMessageAsync(authSettings.AuthReportChannel, $"{mention} {LM.Get("grantRolesMessage", characterData.name)}")
                            .ConfigureAwait(false);
                }

                await LogHelper.LogInfo($"Granting roles to {characterData.name} {(isPreliminary ? $"[AUTO-AUTH from {authGroupName}]" : "[GENERAL]")}", LogCat.AuthCheck);

                await APIHelper.DiscordAPI.AssignRolesToUser(discordUser, rolesToAdd);

                if (missedRoles.Any())
                    await LogHelper.LogWarning($"Missing discord roles: {string.Join(',', missedRoles)}");

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

                if(channelId != 0)
                    await APIHelper.DiscordAPI.SendMessageAsync(channelId, LM.Get("msgAuthSuccess", discordUser.Mention, characterData.name));
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
                await LogHelper.LogEx($"Failed adding Roles to User {characterData.name}, Reason: {ex.Message}", ex, LogCat.AuthCheck);
                return false;
            }

            return true;
        }
    }
}
