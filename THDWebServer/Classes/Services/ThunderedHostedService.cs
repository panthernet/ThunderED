using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace THDWebServer.Classes.Services
{
    public class ThunderedHostedService: IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await ThunderED.ExternalAccess.Start();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await ThunderED.ExternalAccess.Shutdown();
        }
    }
}
