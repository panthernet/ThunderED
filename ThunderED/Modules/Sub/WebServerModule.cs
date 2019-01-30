using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Modules.OnDemand;

namespace ThunderED.Modules.Sub
{
    public class WebServerModule: AppModuleBase, IDisposable
    {
        private static System.Net.Http.HttpListener _listener;
        public override LogCat Category => LogCat.WebServer;

        public static string HttpPrefix => SettingsManager.Settings.Config.UseHTTPS ? "https" : "http";

        public static Dictionary<string, Func<HttpListenerRequestEventArgs, Task<bool>>> ModuleConnectors { get; } = new Dictionary<string, Func<HttpListenerRequestEventArgs, Task<bool>>>();

        public WebServerModule()
        {
            LogHelper.LogModule("Inititalizing WebServer module...", Category).GetAwaiter().GetResult();
            ModuleConnectors.Clear();
        }

        public override async Task Run(object prm)
        {
            if(!Settings.Config.ModuleWebServer) return;

            if (_listener == null || !_listener.IsListening)
            {
                await LogHelper.LogInfo("Starting Web Server", Category);
                _listener?.Dispose();
                var port = Settings.WebServerModule.WebListenPort;
                var extPort = Settings.WebServerModule.WebExternalPort;
                var ip = Settings.WebServerModule.WebListenIP;


                _listener = new System.Net.Http.HttpListener(IPAddress.Parse(ip), port);
                _listener.Request += async (sender, context) =>
                {
                    try
                    {
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
                                return;
                            if (request.Url.LocalPath.EndsWith(".less") || request.Url.LocalPath.EndsWith(".css"))
                                response.Headers.ContentType.Add("text/css");
                            if (request.Url.LocalPath.EndsWith(".js"))
                                response.Headers.ContentType.Add("text/javascript");

                            await response.WriteContentAsync(File.ReadAllText(path));

                            return;
                        }

                        if (request.Url.LocalPath == "/favicon.ico")
                        {
                            var path = Path.Combine(SettingsManager.RootDirectory, Path.GetFileName(request.Url.LocalPath));
                            if (!File.Exists(path))
                                return;
                            await response.WriteContentAsync(File.ReadAllText(path));
                            return;
                        }

                        if (request.Url.LocalPath == "/" || request.Url.LocalPath == $"{port}/" || request.Url.LocalPath == $"{extPort}/")
                        {
                           // var extIp = Settings.WebServerModule.WebExternalIP;

                            var text = File.ReadAllText(SettingsManager.FileTemplateMain)
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

                            text = text.Replace("{manageControls}", manageText.ToString());
                            await WriteResponce(text, response);

                            return;
                        }

                        if (request.Url.LocalPath == "/authPage.html" || request.Url.LocalPath == $"{port}/authPage.html" || request.Url.LocalPath == $"{extPort}/authPage.html")
                        {
                            var extIp = Settings.WebServerModule.WebExternalIP;
                            var authUrl = $"{HttpPrefix}://{extIp}:{extPort}/auth.php";

                            var text = File.ReadAllText(SettingsManager.FileTemplateAuthPage)
                                .Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{backText}", LM.Get("backText"));

                            //auth controls
                            var authText = new StringBuilder();

                            //stands auth
                            if (Settings.Config.ModuleAuthWeb)
                            {
                                if (SettingsManager.Settings.WebAuthModule.AuthGroups.Count > 0)
                                    authText.Append($"<h2>{LM.Get("authWebDiscordHeader")}</h4>{LM.Get("authPageGeneralAuthHeader")}");

                                foreach (var groupPair in Settings.WebAuthModule.AuthGroups.Where(a=> a.Value.StandingsAuth != null))
                                {
                                    var group = groupPair.Value;
                                    var url = $"{authUrl}?group={HttpUtility.UrlEncode(groupPair.Key)}";
                                    authText.Append($"\n<a href=\"{url}\" class=\"btn btn-info btn-block\" role=\"button\">{group.CustomButtonText}</a>");                                    
                                }
                            }

                            //auth
                            if (Settings.Config.ModuleAuthWeb)
                            {
                                foreach (var @group in SettingsManager.Settings.WebAuthModule.AuthGroups.Where(a=> a.Value.StandingsAuth == null))
                                {
                                    if (group.Value.ESICustomAuthRoles.Any())
                                    {
                                        //var customAuthString = GetCustomAuthUrl(group.Value.ESICustomAuthRoles, group.Key);
                                        var url = $"{authUrl}?group={HttpUtility.UrlEncode(group.Key)}";
                                        var bText = group.Value.CustomButtonText ?? $"{LM.Get("authButtonDiscordText")} - {group.Key}";
                                        authText.Append($"\n<a href=\"{url}\" class=\"btn btn-info btn-block\" role=\"button\">{bText}</a>");
                                    }
                                    else
                                    {
                                        var url = $"{authUrl}?group={HttpUtility.UrlEncode(group.Key)}";
                                        var bText = group.Value.CustomButtonText ?? $"{LM.Get("authButtonDiscordText")} - {group.Key}";
                                        authText.Append($"<a href=\"{url}\" class=\"btn btn-info btn-block\" role=\"button\">{bText}</a>");
                                    }
                                }
                            }

                            var len = authText.Length;
                            bool smth = false;

                            //stands auth
                            if (Settings.Config.ModuleAuthWeb)
                            {
                                foreach (var groupPair in Settings.WebAuthModule.AuthGroups.Where(a=> a.Value.StandingsAuth != null))
                                {
                                    var group = groupPair.Value;
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
                                foreach (var notifyGroup in Settings.ContractNotificationsModule.Groups)
                                {
                                    var group = notifyGroup.Value;
                                    var authNurl = GetContractsAuthURL(group.FeedPersonalContracts, group.FeedCorporateContracts, notifyGroup.Key);
                                    authText.Append($"\n<a href=\"{authNurl}\" class=\"btn btn-info btn-block\" role=\"button\">{group.ButtonText}</a>");
                                    
                                }
                                smth = true;
                            }

                            if (smth)
                                authText.Insert(len, $"<h2>{LM.Get("authWebSystemHeader")}</h2>{LM.Get("authPageSystemAuthHeader")}");

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
                                    break;
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
                .Replace("{header}", LM.Get("authTemplateHeader"))
                .Replace("{body}", LM.Get("WebRequestUnexpected"))
                .Replace("{backText}", LM.Get("backText"));
        }


        internal static string GetAccessDeniedPage(string header, string message, string description = null)
        {
            return File.ReadAllText(SettingsManager.FileTemplateAuth3)
                .Replace("{message}", message)
                .Replace("{header}", header)
                .Replace("{header2}", header)
                .Replace("{description}", description)
                .Replace("{body}", "")
                .Replace("{backText}", LM.Get("backText"));
        }

        public static async Task WriteResponce(string message, System.Net.Http.HttpListenerResponse response)
        {
            response.Headers.ContentEncoding.Add("utf-8");
            response.Headers.ContentType.Add("text/html;charset=utf-8");
            await response.WriteContentAsync(message);
        }

        public static string GetWebSiteUrl()
        {
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            return  $"{HttpPrefix}://{extIp}:{extPort}";
        }

        
        public static string GetAuthNotifyURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize/?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope=esi-characters.read_notifications.v1+esi-universe.read_structures.v1&state=9";
        }

        public static string GetStandsAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";
            var permissions = new []
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
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=11";
        }

        public static string GetOpenContractURL(long contractId)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=opencontract{contractId}&scope=esi-ui.open_window.v1";
        }

        public static string GetMailAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope=esi-mail.read_mail.v1+esi-mail.send_mail.v1+esi-mail.organize_mail.v1&state=12";
        }

