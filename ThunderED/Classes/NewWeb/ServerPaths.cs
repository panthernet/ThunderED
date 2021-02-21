using System.Collections.Generic;
using System.Web;
using ThunderED.Modules;

namespace ThunderED.Classes
{
    public static class ServerPaths
    {
        public static string HttpPrefix => SettingsManager.Settings.WebServerModule.UseHTTPS ? "https" : "http";

        #region Simple Urls

        public static string GetGeneralAuthPageUrl()
        {
            return "/authpage";
        }

        public static string GetFeedAuthPageUrl()
        {
            return "/feedauthpage";
        }

        public static string GetHrmPageUrl()
        {
            return "/hrm";
        }
        public static string GetWebSettingsPageUrl()
        {
            return "/settings";
        }

        public static string GetTimersPageUrl()
        {
            return "/timers";
        }

        public static string GetMiningSchedulePageUrl()
        {
            return "/ms";
        }

        public static string GetAuthUrl()
        {
            return "/auth";
        }

        public static string GetUserAuthCallbackUrl()
        {
            return "/auth";
        }

        public static string GetFeedSuccessUrl()
        {
            return "/feedsuccess";
        }

        public static string GetAuthSuccessUrl()
        {
            return "/authsuccess";
        }


        public static string GetBadRequestUrl()
        {
            return "/badrq";
        }

        #endregion


        public static string GetOneButtonUrl()
        {
            return $"/auth?group={HttpUtility.UrlEncode(WebAuthModule.DEF_NOGROUP_NAME)}";
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

        #region Auth URLs

        public static string GetUserAuthUrl()
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientId}&state=userauth";
        }

        internal static string GetAuthUrlOneButton(string ip)
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientId}&state=oneButton|{ip}";
        }
        public static string GetAuthUrlAltRegButton(string ip)
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientId}&state=altReg|{ip}";
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

        public static string GetAuthNotifyURL()
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize/?response_type=code&redirect_uri={callbackurl}&client_id={clientId}&scope=esi-characters.read_notifications.v1+esi-universe.read_structures.v1&state=9";
        }

        public static string GetMailAuthURL()
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientId}&scope=esi-mail.read_mail.v1+esi-mail.send_mail.v1+esi-mail.organize_mail.v1&state=12";
        }

        public static string GetContractsAuthURL(bool readChar, bool readCorp, string groupName)
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
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
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientId}&scope={pString}&state=cauth{HttpUtility.UrlEncode(groupName)}";
        }

        public static string GetIndustryJobsAuthURL(bool readChar, bool readCorp, string groupName)
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
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
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientId}&scope={pString}&state=ijobsauth{HttpUtility.UrlEncode(groupName)}";
        }

        internal static string GetCustomAuthUrl(string ip, List<string> permissions, string group = null, long mainCharacterId = 0)
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();

            var grp = string.IsNullOrEmpty(group) ? null : $"&state=x{HttpUtility.UrlEncode(group)}";
            var mc = mainCharacterId == 0 ? null : $"|{mainCharacterId}";

            var pString = string.Join('+', permissions);
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientId}&scope={pString}{grp}{mc}|{ip}";
        }

        internal static string GetAuthUrl(string ip, string groupName = null, long mainCharacterId = 0)
        {
            var clientId = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var callbackurl = GetCallBackUrl();
            var grp = string.IsNullOrEmpty(groupName) ? null : $"&state=x{HttpUtility.UrlEncode(groupName)}";
            var mc = mainCharacterId == 0 ? null : $"|{mainCharacterId}";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&amp;redirect_uri={callbackurl}&amp;client_id={clientId}{grp}{mc}|{ip}";
        }
        #endregion


    }
}
