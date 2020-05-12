using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using ByteSizeLib;
using Discord.Commands;
using ThunderED.Helpers;

namespace ThunderED.Modules.Static
{
    internal class AboutModule: AppModuleBase
    {
        public override LogCat Category => LogCat.About;

        internal static async Task About(ICommandContext context)
        {
            try
            {
                var channel = context.Channel;

                var botid = APIHelper.DiscordAPI.GetCurrentUser().Id;
                var memoryUsed = ByteSize.FromBytes(Process.GetCurrentProcess().WorkingSet64);
                var runTime = DateTime.Now - Process.GetCurrentProcess().StartTime;
                var totalUsers = APIHelper.DiscordAPI.GetUsersCount();

                await APIHelper.DiscordAPI.SendMessageAsync(channel,
                    $"{context.User.Mention},{Environment.NewLine}{Environment.NewLine}" +
                    $"```ThunderED v{Program.VERSION} - Thunder EVE Discord Bot{Environment.NewLine}{Environment.NewLine}" +
                    $"Developer: panthernet (In-game Name: Captain PantheR){Environment.NewLine}" +
                    $"Bot ID: {botid}{Environment.NewLine}{Environment.NewLine}" +
                    $"Run Time: {runTime.Days} Days {runTime.Hours} Hours {runTime.Minutes} Minutes {runTime.Seconds} Seconds{Environment.NewLine}{Environment.NewLine}" +
                    $"Statistics:{Environment.NewLine}" +
                    $"Memory Used: {Math.Round(memoryUsed.LargestWholeNumberDecimalValue, 2)} {memoryUsed.LargestWholeNumberDecimalSymbol}{Environment.NewLine}" +
                    $"Total Users Seen: {totalUsers}```").ConfigureAwait(false);
                await Task.CompletedTask;
            }catch
            {
                // ignore
            }
        }
    }
}