        internal static string GetAuthPageUrl()
        {
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var text = !string.IsNullOrEmpty(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup) && SettingsManager.Settings.WebAuthModule.AuthGroups.Keys.Contains(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup)
                ? $"auth.php?group={HttpUtility.UrlEncode(SettingsManager.Settings.WebAuthModule.DefaultAuthGroup)}"
                : null;
            return $"{HttpPrefix}://{extIp}:{extPort}/{text}";
        }

        internal static string GetAuthUrl()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&amp;redirect_uri={callbackurl}&amp;client_id={clientID}";
        }

        internal static string GetCustomAuthUrl(List<string> permissions, string group = null)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";

            var grp = string.IsNullOrEmpty(group) ? null : $"&state=x{HttpUtility.UrlEncode(group)}";

            var pString = string.Join('+', permissions);
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope={pString}{grp}";
        }


        public static string GetTimersURL()
        {
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            return $"{HttpPrefix}://{extIp}:{extPort}/timers.php";
        }



        
        public static string GetHRMAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=matahari";
        }

        public static string GetContractsAuthURL(bool readChar, bool readCorp, string groupName)
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"{HttpPrefix}://{extIp}:{extPort}/callback.php";
            var list = new List<string>();
            if(readChar)
                list.Add("esi-contracts.read_character_contracts.v1");
            if(readCorp)
                list.Add("esi-contracts.read_corporation_contracts.v1");
            var pString = string.Join('+', list);
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope={pString}&state=cauth{HttpUtility.UrlEncode(groupName)}";
        }

        public static string GetHRMInspectURL(long id, string authCode)
        {
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            return $"{HttpPrefix}://{extIp}:{extPort}/hrm.php?data=inspect{id}&id={authCode}&state=matahari";
        }

        public static string GetHRMMainURL(string authCode)
        {
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            return $"{HttpPrefix}://{extIp}:{extPort}/hrm.php?data=0&id={authCode}&state=matahari";
        }

        public static string GetHRM_AjaxMailURL(long mailId, long inspectCharId, string authCode)
        {
            return $"hrm.php?data=mail{mailId}_{inspectCharId}&id={authCode}&state=matahari";
        }

        
        public static string GetHRM_AjaxMailListURL(long inspectCharId, string authCode)
        {
            return $"hrm.php?data=maillist{inspectCharId}&id={authCode}&state=matahari&page=";
        }
        
        public static string GetHRM_AjaxTransactListURL(long inspectCharId, string authCode)
        {
            return $"hrm.php?data=transactlist{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxJournalListURL(long inspectCharId, string authCode)
        {
            return $"hrm.php?data=journallist{inspectCharId}&id={authCode}&state=matahari&page=";
        }
        
        public void Dispose()
        {
            ModuleConnectors.Clear();
        }

        public static string GetHRM_AjaxLysListURL(long inspectCharId, string authCode)
        {
            return $"hrm.php?data=lys{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxContractsListURL(long inspectCharId, string authCode)
        {
            return $"hrm.php?data=contracts{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxContactsListURL(long inspectCharId, string authCode)
        {
            return $"hrm.php?data=contacts{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_AjaxSkillsListURL(long inspectCharId, string authCode)
        {
            return $"hrm.php?data=skills{inspectCharId}&id={authCode}&state=matahari&page=";
        }

        public static string GetHRM_SearchMailURL(long inspectCharId, string authCode)
        {
            return $"hrm.php?data=searchMail{inspectCharId}&id={authCode}&state=matahari&query=";

        }

        public static string GetHRM_DeleteCharAuthURL(int inspectCharId, string authCode)
        {
            return $"hrm.php?data=deleteAuth{inspectCharId}&id={authCode}&state=matahari";
        }
    }
}
