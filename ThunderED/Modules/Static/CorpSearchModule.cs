using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using ThunderED.Helpers;

namespace ThunderED.Modules.Static
{
    internal class CorpSearchModule: AppModuleBase
    {
        public override LogCat Category => LogCat.CorpSearch;

        internal static async Task CorpSearch(ICommandContext context, string name)
        {
            var reason = LogCat.CorpSearch.ToString();
            var corpIDLookup = await APIHelper.ESIAPI.SearchCorporationId(reason, name);
            if (corpIDLookup == null)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("corpNotFound"));
                return;
            }
            var corporationData = await APIHelper.ESIAPI.GetCorporationData(reason, corpIDLookup.corporation[0]);
            var allianceData = await APIHelper.ESIAPI.GetAllianceData(reason, corporationData.alliance_id);
            var ceo = await APIHelper.ESIAPI.GetCharacterData(reason, corporationData.ceo_id);
            var alliance = allianceData?.name ?? "None";

            

            var zkillContent = await APIHelper.ZKillAPI.GetCorporationData(corpIDLookup.corporation[0]);

            var textNames = $"{LM.Get("Corporation")}:\n{LM.Get("Alliance")}:\n{ceo.name}\n{corporationData.member_count}";
            var textValues = $"{corporationData.name}\n{alliance}\n[{LM.Get("CEO")}](https://zkillboard.com/character/{corporationData.ceo_id}/)\n{LM.Get("Pilots")}";

            var supersCount = zkillContent.hasSupers ? zkillContent.supers.FirstOrDefault(a=>a.title == "Titans")?.data.Length : 0;
            var titansCount = zkillContent.hasSupers ? zkillContent.supers.FirstOrDefault(a => a.title == "Supercarriers")?.data.Length : 0;
            var system = zkillContent.topLists.FirstOrDefault(a => a.type == "solarSystem")?.values.FirstOrDefault()?.solarSystemName ?? "???";
            var textPvpNames = $"{LM.Get("Dangerous")}:\n{LM.Get("FleetCHance2")}:\n{LM.Get("corpSoloKills")}\n{LM.Get("corpTotalKills")}\n{LM.Get("corpKnownSupers")}\n{LM.Get("corpActiveSystem")}";
            var textPvpValues = $"{zkillContent.dangerRatio}%\n{zkillContent.gangRatio}%\n{zkillContent.soloKills}\n{zkillContent.shipsDestroyed}\n{supersCount}/{titansCount}\n{system}";


            var desc = await MailModule.PrepareBodyMessage(corporationData.description);
            var builder = new EmbedBuilder()
                .WithDescription(
                    $"[zKillboard](https://zkillboard.com/corporation/{corpIDLookup.corporation[0]}) / [EVEWho](https://evewho.com/corp/{HttpUtility.UrlEncode(corporationData.name)})")
                .WithColor(new Color(0x4286F4))
                .WithThumbnailUrl($"https://image.eveonline.com/Corporation/{corpIDLookup.corporation[0]}_64.png")
                .WithAuthor(author =>
                {
                    author
                        .WithName($"{corporationData.name}");
                })
                .AddInlineField(LM.Get("corpGeneralInfo"), textNames)
                .AddInlineField("-", textValues)
                .AddInlineField(LM.Get("corpPvpInfo"), textPvpNames)
                .AddInlineField("-", textPvpValues)
                .AddField(LM.Get("corpDescription"), desc)
                ;

            var embed = builder.Build();


            await APIHelper.DiscordAPI.ReplyMessageAsync(context,"", embed);
            await LogHelper.LogInfo($"Sending {context.Message.Author} Corporation Info Request", LogCat.CorpSearch);            
        }

    }

}
