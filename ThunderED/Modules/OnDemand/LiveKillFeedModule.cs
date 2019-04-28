using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using ThunderED.Classes;
using ThunderED.Classes.Entities;
using ThunderED.Helpers;
using ThunderED.Json.ZKill;
using ThunderED.Modules.Sub;

namespace ThunderED.Modules.OnDemand
{
    public partial class LiveKillFeedModule: AppModuleBase
    {
        private long _lastPosted;
        public override LogCat Category => LogCat.KillFeed;

        public LiveKillFeedModule()
        {
            LogHelper.LogModule("Inititalizing LiveKillFeed module...", Category).GetAwaiter().GetResult();
            ZKillLiveFeedModule.Queryables.Add(ProcessKill);
        }

        private async Task ProcessKill(JsonZKill.Killmail kill)
        {
            try
            {
                if (_lastPosted == kill.killmail_id) return;

                if(Settings.ZKBSettingsModule.AvoidDupesAcrossAllFeeds && RadiusKillFeedModule.UpdateDistinctEntriesExternal(kill.killmail_id))
                    return;

                var postedGlobalBigKill = false;
                var bigKillGlobalValue = SettingsManager.Settings.LiveKillFeedModule.BigKill;
                var bigKillGlobalChan = SettingsManager.Settings.LiveKillFeedModule.BigKillChannel;
                var isNPCKill = kill.zkb.npc;

                var km = new KillDataEntry();

                foreach (var groupPair in Settings.LiveKillFeedModule.GroupsConfig)
                {
                    var group = groupPair.Value;
                    if ((!group.FeedPveKills && isNPCKill) || (!group.FeedPvpKills && !isNPCKill)) continue;

                    var minimumValue = group.MinimumValue;
                    var minimumLossValue = group.MinimumLossValue;
                    var allianceIdList = group.AllianceID;
                    var corpIdList = group.CorpID;
                    var bigKillValue = group.BigKillValue;
                    var c = group.DiscordChannel;
                    var sendBigToGeneral = group.BigKillSendToGeneralToo;
                    var bigKillChannel = group.BigKillChannel;
                    var discordGroupName = groupPair.Key;
                    var isUrlOnly = group.FeedUrlsOnly;

                    if (c == 0)
                    {
                        await LogHelper.LogWarning($"Group {groupPair.Key} has no 'discordChannel' specified! Kills will be skipped.", Category);
                        continue;
                    }

                    var value = kill.zkb.totalValue;


                    if (bigKillGlobalChan != 0 && bigKillGlobalValue != 0 && value >= bigKillGlobalValue && !postedGlobalBigKill)
                    {
                        postedGlobalBigKill = true;

                        if (isUrlOnly)
                            await APIHelper.DiscordAPI.SendMessageAsync(bigKillGlobalChan, kill.zkb.url);
                        else
                        {                            
                            if (await km.Refresh(Reason, kill) && !await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, km.dic, bigKillGlobalChan, groupPair.Value.ShowGroupName ? discordGroupName : " "))
                            {
                                await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillGlobalChan, new Color(0xFA2FF4), km, null);
                            }
                        }

                        await LogHelper.LogInfo($"Posting Global Big Kill: {kill.killmail_id}  Value: {value:n0} ISK", Category);
                    }

                    if (!allianceIdList.Any() && !corpIdList.Any())
                    {
                        if (value >= minimumValue)
                        {
                            if (isUrlOnly)
                                await APIHelper.DiscordAPI.SendMessageAsync(c, kill.zkb.url);
                            else if (await km.Refresh(Reason, kill) && !await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailGeneral, km.dic, c, groupPair.Value.ShowGroupName ? discordGroupName : " "))
                            {
                                await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00FF00), km, null);
                            }

