using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Helpers;
using ThunderED.Json;

namespace ThunderED.Modules
{
    public class NullCampaignModule: AppModuleBase
    {
        public override LogCat Category => LogCat.NullCampaign;
        private DateTime _nextNotificationCheck = DateTime.FromFileTime(0);
        private readonly ConcurrentDictionary<long, string> _tags = new ConcurrentDictionary<long, string>();

        public override async Task Initialize()
        {
            await LogHelper.LogModule("Initializing Null Campaigns module...", Category);
            var data = Settings.NullCampaignModule.GetEnabledGroups().ToDictionary(pair => pair.Key, pair => pair.Value.LocationEntities);
            await ParseMixedDataArray(data, MixedParseModeEnum.Location);
        }

        public override async Task Run(object prm)
        {

            if (IsRunning || !APIHelper.IsDiscordAvailable) return;
            if (TickManager.IsNoConnection || TickManager.IsESIUnreachable) return;
            IsRunning = true;

            await ProcessExistingCampaigns();

            try
            {
                if (DateTime.Now <= _nextNotificationCheck) return;
                _nextNotificationCheck = DateTime.Now.AddMinutes(Settings.NullCampaignModule.CheckIntervalInMinutes);

                var etag = _tags.GetOrNull(0);
                var result = await APIHelper.ESIAPI.GetNullCampaigns(Reason, etag);
                _tags.AddOrUpdateEx(0, result.Data.ETag);
                if(result.Data.IsNotModified) return;

                var allCampaigns = result.Result;
                if(allCampaigns == null) return;
                foreach (var (groupName, group) in Settings.NullCampaignModule.GetEnabledGroups())
                {
                    var systemIds = new List<long>();
                    var regionIds = GetParsedRegions(groupName) ?? new List<long>();
                    var constIds = GetParsedConstellations(groupName) ?? new List<long>();
                    var sysIds = GetParsedSolarSystems(groupName) ?? new List<long>();
                    foreach (var regionId in regionIds)
                        systemIds.AddRange((await DbHelper.GetSystemsByRegion(regionId))?.Select(a=> a.SolarSystemId));
                    foreach (var cId in constIds)
                        systemIds.AddRange((await DbHelper.GetSystemsByConstellation(cId))?.Select(a=> a.SolarSystemId));
                    systemIds.AddRange(sysIds);

                    var campaigns = allCampaigns.Where(a => systemIds.Contains(a.solar_system_id));
                    var existIds = await DbHelper.GetNullsecCampaignIdList(groupName);
                    campaigns = campaigns.Where(a => !existIds.Contains(a.campaign_id));

                    foreach (var campaign in campaigns)
                    {
                        if(await DbHelper.IsNullsecCampaignExists(groupName, campaign.campaign_id))
                            continue;

                        var startTime = campaign.Time;
                        var totalMinutes = DateTime.UtcNow >= startTime ? 0 : (int)(startTime - DateTime.UtcNow).TotalMinutes;
                        if(totalMinutes == 0) continue;

                        await DbHelper.UpdateNullCampaign(groupName, campaign.campaign_id, startTime, campaign);
                        if(group.ReportNewCampaign)
                            await PrepareMessage(campaign, group, LM.Get("NC_NewCampaign"), 0x00FF00);

                        await LogHelper.LogInfo($"Nullsec Campaign {campaign.campaign_id} has been registered! [{groupName} - {campaign.campaign_id}]", Category, true, false);
                    }

                }

            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                IsRunning = false;
            }
        }

        private bool _isCheckRunning;

        private DateTime _nextNotificationCheck2 = DateTime.MinValue;

