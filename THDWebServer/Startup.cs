using System.Threading.Tasks;
using Blazored.Modal;
using Matrix.Xmpp.MessageArchiving;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using THDWebServer.Authentication;
using THDWebServer.Classes;
using ThunderED;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Modules.OnDemand;

namespace THDWebServer
{
    public class Startup
    {
        private BackgroundSocketProcessor BackgroundSocketProcessor { get; } = new BackgroundSocketProcessor();

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
           // services.AddDefaultIdentity<ApplicationUser>()
           //     .AddUserStore<CustomUserStore>();

            services.AddRazorPages();
            services.AddServerSideBlazor();
            
            //services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<ApplicationUser>>();
            services.AddScoped<AuthenticationStateProvider, CustomAuthenticationStateProvider>();
            services.AddAuthorizationCore();

            services.AddHttpContextAccessor();
            services.AddProtectedBrowserStorage();
            services.AddBlazoredModal();
            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // app.UseCcpCallbackHandler();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            if(SettingsManager.Settings.WebServerModule.UseHTTPS)
                app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();

            //app.UseAuthentication();
            //app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapControllers();
                endpoints.Map("/chatrelay", async context =>
                {
                    await LogHelper.Log($"QUERY: {context.Request.Path}", LogSeverity.Info, LogCat.Default);

                    if (SettingsManager.Settings.Config.ModuleChatRelay)
                    {
                        await TickManager.GetModule<ChatRelayModule>().ProcessRaw(context);
                        return;
                    }
                });
            });

            //TODO
            if (WebConfig.Instance.Api.IsEnabled)
            {
                app.UseWebSockets();
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path == "/ws")
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            using (var webSocket = await context.WebSockets.AcceptWebSocketAsync())
                            {
                                var finish = new TaskCompletionSource<bool>();

                                await BackgroundSocketProcessor.AddSocket(webSocket, finish);
                                await finish.Task;
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else
                    {
                        if (next != null)
                            await next();
                    }
                });
            }
        }
    }
}
