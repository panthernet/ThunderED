using System.Threading.Tasks;
using Blazor.Extensions.Storage.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.ProtectedBrowserStorage;
using Microsoft.Extensions.Logging;
using ThunderED;
using ThunderED.Classes;

namespace THDWebServer.Classes.Middleware
{
    public class CcpCallbackHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<CcpCallbackHandlerMiddleware> _logger;

        public CcpCallbackHandlerMiddleware(RequestDelegate next, ILogger<CcpCallbackHandlerMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        //[Inject]
        // protected ISessionStorage SessionStorage { get; set; }

        [Inject]
        protected ProtectedSessionStorage ProtectedSessionStore { get; set; }

       /* public async Task Invoke(HttpContext context)
        {
            var request = context.Request;

            if (request.Path.HasValue && request.Path.Value == "/callback" && request.QueryString.HasValue)
            {
                _logger.LogInformation($"Processing callback...");
                var result = await ExternalAccess.ProcessCallback(request.QueryString.Value);

                switch (result.Result)
                {
                    case WebQueryResultEnum.Success:
                        return;
                    case WebQueryResultEnum.EsiFailure:
                        context.Response.Redirect("/esifailure");
                        return;
                    case WebQueryResultEnum.BadFeedRequest:
                        context.Response.Redirect("/badfeedrq");
                        return;
                    case WebQueryResultEnum.UnauthorizedFeedRequest:
                        context.Response.Redirect("/unafeedrq");
                        return;
                    case WebQueryResultEnum.FeedSuccess:
                        //var svc = (RequestResultState)context.RequestServices.GetService(typeof(RequestResultState));
                        //context.Session.SetString("name", result.GetValue<string>("name"));
                        //svc.CharacterName = result.GetValue<string>("name");
                        //await SessionStorage.SetItem("name", result.GetValue<string>("name"));
                        //var svc = (ISessionStorage)context.RequestServices.GetService(typeof(ISessionStorage));
                        // await svc.SetItem("name", result.GetValue<string>("name"));
                        await ProtectedSessionStore.SetAsync("name", result.GetValue<string>("name"));
                        context.Response.Redirect("/feedok");
                        return;
                    default:
                        break;
                }
                
                
            }


            //pass the request
            await _next.Invoke(context);

        }*/
    }
}
