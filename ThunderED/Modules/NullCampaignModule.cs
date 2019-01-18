using System;
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

        public override async Task Run(object prm)
        {

            if (IsRunning) return;
            IsRunning = true;

            await ProcessExistingCampaigns();

            try
            {
                if (DateTime.Now <= _nextNotificationCheck) return;
                _nextNotificationCheck = DateTime.Now.AddMinutes(Settings.NullCampaignModule.CheckIntervalInMinutes);

                var allCampaigns = await APIHelper.ESIAPI.GetNullCampaigns(Reason);
                foreach (var pair in Settings.NullCampaignModule.Groups)
                {
                    var groupName = pair.Key;
                    var group = pair.Value;
                    var systems = new List<JsonClasses.SystemName>();
                    
                    foreach (var regionId in @group.Regions) 
                        systems.AddRange(await SQLHelper.GetSystemsByRegion(regionId));
                    foreach (var cId in @group.Constellations) 
                        systems.AddRange(await SQLHelper.GetSystemsByConstellation(cId));

                    var systemIds = systems.Select(a => a.system_id);
                    var campaigns = allCampaigns.Where(a => systemIds.Contains(a.solar_system_id));
                    var existIds = await SQLHelper.GetNullsecCampaignIdList(groupName);
                    campaigns = campaigns.Where(a => !existIds.Contains(a.campaign_id));

                    foreach (var campaign in campaigns)
                    {
                        if(await SQLHelper.IsNullsecCampaignExists(groupName, campaign.campaign_id))
                            continue;

                        var startTime = campaign.Time;
                        var totalMinutes = DateTime.UtcNow >= startTime ? 0 : (int)(startTime - DateTime.UtcNow).TotalMinutes;
                        if(totalMinutes == 0) continue;

                        await SQLHelper.UpdateNullCampaign(groupName, campaign.campaign_id, startTime, campaign.ToJson());
                        if(group.ReportNewCampaign)
                            await PrepareMessage(campaign, pair.Value, LM.Get("NC_NewCampaign"), 0x00FF00);

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
                foreach (var pair in Settings.NullCampaignModule.Groups)
                {
                    foreach (var campaign in await SQLHelper.GetNullCampaigns(pair.Key))
                    {
                        var startTime = campaign.Time;
                        //delete outdated campaigns
                        if (startTime <= DateTime.UtcNow)
                        {
                            if (!pair.Value.Announces.Any())
                                await PrepareMessage(campaign, pair.Value, LM.Get("NC_LessThanMinsLeft", TimeSpan.FromMinutes(0).ToFormattedString()), 0xFF0000);

                            await SQLHelper.DeleteNullCampaign(pair.Key, campaign.campaign_id);
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
                                    await SQLHelper.UpdateNullCampaignAnnounce(pair.Key, campaign.campaign_id, announce);
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
            var c = await APIHelper.ESIAPI.GetConstellationData(Reason, system.constellation_id);
            var region = await APIHelper.ESIAPI.GetRegionData(Reason, c.region_id);

            var defender = await APIHelper.ESIAPI.GetAllianceData(Reason, campaign.defender_id);
            await NotifyNullsecCampaign(campaign, message, region.name, system.name, defender.name, group, color);
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
