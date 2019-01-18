using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json.Internal;
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


                if (request.HttpMethod == HttpMethod.Get.ToString())
                {
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
                        var characterId = result == null ? 0 : Convert.ToInt64(result[0]);


                        if (result == null || await CheckAccess(characterId) == false)
                        {
                            await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied")), response);
                            return true;
                        }

                        var authCode = WebAuthModule.GetUniqID();
                        await SQLHelper.UpdateHrmAuth(characterId, authCode);
                        //redirect to timers
                        await response.RedirectAsync(new Uri(WebServerModule.GetHRMMainURL(authCode)));

                        return true;
                    }

                    if (request.Url.LocalPath.StartsWith("/hrm.php") || request.Url.LocalPath.StartsWith($"{extPort}/hrm.php") ||
                        request.Url.LocalPath.StartsWith($"{port}/hrm.php"))
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

                        var data = prms.FirstOrDefault(a => a.StartsWith("data")).Split('=')[1];
                        var authCode = prms.FirstOrDefault(a => a.StartsWith("id")).Split('=')[1];
                        var state = prms.FirstOrDefault(a => a.StartsWith("state")).Split('=')[1];
                        var page = Convert.ToInt32(prms.FirstOrDefault(a => a.StartsWith("page"))?.Split('=')[1] ?? "0");
                        var query = prms.FirstOrDefault(a => a.StartsWith("query"))?.Split('=')[1];

                        if (state != "matahari")
                        {
                            if (IsNoRedirect(data))
                                await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                            else
                                await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }

                        var characterId = await SQLHelper.GetHRAuthCharacterId(authCode);

                        var rChar = characterId == 0 ? null : await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                        if (rChar == null)
                        {
                            if (IsNoRedirect(data))
                                await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                            else await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }


                        //have charId - had to check it
                        //check in db
                        var timeout = Settings.HRMModule.AuthTimeoutInMinutes;
                        if (timeout != 0)
                        {
                            var result = await SQLHelper.GetHRAuthTime(characterId);
                            if (result == null || (DateTime.Now - DateTime.Parse(result)).TotalMinutes > timeout)
                            {
                                if (IsNoRedirect(data))
                                    await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                                //redirect to auth
                                else await response.RedirectAsync(new Uri(WebServerModule.GetHRMAuthURL()));
                                return true;
                            }

                            //prolong session
                            if (data == "0" || data.StartsWith("inspect"))
                                await SQLHelper.SetHRAuthTime(authCode);
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
                            case var s when s.StartsWith("deleteAuth"):
                            {
                                try
                                {
                                    var searchCharId = Convert.ToInt64(s.Replace("deleteAuth", ""));
                                    if (searchCharId == 0) return true;

                                    await SQLHelper.DeleteAuthDataByCharId(searchCharId);
                                    await response.RedirectAsync(new Uri(WebServerModule.GetHRMMainURL(authCode)));
                                }
                                catch (Exception ex)
                                {
                                    await LogHelper.LogEx(s, ex, Category);
                                }

                                return true;
                            }
                            case var s when s.StartsWith("searchMail"):
                            {
                                try
                                {
                                    var searchCharId = Convert.ToInt64(s.Replace("searchMail", ""));
                                    if (string.IsNullOrEmpty(query))
                                    {
                                        //search page load
                                        var pageBody = File.ReadAllText(SettingsManager.FileTemplateHRM_SearchMailPage)
                                                .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                                                .Replace("{LogOutUrl}", WebServerModule.GetWebSiteUrl())
                                                .Replace("{LogOut}", LM.Get("LogOut"))
                                                .Replace("{searchMailContentHeader}", LM.Get("hrmSearchMailResultsHeader"))
                                                .Replace("{searchMailHeader}", LM.Get("hrmSearchMailHeader"))
                                                .Replace("{entryUserType}", LM.Get("hrmEntryUserType"))
                                                .Replace("{entryUserTypeAuthed}", LM.Get("hrmSearchMailAuthenticated"))
                                                .Replace("{entryUserTypeAwaiting}", LM.Get("hrmSearchMailAwaitingAuth"))
                                                .Replace("{StartSearchMail}", LM.Get("hrmSearchMailSearchButton"))
                                                .Replace("{entrySearchFor}", LM.Get("hrmSearchMailSearchFor"))
                                                .Replace("{entrySearchForSenderChar}", LM.Get("hrmSearchMailSearchForChar"))
                                                .Replace("{entrySearchForSenderCorp}", LM.Get("hrmSearchMailSearchForCorp"))
                                                .Replace("{entrySearchForSenderAlliance}", LM.Get("hrmSearchMailSearchForAlliance"))
                                                .Replace("{entrySearchForTitle}", LM.Get("hrmSearchMailSearchForSubject"))
                                                .Replace("{entrySearchTerm}", LM.Get("hrmSearchMailTerm"))
                                                .Replace("{entrySearchTermTooltip}", LM.Get("hrmSearchMailTermTooltip"))
                                                .Replace("{mailSearchUrl}", WebServerModule.GetHRM_SearchMailURL(searchCharId, authCode))
                                                .Replace("{backUrl}", WebServerModule.GetHRMMainURL(authCode))
                                                .Replace("{Back}", LM.Get("Back"))
                                                .Replace("{Close}", LM.Get("Close"))
                                                .Replace("{mailHeader}", LM.Get("mailHeader"))
                                                .Replace("{isGlobal}", searchCharId > 0 ? "false" : "true")

                                            ;
                                        await WebServerModule.WriteResponce(pageBody, response);

                                        return true;
                                    }

                                    SearchMailItem item = null;
                                    try
                                    {
                                        item = JsonConvert.DeserializeObject<SearchMailItem>(HttpUtility.UrlDecode(query));
                                    }
                                    catch(Exception ex)
                                    {
                                        await WebServerModule.WriteResponce(LM.Get("hrmSearchMailInvalidRequest"), response);
                                        return true;
                                    }

                                    if (item == null)
                                        return true;
                                    var mailHtml = await MailModule.SearchRelated(searchCharId, item, authCode);

                                    var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Table)
                                        .Replace("{table}", mailHtml);

                                    await WebServerModule.WriteResponce(text, response);

                                }
                                catch (Exception ex)
                                {
                                    await LogHelper.LogEx("searchMail", ex, Category);
                                    return true;
                                }
                            }
                                break;
                            //main page
                            case "0":
                            {
                                var membersHtml = await GenerateMembersListHtml(authCode);
                                var awaitingHtml = await GenerateAwaitingListHtml(authCode);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Main).Replace("{header}", LM.Get("hrmTemplateHeader"))
                                        .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                                        .Replace("{LogOutUrl}", WebServerModule.GetWebSiteUrl())
                                        .Replace("{LogOut}", LM.Get("LogOut"))
                                        .Replace("{locale}", LM.Locale)
                                        .Replace("{membersContent}", membersHtml)
                                        .Replace("{awaitingContent}", awaitingHtml)
                                        .Replace("{membersHeader}", LM.Get("hrmMembersHeader"))
                                        .Replace("{awaitingHeader}", LM.Get("hrmAwaitingHeader"))
                                        .Replace("{butSearchMail}", LM.Get("hrmButSearchMail"))
                                        .Replace("{butSearchMailUrl}", WebServerModule.GetHRM_SearchMailURL(0, authCode))

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
                                    var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                    var hasToken = authUserEntity != null && authUserEntity.HasToken;

                                    var corpHistoryHtml = await GenerateCorpHistory(inspectCharId);
                                    var pList = authUserEntity?.Data.PermissionsList;
                                    var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Inspect).Replace("{header}", LM.Get("hrmInspectingHeader", iChar.name))
                                            .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                                            .Replace("{charId}", characterId.ToString())
                                            .Replace("{LogOutUrl}", WebServerModule.GetWebSiteUrl())
                                            .Replace("{LogOut}", LM.Get("LogOut"))
                                            .Replace("{locale}", LM.Locale)
                                            .Replace("{imgCorp}", $"https://image.eveonline.com/Corporation/{iChar.corporation_id}_64.png")
                                            .Replace("{imgAlly}", $"https://image.eveonline.com/Alliance/{iCorp.alliance_id ?? 0}_64.png")
                                            .Replace("{imgChar}", $"https://image.eveonline.com/Character/{inspectCharId}_256.jpg")
                                            .Replace("{zkillChar}", $"https://zkillboard.com/character/{inspectCharId}")
                                            .Replace("{zkillCorp}", $"https://zkillboard.com/corporation/{iChar.corporation_id}")
                                            .Replace("{zkillAlly}", $"https://zkillboard.com/alliance/{iCorp.alliance_id ?? 0}")
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

                                            .Replace("{disableMail}", SettingsManager.HasReadMailScope(pList) ? null : "d-none")
                                            .Replace("{allowMailBool}", SettingsManager.HasReadMailScope(pList) ? "true" : "false")
                                            .Replace("{disableWallet}", SettingsManager.HasCharWalletScope(pList) ? null : "d-none")
                                            .Replace("{allowWalletBool}", SettingsManager.HasCharWalletScope(pList) ? "true" : "false")
                                            .Replace("{disableCharStats}", SettingsManager.HasCharWalletScope(pList) ? null : "d-none")
                                            .Replace("{allowCharStatsBool}", SettingsManager.HasCharWalletScope(pList) ? "true" : "false")
                                            .Replace("{disableContracts}", SettingsManager.HasCharWalletScope(pList) ? null : "d-none")
                                            .Replace("{allowContractsBool}", SettingsManager.HasCharWalletScope(pList) ? "true" : "false")
                                            .Replace("{disableContacts}", SettingsManager.HasCharContactsScope(pList) ? null : "d-none")
                                            .Replace("{allowContactsBool}", SettingsManager.HasCharContactsScope(pList) ? "true" : "false")
                                            .Replace("{disableSP}", SettingsManager.HasCharSkillsScope(pList) ? null : "d-none")
                                            .Replace("{disableSkills}", SettingsManager.HasCharSkillsScope(pList) ? null : "d-none")
                                            .Replace("{allowSkillsBool}", SettingsManager.HasCharSkillsScope(pList) ? "true" : "false")
                                        
                                            .Replace("{butSearchMail}", LM.Get("hrmButSearchMail"))
                                            .Replace("{butSearchMailUrl}", WebServerModule.GetHRM_SearchMailURL(inspectCharId, authCode))

                                            .Replace("{ConfirmUserDelete}", LM.Get("hrmButDeleteUserAuthConfirm"))
                                            .Replace("{butDeleteUserUrl}", WebServerModule.GetHRM_DeleteCharAuthURL(inspectCharId, authCode))
                                            .Replace("{butDeleteUser}", LM.Get("hrmButDeleteUserAuth"))
                                        ;

                                    //private info
                                    if (hasToken)
                                    {
                                        var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                            Settings.WebServerModule.CcpAppSecret);
                                        if (SettingsManager.HasReadMailScope(pList))
                                        {
                                            var total = await GetMailPagesCount(token, inspectCharId);
                                            text = text.Replace("totalMailPages!!", total.ToString());
                                        }

                                        if (SettingsManager.HasCharWalletScope(pList))
                                        {
                                            var total = await GetCharTransactPagesCount(token, inspectCharId);
                                            text = text.Replace("totalTransactPages!!", total.ToString());
                                            total = await GetCharJournalPagesCount(token, inspectCharId);
                                            text = text.Replace("totalJournalPages!!", total.ToString());
                                        }

                                        if (SettingsManager.HasCharStatsScope(pList))
                                        {
                                            var total = GetCharJournalPagesCount();
                                            text = text.Replace("totalLysPages!!", total.ToString());
                                        }

                                        if (SettingsManager.HasCharContractsScope(pList))
                                        {
                                            var total = await GetCharContractsPagesCount(token, inspectCharId);
                                            text = text.Replace("totalcontractsPages!!", total.ToString());
                                        }

                                        if (SettingsManager.HasCharContactsScope(pList))
                                        {
                                            var total = await GetCharContactsPagesCount(token, inspectCharId);
                                            text = text.Replace("totalcontactsPages!!", total.ToString());
                                        }

                                        if (SettingsManager.HasCharSkillsScope(pList))
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
                                var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                var hasToken = authUserEntity != null && authUserEntity.HasToken;
                                if (hasToken && page > 0)
                                {
                                    var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
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
                                var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                var hasToken = authUserEntity != null && authUserEntity.HasToken;
                                if (hasToken && page > 0)
                                {
                                    var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
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
                                var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                var hasToken = authUserEntity != null && authUserEntity.HasToken;
                                if (hasToken && page > 0)
                                {
                                    var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
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
                                var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                var hasToken = authUserEntity != null && authUserEntity.HasToken;
                                if (hasToken && page > 0)
                                {
                                    var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
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
                                var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                var hasToken = authUserEntity != null && authUserEntity.HasToken;
                                if (hasToken && page > 0)
                                {
                                    var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
                                        Settings.WebServerModule.CcpAppSecret);
                                    var html = await GenerateContactsHtml(token, inspectCharId, page, characterId);

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
                                var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                var hasToken = authUserEntity != null && authUserEntity.HasToken;
                                if (hasToken && page > 0)
                                {
                                    var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
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
                                var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                var hasToken = authUserEntity != null && authUserEntity.HasToken;
                                if (hasToken && page > 0)
                                {
                                    var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
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
                                    var inspectCharacterId = Convert.ToInt64(values[1]);
                                    var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharacterId);
                                    var token = await APIHelper.ESIAPI.RefreshToken(authUserEntity.RefreshToken, Settings.WebServerModule.CcpAppClientId,
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
                else if (request.HttpMethod == HttpMethod.Post.ToString())
                {
                }
            }
            catch (Exception ex)
            {
                await response.WriteContentAsync("ERROR: Server error");
                await LogHelper.LogEx(ex.Message, ex, Category);
            }

            return false;
        }

        public class SearchMailItem
        {
            public int smAuthType;
            public int smSearchType;
            public string smText;
        }

        private async Task<bool> CheckAccess(long characterId)
        {
            var firstCheck = !Settings.HRMModule.UsersAccessList.Contains(characterId);

            if (firstCheck)
            {
                var discordId = await SQLHelper.GetAuthUserDiscordId(characterId);
                if (discordId > 0)
                {
                    firstCheck = !APIHelper.DiscordAPI.GetUserRoleNames(discordId).Intersect(Settings.HRMModule.RolesAccessList).Any();
                }
            }

            return !firstCheck;
        }

    }
}
