using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Matrix.Xmpp.MessageCarbons;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public class WebSettingsModule: AppModuleBase
    {
        public sealed override LogCat Category => LogCat.WebSettings;

        public WebSettingsModule()
        {
            LogHelper.LogModule("Initializing Web Settings Editor module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, OnAuthRequest);

        }

        public override async Task Initialize()
        {
            var data = Settings.WebConfigEditorModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.AllowedEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Member);

            foreach (var id in GetAllParsedCharacters())
                await APIHelper.ESIAPI.RemoveAllCharacterDataFromCache(id);

            await APIHelper.DiscordAPI.CheckAndNotifyBadDiscordRoles(Settings.WebConfigEditorModule.GetEnabledGroups().Values.SelectMany(a => a.AllowedDiscordRoles).Distinct().ToList(), Category);

        }

        private async Task<bool> OnAuthRequest(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleWebConfigEditor) return false;

            var request = context.Request;
            var response = context.Response;
            if (request.HttpMethod != HttpMethod.Get.ToString())
                return false;

            var extIp = Settings.WebServerModule.WebExternalIP;
            var extPort = Settings.WebServerModule.WebExternalPort;

            try
            {
                RunningRequestCount++;
                if (request.Url.LocalPath == "/callback" || request.Url.LocalPath == $"{extPort}/callback")
                {
                    var clientID = Settings.WebServerModule.CcpAppClientId;
                    var secret = Settings.WebServerModule.CcpAppSecret;
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    if (prms.Length == 0 || prms[0].Split('=').Length == 0 || string.IsNullOrEmpty(prms[0]))
                        return false;
                    var code = prms[0].Split('=')[1];
                    var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;
                    if (state != "settings") return false;
                    //have code
                    var result = await WebAuthModule.GetCharacterIdFromCode(code, clientID, secret);
                    var characterId = result == null ? 0 : Convert.ToInt64(result[0]);

                    if (characterId == 0)
                    {
                        await WebServerModule.WriteResponce(
                            WebServerModule.GetAccessDeniedPage("Web Config Editor", LM.Get("accessDenied"),
                                WebServerModule.GetWebSiteUrl()), response);
                        return true;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                    if (rChar == null)
                    {
                        await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return true;
                    }

                    if (await CheckAccess(characterId, rChar) == null)
                    {
                        await WebServerModule.WriteResponce(
                            WebServerModule.GetAccessDeniedPage("Web Config Editor", LM.Get("accessDenied"),
                                WebServerModule.GetWebSiteUrl()), response);
                        return true;
                    }

                    var we = new WebEditorAuthEntry
                    {
                        Id = characterId, Code = Guid.NewGuid().ToString("N"), Time = DateTime.Now
                    };
                    await SQLHelper.SaveWebEditorAuthEntry(we);
                    var url = WebServerModule.GetWebEditorUrl(we.Code);
                    await response.RedirectAsync(new Uri(url));
                    return true;
                }

                if (request.Url.LocalPath == "/settings" || request.Url.LocalPath == $"{extPort}/settings")
                {
                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    if (prms.Length == 0 || prms[0].Split('=').Length == 0 || string.IsNullOrEmpty(prms[0]))
                        return false;
                    var code = prms.FirstOrDefault(a => a.StartsWith("code"))?.Split('=')[1];
                    var state = prms.FirstOrDefault(a => a.StartsWith("state"))?.Split('=')[1];

                    if (state != "settings" && state != "settings_sa" && state != "settings_ti") return false;
                    if (code == null)
                    {
                        await WebServerModule.WriteResponce(
                            WebServerModule.GetAccessDeniedPage("Web Config Editor", LM.Get("accessDenied"),
                                WebServerModule.GetWebSiteUrl()), response);
                        return true;
                    }

                    var we = await SQLHelper.GetWebEditorAuthEntry(code);
                    if (we == null)
                    {
                        await WebServerModule.WriteResponce(
                            WebServerModule.GetAccessDeniedPage("Web Config Editor", LM.Get("accessDenied"),
                                WebServerModule.GetWebSiteUrl()), response);
                        return true;
                    }

                    var rChar = await APIHelper.ESIAPI.GetCharacterData(Reason, we.Id, true);
                    if (rChar == null)
                    {
                        await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return true;
                    }

                    var filter = await CheckAccess(we.Id, rChar);
                    if (filter == null)
                    {
                        await WebServerModule.WriteResponce(
                            WebServerModule.GetAccessDeniedPage("Web Config Editor", LM.Get("accessDenied"),
                                WebServerModule.GetWebSiteUrl()), response);
                        return true;
                    }

                    if (state == "settings_sa")
                    {
                        if (!filter.CanEditSimplifiedAuth)
                        {
                            await WebServerModule.WriteResponce("Nope", response);
                            return true;
                        }

                        var data = prms.FirstOrDefault(a => a.StartsWith("data"))?.Split('=')[1];
                        data = HttpUtility.UrlDecode(data);
                        if (data != null)
                        {
                            //load clean settings from file
                            await SettingsManager.UpdateSettings();

                            var convData = JsonConvert.DeserializeObject<List<SaData>>(data);
                            convData = convData.Where(a =>
                                !string.IsNullOrEmpty(a.Name?.Trim()) && !string.IsNullOrEmpty(a.Group?.Trim()) &&
                                !string.IsNullOrEmpty(a.Roles?.Trim())).ToList();
                            await SettingsManager.SaveSimplifiedAuthData(convData
                                .Select(a => $"{a.Name}|{a.Group}|{a.Roles}").ToList());
                            //inject updated simplified auth data
                            await SettingsManager.LoadSimplifiedAuth();
                            //rebuild auth cache
                            if (Settings.Config.ModuleAuthWeb)
                                await TickManager.GetModule<WebAuthModule>().Initialize();
                            await LogHelper.LogInfo("Simplified auth update completed!", Category);
                        }
                        else
                        {
                            var simplifiedAuthEntities = await SettingsManager.GetSimplifiedAuthData();
                            var x = new
                            {
                                total = simplifiedAuthEntities.Count,
                                totalNotFiltered = simplifiedAuthEntities.Count,
                                rows = simplifiedAuthEntities
                            };
                            var json = JsonConvert.SerializeObject(x);
                            await WebServerModule.WriteJsonResponse(json, response);
                        }

                        return true;
                    }

                    if (state == "settings_ti")
                    {
                        if (!filter.CanEditTimers)
                        {
                            await WebServerModule.WriteResponce("Nope", response);
                            return true;
                        }

                        var data = prms.FirstOrDefault(a => a.StartsWith("data"))?.Split('=')[1];
                        data = HttpUtility.UrlDecode(data);
                        if (data != null)
                        {

                            var convData = JsonConvert.DeserializeObject<List<TiData>>(data);
                            convData = convData.Where(a =>
                                !string.IsNullOrEmpty(a.Name?.Trim()) && !string.IsNullOrEmpty(a.Entities?.Trim()) &&
                                !string.IsNullOrEmpty(a.Roles?.Trim())).ToList();
                            await SaveTimersAuthData(convData);
                        }
                        else
                        {
                            var ent = SettingsManager.Settings.TimersModule.AccessList.Select(a => new TiData
                            {
                                Name = a.Key,
                                Entities = string.Join(",", a.Value.FilterEntities.Select(b => b.ToString())),
                                Roles = string.Join(",", a.Value.FilterDiscordRoles)
                            }).ToList();
                            var x = new {total = ent.Count, totalNotFiltered = ent.Count, rows = ent};
                            var json = JsonConvert.SerializeObject(x);
                            await WebServerModule.WriteJsonResponse(json, response);
                        }

                        return true;
                    }

                    //check in db
                    var timeout = Settings.WebConfigEditorModule.SessionTimeoutInMinutes;
                    if (timeout != 0)
                    {
                        if ((DateTime.Now - we.Time).TotalMinutes > timeout)
                        {
                            await SQLHelper.DeleteWebEditorEntry(we.Id);
                            //redirect to auth
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebConfigAuthURL()));
                            return true;
                        }

                        //update session overwise
                        we.Time = DateTime.Now;
                        await SQLHelper.SaveWebEditorAuthEntry(we);
                    }

                    //var groups = string.Join(", ", Settings.WebAuthModule.AuthGroups.Keys.Distinct());

                    var sb = new StringBuilder();
                    foreach (var groupName in Settings.WebAuthModule.AuthGroups.Keys.Distinct())
                        sb.Append($"{{value: '{groupName}', text: '{groupName}'}},");
                    sb.Remove(sb.Length - 1, 1);
                    var groupList = $"[{sb}]";

                    var timersContent = string.Empty;
                    var timersScripts = string.Empty;
                    var simpleAuthContent = string.Empty;
                    var simpleAuthScripts = string.Empty;
                    if (filter.CanEditSimplifiedAuth)
                    {
                        simpleAuthContent = File.ReadAllText(SettingsManager.FileTemplateSettingsPage_SimpleAuth)
                                .Replace("{saAddEntry}", LM.Get("webSettingsAddEntryButton"))
                                .Replace("{saDeleteEntry}", LM.Get("webSettingsDeleteEntryButton"))
                                .Replace("{saSave}", LM.Get("webSettingsSaveEntryButton"))
                                .Replace("{saTableColumnName}", LM.Get("webSettingsSaColumnName"))
                                .Replace("{saTableColumnGroup}", LM.Get("webSettingsSaColumnGroup"))
                                .Replace("{saTableColumnRoles}", LM.Get("webSettingsSaColumnRoles"))

                                .Replace("{serverAddress}",
                                    $"{Settings.WebServerModule.WebExternalIP}:{Settings.WebServerModule.WebExternalPort}")
                                .Replace("{code}", code)
                                .Replace("{locale}", SettingsManager.Settings.Config.Language)
                            ;

                        simpleAuthScripts = File
                                .ReadAllText(SettingsManager.FileTemplateSettingsPage_SimpleAuth_Scripts)
                                .Replace("{saGroupList}", groupList)
                                .Replace("{postSimplifiedAuthUrl}", WebServerModule.GetWebEditorSimplifiedAuthUrl(code))
                            ;
                    }

                    if (filter.CanEditTimers)
                    {
                        timersContent = File.ReadAllText(SettingsManager.FileTemplateSettingsPage_Timers)
                                .Replace("{saAddEntry}", LM.Get("webSettingsAddEntryButton"))

                                .Replace("{saAddEntry}", LM.Get("webSettingsAddEntryButton"))
                                .Replace("{saDeleteEntry}", LM.Get("webSettingsDeleteEntryButton"))
                                .Replace("{saSave}", LM.Get("webSettingsSaveEntryButton"))
                                .Replace("{saTableColumnName}", LM.Get("webSettingsSaColumnName"))
                                .Replace("{tiTableColumnEntities}", LM.Get("webSettingsTiColumnEntities"))
                                .Replace("{saTableColumnRoles}", LM.Get("webSettingsSaColumnRoles"))

                                .Replace("{serverAddress}",
                                    $"{Settings.WebServerModule.WebExternalIP}:{Settings.WebServerModule.WebExternalPort}")
                                .Replace("{code}", code)
                                .Replace("{locale}", SettingsManager.Settings.Config.Language)
                            ;

                        timersScripts = File.ReadAllText(SettingsManager.FileTemplateSettingsPage_Timers_Scripts)
                                .Replace("{postTimersUrl}", WebServerModule.GetWebEditorTimersUrl(code))
                            ;
                    }

                    var saActive = filter.CanEditSimplifiedAuth ? "active" : null;
                    var tiActive = saActive != null || !filter.CanEditTimers ? null : "active";

                    var text = File.ReadAllText(SettingsManager.FileTemplateSettingsPage)
                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceTables())
                            .Replace("{header}", LM.Get("webSettingsHeader"))
                            .Replace("{Back}", LM.Get("Back"))
                            .Replace("{LogOut}", LM.Get("LogOut"))
                            .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                            .Replace("{LogOutUrl}", WebServerModule.GetWebSiteUrl())
                            .Replace("{backUrl}", WebServerModule.GetWebSiteUrl())
                            .Replace("{simpleAuthContent}", simpleAuthContent)
                            .Replace("{simpleAuthScripts}", simpleAuthScripts)
                            .Replace("{timersContent}", timersContent)
                            .Replace("{timersScripts}", timersScripts)
                            .Replace("{code}", code)
                            .Replace("{locale}", SettingsManager.Settings.Config.Language)
                            .Replace("{simpleAuthTabName}", LM.Get("webSettingsSimpleAuthTabName"))
                            .Replace("{timersTabName}", LM.Get("webSettingsTimersTabName"))
                            .Replace("{saPageVisible}", filter.CanEditSimplifiedAuth ? null : "d-none")
                            .Replace("{tiPageVisible}", filter.CanEditTimers ? null : "d-none")
                            .Replace("{saPageActive}", saActive)
                            .Replace("{tiPageActive}", tiActive)
                        ;
                    await WebServerModule.WriteResponce(text, response);
                    return true;
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
        }

        private async Task SaveTimersAuthData(List<TiData> convData)
        {
            try
            {
                var groups = new Dictionary<string, TimersAccessGroup>();
                Settings.TimersModule.AccessList.Clear();
                foreach (var data in convData)
                {
                    var group = new TimersAccessGroup();
                    var lst = data.Entities.Split(",");
                    foreach (var item in lst)
                    {
                        if (long.TryParse(item, out var value))
                            group.FilterEntities.Add(value);
                        else group.FilterEntities.Add(item);
                    }

                    group.FilterDiscordRoles = data.Roles.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToList();
                    groups.Add(data.Name, group);
                }

                foreach (var @group in groups)
                    Settings.TimersModule.AccessList.Add(group.Key, group.Value);

                await Settings.SaveAsync();
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(nameof(SaveTimersAuthData), ex, Category);
            }
        }

        private class SaData
        {
#pragma warning disable 649
            public string Name;
#pragma warning restore 649
#pragma warning disable 649
            public string Group;
#pragma warning restore 649
#pragma warning disable 649
            public string Roles;
#pragma warning restore 649
        }

        private class TiData
        {
            public string Name;
            public string Entities;
            public string Roles;
        }

        private async Task<WCEAccessFilter> CheckAccess(long characterId, JsonClasses.CharacterData rChar)
        {
            var authgroups = Settings.WebConfigEditorModule.GetEnabledGroups();

            if (authgroups.Count == 0 || authgroups.Values.All(a => !a.AllowedEntities.Any() && !a.AllowedDiscordRoles.Any()))
            {
                return new WCEAccessFilter();
            }

            //check discord roles auth
            foreach (var filter in authgroups.Values)
            {
                if (filter.AllowedDiscordRoles.Any())
                {
                    var authUser = await SQLHelper.GetAuthUserByCharacterId(characterId);
                    if (authUser != null && authUser.DiscordId > 0)
                    {
                        if (APIHelper.DiscordAPI.GetUserRoleNames(authUser.DiscordId).Intersect(filter.AllowedDiscordRoles).Any())
                            return filter;
                    }
                }
            }

            //check for Discord admins
            var discordId = SQLHelper.GetAuthUserDiscordId(characterId).GetAwaiter().GetResult();
            if (discordId > 0)
            {
                var roles = string.Join(',', APIHelper.DiscordAPI.GetUserRoleNames(discordId));
                if (!string.IsNullOrEmpty(roles))
                {
                    var exemptRoles = Settings.Config.DiscordAdminRoles;
                    if(roles.Replace("&br;", "\"").Split(',').Any(role => exemptRoles.Contains(role)))
                        return new WCEAccessFilter();
                }
            }



            foreach (var accessList in Settings.WebConfigEditorModule.GetEnabledGroups())
            {
                var filterName = accessList.Key;
                var filter = accessList.Value;
                var accessChars = GetParsedCharacters(filterName) ?? new List<long>();
                var accessCorps = GetParsedCorporations(filterName) ?? new List<long>();
                var accessAlliance = GetParsedAlliances(filterName) ?? new List<long>();
                if (!accessCorps.Contains(rChar.corporation_id) && (!rChar.alliance_id.HasValue || !(rChar.alliance_id > 0) || (!accessAlliance.Contains(
                                                                        rChar.alliance_id
                                                                            .Value))))
                {
                    if (!accessChars.Contains(characterId))
                    {
                        continue;
                    }
                }

                return filter;
            }

            return null;
        }

        public static bool HasWebAccess(in long id)
        {
            if (!SettingsManager.Settings.Config.ModuleWebConfigEditor) return false;
            return TickManager.GetModule<WebSettingsModule>().GetAllParsedCharacters().Contains(id);
        }
    }
}
