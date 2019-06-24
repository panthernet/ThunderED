using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules
{
    public class TestModule: AppModuleBase
    {
        public override LogCat Category => LogCat.Default;

        public static async Task DebugKillmailMessage(ICommandContext context, string template, bool isNpcKill = false)
        {
            try
            {
               // var lines = (await File.ReadAllLinesAsync(Path.Combine(SettingsManager.RootDirectory, "Templates/Messages/default", "def.Template.killMailGeneral.txt")))
               //     .Where(a => !a.StartsWith("//") && !string.IsNullOrWhiteSpace(a)).ToList();
                var dic = new Dictionary<string, string>
                {
                    {"{shipID}", "28848"},
                    {"{shipType}", "Nemesis"},
                    {"{iskValue}", "123 456 789"},
                    {"{systemName}", "Asakai"},
                    {"{systemSec}", "0.4"},
                    {"{victimName}", "Don Chack"},
                    {"{victimCorpName}", "Nebula Alba"},
                    {"{victimCorpTicker}", "INEBI"},
                    {"{victimAllyTicker}", "<UF>"},
                    {"{attackerName}", "Rad1st"},
                    {"{attackerCorpName}", "Airguard"},
                    {"{attackerCorpTicker}", "AIRG"},
                    {"{attackerAllyTicker}", "<-LSH->"},
                    {"{attackersCount}", "1"},
                    {"{kmId}", "69474440"},
                    {"{timestamp}", "27.04.2018"},
                    {"{isNpcKill}", isNpcKill ? "true" : "false"},
                    {"{isLoss}", "false"},
                };

                var path = Path.Combine(SettingsManager.RootDirectory, "Templates", "Messages", template);
                if(!File.Exists(path))
                    return;
                var embed = await TemplateHelper.CompileTemplate(MessageTemplateType.KillMailGeneral, path, dic);
                if (embed == null)
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, "Test embed failed!");
                    return;
                }
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, " ", embed);

              
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("test", ex, LogCat.Debug);
            }
        }
    }
}
