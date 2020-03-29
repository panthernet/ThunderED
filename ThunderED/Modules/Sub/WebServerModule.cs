using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using HttpListener = System.Net.Http.HttpListener;

namespace ThunderED.Modules.Sub
{
    public sealed partial class WebServerModule : AppModuleBase, IDisposable
    {
        private static HttpListener _listener;
        private static HttpListener _statusListener;
        private readonly ConcurrentQueue<DateTime> _statusQueries = new ConcurrentQueue<DateTime>();
        private readonly Timer _statusTimer = new Timer(5000);
        public override LogCat Category => LogCat.WebServer;

        public static string HttpPrefix => SettingsManager.Settings.WebServerModule.UseHTTPS ? "https" : "http";

        public static Dictionary<string, Func<HttpListenerRequestEventArgs, Task<bool>>> ModuleConnectors { get; } = new Dictionary<string, Func<HttpListenerRequestEventArgs, Task<bool>>>();


        public WebServerModule()
        {
            LogHelper.LogModule("Initializing WebServer module...", Category).GetAwaiter().GetResult();
            ModuleConnectors.Clear();
            ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, sslPolicyErrors) => true;
        }

        public override async Task Initialize()
        {
            if (Settings.WebServerModule.WebExternalPort == Settings.WebServerModule.ServerStatusPort && Settings.WebServerModule.ServerStatusPort != 0)
                await LogHelper.LogWarning($"Web server is configured to use teh same {Settings.WebServerModule.ServerStatusPort} port for both http and status queries!");
            if (Settings.WebServerModule.ServerStatusPort > 0)
            {
                _statusTimer.Elapsed += (sender, args) =>
                {
                    if (_statusQueries.Any())
                        _statusQueries.TryDequeue(out _);
                };
                _statusTimer.Start();
            }
        }

        public override async Task Run(object prm)
        {
            if (!Settings.Config.ModuleWebServer)
            {
                return;
            }

            if (Settings.WebServerModule.ServerStatusPort > 0 && (_statusListener == null || !_statusListener.IsListening))
            {
                await LogHelper.LogInfo("Starting Web Server - Status Reporter", Category);
                try
                {
                    _statusListener?.Dispose();
                }
                catch
                {
                    // ignored
                }

                var ip = "0.0.0.0";

                _statusListener = new HttpListener(IPAddress.Parse(ip), Settings.WebServerModule.ServerStatusPort);
                _statusListener.Request += async (sender, context) =>
                {
                    try
                    {
                        if(_statusQueries.Count > 10) return;
                        _statusQueries.Enqueue(DateTime.Now);
                       // var request = context.Request;
                        var response = context.Response;
                        //OK, NO_ESI, NO_CONNECTION, NO_DISCORD
                        var value = "OK";
                        if (TickManager.IsESIUnreachable)
                            value = "NO_ESI";
                        if (TickManager.IsNoConnection)
                            value = "NO_CONNECTION";
                        if (!APIHelper.DiscordAPI.IsAvailable)
                        {
                            if(Settings.WebServerModule.NoStatusResponseOnDiscordDisconnection)
                                return;
                            value = "NO_DISCORD";
                        }

                        await response.WriteContentAsync(value);
                    }                   
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx(ex.Message, ex, Category);
                    }
                    finally
                    {
                        try
                        {
                            context.Response.Close();
                        }
                        catch
                        {
                            //ignore
                        }
                    }
                };
                _statusListener.Start();
            }

