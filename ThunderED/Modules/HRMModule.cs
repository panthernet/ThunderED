using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules
{
    public partial class HRMModule: AppModuleBase
    {
        public override LogCat Category => LogCat.HRM;
            
        public HRMModule()
        {
            LogHelper.LogModule("Inititalizing HRM module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, OnRequestReceived);
        }

        private static DateTime _lastUpdateDate = DateTime.MinValue;
        private static DateTime _lastSpyMailUpdateDate = DateTime.MinValue;

        public override async Task Run(object prm)
        {
            if (!Settings.Config.ModuleHRM) return;

            if(IsRunning) return;
            IsRunning = true;
            try
            {
                if ((DateTime.Now - _lastUpdateDate).TotalMinutes >= 25)
                {
                    _lastUpdateDate = DateTime.Now;
                    var list = await SQLHelper.GetAuthUsersWithPerms((int)UserStatusEnum.Dumped);
                    foreach (var user in list)
                    {
                        if (Settings.HRMModule.DumpInvalidationInHours > 0 && user.DumpDate.HasValue && (DateTime.Now - user.DumpDate.Value).TotalHours >= Settings.HRMModule.DumpInvalidationInHours)
                        {
                            await LogHelper.LogInfo($"Disposing dumped member {user.Data.CharacterName}({user.CharacterId})...");
                            await SQLHelper.DeleteAuthDataByCharId(user.CharacterId);
                        }
                    }
                }

                if (Settings.HRMModule.SpiesMailFeedChannelId > 0 && (DateTime.Now - _lastSpyMailUpdateDate).TotalMinutes >= 10)
                {
                    _lastSpyMailUpdateDate = DateTime.Now;
                    var spies = await SQLHelper.GetAuthUsers((int) UserStatusEnum.Spying);
                    await MailModule.FeedSpyMail(spies, Settings.HRMModule.SpiesMailFeedChannelId);
                }
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


                        if (result == null || await CheckAccess(characterId) == null)
                        {
                            await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetWebSiteUrl()), response);
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
                            if (HRMModule.IsNoRedirect(data))
                                await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                            else
                                await response.RedirectAsync(new Uri(WebServerModule.GetWebSiteUrl()));
                            return true;
                        }

                        var characterId = await SQLHelper.GetHRAuthCharacterId(authCode);

                        var rChar = characterId == 0 ? null : await APIHelper.ESIAPI.GetCharacterData(Reason, characterId, true);
                        if (rChar == null)
                        {
                            if (HRMModule.IsNoRedirect(data))
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
                                if (HRMModule.IsNoRedirect(data))
                                    await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                                //redirect to auth
                                else await response.RedirectAsync(new Uri(WebServerModule.GetHRMAuthURL()));
                                return true;
                            }

                            //prolong session
                            if (data == "0" || data.StartsWith("inspect"))
                                await SQLHelper.SetHRAuthTime(authCode);
                        }

                        var accessFilter = await CheckAccess(characterId);
                        if (accessFilter == null)
                        {
                            if (HRMModule.IsNoRedirect(data))
                                await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                            else await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetWebSiteUrl()), response);
                            return true;
                        }

                        switch (data)
                        {
                            case var s when s.StartsWith("moveToSpies"):
                            {
                                if (!accessFilter.CanMoveToSpies) return true;
                                var charId = Convert.ToInt64(s.Replace("moveToSpies", ""));
                                if (charId == 0) return true;
                                var user = await SQLHelper.GetAuthUserByCharacterId(charId);
                                if (user == null) return true;
                                user.SetStateSpying();
                                await SQLHelper.SaveAuthUser(user);
                                await response.RedirectAsync(new Uri(WebServerModule.GetHRMMainURL(authCode)));
                                return true;
                            }
                            case var s when s.StartsWith("deleteAuth"):
                            {
                                try
                                {
                                    var searchCharId = Convert.ToInt64(s.Replace("deleteAuth", ""));
                                    if (searchCharId == 0) return true;
                                    if (!await IsValidUserForInteraction(accessFilter, searchCharId) || !accessFilter.CanKickUsers)
                                        return true;

                                    var sUser = await SQLHelper.GetAuthUserByCharacterId(searchCharId);

                                    if (sUser == null) return true;
                                    if (Settings.HRMModule.UseDumpForMembers && !sUser.IsDumped)
                                    {
                                        sUser.SetStateDumpster();
                                        await SQLHelper.SaveAuthUser(sUser);
                                    }
                                    else
                                    {
                                        await SQLHelper.DeleteAuthDataByCharId(searchCharId);
                                    }

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
                                    if (!await IsValidUserForInteraction(accessFilter, searchCharId) || !accessFilter.CanSearchMail)
                                    {
                                        await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetHRMMainURL(authCode)), response);
                                        return true;
                                    }

                                    if (string.IsNullOrEmpty(query))
                                    {
                                        //search page load
                                        var pageBody = File.ReadAllText(SettingsManager.FileTemplateHRM_SearchMailPage)
                                                .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(true))
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
                                return true;
                            }
                            case var s when s.StartsWith("loadMembers"):
                            {
                                var typeValue = Convert.ToInt64(prms.FirstOrDefault(a=> a.StartsWith("memberType")).Split('=')[1]);
                                var authState = (UserStatusEnum) typeValue;
                                if(authState == UserStatusEnum.Authed && !accessFilter.IsAuthedUsersVisible)
                                {
                                    await WebServerModule.WriteResponce(LM.Get("accessDenied"), response);
                                    return true;
                                }
                                if(authState == UserStatusEnum.Dumped && !accessFilter.IsDumpedUsersVisible)
                                {
                                    await WebServerModule.WriteResponce(LM.Get("accessDenied"), response);
                                    return true;
                                }
                                if(authState == UserStatusEnum.Spying && !accessFilter.IsSpyUsersVisible)
                                {
                                    await WebServerModule.WriteResponce(LM.Get("accessDenied"), response);
                                    return true;
                                }
                                if((authState == UserStatusEnum.Initial || authState == UserStatusEnum.Awaiting) && !accessFilter.IsAwaitingUsersVisible)
                                {
                                    await WebServerModule.WriteResponce(LM.Get("accessDenied"), response);
                                    return true;
                                }

                                var filterValue = Convert.ToInt64(prms.FirstOrDefault(a=> a.StartsWith("filter")).Split('=')[1]);

                                switch (authState)
                                {
                                    case UserStatusEnum.Authed:
                                        await WebServerModule.WriteResponce((await HRMModule.GenerateMembersListHtml(authCode, accessFilter, filterValue, GenMemType.Members))[0], response);
                                        return true;
                                    case UserStatusEnum.Dumped:
                                        await WebServerModule.WriteResponce((await HRMModule.GenerateDumpListHtml(authCode, accessFilter, filterValue, GenMemType.Members))[0], response);
                                        return true;
                                    case UserStatusEnum.Spying:
                                        await WebServerModule.WriteResponce((await HRMModule.GenerateSpiesListHtml(authCode, accessFilter, filterValue, GenMemType.Members))[0], response);
                                        return true;
                                    default:
                                        await WebServerModule.WriteResponce((await HRMModule.GenerateAwaitingListHtml(authCode, accessFilter, filterValue, GenMemType.Members))[0], response);
                                        return true;
                                }
                            }
                            //main page
                            case "0":
                            {
                                var res = !accessFilter.IsAuthedUsersVisible ? new string[] {null, null} : await HRMModule.GenerateMembersListHtml(authCode, accessFilter, 0, GenMemType.Filter);
                                var membersHtml = res[0];
                                var membersFilterContent = res[1];
                                var membersAllowed = accessFilter.IsAuthedUsersVisible ? "true" : "false";
                                var membersUrl = WebServerModule.GetHRM_AjaxMembersURL((int)UserStatusEnum.Authed, authCode);
                                res = !accessFilter.IsAwaitingUsersVisible ? new string[] {null, null} :  await HRMModule.GenerateAwaitingListHtml(authCode, accessFilter, 0, GenMemType.Filter);
                                var awaitingHtml = res[0];
                                var awaitingFilterContent = res[1];
                                var awaitingAllowed = accessFilter.IsAwaitingUsersVisible ? "true" : "false";
                                var awaitingUrl = WebServerModule.GetHRM_AjaxMembersURL((int)UserStatusEnum.Awaiting, authCode);
                                res = !accessFilter.IsDumpedUsersVisible ? new string[] {null, null} :  await HRMModule.GenerateDumpListHtml(authCode, accessFilter, 0, GenMemType.Filter);
                                var dumpHtml = res[0];
                                var dumpFilterContent = res[1];
                                var dumpedAllowed = accessFilter.IsDumpedUsersVisible ? "true" : "false";
                                var dumpedUrl = WebServerModule.GetHRM_AjaxMembersURL((int)UserStatusEnum.Dumped, authCode);
                                res = !accessFilter.IsSpyUsersVisible ? new string[] {null, null} :  await HRMModule.GenerateSpiesListHtml(authCode, accessFilter, 0, GenMemType.Filter);
                                var spiesHtml = res[0];
                                var spiesFilterContent = res[1];
                                var spiesAllowed = accessFilter.IsSpyUsersVisible ? "true" : "false";
                                var spiesUrl = WebServerModule.GetHRM_AjaxMembersURL((int)UserStatusEnum.Spying, authCode);

                                var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Main).Replace("{header}", LM.Get("hrmTemplateHeader"))
                                        .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(true)+WebServerModule.GetHtmlResourceConfirmation())
                                        .Replace("{loggedInAs}", LM.Get("loggedInAs", rChar.name))
                                        .Replace("{LogOutUrl}", WebServerModule.GetWebSiteUrl())
                                        .Replace("{LogOut}", LM.Get("LogOut"))
                                        .Replace("{locale}", LM.Locale)
                                       // .Replace("{membersContent}", membersHtml)
                                       // .Replace("{awaitingContent}", awaitingHtml)
                                      //  .Replace("{dumpContent}", dumpHtml)
                                        .Replace("{authedFilterContent}", membersFilterContent)
                                        .Replace("{awaitingFilterContent}", awaitingFilterContent)
                                        .Replace("{dumpedFilterContent}", dumpFilterContent)
                                        .Replace("{spyingFilterContent}", spiesFilterContent)

                                        .Replace("{allowAuthedScript}", membersAllowed)
                                        .Replace("{allowAwaitingScript}", awaitingAllowed)
                                        .Replace("{allowDumpedScript}", dumpedAllowed)
                                        .Replace("{allowSpyingScript}", spiesAllowed)
                                        .Replace("{authedListUrl}", membersUrl)
                                        .Replace("{awaitingListUrl}", awaitingUrl)
                                        .Replace("{dumpedListUrl}", dumpedUrl)
                                        .Replace("{spyingListUrl}", spiesUrl)

                                        .Replace("{membersHeader}", LM.Get("hrmMembersHeader"))
                                        .Replace("{awaitingHeader}", LM.Get("hrmAwaitingHeader"))
                                        .Replace("{dumpHeader}", LM.Get("hrmDumpHeader"))
                                        .Replace("{spyingHeader}", LM.Get("hrmSpiesHeader"))
                                        .Replace("{butSearchMail}", LM.Get("hrmButSearchMail"))
                                        .Replace("{butSearchMailUrl}", WebServerModule.GetHRM_SearchMailURL(0, authCode))
                                        .Replace("{disableAuthed}", !accessFilter.IsAuthedUsersVisible ? " d-none" : null)
                                        .Replace("{disableAwaiting}", !accessFilter.IsAwaitingUsersVisible ? " d-none" : null)
                                        .Replace("{disableDumped}", !accessFilter.IsDumpedUsersVisible ? " d-none" : null)
                                        .Replace("{disableSpying}", !accessFilter.IsSpyUsersVisible ? " d-none" : null)
                                        .Replace("{canSearchMail}", !accessFilter.CanSearchMail ? " d-none" : null)

                                    ;
                                await WebServerModule.WriteResponce(text, response);
                            }
                                break;
                            case var s when s.StartsWith("inspect"):
                            {
                                try
                                {
                                    if (!long.TryParse(data.Replace("inspect", ""), out var inspectCharId))
                                    {
                                        await response.RedirectAsync(new Uri(WebServerModule.GetHRMAuthURL()));
                                        return true;
                                    }

                                    if (!await IsValidUserForInteraction(accessFilter, inspectCharId))
                                    {
                                        await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetHRMMainURL(authCode)), response);
                                        return true;
                                    }

                                    var iChar = await APIHelper.ESIAPI.GetCharacterData(Reason, inspectCharId, true);
                                    var iCorp = await APIHelper.ESIAPI.GetCorporationData(Reason, iChar.corporation_id, true);
                                    var iAlly = iCorp.alliance_id.HasValue ? await APIHelper.ESIAPI.GetAllianceData(Reason, iCorp.alliance_id.Value, true) : null;
                                    var authUserEntity = await SQLHelper.GetAuthUserByCharacterId(inspectCharId);
                                    
                                    if(!accessFilter.CanAccessUser(authUserEntity.AuthState))
                                    {
                                        await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetHRMMainURL(authCode)), response);
                                        return true;
                                    }
                                    var hasToken = authUserEntity != null && authUserEntity.HasToken;

                                    var corpHistoryHtml = await GenerateCorpHistory(inspectCharId);
                                    var pList = authUserEntity?.Data.PermissionsList;
                                    var text = File.ReadAllText(SettingsManager.FileTemplateHRM_Inspect).Replace("{header}", LM.Get("hrmInspectingHeader", iChar.name))
                                            .Replace("{headerContent}", WebServerModule.GetHtmlResourceDefault(true) + WebServerModule.GetHtmlResourceConfirmation()+ WebServerModule.GetHtmlResourceBootpage())
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

                                            .Replace("{canSearchMail}", !accessFilter.CanSearchMail ? " d-none" : null)
                                            .Replace("{canKickUsers}", !accessFilter.CanKickUsers ? " d-none" : null)


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
                                            .Replace("{disableCharStats}", SettingsManager.HasCharStatsScope(pList) ? null : "d-none")
                                            .Replace("{allowCharStatsBool}", SettingsManager.HasCharStatsScope(pList) ? "true" : "false")
                                            .Replace("{disableContracts}", SettingsManager.HasCharContractsScope(pList) ? null : "d-none")
                                            .Replace("{allowContractsBool}", SettingsManager.HasCharContractsScope(pList) ? "true" : "false")
                                            .Replace("{disableContacts}", SettingsManager.HasCharContactsScope(pList) ? null : "d-none")
                                            .Replace("{allowContactsBool}", SettingsManager.HasCharContactsScope(pList) ? "true" : "false")
                                            .Replace("{disableSP}", SettingsManager.HasCharSkillsScope(pList) ? null : "d-none")
                                            .Replace("{disableSkills}", SettingsManager.HasCharSkillsScope(pList) ? null : "d-none")
                                            .Replace("{allowSkillsBool}", SettingsManager.HasCharSkillsScope(pList) ? "true" : "false")
                                            .Replace("{disableISK}", SettingsManager.HasCharWalletScope(pList) ? null : "d-none")
                                            .Replace("{disableLocation}", SettingsManager.HasCharLocationScope(pList) ? null : "d-none")
                                            .Replace("{disableShip}", SettingsManager.HasCharShipTypeScope(pList) ? null : "d-none")
                                        
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

                                        if (string.IsNullOrEmpty(token))
                                        {
                                            await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("hrmInvalidUserToken"), WebServerModule.GetHRMMainURL(authCode)), response);
                                            return true;
                                        }
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

                                            var value = await APIHelper.ESIAPI.GetCharacterWalletBalance(Reason, inspectCharId, token);
                                            text = text.Replace("{totalISK}", value.ToString("N"));
                                        }

                                        if (SettingsManager.HasCharLocationScope(pList))
                                        {
                                            var locationData = await APIHelper.ESIAPI.GetCharacterLocation(Reason, inspectCharId, token);
                                            var system = await APIHelper.ESIAPI.GetSystemData(Reason, locationData.solar_system_id);
                                            var station = locationData.station_id > 0 ? await APIHelper.ESIAPI.GetStationData(Reason, locationData.station_id, token) : null;
                                            var citadel = locationData.structure_id > 0 ? await APIHelper.ESIAPI.GetStructureData(Reason, locationData.structure_id, token) : null;
                                            var loc = station != null ? station.name : citadel?.name;
                                            
                                            var value = $"{system.name} {loc}";
                                            text = text.Replace("{locationData}", value).Replace("{hrmInspectLocation}", LM.Get("hrmInspectLocation"));
                                        }

                                        if (SettingsManager.HasCharLocationScope(pList))
                                        {
                                            var stData = await APIHelper.ESIAPI.GetCharacterShipType(Reason, inspectCharId, token);
                                            var tType = await APIHelper.ESIAPI.GetTypeId(Reason, stData.ship_type_id);
                                            
                                            text = text.Replace("{shipData}", tType.name).Replace("{hrmInspectShip}", LM.Get("hrmInspectShip"));
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
                                    await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetHRMMainURL(authCode)), response);
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
                                if (!IsValidUserForInteraction(accessFilter, authUserEntity))
                                {
                                    await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetHRMMainURL(authCode)), response);
                                    return true;
                                }
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
                                if (!IsValidUserForInteraction(accessFilter, authUserEntity))
                                {
                                    await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetHRMMainURL(authCode)), response);
                                    return true;
                                }
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
                                if (!IsValidUserForInteraction(accessFilter, authUserEntity))
                                {
                                    await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetHRMMainURL(authCode)), response);
                                    return true;
                                }
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
                                if (!IsValidUserForInteraction(accessFilter, authUserEntity))
                                {
                                    await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetWebSiteUrl()), response);
                                    return true;
                                }
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
                                if (!IsValidUserForInteraction(accessFilter, authUserEntity))
                                {
                                    await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetWebSiteUrl()), response);
                                    return true;
                                }
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
                                if (!IsValidUserForInteraction(accessFilter, authUserEntity))
                                {
                                    await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetWebSiteUrl()), response);
                                    return true;
                                }
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
                                if (!IsValidUserForInteraction(accessFilter, authUserEntity))
                                {
                                    await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetWebSiteUrl()), response);
                                    return true;
                                }
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
                                    if (!IsValidUserForInteraction(accessFilter, authUserEntity))
                                    {
                                        await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetWebSiteUrl()), response);
                                        return true;
                                    }
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
                                        var msg = await MailModule.PrepareBodyMessage(mail.body, true);
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
                                    if (HRMModule.IsNoRedirect(data))
                                        await WebServerModule.WriteResponce(LM.Get("pleaseReauth"), response);
                                    else await WebServerModule.WriteResponce(WebServerModule.GetAccessDeniedPage("HRM Module", LM.Get("accessDenied"), WebServerModule.GetHRMMainURL(authCode)), response);
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

        private async Task<bool> IsValidUserForInteraction(HRMAccessFilter filter, long characterId)
        {
            var user = await SQLHelper.GetAuthUserByCharacterId(characterId);
            return IsValidUserForInteraction(filter, user);

        }
        private static bool IsValidUserForInteraction(HRMAccessFilter filter, AuthUserEntity user)
        {
            if (user == null) return false;

            var isNotAuthed = user.IsPending;
            //is not authed and filter option ? DENY
            if (!filter.IsAwaitingUsersVisible && isNotAuthed)
                return false;

            if (!filter.IsDumpedUsersVisible && user.IsDumped)
                return false;

            if (!filter.IsSpyUsersVisible && user.IsSpying)
                return false;

            //invalid group ? DENY
            if (!user.IsDumped && !user.IsSpying && (!isNotAuthed || filter.ApplyGroupFilterToAwaitingUsers) && filter.AuthGroupNamesFilter.Any() && !filter.AuthGroupNamesFilter.Contains(user.GroupName))
                return false;

            //authed and have invalid ally or corp? DENY
            if(!isNotAuthed && !user.IsDumped && !user.IsSpying && ((filter.AuthAllianceIdFilter.Any() && user.Data.AllianceId > 0 && !filter.AuthAllianceIdFilter.Contains(user.Data.AllianceId)) ||
               (filter.AuthCorporationIdFilter.Any() && !filter.AuthCorporationIdFilter.Contains(user.Data.CorporationId))))
                return false;

            return true;
        }

        public class SearchMailItem
        {
            public int smAuthType;
            public int smSearchType;
            public string smText;
        }

        private async Task<HRMAccessFilter> CheckAccess(long characterId)
        {
            var result = Settings.HRMModule.AccessList.Values.FirstOrDefault(a=> a.UsersAccessList.Contains(characterId));

            if (result != null) return result;
            var discordId = await SQLHelper.GetAuthUserDiscordId(characterId);
            if (discordId <= 0) return null;
            var discordRoles = APIHelper.DiscordAPI.GetUserRoleNames(discordId);
            foreach (var list in Settings.HRMModule.AccessList.Values)
            {
                if (discordRoles.Intersect(list.RolesAccessList).Any())
                    return list;
            }

            return null;
        }

    }

    internal enum GenMemType
    {
        Members,
        Filter,
        All
    }
}
