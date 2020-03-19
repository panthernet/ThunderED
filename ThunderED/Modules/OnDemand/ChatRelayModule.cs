using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public class ChatRelayModule : AppModuleBase
    {
        public override LogCat Category => LogCat.ChatRelay;

        private readonly Dictionary<string, List<string>> _pool = new Dictionary<string, List<string>>();

        public ChatRelayModule()
        {
            LogHelper.LogModule("Initializing ChatRelay module...", Category).GetAwaiter().GetResult();
            WebServerModule.ModuleConnectors.Add(Reason, OnRequestReceived);
        }

        private async Task<bool> OnRequestReceived(HttpListenerRequestEventArgs context)
        {
            if (!Settings.Config.ModuleChatRelay) return false;

            var request = context.Request;
            var response = context.Response;

            try
            {
                RunningRequestCount++;

                var extPort = Settings.WebServerModule.WebExternalPort;
                var port = Settings.WebServerModule.WebExternalPort;

                if (request.HttpMethod == HttpMethod.Post.ToString())
                {
                    if (request.Url.LocalPath == "/chatrelay" || request.Url.LocalPath == $"{extPort}/chatrelay" ||
                        request.Url.LocalPath == $"{port}/chatrelay")
                    {
                        var prms = request.Url.Query.TrimStart('?').Split('&');
                        if (prms.Length != 3)
                        {
                            await response.WriteContentAsync("ERROR: Bad request");
                            return true;
                        }

                        var message = HttpUtility.UrlDecode(prms[0].Split('=')[1]);
                        var code = Encoding.UTF8.GetString(Convert.FromBase64String(
                            $"{HttpUtility.UrlDecode(prms[1].Split('=')[1])?.Replace("-", "+").Replace("_", "/")}"));
                        var relays = Settings.ChatRelayModule.RelayChannels.Where(a => a.Code == code);
                        var iChannel = HttpUtility.UrlDecode(prms[2].Split('=')[1]);

                        foreach (var relay in relays)
                        {
                            if (relay.DiscordChannelId == 0)
                            {
                                await LogHelper.LogError($"Relay with code {code} has no discord channel specified!",
                                    Category);
                                await response.WriteContentAsync("ERROR: Bad server config");
                                return true;
                            }

                            if (relay.EVEChannelName != iChannel)
                            {
                                await LogHelper.LogError(
                                    $"Relay with code {code} has got message with channel mismatch!", Category);
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
                        }

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
            finally
            {
                RunningRequestCount--;
            }

            return false;
        }
    }
}
