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
                var dic = new Dictionary<string, string>
                {
                    {"{shipID}", "11377"},
                    {"{shipType}", "Nemesis"},
                    {"{iskValue}", "123 456 789"},
                    {"{systemName}", "Asakai"},
                    {"{systemSec}", "0.4"},
                    {"{victimName}", "Don Chack"},
                    {"{victimID}", "91579024"},
                    {"{victimCorpName}", "Nebula Alba"},
                    {"{victimCorpID}", "98535812"},
                    {"{victimCorpTicker}", "INEBI"},
                    {"{victimAllyTicker}", "UF"},
                    {"{victimAllyID}", "99005333"},
                    {"{attackerName}", "Rad1st"},
                    {"{attackerID}", "90172171"},
                    {"{attackerCorpName}", "Airguard"},
                    {"{attackerCorpID}", "1903628557"},
                    {"{attackerCorpTicker}", "AIRG"},
                    {"{attackerAllyTicker}", "-LSH-"},
                    {"{attackerAllyName}", "Lowsechnaya Sholupen"},
                    {"{attackerAllyID}", "99003557"},
                    {"{attackersCount}", "1"},
                    {"{kmId}", "69474440"},
                    {"{timestamp}", "27.04.2018"},
                    {"{isNpcKill}", isNpcKill ? "true" : "false"},
                    {"{isLoss}", "false"},
                    
                    {"{attackershipID}", "23919"},
                    {"{attackershipType}", "Aeon"},
                    {"{iskFittedValue}", "23 456 789"},
                    {"{systemID}", "30045332"},
                    {"{constName} ", "Kurala"},
                    {"{regionName}", "Black Rise"},
                    {"{regionID}", "10000069"},
                    {"{victimAllyOrCorpName}", "United Fleet"},
                    {"{victimAllyOrCorpTicker}", "UF"},
                    {"{attackerAllyOrCorpName}", "Lowsechnaya Sholupen"},
                    {"{attackerAllyOrCorpTicker}", "-LSH-"},

                };

                var path = Path.Combine(SettingsManager.RootDirectory, "Templates", "Messages", template);
                if(!File.Exists(path))
                    return;
                var embed = await TemplateHelper.CompileTemplate(path, dic);
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
