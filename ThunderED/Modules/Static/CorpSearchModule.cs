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
            var ceoNameContent = await APIHelper.ESIAPI.GetCharacterData(reason, corporationData.ceo_id);
            var alliance = allianceData?.name ?? "None";

            var builder = new EmbedBuilder()
                .WithDescription(
                    $"[zKillboard](http://www.zkillboard.com/corporation/{corpIDLookup.corporation[0]}) / [EVEWho](https://evewho.com/corp/{HttpUtility.UrlEncode(corporationData.name)})")
                .WithColor(new Color(0x4286F4))
                .WithThumbnailUrl($"https://image.eveonline.com/Corporation/{corpIDLookup.corporation[0]}_64.png")
                .WithAuthor(author =>
                {
                    author
                        .WithName($"{corporationData.name}");
                })
                .AddField(LM.Get("Additionaly"), "\u200b")
                .AddInlineField($"{LM.Get("Corporation")}:", $"{corporationData.name}")
                .AddInlineField($"{LM.Get("Alliance")}:", alliance)
                .AddInlineField(LM.Get("CEO"), $"{ceoNameContent.name}")
                .AddInlineField(LM.Get("Pilots"), $"{corporationData.member_count}");

            var embed = builder.Build();


            await APIHelper.DiscordAPI.ReplyMessageAsync(context,"", embed);
            await LogHelper.LogInfo($"Sending {context.Message.Author} Corporation Info Request", LogCat.CorpSearch);            
        }

    }

}