            if (_listener == null || !_listener.IsListening)
            {
                await LogHelper.LogInfo("Starting Web Server", Category);
                try
                {
                    _listener?.Dispose();
                }
                catch
                {
                    // ignored
                }

                //TODO cleanup all occurences in modules in some of the following releases
                var extPort = Settings.WebServerModule.WebExternalPort;
                var ip = "0.0.0.0";
                _listener = new System.Net.Http.HttpListener(IPAddress.Parse(ip), extPort);
                _listener.Request += async (sender, context) =>
                {
                    if(Program.IsClosing) return;
                    try
                    {
                        RunningRequestCount++;
                        var request = context.Request;
                        var response = context.Response;

                        if (request.Url.LocalPath.EndsWith(".js") || request.Url.LocalPath.EndsWith(".less") || request.Url.LocalPath.EndsWith(".css"))
                        {
                            var path = Path.Combine(SettingsManager.RootDirectory, "Content", "scripts", Path.GetFileName(request.Url.LocalPath));
                            if (request.Url.LocalPath.Contains("moments"))
                            {
                                path = Path.Combine(SettingsManager.RootDirectory, "Content", "scripts", "moments", Path.GetFileName(request.Url.LocalPath));
                            }

                            if (!File.Exists(path))
                            {
                                return;
                            }

                            if (request.Url.LocalPath.EndsWith(".less") || request.Url.LocalPath.EndsWith(".css"))
                            {
                                response.Headers.ContentType.Add("text/css");
                            }

                            if (request.Url.LocalPath.EndsWith(".js"))
                            {
                                response.Headers.ContentType.Add("text/javascript");
                            }

                            await response.WriteContentAsync(File.ReadAllText(path));

                            return;
                        }

                        if (request.Url.LocalPath == "/favicon.ico")
                        {
                            var path = Path.Combine(SettingsManager.RootDirectory, Path.GetFileName(request.Url.LocalPath));
                            if (!File.Exists(path))
                            {
                                return;
                            }

                            await response.WriteContentAsync(File.ReadAllText(path));
                            return;
                        }

                        if (request.Url.LocalPath == "/" || request.Url.LocalPath == $"{extPort}/")
                        {
                            // var extIp = Settings.WebServerModule.WebExternalIP;

                            var text = File.ReadAllText(SettingsManager.FileTemplateMain)
                                .Replace("{headerContent}", GetHtmlResourceDefault(false))
                                .Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{webWelcomeHeader}", LM.Get("webWelcomeHeader"))
                                .Replace("{authButtonText}", LM.Get("butGeneralAuthPage"));

                            //managecontrols
                            var manageText = new StringBuilder();
                            //timers
                            if (Settings.Config.ModuleTimers)
                            {
                                var authNurl = GetTimersURL();
                                manageText.Append($"\n<a href=\"{authNurl}\" class=\"btn btn-info btn-block\" role=\"button\">{LM.Get("authButtonTimersText")}</a>");
                            }

                            if (Settings.Config.ModuleHRM)
                            {
                                var authNurl = GetHRMAuthURL();
                                manageText.Append($"\n<a href=\"{authNurl}\" class=\"btn btn-info btn-block\" role=\"button\">{LM.Get("authButtonHRMText")}</a>");
                            }

                            if (Settings.Config.ModuleWebConfigEditor)
                            {
                                var authNurl = GetWebConfigAuthURL();
                                manageText.Append($"\n<a href=\"{authNurl}\" class=\"btn btn-info btn-block\" role=\"button\">{LM.Get("buttonSettingsText")}</a>");
                            }

                            text = text.Replace("{manageControls}", manageText.ToString());
                            await WriteResponce(text, response);

                            return;
                        }

                        if (request.Url.LocalPath == "/authPage.html" || request.Url.LocalPath == $"{extPort}/authPage.html")
                        {
                            var extIp = Settings.WebServerModule.WebExternalIP;
                            var authUrl = $"{GetWebSiteUrl()}/auth";

                            var text = File.ReadAllText(SettingsManager.FileTemplateAuthPage)
                                .Replace("{headerContent}", GetHtmlResourceDefault(false))
                                .Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{backText}", LM.Get("backText"));

                            //auth controls
                            var authText = new StringBuilder();

                            var grps = Settings.WebAuthModule.GetEnabledAuthGroups();
                            if (Settings.Config.ModuleAuthWeb && grps.Count > 0)
                            {
                                authText.Append($"<h2>{LM.Get("authWebDiscordHeader")}</h4>{LM.Get("authPageGeneralAuthHeader")}");
                            }

                            var groupsForCycle = Settings.WebAuthModule.UseOneAuthButton
                                ? grps.Where(a => a.Value.ExcludeFromOneButtonMode || a.Value.BindToMainCharacter)
                                : grps;

                            if (Settings.WebAuthModule.UseOneAuthButton)
                            {
                                if (Settings.Config.ModuleAuthWeb)
                                {
                                    var url = $"{authUrl}?group={HttpUtility.UrlEncode(WebAuthModule.DEF_NOGROUP_NAME)}";
                                    authText.Append($"\n<a href=\"{url}\" class=\"btn btn-info btn-block\" role=\"button\">{LM.Get("authButtonDiscordText")}</a>");
                                }
                            }

                            //stands auth
                            if (Settings.Config.ModuleAuthWeb)
                            {
                                foreach (var groupPair in groupsForCycle.Where(a => a.Value.StandingsAuth != null))
                                {
                                    var group = groupPair.Value;
                                    if(group.Hidden) continue;
                                    var url = $"{authUrl}?group={HttpUtility.UrlEncode(groupPair.Key)}";
                                    authText.Append($"\n<a href=\"{url}\" class=\"btn btn-info btn-block\" role=\"button\">{group.CustomButtonText}</a>");
                                }
                            }

                            //auth
                            if (Settings.Config.ModuleAuthWeb)
                            {
                                foreach (var @group in groupsForCycle.Where(a => a.Value.StandingsAuth == null))
                                {
                                    if(group.Value.Hidden) continue;
                                    var url = $"{authUrl}?group={HttpUtility.UrlEncode(group.Value.BindToMainCharacter ? WebAuthModule.DEF_ALTREGGROUP_NAME : group.Key)}";
                                    var bText = group.Value.CustomButtonText ?? $"{LM.Get("authButtonDiscordText")} - {group.Key}";
                                    authText.Append($"<a href=\"{url}\" class=\"btn btn-info btn-block\" role=\"button\">{bText}</a>");
                                }
                            }


                            var len = authText.Length;
                            var smth = false;

                            //stands auth
                            if (Settings.Config.ModuleAuthWeb)
                            {
                                foreach (var groupPair in grps.Where(a => a.Value.StandingsAuth != null))
                                {
                                    var group = groupPair.Value;
                                    if(group.Hidden) continue;
                                    var url = GetStandsAuthURL();
                                    authText.Append($"\n<a href=\"{url}\" class=\"btn btn-info btn-block\" role=\"button\">{group.StandingsAuth.WebAdminButtonText}</a>");
                                }
                                smth = true;
                            }

                            //notifications
                            if (Settings.Config.ModuleNotificationFeed)
                            {
                                var authNurl = GetAuthNotifyURL();
                                authText.Append($"\n<a href=\"{authNurl}\" class=\"btn btn-info btn-block\" role=\"button\">{LM.Get("authButtonNotifyText")}</a>");
                                smth = true;
                            }
                            //mail
                            if (Settings.Config.ModuleMail)
                            {
                                var authNurl = GetMailAuthURL();
                                authText.Append($"\n<a href=\"{authNurl}\" class=\"btn btn-info btn-block\" role=\"button\">{LM.Get("authButtonMailText")}</a>");
                                smth = true;
                            }
                            if (Settings.Config.ModuleContractNotifications)
                            {
                                foreach (var notifyGroup in Settings.ContractNotificationsModule.GetEnabledGroups())
                                {
                                    var group = notifyGroup.Value;
                                    var authNurl = GetContractsAuthURL(group.FeedPersonalContracts, group.FeedCorporateContracts, notifyGroup.Key);
                                    authText.Append($"\n<a href=\"{authNurl}\" class=\"btn btn-info btn-block\" role=\"button\">{group.ButtonText}</a>");

                                }
                                smth = true;
                            }

                            if (Settings.Config.ModuleIndustrialJobs)
                            {
                                foreach (var (key, group) in Settings.IndustrialJobsModule.GetEnabledGroups())
                                {
                                    var authNurl = GetIndustryJobsAuthURL(group.Filters.Any(a=> a.Value.FeedPersonalJobs), group.Filters.Any(a=> a.Value.FeedCorporateJobs), key);
                                    authText.Append($"\n<a href=\"{authNurl}\" class=\"btn btn-info btn-block\" role=\"button\">{group.ButtonText ?? key}</a>");

                                }
                                smth = true;
                            }

                            if (smth)
                            {
                                authText.Insert(len, $"<h2>{LM.Get("authWebSystemHeader")}</h2>{LM.Get("authPageSystemAuthHeader")}");
                            }

                            text = text.Replace("{authControls}", authText.ToString()).Replace("{authHeaderText}", LM.Get("authPageHeaderText"));
                            await WriteResponce(text, response);
                            return;
                        }

                        var result = false;

                        foreach (var method in ModuleConnectors.Values)
                        {
                            try
                            {
                                result = await method(context);
                                if (result)
                                {
                                    break;
                                }
                            }
                            catch (Exception ex)
                            {
                                await LogHelper.LogEx($"Module method {method.Method.Name} throws ex!", ex, Category);
                            }
                        }

                        if (!result)
                        {
                            await WriteResponce(Get404Page(), response);
                        }
                    }
                    catch (Exception ex)
                    {
                        await LogHelper.LogEx(ex.Message, ex, Category);
                    }
                    finally
                    {
                        RunningRequestCount--;
                        try
                        {
                            context.Response.Close();
                        }
                        catch
                        {
                            //ignore
                        }
                    }
                };
                _listener.Start();
            }
        }

        internal static string Get404Page()
        {
            return File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", "404 Not Found!")
                .Replace("{headerContent}", GetHtmlResourceDefault(false))
                .Replace("{header}", LM.Get("authTemplateHeader"))
                .Replace("{body}", LM.Get("WebRequestUnexpected"))
                .Replace("{backUrl}", GetWebSiteUrl())
                .Replace("{backText}", LM.Get("backText"));
        }


        internal static string GetAccessDeniedPage(string header, string message, string backUrl, string description = null)
        {
            return File.ReadAllText(SettingsManager.FileTemplateAuth3)
                .Replace("{headerContent}", GetHtmlResourceDefault(false))
                .Replace("{message}", message)
                .Replace("{header}", header)
                .Replace("{header2}", header)
                .Replace("{description}", description)
                .Replace("{body}", "")
                .Replace("{backText}", LM.Get("backText"))
                .Replace("{backUrl}", backUrl);
        }

        public static async Task WriteResponce(string message, System.Net.Http.HttpListenerResponse response, bool noCache = false)
        {
            response.Headers.ContentEncoding.Add("utf-8");
            response.Headers.ContentType.Add("text/html;charset=utf-8");
            if (noCache)
            {
                response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                response.Headers.Add("Pragma", "no-cache"); // HTTP 1.0.
                response.Headers.Add("Expires", "0"); // Proxies.
            }
            await response.WriteContentAsync(message);
        }

        public static async Task WriteJsonResponse(string message, System.Net.Http.HttpListenerResponse response, bool noCache = false)
        {
            response.Headers.ContentEncoding.Add("utf-8");
            response.Headers.ContentType.Add("application/json");
            if (noCache)
            {
                response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate");
                response.Headers.Add("Pragma", "no-cache"); // HTTP 1.0.
                response.Headers.Add("Expires", "0"); // Proxies.
            }
            await response.WriteContentAsync(message);
        }



        public static string GetWebSiteUrl()
        {
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var usePort = SettingsManager.Settings.WebServerModule.UsePortInUrl;

            return usePort ? $"{HttpPrefix}://{extIp}:{extPort}" : $"{HttpPrefix}://{extIp}";
        }
        private static string GetCallBackUrl()
        {
            var callbackurl = $"{GetWebSiteUrl()}/callback";
            return callbackurl;
        }
        public static string GetWebConfigAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=settings";
        }

        public static string GetAuthNotifyURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize/?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope=esi-characters.read_notifications.v1+esi-universe.read_structures.v1&state=9";
        }

        public static string GetStandsAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            var permissions = new[]
            {
                "esi-alliances.read_contacts.v1",
                "esi-characters.read_contacts.v1",
                "esi-corporations.read_contacts.v1"
            };
            var pString = string.Join('+', permissions);

            return $"https://login.eveonline.com/oauth/authorize/?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope={pString}&state=authst";
        }

        public static string GetTimersAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=11";
        }

        public static string GetOpenContractURL(long contractId)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=opencontract{contractId}&scope=esi-ui.open_window.v1";
        }

        public static string GetMailAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope=esi-mail.read_mail.v1+esi-mail.send_mail.v1+esi-mail.organize_mail.v1&state=12";
        }

        internal static string GetAuthPageUrl()
        {
            var text = !string.IsNullOrEmpty(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup) && SettingsManager.Settings.WebAuthModule.GetEnabledAuthGroups().Keys.Contains(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup)
                ? $"auth?group={HttpUtility.UrlEncode(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup)}"
                : null;
            return $"{GetWebSiteUrl()}/{text}";
        }

        public static string GetAuthLobbyUrl()
        {
            return $"{GetWebSiteUrl()}/authPage.html";
        }


        internal static string GetAuthUrl(string ip, string groupName = null, long mainCharacterId = 0)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            var grp = string.IsNullOrEmpty(groupName) ? null : $"&state=x{HttpUtility.UrlEncode(groupName)}";
            var mc = mainCharacterId == 0 ? null : $"|{mainCharacterId}";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&amp;redirect_uri={callbackurl}&amp;client_id={clientID}{grp}{mc}|{ip}";
        }

        internal static string GetAuthUrlOneButton(string ip)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=oneButton|{ip}";
        }
        internal static string GetAuthUrlAltRegButton(string ip)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=altReg|{ip}";
        }

        internal static string GetCustomAuthUrl(string ip, List<string> permissions, string group = null, long mainCharacterId = 0)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();

            var grp = string.IsNullOrEmpty(group) ? null : $"&state=x{HttpUtility.UrlEncode(group)}";
            var mc = mainCharacterId == 0 ? null : $"|{mainCharacterId}";

            var pString = string.Join('+', permissions);
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope={pString}{grp}{mc}|{ip}";
        }


        public static string GetTimersURL()
        {
            return $"{GetWebSiteUrl()}/timers";
        }




        public static string GetHRMAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=matahari";
        }

        public static string GetContractsAuthURL(bool readChar, bool readCorp, string groupName)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            var list = new List<string>();
            if (readChar)
            {
                list.Add("esi-contracts.read_character_contracts.v1");
            }

            if (readCorp)
            {
                list.Add("esi-contracts.read_corporation_contracts.v1");
            }

            list.Add("esi-universe.read_structures.v1");
            var pString = string.Join('+', list);
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope={pString}&state=cauth{HttpUtility.UrlEncode(groupName)}";
        }

        public static string GetIndustryJobsAuthURL(bool readChar, bool readCorp, string groupName)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            var list = new List<string>();
            if (readChar)
            {
                list.Add("esi-industry.read_character_jobs.v1");
            }

            if (readCorp)
            {
                list.Add("esi-industry.read_corporation_jobs.v1");
            }

            list.Add("esi-universe.read_structures.v1");
            var pString = string.Join('+', list);
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope={pString}&state=ijobsauth{HttpUtility.UrlEncode(groupName)}";
        }

        public static string GetHRMInspectURL(long id, string authCode)
        {
            return $"{GetWebSiteUrl()}/hrm?data=inspect{id}&id={authCode}&state=matahari";
        }

        public static string GetHRMMainURL(string authCode)
        {
            return $"{GetWebSiteUrl()}/hrm?data=0&id={authCode}&state=matahari";
        }

        public static string GetHRM_AjaxMailURL(long mailId, long inspectCharId, string authCode)
        {
            return $"hrm?data=mail{mailId}_{inspectCharId}&id={authCode}&state=matahari";
        }


        public static string GetHRM_AjaxMailListURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=maillist{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxTransactListURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=transactlist{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxJournalListURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=journallist{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public void Dispose()
        {
            _statusTimer?.Stop();
            ModuleConnectors.Clear();
        }

        public static string GetHRM_AjaxLysListURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=lys{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxContractsListURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=contracts{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxContactsListURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=contacts{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxSkillsListURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=skills{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_SearchMailURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=searchMail{inspectCharId}&id={authCode}&state=matahari&query=";

        }

        public static string GetHRM_DeleteCharAuthURL(long inspectCharId, string authCode)
        {
            return $"hrm?data=deleteAuth{inspectCharId}&id={authCode}&state=matahari";
        }

        public static string GetHRM_AjaxMembersURL(long value, string authCode)
        {
            return $"hrm?data=loadMembers&id={authCode}&state=matahari&memberType={value}&filter=";

        }

        public static string GetHRM_MoveToSpiesURL(long itemCharacterId, string authCode)
        {
            return $"hrm?data=moveToSpies{itemCharacterId}&id={authCode}&state=matahari";
        }

        public static string GetHRM_RestoreDumpedURL(long itemCharacterId, string authCode)
        {
            return $"hrm?data=restoreAuth{itemCharacterId}&id={authCode}&state=matahari";
        }

        public static string GetWebEditorUrl(string code)
        {
            return $"{GetWebSiteUrl()}/settings?code={code}&state=settings";
        }

        public static string GetWebEditorSimplifiedAuthUrl(string code)
        {
            return $"{GetWebSiteUrl()}/settings?code={code}&state=settings_sa&data=";
        }


        public static string GetWebEditorTimersUrl(string code)
        {
            return $"{GetWebSiteUrl()}/settings?code={code}&state=settings_ti&data=";
        }


        #region new web
        public static Dictionary<string, Func<string, CallbackTypeEnum, string, Task<WebQueryResult>>> WebModuleConnectors { get; } = new Dictionary<string, Func<string, CallbackTypeEnum, string, Task<WebQueryResult>>>();

        /// <summary>
        /// Iterate between connected handlers to process request
        /// </summary>
        public static async Task<WebQueryResult> ProcessWebCallbacks(string query, CallbackTypeEnum type, string ip)
        {
            foreach (var method in WebModuleConnectors.Values)
            {
                try
                {
                    var result = await method(query, type, ip);
                    if (result.Result != WebQueryResultEnum.False)
                        return result;
                }
                catch (Exception ex)
                {
                    await LogHelper.LogEx($"Module method {method.Method.Name} throws ex!", ex);
                }
            }

            return WebQueryResult.False;
        }

        

        #endregion
    }
}
