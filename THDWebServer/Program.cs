using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using THDWebServer.Classes;
using THDWebServer.Classes.Services;
using ThunderED;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace THDWebServer
{
    public partial class Program
    {
        public static async Task Main(string[] args)
        {
            System.Net.ServicePointManager.SecurityProtocol =SecurityProtocolType.SystemDefault;

            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, errors) =>
                {
                    return true;
                };

            if (!await LoadSettings())
            {
                try { Console.ReadKey(); } catch { }
                return;
            }

            try
            {
                var fp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "CustomAssets", "favicon.ico");
                var fp2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "favicon.ico");
                if (File.Exists(fp))
                    File.Copy(fp, fp2, true);
            }
            catch
            {
                // ignore
            }

            CreateHostBuilder(args).Build().Run();
            
        }

        private static async Task<bool> LoadConfig()
        {
            if (!File.Exists(SettingsManager.FileSettingsPath))
            {
                var defaultFile = "settings.def.json";
                if (File.Exists(defaultFile))
                {
                    File.Copy(defaultFile, Path.Combine(SettingsManager.DataDirectory, "settings.json"));
                }
                else
                {
                    await LogHelper.LogError(
                        "Please make sure you have settings.json file in bot folder! Create it and fill with correct settings.");
                    return false;
                }
            }

            return true;
        }

        private static async Task<bool> LoadSettings()
        {
            if (await LoadConfig() == false)
                return false;

            if (!await WebConfig.Load("webconfig.json"))
                return false;

            //load settings
            var result = await SettingsManager.Prepare();
            if (!string.IsNullOrEmpty(result))
            {
                await LogHelper.LogError(result);
                return false;
            }

            return true;
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddConsole();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseStartup<Startup>()
                        .UseStaticWebAssets()
                        .ConfigureKestrel((c, options) =>
                        {
                            var isIp = IPAddress.TryParse(SettingsManager.Settings.WebServerModule.WebExternalIP,
                                out var ip);

                            options.ListenAnyIP(SettingsManager.Settings.WebServerModule.WebExternalPort,
                                listenOptions =>
                                {
                                    if (SettingsManager.Settings.WebServerModule.UseHTTPS)
                                    {
                                        if (!string.IsNullOrEmpty(SettingsManager.Settings.WebServerModule
                                            .CertificateStorageName))
                                        {
                                            listenOptions.UseHttps(GetCert().GetAwaiter().GetResult());
                                        }
                                        else if (!string.IsNullOrEmpty(SettingsManager.Settings.WebServerModule
                                            .CertificatePath))
                                        {
                                            listenOptions.UseHttps(
                                                SettingsManager.Settings.WebServerModule.CertificatePath,
                                                SettingsManager.Settings.WebServerModule.CertificatePassword);
                                        }
                                        else listenOptions.UseHttps();
                                    }
                                });

                        });
                    //.UseUrls(
                    //   $"{(SettingsManager.Settings.WebServerModule.UseHTTPS? "https" : "http")}://0.0.0.0:{SettingsManager.Settings.WebServerModule.WebExternalPort}/");
                })
                .ConfigureServices(services =>
                {
                    services.AddHostedService<ThunderedHostedService>();
                    services.AddDbContext<ThunderedDbContext>();
                    services.AddSignalR(e => {
                        e.MaximumReceiveMessageSize = 102400000;
                    });
                })
                ;

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .ConfigureKestrel((context, options) =>
                {
                    // Disable HTTP/2
                    // If it's only your development machines that have this problem, you can wrap
                    // this in `if (context.HostingEnvironment.IsDevelopment())` or some other condition
                    // that is only true in development.
                });

        private static async  Task<X509Certificate2> GetCert()
        {
            using (var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly);
                var cers = store.Certificates.Find(X509FindType.FindBySubjectName, SettingsManager.Settings.WebServerModule.CertificateStorageName, true);
                if (cers.Count > 0)
                {
                    await LogHelper.LogInfo($"Cert count: {cers.Count}");
                    return cers[0];
                }
            }
            await LogHelper.LogInfo($"Cert not found");
            return null;
        }
    }



}
