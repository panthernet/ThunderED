using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
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

        public async Task ProcessPreliminaryApplicant(string remainder)
        {
            var pu = await SQLHelper.GetPendingUser(remainder);
            if(pu == null) return;
            var data = (await SQLHelper.UserTokensGetAllEntries(new Dictionary<string, object> {{"characterID", pu.CharacterId}})).FirstOrDefault();
            if (data == null) return;
            await ProcessPreliminaryApplicant(data.CharacterId, data.CharacterName, data.DiscordUserId, data.GroupName);
        }


        public async Task ProcessPreliminaryApplicant(long characterId, string characterName, ulong discordId, string groupName)
        {
            try
            {
                var group = Settings.WebAuthModule.AuthGroups.FirstOrDefault(a => a.Key == groupName);
                if (group.Value == null)
                {
                    await LogHelper.LogWarning($"Group {groupName} not found for character {characterName} awaiting auth...");
                    return;
                }

                var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                if (rChar == null) return;

                var textCorpId = rChar.corporation_id.ToString();
                var textAllyId = rChar.alliance_id?.ToString();
                if (group.Value.AllowedCorporations.ContainsKey(textCorpId) || (rChar.alliance_id > 0 && group.Value.AllowedAlliances.ContainsKey(textAllyId)))
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
                    await AuthUser(null, code, discordId);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Auth check for {characterName}", ex, Category);
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
                    var characterId = Convert.ToInt64(data[0]);
                    var characterName = data[1].ToString();
                    var discordId = Convert.ToUInt64(data[2]);
                    var groupName = data[3].ToString();
                    await ProcessPreliminaryApplicant(characterId, characterName, discordId, groupName);
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
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    
                    if (prms.Length == 0 || prms[0].Split('=').Length == 0 || string.IsNullOrEmpty(prms[0]))
                    {
                        await WebServerModule.WriteResponce(await WebServerModule.Get404Page(), response);
                        return true;
                    }

                    var groupName = HttpUtility.UrlDecode(prms[0].Split('=')[1]);//string.IsNullOrEmpty(Settings.WebAuthModule.DefaultAuthGroup) || !Settings.WebAuthModule.AuthGroups.ContainsKey(Settings.WebAuthModule.DefaultAuthGroup) ? Settings.WebAuthModule.AuthGroups.Keys.FirstOrDefault() : Settings.WebAuthModule.DefaultAuthGroup;
                    if (!Settings.WebAuthModule.AuthGroups.ContainsKey(groupName))
                    {
                        await WebServerModule.WriteResponce(await WebServerModule.Get404Page(), response);
                        return true;
                    }

                    var grp = Settings.WebAuthModule.AuthGroups[groupName];
                    var url = grp.ESICustomAuthRoles.Any() ? WebServerModule.GetCustomAuthUrl(grp.ESICustomAuthRoles, groupName) : WebServerModule.GetAuthUrl();

                    var text = File.ReadAllText(SettingsManager.FileTemplateAuth).Replace("{authUrl}", url)
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

                        var cFoundList = new List<long>();
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
                            var textCorpId = rChar.corporation_id.ToString();
                            var textAllyId = allianceID.ToString();

                            //has custom ESI roles
                            if (inputGroup != null)
                            {
                                group = inputGroup;
                                if (group.AllowedCorporations.ContainsKey(textCorpId) || (allianceID != 0 && group.AllowedAlliances.ContainsKey(textAllyId)))
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
                                    if (grp.Value.AllowedCorporations.ContainsKey(textCorpId) || (allianceID != 0 && grp.Value.AllowedAlliances.ContainsKey(textAllyId)))
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
                                    {"characterID", Convert.ToInt64(characterID)},
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
            JsonClasses.CharacterData characterData = null;
            try
            {
                discordId = discordId > 0 ? discordId : context.Message.Author.Id;

                //check pending user validity
                var pendingUser = await SQLHelper.GetPendingUser(remainder);
                if (pendingUser == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel, LM.Get("authHasInvalidKey"), true).ConfigureAwait(false);
                    return;
                }
                if(!pendingUser.Active)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, context.Channel,LM.Get("authHasInactiveKey"), true).ConfigureAwait(false);
                    return;
                }
                
                var characterID = pendingUser.CharacterId.ToString();

                //check if we fit some group
                var result = await APIHelper.DiscordAPI.GetRoleGroup(Convert.ToInt64(characterID), discordId);
                var groupName = result?.GroupName;
                //pass auth
                if (!string.IsNullOrEmpty(groupName))
                {
                    var group = SettingsManager.Settings.WebAuthModule.AuthGroups[groupName];
                    var channel = context?.Channel?.Id ?? SettingsManager.Settings.WebAuthModule.AuthReportChannel;
                    characterData = await APIHelper.ESIAPI.GetCharacterData("Auth", characterID);
                    
                    //report to discord
                    var reportChannel = SettingsManager.Settings.WebAuthModule.AuthReportChannel;
                    if (reportChannel != 0)
                    {
                        var mention = group.DefaultMention;
                        if (group.PreliminaryAuthMode)
                            await APIHelper.DiscordAPI.SendMessageAsync(reportChannel, $"{mention} {LM.Get("grantRolesPrelMessage", characterData.name, groupName)}")
                                .ConfigureAwait(false);
                        else
                            await APIHelper.DiscordAPI.SendMessageAsync(reportChannel, $"{mention} {LM.Get("grantRolesMessage", characterData.name)}")
                                .ConfigureAwait(false);
                    }
                    await LogHelper.LogInfo($"Granting roles to {characterData.name} {(group.PreliminaryAuthMode ? $"[AUTO-AUTH from {groupName}]" : "[GENERAL]")}", LogCat.AuthCheck);

                    //disable pending user
                    await SQLHelper.InvalidatePendingUser(remainder);

                    //remove all prevoius users associated with discordID
                    await SQLHelper.DeleteAuthUsers(discordId.ToString());
                    //insert new authUsers
                    await SQLHelper.SQLiteDataInsertOrUpdate("authUsers", new Dictionary<string, object>
                    {
                        {"eveName", characterData.name},
                        {"characterID", characterID},
                        {"discordID", discordId.ToString()},
                        {"role", groupName},
                        {"active", "yes"},
                        {"addedOn", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}
                    });

                    if (group.PreliminaryAuthMode)
                    {
                        await SQLHelper.UserTokensSetAuthState(characterID, 2);
                    }

                    //save tokens
                    var chId = Convert.ToInt64(characterID);
                    if (await SQLHelper.IsEntryExists("userTokens", new Dictionary<string, object> {{"characterID", chId}}))
                    {
                        await SQLHelper.SQLiteDataUpdate("userTokens", "discordUserId", discordId, "characterID", chId);
                        await SQLHelper.SQLiteDataUpdate("userTokens", "authState", 2, "characterID", chId);
                    }

                    //run roles assignment
                    await APIHelper.DiscordAPI.UpdateUserRoles(discordId, SettingsManager.Settings.WebAuthModule.ExemptDiscordRoles,
                        SettingsManager.Settings.WebAuthModule.AuthCheckIgnoreRoles);

                    //notify about success
                    if(channel != 0)
                        await APIHelper.DiscordAPI.SendMessageAsync(channel, LM.Get("msgAuthSuccess", APIHelper.DiscordAPI.GetUserMention(discordId), characterData.name));
                }
                else
                {
                    await APIHelper.DiscordAPI.SendMessageAsync(context.Channel, LM.Get("ESIFailure")).ConfigureAwait(false);
                    await LogHelper.LogError("ESI Failure", LogCat.AuthWeb);
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx($"Failed adding Roles to User {characterData?.name}, Reason: {ex.Message}", ex, LogCat.AuthCheck);
            }
        }
    }
}
