using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules.Sub
{
    public class WebServerModule: AppModuleBase, IDisposable
    {
        private static System.Net.Http.HttpListener _listener;
        public override LogCat Category => LogCat.WebServer;

        public static Dictionary<string, Func<HttpListenerRequestEventArgs, Task<bool>>> ModuleConnectors { get; } = new Dictionary<string, Func<HttpListenerRequestEventArgs, Task<bool>>>();

        public WebServerModule()
        {
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

                        if (request.Url.LocalPath == "/" || request.Url.LocalPath == $"{port}/" || request.Url.LocalPath == $"{extPort}/")
                        {
                            var extIp = Settings.WebServerModule.WebExternalIP;
                            var authUrl = $"http://{extIp}:{extPort}/auth.php";
                            var authNurl = GetAuthNotifyURL();

                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                            var text = File.ReadAllText(SettingsManager.FileTemplateMain).Replace("{authUrl}", authUrl)
                                    .Replace("{authNotifyUrl}", authNurl).Replace("{header}", LM.Get("authTemplateHeader"))
                                    .Replace("{timersUrl}", GetTimersURL())
                                    .Replace("{authButtonDiscordText}", LM.Get("authButtonDiscordText"))
                                    .Replace("{authButtonNotifyText}", LM.Get("authButtonNotifyText"))
                                    .Replace("{authButtonTimersText}", LM.Get("authButtonTimersText"))
                                    .Replace("{authMailUrl}", GetMailAuthURL())
                                    .Replace("{authButtonMailText}", LM.Get("authButtonMailText"))
                                    .Replace("{webAuthHeader}", LM.Get("webAuthHeader"))
                                    .Replace("{webWelcomeHeader}", LM.Get("webWelcomeHeader"))
                                ;
                            text = text.Replace("{disableWebAuth}", !Settings.Config.ModuleAuthWeb ? "disabled" : "");
                            text = text.Replace("{disableWebNotify}", !Settings.Config.ModuleNotificationFeed ? "disabled" : "");
                            text = text.Replace("{disableWebTimers}", !Settings.Config.ModuleTimers ? "disabled" : "");
                            text = text.Replace("{disableMailNotify}", !Settings.Config.ModuleMail ? "disabled" : "");

                            await response.WriteContentAsync(text);
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
                            response.Headers.ContentEncoding.Add("utf-8");
                            response.Headers.ContentType.Add("text/html;charset=utf-8");
                            await response.WriteContentAsync(File.ReadAllText(SettingsManager.FileTemplateAuth3).Replace("{message}", "404 Not Found!")
                                .Replace("{header}", LM.Get("authTemplateHeader"))
                                .Replace("{body}", LM.Get("WebRequestUnexpected"))
                            );
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

        public static string GetWebSiteUrl()
        {
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            return  $"http://{extIp}:{extPort}";
        }

        
        public static string GetAuthNotifyURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"http://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize/?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope=esi-characters.read_notifications.v1+esi-universe.read_structures.v1+esi-characters.read_chat_channels.v1&state=9";
        }

        public static string GetTimersAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"http://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&state=11";
        }

        public static string GetMailAuthURL()
        {
            var clientID = SettingsManager.Settings.WebServerModule.CcpAppClientId;
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            var callbackurl =  $"http://{extIp}:{extPort}/callback.php";
            return $"https://login.eveonline.com/oauth/authorize?response_type=code&redirect_uri={callbackurl}&client_id={clientID}&scope=esi-mail.read_mail.v1+esi-mail.send_mail.v1+esi-mail.organize_mail.v1&state=12";
        }


        public static string GetTimersURL()
        {
            var extIp = SettingsManager.Settings.WebServerModule.WebExternalIP;
            var extPort = SettingsManager.Settings.WebServerModule.WebExternalPort;
            return $"http://{extIp}:{extPort}/timers.php";
        }

        public void Dispose()
        {
            ModuleConnectors.Clear();
        }
    }
}
