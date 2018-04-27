using System;
using System.Threading.Tasks;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules.Static
{
    public class EveTimeModule: AppModuleBase
    {
        public override LogCat Category => LogCat.EveTime;
        public override Task Run(object prm)
        {
            return Task.CompletedTask;
        }

        internal static async Task CheckTime(ICommandContext context)
        {
            try
            {
                var utcTime = DateTime.UtcNow.ToString( SettingsManager.Get("config", "timeFormat"));
                await APIHelper.DiscordAPI.ReplyMessageAsync((ICommandContext) context, $"{LM.Get("evetime")}: {utcTime}");
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("EveTime", ex, LogCat.EveTime);
            }

        }
    }
}
