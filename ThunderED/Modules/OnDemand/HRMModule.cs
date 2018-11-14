using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public partial class HRMModule: AppModuleBase
    {
        public override LogCat Category => LogCat.HRM;
            
        public HRMModule()
        {
            LogHelper.LogModule("Inititalizing HRM module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, OnRequestReceived);
        }

        private async Task<bool> OnRequestReceived(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleHRM) return false;

            var request = context.Request;
            var response = context.Response;

            try
            {
                var extPort = Settings.WebServerModule.WebExternalPort;
                var port = Settings.WebServerModule.WebListenPort;

                if (request.Url.LocalPath == "/callback.php" || request.Url.LocalPath == $"{extPort}/callback.php" || request.Url.LocalPath == $"{port}/callback.php")
                {
                    var clientID = Settings.WebServerModule.CcpAppClientId;
                    var secret = Settings.WebServerModule.CcpAppSecret;

                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    var code = prms[0].Split('=')[1];
                    var state = prms.Length > 1 ? prms[1].Split('=')[1] : null;

                    //not an HRM query
                    if (state != "matahari") return false;

                    //have code
                    var result = await WebAuthModule.GetCharacterIdFromCode(code, clientID, secret);
                    var characterId = result == null ? 0 : Convert.ToInt32(result[0]);


                    if (result == null || await CheckAccess(characterId) == false)
                    {
                        await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied")), response);
                        return true;
                    }

                    var authCode = WebAuthModule.GetUniqID();
                    await SQLHelper.SQLiteDataInsertOrUpdate("hrmAuth", new Dictionary<string, object> {{"id", result[0]}, {"time", DateTime.Now}, {"code", authCode}});
                    //redirect to timers
                    await response.RedirectAsync(new Uri(WebServerModule.GetHRMMainURL(authCode)));

                    return true;
                }

                if (request.Url.LocalPath.StartsWith("/hrm.php") || request.Url.LocalPath.StartsWith($"{extPort}/hrm.php") || request.Url.LocalPath.StartsWith($"{port}/hrm.php"))
                {
                    if (string.IsNullOrWhiteSpace(request.Url.Query))
                    {
                        //redirect to auth
                        await response.RedirectAsync(new Uri(WebServerModule.GetHRMAuthURL()));
                        return true;
                    }

                    var prms = request.Url.Query.TrimStart('?').Split('&');
                    if (prms.Length < 3)
                    {
                        await WebServerModule.WriteResponce(LM.Get("AccessDenied"), response);
                        return true;
                    }

                    var data = prms.FirstOrDefault(a=> a.StartsWith("data")).Split('=')[1];
                    var authCode = prms.FirstOrDefault(a=> a.StartsWith("id")).Split('=')[1];
                    var state = prms.FirstOrDefault(a=> a.StartsWith("state")).Split('=')[1];
                    var page = Convert.ToInt32(prms.FirstOrDefault(a=> a.StartsWith("page"))?.Split('=')[1] ?? "0");

                    if (state != "matahari")
                    {
                        if (IsNoRedirect(data))
                            await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                        else 
                            await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return true;
                    }

                    var chId = await SQLHelper.SQLiteDataQuery<string>("hrmAuth", "id", "code", authCode);

                    var rChar = string.IsNullOrEmpty(chId) ? null : await APIHelper.ESIAPI.GetCharacterData(Reason, chId, true);
                    if (rChar == null)
                    {
                        if (IsNoRedirect(data))
                            await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                        else await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                        return true;
                    }

                    var characterId = Convert.ToInt32(chId);

                        
                    //have charId - had to check it
                    //check in db
                    var timeout = Settings.HRMModule.AuthTimeoutInMinutes;
                    if (timeout != 0)
                    {
                        var result = await SQLHelper.SQLiteDataQuery<string>("hrmAuth", "time", "id", chId);
                        if (result == null || (DateTime.Now - DateTime.Parse(result)).TotalMinutes > timeout)
                        {
                            if (IsNoRedirect(data))
                                await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                            //redirect to auth
                            else await response.RedirectAsync(new Uri(WebServerModule.GetHRMAuthURL()));
                            return true;
                        }
                        //prolong session
                        if(data == "0" || data.StartsWith("inspect"))
                            await SQLHelper.SQLiteDataUpdate("hrmAuth", "time", DateTime.Now, "code", authCode);
                    }

                    if (await CheckAccess(characterId) == false)
                    {
                        if (IsNoRedirect(data))
                            await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                        else await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied")), response);
                        return true;
                    }

                    switch (data)
                    {
                        //main page
                        case "0":
                        {
                            var membersHtml = await GenerateMembersListHtml(authCode);
                            var awaitingHtml = await GenerateAwaitingListHtml(authCode);

                            var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Main).Replace("{header}", LM.Get("hrmTemplateHeader"))
                                    .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                                    .Replace("{charId}", characterId.ToString())
                                    .Replace("{LogOutUrl}", WebServerModule.GetWebSiteUrl())
                                    .Replace("{LogOut}", LM.Get("LogOut"))
                                    .Replace("{locale}", LM.Locale)
                                    .Replace("{membersContent}", membersHtml)
                                    .Replace("{awaitingContent}", awaitingHtml)
                                    .Replace("{membersHeader}", LM.Get("hrmMembersHeader"))
                                    .Replace("{awaitingHeader}", LM.Get("hrmAwaitingHeader"))
                                ;
                            await WebServerModule.WriteResponce(text, response);
                        }
                            break;
                        case var s when s.StartsWith("inspect"):
                        {
                            try
                            {
                                if (!int.TryParse(data.Replace("inspect", ""), out var inspectCharId))
                                {
                                    await response.RedirectAsync(new Uri(WebServerModule.GetHRMAuthURL()));
                                    return true;
                                }

                                var iChar = await APIHelper.ESIAPI.GetCharacterData(Reason, inspectCharId, true);
                                var iCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, iChar.corporation_id, true);
                                var iAlly = iCorp.alliance_id.HasValue ? await APIHelper.ESIAPI.GetAllianceData(Reason, iCorp.alliance_id.Value, true) : null;
                                var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharId);
                                var hasToken = userTokenEntity != null;

                                var corpHistoryHtml = await GenerateCorpHistory(inspectCharId);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Inspect).Replace("{header}", LM.Get("hrmInspectingHeader", iChar.name))
                                        .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                                        .Replace("{charId}", characterId.ToString())
                                        .Replace("{LogOutUrl}", WebServerModule.GetWebSiteUrl())
                                        .Replace("{LogOut}", LM.Get("LogOut"))
                                        .Replace("{locale}", LM.Locale)
                                        .Replace("{imgCorp}", $"https://image.eveonline.com/Corporation/{iChar.corporation_id}_64.png")
                                        .Replace("{imgAlly}", $"https://image.eveonline.com/Alliance/{iCorp.alliance_id ?? 0}_64.png")
                                        .Replace("{imgChar}", $"https://image.eveonline.com/Character/{inspectCharId}_256.jpg")
                                        .Replace("{zkillChar}", $"http://www.zkillboard.com/character/{inspectCharId}")
                                        .Replace("{zkillCorp}", $"http://www.zkillboard.com/corporation/{iChar.corporation_id}")
                                        .Replace("{zkillAlly}", $"http://www.zkillboard.com/alliance/{iCorp.alliance_id ?? 0}")
                                        .Replace("{charName}", iChar.name)
                                        .Replace("{charCorpName}", $"{iCorp.name} [{iCorp.ticker}]")
                                        .Replace("{charAllyName}", $"{iAlly?.name} [{iAlly?.ticker}]")
                                        .Replace("{charBirthday}", $"{iChar.birthday.ToString(Settings.Config.ShortTimeFormat)}")
                                        .Replace("{corpHistoryTable}", corpHistoryHtml)
                                        .Replace("{disableAlly}", iAlly == null ? "d-none" : null)
                                        .Replace("{Close}", LM.Get("Close"))
                                        .Replace("{secStatus}", iChar.security_status.HasValue ? Math.Round(iChar.security_status.Value, 1).ToString("0.0") : null)
                                        .Replace("{mailHeader}", LM.Get("mailHeader"))
                                        .Replace("{backUrl}", WebServerModule.GetHRMMainURL(authCode))
                                        .Replace("{Back}", LM.Get("Back"))
                                        
                                        .Replace("{hrmInspectChar}", LM.Get("hrmInspectChar"))
                                        .Replace("{hrmInspectCorp}", LM.Get("hrmInspectCorp"))
                                        .Replace("{hrmInspectAlly}", LM.Get("hrmInspectAlly"))
                                        .Replace("{hrmInspectSS}", LM.Get("hrmInspectSS"))
                                        .Replace("{hrmInspectBirth}", LM.Get("hrmInspectBirth"))

                                        
                                        .Replace("{tabLastYearStats}", LM.Get("hrmTabLastYearStats"))
                                        .Replace("{tabJournal}", LM.Get("hrmTabJournal"))
                                        .Replace("{tabTransfers}", LM.Get("hrmTabTransfers"))
                                        .Replace("{tabContracts}", LM.Get("hrmTabContracts"))
                                        .Replace("{tabMail}", LM.Get("hrmTabMail"))
                                        .Replace("{tabCorpHistory}", LM.Get("hrmTabCorpHistory"))
                                        .Replace("{tabContacts}", LM.Get("hrmTabContacts"))
                                        .Replace("{tabSkills}", LM.Get("hrmTabSkills"))

                                        .Replace("{mailListUrl}", WebServerModule.GetHRM_AjaxMailListURL(inspectCharId, authCode))
                                        .Replace("{transactListUrl}", WebServerModule.GetHRM_AjaxTransactListURL(inspectCharId, authCode))
                                        .Replace("{journalListUrl}", WebServerModule.GetHRM_AjaxJournalListURL(inspectCharId, authCode))
                                        .Replace("{lysListUrl}", WebServerModule.GetHRM_AjaxLysListURL(inspectCharId, authCode))
                                        .Replace("{contractsListUrl}", WebServerModule.GetHRM_AjaxContractsListURL(inspectCharId, authCode))
                                        .Replace("{contactsListUrl}", WebServerModule.GetHRM_AjaxContactsListURL(inspectCharId, authCode))
                                        .Replace("{skillsListUrl}", WebServerModule.GetHRM_AjaxSkillsListURL(inspectCharId, authCode))

                                        .Replace("{disableMail}", SettingsManager.HasReadMailScope(userTokenEntity.PermissionsList) ? null : "d-none")
                                        .Replace("{allowMailBool}", SettingsManager.HasReadMailScope(userTokenEntity.PermissionsList) ? "true" : "false")
                                        .Replace("{disableWallet}", SettingsManager.HasCharWalletScope(userTokenEntity.PermissionsList) ? null : "d-none")
                                        .Replace("{allowWalletBool}", SettingsManager.HasCharWalletScope(userTokenEntity.PermissionsList) ? "true" : "false")
                                        .Replace("{disableCharStats}", SettingsManager.HasCharWalletScope(userTokenEntity.PermissionsList) ? null : "d-none")
                                        .Replace("{allowCharStatsBool}", SettingsManager.HasCharWalletScope(userTokenEntity.PermissionsList) ? "true" : "false")
                                        .Replace("{disableContracts}", SettingsManager.HasCharWalletScope(userTokenEntity.PermissionsList) ? null : "d-none")
                                        .Replace("{allowContractsBool}", SettingsManager.HasCharWalletScope(userTokenEntity.PermissionsList) ? "true" : "false")
                                        .Replace("{disableContacts}", SettingsManager.HasCharContactsScope(userTokenEntity.PermissionsList) ? null : "d-none")
                                        .Replace("{allowContactsBool}", SettingsManager.HasCharContactsScope(userTokenEntity.PermissionsList) ? "true" : "false")
                                        .Replace("{disableSP}", SettingsManager.HasCharSkillsScope(userTokenEntity.PermissionsList) ? null : "d-none")
                                        .Replace("{disableSkills}", SettingsManager.HasCharSkillsScope(userTokenEntity.PermissionsList) ? null : "d-none")
                                        .Replace("{allowSkillsBool}", SettingsManager.HasCharSkillsScope(userTokenEntity.PermissionsList) ? "true" : "false")
                                    ;

                                //private info
                                if (hasToken)
                                {
                                    var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                        Settings.WebServerModule.CcpAppSecret);
                                    if (SettingsManager.HasReadMailScope(userTokenEntity.PermissionsList))
                                    {
                                        var total = await GetMailPagesCount(token, inspectCharId);
                                        text = text.Replace("totalMailPages!!", total.ToString());
                                    }

                                    if (SettingsManager.HasCharWalletScope(userTokenEntity.PermissionsList))
                                    {
                                        var total = await GetCharTransactPagesCount(token, inspectCharId);
                                        text = text.Replace("totalTransactPages!!", total.ToString());
                                        total = await GetCharJournalPagesCount(token, inspectCharId);
                                        text = text.Replace("totalJournalPages!!", total.ToString());
                                    }

                                    if (SettingsManager.HasCharStatsScope(userTokenEntity.PermissionsList))
                                    {
                                        var total = GetCharJournalPagesCount();
                                        text = text.Replace("totalLysPages!!", total.ToString());
                                    }
                                    
                                    if (SettingsManager.HasCharContractsScope(userTokenEntity.PermissionsList))
                                    {
                                        var total = await GetCharContractsPagesCount(token, inspectCharId);
                                        text = text.Replace("totalcontractsPages!!", total.ToString());
                                    }
                                    if (SettingsManager.HasCharContactsScope(userTokenEntity.PermissionsList))
                                    {
                                        var total = await GetCharContactsPagesCount(token, inspectCharId);
                                        text = text.Replace("totalcontactsPages!!", total.ToString());
                                    }

                                    if (SettingsManager.HasCharSkillsScope(userTokenEntity.PermissionsList))
                                    {
                                        var skills = await APIHelper.ESIAPI.GetCharSkills(Reason, inspectCharId, token);
                                        text = text.Replace("{totalSP}", skills.total_sp.ToString("N0"));
                                        var total = await GetCharSkillsPagesCount(token, inspectCharId);
                                        text = text.Replace("totalskillsPages!!", total.ToString());
                                    }
                                }

                                text = text.Replace("totalMailPages!!", "0");
                                text = text.Replace("totalTransactPages!!", "0");
                                text = text.Replace("totalJournalPages!!", "0");
                                text = text.Replace("totalLysPages!!", "0");
                                text = text.Replace("totalcontractsPages!!", "0");
                                text = text.Replace("totalcontactsPages!!", "0");
                                text = text.Replace("totalskillsPages!!", "0");
                                text = text.Replace("{totalSP}", "0");


                                await WebServerModule.WriteResponce(text, response);
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogEx("Inspection Error", ex, Category);
                                await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied")), response);
                                return true;
                            }
                        }
                            break;
                        case var s when s.StartsWith("maillist"):
                        {
                            var values = data.Replace("maillist", "");
                            if (!int.TryParse(values, out var inspectCharId))
                                return true;
                            var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharId);
                            var hasToken = userTokenEntity != null;
                            if (hasToken && page > 0)
                            {
                                var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                    Settings.WebServerModule.CcpAppSecret);
                                var mailHtml = await GenerateMailHtml(token, inspectCharId, authCode, page);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Table)
                                    .Replace("{table}", mailHtml);
                                   
                                await WebServerModule.WriteResponce(text, response);
                            }
                        }
                            break;
                        case var s when s.StartsWith("transactlist"):
                        {
                            var values = data.Replace("transactlist", "");
                            if (!int.TryParse(values, out var inspectCharId))
                                return true;
                            var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharId);
                            var hasToken = userTokenEntity != null;
                            if (hasToken && page > 0)
                            {
                                var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                    Settings.WebServerModule.CcpAppSecret);
                                var html = await GenerateTransactionsHtml(token, inspectCharId, page);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Table)
                                    .Replace("{table}", html);
                                   
                                await WebServerModule.WriteResponce(text, response);
                            }
                        }
                            break;
                        case var s when s.StartsWith("journallist"):
                        {
                            var values = data.Replace("journallist", "");
                            if (!int.TryParse(values, out var inspectCharId))
                                return true;
                            var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharId);
                            var hasToken = userTokenEntity != null;
                            if (hasToken && page > 0)
                            {
                                var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                    Settings.WebServerModule.CcpAppSecret);
                                var html = await GenerateJournalHtml(token, inspectCharId, page);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Table)
                                    .Replace("{table}", html);
                                   
                                await WebServerModule.WriteResponce(text, response);
                            }
                        }
                            break;
                        case var s when s.StartsWith("contracts"):
                        {
                            var values = data.Replace("contracts", "");
                            if (!int.TryParse(values, out var inspectCharId))
                                return true;
                            var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharId);
                            var hasToken = userTokenEntity != null;
                            if (hasToken && page > 0)
                            {
                                var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                    Settings.WebServerModule.CcpAppSecret);
                                var html = await GenerateContractsHtml(token, inspectCharId, page);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Table)
                                    .Replace("{table}", html);
                                   
                                await WebServerModule.WriteResponce(text, response);
                            }
                        }
                            break;
                        case var s when s.StartsWith("contacts"):
                        {
                            var values = data.Replace("contacts", "");
                            if (!int.TryParse(values, out var inspectCharId))
                                return true;
                            var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharId);
                            var hasToken = userTokenEntity != null;
                            if (hasToken && page > 0)
                            {
                                var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                    Settings.WebServerModule.CcpAppSecret);
                                var html = await GenerateContactsHtml(token, inspectCharId, page);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Table)
                                    .Replace("{table}", html);
                                   
                                await WebServerModule.WriteResponce(text, response);
                            }
                        }
                            break;
                        case var s when s.StartsWith("skills"):
                        {
                            var values = data.Replace("skills", "");
                            if (!int.TryParse(values, out var inspectCharId))
                                return true;
                            var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharId);
                            var hasToken = userTokenEntity != null;
                            if (hasToken && page > 0)
                            {
                                var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                    Settings.WebServerModule.CcpAppSecret);
                                var html = await GenerateSkillsHtml(token, inspectCharId, page);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Table)
                                    .Replace("{table}", html);
                                   
                                await WebServerModule.WriteResponce(text, response);
                            }
                        }
                            break;

                        case var s when s.StartsWith("lys"):
                        {
                            var values = data.Replace("lys", "");
                            if (!int.TryParse(values, out var inspectCharId))
                                return true;
                            var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharId);
                            var hasToken = userTokenEntity != null;
                            if (hasToken && page > 0)
                            {
                                var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                    Settings.WebServerModule.CcpAppSecret);
                                var html = await GenerateLysHtml(token, inspectCharId, page);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Table)
                                    .Replace("{table}", html);
                                   
                                await WebServerModule.WriteResponce(text, response);
                            }
                        }
                            break;
                        case var s when s.StartsWith("mail"):
                        {
                            try
                            {
                                var values = data.Replace("mail", "").Split('_');
                                if (!int.TryParse(values[0], out var mailBodyId))
                                    return true;
                                var inspectCharacterId = Convert.ToInt32(values[1]);
                                var userTokenEntity = await SQLHelper.UserTokensGetEntry(inspectCharacterId);
                                var token = await APIHelper.ESIAPI.RefreshToken(userTokenEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                    Settings.WebServerModule.CcpAppSecret);
                                var mail = await APIHelper.ESIAPI.GetMail(Reason, inspectCharacterId, token, mailBodyId);
                                if (mail != null)
                                {
                                    var from = await APIHelper.ESIAPI.GetCharacterData(Reason, mail.@from);
                                    var corpHistory = await APIHelper.ESIAPI.GetCharCorpHistory(Reason, mail.@from);
                                    var date = DateTime.Parse(mail.timestamp);
                                    var corp = corpHistory.OrderByDescending(a => a.Date).FirstOrDefault(a => date >= a.Date);
                                    var fromCorp = corp == null ? null : await APIHelper.ESIAPI.GetCorporationData(Reason, corp.corporation_id);
                                    var corpText = fromCorp == null ? null : $" - {fromCorp.name}[{fromCorp.ticker}]";
                                    //var msg = Regex.Replace(mail.body, @"<font size\s*=\s*"".+?""", @"<font ");
                                    var msg = await MailModule.PrepareBodyMessage(mail.body);
                                    var text = File.ReadAllText(SettingsManager.FileTemplateHRM_MailBody)
                                            .Replace("{DateHeader}", LM.Get("mailDateHeader"))
                                            .Replace("{SubjectHeader}", LM.Get("mailSubjectHeader"))
                                            .Replace("{FromHeader}", LM.Get("mailFromHeader"))
                                            .Replace("{TextHeader}", LM.Get("mailTextHeader"))
                                            .Replace("{mailDate}", date.ToString(Settings.Config.ShortTimeFormat))
                                            .Replace("{mailSubject}", mail.subject)
                                            .Replace("{maiFrom}", $"{@from?.name}{corpText}")
                                            .Replace("{mailText}", msg)
                                        ;

                                    await WebServerModule.WriteResponce(text, response);
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogEx("Inspection Mail Body Error", ex, Category);
                                if (IsNoRedirect(data))
                                    await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                                else await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied")), response);
                                return true;
                            }
                        }
                            break;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                await response.WriteContentAsync("ERROR: Server error");
                await LogHelper.LogEx(ex.Message, ex, Category);
            }

            return false;
        }

        private async Task<bool> CheckAccess(int characterId)
        {
            var firstCheck = !Settings.HRMModule.UsersAccessList.Contains(characterId);

            if (firstCheck)
            {
                var discordId = await SQLHelper.SQLiteDataQuery<ulong>("authUsers", "discordID", "characterID", characterId);
                if (discordId > 0)
                {
                    firstCheck = !APIHelper.DiscordAPI.GetUserRoleNames(discordId).Intersect(Settings.HRMModule.RolesAccessList).Any();
                }
            }

            return !firstCheck;
        }

    }
}
