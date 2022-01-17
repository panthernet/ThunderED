using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public class ChatRelayModule : AppModuleBase
    {
        public override LogCat Category => LogCat.ChatRelay;

        private readonly Dictionary<string, List<string>> _pool = new Dictionary<string, List<string>>();

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing ChatRelay module...", Category);
            /*if (WebServerModule.WebModuleConnectors.ContainsKey(Reason))
                WebServerModule.WebModuleConnectors.Remove(Reason);
            WebServerModule.WebModuleConnectors.Add(Reason, ProcessRequest);*/
        }

        private string UnwrapCode(string code)
        {
            var dep = code.Replace("-", "+").Replace("_", "/");
            var debase = Convert.FromBase64String(dep);
            return Encoding.UTF8.GetString(debase);
        }

        private async Task<WebQueryResult> ProcessRequest(Dictionary<string, StringValues> query, CallbackTypeEnum type, string ip, WebAuthUserData data)
        {
            if (!Settings.Config.ModuleChatRelay) return WebQueryResult.False;
            //await LogHelper.LogWarning($"{query}", Category);

            try
            {
                RunningRequestCount++;

                var message = query["msg"].ToString();
                var code = UnwrapCode(query["code"].ToString());
                var relays = Settings.ChatRelayModule.RelayChannels.Where(a => a.Code == code);
                var iChannel = query["ch"].ToString();

                foreach (var relay in relays)
                {
                    if (relay.DiscordChannelId == 0)
                    {
                        await LogHelper.LogError($"Relay with code {code} has no discord channel specified!",
                            Category);
                        return new WebQueryResult(WebQueryResultEnum.ChatRelayError);
                    }

                    if (relay.EVEChannelName != iChannel)
                    {
                        await LogHelper.LogError(
                            $"Relay with code {code} has got message with channel mismatch!", Category);
                        return new WebQueryResult(WebQueryResultEnum.ChatRelayError);
                    }

                    if (!_pool.ContainsKey(code))
                        _pool.Add(code, new List<string>());
                    var list = _pool[code];
                    if (list.Contains(message))
                    {
                        return new WebQueryResult(WebQueryResultEnum.ChatRelayDupe);
                    }

                    await APIHelper.DiscordAPI.SendMessageAsync(relay.DiscordChannelId, message);


                    list.Add(message);
                    if (list.Count > 20)
                        list.RemoveAt(0);
                }

                return new WebQueryResult(WebQueryResultEnum.ChatRelayOK);

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                return new WebQueryResult(WebQueryResultEnum.ChatRelayError);
            }
            finally
            {
                RunningRequestCount--;
            }

            return WebQueryResult.False;
        }

        public async Task ProcessRaw(HttpContext context)
        {

            if (context.Request.Method != "POST" || context.Request.Query.Keys.Count != 3 || !context.Request.Path.Value.Contains("/chatrelay"))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("ERROR: General error");
            }

            var result = await ProcessRequest(context.Request.Query.ToDictionary(a=>a.Key, a=>a.Value), CallbackTypeEnum.Callback, null, null);
            if (result.Result == WebQueryResultEnum.ChatRelayOK)
                await context.Response.WriteAsync("OK");
            else if (result.Result == WebQueryResultEnum.ChatRelayDupe)
                await context.Response.WriteAsync("DUPE");
            else if (result.Result == WebQueryResultEnum.ChatRelayError)
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("ERROR: General error");
            }
        }
    }
}
