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
            if (corpIDLookup == null || corpIDLookup.corporation == null)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("corpNotFound"));
                return;
            }
            var corporationData = await APIHelper.ESIAPI.GetCorporationData(reason, corpIDLookup.corporation[0]);
            var allianceData = await APIHelper.ESIAPI.GetAllianceData(reason, corporationData.alliance_id);
            var ceo = await APIHelper.ESIAPI.GetCharacterData(reason, corporationData.ceo_id);
            var alliance = allianceData?.name ?? LM.Get("None");
            var allianceTicker = allianceData != null ? $"[{allianceData?.ticker}]" : "";

            var lite = await APIHelper.ZKillAPI.GetLiteCorporationData(corpIDLookup.corporation[0]);
            var zkillContent = await APIHelper.ZKillAPI.GetCorporationData(corpIDLookup.corporation[0], !lite.hasSupers);

            var textNames = $"{LM.Get("Corporation")}:\n{LM.Get("Alliance")}:\n{LM.Get("CEO")}\n{LM.Get("Pilots")}";
            var textValues = $"{corporationData.name}[{corporationData.ticker}]\n{alliance}{allianceTicker}\n[{ceo.name}](https://zkillboard.com/character/{corporationData.ceo_id}/)\n{corporationData.member_count}";

            var supersCount = zkillContent == null ? "???" : ( zkillContent.hasSupers ? zkillContent.supers?.titans?.data?.Length.ToString() : "0");
            var titansCount = zkillContent == null ? "???" : ( zkillContent.hasSupers ? zkillContent.supers?.supercarriers?.data?.Length.ToString() : "0");
            var system = zkillContent?.topLists?.FirstOrDefault(a => a.type == "solarSystem")?.values?.FirstOrDefault()?.solarSystemName ?? "???";
            var textPvpNames = $"{LM.Get("Dangerous")}:\n{LM.Get("FleetCHance2")}:\n{LM.Get("corpSoloKills")}\n{LM.Get("corpTotalKills")}\n{LM.Get("corpKnownSupers")}\n{LM.Get("corpActiveSystem")}";
            var textPvpValues = $"{zkillContent?.dangerRatio ?? 0}%\n{zkillContent?.gangRatio ?? 0}%\n{zkillContent?.soloKills ?? 0}\n{zkillContent?.shipsDestroyed ?? 0}\n{supersCount}/{titansCount}\n{system}";


            var sList = await MailModule.PrepareBodyMessage(corporationData.description);
            var desc = sList[0];
            if (desc.Length > 1024)
                desc = desc.Substring(0, 1023);
            desc = string.IsNullOrWhiteSpace(desc) ? "-" : desc;
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
                .AddField(LM.Get("corpGeneralInfo"), textNames, true)
                .AddField("-", textValues, true)
                .AddField(LM.Get("corpPvpInfo"), textPvpNames, true)
                .AddField("-", textPvpValues, true)
                .AddField(LM.Get("corpDescription"), desc)
                ;

            var embed = builder.Build();


            await APIHelper.DiscordAPI.ReplyMessageAsync(context," ", embed);
            await LogHelper.LogInfo($"Sending {context.Message.Author} Corporation Info Request", LogCat.CorpSearch);            
        }

    }

}
