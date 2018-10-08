using System;
using System.Threading.Tasks;
using System.Web;
using Discord;
using Discord.Commands;
using ThunderED.Helpers;
using ThunderED.Json;
using ThunderED.Json.ZKill;

namespace ThunderED.Modules.Static
{
    public class CharSearchModule: AppModuleBase
    {
        public override LogCat Category => LogCat.CharSearch;

        internal static async Task SearchCharacter(ICommandContext context, string name)
        {
            var channel = context.Channel;

            var charSearch = await APIHelper.ESIAPI.SearchCharacterId(LogCat.CharSearch.ToString(), name);
            if (charSearch == null)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("charNotFound"), true);
                return;
            }

            var characterId = charSearch.character[0];

            var characterData = await APIHelper.ESIAPI.GetCharacterData(LogCat.CharSearch.ToString(), characterId, true);
            if (characterData == null)
            {
                await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("charNotFound"), true);
                return;
            }

            var corporationData = await APIHelper.ESIAPI.GetCorporationData(LogCat.CharSearch.ToString(), characterData.corporation_id);

            var zkillContent = await APIHelper.ZKillAPI.GetCharacterKills(characterId);
            var characterStats = await APIHelper.ZKillAPI.GetCharacterStats(characterId);
            var zkillLosses = await APIHelper.ZKillAPI.GetCharacterLosses(characterId);

            var zkillLast = zkillContent.Count > 0 ? zkillContent[0] : new JsonClasses.ESIKill();
            var systemData = await APIHelper.ESIAPI.GetSystemData("", zkillLast.solar_system_id);
            var lastShipType = LM.Get("Unknown");

            if (zkillLast.victim != null && zkillLast.victim.character_id == characterId)
                lastShipType = zkillLast.victim.ship_type_id.ToString();
            else if (zkillLast.victim != null)
            {
                foreach (var attacker in zkillLast.attackers)
                {
                    if (attacker.character_id == characterId)
                        lastShipType = attacker.ship_type_id.ToString();
                }
            }

            var lastShip = await APIHelper.ESIAPI.GetTypeId("", lastShipType);
            var lastSeen = zkillLast.killmail_time;
            var allianceData = await APIHelper.ESIAPI.GetAllianceData("", characterData.alliance_id);

            var alliance = allianceData?.name ?? LM.Get("None");
            var lastSeenSystem = systemData?.name ?? LM.Get("None");
            var lastSeenShip = lastShip?.name ?? LM.Get("None");
            var lastSeenTime = lastSeen == DateTime.MinValue ? LM.Get("longTimeAgo") : $"{lastSeen}";
            var dangerous = characterStats.dangerRatio > 75 ? LM.Get("Dangerous") : LM.Get("Snuggly");
            var gang = characterStats.gangRatio > 70 ? LM.Get("fleetChance") : LM.Get("soloChance");

            var cynoCount = 0;
            var covertCount = 0;

            foreach (var kill in zkillLosses)
            {
                if (kill.victim.character_id == characterId)
                {
                    foreach (var item in kill.victim.items)
                    {
                        if (item.item_type_id == 21096)
                            cynoCount++;
                        if (item.item_type_id == 28646)
                            covertCount++;
                    }
                }
            }

            var text1 = characterStats.dangerRatio == 0 ? LM.Get("Unavailable") : HelpersAndExtensions.GenerateUnicodePercentage(characterStats.dangerRatio);
            var text2 = characterStats.gangRatio == 0 ? LM.Get("Unavailable") : HelpersAndExtensions.GenerateUnicodePercentage(characterStats.gangRatio);

            var builder = new EmbedBuilder()
                .WithDescription(
                    $"[zKillboard](https://zkillboard.com/character/{characterId}/) / [EVEWho](https://evewho.com/pilot/{HttpUtility.UrlEncode(characterData.name)})")
                .WithColor(new Color(0x4286F4))
                .WithThumbnailUrl($"https://image.eveonline.com/Character/{characterId}_64.jpg")
                .WithAuthor(author =>
                {
                    author
                        .WithName($"{characterData.name}");
                })
                .AddField(LM.Get("Additionaly"), "\u200b")
                .AddInlineField($"{LM.Get("Corporation")}:", $"{corporationData.name}")
                .AddInlineField($"{LM.Get("Alliance")}:", $"{alliance}")
                .AddInlineField($"{LM.Get("HasBeenSeen")}:", $"{lastSeenSystem}")
                .AddInlineField($"{LM.Get("OnShip")}:", $"{lastSeenShip}")
                .AddInlineField($"{LM.Get("Seen")}:", $"{lastSeenTime}")
                .AddField("\u200b", "\u200b")
                .AddInlineField(LM.Get("CommonCyno"), $"{cynoCount}")
                .AddInlineField(LM.Get("CovertCyno"), $"{covertCount}")
                .AddInlineField(LM.Get("Dangerous"), $"{text1}{Environment.NewLine}{Environment.NewLine}**{dangerous} {characterStats.dangerRatio}%**")
                .AddInlineField(LM.Get("FleetChance2"), $"{text2}{Environment.NewLine}{Environment.NewLine}**{characterStats.gangRatio}% {gang}**");

            var embed = builder.Build();

            await APIHelper.DiscordAPI.SendMessageAsync(channel, "", embed).ConfigureAwait(false);
            await LogHelper.LogInfo($"Sending {context.Message.Author} Character Info Request", LogCat.CharSearch).ConfigureAwait(false);

            await Task.CompletedTask;
        }
    }
}
