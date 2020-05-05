using System;
using System.Collections.Generic;
using ThunderED.Classes.Enums;

namespace ThunderED.Classes
{
    public class WebQueryResult
    {
        public static WebQueryResult GeneralAuthSuccess => new WebQueryResult(WebQueryResultEnum.GeneralAuthSuccess, "/authsuccess", ServerPaths.GetGeneralAuthPageUrl());
        public static WebQueryResult False => new WebQueryResult(WebQueryResultEnum.False);
        public static WebQueryResult EsiFailure => new WebQueryResult(WebQueryResultEnum.EsiFailure, "/badrq") { Message1 = LM.Get("ESIFailure") };
        public static WebQueryResult BadRequestToRoot=> new WebQueryResult(WebQueryResultEnum.BadRequest, "/badrq", "/");
        public static WebQueryResult BadRequestToGeneralAuth => new WebQueryResult(WebQueryResultEnum.BadRequest, "/badrq", ServerPaths.GetGeneralAuthPageUrl());
        public static WebQueryResult BadRequestToSystemAuth => new WebQueryResult(WebQueryResultEnum.BadRequest, "/badrq", ServerPaths.GetFeedAuthPageUrl());
        public static WebQueryResult RedirectUrl => new WebQueryResult(WebQueryResultEnum.RedirectUrl);

        public string Message1 { get; set; }
        public string Message2 { get; set; }
        public string Message3 { get; set; }

        public bool HasMessage => !string.IsNullOrEmpty(Message1) || !string.IsNullOrEmpty(Message2) || !string.IsNullOrEmpty(Message3);

        public WebQueryResultEnum Result;
        public Dictionary<string, object> Values = new Dictionary<string, object>();

        public bool ForceRedirect { get; }

        public WebQueryResult(WebQueryResultEnum result, string url = null, string returnUrl = null, bool force = false)
        {
            ForceRedirect = force;
            Result = result;
            if (url != null)
                AddUrl(url);
            if (returnUrl != null)
                AddReturnUrl(returnUrl);
        }

        public T GetValue<T>(string name)
        {
            return !Values.ContainsKey(name)
                ? default
                : Values[name] switch
                {
                    int s => (T)(object)s,
                    double s => (T)(object)s,
                    string s => (T)(object)s,
                    long s => (T)(object)s,
                    bool s => (T)(object)s,
                    _ => default
                };
        }

        public void AddValue(string name, object value)
        {
            Values.Add(name, value);
        }

        public void AddUrl(string url)
        {
            Values.Add("url", url);
        }

        public string GetUrl()
        {
            return Values.ContainsKey("url") ? (string)Values["url"] : null;
        }

        public void AddReturnUrl(string url)
        {
            Values.Add("returnUrl", url);
        }

        public string GetReturnUrl()
        {
            return Values.ContainsKey("returnUrl") ? (string)Values["returnUrl"] : null;
        }
    }
}
