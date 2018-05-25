using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ThunderED.Classes;
using ThunderED.Classes.ChatRelay;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public class ChatRelayModule : AppModuleBase
    {
        public override LogCat Category => LogCat.ChatRelay;

        public ChatRelaySettings Settings { get; private set; }

        private readonly Dictionary<string, List<string>> _pool = new Dictionary<string, List<string>>();

        public ChatRelayModule()
        {
            Settings = ChatRelaySettings.Load(SettingsManager.FileSettingsPath);
            WebServerModule.ModuleConnectors.Add(Reason, OnRequestReceived);
        }

        private async Task<bool> OnRequestReceived(HttpListenerRequestEventArgs context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                var extPort = SettingsManager.Get("webServerModule", "webExternalPort");
                var port = SettingsManager.Get("webServerModule", "webListenPort");

                if (request.HttpMethod == HttpMethod.Post.ToString())
                {
                    if (request.Url.LocalPath == "/chatrelay.php" || request.Url.LocalPath == $"{extPort}/chatrelay.php" || request.Url.LocalPath == $"{port}/chatrelay.php")
                    {
                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        if (prms.Length != 3)
                        {
                            await response.WriteContentAsync("ERROR: Bad request");
                            return true;
                        }

                        var message = HttpUtility.UrlDecode(prms[0].Split('=')[1]);
                        var code = Encoding.UTF8.GetString(Convert.FromBase64String($"{HttpUtility.UrlDecode(prms[1].Split('=')[1]).Replace("-", "+").Replace("_", "/")}"));
                        var relay = Settings.ChatRelayModule.RelayChannels.FirstOrDefault(a => a.Code == code);
                        var iChannel = HttpUtility.UrlDecode(prms[2].Split('=')[1]);
                        //check relay code
                        if (relay == null)
                        {
                            await LogHelper.LogWarning($"Got message with unknown code {code}. Body: {message}", Category);
                            await response.WriteContentAsync("ERROR: Bad code");
                            return true;
                        }

                        if (relay.DiscordChannelId == 0)
                        {
                            await LogHelper.LogError($"Relay with code {code} has no discord channel specified!", Category);
                            await response.WriteContentAsync("ERROR: Bad server config");
                            return true;
                        }

                        if (relay.EVEChannelName != iChannel)
                        {
                            await LogHelper.LogError($"Relay with code {code} has got message with channel mismatch!", Category);
                            await response.WriteContentAsync("ERROR: Invalid channel name");
                            return true;

                        }

                        if (!_pool.ContainsKey(code))
                            _pool.Add(code, new List<string>());
                        var list = _pool[code];
                        if (list.Contains(message))
                        {
                            await response.WriteContentAsync("DUPE");
                            return true;
                        }

                        await APIHelper.DiscordAPI.SendMessageAsync(relay.DiscordChannelId, message);

                        list.Add(message);
                        if (list.Count > 20)
                            list.RemoveAt(0);

                        await response.WriteContentAsync("OK");

                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                await response.WriteContentAsync("ERROR: Server error");
                await LogHelper.LogEx(ex.Message, ex, Category);
            }

            return false;
        }
    }
}
