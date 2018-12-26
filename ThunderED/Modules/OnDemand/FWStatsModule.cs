using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;

namespace ThunderED.Modules.OnDemand
{
    public static class FWStatsModule
    {
        private static bool _isPostRunning;

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
                int factionId;
                int factionCorpId;
                int oppFactionId;
                string factionName;
                string factionImage;

                switch (faction)
                {
                    case 'c':
                        factionId = 500001;
                        oppFactionId = 500004;
                        factionName = "Caldari";
                        factionImage = SettingsManager.Settings.Resources.ImgFactionCaldari;
                        factionCorpId = 1000180;
                        break;
                    case 'g':
                        factionId = 500004;
                        oppFactionId = 500001;
                        factionName = "Gallente";
                        factionImage = SettingsManager.Settings.Resources.ImgFactionGallente;
                        factionCorpId = 1000181;
                        break;
                    case 'a':
                        factionId = 500003;
                        oppFactionId = 500002;
                        factionName = "Amarr";
                        factionImage = SettingsManager.Settings.Resources.ImgFactionAmarr;
                        factionCorpId = 1000179;
                        break;
                    case 'm':
                        factionId = 500002;
                        oppFactionId = 500003;
                        factionName = "Minmatar";
                        factionImage = SettingsManager.Settings.Resources.ImgFactionMinmatar;
                        factionCorpId = 1000182;
                        break;
                    default:
                        return;
                }

                var stats = (await APIHelper.ESIAPI.GetFWStats("General")).FirstOrDefault(a => a.faction_id == factionId);

                var statOccupiedSystemsCount = stats.systems_controlled;
                var statKillsYesterday = stats.kills.yesterday;
                var statPilots = stats.pilots;

                var sysList = await APIHelper.ESIAPI.GetFWSystemStats("General");
                var statTotalSystemsCount = sysList.Count(a => a.owner_faction_id == factionId || a.owner_faction_id == oppFactionId);
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
                        var lpstockList = await LPStockModule.GetLPStockInformation(factionCorpId, 5);
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
                        prognose = LM.Get("fwstats_goselllp", avgRatio, factionCorpId, topItems);
                }

                var embed = new EmbedBuilder()
                    .WithTitle(LM.Get("fwstats_title", factionName))
                    .AddInlineField(LM.Get("fwstats_systems"), $"{statOccupiedSystemsCount}/{statTotalSystemsCount}")
                    .AddInlineField(LM.Get("fwstats_pilots"), LM.Get("fwstats_pilotsText", statPilots, statKillsYesterday))
                    .AddInlineField(LM.Get("fwstats_tip"), prognose)
                    .WithColor(0x00FF00);

                if (!string.IsNullOrEmpty(factionImage))
                    embed.WithThumbnailUrl(factionImage);

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
    }
}
