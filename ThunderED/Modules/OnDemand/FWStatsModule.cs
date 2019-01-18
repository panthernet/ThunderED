using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules.OnDemand
{
    public static class FWStatsModule
    {
        private static bool _isPostRunning;

        public class FWFactionData
        {
            public long factionId;
            public long factionCorpId;
            public long oppFactionId;
            public string factionName;
            public string factionImage;
        }

        public static async Task PostFWSTats(char faction, IMessageChannel channel)
        {
            if (_isPostRunning)
            {
                await APIHelper.DiscordAPI.SendMessageAsync(channel, LM.Get("commandInProgress")).ConfigureAwait(false);
                return;
            }

            _isPostRunning = true;
            try
            {
                var fwData = GetFWData(faction);

                var stats = (await APIHelper.ESIAPI.GetFWStats("General")).FirstOrDefault(a => a.faction_id == fwData.factionId);

                var statOccupiedSystemsCount = stats.systems_controlled;
                var statKillsYesterday = stats.kills.yesterday;
                var statPilots = stats.pilots;

                var sysList = await APIHelper.ESIAPI.GetFWSystemStats("General");
                var statTotalSystemsCount = sysList.Count(a => a.owner_faction_id == fwData.factionId || a.owner_faction_id == fwData.oppFactionId);
                var mTotalPoint = statTotalSystemsCount * 6;
                var p = statOccupiedSystemsCount / (double) mTotalPoint * 100;
                var pSYs = statOccupiedSystemsCount / (double) statTotalSystemsCount * 100;

                var statTier = 1;
                if (p > 20)
                    statTier = 2;
                else if (p > 40)
                    statTier = 3;
                else if (p > 60)
                    statTier = 4;
                else if (statTier > 80)
                    statTier = 5;

                var avgRatio = 0d;
                var topItems = string.Empty;

                string prognose = LM.Get("fwstats_notip");
                if (pSYs > 70)
                    prognose = LM.Get("fwstats_gofarm", (int) pSYs);
                else if (!SettingsManager.Settings.Config.ModuleLPStock && pSYs < 30)
                    prognose = LM.Get("fwstats_gosell", (int) pSYs);
                else
                {
                    if (SettingsManager.Settings.Config.ModuleLPStock)
                    {
                        var lpstockList = await LPStockModule.GetLPStockInformation(fwData.factionCorpId, 5);
                        if (lpstockList.Count > 0)
                        {
                            avgRatio = Math.Round(lpstockList.Average(a => a.Ratio), 1);
                            var sb = new StringBuilder();
                            for (var i = 0; i < lpstockList.Count && i < 3; i++)
                            {
                                sb.Append((i + 1).ToString());
                                sb.Append(". ");
                                sb.Append(lpstockList[i].Name);
                                sb.Append(" (");
                                sb.Append(lpstockList[i].Ratio);
                                sb.Append(")\n");
                            }

                            topItems = sb.ToString();
                        }
                    }

                    if (avgRatio >= 1400)
                        prognose = LM.Get("fwstats_goselllp", avgRatio, fwData.factionCorpId, topItems);
                }

                var embed = new EmbedBuilder()
                    .WithTitle(LM.Get("fwstats_title", fwData.factionName))
                    .AddField(LM.Get("fwstats_systems"), $"{statOccupiedSystemsCount}/{statTotalSystemsCount}", true)
                    .AddField(LM.Get("fwstats_pilots"), LM.Get("fwstats_pilotsText", statPilots, statKillsYesterday), true)
                    .AddField(LM.Get("fwstats_tip"), prognose, true)
                    .WithColor(0x00FF00);

                if (!string.IsNullOrEmpty(fwData.factionImage))
                    embed.WithThumbnailUrl(fwData.factionImage);

                await APIHelper.DiscordAPI.SendMessageAsync(channel, " ", embed.Build()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx("", ex);
            }
            finally
            {
                _isPostRunning = false;
            }
        }

        public static async Task<FWFactionData> GetCorpData(string command)
        {
            var fwData = new FWFactionData();
            switch (command.ToLower())
            {
                case "c":
                case "caldari":
                    fwData.factionId = 500001;
                    fwData.oppFactionId = 500004;
                    fwData.factionName = "Caldari";
                    fwData.factionImage = SettingsManager.Settings.Resources.ImgFactionCaldari;
                    fwData.factionCorpId = 1000180;
                    break;
                case "g":
                case "gallente":
                    fwData.factionId = 500004;
                    fwData.oppFactionId = 500001;
                    fwData.factionName = "Gallente";
                    fwData.factionImage = SettingsManager.Settings.Resources.ImgFactionGallente;
                    fwData.factionCorpId = 1000181;
                    break;
                case "a":
                case "amarr":
                    fwData.factionId = 500003;
                    fwData.oppFactionId = 500002;
                    fwData.factionName = "Amarr";
                    fwData.factionImage = SettingsManager.Settings.Resources.ImgFactionAmarr;
                    fwData.factionCorpId = 1000179;
                    break;
                case "m":
                case "minmatar":
                    fwData.factionId = 500002;
                    fwData.oppFactionId = 500003;
                    fwData.factionName = "Minmatar";
                    fwData.factionImage = SettingsManager.Settings.Resources.ImgFactionMinmatar;
                    fwData.factionCorpId = 1000182;
                    break;
                default:
                    var res = (await APIHelper.ESIAPI.SearchCorporationId("LP", command))?.corporation?.FirstOrDefault();
                    if (res.HasValue)
                    {
                        var npcCorps = await APIHelper.ESIAPI.GetNpcCorps("LP");
                        if (!npcCorps.Contains(res.Value))
                            return null;
                        fwData.factionImage = await APIHelper.ESIAPI.GetCorporationIcons("LP", res.Value, 64);                        
                        fwData.factionCorpId = res.Value;
                        fwData.factionName = command;
                    }
                    else return null;
                    break;
            }

            return fwData;
        }

        public static FWFactionData GetFWData(char faction)
        {
            var fwData = new FWFactionData();

            switch (faction)
            {
                case 'c':
                    fwData.factionId = 500001;
                    fwData.oppFactionId = 500004;
                    fwData.factionName = "Caldari";
                    fwData.factionImage = SettingsManager.Settings.Resources.ImgFactionCaldari;
                    fwData.factionCorpId = 1000180;
                    break;
                case 'g':
                    fwData.factionId = 500004;
                    fwData.oppFactionId = 500001;
                    fwData.factionName = "Gallente";
                    fwData.factionImage = SettingsManager.Settings.Resources.ImgFactionGallente;
                    fwData.factionCorpId = 1000181;
                    break;
                case 'a':
                    fwData.factionId = 500003;
                    fwData.oppFactionId = 500002;
                    fwData.factionName = "Amarr";
                    fwData.factionImage = SettingsManager.Settings.Resources.ImgFactionAmarr;
                    fwData.factionCorpId = 1000179;
                    break;
                case 'm':
                    fwData.factionId = 500002;
                    fwData.oppFactionId = 500003;
                    fwData.factionName = "Minmatar";
                    fwData.factionImage = SettingsManager.Settings.Resources.ImgFactionMinmatar;
                    fwData.factionCorpId = 1000182;
                    break;
                default:
                    return null;
            }

            return fwData;
        }

        private static bool _isDisplayBadStandingsRunning;

        public static async Task DisplayBadStandings(ICommandContext context, string commandParams)
        {
            if (_isDisplayBadStandingsRunning)
            {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("badstandBusy"));
                return;
            }

            _isDisplayBadStandingsRunning = true;
            try
            {

                if (string.IsNullOrEmpty(commandParams))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("badstandHelp", SettingsManager.Settings.Config.BotDiscordCommandPrefix, "badstand"));
                    return;
                }

                //var arr = commandParams.Split(' ');
                var stWho = commandParams;

                FWFactionData data = null;
                bool isFaction = false;
                switch (stWho.ToLower())
                {
                    case "c":
                    case "caldari":
                    case "g":
                    case "gallente":
                    case "a":
                    case "amarr":
                    case "m":
                    case "minmatar":
                        data = GetFWData(stWho[0]);
                        isFaction = true;
                        break;
                    case "state protectorate":
                        data = GetFWData('c');
                        break;
                    case "federal defence union":
                        data = GetFWData('g');
                        break;
                    case "24th imperial crusade":
                        data = GetFWData('a');
                        break;
                    case "tribal liberation force":
                        data = GetFWData('m');
                        break;
                    default:
                        data = await GetCorpData(stWho);
                        break;
                }

                var users = await SQLHelper.GetAuthUsersWithPerms(2);
                if (!users.Any())
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("badstandNoUsers"));
                    return;
                }

                var list = new List<StandsEntity>();
                var from_t = isFaction ? "faction" : "npc_corp";
                var lookupId = isFaction ? data.factionId : data.factionCorpId;
                foreach (var user in users)
                {
                    if (!SettingsManager.HasCharStandingsScope(user.Data.PermissionsList)) continue;
                    var token = await APIHelper.ESIAPI.RefreshToken(user.RefreshToken, SettingsManager.Settings.WebServerModule.CcpAppClientId,
                        SettingsManager.Settings.WebServerModule.CcpAppSecret);
                    if (string.IsNullOrEmpty(token)) continue;
                    var st = await APIHelper.ESIAPI.GetcharacterStandings("FWStats", user.CharacterId, token);
                    var exStand = st.FirstOrDefault(a => a.from_type == from_t && a.from_id == lookupId);
                    if (exStand == null) continue;
                    list.Add(new StandsEntity {Name = user.Data.CharacterName, CharId = user.CharacterId, Stand = exStand.standing, Tickers = ""});
                }

                if (!list.Any() || list.All(a => a.Stand >= 0))
                {
                    await APIHelper.DiscordAPI.ReplyMessageAsync(context, LM.Get("badstandNoNegative"));
                    return;
                }

                list = list.OrderBy(a => a.Stand).TakeSmart(10).ToList();

                var sb = new StringBuilder();
                foreach (var entity in list)
                {
                    sb.Append($"{entity.Name} ({entity.Stand:N2})\n");
                }

                var embed = new EmbedBuilder()
                    .WithTitle(LM.Get("badstandTitle", data.factionName))
                    .AddField(LM.Get("badstandTopTenTitle"), sb.ToString(), true)
                    .WithColor(0xFF0000);
                if (!string.IsNullOrEmpty(data.factionImage))
                    embed.WithThumbnailUrl(data.factionImage);

                await APIHelper.DiscordAPI.ReplyMessageAsync(context, " ", embed.Build()).ConfigureAwait(false);
            }
            finally
            {
                _isDisplayBadStandingsRunning = false;
            }
        }

        private class StandsEntity
        {
            public long CharId;
            public double Stand;
            public string Name;
            public string Tickers;

        }
    }
}