        public async Task ProcessExistingCampaigns()
        {
            if (_isCheckRunning) return;
            try
            {
                _isCheckRunning = true;
                if (DateTime.Now <= _nextNotificationCheck2) return;
                _nextNotificationCheck2 = DateTime.Now.AddMinutes(1);

                await LogHelper.LogModule("Running NullCampaign module check...", Category);
                foreach (var pair in Settings.NullCampaignModule.GetEnabledGroups())
                {
                    foreach (var campaign in await DbHelper.GetNullCampaigns(pair.Key))
                    {
                        var startTime = campaign.Time;
                        //delete outdated campaigns
                        if (startTime <= DateTime.UtcNow)
                        {
                            if (!pair.Value.Announces.Any())
                                await PrepareMessage(campaign, pair.Value, LM.Get("NC_LessThanMinsLeft", TimeSpan.FromMinutes(0).ToFormattedString()), 0xFF0000);

                            await DbHelper.DeleteNullCampaign(pair.Key, campaign.campaign_id);
                            await LogHelper.LogInfo($"Nullsec Campaign {campaign.campaign_id} has been deleted...", Category, true, false);
                            continue;
                        }

                        if (pair.Value.Announces.Any())
                        {
                            var announceList = pair.Value.Announces.OrderBy(a => a).ToList();
                            var max = announceList.Max();
                            //not a notification time
                            var minutesLeft = (startTime - DateTime.UtcNow).TotalMinutes;
                            if (minutesLeft > max)
                                continue;


                            foreach (var announce in announceList.Where(a => campaign.LastAnnounce == 0 || a < campaign.LastAnnounce))
                            {
                                if (minutesLeft < announce)
                                {
                                    await PrepareMessage(campaign, pair.Value, LM.Get("NC_LessThanMinsLeft", TimeSpan.FromMinutes(minutesLeft).ToFormattedString()), 0xFF0000);
                                    //update last announce
                                    await DbHelper.UpdateNullCampaignAnnounce(pair.Key, campaign.campaign_id, announce);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
            finally
            {
                _isCheckRunning = false;
            }
        }

        private async Task PrepareMessage(JsonClasses.NullCampaignItem campaign, NullCampaignGroup @group, string message, uint color)
        {
            var system = await APIHelper.ESIAPI.GetSystemData(Reason, campaign.solar_system_id);
            var c = await APIHelper.ESIAPI.GetConstellationData(Reason, system.ConstellationId);
            var region = await APIHelper.ESIAPI.GetRegionData(Reason, c.RegionId);

            var defender = await APIHelper.ESIAPI.GetAllianceData(Reason, campaign.defender_id);
            await NotifyNullsecCampaign(campaign, message, region.RegionName, system.SolarSystemName, defender.name, group, color);
        }

        private async Task NotifyNullsecCampaign(JsonClasses.NullCampaignItem campaign, string message, string region, string system, string defender, NullCampaignGroup @group,
            uint color)
        {
            try
            {

                var embed = new EmbedBuilder()
                    .WithTitle(message)
                    .AddField(LM.Get("NC_StartTime"), LM.Get("NC_StartTimeText", $"{campaign.Time.ToString(Settings.Config.ShortTimeFormat)} ET", (campaign.Time - DateTimeOffset.UtcNow).ToFormattedString()) , true)
                    .AddField(LM.Get("NC_type"), campaign.event_type == "ihub_defense" ? "IHUB" : "TCU", true)
                    .AddField(LM.Get("NC_Score"), LM.Get("NC_ScoreText", campaign.attackers_score.ToPercent(), campaign.defender_score.ToPercent()), true) //"Attacker {0} vs Defender {1}" 
                    .AddField(LM.Get("NC_Location"), LM.Get("NC_LocationText", region, system), true) // "{0} / {1}"
                    .AddField(LM.Get("NC_Defender"), defender, true)
                    .WithTimestamp(campaign.Time)
                    .WithColor(color);
                    
                if (!string.IsNullOrEmpty(Settings.Resources.ImgEntosisAlert))
                    embed.WithThumbnailUrl(Settings.Resources.ImgEntosisAlert);

                var mention = group.Mentions.Any() ? string.Join(", ", group.Mentions) : group.DefaultMention;

                await APIHelper.DiscordAPI.SendMessageAsync(APIHelper.DiscordAPI.GetChannel(group.DiscordChannelId), mention, embed.Build()).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
            }
        }
    }
}
