using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Timers;
using ThunderED.Classes;
using ThunderED.Classes.Enums;
using ThunderED.Helpers;
using HttpListener = System.Net.Http.HttpListener;

namespace ThunderED.Modules.Sub
{
    public sealed class WebServerModule : AppModuleBase, IDisposable
    {
        private static HttpListener _statusListener;
        private readonly ConcurrentQueue<DateTime> _statusQueries = new ConcurrentQueue<DateTime>();
        private readonly Timer _statusTimer = new Timer(5000);
        public override LogCat Category => LogCat.WebServer;

        public static Dictionary<string, Func<string, CallbackTypeEnum, string, WebAuthUserData, Task<WebQueryResult>>> WebModuleConnectors { get; } = new Dictionary<string, Func<string, CallbackTypeEnum, string, WebAuthUserData, Task<WebQueryResult>>>();

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing WebServer module...", Category);

            if (Settings.WebServerModule.WebExternalPort == Settings.WebServerModule.ServerStatusPort && Settings.WebServerModule.ServerStatusPort != 0)
                await LogHelper.LogWarning($"Web server is configured to use the same {Settings.WebServerModule.ServerStatusPort} port for both http and status queries!");
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
                        if (!APIHelper.IsDiscordAvailable)
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
        }



        /// <summary>
        /// Iterate between connected handlers to process request
        /// </summary>
        public static async Task<WebQueryResult> ProcessWebCallbacks(string query, CallbackTypeEnum type, string ip,
            WebAuthUserData data)
        {
            foreach (var method in WebModuleConnectors.Values)
            {
                try
                {
                    var result = await method(query, type, ip, data);
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

        public void Dispose()
        {
            _statusTimer?.Stop();
        }
    }
}