                            await LogHelper.LogInfo($"Posting Global Kills: {kill.killmail_id}  Value: {value:n0} ISK", Category);
                        }
                    }
                    else
                    {
                        //ally & corp 

                        //Losses
                        //Big
                        if (bigKillChannel != 0 && bigKillValue != 0 && value >= bigKillValue)
                        {
                            if (kill.victim.alliance_id != 0 && allianceIdList.Contains(kill.victim.alliance_id) || corpIdList.Contains(kill.victim.corporation_id))
                            {
                                if (isUrlOnly)
                                {
                                    await APIHelper.DiscordAPI.SendMessageAsync(bigKillChannel, kill.zkb.url);
                                    if (sendBigToGeneral && c != bigKillChannel)
                                        await APIHelper.DiscordAPI.SendMessageAsync(c, kill.zkb.url);
                                }
                                else if (await km.Refresh(Reason, kill))
                                {
                                    km.dic["{isLoss}"] = "true";
                                    try
                                    {
                                        if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, km.dic, bigKillChannel, 
                                            groupPair.Value.ShowGroupName ? discordGroupName : " "))
                                        {
                                            await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillChannel, new Color(0xD00000), km, null,
                                                groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                            if (sendBigToGeneral && c != bigKillChannel)
                                                if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, km.dic, c, 
                                                    groupPair.Value.ShowGroupName ? discordGroupName : " "))
                                                    await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0xD00000), km, null,
                                                        groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                        }
                                    }
                                    finally
                                    {
                                        km.dic.Remove("{isLoss}");
                                    }
                                }

                                await LogHelper.LogInfo($"Posting     Big Loss: {kill.killmail_id}  Value: {value:n0} ISK", Category);
                                continue;
                            }
                        }

                        //Common
                        if (minimumLossValue == 0 || minimumLossValue <= value)
                        {
                            if (kill.victim.alliance_id != 0 && allianceIdList.Contains(kill.victim.alliance_id) || corpIdList.Contains(kill.victim.corporation_id))
                            {
                                if (isUrlOnly)
                                    await APIHelper.DiscordAPI.SendMessageAsync(c, kill.zkb.url);
                                else if (await km.Refresh(Reason, kill))
                                {
                                    km.dic["{isLoss}"] = "true";
                                    try
                                    {
                                        if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailGeneral, km.dic, c, 
                                            groupPair.Value.ShowGroupName ? discordGroupName : " "))
                                        {
                                            await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0xFF0000), km, null,
                                                groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                        }
                                    }
                                    finally
                                    {
                                        km.dic.Remove("{isLoss}");
                                    }
                                }

                                await LogHelper.LogInfo($"Posting         Loss: {kill.killmail_id}  Value: {value:n0} ISK", Category);

                                continue;
                            }
                        }

                        //Kills
                        foreach (var attacker in kill.attackers.ToList())
                        {
                            if (bigKillChannel != 0 && bigKillValue != 0 && value >= bigKillValue && !isNPCKill)
                            {
                                if ((attacker.alliance_id != 0 && allianceIdList.Contains(attacker.alliance_id)) ||
                                    (!allianceIdList.Any() && corpIdList.Contains(attacker.corporation_id)))
                                {
                                    if (isUrlOnly)
                                    {
                                        await APIHelper.DiscordAPI.SendMessageAsync(bigKillChannel, kill.zkb.url);
                                        if (sendBigToGeneral && c != bigKillChannel)
                                            await APIHelper.DiscordAPI.SendMessageAsync(c, kill.zkb.url);
                                    }
                                    else if (await km.Refresh(Reason, kill))
                                    {
                                        km.dic["{isLoss}"] = "false";
                                        try
                                        {
                                            if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, km.dic, bigKillChannel, 
                                                groupPair.Value.ShowGroupName ? discordGroupName : " "))
                                            {
                                                await APIHelper.DiscordAPI.SendEmbedKillMessage(bigKillChannel, new Color(0x00D000), km, null,
                                                    groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                                if (sendBigToGeneral && c != bigKillChannel)
                                                {
                                                    if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailBig, km.dic, c, 
                                                        groupPair.Value.ShowGroupName ? discordGroupName : " "))
                                                        await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00D000), km, null,
                                                            groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                                }

                                                await LogHelper.LogInfo($"Posting     Big Kill: {kill.killmail_id}  Value: {value:#,##0} ISK", Category);
                                            }
                                        }
                                        finally
                                        {
                                            km.dic.Remove("{isLoss}");
                                        }
                                    }

                                    break;
                                }
                            }
                            else if (!isNPCKill && attacker.alliance_id != 0 && allianceIdList.Any() && allianceIdList.Contains(attacker.alliance_id) ||
                                     !isNPCKill && !allianceIdList.Any() && corpIdList.Contains(attacker.corporation_id))
                            {
                                if (isUrlOnly)
                                    await APIHelper.DiscordAPI.SendMessageAsync(c, kill.zkb.url);
                                else if (await km.Refresh(Reason, kill))
                                {
                                    km.dic["{isLoss}"] = "false";
                                    try
                                    {
                                        if (!await TemplateHelper.PostTemplatedMessage(MessageTemplateType.KillMailGeneral, km.dic, c, 
                                            groupPair.Value.ShowGroupName ? discordGroupName : " "))
                                        {
                                            await APIHelper.DiscordAPI.SendEmbedKillMessage(c, new Color(0x00FF00), km, null,
                                                groupPair.Value.ShowGroupName ? discordGroupName : " ");
                                        }
                                    }
                                    finally
                                    {
                                        km.dic.Remove("{isLoss}");
                                    }
                                }

                                await LogHelper.LogInfo($"Posting         Kill: {kill.killmail_id}  Value: {value:#,##0} ISK", Category);
                                break;
                            }
                        }
                    }
                }

                _lastPosted = kill.killmail_id;
            }
            catch (Exception ex)
            {
                await LogHelper.LogEx(ex.Message, ex, Category);
                await LogHelper.LogWarning($"Error processing kill ID {kill?.killmail_id} ! Msg: {ex.Message}", Category);
            }
        }
    }
}
